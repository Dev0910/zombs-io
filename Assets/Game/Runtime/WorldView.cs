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

        private readonly Camera camera;
        private readonly Transform root;
        private readonly SpriteRenderer ground;
        private readonly List<SpriteRenderer> parts = new();
        private readonly List<Burst> bursts = new();
        private RunState run;
        private int used;
        private float swing;
        private Color lighting;
        private static readonly Color Skin = new Color32(255, 205, 123, 255);
        private static readonly Color HealthColor = new Color32(112, 166, 13, 255);
        private static readonly Color EnemyHealth = new Color32(173, 60, 46, 255);
        private static readonly Color NightColor = new(.63f, .65f, .88f);

        public WorldView(Camera camera)
        {
            this.camera = camera;
            root = new GameObject("World sprites").transform;
            ground = CreatePart("Grass grid");
            ground.sprite = SpriteArt.WorldTile;
            ground.drawMode = SpriteDrawMode.Tiled;
            ground.tileMode = SpriteTileMode.Continuous;
            ground.size = new Vector2(70, 40);
            ground.sortingOrder = -100;
            ground.transform.SetParent(root, false);
        }

        public void Attach(RunState state)
        {
            if (run != null) run.EffectOccurred -= AddEffect;
            run = state;
            run.EffectOccurred += AddEffect;
            bursts.Clear();
            swing = 0;
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
            if (bursts.Count == 100) bursts.RemoveAt(0);
            float duration = effect.Text != null ? .85f : effect.Kind == EffectKind.Explosion ? .38f : .25f;
            bursts.Add(new Burst { Effect = effect, Time = duration, Duration = duration });
        }

        public void Draw(float dt, bool intro, BuildingKind? placing, int rotation, Structure selected, bool healing, Vector2 cursor)
        {
            used = 0;
            lighting = Color.Lerp(Color.white, NightColor, run.LightBlend);
            Vector2 center = intro ? new Vector2(-2.5f, 0) : run.Player;
            camera.transform.position = new Vector3(center.x, center.y, -20);
            ground.transform.position = new Vector3(Mathf.Floor(center.x), Mathf.Floor(center.y), 0);
            ground.color = lighting;
            ground.size = new Vector2(Mathf.Ceil(camera.aspect * camera.orthographicSize + 2) * 2,
                Mathf.Ceil(camera.orthographicSize + 2) * 2);
            float halfWidth = camera.aspect * camera.orthographicSize + 3;
            float halfHeight = camera.orthographicSize + 3;
            foreach (var node in run.Nodes)
            {
                Vector2 relative = node.Position - center;
                if (Mathf.Abs(relative.x) > halfWidth || Mathf.Abs(relative.y) > halfHeight) continue;
                float scale = node.Stone ? 3 : 4;
                if (node.Flash > 0) scale *= .95f + node.Flash * .36f;
                Part(node.Stone ? SpriteArt.Rock : SpriteArt.Tree, node.Position, Vector2.one * scale, 0, 0,
                    node.Flash > 0 ? new Color(1, 1, .87f) : Color.white);
            }
            foreach (var building in run.Buildings)
            {
                Vector2 relative = building.Position - center;
                if (Mathf.Abs(relative.x) > halfWidth || Mathf.Abs(relative.y) > halfHeight) continue;
                DrawBuilding(building.Kind, building.Tier, building.Position, building.Angle, Color.white, 5);
                if (building.Health < building.MaximumHealth - .01f)
                    Bar(building.Position - new Vector2(0, building.Data.size * .38f), building.Data.size * .81f, .14f,
                        building.Health / building.MaximumHealth, HealthColor, 28);
            }
            if (selected != null && SpriteArt.IsTower(selected.Kind))
                RangeCircle(selected.Position, selected.Data.Range(selected.Tier));
            if (run.HealRemaining > 0)
            {
                float size = run.Catalog.healRadius * 2;
                Part(SpriteArt.Circle, run.HealPoint, Vector2.one * size, 0, 2, new Color(.37f, .99f, .38f, .18f));
                Part(SpriteArt.RangeOutline, run.HealPoint, Vector2.one * size, 0, 3, new Color(.5f, 1, .51f, .53f));
            }

            if (!intro && !run.Dead && !run.Defeated)
            {
                swing = Mathf.Max(0, swing - dt);
                float angle = RunState.Angle(run.Aim);
                DrawPerson(run.Player, angle, 1.48f, Skin, 25, swing, SpriteArt.Item(run.Weapon, run.ItemTier(run.Weapon)));
                if (run.WearingHat) Part(SpriteArt.Hat, run.Player + run.Aim * .18f, Vector2.one * 1.3f, angle, 29, Color.white);
                if (run.Protected)
                    Part(SpriteArt.Outline, run.Player, Vector2.one * 1.85f, 0, 29, new Color(.65f, .87f, 1, .48f));
                Bar(run.Player + Vector2.down * 1.45f, 1.73f, .20f, run.Health / run.Catalog.playerHealth, HealthColor, 29);
                if (run.ActivePet.HasValue && run.PetHealth > 0)
                {
                    Part(SpriteArt.Item(run.ActivePet.Value), run.PetPosition, Vector2.one * 1.06f, angle, 23, Color.white);
                    Bar(run.PetPosition + Vector2.down * .68f, .9f, .12f, run.PetHealth / run.PetMaxHealth, HealthColor, 24);
                }
            }
            foreach (var enemy in run.Enemies)
            {
                if (enemy.Health <= 0) continue;
                Color color = enemy.Flash > 0 ? Color.Lerp(enemy.Color, new Color(1, .63f, .58f), .43f) : enemy.Color;
                DrawPerson(enemy.Position, enemy.Angle, enemy.Radius * 2.04f, color, 16, 0, enemy.Boss ? SpriteArt.Item(ItemKind.Spear) : null);
                if (enemy.Boss)
                    Part(SpriteArt.Crown, enemy.Position + Vector2.up * .5f, Vector2.one * 1.3f, 0, 20, Color.white);
                Bar(enemy.Position + Vector2.down * (enemy.Radius + .52f), enemy.Radius * 2.4f, .12f,
                    enemy.Health / enemy.MaxHealth, EnemyHealth, 22);
            }
            foreach (var shot in run.Projectiles)
            {
                bool arrow = shot.Kind == ShotKind.Arrow;
                var size = arrow ? new Vector2(.55f, .83f) : Vector2.one * (shot.Kind == ShotKind.Bomb ? .67f : .43f);
                Part(arrow ? SpriteArt.Arrow : SpriteArt.Orb(shot.Kind == ShotKind.Magic), shot.Position, size,
                    RunState.Angle(shot.Direction), 30, Color.white);
            }
            if (placing.HasValue)
            {
                var kind = placing.Value;
                bool valid = run.CanPlace(kind, cursor) == Placement.Allowed;
                Vector2 point = run.CenterFor(kind, cursor);
                if (SpriteArt.IsTower(kind)) RangeCircle(point, run.Catalog.Building(kind).Range(1));
                Color color = valid ? new Color(.9f, 1, .91f, .60f) : new Color(1, .40f, .36f, .63f);
                Part(SpriteArt.Pixel, point, Vector2.one * run.Catalog.Building(kind).size, 0, 32,
                    valid ? new Color(.69f, .87f, .55f, .12f) : new Color(.81f, .21f, .12f, .27f));
                DrawBuilding(kind, 1, point, rotation * 90, color, 33);
            }
            if (healing)
                Part(SpriteArt.RangeOutline, cursor, Vector2.one * (run.Catalog.healRadius * 2), 0, 33, new Color(.65f, 1, .57f, .58f));
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var burst = bursts[i];
                burst.Time -= dt;
                if (burst.Time <= 0)
                {
                    bursts.RemoveAt(i);
                    continue;
                }
                float time = burst.Time / burst.Duration;
                if (burst.Effect.Kind == EffectKind.Explosion)
                {
                    Part(SpriteArt.Circle, burst.Effect.Point, Vector2.one * Mathf.Lerp(3.5f, .8f, time), 0, 34,
                        new Color(1, .51f, .20f, time * .42f));
                    Part(SpriteArt.Outline, burst.Effect.Point, Vector2.one * Mathf.Lerp(4.2f, 1.1f, time), 0, 35,
                        new Color(1, .75f, .38f, time * .82f));
                }
                if (burst.Effect.Kind == EffectKind.Upgraded || burst.Effect.Kind == EffectKind.Placed)
                    Part(SpriteArt.Outline, burst.Effect.Point, Vector2.one * Mathf.Lerp(2.8f, 1.25f, time), 0, 31,
                        new Color(.82f, 1, .63f, time * .75f));
            }
            for (int i = used; i < parts.Count && parts[i].enabled; i++) parts[i].enabled = false;
        }

        private void RangeCircle(Vector2 point, float radius)
        {
            float size = radius * 2;
            Part(SpriteArt.Circle, point, Vector2.one * size, 0, 1, new Color(.88f, .72f, .28f, .085f));
            Part(SpriteArt.RangeOutline, point, Vector2.one * size, 0, 2, new Color(.95f, .75f, .31f, .28f));
        }

        public void DrawLabels(System.Action<Vector2, string, Color> text)
        {
            foreach (var burst in bursts)
            {
                if (burst.Effect.Text == null) continue;
                float progress = 1 - burst.Time / burst.Duration;
                Color color = burst.Effect.Kind == EffectKind.Damage ? new Color(1, .64f, .53f) : Color.white;
                color.a = Mathf.Clamp01(2 - progress * 2);
                text(burst.Effect.Point + Vector2.up * (1.55f + progress * 1.22f), burst.Effect.Text, color);
            }
        }

        private void DrawBuilding(BuildingKind kind, int tier, Vector2 point, float angle, Color color, int layer)
        {
            float size = run.Catalog.Building(kind).size * 1.03f;
            Part(SpriteArt.Base(kind, tier), point, Vector2.one * size, 0, layer, color);
            if (SpriteArt.IsTower(kind))
            {
                float turretSize = kind == BuildingKind.Arrow ? size * 1.23f : size;
                Part(SpriteArt.Turret(kind, tier), point, Vector2.one * turretSize, angle, layer + 1, color);
            }
        }

        private void DrawPerson(Vector2 point, float angle, float size, Color color, int layer, float swingTime, Sprite weapon)
        {
            Vector2 forward = RunState.Direction(angle);
            Vector2 right = new(forward.y, -forward.x);
            Part(SpriteArt.Hand, point + forward * (size * .39f) - right * (size * .37f),
                Vector2.one * (size * .30f), 0, layer, color);
            Part(SpriteArt.Hand, point + forward * (size * .39f) + right * (size * .37f),
                Vector2.one * (size * .30f), 0, layer, color);
            if (weapon != null)
            {
                float arc = swingTime > 0 ? Mathf.Sin(swingTime / .22f * Mathf.PI) * 67 : 0;
                Vector2 direction = RunState.Direction(angle + 20 - arc);
                Part(weapon, point + direction * (size * .66f) + right * (size * .11f), Vector2.one * (size * .83f),
                    angle + 18 - arc, layer + 1, Color.white);
            }
            Part(SpriteArt.Body, point, Vector2.one * size, 0, layer + 2, color);
        }

        private void Bar(Vector2 position, float width, float height, float fraction, Color color, int order)
        {
            Part(SpriteArt.Pixel, position, new Vector2(width, height), 0, order, new Color(.22f, .25f, .15f, .82f));
            float fill = (width - .05f) * Mathf.Clamp01(fraction);
            if (fill > 0)
                Part(SpriteArt.Pixel, position + Vector2.left * ((width - fill - .05f) * .5f),
                    new Vector2(fill, height * .69f), 0, order + 1, color);
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
    }
}
