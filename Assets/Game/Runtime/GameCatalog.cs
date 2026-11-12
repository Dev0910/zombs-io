using System;
using UnityEngine;

namespace Zombs
{
    public enum BuildingKind { Wall, Door, SlowTrap, Arrow, Cannon, Melee, Bomb, Mage, Smelter, Miner, Generator, Assembler }
    public enum ResourceKind { Wood, Stone, Iron, Coal }
    public enum ItemKind { Pickaxe, Spear, Bow, Bomb, Shield, HealthPotion, PetPotion, Carl, Woody, Hat }
    public enum SpellKind { HealTowers, Timeout }

    [Serializable]
    public struct Price
    {
        public float wood, stone, iron, coal, plate, parts;
        public int tokens;

        public Price(float wood = 0, float stone = 0, float iron = 0, float coal = 0,
            float plate = 0, float parts = 0, int tokens = 0)
        {
            this.wood = wood;
            this.stone = stone;
            this.iron = iron;
            this.coal = coal;
            this.plate = plate;
            this.parts = parts;
            this.tokens = tokens;
        }

        public static Price Gathered(ResourceKind kind, float amount) => kind switch
        {
            ResourceKind.Wood => new Price(wood: amount),
            ResourceKind.Stone => new Price(stone: amount),
            ResourceKind.Iron => new Price(iron: amount),
            ResourceKind.Coal => new Price(coal: amount),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public bool Covers(Price price) =>
            wood + .0001f >= price.wood && stone + .0001f >= price.stone &&
            iron + .0001f >= price.iron && coal + .0001f >= price.coal &&
            plate + .0001f >= price.plate && parts + .0001f >= price.parts && tokens >= price.tokens;

        public static Price operator +(Price left, Price right) =>
            new(left.wood + right.wood, left.stone + right.stone, left.iron + right.iron, left.coal + right.coal,
                left.plate + right.plate, left.parts + right.parts, left.tokens + right.tokens);

        public static Price operator -(Price left, Price right) =>
            new(Mathf.Max(0, left.wood - right.wood), Mathf.Max(0, left.stone - right.stone),
                Mathf.Max(0, left.iron - right.iron), Mathf.Max(0, left.coal - right.coal),
                Mathf.Max(0, left.plate - right.plate), Mathf.Max(0, left.parts - right.parts),
                Mathf.Max(0, left.tokens - right.tokens));

        public static Price operator *(Price price, float multiplier) =>
            new(Mathf.Floor(price.wood * multiplier), Mathf.Floor(price.stone * multiplier),
                Mathf.Floor(price.iron * multiplier), Mathf.Floor(price.coal * multiplier),
                Mathf.Floor(price.plate * multiplier), Mathf.Floor(price.parts * multiplier),
                Mathf.FloorToInt(price.tokens * multiplier));
    }

    [Serializable]
    public sealed class Recipe
    {
        public Price input, output;
        public string outputName;
        public float seconds = 2;
    }

    [Serializable]
    public sealed class BuildingData
    {
        public BuildingKind kind;
        public string title, description;
        public int size = 2, limit = 6, requiredGeneratorTier = 1;
        public Price[] prices;
        public float[] health, damage, income, capacity, fuelCapacity, ranges;
        public float range, interval, splash;
        public float Value(float[] values, int tier) => values == null || values.Length == 0 ? 0 : values[tier - 1];
        public float Range(int tier) => ranges == null || ranges.Length == 0 ? range : ranges[tier - 1];
    }

    [Serializable]
    public sealed class ItemData
    {
        public ItemKind kind;
        public string title, description;
        public Price[] prices;
        public float[] power, recovery, recharge;
        public float interval, range, splash;
    }

    [Serializable]
    public sealed class ZombieData
    {
        public Color color;
        public int firstWave;
        public float health, damage, speed, score;
    }

    [CreateAssetMenu(menuName = "Zombs/Game Catalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        public BuildingData[] buildings;
        public ItemData[] items;
        public ZombieData[] zombies;
        public int worldSize = 420;
        public float buildRadius = 18, reach = 12, playerSpeed = 7.6f, playerHealth = 100;
        public float daySeconds = 60, nightSeconds = 60, spawnRadius = 13, spawnInterval = 1.35f;
        public int firstBossWave = 9, bossInterval = 8, lastBossWave = 121, bossCount = 1, bossTokens = 100;
        public float bossHealth = 4800, bossDamage = 42, bossScore = 1000;
        public int firstWaveCount = 14, extraEnemiesPerWave = 6, maximumWaveCount = 85;
        public float[] harvestYield = { 1.5f, 3, 3, 4.5f, 4.5f, 6, 9 };
        public float starterRadius = 8, generatorSiteRadius = 5, initialFuel = 90;
        public float breathSeconds = 5, breathRefillSeconds = 1.5f, poisonDamagePerSecond = 8;
        public float woodFuelSeconds = 6, coalFuelSeconds = 20, minerReach = 3.5f, minerFuelPerSecond = .25f;
        public Price healPrice = new(stone: 10, plate: 2), timeoutPrice = new(coal: 10, parts: 8);
        public Recipe smelting = new() { input = new Price(iron: 2), output = new Price(plate: 1), outputName = "plate" };
        public Recipe assembly = new() { input = new Price(wood: 1, plate: 1), output = new Price(parts: 1), outputName = "part" };
        public float healRadius = 12, healDuration = 10, healPerSecond = .10f;
        public float healCooldown = 240, timeoutCooldown = 240;
        public float playerPotionCooldown = 15, petPotionCooldown = 10, invulnerableSeconds = 3;

        public BuildingData Building(BuildingKind kind) => buildings[(int)kind];
        public Recipe RecipeFor(BuildingKind kind) => kind switch
        {
            BuildingKind.Smelter => smelting,
            BuildingKind.Assembler => assembly,
            _ => null
        };
        public ItemData Item(ItemKind kind) => items[(int)kind];
        public bool IsBossWave(int wave) => wave >= firstBossWave && wave <= lastBossWave && (wave - firstBossWave) % bossInterval == 0;

        public ZombieData Zombie(int wave)
        {
            for (int index = zombies.Length - 1; index > 0; index--)
                if (wave >= zombies[index].firstWave)
                    return zombies[index];
            return zombies[0];
        }

        public static GameCatalog CreateDefaults()
        {
            var catalog = CreateInstance<GameCatalog>();
            catalog.buildings = new[]
            {
                Structure(BuildingKind.Wall, "Wall", "Blocks enemies from reaching your towers.", 1, 250, 2, 0, 150, 250),
                Structure(BuildingKind.Door, "Door", "Lets you walk through your defenses.", 1, 40, 5, 5, 100, 180),
                Structure(BuildingKind.SlowTrap, "Slow Trap", "Slows enemies entering your base.", 1, 12, 5, 5, 100, 150),
                Tower(BuildingKind.Arrow, "Arrow Tower", "Fast single-target shots.", 5, 150, 600f / 84, .52f, 20),
                Tower(BuildingKind.Cannon, "Cannon Tower", "Slow shots with area damage.", 15, 180, 6.6f, 1.2f, 35, 1.6f),
                Tower(BuildingKind.Melee, "Melee Tower", "High damage in one direction. Press R to rotate.", 10, 240, 3f, .62f, 51),
                Tower(BuildingKind.Bomb, "Bomb Tower", "Large explosions at a slow rate of fire.", 10, 150, 1000f / 84, 2.15f, 40, 2.5f),
                Tower(BuildingKind.Mage, "Mage Tower", "Fires multiple projectiles at close range.", 15, 170, 4.8f, .67f, 21),
                Structure(BuildingKind.Smelter, "Smelter", "Turns iron ore into plates.", 2, 4, 15, 15, 210, 320),
                Structure(BuildingKind.Miner, "Miner", "Fuel it to extract a nearby deposit.", 2, 6, 20, 15, 300, 450),
                Structure(BuildingKind.Generator, "Oxygen Generator", "Burns fuel to make air safe.", 2, 1, 0, 0, 1500, 1800),
                Structure(BuildingKind.Assembler, "Assembler", "Makes parts from plates and wood.", 2, 4, 15, 10, 210, 320)
            };
            var generator = catalog.Building(BuildingKind.Generator);
            generator.prices = new[] { default, new Price(20, 20, parts: 6), new Price(30, 30, parts: 12),
                new Price(45, 40, parts: 20), new Price(65, 55, parts: 32), new Price(85, 75, parts: 48),
                new Price(115, 100, parts: 70), new Price(150, 130, parts: 100) };
            generator.health = new float[] { 1500, 1800, 2300, 2800, 3600, 4800, 6500, 9000 };
            generator.ranges = new float[] { 12, 16, 20, 23, 26, 29, 32, 35 };
            generator.fuelCapacity = new float[] { 180, 240, 300, 360, 420, 480, 540, 600 };

            var arrow = catalog.Building(BuildingKind.Arrow);
            arrow.health[1] = 200;
            arrow.damage[1] = 40;
            arrow.ranges = Array.ConvertAll(new[] { 600, 650, 700, 750, 800, 850, 900, 950 }, value => value / 84f);
            var bomb = catalog.Building(BuildingKind.Bomb);
            bomb.health[1] = 200;
            bomb.health[2] = 400;
            bomb.damage[1] = 60;
            bomb.damage[2] = 100;

            var miner = catalog.Building(BuildingKind.Miner);
            miner.requiredGeneratorTier = 2;
            miner.prices[0] = new Price(20, 15, parts: 3);
            miner.income = new[] { .12f, .18f, .25f, .33f, .42f, .52f, .64f, .8f };
            miner.capacity = new float[] { 24, 32, 48, 64, 80, 96, 112, 128 };
            miner.fuelCapacity = new float[] { 45, 60, 75, 90, 105, 120, 135, 150 };
            catalog.Building(BuildingKind.Assembler).prices[0] = new Price(15, 10, plate: 2);
            foreach (var kind in new[] { BuildingKind.Smelter, BuildingKind.Assembler })
                catalog.Building(kind).capacity = new float[] { 10, 15, 20, 25, 30, 35, 40, 50 };

            catalog.items = new[]
            {
                Item(ItemKind.Pickaxe, "Pickaxe", "Melee weapon and harvesting tool.", Equipment(default, 3, 6, 10, 16, 24, 36),
                    new float[] { 20, 20, 25, 30, 35, 42, 52 }, .30f, 1.65f),
                Item(ItemKind.Spear, "Spear", "Longer reach and quick thrusts.", Equipment(new Price(12, 10), 4, 8, 13, 20, 30, 44),
                    new float[] { 30, 37, 45, 53, 65, 82, 105 }, .25f, 2.55f),
                Item(ItemKind.Bow, "Bow", "Fires an arrow in the direction you aim.", Equipment(new Price(10, 5), 3, 7, 12, 20, 30, 42),
                    new float[] { 20, 28, 40, 55, 76, 100, 136 }, .5f, 12),
                Item(ItemKind.Bomb, "Bomb", "Throws an explosive that damages nearby enemies.", Equipment(new Price(stone: 8, coal: 2), 4, 8, 14, 22, 32, 45),
                    new float[] { 10, 16, 25, 39, 55, 80, 110 }, .5f, 17.5f, 2.1f),
                Item(ItemKind.Shield, "Shield", "Absorbs damage from zombies.", Equipment(new Price(stone: 8, plate: 2), 4, 7, 12, 18, 24, 32, 42, 54, 70),
                    new float[] { 500, 1000, 1800, 4000, 10000, 20000, 35000, 50000, 65000, 85000 }),
                Item(ItemKind.HealthPotion, "Health Potion", "Heals you to full health. Press F to use.", new[] { new Price(wood: 5, stone: 5) }, new[] { 100f }),
                Item(ItemKind.PetPotion, "Pet Potion", "Heals your active pet to full health.", new[] { new Price(wood: 5, stone: 5) }, new[] { 100f }),
                Item(ItemKind.Carl, "C. A. R. L", "Fights by your side in close-range combat.", new Price[1], new[] { 16f }),
                Item(ItemKind.Woody, "Woody", "Helps harvest the resource you attack.", new Price[1], new[] { 1.5f }),
                Item(ItemKind.Hat, "Horns", "Equip the decorative horns.", new Price[1], new[] { 0f })
            };
            catalog.Item(ItemKind.Shield).recovery = new float[] { 10, 9, 8, 7, 6, 6, 6, 6, 6, 6 };
            catalog.Item(ItemKind.Shield).recharge = new float[] { 50, 100, 200, 400, 1000, 2000, 3500, 5000, 6500, 8500 };
            catalog.zombies = new[]
            {
                Enemy(new Color32(91, 135, 71, 255), 1, 62, 7, 3.9f, 100),
                Enemy(new Color32(106, 142, 172, 255), 9, 105, 10, 4.1f, 125),
                Enemy(new Color32(224, 189, 72, 255), 17, 178, 15, 4.2f, 150),
                Enemy(new Color32(187, 77, 74, 255), 33, 280, 22, 4.35f, 175),
                Enemy(new Color32(151, 95, 175, 255), 57, 460, 30, 4.5f, 200),
                Enemy(new Color32(220, 137, 67, 255), 89, 720, 39, 4.6f, 250)
            };
            return catalog;
        }

        private static BuildingData Structure(BuildingKind kind, string title, string description, int size, int limit,
            int wood, int stone, float firstHealth, float secondHealth)
        {
            var prices = new Price[8];
            var health = new float[8];
            prices[0] = new Price(wood, stone);
            health[0] = firstHealth;
            int[] material = { 0, 12, 20, 30, 42, 58, 76, 100 };
            int[] parts = { 0, 2, 4, 7, 10, 15, 22, 32 };
            for (int tier = 1; tier < 8; tier++)
            {
                float scale = size == 1 ? .4f : 1;
                prices[tier] = new Price(Mathf.Ceil(material[tier] * scale), stone == 0 ? 0 : Mathf.Ceil(material[tier] * scale),
                    parts: Mathf.Ceil(parts[tier] * scale));
                health[tier] = Mathf.Round(secondHealth * Mathf.Pow(1.55f, tier - 1));
            }
            return new BuildingData { kind = kind, title = title, description = description, size = size, limit = limit,
                prices = prices, health = health };
        }

        private static BuildingData Tower(BuildingKind kind, string title, string description, int cost, float health,
            float range, float interval, float damage, float splash = 0)
        {
            var data = Structure(kind, title, description, 2, 6, cost, cost, health, health * 1.65f);
            data.range = range;
            data.interval = interval;
            data.splash = splash;
            data.damage = new float[8];
            for (int tier = 0; tier < 8; tier++)
                data.damage[tier] = Mathf.Round(damage * Mathf.Pow(1.55f, tier));
            return data;
        }

        private static ItemData Item(ItemKind kind, string title, string description, Price[] prices,
            float[] power, float interval = 0, float range = 0, float splash = 0) =>
            new() { kind = kind, title = title, description = description, prices = prices, power = power,
                interval = interval, range = range, splash = splash };

        private static ZombieData Enemy(Color color, int firstWave, float health, float damage, float speed, float score) =>
            new() { color = color, firstWave = firstWave, health = health, damage = damage, speed = speed, score = score };

        private static Price[] Equipment(Price starter, params int[] parts)
        {
            var prices = new Price[parts.Length + 1];
            prices[0] = starter;
            for (int i = 0; i < parts.Length; i++)
                prices[i + 1] = new Price(parts: parts[i]);
            return prices;
        }
    }
}
