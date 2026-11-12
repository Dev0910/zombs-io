using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    // Increment the version if the schema, enum indices or seeded map layout changes incompatibly.
    [Serializable]
    internal sealed class RunSnapshot
    {
        internal const int CurrentVersion = 1;
        public int version, seed, worldSize, nodeCount, randomDraws;
        public int wave, score, remainingSpawns, nextSpawn;
        public bool night, nightHadWave, timeoutPending, timeoutActive, defeated;
        public float phaseTime, spawnClock;
        public Price resources;
        public Vector2 player, aim, healPoint, petPosition;
        public float health, shield, breath, petHealth;
        public int weapon, pet, petExperience, petEvolution;
        public bool autoAttack, wearingHat;
        public float attackCooldown, healRemaining, playerRecovery, poisonClock;
        public float potionCooldown, petPotionCooldown, respawnProtection;
        public float petAttackCooldown, petRespawnCooldown, shieldRecovery, healRecharge, timeoutRecharge;
        public int[] itemTiers;
        public float[] campTimers;
        public List<Node> nodes = new();
        public List<Building> buildings = new();
        public List<Hostile> enemies = new();
        public List<Shot> projectiles = new();

        [Serializable]
        internal struct Node
        {
            public int index;
            public Vector2 position;
            public float remaining;
        }

        [Serializable]
        internal struct Building
        {
            public int kind, size, tier, rotation, queued, depositKind, depositIndex;
            public Vector2Int anchor;
            public float health, stored, fuel, angle, processProgress, cooldown, recovery;
            public Price invested;
        }

        [Serializable]
        internal struct Hostile
        {
            public Vector2 position;
            public Color color;
            public float health, maxHealth, angle, damage, speed, score, flash, cooldown, slow;
            public bool boss, provoked;
            public int camp;
        }

        [Serializable]
        internal struct Shot
        {
            public Vector2 position, direction;
            public int kind;
            public float speed, distance, damage, splash;
        }
    }

    public sealed partial class RunState
    {
        internal RunSnapshot Capture()
        {
            var snapshot = new RunSnapshot
            {
                version = RunSnapshot.CurrentVersion, seed = Seed, worldSize = Catalog.worldSize,
                nodeCount = nodes.Count, randomDraws = randomDraws,
                wave = Wave, score = Score, night = Night, defeated = Defeated,
                phaseTime = PhaseTime, spawnClock = spawnClock, nextSpawn = nextSpawn,
                remainingSpawns = remainingSpawns, nightHadWave = nightHadWave,
                timeoutPending = TimeoutPending, timeoutActive = TimeoutActive,
                resources = Resources, player = Player, aim = Aim, health = Health, shield = Shield, breath = Breath,
                weapon = (int)Weapon, autoAttack = AutoAttack, wearingHat = WearingHat,
                pet = ActivePet.HasValue ? (int)ActivePet.Value : -1, petPosition = PetPosition,
                petHealth = PetHealth, petExperience = PetExperience, petEvolution = PetEvolution,
                healPoint = healPoint, healRemaining = healRemaining, attackCooldown = attackCooldown,
                playerRecovery = playerRecovery, poisonClock = poisonClock, potionCooldown = potionCooldown,
                petPotionCooldown = petPotionCooldown, respawnProtection = respawnProtection,
                petAttackCooldown = petAttackCooldown, petRespawnCooldown = petRespawnCooldown,
                shieldRecovery = shieldRecovery, healRecharge = healRecharge, timeoutRecharge = timeoutRecharge,
                itemTiers = (int[])itemTiers.Clone(), campTimers = (float[])campTimers.Clone()
            };

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.WasGathered)
                    snapshot.nodes.Add(new RunSnapshot.Node { index = i, position = node.Position, remaining = node.Remaining });
            }
            foreach (var building in buildings)
            {
                snapshot.buildings.Add(new RunSnapshot.Building
                {
                    kind = (int)building.Kind, size = building.Data.size, anchor = building.Anchor,
                    tier = building.Tier, rotation = building.Rotation, health = building.Health,
                    stored = building.Stored, fuel = building.Fuel, angle = building.Angle, queued = building.Queued,
                    processProgress = building.ProcessProgress, cooldown = building.Cooldown,
                    recovery = building.Recovery, invested = building.Invested,
                    depositKind = building.DepositKind.HasValue ? (int)building.DepositKind.Value : -1,
                    depositIndex = building.DepositNode == null ? -1 : nodes.IndexOf(building.DepositNode)
                });
            }
            foreach (var enemy in enemies)
            {
                snapshot.enemies.Add(new RunSnapshot.Hostile
                {
                    position = enemy.Position, color = enemy.Color, health = enemy.Health, maxHealth = enemy.MaxHealth,
                    angle = enemy.Angle, damage = enemy.Damage, speed = enemy.Speed, score = enemy.Score, flash = enemy.Flash,
                    boss = enemy.Boss, camp = enemy.Camp, cooldown = enemy.Cooldown, slow = enemy.Slow, provoked = enemy.Provoked
                });
            }
            foreach (var shot in projectiles)
            {
                snapshot.projectiles.Add(new RunSnapshot.Shot
                {
                    position = shot.Position, direction = shot.Direction, kind = (int)shot.Kind,
                    speed = shot.Speed, distance = shot.Distance, damage = shot.Damage, splash = shot.Splash
                });
            }
            return snapshot;
        }

        internal static RunState Restore(GameCatalog catalog, RunSnapshot snapshot)
        {
            if (snapshot == null || snapshot.version != RunSnapshot.CurrentVersion ||
                snapshot.worldSize != catalog.worldSize || snapshot.randomDraws < 0 || snapshot.randomDraws > 10_000_000 ||
                snapshot.itemTiers?.Length != catalog.items.Length || snapshot.campTimers == null ||
                snapshot.nodes == null || snapshot.buildings == null || snapshot.enemies == null || snapshot.projectiles == null)
                return null;

            var state = new RunState(catalog, snapshot.seed);
            if (!state.RestoreMap(snapshot) || !state.RestoreActors(snapshot)) return null;

            // The seeded terrain has its own RNG. Replay only the draws used for wave spawns.
            for (int i = 0; i < snapshot.randomDraws; i++) state.random.NextDouble();
            state.randomDraws = snapshot.randomDraws;
            state.Wave = snapshot.wave;
            state.Score = snapshot.score;
            state.Night = snapshot.night;
            state.Defeated = snapshot.defeated;
            state.PhaseTime = snapshot.phaseTime;
            state.spawnClock = snapshot.spawnClock;
            state.nextSpawn = snapshot.nextSpawn;
            state.remainingSpawns = snapshot.remainingSpawns;
            state.nightHadWave = snapshot.nightHadWave;
            state.TimeoutPending = snapshot.timeoutPending;
            state.TimeoutActive = snapshot.timeoutActive;
            state.Resources = snapshot.resources;
            state.Player = snapshot.player;
            state.Aim = snapshot.aim;
            state.Health = snapshot.health;
            state.Shield = snapshot.shield;
            state.Breath = snapshot.breath;
            state.Weapon = (ItemKind)snapshot.weapon;
            state.AutoAttack = snapshot.autoAttack;
            state.WearingHat = snapshot.wearingHat;
            state.ActivePet = snapshot.pet == -1 ? null : (ItemKind)snapshot.pet;
            state.PetPosition = snapshot.petPosition;
            state.PetHealth = snapshot.petHealth;
            state.PetExperience = snapshot.petExperience;
            state.PetEvolution = snapshot.petEvolution;
            state.healPoint = snapshot.healPoint;
            state.healRemaining = snapshot.healRemaining;
            state.attackCooldown = snapshot.attackCooldown;
            state.playerRecovery = snapshot.playerRecovery;
            state.poisonClock = snapshot.poisonClock;
            state.potionCooldown = snapshot.potionCooldown;
            state.petPotionCooldown = snapshot.petPotionCooldown;
            state.respawnProtection = snapshot.respawnProtection;
            state.petAttackCooldown = snapshot.petAttackCooldown;
            state.petRespawnCooldown = snapshot.petRespawnCooldown;
            state.shieldRecovery = snapshot.shieldRecovery;
            state.healRecharge = snapshot.healRecharge;
            state.timeoutRecharge = snapshot.timeoutRecharge;
            snapshot.itemTiers.CopyTo(state.itemTiers, 0);
            snapshot.campTimers.CopyTo(state.campTimers, 0);
            return state;
        }

        private bool RestoreMap(RunSnapshot snapshot)
        {
            if (snapshot.nodeCount != nodes.Count || snapshot.campTimers.Length != campTimers.Length) return false;
            var changedNodes = new HashSet<int>();
            foreach (var saved in snapshot.nodes)
            {
                if ((uint)saved.index >= (uint)nodes.Count || !changedNodes.Add(saved.index) ||
                    nodes[saved.index].Position != saved.position || !nodes[saved.index].RestoreRemaining(saved.remaining))
                    return false;
            }

            foreach (var saved in snapshot.buildings)
            {
                if ((uint)saved.kind >= (uint)Catalog.buildings.Length || saved.rotation is < 0 or > 3 ||
                    saved.depositIndex < -1 || saved.depositIndex >= nodes.Count ||
                    saved.queued < 0 || !Finite(saved.health, saved.stored, saved.fuel, saved.angle,
                        saved.processProgress, saved.cooldown, saved.recovery) || !Valid(saved.invested))
                    return false;
                var data = Catalog.Building((BuildingKind)saved.kind);
                if (saved.size != data.size || saved.tier < 1 || saved.tier > data.prices.Length ||
                    (data.kind == BuildingKind.Generator && Generator != null) ||
                    (saved.queued > 0 && Catalog.RecipeFor(data.kind) == null)) return false;

                var deposit = saved.depositIndex == -1 ? null : nodes[saved.depositIndex];
                if (data.kind == BuildingKind.Miner)
                {
                    if (saved.depositKind < (int)ResourceKind.Stone || saved.depositKind > (int)ResourceKind.Coal ||
                        (deposit != null && (int)deposit.Kind != saved.depositKind)) return false;
                }
                else if (saved.depositKind != -1 || deposit != null) return false;

                var building = new Structure(data, saved.anchor, saved.rotation)
                {
                    Tier = saved.tier, Health = saved.health, Stored = saved.stored, Fuel = saved.fuel,
                    Angle = saved.angle, Queued = saved.queued, ProcessProgress = saved.processProgress,
                    Cooldown = saved.cooldown, Recovery = saved.recovery, Invested = saved.invested,
                    DepositKind = saved.depositKind == -1 ? null : (ResourceKind)saved.depositKind, DepositNode = deposit
                };
                float half = Catalog.worldSize * .5f - data.size * .5f;
                if (Mathf.Abs(building.Position.x) >= half || Mathf.Abs(building.Position.y) >= half) return false;
                for (int x = 0; x < data.size; x++)
                for (int y = 0; y < data.size; y++)
                    if (occupied.ContainsKey(saved.anchor + new Vector2Int(x, y))) return false;
                AddBuilding(building);
            }
            return buildings.Count == 0 || (Generator != null && !snapshot.defeated);
        }

        private bool RestoreActors(RunSnapshot snapshot)
        {
            if (snapshot.wave < 0 || snapshot.score < 0 || snapshot.remainingSpawns < 0 || snapshot.nextSpawn < 0 ||
                snapshot.weapon < (int)ItemKind.Pickaxe || snapshot.weapon > (int)ItemKind.Bomb ||
                snapshot.itemTiers[snapshot.weapon] < 1 || snapshot.itemTiers[(int)ItemKind.Pickaxe] < 1 ||
                (snapshot.pet != -1 && snapshot.pet != (int)ItemKind.Carl && snapshot.pet != (int)ItemKind.Woody) ||
                !Valid(snapshot.resources) || !Valid(snapshot.player) || !Valid(snapshot.aim) ||
                !Valid(snapshot.healPoint) || !Valid(snapshot.petPosition) || !Finite(snapshot.campTimers) ||
                !Finite(snapshot.phaseTime, snapshot.spawnClock, snapshot.health, snapshot.shield, snapshot.breath,
                    snapshot.petHealth, snapshot.healRemaining, snapshot.attackCooldown, snapshot.playerRecovery,
                    snapshot.poisonClock, snapshot.potionCooldown, snapshot.petPotionCooldown, snapshot.respawnProtection,
                    snapshot.petAttackCooldown, snapshot.petRespawnCooldown, snapshot.shieldRecovery,
                    snapshot.healRecharge, snapshot.timeoutRecharge))
                return false;
            for (int i = 0; i < itemTiers.Length; i++)
                if (snapshot.itemTiers[i] < 0 || snapshot.itemTiers[i] > Catalog.items[i].prices.Length) return false;
            foreach (var saved in snapshot.enemies)
            {
                if (saved.camp < -1 || saved.camp >= camps.Length || saved.maxHealth <= 0 ||
                    !Valid(saved.position) || !Finite(saved.health, saved.maxHealth, saved.angle, saved.damage, saved.speed,
                        saved.score, saved.flash, saved.cooldown, saved.slow, saved.color.r, saved.color.g, saved.color.b, saved.color.a))
                    return false;
                enemies.Add(new Enemy(saved.position, saved.color, saved.maxHealth, saved.damage, saved.speed, saved.score, saved.boss, saved.camp)
                {
                    Health = saved.health, Angle = saved.angle, Flash = saved.flash,
                    Cooldown = saved.cooldown, Slow = saved.slow, Provoked = saved.provoked
                });
            }
            foreach (var saved in snapshot.projectiles)
            {
                if (saved.kind < (int)ShotKind.Arrow || saved.kind > (int)ShotKind.Magic ||
                    !Valid(saved.position) || !Valid(saved.direction) || !Finite(saved.speed, saved.distance, saved.damage, saved.splash))
                    return false;
                projectiles.Add(new Projectile(saved.position, saved.direction, (ShotKind)saved.kind,
                    saved.speed, saved.distance, saved.damage, saved.splash));
            }
            return true;
        }

        private static bool Valid(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
        private static bool Valid(Price value) => value.Covers(default) &&
            Finite(value.wood, value.stone, value.iron, value.coal, value.plate, value.parts);

        private static bool Finite(params float[] values)
        {
            foreach (float value in values)
                if (!float.IsFinite(value)) return false;
            return true;
        }
    }
}
