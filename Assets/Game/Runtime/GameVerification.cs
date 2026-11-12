#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zombs
{
    public sealed partial class GameSession
    {
        private enum Checkpoint { Title, Wood, ToStone, Stone, ToBase, Placement, Defense, Loss, Recovery, Fresh, Finished }
        private sealed class Verification
        {
            public string Directory;
            public float Speed, StageAge, ServiceClock;
            public Checkpoint Stage;
            public int LastLoggedWave, Deaths;
            public bool BossSeen, BossCaptured, BossReward, FirstWaveCaptured, RealTimeBoss;
        }

        private Verification verification;
        private static readonly (BuildingKind Kind, int X, int Y)[] Blueprint =
        {
            (BuildingKind.Generator, -4, 0),
            (BuildingKind.Bomb, -6, 0), (BuildingKind.Bomb, -2, 0),
            (BuildingKind.Bomb, -4, 2), (BuildingKind.Bomb, -4, -2),
            (BuildingKind.Bomb, -6, 2), (BuildingKind.Bomb, -6, -2),
            (BuildingKind.Arrow, -2, 2), (BuildingKind.Arrow, -2, -2),
            (BuildingKind.Arrow, -8, 0), (BuildingKind.Arrow, -8, -2),
            (BuildingKind.Arrow, -6, 4), (BuildingKind.Arrow, -6, -4),
            (BuildingKind.Smelter, -4, 4), (BuildingKind.Smelter, -4, -4),
            (BuildingKind.Smelter, -2, 4), (BuildingKind.Smelter, -2, -4),
            (BuildingKind.Smelter, 0, 2), (BuildingKind.Smelter, 0, 4),
            (BuildingKind.Smelter, -8, 2), (BuildingKind.Smelter, -8, -4),
            (BuildingKind.Miner, -2, 6)
        };

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-zombs-playthrough");
            if (index < 0 || index + 1 >= args.Length) return;
            var output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            verification = new Verification
            {
                Directory = output,
                Speed = args.Contains("-zombs-fast") || args.Contains("-zombs-first-boss") ? 30 : 1,
                RealTimeBoss = args.Contains("-zombs-first-boss")
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
            if (run.Defeated && verification.Stage < Checkpoint.Loss)
            {
                SaveVerificationFrame("unexpected-loss");
                FinishVerification(false, $"Generator lost prematurely at wave {run.Wave}.");
            }
            switch (verification.Stage)
            {
                case Checkpoint.Title:
                    if (verification.StageAge < 1.5f) return true;
                    if (verification.StageAge - frame < 1.5f) SaveVerificationFrame("01-title");
                    if (verification.StageAge < 1.75f) return true;
                    intro = false;
                    profile.nickname = "Player";
                    Attach(new RunState(catalog, 5238));
                    AdvanceVerification(Checkpoint.Wood, "New empty run, seed 5238.");
                    return true;
                case Checkpoint.Wood:
                    cursor = run.Nodes[0].Position;
                    attack = true;
                    if (run.Resources.wood >= 245)
                        AdvanceVerification(Checkpoint.ToStone, $"Gathered {run.Resources.wood:0} wood with the pickaxe.");
                    break;
                case Checkpoint.ToStone:
                    if (run.Player.y < 4.4f) move = Vector2.up;
                    else AdvanceVerification(Checkpoint.Stone, "Reached the stone node.");
                    break;
                case Checkpoint.Stone:
                    cursor = run.Nodes[1].Position;
                    attack = true;
                    if (run.Resources.stone >= 245)
                    {
                        SaveVerificationFrame("02-gathering");
                        AdvanceVerification(Checkpoint.ToBase, $"Gathered {run.Resources.stone:0} stone with the pickaxe.");
                    }
                    break;
                case Checkpoint.ToBase:
                    if (run.Player.y > .12f)
                    {
                        move = Vector2.down * Mathf.Min(1, run.Player.y / (elapsed * catalog.playerSpeed));
                        break;
                    }
                    SelectBuild(BuildingKind.Generator);
                    cursor = new Vector2(-4, 0);
                    AdvanceVerification(Checkpoint.Placement, "Generator placement preview.");
                    break;
                case Checkpoint.Placement:
                    if (verification.StageAge < .75f) return true;
                    if (verification.StageAge - frame < .75f) SaveVerificationFrame("03-placement-preview");
                    if (verification.StageAge < .95f) return true;
                    ResetSelection();
                    BuildBlueprint();
                    if (run.Generator == null || run.Count(BuildingKind.Bomb) < 4 || run.Count(BuildingKind.Smelter) < 4)
                    {
                        FinishVerification(false, "Could not place the opening base.");
                        break;
                    }
                    run.Buy(ItemKind.Carl);
                    AdvanceVerification(Checkpoint.Defense, $"Placed {run.Buildings.Count} structures with earned resources.");
                    break;
                case Checkpoint.Defense:
                    if (verification.RealTimeBoss && verification.Speed > 1 && run.Wave == catalog.firstBossWave - 1 && !run.Night)
                    {
                        verification.Speed = 1;
                        VerificationLog("Switched to normal speed before the first boss night.");
                    }
                    if (run.Wave > verification.LastLoggedWave)
                    {
                        verification.LastLoggedWave = run.Wave;
                        VerificationLog($"Wave {run.Wave}, tier {run.Generator.Tier}, {run.Buildings.Count} buildings, gold {run.Resources.gold:0}.");
                    }
                    if (run.Wave == 1 && run.Enemies.Count > 0 && !verification.FirstWaveCaptured)
                    {
                        verification.FirstWaveCaptured = true;
                        SaveVerificationFrame("04-first-wave");
                    }
                    if (run.Wave == catalog.firstBossWave && !verification.BossSeen && run.Enemies.Any(enemy => enemy.Boss))
                    {
                        verification.BossSeen = true;
                        VerificationLog("Observed first boss on the field.");
                    }
                    if (verification.BossSeen && !verification.BossCaptured &&
                        run.Enemies.Any(enemy => enemy.Boss && enemy.Health > 0 && OnScreen(enemy.Position)))
                    {
                        verification.BossCaptured = true;
                        SaveVerificationFrame("05-boss-wave");
                    }
                    if (verification.BossSeen && !verification.BossReward && run.Resources.tokens > 0)
                    {
                        verification.BossReward = true;
                        VerificationLog($"Earned {run.Resources.tokens} boss tokens.");
                    }
                    if (verification.BossSeen && !run.Night && run.Wave == catalog.firstBossWave)
                    {
                        if (!verification.BossReward)
                        {
                            FinishVerification(false, "Survived the first boss wave without a boss reward.");
                            break;
                        }
                        AdvanceVerification(Checkpoint.Loss, $"Survived the first boss wave with score {run.Score}.");
                        break;
                    }
                    if (run.Dead)
                    {
                        verification.Deaths++;
                        run.Respawn();
                    }
                    verification.ServiceClock += elapsed;
                    if (verification.ServiceClock >= 1.5f)
                    {
                        verification.ServiceClock = 0;
                        BuildBlueprint();
                        ServiceBase();
                    }
                    break;
                case Checkpoint.Loss:
                    foreach (var building in run.Buildings.ToArray())
                        if (building != run.Generator) run.Sell(building);
                    if (run.Dead && !run.Defeated)
                    {
                        if (verification.Deaths == 0)
                        {
                            verification.Deaths++;
                            SaveVerificationFrame("06-player-death");
                            VerificationLog("Player death left the stash standing.");
                        }
                    }
                    if (run.Defeated)
                    {
                        if (verification.StageAge < 1.5f) return true;
                        SaveVerificationFrame("07-stash-loss");
                        AdvanceVerification(Checkpoint.Recovery, $"Generator loss recorded at wave {run.Wave}.");
                    }
                    break;
                case Checkpoint.Recovery:
                    if (verification.StageAge < .75f) return true;
                    RestartAfterLoss();
                    if (run.Dead || run.Defeated || run.Generator != null || run.Wave != 0 || run.ItemTier(ItemKind.Pickaxe) != 1)
                    {
                        FinishVerification(false, "Respawn after stash loss did not reset the base.");
                        break;
                    }
                    AdvanceVerification(Checkpoint.Fresh, "Respawn restored player control with a clean base.");
                    break;
                case Checkpoint.Fresh:
                    if (verification.StageAge < .8f) return true;
                    Attach(new RunState(catalog, 5239));
                    if (run.Resources.wood != 0 || run.Resources.stone != 0 || run.Resources.gold != 0)
                    {
                        FinishVerification(false, "The fresh run inherited an old balance.");
                        break;
                    }
                    FinishVerification(true, "Passed gathering, construction, waves, boss reward, stash loss, respawn and clean restart.");
                    break;
                case Checkpoint.Finished:
                    if (verification.StageAge > 1.4f) Application.Quit();
                    return true;
            }
            if (!run.Defeated && verification.Stage != Checkpoint.Finished) StepGame(elapsed, move, attack);
            return true;
        }

        private void BuildBlueprint()
        {
            foreach (var entry in Blueprint)
            {
                Vector2 point = new(entry.X, entry.Y);
                if (run.At(point) == null) run.Place(entry.Kind, point, 0, out _);
            }
        }

        private void ServiceBase()
        {
            if (run.Generator.Tier < 5) run.Upgrade(run.Generator, false);
            if (run.ItemTier(ItemKind.Bow) == 0 && run.Resources.gold >= 100)
                run.Buy(ItemKind.Bow);
            foreach (var building in run.Buildings)
            {
                if (building.Health < building.MaximumHealth * .42f) run.Repair(building);
                if (building.Kind == BuildingKind.Miner)
                {
                    if (building.Fuel < 100) run.Deposit(building);
                    if (building.Stored >= 16) run.Collect(building);
                }
                if (building != run.Generator && building.Kind != BuildingKind.Miner &&
                    (SpriteArt.IsTower(building.Kind) || building.Kind == BuildingKind.Smelter) &&
                    run.Resources.wood >= 22 && run.Resources.stone >= 22)
                    run.Upgrade(building, false);
            }
            if (run.Health < catalog.playerHealth * .4f) run.HealPlayer();
        }

        private void AdvanceVerification(Checkpoint checkpoint, string message)
        {
            verification.Stage = checkpoint;
            verification.StageAge = 0;
            VerificationLog(message);
        }

        private void FinishVerification(bool success, string message)
        {
            AdvanceVerification(Checkpoint.Finished, $"{(success ? "PASS" : "FAIL")}: {message}");
            File.WriteAllText(Path.Combine(verification.Directory, "result.txt"), success ? "PASS" : "FAIL");
        }

        private void VerificationLog(string message) =>
            File.AppendAllText(Path.Combine(verification.Directory, "playthrough.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");

        private void SaveVerificationFrame(string name) =>
            ScreenCapture.CaptureScreenshot(Path.Combine(verification.Directory, name + ".png"));

        private bool OnScreen(Vector2 point)
        {
            Vector3 screen = sceneCamera.WorldToViewportPoint(point);
            return screen.x > .08f && screen.x < .92f && screen.y > .12f && screen.y < .90f;
        }
    }
}
#endif
