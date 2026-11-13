using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Zombs
{
    public sealed class SurvivalTests
    {
        private static readonly Vector2 Generator = new(-4, 0);
        private static readonly Vector2 North = new(-4, 2);
        private static readonly Vector2 South = new(-4, -2);
        private GameCatalog catalog;

        [SetUp]
        public void SetUp() => catalog = GameCatalog.CreateDefaults();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

        [Test]
        public void FeedbackTracksSuccessfulActionsAndStateTransitions()
        {
            catalog.initialFuel = 1;
            catalog.daySeconds = 5;
            catalog.smelting.seconds = .2f;
            var run = New(new Price(50, 50, iron: 4));
            var events = new List<Effect>();
            run.EffectOccurred += events.Add;
            var core = Place(run, BuildingKind.Generator, Generator);
            var smelter = Place(run, BuildingKind.Smelter, North);
            Assert.That(run.QueueProduction(smelter), Is.EqualTo(2));
            Advance(run, 1.1f);
            Assert.That(events.Count(effect => effect.Kind == EffectKind.ProductionReady), Is.EqualTo(1));
            Assert.That(events.Count(effect => effect.Kind == EffectKind.PowerLost), Is.EqualTo(1));

            Assert.That(run.Refuel(core, ResourceKind.Coal), Is.False);
            Assert.That(run.Refuel(core, ResourceKind.Wood), Is.True);
            Assert.That(run.Refuel(core, ResourceKind.Wood), Is.True);
            Assert.That(events.Count(effect => effect.Kind == EffectKind.PowerRestored), Is.EqualTo(1));
            Assert.That(events.Count(effect => effect.Kind == EffectKind.Refueled), Is.EqualTo(1));

            Vector2 tree = run.Nodes.First(node => node.Kind == ResourceKind.Wood).Position;
            Advance(run, .22f, move: tree.normalized);
            Advance(run, .1f, aim: tree, attack: true);
            var harvest = events.Single(effect => effect.Kind == EffectKind.Harvested);
            Assert.That(harvest.Point, Is.EqualTo(tree));
            Assert.That(harvest.Material, Is.EqualTo(ResourceKind.Wood));
            Advance(run, 4);
            Assert.That(events.Count(effect => effect.Kind == EffectKind.WaveStarted), Is.EqualTo(1));
        }

        [Test]
        public void PickaxeHarvestUsesCooldownAndFractionalYield()
        {
            var run = New();
            Vector2 tree = run.Nodes.First(node => node.Kind == ResourceKind.Wood).Position;
            run.Step(Vector2.zero, tree, true);
            Assert.That(run.Resources.wood, Is.EqualTo(1.5f));
            Advance(run, .23f, aim: tree, attack: true);
            Assert.That(run.Resources.wood, Is.EqualTo(1.5f));
            Advance(run, .10f, aim: tree, attack: true);
            Assert.That(run.Resources.wood, Is.EqualTo(3));
            Advance(run, .64f, move: Vector2.up);
            Vector2 rock = run.Nodes.First(node => node.Kind == ResourceKind.Stone).Position;
            Advance(run, 1.1f, aim: rock, attack: true);
            Assert.That(run.Resources.stone, Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(run.Resources.wood, Is.EqualTo(3));
        }

        [Test]
        public void BuildingChecksKeepPaymentAndOccupiedCellsConsistent()
        {
            var run = New(new Price(15, 15));
            Assert.That(run.CanPlace(BuildingKind.Smelter, North), Is.EqualTo(Placement.NeedGenerator));
            Assert.That(run.CanPlace(BuildingKind.Generator, new Vector2(-6, 0)), Is.EqualTo(Placement.StarterSite));
            Place(run, BuildingKind.Generator, Generator);
            Assert.That(run.CanPlace(BuildingKind.Smelter, Generator + Vector2.right), Is.EqualTo(Placement.Occupied));
            Assert.That(run.CanPlace(BuildingKind.Smelter, new Vector2(40, 0)), Is.EqualTo(Placement.TooFar));
            Assert.That(run.Resources.wood, Is.EqualTo(15));
            Place(run, BuildingKind.Smelter, North);
            Assert.That(run.Resources.wood, Is.Zero);
            Assert.That(run.CanPlace(BuildingKind.Smelter, South), Is.EqualTo(Placement.Insufficient));
            Assert.That(run.Sell(run.At(North)), Is.True);
            Assert.That(run.Resources.wood, Is.EqualTo(7));
            Assert.That(run.At(North), Is.Null);
            Assert.That(run.Sell(run.Generator), Is.False);
        }

        [Test]
        public void GeneratorGatesAllEightTiersAndBulkUpgradesSpendPerBuilding()
        {
            var run = New(new Price(10000, 10000, parts: 10000));
            Place(run, BuildingKind.Generator, Generator);
            var first = Place(run, BuildingKind.Arrow, North);
            var second = Place(run, BuildingKind.Arrow, South);
            Assert.That(run.Upgrade(first, true), Is.Zero);
            for (int tier = 2; tier <= 8; tier++)
            {
                float radius = run.OxygenRadius;
                float capacity = run.FuelCapacity(run.Generator);
                Assert.That(run.Upgrade(run.Generator, false), Is.EqualTo(1));
                Assert.That(run.OxygenRadius, Is.GreaterThan(radius));
                Assert.That(run.FuelCapacity(run.Generator), Is.GreaterThan(capacity));
                Price balance = run.Resources;
                var price = first.Data.prices[tier - 1];
                Assert.That(run.Upgrade(first, true), Is.EqualTo(2));
                Assert.That(run.Resources.parts, Is.EqualTo(balance.parts - price.parts * 2));
                Assert.That(run.Resources.wood, Is.EqualTo(balance.wood - price.wood * 2));
                Assert.That(first.Tier, Is.EqualTo(tier));
                Assert.That(second.Tier, Is.EqualTo(tier));
            }
            Assert.That(run.Upgrade(first, true), Is.Zero);
        }

        [Test]
        public void FuelledMinerUnlocksAtTierTwoAndDepletesTheSelectedDeposit()
        {
            var run = New(new Price(100, 100, coal: 6, parts: 20));
            Place(run, BuildingKind.Generator, Generator);
            Vector2 spot = new(1, 3);
            Assert.That(run.CanPlace(BuildingKind.Miner, spot), Is.EqualTo(Placement.TierLocked));
            Assert.That(run.Upgrade(run.Generator, false), Is.EqualTo(1));
            Assert.That(run.CanPlace(BuildingKind.Miner, North), Is.EqualTo(Placement.NeedsDeposit));
            var miner = Place(run, BuildingKind.Miner, spot);
            Assert.That(miner.DepositKind, Is.EqualTo(ResourceKind.Stone));
            float remaining = miner.DepositNode.Remaining;
            Advance(run, 2);
            Assert.That(miner.Stored, Is.Zero);
            Assert.That(run.Refuel(miner, ResourceKind.Coal), Is.True);
            Advance(run, 10);
            Assert.That(miner.Stored, Is.EqualTo(1.2f).Within(.002f));
            Assert.That(miner.DepositNode.Remaining, Is.EqualTo(remaining - miner.Stored).Within(.005f));
            float before = run.Resources.stone;
            Assert.That(run.Collect(miner), Is.True);
            Assert.That(run.Resources.stone, Is.EqualTo(before + 1.2f).Within(.002f));
            Assert.That(run.Collect(miner), Is.False);
        }

        [Test]
        public void GeneratorBurnsFuelAndPoisonUsesReserveBeforeDamagingHealth()
        {
            catalog.initialFuel = 1;
            var run = New(new Price(wood: 1, coal: 1));
            var generator = Place(run, BuildingKind.Generator, Generator);
            Advance(run, 1.1f);
            Assert.That(run.GeneratorOnline, Is.False);
            Assert.That(run.CanBreathe, Is.False);
            Advance(run, 4.5f);
            Assert.That(run.Health, Is.EqualTo(catalog.playerHealth));
            Advance(run, 1.4f);
            Assert.That(run.Health, Is.EqualTo(catalog.playerHealth - 8).Within(.5f));
            Assert.That(run.Refuel(generator, ResourceKind.Wood), Is.True);
            Assert.That(run.CanBreathe, Is.True);
            Advance(run, 1.6f);
            Assert.That(run.Breath, Is.EqualTo(catalog.breathSeconds));
            Assert.That(run.Refuel(generator, ResourceKind.Coal), Is.True);
            Assert.That(generator.Fuel, Is.EqualTo(24.4f).Within(.05f));
            Assert.That(run.Resources.wood + run.Resources.coal, Is.Zero);
        }

        [Test]
        public void OreFlowsThroughBothMachinesIntoGeneratorAndEquipmentUpgrades()
        {
            var run = New(new Price(100, 100, iron: 30));
            Place(run, BuildingKind.Generator, Generator);
            Assert.That(run.Buy(ItemKind.Pickaxe), Is.False);
            var smelter = Place(run, BuildingKind.Smelter, North);
            Assert.That(run.QueueProduction(smelter), Is.EqualTo(5));
            Assert.That(run.QueueProduction(smelter), Is.EqualTo(5));
            Assert.That(run.QueueProduction(smelter), Is.Zero);
            Assert.That(run.Resources.iron, Is.EqualTo(10));
            Assert.That(run.Collect(smelter), Is.False);
            Advance(run, 20.1f);
            Assert.That(run.Collect(smelter), Is.True);
            Assert.That(run.Resources.plate, Is.EqualTo(10));
            var assembler = Place(run, BuildingKind.Assembler, South);
            Assert.That(run.QueueProduction(assembler), Is.EqualTo(5));
            Assert.That(run.QueueProduction(assembler), Is.EqualTo(3));
            Assert.That(run.QueueProduction(smelter), Is.EqualTo(5));
            Advance(run, 10.1f);
            Assert.That(run.Collect(smelter), Is.True);
            Assert.That(run.QueueProduction(assembler), Is.EqualTo(2));
            Advance(run, 10.1f);
            Assert.That(run.Collect(assembler), Is.True);
            Assert.That(run.Upgrade(run.Generator, false), Is.EqualTo(1));
            Assert.That(run.Resources.parts, Is.EqualTo(4));
            Assert.That(run.Buy(ItemKind.Pickaxe), Is.True);
            Assert.That(run.ItemTier(ItemKind.Pickaxe), Is.EqualTo(2));
            Assert.That(run.Resources.parts, Is.EqualTo(1));
        }

        [Test]
        public void SeedControlsOuterVeinsAndEveryStartHasFuelAndOre()
        {
            var first = new RunState(catalog, 1234);
            var repeat = new RunState(catalog, 1234);
            var different = new RunState(catalog, 1235);
            Assert.That(first.Nodes.Select(node => (node.Position, node.Kind, node.Remaining)),
                Is.EqualTo(repeat.Nodes.Select(node => (node.Position, node.Kind, node.Remaining))));
            Assert.That(first.Nodes.Select(node => node.Position), Is.Not.EqualTo(different.Nodes.Select(node => node.Position)));
            foreach (int seed in Enumerable.Range(-64, 129).Concat(new[] { int.MinValue, int.MaxValue, 5238 }))
            {
                var run = new RunState(catalog, seed);
                foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
                    Assert.That(run.Nodes.Any(node => node.Kind == kind && node.Position.magnitude < 20), Is.True, $"Seed {seed}: {kind}");
                Assert.That(run.Nodes.Count(node => node.Kind == ResourceKind.Coal && node.Position.magnitude < 25), Is.GreaterThan(1));
            }
        }

        [Test]
        public void TierThreeAssemblerFeedsFromNearbySmelterAfterSnapshotRestore()
        {
            var run = New(new Price(1000, 1000, plate: 20, parts: 100));
            var generator = Place(run, BuildingKind.Generator, Generator);
            var smelter = Place(run, BuildingKind.Smelter, North);
            var assembler = Place(run, BuildingKind.Assembler, South);
            smelter.Stored = 2;
            Advance(run, .1f);
            Assert.That(assembler.Queued, Is.Zero);
            Assert.That(smelter.Stored, Is.EqualTo(2));
            Assert.That(run.Upgrade(generator, false), Is.EqualTo(1));
            Assert.That(run.Upgrade(generator, false), Is.EqualTo(1));
            Assert.That(run.Upgrade(assembler, false), Is.EqualTo(1));
            Assert.That(run.Upgrade(assembler, false), Is.EqualTo(1));

            float wood = run.Resources.wood;
            run = RunState.Restore(catalog, run.Capture());
            Assert.That(run, Is.Not.Null);
            Advance(run, .1f);
            Assert.That(run.At(North).Stored, Is.Zero);
            Assert.That(run.At(South).Queued, Is.EqualTo(2));
            Assert.That(run.Resources.wood, Is.EqualTo(wood - 2));
            Advance(run, 6);
            Assert.That(run.Collect(run.At(South)), Is.True);
        }

        [TestCase(-64)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(64)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void EmptyTankCanRestartOnHarvestedWoodAndSupportACoalTrip(int seed)
        {
            catalog.initialFuel = 1;
            var run = new RunState(catalog, seed);
            var core = Place(run, BuildingKind.Generator, new Vector2(-3, 0));
            Advance(run, catalog.initialFuel + .1f);
            Assert.That(run.GeneratorOnline, Is.False);
            Vector2 tree = run.Nodes.First(node => node.Kind == ResourceKind.Wood).Position;
            Advance(run, .21f, move: tree.normalized);
            Advance(run, .7f, aim: tree, attack: true);
            Assert.That(run.Refuel(core, ResourceKind.Wood), Is.True);
            Assert.That(run.CanBreathe, Is.True);

            Vector2 coal = run.Nodes.Where(node => node.Kind == ResourceKind.Coal)
                .OrderBy(node => (node.Position - new Vector2(10, 4)).sqrMagnitude).First().Position;
            for (int i = 0; i < 360 && run.Resources.coal < 2; i++)
            {
                Vector2 delta = coal - run.Player;
                run.Step(delta.magnitude > 1.35f ? delta.normalized : Vector2.zero, coal, true);
            }
            Assert.That(run.Resources.coal, Is.GreaterThanOrEqualTo(2), $"Seed {seed}");
            Assert.That(run.Dead, Is.False);
            for (int i = 0; i < 360 && !run.InReach(core); i++)
                run.Step((Vector2.zero - run.Player).normalized, Vector2.zero, false);
            Assert.That(run.Refuel(core, ResourceKind.Coal), Is.True);
        }

        [TestCase(1234)]
        [TestCase(-19)]
        [TestCase(5238)]
        public void FreshRunThroughFirstBoss(int seed)
        {
            var run = new RunState(catalog, seed);
            var core = Place(run, BuildingKind.Generator, new Vector2(-3, 0));
            int woodBurned = 0, coalBurned = 0;
            float minimumFuel = core.Fuel, elapsed = 0;
            Price defenseCost = default;
            void Tick(Vector2 move = default, Vector2 aim = default, bool attack = false)
            {
                if (run.InReach(core) && core.Fuel < 90)
                {
                    int coal = run.FuelLoad(core, ResourceKind.Coal);
                    if (run.Refuel(core, ResourceKind.Coal)) coalBurned += coal;
                    else
                    {
                        int wood = run.FuelLoad(core, ResourceKind.Wood);
                        if (run.Resources.wood >= 25 && run.Refuel(core, ResourceKind.Wood)) woodBurned += wood;
                    }
                }
                run.Step(move, aim, attack);
                elapsed += RunState.TickSeconds;
                minimumFuel = Mathf.Min(minimumFuel, core.Fuel);
                Assert.That(run.Dead || run.Defeated, Is.False, $"Seed {seed}, wave {run.Wave}, t={elapsed:0}, player={run.Player}");
                Assert.That(elapsed, Is.LessThan(1350));
            }

            void Go(Vector2 position)
            {
                for (int i = 0; i < 360; i++)
                {
                    Vector2 delta = position - run.Player;
                    if (delta.magnitude < .09f) return;
                    Tick(Vector2.ClampMagnitude(delta / (catalog.playerSpeed * RunState.TickSeconds), 1));
                }
                Assert.Fail($"Seed {seed}: could not walk to {position} from {run.Player}");
            }

            void Gather(ResourceKind kind, float target, Vector2 approach)
            {
                Go(approach);
                var node = run.Nodes.Where(n => n.Kind == kind && !n.Depleted)
                    .OrderBy(n => (n.Position - run.Player).sqrMagnitude).First();
                for (int i = 0; i < 2400 && !run.Resources.Covers(Price.Gathered(kind, target)); i++)
                {
                    Vector2 delta = node.Position - run.Player;
                    Tick(delta.magnitude > 1.35f ? delta.normalized : Vector2.zero, node.Position, true);
                    if (node.Depleted)
                        node = run.Nodes.Where(n => n.Kind == kind && !n.Depleted)
                            .OrderBy(n => (n.Position - run.Player).sqrMagnitude).First();
                }
                Assert.That(run.Resources.Covers(Price.Gathered(kind, target)), Is.True, $"Seed {seed}: gather {kind}");
            }

            void Defense(BuildingKind kind, Vector2 position)
            {
                Place(run, kind, position);
                defenseCost += catalog.Building(kind).prices[0];
            }

            Gather(ResourceKind.Wood, 65, Vector2.zero);
            Gather(ResourceKind.Stone, 90, new Vector2(0, 4));
            Go(Vector2.zero);
            Defense(BuildingKind.Arrow, new Vector2(-5, 2));
            Defense(BuildingKind.Arrow, new Vector2(-1, 2));
            Defense(BuildingKind.Arrow, new Vector2(-3, -2));
            Defense(BuildingKind.Arrow, new Vector2(-5, -2));
            while (run.Wave < 1 || run.Night) Tick();
            float firstNightFuel = core.Fuel;
            var smelter = Place(run, BuildingKind.Smelter, new Vector2(-1, -2));
            Go(new Vector2(1, 1.15f));
            Go(new Vector2(7, 2));
            Go(new Vector2(9, -4));
            Gather(ResourceKind.Iron, 20, new Vector2(12, -7));
            Go(new Vector2(9, -4));
            Go(new Vector2(7, 2));
            Go(new Vector2(1, 1.15f));
            Go(Vector2.zero);
            float productionStarted = elapsed;
            while (run.Resources.plate < 10)
            {
                run.QueueProduction(smelter);
                run.Collect(smelter);
                Tick();
            }
            Gather(ResourceKind.Wood, 120, Vector2.zero);
            Go(Vector2.zero);
            var assembler = Place(run, BuildingKind.Assembler, new Vector2(-1, -4));
            while (run.Resources.parts < 6)
            {
                run.QueueProduction(assembler);
                run.Collect(assembler);
                Tick();
            }
            Assert.That(run.Upgrade(core, false), Is.EqualTo(1));
            float tierTwoAt = elapsed;
            Defense(BuildingKind.Bomb, new Vector2(-3, 2));
            Defense(BuildingKind.Bomb, new Vector2(-3, -4));
            while (run.Wave < catalog.firstBossWave || run.Night)
            {
                if (run.Resources.wood < 30 && !run.Night)
                {
                    Gather(ResourceKind.Wood, 145, Vector2.zero);
                    Go(Vector2.zero);
                }
                foreach (var building in run.Buildings)
                    if (building.Health < building.MaximumHealth * .75f) run.Repair(building);
                Tick();
            }
            TestContext.WriteLine($"Seed {seed}: fuel min {minimumFuel:0.0}s, first dawn {firstNightFuel:0.0}s, burned {woodBurned} wood and {coalBurned} coal. " +
                $"Defense {defenseCost.wood} wood/{defenseCost.stone} stone, production start {productionStarted:0.0}s, Tier II {tierTwoAt:0.0}s, boss night clear {elapsed:0.0}s.");
            Assert.That(run.Resources.tokens, Is.GreaterThanOrEqualTo(catalog.bossTokens));
        }

        [Test]
        public void TowerFiresAndKillsWithoutPlayerAttack()
        {
            catalog.daySeconds = 2;
            catalog.firstWaveCount = 5;
            catalog.spawnRadius = 5;
            catalog.zombies[0].health = 12;
            var run = New(new Price(30, 30));
            Place(run, BuildingKind.Generator, Generator);
            Place(run, BuildingKind.Arrow, North);
            Place(run, BuildingKind.Arrow, South);
            bool sawArrow = false;
            for (int i = 0; i < 400; i++)
            {
                run.Step(Vector2.zero, Vector2.zero, false);
                sawArrow |= run.Projectiles.Any(shot => shot.Kind == ShotKind.Arrow);
            }
            Assert.That(sawArrow, Is.True);
            Assert.That(run.Score, Is.GreaterThan(0));
        }

        [Test]
        public void TimeoutSkipsOneNightAndWaveNineSpawnsTheSoloBoss()
        {
            catalog.daySeconds = 6;
            catalog.nightSeconds = 4;
            catalog.firstWaveCount = 4;
            catalog.maximumWaveCount = 4;
            catalog.zombies[0].damage = 0;
            var run = New(new Price(coal: 20, parts: 16));
            Advance(run, 8);
            Assert.That(run.Wave, Is.Zero);
            Place(run, BuildingKind.Generator, Generator);
            Assert.That(run.Cast(SpellKind.Timeout, Generator), Is.True);
            Assert.That(run.Cast(SpellKind.Timeout, Generator), Is.False);
            Assert.That(run.Resources.coal, Is.EqualTo(10));
            Advance(run, 8.8f);
            Assert.That(run.Wave, Is.EqualTo(1));
            Assert.That(run.TimeoutActive, Is.True);
            Assert.That(run.Enemies, Is.Empty);
            Advance(run, 10);
            Assert.That(run.Wave, Is.EqualTo(2));
            Assert.That(run.TimeoutActive, Is.False);
            Assert.That(run.Enemies.Count, Is.GreaterThan(0));
            Advance(run, 62.15f);
            Assert.That(run.Wave, Is.EqualTo(8));
            Assert.That(run.Enemies.Any(enemy => enemy.Boss), Is.False);
            Advance(run, 7);
            Assert.That(run.Wave, Is.EqualTo(9));
            Assert.That(run.Enemies.Count(enemy => enemy.Boss), Is.EqualTo(1));
            Assert.That(run.Enemies.Count, Is.EqualTo(1));
        }

        [Test]
        public void LateWavesRemainBoundedAndAdvanceBeyondWaveOneHundred()
        {
            catalog.daySeconds = 6;
            catalog.nightSeconds = 4;
            catalog.maximumWaveCount = 6;
            foreach (var enemy in catalog.zombies) enemy.damage = 0;
            catalog.bossDamage = 0;
            var run = New();
            Place(run, BuildingKind.Generator, Generator);
            Advance(run, 1299);
            Assert.That(run.Wave, Is.EqualTo(130));
            Assert.That(run.Enemies.Count, Is.LessThanOrEqualTo(catalog.maximumWaveCount));
            Assert.That(run.Enemies.All(enemy => !float.IsNaN(enemy.Health) && enemy.MaxHealth > 0), Is.True);
        }

        [Test]
        public void HealTowersChargesMaterialsAndEnforcesItsRecharge()
        {
            catalog.healCooldown = 12;
            var run = New(new Price(stone: 30, plate: 6));
            Assert.That(run.Cast(SpellKind.HealTowers, Generator), Is.True);
            Assert.That(run.Resources.plate, Is.EqualTo(4));
            Assert.That(run.HealPoint, Is.EqualTo(Generator));
            Advance(run, 11);
            Assert.That(run.HealRemaining, Is.Zero);
            Assert.That(run.Cast(SpellKind.HealTowers, Generator), Is.False);
            Advance(run, 1.1f);
            Assert.That(run.Cast(SpellKind.HealTowers, Generator), Is.True);
        }

        [Test]
        public void DeathKeepsTheBaseAndLoadoutAndDefeatAllowsRecovery()
        {
            catalog.daySeconds = 1;
            catalog.nightSeconds = 50;
            catalog.spawnRadius = 4;
            catalog.firstWaveCount = 26;
            catalog.playerHealth = 5;
            catalog.zombies[0].damage = 10;
            catalog.Building(BuildingKind.Generator).health[0] = 600;
            var run = New(new Price(100, 100));
            Place(run, BuildingKind.Generator, Generator);
            Assert.That(run.Buy(ItemKind.Bow), Is.True);
            run.Equip(ItemKind.Bow);
            Advance(run, .24f, move: Vector2.left);
            for (int i = 0; i < 900 && !run.Dead && !run.Defeated; i++)
                run.Step(Vector2.zero, Vector2.zero, false);
            Assert.That(run.Dead, Is.True);
            Assert.That(run.Defeated, Is.False);
            Assert.That(run.Respawn(), Is.True);
            Assert.That(run.Protected, Is.True);
            Assert.That(run.Weapon, Is.EqualTo(ItemKind.Bow));
            Assert.That(run.Generator, Is.Not.Null);
            Advance(run, .65f, move: Vector2.right);
            Advance(run, 70);
            Assert.That(run.Defeated, Is.True);
            Assert.That(run.Buildings, Is.Empty);
            run = RunState.Restore(catalog, JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(run.Capture())));
            Assert.That(run, Is.Not.Null);
            Assert.That(run.Defeated, Is.True);
            Assert.That(run.RecoverAfterDefeat(), Is.True);
            Assert.That(run.Resources.wood, Is.EqualTo(67));
            Assert.That(run.ItemTier(ItemKind.Bow), Is.Zero);
            Assert.That(run.Wave, Is.Zero);
            Assert.That(run.Health, Is.EqualTo(catalog.playerHealth));
            Place(run, BuildingKind.Generator, Generator);
        }

        [Test]
        public void CheckpointRoundTripContinuesTheSameWaveAndProduction()
        {
            catalog.daySeconds = 1;
            var run = New(new Price(200, 200, iron: 20, coal: 6, parts: 20));
            Place(run, BuildingKind.Generator, Generator);
            Assert.That(run.Upgrade(run.Generator, false), Is.EqualTo(1));
            var smelter = Place(run, BuildingKind.Smelter, North);
            Assert.That(run.QueueProduction(smelter), Is.GreaterThan(0));
            var miner = Place(run, BuildingKind.Miner, new Vector2(1, 3));
            var deposit = miner.DepositNode;
            deposit.Extract(deposit.Remaining);
            Assert.That(run.Refuel(miner, ResourceKind.Coal), Is.True);
            Assert.That(run.Buy(ItemKind.Woody), Is.True);
            Assert.That(run.Buy(ItemKind.Bow), Is.True);
            run.Equip(ItemKind.Bow);
            Advance(run, 3);
            run.Step(Vector2.zero, Vector2.left, true);
            Assert.That(run.Night, Is.True);
            Assert.That(run.Enemies, Is.Not.Empty);
            Assert.That(run.Projectiles, Is.Not.Empty);
            Assert.That(smelter.Queued, Is.GreaterThan(0));
            Assert.That(miner.DepositNode, Is.Null);

            string checkpoint = JsonUtility.ToJson(run.Capture());
            var loaded = RunState.Restore(catalog, JsonUtility.FromJson<RunSnapshot>(checkpoint));
            Assert.That(loaded, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(loaded.Capture()), Is.EqualTo(checkpoint));
            Assert.That(loaded.Nodes.Single(node => node.Position == deposit.Position).Depleted, Is.True);
            Assert.That(loaded.Nodes[0].Remaining, Is.EqualTo(float.PositiveInfinity));
            Assert.That(loaded.At(loaded.Generator.Position), Is.SameAs(loaded.Generator));
            Assert.That(loaded.Count(BuildingKind.Miner), Is.EqualTo(1));

            Advance(run, 4, aim: Vector2.left, attack: true);
            Advance(loaded, 4, aim: Vector2.left, attack: true);
            Assert.That(JsonUtility.ToJson(loaded.Capture()), Is.EqualTo(JsonUtility.ToJson(run.Capture())));
        }

        [Test]
        public void CheckpointRejectsInvalidVersionAndOverlappingFootprints()
        {
            var run = New();
            Place(run, BuildingKind.Generator, Generator);
            var checkpoint = run.Capture();
            checkpoint.version++;
            Assert.That(RunState.Restore(catalog, checkpoint), Is.Null);
            checkpoint.version = RunSnapshot.CurrentVersion;
            var overlapping = checkpoint.buildings[0];
            overlapping.kind = (int)BuildingKind.Arrow;
            overlapping.size = catalog.Building(BuildingKind.Arrow).size;
            checkpoint.buildings.Add(overlapping);
            Assert.That(RunState.Restore(catalog, checkpoint), Is.Null);
        }

        [Test]
        public void DamagedCheckpointFallsBackAndKeepsTheBackupUntilAReplacementIsSafe()
        {
            string directory = Path.Combine(Path.GetTempPath(), "zombs-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new RunSaveStore(directory);
                var run = New();
                Place(run, BuildingKind.Generator, Generator);
                Assert.That(store.Save(run, newRun: true), Is.True);
                float oldFuel = run.Generator.Fuel;
                Advance(run, 3);
                Assert.That(store.Save(run), Is.True);

                string path = Path.Combine(directory, "survival-run.json");
                File.WriteAllText(path, "{\"version\":1}");
                store = new RunSaveStore(directory);
                var recovered = store.Load(catalog, out bool usedBackup);
                Assert.That(usedBackup, Is.True);
                Assert.That(recovered.Generator.Fuel, Is.EqualTo(oldFuel));

                string backup = File.ReadAllText(path + ".bak");
                Assert.That(store.Save(recovered), Is.True);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
                Assert.That(store.Load(catalog, out usedBackup).Generator.Fuel, Is.EqualTo(oldFuel));
                Assert.That(usedBackup, Is.False);

                Assert.That(store.Save(new RunState(catalog, 2026), newRun: true), Is.True);
                Assert.That(store.Load(catalog, out _).Seed, Is.EqualTo(2026));
                Assert.That(File.Exists(path + ".bak"), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private RunState New(Price resources = default) => new(catalog, 5238, resources);

        private static Structure Place(RunState run, BuildingKind kind, Vector2 position)
        {
            Assert.That(run.Place(kind, position, 0, out var result), Is.True, $"{kind}: {result}");
            return run.At(position);
        }

        private static void Advance(RunState run, float seconds, Vector2 move = default, Vector2 aim = default, bool attack = false)
        {
            int ticks = (int)Math.Round(seconds / RunState.TickSeconds);
            for (int i = 0; i < ticks; i++) run.Step(move, aim, attack);
        }
    }
}
