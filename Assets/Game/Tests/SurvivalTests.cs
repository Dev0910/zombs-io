using System;
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
            for (int seed = -10; seed <= 10; seed++)
            {
                var run = new RunState(catalog, seed);
                foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
                    Assert.That(run.Nodes.Any(node => node.Kind == kind && node.Position.magnitude < 20), Is.True, $"Seed {seed}: {kind}");
                Assert.That(run.Nodes.Count(node => node.Kind == ResourceKind.Coal && node.Position.magnitude < 25), Is.GreaterThan(1));
            }
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
            Assert.That(run.RecoverAfterDefeat(), Is.True);
            Assert.That(run.Resources.wood, Is.EqualTo(67));
            Assert.That(run.ItemTier(ItemKind.Bow), Is.Zero);
            Assert.That(run.Wave, Is.Zero);
            Assert.That(run.Health, Is.EqualTo(catalog.playerHealth));
            Place(run, BuildingKind.Generator, Generator);
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
