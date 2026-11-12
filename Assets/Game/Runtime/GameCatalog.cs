using System;
using UnityEngine;

namespace Zombs
{
    public enum BuildingKind { Wall, Door, SlowTrap, Arrow, Cannon, Melee, Bomb, Mage, Mine, Harvester, Generator }
    public enum ItemKind { Pickaxe, Spear, Bow, Bomb, Shield, HealthPotion, PetPotion, Carl, Woody, Hat }
    public enum SpellKind { HealTowers, Timeout }

    [Serializable]
    public struct Price
    {
        public float wood, stone, gold;
        public int tokens;

        public Price(float wood = 0, float stone = 0, float gold = 0, int tokens = 0)
        {
            this.wood = wood;
            this.stone = stone;
            this.gold = gold;
            this.tokens = tokens;
        }

        public bool Covers(Price price) =>
            wood + .0001f >= price.wood && stone + .0001f >= price.stone &&
            gold + .0001f >= price.gold && tokens >= price.tokens;

        public static Price operator +(Price left, Price right) =>
            new(left.wood + right.wood, left.stone + right.stone, left.gold + right.gold, left.tokens + right.tokens);
        public static Price operator -(Price left, Price right) =>
            new(Mathf.Max(0, left.wood - right.wood), Mathf.Max(0, left.stone - right.stone),
                Mathf.Max(0, left.gold - right.gold), Mathf.Max(0, left.tokens - right.tokens));
        public static Price operator *(Price price, float multiplier) =>
            new(Mathf.Floor(price.wood * multiplier), Mathf.Floor(price.stone * multiplier),
                Mathf.Floor(price.gold * multiplier), Mathf.FloorToInt(price.tokens * multiplier));
    }

    [Serializable]
    public sealed class BuildingData
    {
        public BuildingKind kind;
        public string title, description;
        public int size = 2, limit = 6;
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
        public float health, damage, speed, reward;
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
        public float bossHealth = 4800, bossDamage = 42, bossGold = 1100;
        public int firstWaveCount = 14, extraEnemiesPerWave = 6, maximumWaveCount = 85;
        public float[] harvestYield = { 1.5f, 3, 3, 4.5f, 4.5f, 6, 9 };
        public Price healPrice = new(gold: 1000), timeoutPrice = new(gold: 10000);
        public float healRadius = 12, healDuration = 10, healPerSecond = .10f;
        public float healCooldown = 240, timeoutCooldown = 240;
        public float playerPotionCooldown = 15, petPotionCooldown = 10, invulnerableSeconds = 3;

        public BuildingData Building(BuildingKind kind) => buildings[(int)kind];
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
                Structure(BuildingKind.Smelter, "Gold Mine", "Generates gold over time.", 2, 8, 5, 5, 150, 250),
                Structure(BuildingKind.Miner, "Resource Harvester", "Spend gold to harvest wood and stone automatically.", 2, 2, 50, 50, 300, 450),
                Structure(BuildingKind.Generator, "Oxygen Generator", "Establishes your base and starts zombie waves.", 2, 1, 0, 0, 1500, 1800)
            };
            catalog.Building(BuildingKind.Generator).prices = Gold(0, 5000, 10000, 16000, 32000, 64000, 120000, 200000);
            catalog.Building(BuildingKind.Generator).health = new float[] { 1500, 1800, 2300, 2800, 3600, 4800, 6500, 9000 };
            var arrow = catalog.Building(BuildingKind.Arrow);
            arrow.prices[1] = new Price(25, 20, 100);
            arrow.health[1] = 200;
            arrow.damage[1] = 40;
            arrow.ranges = Array.ConvertAll(new[] { 600, 650, 700, 750, 800, 850, 900, 950 }, value => value / 84f);
            var bomb = catalog.Building(BuildingKind.Bomb);
            bomb.prices[1] = new Price(25, 25, 100);
            bomb.prices[2] = new Price(40, 40, 200);
            bomb.health[1] = 200;
            bomb.health[2] = 400;
            bomb.damage[1] = 60;
            bomb.damage[2] = 100;
            catalog.Building(BuildingKind.Smelter).income = new[] { 6f, 8, 10.5f, 15, 18, 22.5f, 37.5f, 52.5f };
            var harvester = catalog.Building(BuildingKind.Miner);
            harvester.income = new[] { 3.75f, 6.975f, 6.825f, 10.8f, 12.375f, 15, 20.25f, 24 };
            harvester.capacity = new float[] { 800, 1600, 2400, 3200, 4000, 4800, 5600, 7200 };
            harvester.fuelCapacity = new float[] { 1600, 2400, 2800, 4000, 4800, 4800, 9600, 12000 };
            catalog.items = new[]
            {
                Item(ItemKind.Pickaxe, "Pickaxe", "Melee weapon and harvesting tool.", Gold(0, 1000, 3000, 6000, 12000, 24000, 48000),
                    new float[] { 20, 20, 25, 30, 35, 42, 52 }, .30f, 1.65f),
                Item(ItemKind.Spear, "Spear", "Longer reach and quick thrusts.", Gold(1400, 2800, 5600, 11200, 22500, 45000, 90000),
                    new float[] { 30, 37, 45, 53, 65, 82, 105 }, .25f, 2.55f),
                Item(ItemKind.Bow, "Bow", "Fires an arrow in the direction you aim.", Gold(100, 500, 2000, 5000, 15000, 35000, 80000),
                    new float[] { 20, 28, 40, 55, 76, 100, 136 }, .5f, 12),
                Item(ItemKind.Bomb, "Bomb", "Throws an explosive that damages nearby enemies.", Gold(100, 500, 2000, 5000, 15000, 35000, 80000),
                    new float[] { 10, 16, 25, 39, 55, 80, 110 }, .5f, 17.5f, 2.1f),
                Item(ItemKind.Shield, "Shield", "Absorbs damage from zombies.", Gold(1000, 3000, 7000, 14000, 18000, 22000, 24000, 30000, 45000, 70000),
                    new float[] { 500, 1000, 1800, 4000, 10000, 20000, 35000, 50000, 65000, 85000 }),
                Item(ItemKind.HealthPotion, "Health Potion", "Heals you to full health. Press F to use.", Gold(100), new[] { 100f }),
                Item(ItemKind.PetPotion, "Pet Potion", "Heals your active pet to full health.", Gold(100), new[] { 100f }),
                Item(ItemKind.Carl, "C. A. R. L", "Fights by your side in close-range combat.", Gold(0), new[] { 16f }),
                Item(ItemKind.Woody, "Woody", "Helps harvest the resource you attack.", Gold(0), new[] { 1.5f }),
                Item(ItemKind.Hat, "Horns", "Equip the decorative horns.", Gold(0), new[] { 0f })
            };
            catalog.Item(ItemKind.Shield).recovery = new float[] { 10, 9, 8, 7, 6, 6, 6, 6, 6, 6 };
            catalog.Item(ItemKind.Shield).recharge = new float[] { 50, 100, 200, 400, 1000, 2000, 3500, 5000, 6500, 8500 };
            catalog.zombies = new[]
            {
                Enemy(new Color32(91, 135, 71, 255), 1, 62, 7, 3.9f, 4),
                Enemy(new Color32(106, 142, 172, 255), 9, 105, 10, 4.1f, 8),
                Enemy(new Color32(224, 189, 72, 255), 17, 178, 15, 4.2f, 13),
                Enemy(new Color32(187, 77, 74, 255), 33, 280, 22, 4.35f, 19),
                Enemy(new Color32(151, 95, 175, 255), 57, 460, 30, 4.5f, 30),
                Enemy(new Color32(220, 137, 67, 255), 89, 720, 39, 4.6f, 48)
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
            int[] material = { 0, 15, 35, 60, 100, 180, 300, 550 };
            int[] gold = { 0, 200, 500, 1000, 2200, 4500, 9000, 18000 };
            for (int tier = 1; tier < 8; tier++)
            {
                float scale = size == 1 ? .4f : 1;
                prices[tier] = new Price(Mathf.Ceil(material[tier] * scale), stone == 0 ? 0 : Mathf.Ceil(material[tier] * scale),
                    Mathf.Ceil(gold[tier] * scale));
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

        private static ZombieData Enemy(Color color, int firstWave, float health, float damage, float speed, float reward) =>
            new() { color = color, firstWave = firstWave, health = health, damage = damage, speed = speed, reward = reward };

        private static Price[] Gold(params int[] values) => Array.ConvertAll(values, value => new Price(gold: value));
    }
}
