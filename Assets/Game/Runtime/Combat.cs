using UnityEngine;

namespace Zombs
{
    public enum ShotKind { Arrow, Cannon, Bomb, Magic }

    public sealed class Enemy
    {
        public Vector2 Position { get; internal set; }
        public float Angle { get; internal set; }
        public Color Color { get; }
        public float Health { get; internal set; }
        public float MaxHealth { get; }
        public float Radius { get; }
        public bool Boss { get; }
        public bool Neutral => Camp >= 0;
        public float Flash { get; internal set; }
        internal readonly float Damage, Speed, Score;
        internal readonly int Camp;
        internal float Cooldown, Slow;
        internal bool Provoked;

        internal Enemy(Vector2 position, Color color, float health, float damage, float speed, float score, bool boss, int camp = -1)
        {
            Position = position;
            Color = color;
            Health = MaxHealth = health;
            Damage = damage;
            Speed = speed;
            Score = score;
            Radius = boss ? .87f : camp >= 0 ? .68f : .44f;
            Boss = boss;
            Camp = camp;
        }
    }

    public sealed class Projectile
    {
        public Vector2 Position { get; internal set; }
        public Vector2 Direction { get; }
        public ShotKind Kind { get; }
        internal float Distance;
        internal readonly float Damage, Speed, Splash;

        internal Projectile(Vector2 position, Vector2 direction, ShotKind kind, float speed, float distance, float damage, float splash)
        {
            Position = position;
            Direction = direction;
            Kind = kind;
            Speed = speed;
            Distance = distance;
            Damage = damage;
            Splash = splash;
        }
    }

    public sealed partial class RunState
    {
        private void Attack(Vector2 point)
        {
            var data = Catalog.Item(Weapon);
            int tier = ItemTier(Weapon);
            attackCooldown += data.interval;
            attackCooldown = Mathf.Max(attackCooldown, data.interval - TickSeconds);
            Emit(EffectKind.Swing, Player);
            float damage = data.power[tier - 1];
            if (Weapon == ItemKind.Bow || Weapon == ItemKind.Bomb)
            {
                bool bomb = Weapon == ItemKind.Bomb;
                float distance = bomb ? Mathf.Clamp(Vector2.Distance(Player, point), 1.3f, data.range) : data.range;
                Shoot(Player + Aim * .65f, Aim, bomb ? ShotKind.Bomb : ShotKind.Arrow, distance, damage, data.splash);
                return;
            }

            if (Weapon == ItemKind.Pickaxe)
            {
                var node = TargetResource(data.range);
                if (node != null)
                {
                    float amount = Catalog.harvestYield[tier - 1];
                    if (ActivePet == ItemKind.Woody && PetHealth > 0 && Vector2.Distance(PetPosition, node.Position) < 5)
                    {
                        amount += Catalog.Item(ItemKind.Woody).power[0] + PetEvolution;
                        GainPetExperience(1);
                    }
                    amount = node.Extract(amount);
                    Resources += Price.Gathered(node.Kind, amount);
                    node.Flash = .14f;
                    Emit(EffectKind.Gain, Player, $"+{amount:0.#} {ResourceName(node.Kind)}");
                }
            }
            Enemy target = null;
            float best = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy.Health <= 0) continue;
                Vector2 delta = enemy.Position - Player;
                float distance = delta.magnitude;
                if (distance > data.range + enemy.Radius || distance > best || Vector2.Dot(delta.normalized, Aim) < .35f)
                    continue;
                best = distance;
                target = enemy;
            }
            if (target != null) HitEnemy(target, damage);
        }

        private ResourceNode TargetResource(float range)
        {
            Vector2Int bucket = ResourceBucket(Player);
            ResourceNode target = null;
            float best = float.MaxValue;
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (!resourceBuckets.TryGetValue(bucket + new Vector2Int(x, y), out var nearby)) continue;
                foreach (var node in nearby)
                {
                    if (node.Depleted) continue;
                    Vector2 delta = node.Position - Player;
                    float distance = delta.magnitude;
                    float surface = distance - node.Radius;
                    if (surface > range || surface >= best || Vector2.Dot(delta.normalized, Aim) < .65f) continue;
                    best = surface;
                    target = node;
                }
            }
            return target;
        }

        private void FireTower(Structure tower)
        {
            Vector2 forward = Direction(tower.Angle);
            float range = tower.Data.Range(tower.Tier);
            var target = NearestEnemy(tower.Position, range, forward, tower.Kind == BuildingKind.Melee);
            if (target == null) return;
            Vector2 aim = (target.Position - tower.Position).normalized;
            tower.Cooldown = tower.Data.interval;
            if (tower.Kind != BuildingKind.Melee) tower.Angle = Angle(aim);
            float damage = tower.Data.damage[tower.Tier - 1];
            switch (tower.Kind)
            {
                case BuildingKind.Melee:
                    HitEnemy(target, damage);
                    Emit(EffectKind.Impact, target.Position);
                    break;
                case BuildingKind.Arrow:
                    Shoot(tower.Position, aim, ShotKind.Arrow, range + 1, damage);
                    break;
                case BuildingKind.Cannon:
                    Shoot(tower.Position, aim, ShotKind.Cannon, range + 1, damage, tower.Data.splash);
                    break;
                case BuildingKind.Bomb:
                    Shoot(tower.Position, aim, ShotKind.Bomb, Vector2.Distance(tower.Position, target.Position), damage, tower.Data.splash);
                    break;
                case BuildingKind.Mage:
                    int shots = tower.Tier == 8 ? 4 : tower.Tier >= 4 ? 3 : 2;
                    for (int index = 0; index < shots; index++)
                    {
                        Vector2 direction = Direction(tower.Angle + (index - (shots - 1) * .5f) * 12);
                        Shoot(tower.Position, direction, ShotKind.Magic, range + 1, damage);
                    }
                    break;
            }
        }

        private Enemy NearestEnemy(Vector2 position, float range, Vector2 forward = default, bool directional = false)
        {
            Enemy target = null;
            float best = range * range;
            foreach (var enemy in enemies)
            {
                if (enemy.Health <= 0 || (enemy.Neutral && !enemy.Provoked)) continue;
                Vector2 delta = enemy.Position - position;
                float distance = delta.sqrMagnitude;
                if (distance >= best || (directional && Vector2.Dot(delta.normalized, forward) < .70f)) continue;
                best = distance;
                target = enemy;
            }
            return target;
        }

        private void Shoot(Vector2 position, Vector2 aim, ShotKind kind, float distance, float damage, float splash = 0)
        {
            float speed = kind == ShotKind.Bomb ? 13 : kind == ShotKind.Cannon ? 20 : 26;
            projectiles.Add(new Projectile(position, aim, kind, speed, distance, damage, splash));
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var shot = projectiles[i];
                float distance = Mathf.Min(shot.Distance, shot.Speed * dt);
                Vector2 end = shot.Position + shot.Direction * distance;
                Enemy hit = null;
                float nearest = float.MaxValue;
                foreach (var enemy in enemies)
                {
                    if (enemy.Health <= 0) continue;
                    float projection = Mathf.Clamp(Vector2.Dot(enemy.Position - shot.Position, shot.Direction), 0, distance);
                    float radius = enemy.Radius + .12f;
                    if (projection >= nearest ||
                        (enemy.Position - (shot.Position + shot.Direction * projection)).sqrMagnitude > radius * radius) continue;
                    nearest = projection;
                    hit = enemy;
                }
                shot.Position = hit == null ? end : shot.Position + shot.Direction * nearest;
                shot.Distance -= distance;
                if (hit == null && shot.Distance > 0) continue;
                if (shot.Splash > 0 && (hit != null || shot.Kind == ShotKind.Bomb))
                {
                    foreach (var enemy in enemies)
                        if (enemy.Health > 0 && Vector2.Distance(shot.Position, enemy.Position) <= shot.Splash + enemy.Radius)
                            HitEnemy(enemy, shot.Damage);
                    Emit(EffectKind.Explosion, shot.Position);
                }
                else if (hit != null)
                {
                    HitEnemy(hit, shot.Damage);
                    Emit(EffectKind.Impact, shot.Position);
                }
                projectiles.RemoveAt(i);
            }
        }

        private void SpawnWaveEnemy()
        {
            bool boss = Catalog.IsBossWave(Wave);
            float angle = (nextSpawn++ % 4) * 90 + Range(-25, 25);
            Vector2 position = Generator.Position + Direction(angle) * (Catalog.spawnRadius + Range(0, 3));
            float bound = Catalog.worldSize * .5f - 1;
            position.x = Mathf.Clamp(position.x, -bound, bound);
            position.y = Mathf.Clamp(position.y, -bound, bound);
            var data = Catalog.Zombie(Wave);
            float scale = 1 + Mathf.Min(12, (Wave - data.firstWave) * .055f);
            float bossScale = 1 + .38f * ((Wave - Catalog.firstBossWave) / Catalog.bossInterval);
            enemies.Add(new Enemy(position, boss ? new Color32(125, 119, 163, 255) : data.color,
                boss ? Catalog.bossHealth * bossScale : data.health * scale,
                boss ? Catalog.bossDamage * bossScale : data.damage * scale,
                data.speed * (boss ? .85f : 1), boss ? Catalog.bossScore * bossScale : data.score, boss));
        }

        private bool OverlapsEnemy(Vector2 center, float half)
        {
            foreach (var enemy in enemies)
                if (enemy.Health > 0 && CircleHitsCell(enemy.Position, enemy.Radius, center, half)) return true;
            return false;
        }

        private void UpdateEnemies(float dt)
        {
            for (int index = enemies.Count - 1; index >= 0; index--)
            {
                var enemy = enemies[index];
                if (enemy.Health <= 0)
                {
                    enemies.RemoveAt(index);
                    if (enemy.Neutral) campTimers[enemy.Camp] = 50;
                    continue;
                }
                enemy.Flash = Mathf.Max(0, enemy.Flash - dt);
                enemy.Cooldown -= dt;
                enemy.Slow -= dt;
                if (!Night && !enemy.Neutral)
                {
                    enemy.Health -= enemy.MaxHealth * dt * .19f;
                    if (enemy.Health <= 0) continue;
                }
                if (enemy.Neutral && !enemy.Provoked) continue;
                Vector2 target = enemy.Neutral ? Player : Generator?.Position ?? Player;
                bool chasePlayer = !Dead && (enemy.Neutral || Vector2.Distance(enemy.Position, Player) < 1.7f);
                if (chasePlayer) target = Player;
                if (enemy.Neutral && (Dead || Vector2.Distance(enemy.Position, camps[enemy.Camp]) > 14))
                {
                    enemy.Provoked = false;
                    enemy.Position = camps[enemy.Camp];
                    enemy.Health = enemy.MaxHealth;
                    continue;
                }
                Vector2 direction = (target - enemy.Position).normalized;
                if (direction != Vector2.zero) enemy.Angle = Angle(direction);
                if (chasePlayer && Vector2.Distance(Player, enemy.Position) < enemy.Radius + PlayerRadius + .14f)
                {
                    if (enemy.Cooldown <= 0 && !Protected)
                    {
                        HitPlayer(enemy);
                        enemy.Cooldown = enemy.Boss ? .9f : .75f;
                    }
                    continue;
                }
                if (PetHealth > 0 && Vector2.Distance(enemy.Position, PetPosition) < enemy.Radius + .5f && enemy.Cooldown <= 0)
                {
                    PetHealth = Mathf.Max(0, PetHealth - enemy.Damage);
                    if (PetHealth <= 0) petRespawnCooldown = 3;
                    enemy.Cooldown = 1;
                }
                if (At(enemy.Position)?.Kind == BuildingKind.SlowTrap) enemy.Slow = .22f;
                Vector2 offset = (direction + Separation(index, enemy) * .48f).normalized *
                    (enemy.Speed * dt * (enemy.Slow > 0 ? .48f : 1));
                var blocker = SolidBuilding(enemy.Position + offset, enemy.Radius, false);
                if (blocker == null)
                {
                    enemy.Position += offset;
                    continue;
                }
                if (!enemy.Neutral && enemy.Cooldown <= 0)
                {
                    enemy.Cooldown = enemy.Boss ? .9f : .75f;
                    blocker.Health -= enemy.Damage;
                    blocker.Recovery = 8;
                    if (blocker.Health <= 0)
                    {
                        if (blocker == Generator)
                        {
                            LoseGenerator();
                            return;
                        }
                        Remove(blocker);
                        Emit(EffectKind.Explosion, blocker.Position);
                    }
                }
            }
            UpdateCamps(dt);
        }

        private Vector2 Separation(int index, Enemy enemy)
        {
            Vector2 away = Vector2.zero;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (i == index || enemies[i].Health <= 0) continue;
                Vector2 delta = enemy.Position - enemies[i].Position;
                float space = (enemy.Radius + enemies[i].Radius) * .96f;
                float squareDistance = delta.sqrMagnitude;
                if (squareDistance > .0001f && squareDistance < space * space)
                    away += delta.normalized * (1 - delta.magnitude / space);
            }
            return away;
        }

        private void UpdateCamps(float dt)
        {
            for (int i = 0; i < camps.Length; i++)
            {
                campTimers[i] -= dt;
                if (campTimers[i] > 0 || Vector2.Distance(Player, camps[i]) > 25) continue;
                bool occupiedCamp = false;
                foreach (var enemy in enemies)
                    if (enemy.Camp == i)
                    {
                        occupiedCamp = true;
                        break;
                    }
                if (occupiedCamp) continue;
                enemies.Add(new Enemy(camps[i], new Color32(157, 96, 121, 255),
                    125 + i * 18, 13 + i, 4.1f, 50, false, i));
            }
        }

        private void HitPlayer(Enemy enemy)
        {
            float amount = enemy.Damage;
            int shieldTier = ItemTier(ItemKind.Shield);
            if (!enemy.Neutral && shieldTier > 0)
            {
                shieldRecovery = Catalog.Item(ItemKind.Shield).recovery[shieldTier - 1];
                float absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
            }
            Health = Mathf.Max(0, Health - amount);
            playerRecovery = 8;
            Emit(EffectKind.Damage, Player, Mathf.CeilToInt(enemy.Damage).ToString());
            if (Dead) AutoAttack = false;
        }

        private void HitEnemy(Enemy enemy, float damage)
        {
            enemy.Provoked = true;
            enemy.Health -= damage;
            enemy.Flash = .14f;
            Emit(EffectKind.Damage, enemy.Position, Mathf.CeilToInt(damage).ToString());
            if (enemy.Health > 0) return;
            if (enemy.Boss) Resources += new Price(tokens: Catalog.bossTokens);
            AddScore(Mathf.RoundToInt(enemy.Score));
            if (PetHealth > 0) GainPetExperience(enemy.Boss ? 35 : 7);
        }

        private void UpdatePet(float dt)
        {
            if (ActivePet == null || PetHealth <= 0 || Dead) return;
            petAttackCooldown -= dt;
            Enemy target = ActivePet == ItemKind.Carl ? NearestEnemy(Player, 5) : null;
            Vector2 destination = target?.Position ?? Player - Aim * 1.15f;
            if (Vector2.Distance(PetPosition, destination) > (target == null ? .25f : .8f))
                PetPosition = Vector2.MoveTowards(PetPosition, destination, dt * 8.5f);
            if (target != null && petAttackCooldown <= 0 && Vector2.Distance(PetPosition, destination) < 1.4f)
            {
                petAttackCooldown = .68f;
                HitEnemy(target, Catalog.Item(ItemKind.Carl).power[0] + PetLevel * 2 + PetEvolution * 10);
            }
        }

        private void GainPetExperience(int experience)
        {
            float previousMax = PetMaxHealth;
            PetExperience += experience;
            PetHealth *= PetMaxHealth / previousMax;
        }

        public static Vector2 Direction(float angle) => new(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
        public static float Angle(Vector2 direction) => Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
    }
}
