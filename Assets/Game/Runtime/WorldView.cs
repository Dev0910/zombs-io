using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    internal sealed class WorldView
    {
        private sealed class Burst
        {
            public Effect Effect;
            public float Time, Duration;
        }

        private static readonly Color DayGround = new(.96f, 1, 1);
        private static readonly Color NightGround = new(.57f, .69f, .90f);
        private static readonly Color NightObjects = new(.76f, .83f, .99f);
        private static readonly Color Air = new(.39f, .99f, .87f);
        private static readonly Color Amber = new(1, .73f, .39f);
        private static readonly Color Coral = new(1, .43f, .43f);
        private static readonly Color Violet = new(.78f, .61f, 1);
        private static readonly Color Armor = new(.84f, .89f, .82f);
        private readonly Camera camera;
        private readonly Transform root;
        private readonly SpriteRenderer ground;
        private readonly List<SpriteRenderer> parts = new();
        private readonly List<Burst> bursts = new();
        private RunState run;
        private Vector2 lastPlayer;
        private int used;
        private float swing, clock, stride, moving;
        private Color lighting;

        public WorldView(Camera camera)
        {
            this.camera = camera;
            root = new GameObject("Outpost world").transform;
            ground = CreatePart("Basalt");
            ground.sprite = SpriteArt.WorldTile;
            ground.drawMode = SpriteDrawMode.Tiled;
            ground.tileMode = SpriteTileMode.Continuous;
            ground.sortingOrder = -100;
        }

        public void Attach(RunState state)
        {
            if (run != null) run.EffectOccurred -= AddEffect;
            run = state;
            run.EffectOccurred += AddEffect;
            bursts.Clear();
            clock = swing = stride = moving = 0;
            lastPlayer = run.Player;
        }

        public void Dispose()
        {
            if (run != null) run.EffectOccurred -= AddEffect;
            if (root != null) Object.Destroy(root.gameObject);
        }

        private void AddEffect(Effect effect)
        {
            if (effect.Kind == EffectKind.Swing)
            {
                swing = .22f;
                return;
            }
            if (effect.Kind == EffectKind.Gain)
                bursts.RemoveAll(burst => burst.Effect.Kind == EffectKind.Gain);
            if (bursts.Count == 80) bursts.RemoveAt(0);
            float duration = effect.Kind switch
            {
                EffectKind.Explosion => .44f,
                EffectKind.Placed or EffectKind.Upgraded => .48f,
                _ => effect.Text != null ? .57f : .23f
            };
            bursts.Add(new Burst { Effect = effect, Time = duration, Duration = duration });
        }

        public void Draw(float dt, bool intro, BuildingKind? placing, int rotation, Structure selected, bool healing, Vector2 cursor)
        {
            if (intro)
            {
                HideParts(0);
                ground.enabled = false;
                return;
            }

            used = 0;
            clock += dt;
            float travel = Vector2.Distance(lastPlayer, run.Player);
            stride += travel * 7;
            moving = Mathf.MoveTowards(moving, travel > .001f ? 1 : 0, dt * 9);
            lastPlayer = run.Player;
            lighting = Color.Lerp(Color.white, NightObjects, run.LightBlend);
            Vector2 center = run.Player;
            camera.transform.position = new Vector3(center.x, center.y, -20);
            ground.enabled = true;
            ground.transform.position = new Vector3(Mathf.Floor(center.x / 8) * 8, Mathf.Floor(center.y / 8) * 8, 0);
            ground.color = Color.Lerp(DayGround, NightGround, run.LightBlend);
            float width = camera.aspect * camera.orthographicSize, height = camera.orthographicSize;
            ground.size = new Vector2(Mathf.Ceil((width + 8) / 8) * 16, Mathf.Ceil((height + 8) / 8) * 16);

            DrawTerrain(center, width, height);
            DrawCoverage(placing);
            if (placing.HasValue) DrawPlacement(placing.Value, rotation, cursor);

            foreach (var node in run.Nodes)
            {
                if (node.Depleted || !Visible(node.Position, center, width, height)) continue;
                int seed = (int)(node.Position.x * 31 + node.Position.y * 83);
                float size = node.Radius * 2.05f;
                Part(SpriteArt.Shadow, node.Position + new Vector2(.19f, -.15f), new Vector2(size * 1.22f, size * 1.1f), 0, -3, new Color(1, 1, 1, .68f));
                float impact = Mathf.Clamp01(node.Flash * 7);
                Part(SpriteArt.Deposit(node.Kind, (seed & int.MaxValue) % 3), node.Position,
                    Vector2.one * (size * (1 - impact * .04f)), seed % 43, 0,
                    Color.Lerp(Color.white, new Color(1, .88f, .65f), impact * .65f));
            }

            foreach (var building in run.Buildings)
            {
                if (!Visible(building.Position, center, width, height)) continue;
                float size = building.Data.size;
                Part(SpriteArt.FootprintShadow, building.Position + new Vector2(.09f, -.12f), Vector2.one * size * 1.08f,
                    0, 1, Color.white);
                DrawBuilding(building.Kind, building.Tier, building.Position, building.Angle, Color.white, 5);
                DrawMachineState(building);
                if (building.Health < building.MaximumHealth * .98f)
                    Bar(building.Position + Vector2.down * (size * .49f), size * .78f, .12f,
                        building.Health / building.MaximumHealth, Air, 14);
            }

            if (selected != null)
            {
                Corners(selected.Position, selected.Data.size * 1.06f, Air, 32);
                if (SpriteArt.IsTower(selected.Kind)) RangeCircle(selected.Position, selected.Data.Range(selected.Tier));
            }
            if (run.HealRemaining > 0)
                Zone(run.HealPoint, run.Catalog.healRadius, Air, .08f, false, 2);
            if (healing)
                Zone(cursor, run.Catalog.healRadius, Air, .025f, true, 2);

            if (!run.Dead && !run.Defeated)
            {
                swing = Mathf.Max(0, swing - dt);
                DrawOperator();
                if (run.ActivePet.HasValue && run.PetHealth > 0)
                {
                    float hover = Mathf.Sin(clock * 4 + run.PetPosition.x) * .047f;
                    Part(SpriteArt.Shadow, run.PetPosition + Vector2.down * .10f, new Vector2(1.14f, .74f), 0, 19, new Color(1, 1, 1, .65f));
                    Part(SpriteArt.Item(run.ActivePet.Value), run.PetPosition + Vector2.up * hover, Vector2.one * 1.13f,
                        RunState.Angle(run.Aim), 23, Color.white);
                    if (run.PetHealth < run.PetMaxHealth)
                        Bar(run.PetPosition + Vector2.down * .59f, .87f, .095f, run.PetHealth / run.PetMaxHealth, Air, 24);
                }
            }
            foreach (var enemy in run.Enemies)
            {
                if (enemy.Health > 0 && Visible(enemy.Position, center, width, height)) DrawEnemy(enemy);
            }
            foreach (var shot in run.Projectiles)
            {
                if (!Visible(shot.Position, center, width, height)) continue;
                Color accent = shot.Kind switch { ShotKind.Arrow => Air, ShotKind.Magic => Violet, _ => Amber };
                float length = shot.Kind == ShotKind.Arrow ? .66f : .46f;
                Vector2 trail = shot.Position - shot.Direction * (length * .5f);
                Part(SpriteArt.Pixel, trail, new Vector2(.056f, length), RunState.Angle(shot.Direction), 30,
                    new Color(accent.r, accent.g, accent.b, .49f));
                Part(SpriteArt.Glow, shot.Position, Vector2.one * .80f, 0, 29,
                    new Color(accent.r, accent.g, accent.b, .25f));
                Part(SpriteArt.Shot(shot.Kind), shot.Position, Vector2.one * (shot.Kind == ShotKind.Bomb ? .46f : .29f),
                    RunState.Angle(shot.Direction), 31, Color.white);
            }

            DrawEffects(dt, center, width, height);
            HideParts(used);
        }

        private static bool Visible(Vector2 position, Vector2 center, float width, float height) =>
            Mathf.Abs(position.x - center.x) < width + 3 && Mathf.Abs(position.y - center.y) < height + 3;

        private void DrawTerrain(Vector2 center, float width, float height)
        {
            const int strideSize = 5;
            int left = Mathf.FloorToInt((center.x - width) / strideSize), right = Mathf.CeilToInt((center.x + width) / strideSize);
            int bottom = Mathf.FloorToInt((center.y - height) / strideSize), top = Mathf.CeilToInt((center.y + height) / strideSize);
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
            {
                int hash = unchecked(x * 73856093 ^ y * 19349663) & int.MaxValue;
                Vector2 position = new(x * strideSize + (hash % 11) * .13f, y * strideSize + (hash % 13) * .11f);
                Part(SpriteArt.TerrainDetail(hash % 5), position, Vector2.one * 4.7f, hash % 360, -75, Color.white);
                if (hash % 3 != 0) continue;
                float drift = clock * .34f + hash % 13;
                float opacity = .10f + .07f * Mathf.Sin(drift);
                Part(SpriteArt.Circle, position + new Vector2(Mathf.Sin(drift), Mathf.Cos(drift * .7f)) * 1.1f,
                    Vector2.one * (.045f + hash % 4 * .023f), 0, -61, new Color(.59f, .83f, .89f, opacity));
            }
        }

        private void DrawCoverage(BuildingKind? placing)
        {
            if (run.Defeated) return;
            if (run.OxygenRadius > 0)
            {
                if (placing == BuildingKind.Generator)
                    Part(SpriteArt.AirFill, run.OxygenCenter, Vector2.one * run.OxygenRadius * 2, 0, -60, new Color(Air.r, Air.g, Air.b, .042f));
                else Zone(run.OxygenCenter, run.OxygenRadius, new Color(Air.r, Air.g, Air.b, .76f), run.Generator == null ? .055f : .075f, run.Generator == null, -60);
            }
            else if (run.Generator != null)
                Zone(run.Generator.Position, run.Generator.Data.Range(run.Generator.Tier), Amber, 0, true, -60);
        }

        private void Zone(Vector2 center, float radius, Color accent, float fill, bool dashed, int layer)
        {
            Vector2 diameter = Vector2.one * radius * 2;
            if (fill > 0)
                Part(SpriteArt.AirFill, center, diameter, 0, layer, new Color(accent.r, accent.g, accent.b, fill));
            Part(SpriteArt.AirBoundary(dashed), center, diameter, 0, layer + 1, accent);
        }

        private void DrawPlacement(BuildingKind kind, int rotation, Vector2 cursor)
        {
            bool valid = run.CanPlace(kind, cursor) == Placement.Allowed;
            Vector2 point = run.CenterFor(kind, cursor);
            float size = run.Catalog.Building(kind).size;
            Color accent = valid ? Air : Coral;
            var origin = Vector2Int.FloorToInt(point);
            const int radius = 4;
            var grid = new Color(.58f, .76f, .80f, .14f);
            for (int offset = -radius; offset <= radius; offset++)
            {
                Part(SpriteArt.Pixel, new Vector2(origin.x + offset, origin.y), new Vector2(.022f, radius * 2), 0, -51, grid);
                Part(SpriteArt.Pixel, new Vector2(origin.x, origin.y + offset), new Vector2(radius * 2, .022f), 0, -51, grid);
            }

            if (kind == BuildingKind.Generator)
            {
                Zone(Vector2.zero, run.Catalog.generatorSiteRadius, new Color(.96f, .93f, .76f, .88f), 0, true, -57);
                if (valid) Zone(point, run.Catalog.Building(kind).Range(1), new Color(Air.r, Air.g, Air.b, .57f), 0, false, -56);
            }
            else if (SpriteArt.IsTower(kind)) RangeCircle(point, run.Catalog.Building(kind).Range(1));
            Part(SpriteArt.Pixel, point, Vector2.one * size, 0, 32, new Color(accent.r, accent.g, accent.b, .22f));
            DrawBuilding(kind, 1, point, rotation * 90, new Color(accent.r, accent.g, accent.b, .68f), 33);
            Corners(point, size, accent, 36);
            if (!valid)
            {
                Part(SpriteArt.Pixel, point, new Vector2(size * .58f, .071f), 45, 37, Color.white);
                Part(SpriteArt.Pixel, point, new Vector2(size * .58f, .071f), -45, 37, Color.white);
            }
        }

        private void Corners(Vector2 center, float size, Color color, int order)
        {
            float half = size * .5f, edge = size * .22f;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Part(SpriteArt.Pixel, center + new Vector2(x * (half - edge * .5f), y * half), new Vector2(edge, .038f), 0, order, color);
                Part(SpriteArt.Pixel, center + new Vector2(x * half, y * (half - edge * .5f)), new Vector2(.038f, edge), 0, order, color);
            }
        }

        private void RangeCircle(Vector2 point, float radius) =>
            Zone(point, radius, new Color(Amber.r, Amber.g, Amber.b, .53f), .05f, true, 1);

        private void DrawBuilding(BuildingKind kind, int tier, Vector2 position, float angle, Color color, int layer)
        {
            float size = run.Catalog.Building(kind).size;
            Part(SpriteArt.Base(kind, tier), position, Vector2.one * size, 0, layer, color);
            if (SpriteArt.IsTower(kind))
                Part(SpriteArt.Turret(kind, tier), position, Vector2.one * size, angle, layer + 1, color);
        }

        private void DrawMachineState(Structure building)
        {
            bool active;
            Color accent;
            switch (building.Kind)
            {
                case BuildingKind.Generator:
                    active = run.GeneratorOnline;
                    accent = active ? Air : Coral;
                    if (!active)
                        Part(SpriteArt.Circle, building.Position + Vector2.up * .08f, Vector2.one * .75f, 0, 8, new Color(.075f, .13f, .18f, .91f));
                    break;
                case BuildingKind.Smelter:
                case BuildingKind.Assembler:
                    active = building.Queued > 0;
                    accent = building.Stored >= 1 ? Air : Amber;
                    break;
                case BuildingKind.Miner:
                    active = building.Fuel > 0 && building.DepositNode != null && !building.DepositNode.Depleted &&
                        building.Stored < building.Data.Value(building.Data.capacity, building.Tier);
                    accent = building.Stored >= 1 ? Air : Amber;
                    break;
                default:
                    return;
            }
            Vector2 position = building.Position;
            if (active)
            {
                float radius = building.Kind == BuildingKind.Generator ? 3.4f : 2.3f;
                Color light = building.Kind == BuildingKind.Generator ? Air : Amber;
                Part(SpriteArt.Glow, position, Vector2.one * radius, 0, 3,
                    new Color(light.r, light.g, light.b, building.Kind == BuildingKind.Generator ? .21f : .12f));
            }
            Color indicator = active || building.Stored >= 1 || building.Kind == BuildingKind.Generator
                ? accent : new Color(.34f, .42f, .44f);
            Part(SpriteArt.Pixel, position + new Vector2(.42f, -.54f), new Vector2(.20f, .064f), 0, 9, indicator);
            if (!active) return;

            float pulse = .5f + .5f * Mathf.Sin(clock * 3 + position.x);
            if (building.Kind == BuildingKind.Generator)
            {
                Part(SpriteArt.Rotor, position + Vector2.up * .08f, Vector2.one * .98f, clock * 32, 8,
                    new Color(Air.r, Air.g, Air.b, .34f + pulse * .2f));
            }
            else
            {
                Part(SpriteArt.Pixel, position + new Vector2(-.37f + Mathf.Repeat(clock * .36f, .63f), -.45f),
                    new Vector2(.12f, .04f), 0, 8, new Color(Amber.r, Amber.g, Amber.b, .5f + pulse * .4f));
            }
        }

        private void DrawOperator()
        {
            Vector2 point = run.Player;
            float angle = RunState.Angle(run.Aim), gait = Mathf.Sin(stride) * moving;
            Vector2 right = new(run.Aim.y, -run.Aim.x);
            Part(SpriteArt.Shadow, point + new Vector2(.12f, -.13f), new Vector2(1.64f, 1.24f), 0, 19, new Color(1, 1, 1, .76f));
            for (int side = -1; side <= 1; side += 2)
                Part(SpriteArt.Limb, point + right * (.27f * side) - run.Aim * (.40f + gait * .15f * side),
                    new Vector2(.28f, .40f), angle, 24, Armor);
            Part(SpriteArt.Suit, point, Vector2.one * (1.59f + Mathf.Abs(gait) * .026f), angle, 25, Color.white);
            float arc = swing > 0 ? Mathf.Sin(swing / .22f * Mathf.PI) * 63 : 0;
            Vector2 direction = RunState.Direction(angle + 20 - arc);
            Part(SpriteArt.Item(run.Weapon, run.ItemTier(run.Weapon)), point + direction * .86f + right * .16f,
                Vector2.one * 1.04f, angle + 18 - arc, 27, Color.white);
            if (run.WearingHat)
                Part(SpriteArt.Hat, point + run.Aim * .19f, Vector2.one * .84f, angle, 28, Color.white);
            if (!run.CanBreathe || run.Protected)
            {
                Color accent = run.Protected ? Air : run.Breath > 0 ? Amber : Coral;
                Part(SpriteArt.Outline, point, Vector2.one * 1.76f, 0, 20, accent);
            }
        }

        private void DrawEnemy(Enemy enemy)
        {
            float size = enemy.Radius * (enemy.Boss ? 2.30f : 2.54f);
            float pace = Mathf.Sin(clock * (enemy.Boss ? 5 : 10) + enemy.Position.x * 3);
            Vector2 forward = RunState.Direction(enemy.Angle);
            Vector2 right = new(forward.y, -forward.x);
            Vector2 point = enemy.Position;
            Color tint = Color.Lerp(Color.white, new Color(1, .64f, .54f), Mathf.Clamp01(enemy.Flash * 5));
            Part(SpriteArt.Shadow, point + new Vector2(.13f, -.13f), new Vector2(size * 1.18f, size * .86f), 0, 10, new Color(1, 1, 1, .55f));
            for (int side = -1; side <= 1; side += 2)
                Part(SpriteArt.Limb, point + right * (size * .23f * side) - forward * (size * (.29f + .09f * side * pace)),
                    new Vector2(size * .25f, size * .36f), enemy.Angle + side * 12, 15, new Color(.56f, .50f, .44f));
            int variant = enemy.Neutral ? 1 : (Mathf.RoundToInt(enemy.Color.r * 19) % 3);
            Part(SpriteArt.Infected(variant, enemy.Boss), point, Vector2.one * size, enemy.Angle + pace * 5, 16, tint);
            if (enemy.Boss)
            {
                Part(SpriteArt.ThreatMarker, point + Vector2.up * (size * .72f), Vector2.one * .37f, 0, 19, Color.white);
                Bar(point + Vector2.down * (size * .64f), size * 1.16f, .14f, enemy.Health / enemy.MaxHealth, Coral, 20);
            }
            else
            {
                if (!enemy.Neutral || enemy.Provoked)
                    Part(SpriteArt.HostileMarker, point + Vector2.up * (size * .64f), Vector2.one * .22f, 0, 19, Color.white);
                if (enemy.Health < enemy.MaxHealth * .97f)
                    Bar(point + Vector2.down * (size * .59f), size * .90f, .115f, enemy.Health / enemy.MaxHealth, Coral, 20);
            }
        }

        private void DrawEffects(float dt, Vector2 center, float width, float height)
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Burst burst = bursts[i];
                burst.Time -= dt;
                if (burst.Time <= 0)
                {
                    bursts.RemoveAt(i);
                    continue;
                }
                Vector2 point = burst.Effect.Point;
                if (!Visible(point, center, width, height)) continue;
                float life = burst.Time / burst.Duration, age = 1 - life;
                if (burst.Effect.Kind == EffectKind.Explosion)
                {
                    Part(SpriteArt.Circle, point, Vector2.one * Mathf.Lerp(.44f, 1.6f, age), 0, 34, new Color(1, .59f, .36f, life * .23f));
                    Part(SpriteArt.Outline, point, Vector2.one * Mathf.Lerp(.5f, 3.15f, age), 0, 34, new Color(1, .74f, .44f, life * .72f));
                    Sparks(point, age, life, 7, 1.6f, Amber);
                }
                else if (burst.Effect.Kind is EffectKind.Impact or EffectKind.Gain)
                    Sparks(point, age, life, 4, .63f, Amber);
                else if (burst.Effect.Kind is EffectKind.Placed or EffectKind.Upgraded)
                    Part(SpriteArt.Rotor, point, Vector2.one * Mathf.Lerp(.72f, 2.35f, age), clock * 100, 34, new Color(Air.r, Air.g, Air.b, life * .53f));
            }
        }

        private void Sparks(Vector2 center, float age, float life, int count, float reach, Color accent)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) + center.x * 33;
                Vector2 point = center + RunState.Direction(angle) * Mathf.Lerp(.15f, reach, age);
                Part(SpriteArt.Pixel, point, new Vector2(.04f, .10f + .13f * life), angle, 35,
                    new Color(accent.r, accent.g, accent.b, life * .75f));
            }
        }

        public void DrawLabels(System.Action<Vector2, string, Color> text)
        {
            int shown = 0;
            for (int index = bursts.Count - 1; index >= 0 && shown < 3; index--)
            {
                Burst burst = bursts[index];
                if (burst.Effect.Text == null) continue;
                bool damage = burst.Effect.Kind == EffectKind.Damage;
                if (damage && (burst.Effect.Point - run.Player).sqrMagnitude > 1.4f) continue;
                float life = burst.Time / burst.Duration;
                Color color = damage ? Coral : burst.Effect.Kind == EffectKind.Gain ? Amber : Air;
                color.a = Mathf.Clamp01(life * 2);
                float height = 1.14f + (1 - life) * .74f + shown * .32f;
                text(burst.Effect.Point + Vector2.up * height, burst.Effect.Text, color);
                shown++;
            }
        }

        private void Bar(Vector2 position, float width, float height, float fraction, Color color, int order)
        {
            Part(SpriteArt.Pixel, position, new Vector2(width, height), 0, order, new Color(.04f, .07f, .10f, .96f));
            float fill = (width - .05f) * Mathf.Clamp01(fraction);
            if (fill > 0)
                Part(SpriteArt.Pixel, position + Vector2.left * ((width - fill - .05f) * .5f),
                    new Vector2(fill, height * .64f), 0, order + 1, color);
        }

        private SpriteRenderer CreatePart(string name)
        {
            var gameObject = new GameObject(name, typeof(SpriteRenderer));
            gameObject.transform.SetParent(root, false);
            return gameObject.GetComponent<SpriteRenderer>();
        }

        private void Part(Sprite sprite, Vector2 position, Vector2 scale, float angle, int order, Color color)
        {
            if (used == parts.Count) parts.Add(CreatePart("Sprite"));
            SpriteRenderer part = parts[used++];
            part.enabled = true;
            part.sprite = sprite;
            part.sortingOrder = order;
            part.color = color * lighting;
            part.transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0), Quaternion.Euler(0, 0, angle));
            part.transform.localScale = new Vector3(scale.x, scale.y, 1);
        }

        private void HideParts(int first)
        {
            for (int i = first; i < parts.Count && parts[i].enabled; i++) parts[i].enabled = false;
        }
    }
}
