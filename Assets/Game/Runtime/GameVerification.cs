#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zombs
{
    public sealed partial class GameSession
    {
        private enum Checkpoint
        {
            Title, Preview, Wood, ToStone, Stone, Build, FirstNight, ToIron, Iron, Return,
            Smelt, Assemble, Upgrade, Miner, SecondNight, Outage, Refill, BossDefense, Finished
        }

        private sealed class Verification
        {
            public string Directory;
            public float Speed, StageAge, ServiceClock;
            public Checkpoint Stage;
            public int Waypoint;
            public bool Captured, PoisonSeen, FirstBoss;
        }

        private Verification verification;
        private static readonly Vector2 Site = new(-3, 0);
        private static readonly Vector2[] DefenseSites = { new(-5, 2), new(-1, 2), new(-3, -2), new(-5, -2) };
        private static readonly Vector2[] OreRoute = { new(1, 1.15f), new(7, 2), new(9, -4), new(12, -7) };
        private static readonly Vector2[] HomeRoute = { new(9, -4), new(7, 2), new(1, 1.15f), Vector2.zero };

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-zombs-playthrough");
            if (index < 0 || index + 1 >= args.Length) return;
            var output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            bool firstBoss = args.Contains("-zombs-first-boss");
            verification = new Verification
            {
                Directory = output, Speed = args.Contains("-zombs-fast") || firstBoss ? 20 : 1, FirstBoss = firstBoss
            };
            VerificationLog($"Started packaged playthrough at {verification.Speed:0}x.");
        }

        private bool UpdateVerification()
        {
            if (verification == null) return false;
            float frame = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float elapsed = frame * verification.Speed;
            verification.StageAge += frame;
            Vector2 move = Vector2.zero;
            bool attack = false;
            if (verification.Stage != Checkpoint.Finished &&
                (run.Defeated || run.Dead ||
                 verification.Stage != Checkpoint.BossDefense && verification.StageAge * verification.Speed > 310 ||
                 verification.Stage == Checkpoint.BossDefense && verification.StageAge > 250))
                FinishVerification(false, $"Stopped at {verification.Stage}, player {run.Player}, wave {run.Wave}, health {run.Health:0}, fuel {run.Generator?.Fuel:0}.");

            switch (verification.Stage)
            {
                case Checkpoint.Title:
                    if (!CaptureAfterDelay(.7f, "01-title")) return true;
                    intro = false;
                    profile.nickname = "Player";
                    Attach(new RunState(catalog, 5238));
                    SelectBuild(BuildingKind.Generator);
                    cursor = Site;
                    AdvanceVerification(Checkpoint.Preview, "New run, seed 5238.");
                    return true;
                case Checkpoint.Preview:
                    if (!CaptureAfterDelay(.7f, "02-generator-preview")) return true;
                    WorldClick();
                    if (run.Generator == null)
                    {
                        FinishVerification(false, $"Generator placement failed at {Site}.");
                        break;
                    }
                    AdvanceVerification(Checkpoint.Wood, "Placed a fueled generator through the placement tool.");
                    break;
                case Checkpoint.Wood:
                    cursor = run.Nodes[0].Position;
                    attack = true;
                    if (run.Resources.wood >= 150) AdvanceVerification(Checkpoint.ToStone, $"Gathered {run.Resources.wood:0} wood.");
                    break;
                case Checkpoint.ToStone:
                    if (GuideTo(new Vector2(0, 4.15f), elapsed, out move))
                        AdvanceVerification(Checkpoint.Stone, "Reached the starter stone.");
                    break;
                case Checkpoint.Stone:
                    cursor = run.Nodes[1].Position;
                    attack = true;
                    if (run.Resources.stone >= 100) AdvanceVerification(Checkpoint.Build, $"Gathered {run.Resources.stone:0} stone.");
                    break;
                case Checkpoint.Build:
                    if (!GuideTo(Vector2.zero, elapsed, out move)) break;
                    foreach (var position in DefenseSites)
                        run.Place(BuildingKind.Arrow, position, 0, out _);
                    if (run.Count(BuildingKind.Arrow) < DefenseSites.Length)
                    {
                        FinishVerification(false, "Opening tower placements did not fit.");
                        break;
                    }
                    run.Refuel(run.Generator, ResourceKind.Wood);
                    run.Refuel(run.Generator, ResourceKind.Wood);
                    selected = run.Generator;
                    SaveVerificationFrame("03-generator-controls");
                    AdvanceVerification(Checkpoint.FirstNight, $"Built four towers and refueled, with {run.Generator.Fuel:0}s fuel before night.");
                    break;
                case Checkpoint.FirstNight:
                    if (!verification.Captured && run.Enemies.Any(enemy =>
                        enemy.Health > 0 && Mathf.Abs(enemy.Position.y - run.Player.y) < 9 &&
                        Mathf.Abs(enemy.Position.x - run.Player.x) < 13))
                    {
                        selected = null;
                        verification.Captured = true;
                        SaveVerificationFrame("04-first-wave");
                    }
                    if (run.Wave == 1 && !run.Night)
                    {
                        run.Place(BuildingKind.Smelter, new Vector2(-1, -2), 0, out _);
                        AdvanceVerification(Checkpoint.ToIron, $"Survived night one, score {run.Score}.");
                    }
                    break;
                case Checkpoint.ToIron:
                    if (FollowRoute(OreRoute, elapsed, out move))
                        AdvanceVerification(Checkpoint.Iron, "Reached the iron vein.");
                    break;
                case Checkpoint.Iron:
                    var ore = run.Nodes.Where(node => node.Kind == ResourceKind.Iron && !node.Depleted)
                        .OrderBy(node => (node.Position - run.Player).sqrMagnitude).First();
                    cursor = ore.Position;
                    move = (cursor - run.Player).normalized;
                    attack = true;
                    if (run.Breath <= 0 && !verification.PoisonSeen)
                    {
                        verification.PoisonSeen = true;
                        SaveVerificationFrame("05-poison-exposure");
                    }
                    if (run.Resources.iron >= 24)
                        AdvanceVerification(Checkpoint.Return, $"Mined {run.Resources.iron:0} ore. Poison observed {verification.PoisonSeen}.");
                    break;
                case Checkpoint.Return:
                    if (FollowRoute(HomeRoute, elapsed, out move))
                    {
                        if (!run.CanBreathe)
                        {
                            FinishVerification(false, "Returning to the generator did not restore breathable air.");
                            break;
                        }
                        AdvanceVerification(Checkpoint.Smelt, "Returned to breathable air.");
                    }
                    break;
                case Checkpoint.Smelt:
                    selected = run.At(new Vector2(-1, -2));
                    if (selected == null)
                    {
                        FinishVerification(false, "Smelter placement failed.");
                        break;
                    }
                    run.Collect(selected);
                    run.QueueProduction(selected);
                    if (run.Resources.plate >= 12)
                    {
                        run.Place(BuildingKind.Assembler, new Vector2(-1, -4), 0, out _);
                        AdvanceVerification(Checkpoint.Assemble, "Smelted and collected 12 plates, then built an assembler.");
                    }
                    break;
                case Checkpoint.Assemble:
                    selected = run.At(new Vector2(-1, -4));
                    if (selected == null)
                    {
                        FinishVerification(false, "Assembler placement failed.");
                        break;
                    }
                    run.Collect(selected);
                    run.QueueProduction(selected);
                    if (!verification.Captured && selected.Queued > 0 && run.Resources.parts >= 1)
                    {
                        verification.Captured = true;
                        SaveVerificationFrame("06-assembly");
                    }
                    if (run.Resources.parts >= 9)
                        AdvanceVerification(Checkpoint.Upgrade, $"Collected {run.Resources.parts:0} assembled parts.");
                    break;
                case Checkpoint.Upgrade:
                    if (!verification.Captured)
                    {
                        if (run.Upgrade(run.Generator, false) != 1)
                        {
                            FinishVerification(false, "Could not upgrade the generator with assembled parts.");
                            break;
                        }
                        selected = run.Generator;
                        verification.Captured = true;
                        SaveVerificationFrame("07-upgraded-generator");
                        break;
                    }
                    if (!run.Place(BuildingKind.Miner, new Vector2(1, 3), 0, out var result))
                    {
                        FinishVerification(false, $"Could not place unlocked Miner: {result}.");
                        break;
                    }
                    selected = run.At(new Vector2(1, 3));
                    run.Refuel(selected, ResourceKind.Wood);
                    AdvanceVerification(Checkpoint.Miner, $"Upgraded to radius {run.OxygenRadius:0}, then fueled a miner.");
                    break;
                case Checkpoint.Miner:
                    if (!verification.Captured && selected.Stored >= .75f)
                    {
                        verification.Captured = true;
                        SaveVerificationFrame("08-miner");
                        break;
                    }
                    if (selected.Stored >= 1)
                    {
                        run.Collect(selected);
                        selected = null;
                        AdvanceVerification(Checkpoint.SecondNight, "Collected mined stone.");
                    }
                    break;
                case Checkpoint.SecondNight:
                    if (run.Wave >= 2 && !run.Night)
                        AdvanceVerification(Checkpoint.Outage, $"Survived night two, score {run.Score}.");
                    break;
                case Checkpoint.Outage:
                    if (!run.GeneratorOnline && !run.CanBreathe)
                    {
                        selected = run.Generator;
                        if (!CaptureAfterDelay(.55f, "09-fuel-outage")) break;
                        if (!run.Refuel(run.Generator, ResourceKind.Wood))
                        {
                            FinishVerification(false, "Could not restart an empty generator with wood.");
                            break;
                        }
                        AdvanceVerification(Checkpoint.Refill, "Restored oxygen from an empty tank.");
                    }
                    break;
                case Checkpoint.Refill:
                    if (!run.CanBreathe || !run.GeneratorOnline)
                        FinishVerification(false, "Fuel did not restore the oxygen zone.");
                    else if (run.Breath >= catalog.breathSeconds)
                    {
                        if (verification.FirstBoss) SetUpBossVerification();
                        else FinishVerification(true, "Gathered and defended twice, processed parts, expanded oxygen, mined automatically, and recovered from outage.");
                    }
                    break;
                case Checkpoint.BossDefense:
                    if (run.Wave == catalog.firstBossWave - 1 && !run.Night && verification.Speed > 1)
                    {
                        verification.Speed = 1;
                        VerificationLog("Switched to normal speed on the day before the first boss.");
                    }
                    if (!verification.Captured && run.Enemies.Any(enemy =>
                        enemy.Boss && enemy.Health > 0 && (enemy.Position - run.Player).sqrMagnitude < 10 * 10))
                    {
                        verification.Captured = true;
                        SaveVerificationFrame("10-first-boss");
                    }
                    if (run.Wave == catalog.firstBossWave && !run.Night)
                        FinishVerification(run.Resources.tokens >= catalog.bossTokens,
                            $"Finished the first boss night with {run.Resources.tokens} boss tokens and score {run.Score}.");
                    break;
                case Checkpoint.Finished:
                    if (verification.StageAge > 1) Application.Quit();
                    return true;
            }
            if (verification.Stage != Checkpoint.Finished)
            {
                StepGame(elapsed, move, attack);
                ServiceVerificationBase(elapsed);
            }
            return true;
        }

        private bool GuideTo(Vector2 target, float elapsed, out Vector2 move)
        {
            Vector2 delta = target - run.Player;
            move = Vector2.ClampMagnitude(delta / Mathf.Max(RunState.TickSeconds, elapsed) / catalog.playerSpeed, 1);
            return delta.sqrMagnitude <= .10f * .10f;
        }

        private bool FollowRoute(Vector2[] route, float elapsed, out Vector2 move)
        {
            if (GuideTo(route[verification.Waypoint], elapsed, out move)) verification.Waypoint++;
            return verification.Waypoint == route.Length;
        }

        private void ServiceVerificationBase(float elapsed)
        {
            if (run.Generator == null || verification.Stage < Checkpoint.FirstNight) return;
            verification.ServiceClock += elapsed;
            if (verification.ServiceClock < .5f) return;
            verification.ServiceClock = 0;
            if ((verification.Stage < Checkpoint.Outage || verification.Stage == Checkpoint.BossDefense) && run.Generator.Fuel < 80)
                run.Refuel(run.Generator, verification.Stage == Checkpoint.BossDefense ? ResourceKind.Coal : ResourceKind.Wood);
            foreach (var building in run.Buildings)
                if (building.Health < building.MaximumHealth * .65f) run.Repair(building);
        }

        private void SetUpBossVerification()
        {
            Attach(new RunState(catalog, 5238, new Price(wood: 400, stone: 400, coal: 70, parts: 80)));
            if (!run.Place(BuildingKind.Generator, Site, 0, out _))
            {
                FinishVerification(false, "Could not place the generator for the boss scenario.");
                return;
            }
            for (int tier = 1; tier < 4; tier++) run.Upgrade(run.Generator, false);
            foreach (var position in DefenseSites)
                run.Place(BuildingKind.Arrow, position, 0, out _);
            run.Place(BuildingKind.Bomb, new Vector2(-3, 2), 0, out _);
            run.Place(BuildingKind.Bomb, new Vector2(-1, -2), 0, out _);
            foreach (var building in run.Buildings.Where(building => building.Kind != BuildingKind.Generator))
                for (int tier = 1; tier < 3; tier++) run.Upgrade(building, false);
            AdvanceVerification(Checkpoint.BossDefense, "Opening and fuel recovery passed. Started a separate first-boss scenario with supplied build materials.");
        }

        private bool CaptureAfterDelay(float delay, string filename)
        {
            if (verification.Captured) return true;
            if (verification.StageAge < delay) return false;
            SaveVerificationFrame(filename);
            verification.Captured = true;
            return false;
        }

        private void AdvanceVerification(Checkpoint checkpoint, string message)
        {
            verification.Stage = checkpoint;
            verification.StageAge = 0;
            verification.Waypoint = 0;
            verification.Captured = false;
            VerificationLog(message);
        }

        private void FinishVerification(bool success, string message)
        {
            AdvanceVerification(Checkpoint.Finished, $"{(success ? "PASS" : "FAIL")}: {message}");
            File.WriteAllText(Path.Combine(verification.Directory, "result.txt"), success ? "PASS" : "FAIL");
        }

        private void VerificationLog(string message) =>
            File.AppendAllText(Path.Combine(verification.Directory, "playthrough.log"), $"{Time.unscaledTime:0.00} {message}{Environment.NewLine}");

        private void SaveVerificationFrame(string name) =>
            ScreenCapture.CaptureScreenshot(Path.Combine(verification.Directory, name + ".png"));
    }
}
#endif
