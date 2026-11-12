using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    internal static class SpriteArt
    {
        private static readonly Dictionary<string, Sprite> Sprites = new();
        private static readonly Color32 Ink = C(46, 43, 47);
        private static readonly Color32 Paper = C(255, 255, 255);
        private static readonly Color32 Plank = C(137, 92, 66);
        private static readonly Color32 Steel = C(98, 99, 101);
        private static readonly Color32 SteelLight = C(130, 131, 133);
        private static readonly Color32 Gold = C(249, 176, 74);
        private static readonly Color32 Skin = C(255, 204, 123);
        private static readonly Color32[] Tiers =
        {
            Plank, C(146, 145, 149), C(178, 119, 67), C(179, 164, 91),
            C(185, 204, 215), C(202, 102, 101), C(120, 117, 224), C(79, 194, 137)
        };

        public static Sprite WorldTile => Get("grass", a =>
        {
            a.Box(0, 0, 128, 128, 0, C(111, 145, 64));
            a.Box(0, 0, 128, 8, 0, C(100, 132, 59));
            a.Box(0, 0, 8, 128, 0, C(100, 132, 59));
            a.Box(8, 8, 120, 120, 0, C(114, 149, 66));
        });

        public static Sprite Tree => Get("tree", a =>
        {
            a.Lobed(64, 64, 62, 7, .095f, Ink);
            a.Lobed(64, 64, 56, 7, .095f, C(82, 107, 51));
            a.Lobed(64, 64, 25, 7, .08f, C(94, 125, 54));
        });

        public static Sprite Rock => Get("stone", a =>
        {
            a.Lobed(64, 64, 62, 4, .055f, Ink);
            a.Lobed(64, 64, 54.5f, 4, .055f, C(168, 169, 174));
            a.Lobed(57, 63, 32, 4, .032f, C(188, 189, 193));
        });

        public static Sprite Deposit(ResourceKind kind) => kind switch
        {
            ResourceKind.Wood => Tree,
            ResourceKind.Stone => Rock,
            ResourceKind.Iron => Get("iron-ore", a =>
            {
                a.Poly(Ink, 5, 57, 20, 28, 62, 6, 102, 18, 123, 59, 103, 102, 60, 121, 24, 99);
                a.Poly(C(125, 92, 79), 13, 57, 27, 34, 65, 15, 95, 26, 114, 59, 95, 96, 61, 112, 31, 92);
                a.Stroke(28, 50, 56, 79, 15, C(213, 143, 102));
                a.Stroke(57, 57, 86, 88, 15, C(190, 112, 79));
                a.Stroke(85, 53, 99, 69, 10, C(213, 143, 102));
            }),
            _ => Get("coal", a =>
            {
                a.Poly(Ink, 4, 48, 18, 24, 62, 18, 96, 9, 120, 38, 112, 88, 78, 120, 43, 106, 12, 88);
                a.Poly(C(68, 71, 81), 13, 50, 24, 31, 65, 26, 93, 18, 112, 41, 104, 84, 76, 111, 44, 97, 21, 82);
                a.Poly(C(112, 114, 127), 22, 65, 44, 95, 67, 84, 52, 55);
                a.Poly(C(91, 94, 109), 67, 56, 81, 84, 105, 70, 109, 43, 88, 34);
            })
        };

        public static Sprite MapDeposit(ResourceKind kind) => Get($"map-{kind}", a =>
        {
            switch (kind)
            {
                case ResourceKind.Stone:
                    a.Disc(64, 64, 55, Ink);
                    a.Disc(64, 64, 43, C(192, 195, 204));
                    break;
                case ResourceKind.Iron:
                    a.Poly(Ink, 64, 2, 126, 64, 64, 126, 2, 64);
                    a.Poly(C(234, 155, 104), 64, 17, 111, 64, 64, 111, 17, 64);
                    break;
                case ResourceKind.Coal:
                    a.Box(8, 8, 120, 120, 1, C(194, 199, 219));
                    a.Box(19, 19, 109, 109, 1, Ink);
                    break;
            }
        });

        public static Sprite AirFill => Get("air-fill", a => a.Disc(64, 64, 63.8f, Paper), 512);

        public static Sprite AirBoundary(bool dashed) => Get(dashed ? "air-dashed" : "air-edge", a =>
        {
            if (!dashed)
            {
                a.Annulus(64, 64, 63.8f, .75f, Paper);
                return;
            }
            for (int index = 0; index < 48; index++)
            {
                float start = index * Mathf.PI * 2 / 48;
                float end = start + .062f;
                a.Stroke(64 + 63 * Mathf.Cos(start), 64 + 63 * Mathf.Sin(start),
                    64 + 63 * Mathf.Cos(end), 64 + 63 * Mathf.Sin(end), .8f, Paper);
            }
        }, 512);

        public static Sprite Body => Get("body", a => a.Ring(64, 64, 62, 8, Paper));
        public static Sprite Hand => Body;
        public static Sprite Hat => Get("hat", a =>
        {
            a.Poly(Ink, 8, 106, 21, 86, 36, 80, 43, 61, 28, 44, 14, 58, 6, 79);
            a.Poly(Ink, 120, 106, 107, 86, 92, 80, 85, 61, 100, 44, 114, 58, 122, 79);
            a.Poly(Gold, 12, 94, 20, 80, 32, 75, 36, 62, 28, 51, 18, 62, 11, 80);
            a.Poly(Gold, 116, 94, 108, 80, 96, 75, 92, 62, 100, 51, 110, 62, 117, 80);
        });

        public static Sprite Crown => Get("crown", a =>
        {
            a.Poly(Ink, 10, 32, 4, 93, 42, 71, 64, 112, 86, 71, 124, 93, 118, 32);
            a.Poly(Gold, 19, 42, 16, 77, 43, 58, 64, 94, 84, 58, 112, 77, 109, 42);
        });

        public static Sprite Base(BuildingKind kind, int tier) => Get($"base-{kind}-{tier}", a =>
        {
            var material = Tiers[tier - 1];
            if (kind == BuildingKind.SlowTrap)
            {
                a.Box(5, 5, 123, 123, 14, C(68, 67, 45));
                a.Box(10, 10, 118, 118, 12, material);
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    a.Disc(38 + x * 51, 38 + y * 51, 18, C(108, 72, 49));
                    a.Disc(38 + x * 51, 38 + y * 51, 12, C(217, 127, 63));
                }
                return;
            }
            a.Box(2, 2, 126, 126, 13, Ink);
            a.Box(10, 10, 118, 118, 7, material);
            a.Box(12, 13, 116, 28, 3, Dark(material, .82f));
            if (kind == BuildingKind.Wall)
                a.Box(19, 22, 109, 106, 8, Dark(material, .91f));
            if (kind == BuildingKind.Door)
            {
                a.Box(25, 6, 103, 122, 4, Ink);
                a.Box(34, 10, 94, 118, 4, Dark(material, .8f));
                a.Box(39, 22, 49, 108, 3, material);
                a.Disc(80, 65, 9, Gold);
            }
            if (kind == BuildingKind.Generator)
            {
                a.Box(18, 22, 110, 103, 12, Ink);
                a.Box(25, 28, 103, 96, 9, Steel);
                a.Ring(64, 62, 33, 7, C(52, 100, 107));
                a.Disc(64, 62, 21, C(123, 225, 218));
                a.Disc(58, 69, 8, C(208, 252, 241));
                a.Box(25, 99, 103, 111, 3, C(104, 185, 177));
            }
            if (kind == BuildingKind.Smelter)
            {
                a.Box(18, 20, 110, 108, 10, Ink);
                a.Box(25, 27, 103, 101, 7, Steel);
                a.Box(34, 44, 94, 87, 6, Ink);
                a.Poly(C(238, 134, 63), 40, 50, 50, 70, 61, 62, 72, 80, 89, 50);
                a.Box(39, 31, 89, 39, 2, C(175, 180, 183));
            }
            if (kind == BuildingKind.Assembler)
            {
                a.Box(17, 20, 111, 107, 9, Ink);
                a.Box(24, 27, 104, 99, 6, SteelLight);
                a.Box(32, 36, 96, 59, 3, Steel);
                a.Box(49, 42, 80, 53, 4, C(178, 213, 225));
                a.Stroke(35, 82, 65, 73, 15, Ink);
                a.Stroke(37, 82, 63, 74, 8, C(221, 160, 87));
                a.Stroke(66, 74, 78, 63, 9, C(221, 160, 87));
            }
            if (kind == BuildingKind.Miner)
            {
                a.Box(14, 17, 114, 108, 8, Ink);
                a.Box(22, 25, 106, 100, 4, Steel);
                for (int i = 0; i < 3; i++)
                    a.Box(29 + i * 25, 31, 45 + i * 25, 94, 3, i == 1 ? Gold : Plank);
                a.Box(23, 49, 106, 67, 3, Ink);
                a.Box(29, 53, 99, 62, 2, C(156, 176, 104));
            }
            if (IsTower(kind))
            {
                a.Ring(64, 64, 48, 8, Steel);
                a.Ring(64, 64, 37, 5, Dark(material, .90f));
            }
        });

        public static Sprite Turret(BuildingKind kind, int tier) => Get($"turret-{kind}-{tier}", a =>
        {
            var color = Tiers[tier - 1];
            switch (kind)
            {
                case BuildingKind.Arrow:
                    a.Stroke(17, 61, 64, 82, 15, Ink);
                    a.Stroke(111, 61, 64, 82, 15, Ink);
                    a.Stroke(17, 61, 64, 82, 7, color);
                    a.Stroke(111, 61, 64, 82, 7, color);
                    a.Stroke(17, 59, 64, 23, 3, C(247, 211, 142));
                    a.Stroke(111, 59, 64, 23, 3, C(247, 211, 142));
                    a.Box(48, 13, 80, 125, 6, Ink);
                    a.Box(55, 20, 73, 120, 4, color);
                    a.Box(60, 20, 66, 118, 2, SteelLight);
                    a.Poly(Ink, 31, 27, 97, 27, 85, 3, 43, 3);
                    a.Poly(color, 39, 21, 89, 21, 80, 8, 48, 8);
                    break;
                case BuildingKind.Cannon:
                    a.Box(19, 24, 109, 79, 16, Ink);
                    a.Box(26, 31, 102, 74, 12, color);
                    a.Box(38, 27, 90, 121, 11, Ink);
                    a.Box(45, 35, 83, 114, 7, Steel);
                    a.Box(44, 85, 84, 102, 3, SteelLight);
                    a.Box(38, 111, 90, 124, 3, Ink);
                    break;
                case BuildingKind.Melee:
                    a.Box(24, 19, 104, 102, 12, Ink);
                    a.Box(33, 28, 95, 94, 9, color);
                    a.Box(39, 34, 89, 87, 8, Steel);
                    for (int i = 0; i < 3; i++)
                    {
                        int x = 32 + i * 28;
                        a.Poly(Ink, x - 11, 91, x + 11, 91, x, 126);
                        a.Poly(SteelLight, x - 5, 97, x + 5, 97, x, 116);
                    }
                    break;
                case BuildingKind.Bomb:
                    a.Ring(64, 64, 48, 7, C(63, 61, 61));
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4;
                        a.Disc(64 + Mathf.Cos(angle) * 30, 64 + Mathf.Sin(angle) * 30, 5, color);
                    }
                    a.Ring(64, 64, 21, 6, Steel);
                    a.Disc(64, 64, 8, Ink);
                    break;
                case BuildingKind.Mage:
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.PI / 2 * i + Mathf.PI / 4;
                        a.Ring(64 + Mathf.Cos(angle) * 39, 64 + Mathf.Sin(angle) * 39, 14, 6, color);
                    }
                    a.Ring(64, 64, 35, 7, Steel);
                    a.Ring(64, 64, 23, 6, C(62, 173, 218));
                    a.Disc(59, 69, 6, C(130, 228, 249));
                    break;
            }
        });

        public static Sprite Item(ItemKind item, int tier = 1) => item == ItemKind.Hat ? Hat : Get($"item-{item}-{tier}", a =>
        {
            var color = Tiers[Mathf.Clamp(tier - 1, 0, Tiers.Length - 1)];
            switch (item)
            {
                case ItemKind.Pickaxe:
                    a.Stroke(64, 13, 64, 93, 15, Ink);
                    a.Stroke(64, 13, 64, 93, 7, Plank);
                    a.Poly(Ink, 33, 79, 43, 109, 70, 120, 94, 96, 84, 70, 55, 68);
                    a.Poly(tier == 1 ? Steel : color, 41, 81, 50, 105, 68, 111, 84, 93, 77, 78, 57, 76);
                    break;
                case ItemKind.Spear:
                    a.Stroke(64, 6, 64, 97, 14, Ink);
                    a.Stroke(64, 6, 64, 97, 6, Plank);
                    a.Poly(Ink, 42, 81, 64, 126, 86, 81, 64, 67);
                    a.Poly(SteelLight, 50, 83, 64, 115, 78, 83, 64, 76);
                    break;
                case ItemKind.Bow:
                    a.Stroke(19, 41, 109, 41, 3, C(246, 223, 157));
                    a.Stroke(19, 41, 38, 69, 11, Ink);
                    a.Stroke(38, 69, 64, 80, 11, Ink);
                    a.Stroke(64, 80, 91, 69, 11, Ink);
                    a.Stroke(91, 69, 109, 41, 11, Ink);
                    a.Stroke(20, 43, 39, 69, 4, Plank);
                    a.Stroke(39, 69, 64, 80, 4, Plank);
                    a.Stroke(64, 80, 91, 69, 4, Plank);
                    a.Stroke(91, 69, 108, 43, 4, Plank);
                    a.Stroke(64, 25, 64, 105, 7, Ink);
                    a.Poly(SteelLight, 55, 91, 64, 109, 73, 91);
                    break;
                case ItemKind.Bomb:
                    a.Ring(59, 57, 43, 8, Steel);
                    a.Disc(47, 69, 12, SteelLight);
                    a.Stroke(85, 87, 104, 107, 12, Ink);
                    a.Disc(106, 108, 6, Gold);
                    break;
                case ItemKind.Shield:
                    a.Poly(Ink, 15, 104, 113, 104, 106, 48, 64, 10, 22, 48);
                    a.Poly(C(117, 145, 184), 24, 95, 104, 95, 96, 51, 64, 21, 32, 51);
                    a.Poly(SteelLight, 33, 86, 64, 86, 64, 36, 41, 56);
                    break;
                case ItemKind.HealthPotion:
                case ItemKind.PetPotion:
                    a.Ring(56, 53, 40, 7, item == ItemKind.HealthPotion ? C(190, 57, 52) : C(126, 82, 195));
                    a.Stroke(77, 78, 93, 104, 23, Ink);
                    a.Stroke(77, 78, 91, 101, 12, C(166, 141, 120));
                    a.Disc(40, 64, 7, C(238, 162, 151));
                    break;
                case ItemKind.Carl:
                case ItemKind.Woody:
                    a.Ring(34, 43, 17, 5, Skin);
                    a.Ring(94, 43, 17, 5, Skin);
                    a.Ring(64, 67, 42, 7, item == ItemKind.Carl ? C(200, 162, 109) : C(162, 108, 70));
                    a.Disc(50, 69, 5, Ink);
                    a.Disc(78, 69, 5, Ink);
                    break;
            }
        });

        public static Sprite Arrow => Get("arrow", a =>
        {
            a.Stroke(64, 13, 64, 101, 15, Ink);
            a.Stroke(64, 13, 64, 101, 6, C(155, 122, 99));
            a.Poly(Ink, 37, 83, 64, 124, 91, 83);
            a.Poly(SteelLight, 49, 87, 64, 114, 79, 87);
        });

        public static Sprite Orb(bool magic) => Get(magic ? "magic" : "ball", a =>
            a.Ring(64, 64, 57, 12, magic ? C(82, 189, 242) : C(87, 85, 91)));
        public static Sprite Circle => Get("circle", a => a.Disc(64, 64, 61, Paper));
        public static Sprite Outline => Get("outline", a => a.Annulus(64, 64, 60, 5, Paper));
        public static Sprite RangeOutline => Get("range-outline", a => a.Annulus(64, 64, 61, 1.1f, Paper));
        public static Sprite Pixel => Get("pixel", a => a.Box(0, 0, 128, 128, 0, Paper));

        public static Sprite ShopIcon => Get("shop-icon", a =>
        {
            a.Box(17, 19, 111, 76, 5, Paper);
            a.Box(28, 27, 53, 59, 2, new Color32(0, 0, 0, 0));
            a.Box(67, 19, 98, 58, 2, new Color32(0, 0, 0, 0));
            a.Poly(Paper, 11, 81, 117, 81, 101, 113, 27, 113);
            for (int i = 0; i < 3; i++)
                a.Stroke(39 + 25 * i, 83, 45 + 19 * i, 110, 5, new Color32(0, 0, 0, 0));
        });

        public static Sprite GearIcon => Get("gear-icon", a =>
        {
            a.Lobed(64, 64, 57, 8, .16f, Paper);
            a.Disc(64, 64, 18, new Color32(0, 0, 0, 0));
        });

        public static Sprite ClockIcon => Get("clock-icon", a =>
        {
            a.Annulus(64, 64, 58, 8, Paper);
            a.Stroke(64, 64, 64, 98, 8, Paper);
            a.Stroke(64, 64, 83, 50, 8, Paper);
        });

        public static Sprite HealIcon => Get("heal-icon", a =>
        {
            a.Box(49, 12, 79, 116, 6, Paper);
            a.Box(12, 49, 116, 79, 6, Paper);
        });

        public static Sprite Whistle => Get("whistle", a =>
        {
            a.Ring(41, 58, 31, 7, C(125, 79, 181));
            a.Box(38, 46, 116, 80, 6, Ink);
            a.Box(43, 53, 109, 73, 4, C(125, 79, 181));
            a.Disc(43, 61, 13, C(145, 95, 199));
        });

        public static bool IsTower(BuildingKind kind) => kind >= BuildingKind.Arrow && kind <= BuildingKind.Mage;

        private static Sprite Get(string key, Action<Canvas> draw, int resolution = 128)
        {
            if (Sprites.TryGetValue(key, out var sprite)) return sprite;
            var canvas = new Canvas(resolution);
            draw(canvas);
            var texture = canvas.Texture(key);
            sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(.5f, .5f), resolution, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Sprites.Add(key, sprite);
            return sprite;
        }

        private static Color32 C(byte r, byte g, byte b) => new(r, g, b, 255);
        private static Color32 Dark(Color32 color, float scale) => C((byte)(color.r * scale), (byte)(color.g * scale), (byte)(color.b * scale));

        private sealed class Canvas
        {
            private readonly int resolution, size;
            private readonly float samplesPerUnit;
            private readonly Color32[] pixels;

            public Canvas(int resolution)
            {
                this.resolution = resolution;
                size = resolution * 2;
                samplesPerUnit = size / 128f;
                pixels = new Color32[size * size];
            }

            public void Disc(float x, float y, float radius, Color32 color) =>
                Paint(x - radius, y - radius, x + radius, y + radius, color,
                    (px, py) => (px - x) * (px - x) + (py - y) * (py - y) <= radius * radius);

            public void Ring(float x, float y, float radius, float line, Color32 fill)
            {
                Disc(x, y, radius, Ink);
                Disc(x, y, radius - line, fill);
            }

            public void Annulus(float x, float y, float radius, float line, Color32 color)
            {
                Disc(x, y, radius, color);
                Disc(x, y, radius - line, new Color32(0, 0, 0, 0));
            }

            public void Box(float left, float bottom, float right, float top, float radius, Color32 color) =>
                Paint(left, bottom, right, top, color, (x, y) =>
                {
                    float dx = x - Mathf.Clamp(x, left + radius, right - radius);
                    float dy = y - Mathf.Clamp(y, bottom + radius, top - radius);
                    return dx * dx + dy * dy <= radius * radius;
                });

            public void Stroke(float x1, float y1, float x2, float y2, float width, Color32 color)
            {
                Vector2 start = new(x1, y1), delta = new(x2 - x1, y2 - y1);
                float radius = width * .5f;
                Paint(Mathf.Min(x1, x2) - radius, Mathf.Min(y1, y2) - radius,
                    Mathf.Max(x1, x2) + radius, Mathf.Max(y1, y2) + radius, color, (x, y) =>
                    {
                        Vector2 point = new Vector2(x, y) - start;
                        return (point - delta * Mathf.Clamp01(Vector2.Dot(point, delta) / delta.sqrMagnitude)).sqrMagnitude <= radius * radius;
                    });
            }

            public void Lobed(float x, float y, float radius, int lobes, float amount, Color32 color)
            {
                var points = new float[144];
                for (int i = 0; i < points.Length / 2; i++)
                {
                    float angle = i * Mathf.PI * 4 / points.Length;
                    float length = radius * (1 - amount * .5f + Mathf.Sin(angle * lobes) * amount * .5f);
                    points[2 * i] = x + Mathf.Cos(angle) * length;
                    points[2 * i + 1] = y + Mathf.Sin(angle) * length;
                }
                Poly(color, points);
            }

            public void Poly(Color32 color, params float[] points)
            {
                float left = 128, bottom = 128, right = 0, top = 0;
                for (int i = 0; i < points.Length; i += 2)
                {
                    left = Mathf.Min(left, points[i]);
                    bottom = Mathf.Min(bottom, points[i + 1]);
                    right = Mathf.Max(right, points[i]);
                    top = Mathf.Max(top, points[i + 1]);
                }
                Paint(left, bottom, right, top, color, (x, y) =>
                {
                    bool inside = false;
                    for (int a = 0, b = points.Length - 2; a < points.Length; b = a, a += 2)
                    {
                        float ax = points[a], ay = points[a + 1], bx = points[b], by = points[b + 1];
                        if ((ay > y) != (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) inside = !inside;
                    }
                    return inside;
                });
            }

            private void Paint(float left, float bottom, float right, float top, Color32 color, Func<float, float, bool> covers)
            {
                int lastX = Mathf.Min(size, Mathf.CeilToInt(right * samplesPerUnit));
                int lastY = Mathf.Min(size, Mathf.CeilToInt(top * samplesPerUnit));
                for (int y = Mathf.Max(0, Mathf.FloorToInt(bottom * samplesPerUnit)); y < lastY; y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(left * samplesPerUnit)); x < lastX; x++)
                    if (covers((x + .5f) / samplesPerUnit, (y + .5f) / samplesPerUnit)) pixels[y * size + x] = color;
            }

            public Texture2D Texture(string name)
            {
                var result = new Color32[resolution * resolution];
                for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    int red = 0, green = 0, blue = 0, alpha = 0;
                    for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        var p = pixels[(y * 2 + dy) * size + x * 2 + dx];
                        if (p.a == 0) continue;
                        red += p.r;
                        green += p.g;
                        blue += p.b;
                        alpha++;
                    }
                    if (alpha > 0)
                        result[y * resolution + x] = new Color32((byte)(red / alpha), (byte)(green / alpha), (byte)(blue / alpha), (byte)(255 * alpha / 4));
                }
                var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
                {
                    name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                texture.SetPixels32(result);
                texture.Apply(false, true);
                return texture;
            }
        }
    }
}
