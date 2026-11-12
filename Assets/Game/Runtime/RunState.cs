using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    public enum Placement { Allowed, Dead, NeedGenerator, TierLocked, NeedsDeposit, Limit, TooFar, StarterSite, Occupied, Insufficient }
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
        public ResourceKind Kind { get; }
        public float Radius => Kind == ResourceKind.Wood ? 1.62f : 1.30f;
        public float Remaining { get; private set; }
        public bool Depleted => Remaining <= 0;
        public float Flash { get; internal set; }
        private readonly float initialAmount;
        internal bool WasGathered => Remaining != initialAmount;

        internal ResourceNode(Vector2 position, ResourceKind kind, float amount)
        {
            Position = position;
            Kind = kind;
            initialAmount = Remaining = amount;
        }

        internal float Extract(float amount)
        {
            float extracted = Mathf.Min(Remaining, amount);
            Remaining -= extracted;
            return extracted;
        }

        internal void Restore()
        {
            Remaining = initialAmount;
            Flash = 0;
        }

        internal bool RestoreRemaining(float amount)
        {
            if (!(amount >= 0 && amount <= initialAmount) || float.IsInfinity(amount)) return false;
            Remaining = amount;
            return true;
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
        public int Queued { get; internal set; }
        public float ProcessProgress { get; internal set; }
        public ResourceKind? DepositKind { get; internal set; }
        internal ResourceNode DepositNode;
        internal float Cooldown, Recovery;
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
        private float attackCooldown, healRemaining, playerRecovery, poisonClock;
        private float spawnClock, potionCooldown, petPotionCooldown, respawnProtection;
        private float petAttackCooldown, petRespawnCooldown, shieldRecovery, healRecharge, timeoutRecharge;
        private int remainingSpawns, nextSpawn, randomDraws;
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
        public float Breath { get; private set; }
        public bool GeneratorOnline => Generator != null && Generator.Fuel > 0;
        public Vector2 OxygenCenter => Generator?.Position ?? Vector2.zero;
        public float OxygenRadius => Generator == null ? Catalog.starterRadius :
            GeneratorOnline ? Generator.Data.Range(Generator.Tier) : 0;
        public bool CanBreathe => OxygenRadius > 0 &&
            (Player - OxygenCenter).sqrMagnitude <= OxygenRadius * OxygenRadius;
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
            Breath = catalog.breathSeconds;
            camps = new Vector2[12];
            campTimers = new float[camps.Length];
            PopulateMap();
        }

        public static string ResourceName(ResourceKind kind) => kind switch
        {
            ResourceKind.Wood => "wood",
            ResourceKind.Stone => "stone",
            ResourceKind.Iron => "iron ore",
            ResourceKind.Coal => "coal",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public int Count(BuildingKind kind) => counts[(int)kind];
        public int ItemTier(ItemKind kind) => itemTiers[(int)kind];
        public Structure At(Vector2 point) => occupied.GetValueOrDefault(Vector2Int.FloorToInt(point));
        public ResourceNode ResourceAt(Vector2 point)
        {
            Vector2Int bucket = ResourceBucket(point);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (!resourceBuckets.TryGetValue(bucket + new Vector2Int(x, y), out var nearby)) continue;
                foreach (var node in nearby)
                    if (!node.Depleted && (node.Position - point).sqrMagnitude <= node.Radius * node.Radius)
                        return node;
            }
            return null;
        }

        public Vector2Int AnchorFor(BuildingKind kind, Vector2 point) =>
            Vector2Int.FloorToInt(point - Vector2.one * ((Catalog.Building(kind).size - 1) * .5f));
        public Vector2 CenterFor(BuildingKind kind, Vector2 point) =>
            (Vector2)AnchorFor(kind, point) + Vector2.one * (Catalog.Building(kind).size * .5f);

        public Placement CanPlace(BuildingKind kind, Vector2 point)
        {
            if (Dead || Defeated) return Placement.Dead;
            if (kind != BuildingKind.Generator && Generator == null) return Placement.NeedGenerator;
            var data = Catalog.Building(kind);
            if (kind != BuildingKind.Generator && Generator.Tier < data.requiredGeneratorTier) return Placement.TierLocked;
            if (Count(kind) >= data.limit) return Placement.Limit;
            Vector2Int anchor = AnchorFor(kind, point);
            Vector2 center = (Vector2)anchor + Vector2.one * (data.size * .5f);
            float halfWorld = Catalog.worldSize * .5f;
            if (Mathf.Abs(center.x) + data.size * .5f >= halfWorld || Mathf.Abs(center.y) + data.size * .5f >= halfWorld ||
                (Player - center).sqrMagnitude > Catalog.reach * Catalog.reach ||
                (Generator != null && kind != BuildingKind.Miner &&
                (Generator.Position - center).sqrMagnitude > Catalog.buildRadius * Catalog.buildRadius))
                return Placement.TooFar;
            if (kind == BuildingKind.Generator && center.sqrMagnitude > Catalog.generatorSiteRadius * Catalog.generatorSiteRadius)
                return Placement.StarterSite;
            for (int x = 0; x < data.size; x++)
            for (int y = 0; y < data.size; y++)
                if (occupied.ContainsKey(anchor + new Vector2Int(x, y)))
                    return Placement.Occupied;

            if (CircleHitsCell(Player, PlayerRadius, center, data.size * .5f) ||
                OverlapsResource(center, data.size * .5f) || OverlapsEnemy(center, data.size * .5f))
                return Placement.Occupied;
            if (kind == BuildingKind.Miner && FindDeposit(center) == null) return Placement.NeedsDeposit;
            return Resources.Covers(data.prices[0]) ? Placement.Allowed : Placement.Insufficient;
        }

        public bool Place(BuildingKind kind, Vector2 point, int rotation, out Placement result)
        {
            result = CanPlace(kind, point);
            if (result != Placement.Allowed) return false;
            var building = new Structure(Catalog.Building(kind), AnchorFor(kind, point), rotation);
            Resources -= building.Invested;
            AddBuilding(building);
            if (kind == BuildingKind.Generator)
            {
                building.Fuel = Mathf.Min(Catalog.initialFuel, FuelCapacity(building));
                PhaseTime = 0;
                Night = false;
            }
            if (kind == BuildingKind.Miner)
            {
                building.DepositNode = FindDeposit(building.Position);
                building.DepositKind = building.DepositNode.Kind;
            }
            Emit(EffectKind.Placed, building.Position);
            return true;
        }

        private void AddBuilding(Structure building)
        {
            buildings.Add(building);
            counts[(int)building.Kind]++;
            if (building.Kind == BuildingKind.Generator) Generator = building;
            for (int x = 0; x < building.Data.size; x++)
            for (int y = 0; y < building.Data.size; y++)
                occupied.Add(building.Anchor + new Vector2Int(x, y), building);
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
            Collect(building);
            if (building.Queued > 0)
                Resources += Catalog.RecipeFor(building.Kind).input * building.Queued;
            Resources += Refund(building);
            Remove(building);
            return true;
        }

        public Price RepairPrice(Structure building) =>
            new(stone: Mathf.Ceil((1 - building.Health / building.MaximumHealth) * (8 + 2 * building.Tier)));

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

        public float FuelCapacity(Structure building) => building.Data.Value(building.Data.fuelCapacity, building.Tier);

        public float FuelRate(Structure building) => building.Kind == BuildingKind.Miner ? Mathf.Max(0, Catalog.minerFuelPerSecond) : 1;

        internal float FuelValue(ResourceKind kind) => kind switch
        {
            ResourceKind.Wood => Catalog.woodFuelSeconds,
            ResourceKind.Coal => Catalog.coalFuelSeconds,
            _ => 0
        };

        internal int FuelLoad(Structure building, ResourceKind kind)
        {
            if (!InReach(building) || building.Kind is not (BuildingKind.Generator or BuildingKind.Miner)) return 0;
            float perUnit = FuelValue(kind);
            if (perUnit <= 0) return 0;
            float available = kind == ResourceKind.Wood ? Resources.wood : Resources.coal;
            float room = FuelCapacity(building) - building.Fuel;
            return Mathf.Max(0, Mathf.Min(5, Mathf.FloorToInt(available), Mathf.FloorToInt((room + .001f) / perUnit)));
        }

        public bool Refuel(Structure building, ResourceKind kind)
        {
            int amount = FuelLoad(building, kind);
            if (amount < 1) return false;
            Resources -= Price.Gathered(kind, amount);
            building.Fuel += amount * FuelValue(kind);
            return true;
        }

        internal int ProductionBatchSize(Structure building) => PlanProduction(building, out _);

        public int QueueProduction(Structure building)
        {
            int count = PlanProduction(building, out Price remaining);
            if (count == 0) return 0;
            Resources = remaining;
            building.Queued += count;
            return count;
        }

        private int PlanProduction(Structure building, out Price remaining)
        {
            remaining = Resources;
            if (!InReach(building)) return 0;
            var recipe = Catalog.RecipeFor(building.Kind);
            if (recipe == null) return 0;
            int room = Mathf.FloorToInt(building.Data.Value(building.Data.capacity, building.Tier)
                - building.Stored - building.Queued);
            int count = 0;
            while (count < Mathf.Min(5, room) && remaining.Covers(recipe.input))
            {
                remaining -= recipe.input;
                count++;
            }
            return count;
        }

        public bool Collect(Structure building)
        {
            if (!InReach(building) || building.Stored <= 0) return false;
            if (building.Kind == BuildingKind.Miner && building.DepositKind.HasValue)
                Resources += Price.Gathered(building.DepositKind.Value, building.Stored);
            else if (Catalog.RecipeFor(building.Kind) is Recipe recipe)
                Resources += recipe.output * building.Stored;
            else return false;
            building.Stored = 0;
            return true;
        }

        private ResourceNode FindDeposit(Vector2 center, ResourceKind? kind = null)
        {
            Vector2Int bucket = ResourceBucket(center);
            ResourceNode best = null;
            float nearest = Catalog.minerReach * Catalog.minerReach;
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (!resourceBuckets.TryGetValue(bucket + new Vector2Int(x, y), out var nearby)) continue;
                foreach (var node in nearby)
                {
                    if (node.Depleted || node.Kind == ResourceKind.Wood || (kind.HasValue && node.Kind != kind.Value)) continue;
                    float distance = (node.Position - center).sqrMagnitude;
                    if (distance > nearest) continue;
                    nearest = distance;
                    best = node;
                }
            }
            return best;
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

        internal bool CanHealPlayer => !Dead && !Defeated && Health < Catalog.playerHealth && potionCooldown <= 0 &&
            Resources.Covers(Catalog.Item(ItemKind.HealthPotion).prices[0]);

        public bool HealPlayer()
        {
            if (!CanHealPlayer) return false;
            Resources -= Catalog.Item(ItemKind.HealthPotion).prices[0];
            Health = Catalog.playerHealth;
            potionCooldown = Catalog.playerPotionCooldown;
            Emit(EffectKind.Impact, Player, "Healed");
            return true;
        }

        internal bool CanHealPet => !Dead && !Defeated && ActivePet.HasValue && PetHealth > 0 && PetHealth < PetMaxHealth &&
            petPotionCooldown <= 0 && Resources.Covers(Catalog.Item(ItemKind.PetPotion).prices[0]);

        public bool HealPet()
        {
            if (!CanHealPet) return false;
            Resources -= Catalog.Item(ItemKind.PetPotion).prices[0];
            PetHealth = PetMaxHealth;
            petPotionCooldown = Catalog.petPotionCooldown;
            return true;
        }

        internal int PetEvolutionPrice => 100 * (1 + PetEvolution / 2);
        internal int PetEvolutionLevel => 3 + PetEvolution * 3;
        internal bool CanEvolvePet => !Dead && !Defeated && ActivePet.HasValue && PetHealth > 0 && PetEvolution < 3 &&
            PetLevel >= PetEvolutionLevel && Resources.tokens >= PetEvolutionPrice;

        public bool EvolvePet()
        {
            if (!CanEvolvePet) return false;
            Resources -= new Price(tokens: PetEvolutionPrice);
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
            Breath = Catalog.breathSeconds;
            poisonClock = 0;
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
            int tokens = Resources.tokens;
            Price retained = Resources * .75f;
            retained.tokens = tokens;
            Resources = retained;
            foreach (var node in nodes) node.Restore();
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
            PhaseTime = 0;
            Night = false;
            healRemaining = attackCooldown = potionCooldown = petPotionCooldown = playerRecovery = 0;
            shieldRecovery = healRecharge = timeoutRecharge = 0;
            nightHadWave = TimeoutActive = TimeoutPending = false;
            Defeated = false;
            Player = Vector2.zero;
            Health = Catalog.playerHealth;
            Breath = Catalog.breathSeconds;
            poisonClock = 0;
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
            if (GeneratorOnline) Generator.Fuel = Mathf.Max(0, Generator.Fuel - dt);
            if (!Dead)
            {
                Player = MovePlayer(Vector2.ClampMagnitude(move, 1) * (Catalog.playerSpeed * dt));
                Vector2 direction = aim - Player;
                if (direction.sqrMagnitude > .01f) Aim = direction.normalized;
                UpdateOxygen(dt);
                if (!Dead && (attack || AutoAttack) && attackCooldown <= 0) Attack(aim);
                if (CanBreathe && playerRecovery <= 0) Health = Mathf.Min(Catalog.playerHealth, Health + dt * 2);
                int shieldTier = ItemTier(ItemKind.Shield);
                if (shieldTier > 0 && shieldRecovery <= 0 && Shield < MaxShield)
                    Shield = Mathf.Min(MaxShield, Shield + Catalog.Item(ItemKind.Shield).recharge[shieldTier - 1] * dt);
            }
            if (Generator != null) AdvanceTime(dt);
            UpdateBuildings(dt);
            UpdatePet(dt);
            UpdateProjectiles(dt);
            UpdateEnemies(dt);
            foreach (var node in nodes)
                if (node.Flash > 0) node.Flash -= dt;
        }

        private void UpdateOxygen(float dt)
        {
            if (CanBreathe)
            {
                Breath = Mathf.Min(Catalog.breathSeconds,
                    Breath + dt * Catalog.breathSeconds / Mathf.Max(TickSeconds, Catalog.breathRefillSeconds));
                poisonClock = 0;
                return;
            }

            float exposure = Mathf.Max(0, dt - Breath);
            Breath = Mathf.Max(0, Breath - dt);
            if (Protected || exposure <= 0) return;
            Health = Mathf.Max(0, Health - Catalog.poisonDamagePerSecond * exposure);
            playerRecovery = Mathf.Max(playerRecovery, 3);
            if (Dead) AutoAttack = false;
            poisonClock += exposure;
            if (poisonClock >= 1 || Dead)
            {
                poisonClock %= 1;
                Emit(EffectKind.Damage, Player, "Poison");
            }
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
            healRemaining = Mathf.Max(0, healRemaining - dt);
            foreach (var building in buildings)
            {
                building.Cooldown -= dt;
                building.Recovery -= dt;
                if (building.Recovery <= 0)
                    building.Health = Mathf.Min(building.MaximumHealth, building.Health + dt * building.MaximumHealth * .012f);
                if (healRemaining > 0 && (building.Position - healPoint).sqrMagnitude < Catalog.healRadius * Catalog.healRadius)
                    building.Health = Mathf.Min(building.MaximumHealth, building.Health + dt * building.MaximumHealth * Catalog.healPerSecond);
                if (building.Kind == BuildingKind.Miner) UpdateMiner(building, dt);
                else if (building.Queued > 0) UpdateProduction(building, dt);
                if (building.Data.damage != null && building.Data.damage.Length != 0 && building.Cooldown <= 0)
                    FireTower(building);
            }
        }

        private void UpdateMiner(Structure building, float dt)
        {
            if (building.Fuel <= 0 || !building.DepositKind.HasValue) return;
            float capacity = building.Data.Value(building.Data.capacity, building.Tier);
            if (building.Stored >= capacity) return;
            if (building.DepositNode == null || building.DepositNode.Depleted)
                building.DepositNode = FindDeposit(building.Position, building.DepositKind);
            if (building.DepositNode == null) return;

            float rate = building.Data.Value(building.Data.income, building.Tier);
            if (rate <= 0) return;
            float fuelRate = FuelRate(building);
            float activeTime = fuelRate > 0 ? Mathf.Min(dt, building.Fuel / fuelRate) : dt;
            float extracted = building.DepositNode.Extract(Mathf.Min(capacity - building.Stored, rate * activeTime));
            building.Stored += extracted;
            building.Fuel = Mathf.Max(0, building.Fuel - extracted / rate * fuelRate);
        }

        private void UpdateProduction(Structure building, float dt)
        {
            var recipe = Catalog.RecipeFor(building.Kind);
            float duration = Mathf.Max(TickSeconds, recipe.seconds / (1 + (building.Tier - 1) * .2f));
            building.ProcessProgress += dt;
            if (building.ProcessProgress + .0001f < duration) return;
            building.ProcessProgress = Mathf.Max(0, building.ProcessProgress - duration);
            building.Queued--;
            building.Stored++;
            if (building.Queued == 0) building.ProcessProgress = 0;
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
                    if (!node.Depleted && CircleHitsCell(node.Position, node.Radius + radius, point, halfSize)) return true;
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
            var terrain = new System.Random(unchecked(Seed ^ 0x50C251E3));
            // Keep emergency wood near the center, with room to place the generator and defenses.
            AddNode(new Vector2(2.7f, -1), ResourceKind.Wood, float.PositiveInfinity);
            AddNode(new Vector2(1.15f, 6.4f), ResourceKind.Stone, 160);
            AddNode(new Vector2(-4.8f, -7.5f), ResourceKind.Wood, 160);
            AddVein(new Vector2(14, 6), ResourceKind.Coal, 5, 75, terrain);
            AddVein(new Vector2(14, -10), ResourceKind.Iron, 5, 90, terrain);
            AddVein(new Vector2(-16, 10), ResourceKind.Stone, 5, 90, terrain);
            AddVein(new Vector2(-15, -12), ResourceKind.Wood, 5, 100, terrain);

            int half = Catalog.worldSize / 2;
            for (int x = -half + 12; x < half - 12; x += 23)
            for (int y = -half + 12; y < half - 12; y += 23)
            {
                var center = new Vector2(x + terrain.Next(-7, 8), y + terrain.Next(-7, 8));
                if (center.sqrMagnitude < 34 * 34 || terrain.NextDouble() < .25) continue;
                var kind = (ResourceKind)terrain.Next(4);
                int richness = 90 + Mathf.RoundToInt(center.magnitude * .7f);
                AddVein(center, kind, terrain.Next(4, 10), richness, terrain);
            }
            for (int i = 0; i < camps.Length; i++)
            {
                float angle = (i / (float)camps.Length) * Mathf.PI * 2;
                camps[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 32 : 64);
                while (OverlapsResource(camps[i], .8f)) camps[i] += new Vector2(1.3f, .75f);
            }
        }

        private void AddVein(Vector2 center, ResourceKind kind, int count, int richness, System.Random terrain)
        {
            var frontier = new List<Vector2Int> { Vector2Int.zero };
            var visited = new HashSet<Vector2Int> { Vector2Int.zero };
            float half = Catalog.worldSize * .5f - 2;
            float spacing = kind == ResourceKind.Wood ? 3.65f : 2.95f;
            int added = 0;
            while (frontier.Count > 0 && added < count && visited.Count < count * 12)
            {
                int index = terrain.Next(frontier.Count);
                Vector2Int cell = frontier[index];
                frontier.RemoveAt(index);
                Vector2 point = center + (Vector2)cell * spacing;
                if (Mathf.Abs(point.x) >= half || Mathf.Abs(point.y) >= half ||
                    point.sqrMagnitude < 11 * 11 || OverlapsResource(point, kind == ResourceKind.Wood ? 1.65f : 1.32f))
                    continue;
                AddNode(point, kind, terrain.Next(richness * 3 / 4, richness * 5 / 4 + 1));
                added++;
                foreach (var adjacent in VeinDirections)
                {
                    var next = cell + adjacent;
                    if (visited.Add(next)) frontier.Add(next);
                }
            }
        }

        private static readonly Vector2Int[] VeinDirections =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        private void AddNode(Vector2 position, ResourceKind kind, float amount)
        {
            var node = new ResourceNode(position, kind, amount);
            nodes.Add(node);
            Vector2Int bucket = ResourceBucket(position);
            if (!resourceBuckets.TryGetValue(bucket, out var list))
                resourceBuckets.Add(bucket, list = new List<ResourceNode>());
            list.Add(node);
        }

        private float Range(float from, float to)
        {
            randomDraws++;
            return Mathf.Lerp(from, to, (float)random.NextDouble());
        }
        private void AddScore(long score) => Score = (int)Math.Min(int.MaxValue, Score + score);
        private void Emit(EffectKind kind, Vector2 point, string label = null) => EffectOccurred?.Invoke(new Effect(kind, point, label));
    }
}
