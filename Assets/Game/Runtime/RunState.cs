using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    public enum Placement { Allowed, Dead, NeedGenerator, Limit, TooFar, Occupied, Insufficient }
    public enum EffectKind { Swing, Impact, Explosion, Gain, Damage, Placed, Upgraded, Defeated }

    public readonly struct Effect
    {
        public readonly EffectKind Kind;
        public readonly Vector2 Point;
        public readonly string Text;
        public Effect(EffectKind kind, Vector2 point, string text)
        {
            Kind = kind;
            Point = point;
            Text = text;
        }
    }

    public sealed class ResourceNode
    {
        public Vector2 Position { get; }
        public bool Stone { get; }
        public float Radius => Stone ? 1.30f : 1.62f;
        public float Flash { get; internal set; }

        public ResourceNode(Vector2 position, bool stone)
        {
            Position = position;
            Stone = stone;
        }
    }

    public sealed class Structure
    {
        public BuildingData Data { get; }
        public Vector2Int Anchor { get; }
        public Vector2 Position => (Vector2)Anchor + Vector2.one * (Data.size * .5f);
        public BuildingKind Kind => Data.kind;
        public int Tier { get; internal set; } = 1;
        public int Rotation { get; }
        public float MaximumHealth => Data.health[Tier - 1];
        public float Health { get; internal set; }
        public float Stored { get; internal set; }
        public float Fuel { get; internal set; }
        public float Angle { get; internal set; }
        internal float Cooldown, Recovery, HarvestClock;
        internal Price Invested;

        internal Structure(BuildingData data, Vector2Int anchor, int rotation)
        {
            Data = data;
            Anchor = anchor;
            Rotation = (rotation % 4 + 4) % 4;
            Angle = Rotation * 90;
            Health = MaximumHealth;
            Invested = data.prices[0];
        }
    }

    public sealed partial class RunState
    {
        public const float TickSeconds = 1f / 30f;
        private const float PlayerRadius = .62f;
        private const int ResourceBucketSize = 8;
        private readonly System.Random random;
        private readonly Dictionary<Vector2Int, Structure> occupied = new();
        private readonly Dictionary<Vector2Int, List<ResourceNode>> resourceBuckets = new();
        private readonly List<Structure> buildings = new();
        private readonly List<ResourceNode> nodes = new();
        private readonly List<Enemy> enemies = new();
        private readonly List<Projectile> projectiles = new();
        private readonly int[] counts, itemTiers;
        private readonly Vector2[] camps;
        private readonly float[] campTimers;
        private float attackCooldown, goldClock, healRemaining, playerRecovery;
        private float spawnClock, potionCooldown, petPotionCooldown, respawnProtection;
        private float petAttackCooldown, petRespawnCooldown, shieldRecovery, healRecharge, timeoutRecharge;
        private int remainingSpawns, nextSpawn;
        private bool nightHadWave;
        private Vector2 healPoint;

        public GameCatalog Catalog { get; }
        public int Seed { get; }
        public Price Resources { get; private set; }
        public IReadOnlyList<ResourceNode> Nodes => nodes;
        public IReadOnlyList<Structure> Buildings => buildings;
        public IReadOnlyList<Enemy> Enemies => enemies;
        public IReadOnlyList<Projectile> Projectiles => projectiles;
        public Structure Generator { get; private set; }
        public Vector2 Player { get; private set; }
        public Vector2 Aim { get; private set; } = Vector2.up;
        public float Health { get; private set; }
        public float Shield { get; private set; }
        public float MaxShield => ItemTier(ItemKind.Shield) == 0 ? 0 : Catalog.Item(ItemKind.Shield).power[ItemTier(ItemKind.Shield) - 1];
        public bool Dead => Health <= 0;
        public bool Defeated { get; private set; }
        public bool Night { get; private set; }
        public float PhaseTime { get; private set; }
        public float PhaseLength => Night ? Catalog.nightSeconds : Catalog.daySeconds;
        public float LightBlend => Night ? Mathf.Clamp01(Mathf.Min(PhaseTime, PhaseLength - PhaseTime) / 5) : 0;
        public int Wave { get; private set; }
        public int Score { get; private set; }
        public bool TimeoutPending { get; private set; }
        public bool TimeoutActive { get; private set; }
        public bool AutoAttack { get; private set; }
        public bool WearingHat { get; private set; }
        public ItemKind Weapon { get; private set; } = ItemKind.Pickaxe;
        public ItemKind? ActivePet { get; private set; }
        public Vector2 PetPosition { get; private set; }
        public float PetHealth { get; private set; }
        public float PetMaxHealth => 90 + PetLevel * 12 + PetEvolution * 75;
        public int PetExperience { get; private set; }
        public int PetLevel => 1 + PetExperience / 50;
        public int PetEvolution { get; private set; }
        public float HealRemaining => healRemaining;
        public Vector2 HealPoint => healPoint;
        public bool Protected => respawnProtection > 0;
        public event Action<Effect> EffectOccurred;

        public RunState(GameCatalog catalog, int seed) : this(catalog, seed, default) { }

        internal RunState(GameCatalog catalog, int seed, Price initialResources)
        {
            Catalog = catalog;
            Seed = seed;
            random = new System.Random(seed);
            counts = new int[catalog.buildings.Length];
            itemTiers = new int[catalog.items.Length];
            itemTiers[(int)ItemKind.Pickaxe] = 1;
            Resources = initialResources;
            Health = catalog.playerHealth;
            PhaseTime = catalog.daySeconds * .12f;
            camps = new Vector2[12];
            campTimers = new float[camps.Length];
            PopulateMap();
        }

        public int Count(BuildingKind kind) => counts[(int)kind];
        public int ItemTier(ItemKind kind) => itemTiers[(int)kind];
        public Structure At(Vector2 point) => occupied.GetValueOrDefault(Vector2Int.FloorToInt(point));
        public Vector2Int AnchorFor(BuildingKind kind, Vector2 point) =>
            Vector2Int.FloorToInt(point - Vector2.one * ((Catalog.Building(kind).size - 1) * .5f));
        public Vector2 CenterFor(BuildingKind kind, Vector2 point) =>
            (Vector2)AnchorFor(kind, point) + Vector2.one * (Catalog.Building(kind).size * .5f);

        public Placement CanPlace(BuildingKind kind, Vector2 point)
        {
            if (Dead || Defeated) return Placement.Dead;
            if (kind != BuildingKind.Generator && Generator == null) return Placement.NeedGenerator;
            var data = Catalog.Building(kind);
            if (Count(kind) >= data.limit) return Placement.Limit;
            Vector2Int anchor = AnchorFor(kind, point);
            Vector2 center = (Vector2)anchor + Vector2.one * (data.size * .5f);
            float halfWorld = Catalog.worldSize * .5f;
            if (Mathf.Abs(center.x) + data.size * .5f >= halfWorld || Mathf.Abs(center.y) + data.size * .5f >= halfWorld ||
                (Player - center).sqrMagnitude > Catalog.reach * Catalog.reach ||
                (Generator != null && kind != BuildingKind.Miner &&
                (Generator.Position - center).sqrMagnitude > Catalog.buildRadius * Catalog.buildRadius))
                return Placement.TooFar;
            for (int x = 0; x < data.size; x++)
            for (int y = 0; y < data.size; y++)
                if (occupied.ContainsKey(anchor + new Vector2Int(x, y)))
                    return Placement.Occupied;

            if (CircleHitsCell(Player, PlayerRadius, center, data.size * .5f) ||
                OverlapsResource(center, data.size * .5f) || OverlapsEnemy(center, data.size * .5f))
                return Placement.Occupied;
            return Resources.Covers(data.prices[0]) ? Placement.Allowed : Placement.Insufficient;
        }

        public bool Place(BuildingKind kind, Vector2 point, int rotation, out Placement result)
        {
            result = CanPlace(kind, point);
            if (result != Placement.Allowed) return false;
            var building = new Structure(Catalog.Building(kind), AnchorFor(kind, point), rotation);
            Resources -= building.Invested;
            buildings.Add(building);
            counts[(int)kind]++;
            for (int x = 0; x < building.Data.size; x++)
            for (int y = 0; y < building.Data.size; y++)
                occupied.Add(building.Anchor + new Vector2Int(x, y), building);
            if (kind == BuildingKind.Generator) Generator = building;
            Emit(EffectKind.Placed, building.Position);
            return true;
        }

        public bool InReach(Structure building) =>
            building != null && !Dead && !Defeated && At(building.Position) == building &&
            (Player - building.Position).sqrMagnitude <= Catalog.reach * Catalog.reach;

        public bool CanUpgrade(Structure building) =>
            InReach(building) && building.Tier < building.Data.prices.Length &&
            (building == Generator || (Generator != null && building.Tier < Generator.Tier)) &&
            Resources.Covers(building.Data.prices[building.Tier]);

        public int Upgrade(Structure building, bool bulk)
        {
            if (!CanUpgrade(building)) return 0;
            var oldTier = building.Tier;
            ApplyUpgrade(building);
            if (!bulk) return 1;
            int count = 1;
            foreach (var other in buildings)
            {
                if (other == building || other.Kind != building.Kind || other.Tier != oldTier || !CanUpgrade(other)) continue;
                ApplyUpgrade(other);
                count++;
            }
            return count;
        }

        private void ApplyUpgrade(Structure building)
        {
            Price cost = building.Data.prices[building.Tier];
            Resources -= cost;
            building.Invested += cost;
            float missing = building.MaximumHealth - building.Health;
            building.Tier++;
            building.Health = building.MaximumHealth - missing;
            Emit(EffectKind.Upgraded, building.Position);
        }

        public Price Refund(Structure building) => building.Invested * .5f;

        public bool Sell(Structure building)
        {
            if (!InReach(building) || building == Generator) return false;
            Resources += Refund(building);
            Remove(building);
            return true;
        }

        public Price RepairPrice(Structure building) =>
            new(gold: Mathf.Ceil((building.MaximumHealth - building.Health) * .2f));

        public bool Repair(Structure building)
        {
            if (!InReach(building) || building.Health >= building.MaximumHealth) return false;
            var cost = RepairPrice(building);
            if (!Resources.Covers(cost)) return false;
            Resources -= cost;
            building.Health = building.MaximumHealth;
            Emit(EffectKind.Impact, building.Position, "Repaired");
            return true;
        }

        public bool Deposit(Structure building)
        {
            if (!InReach(building) || building.Kind != BuildingKind.Miner) return false;
            float value = Mathf.Min(100, Mathf.Floor(Resources.gold),
                building.Data.Value(building.Data.fuelCapacity, building.Tier) - building.Fuel);
            if (value < 1) return false;
            Resources -= new Price(gold: value);
            building.Fuel += value;
            return true;
        }

        public bool Collect(Structure building)
        {
            if (!InReach(building) || building.Kind != BuildingKind.Miner || building.Stored <= 0) return false;
            float value = building.Stored;
            Resources += new Price(value, value);
            building.Stored = 0;
            Emit(EffectKind.Gain, Player, $"+{Mathf.FloorToInt(value)} wood / stone");
            return true;
        }

        public void Equip(ItemKind item)
        {
            if (item <= ItemKind.Bomb && ItemTier(item) > 0) Weapon = item;
        }

        public void CycleWeapon()
        {
            for (int i = 1; i <= 4; i++)
            {
                var item = (ItemKind)(((int)Weapon + i) % 4);
                if (ItemTier(item) == 0) continue;
                Weapon = item;
                return;
            }
        }

        public bool Buy(ItemKind item)
        {
            if (Dead || Defeated) return false;
            var data = Catalog.Item(item);
            int tier = ItemTier(item);
            if (item == ItemKind.HealthPotion) return HealPlayer();
            if (item == ItemKind.PetPotion) return HealPet();
            if (item is ItemKind.Carl or ItemKind.Woody)
            {
                if (ActivePet == item && PetHealth > 0) return false;
                if (petRespawnCooldown > 0) return false;
                if (ActivePet != item)
                {
                    PetExperience = 0;
                    PetEvolution = 0;
                }
                ActivePet = item;
                PetPosition = Player;
                PetHealth = PetMaxHealth;
                itemTiers[(int)item] = 1;
                return true;
            }
            if (item == ItemKind.Hat && tier > 0)
            {
                WearingHat = !WearingHat;
                return true;
            }
            if (tier >= data.prices.Length || !Resources.Covers(data.prices[tier])) return false;
            Resources -= data.prices[tier];
            itemTiers[(int)item]++;
            if (item == ItemKind.Shield) Shield = MaxShield;
            if (item == ItemKind.Hat) WearingHat = true;
            Emit(EffectKind.Upgraded, Player);
            return true;
        }

        public bool HealPlayer()
        {
            var price = Catalog.Item(ItemKind.HealthPotion).prices[0];
            if (Dead || Defeated || Health >= Catalog.playerHealth || potionCooldown > 0 || !Resources.Covers(price)) return false;
            Resources -= price;
            Health = Catalog.playerHealth;
            potionCooldown = Catalog.playerPotionCooldown;
            Emit(EffectKind.Impact, Player, "Healed");
            return true;
        }

        public bool HealPet()
        {
            var price = Catalog.Item(ItemKind.PetPotion).prices[0];
            if (Dead || Defeated || ActivePet == null || PetHealth <= 0 || PetHealth >= PetMaxHealth ||
                petPotionCooldown > 0 || !Resources.Covers(price)) return false;
            Resources -= price;
            PetHealth = PetMaxHealth;
            petPotionCooldown = Catalog.petPotionCooldown;
            return true;
        }

        public bool EvolvePet()
        {
            int cost = 100 * (1 + PetEvolution / 2);
            if (Dead || Defeated || ActivePet == null || PetHealth <= 0 || PetEvolution >= 3 ||
                PetLevel < 3 + PetEvolution * 3 || Resources.tokens < cost) return false;
            Resources -= new Price(tokens: cost);
            PetEvolution++;
            PetHealth = PetMaxHealth;
            Emit(EffectKind.Upgraded, PetPosition);
            return true;
        }

        public void Whistle()
        {
            if (PetHealth > 0 && !Dead && !Defeated) PetPosition = Player;
        }

        public bool Cast(SpellKind spell, Vector2 point)
        {
            if (Dead || Defeated) return false;
            if (spell == SpellKind.Timeout)
            {
                if (Generator == null || TimeoutPending || TimeoutActive || timeoutRecharge > 0 || !Resources.Covers(Catalog.timeoutPrice)) return false;
                Resources -= Catalog.timeoutPrice;
                TimeoutPending = true;
                timeoutRecharge = Catalog.timeoutCooldown;
                return true;
            }
            if (healRemaining > 0 || healRecharge > 0 || (Player - point).sqrMagnitude > Catalog.reach * Catalog.reach ||
                !Resources.Covers(Catalog.healPrice)) return false;
            Resources -= Catalog.healPrice;
            healPoint = point;
            healRemaining = Catalog.healDuration;
            healRecharge = Catalog.healCooldown;
            return true;
        }

        public bool Respawn()
        {
            if (!Dead || Defeated) return false;
            Player = Generator == null ? Vector2.zero : RespawnPoint(Generator.Position);
            Aim = Vector2.up;
            Health = Catalog.playerHealth;
            Shield = MaxShield;
            respawnProtection = Catalog.invulnerableSeconds;
            if (PetHealth > 0) PetPosition = Player;
            return true;
        }

        private Vector2 RespawnPoint(Vector2 origin)
        {
            for (int radius = 2; radius <= 7; radius++)
            for (int offset = 0; offset < 8; offset++)
            {
                Vector2 candidate = origin + Direction(offset * 45) * radius;
                float boundary = Catalog.worldSize * .5f - PlayerRadius;
                if (Mathf.Abs(candidate.x) > boundary || Mathf.Abs(candidate.y) > boundary ||
                    OverlapsResource(candidate, 0, PlayerRadius) || SolidBuilding(candidate, PlayerRadius, true) != null) continue;
                return candidate;
            }
            return origin;
        }

        public bool RecoverAfterDefeat()
        {
            if (!Defeated) return false;
            Resources = new Price(Mathf.Floor(Resources.wood * .75f), Mathf.Floor(Resources.stone * .75f),
                Mathf.Floor(Resources.gold * .75f), Resources.tokens);
            Array.Clear(itemTiers, 0, itemTiers.Length);
            itemTiers[(int)ItemKind.Pickaxe] = 1;
            Weapon = ItemKind.Pickaxe;
            WearingHat = false;
            ActivePet = null;
            PetHealth = 0;
            PetExperience = PetEvolution = 0;
            enemies.Clear();
            projectiles.Clear();
            Array.Clear(campTimers, 0, campTimers.Length);
            Wave = Score = 0;
            healRemaining = attackCooldown = potionCooldown = petPotionCooldown = playerRecovery = 0;
            shieldRecovery = healRecharge = timeoutRecharge = 0;
            nightHadWave = TimeoutActive = TimeoutPending = false;
            Defeated = false;
            Player = Vector2.zero;
            Health = Catalog.playerHealth;
            Shield = 0;
            respawnProtection = Catalog.invulnerableSeconds;
            return true;
        }

        public void ToggleAutoAttack() => AutoAttack = !AutoAttack;

        public void Step(Vector2 move, Vector2 aim, bool attack)
        {
            if (Defeated) return;
            const float dt = TickSeconds;
            attackCooldown -= dt;
            respawnProtection -= dt;
            potionCooldown -= dt;
            petPotionCooldown -= dt;
            petRespawnCooldown -= dt;
            playerRecovery -= dt;
            shieldRecovery -= dt;
            healRecharge -= dt;
            timeoutRecharge -= dt;
            if (!Dead)
            {
                Player = MovePlayer(Vector2.ClampMagnitude(move, 1) * (Catalog.playerSpeed * dt));
                Vector2 direction = aim - Player;
                if (direction.sqrMagnitude > .01f) Aim = direction.normalized;
                if ((attack || AutoAttack) && attackCooldown <= 0) Attack(aim);
                if (playerRecovery <= 0) Health = Mathf.Min(Catalog.playerHealth, Health + dt * 2);
                int shieldTier = ItemTier(ItemKind.Shield);
                if (shieldTier > 0 && shieldRecovery <= 0 && Shield < MaxShield)
                    Shield = Mathf.Min(MaxShield, Shield + Catalog.Item(ItemKind.Shield).recharge[shieldTier - 1] * dt);
            }
            AdvanceTime(dt);
            UpdateBuildings(dt);
            UpdatePet(dt);
            UpdateProjectiles(dt);
            UpdateEnemies(dt);
            foreach (var node in nodes)
                if (node.Flash > 0) node.Flash -= dt;
        }

        private void AdvanceTime(float dt)
        {
            PhaseTime += dt;
            if (PhaseTime >= PhaseLength)
            {
                PhaseTime -= PhaseLength;
                if (Night)
                {
                    if (nightHadWave) AddScore((long)Wave * 1000);
                    remainingSpawns = 0;
                    TimeoutActive = false;
                }
                Night = !Night;
                if (Night)
                {
                    nightHadWave = Generator != null;
                    if (nightHadWave)
                    {
                        Wave++;
                        TimeoutActive = TimeoutPending;
                        TimeoutPending = false;
                        nextSpawn = 0;
                        spawnClock = 1.4f;
                        remainingSpawns = TimeoutActive ? 0 : WaveCount(Wave);
                    }
                }
            }
            if (remainingSpawns == 0) return;
            spawnClock -= dt;
            if (spawnClock > 0) return;
            SpawnWaveEnemy();
            remainingSpawns--;
            spawnClock += Mathf.Min(Catalog.spawnInterval, Catalog.nightSeconds * .68f / WaveCount(Wave));
        }

        private int WaveCount(int wave) => Catalog.IsBossWave(wave) ? Catalog.bossCount :
            (int)Math.Min(Catalog.maximumWaveCount, Catalog.firstWaveCount + (long)(wave - 1) * Catalog.extraEnemiesPerWave);

        private void UpdateBuildings(float dt)
        {
            goldClock += dt;
            bool payMines = goldClock + .0001f >= 1;
            if (payMines) goldClock = Mathf.Max(0, goldClock - 1);
            healRemaining = Mathf.Max(0, healRemaining - dt);
            foreach (var building in buildings)
            {
                building.Cooldown -= dt;
                building.Recovery -= dt;
                if (building.Recovery <= 0)
                    building.Health = Mathf.Min(building.MaximumHealth, building.Health + dt * building.MaximumHealth * .012f);
                if (healRemaining > 0 && (building.Position - healPoint).sqrMagnitude < Catalog.healRadius * Catalog.healRadius)
                    building.Health = Mathf.Min(building.MaximumHealth, building.Health + dt * building.MaximumHealth * Catalog.healPerSecond);
                if (building.Kind == BuildingKind.Smelter && payMines)
                    Resources += new Price(gold: building.Data.Value(building.Data.income, building.Tier));
                if (building.Kind == BuildingKind.Miner) HarvestAutomatically(building, dt);
                if (building.Data.damage != null && building.Data.damage.Length != 0 && building.Cooldown <= 0)
                    FireTower(building);
            }
        }

        private static void HarvestAutomatically(Structure building, float dt)
        {
            if (building.Fuel <= 0 || building.Stored >= building.Data.Value(building.Data.capacity, building.Tier)) return;
            building.HarvestClock += dt;
            if (building.HarvestClock + .0001f < 1) return;
            building.HarvestClock = Mathf.Max(0, building.HarvestClock - 1);
            building.Fuel = Mathf.Max(0, building.Fuel - 1);
            building.Stored = Mathf.Min(building.Data.Value(building.Data.capacity, building.Tier),
                building.Stored + building.Data.Value(building.Data.income, building.Tier));
        }

        private void Remove(Structure building)
        {
            counts[(int)building.Kind]--;
            buildings.Remove(building);
            for (int x = 0; x < building.Data.size; x++)
            for (int y = 0; y < building.Data.size; y++)
                occupied.Remove(building.Anchor + new Vector2Int(x, y));
        }

        private void LoseGenerator()
        {
            Generator = null;
            buildings.Clear();
            occupied.Clear();
            Array.Clear(counts, 0, counts.Length);
            Defeated = true;
            AutoAttack = false;
            remainingSpawns = 0;
            Emit(EffectKind.Defeated, Player);
        }

        private Vector2 MovePlayer(Vector2 delta)
        {
            Vector2 position = Player;
            Vector2 horizontal = position + new Vector2(delta.x, 0);
            if (!Blocked(horizontal)) position = horizontal;
            Vector2 vertical = position + new Vector2(0, delta.y);
            if (!Blocked(vertical)) position = vertical;
            return position;
        }

        private bool Blocked(Vector2 point)
        {
            float boundary = Catalog.worldSize * .5f - PlayerRadius;
            if (Mathf.Abs(point.x) > boundary || Mathf.Abs(point.y) > boundary) return true;
            if (OverlapsResource(point, 0, PlayerRadius)) return true;
            return !Protected && SolidBuilding(point, PlayerRadius, true) != null;
        }

        private Structure SolidBuilding(Vector2 point, float radius, bool player)
        {
            Vector2Int min = Vector2Int.FloorToInt(point - Vector2.one * radius);
            Vector2Int max = Vector2Int.FloorToInt(point + Vector2.one * radius);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            {
                if (!occupied.TryGetValue(new Vector2Int(x, y), out var building) ||
                    building.Kind == BuildingKind.SlowTrap || (player && building.Kind == BuildingKind.Door))
                    continue;
                if (CircleHitsCell(point, radius, building.Position, building.Data.size * .5f)) return building;
            }
            return null;
        }

        private bool OverlapsResource(Vector2 point, float halfSize, float radius = 0)
        {
            Vector2Int bucket = ResourceBucket(point);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (!resourceBuckets.TryGetValue(bucket + new Vector2Int(x, y), out var nearby)) continue;
                foreach (var node in nearby)
                    if (CircleHitsCell(node.Position, node.Radius + radius, point, halfSize)) return true;
            }
            return false;
        }

        private static bool CircleHitsCell(Vector2 point, float radius, Vector2 center, float half)
        {
            Vector2 delta = point - center;
            float x = Mathf.Max(Mathf.Abs(delta.x) - half, 0);
            float y = Mathf.Max(Mathf.Abs(delta.y) - half, 0);
            return x * x + y * y < radius * radius;
        }

        private static Vector2Int ResourceBucket(Vector2 position) => Vector2Int.FloorToInt(position / ResourceBucketSize);

        private void PopulateMap()
        {
            AddNode(new Vector2(2.7f, -1.0f), false);
            AddNode(new Vector2(1.15f, 6.4f), true);
            AddNode(new Vector2(-4.8f, -7.5f), false);
            int half = Catalog.worldSize / 2;
            for (int x = -half + 5; x < half - 5; x += 7)
            for (int y = -half + 5; y < half - 5; y += 7)
            {
                if (random.NextDouble() < .37) continue;
                Vector2 point = new(x + Range(-2.8f, 2.8f), y + Range(-2.8f, 2.8f));
                if (point.sqrMagnitude < 83 || OverlapsResource(point, 1.4f)) continue;
                AddNode(point, random.NextDouble() < .39);
            }
            for (int i = 0; i < camps.Length; i++)
            {
                float angle = (i / (float)camps.Length) * Mathf.PI * 2;
                camps[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 32 : 64);
                while (OverlapsResource(camps[i], .8f)) camps[i] += new Vector2(1.3f, .75f);
            }
        }

        private void AddNode(Vector2 position, bool stone)
        {
            var node = new ResourceNode(position, stone);
            nodes.Add(node);
            Vector2Int bucket = ResourceBucket(position);
            if (!resourceBuckets.TryGetValue(bucket, out var list))
                resourceBuckets.Add(bucket, list = new List<ResourceNode>());
            list.Add(node);
        }

        private float Range(float from, float to) => Mathf.Lerp(from, to, (float)random.NextDouble());
        private void AddScore(long score) => Score = (int)Math.Min(int.MaxValue, Score + score);
        private void Emit(EffectKind kind, Vector2 point, string label = null) => EffectOccurred?.Invoke(new Effect(kind, point, label));
    }
}
