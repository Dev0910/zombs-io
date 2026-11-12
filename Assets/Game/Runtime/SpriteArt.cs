using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    internal static class SpriteArt
    {
        private static readonly Dictionary<string, Sprite> Sprites = new();
        private static readonly Color32 Ink = C(11, 18, 25);
        private static readonly Color32 Paper = C(255, 255, 255);
        private static readonly Color32 Recess = C(22, 34, 43);
        private static readonly Color32 Alloy = C(91, 112, 122);
        private static readonly Color32 Edge = C(143, 167, 174);
        private static readonly Color32 Ivory = C(210, 223, 211);
        private static readonly Color32 Cyan = C(111, 239, 225);
        private static readonly Color32 Amber = C(248, 176, 88);
        private static readonly Color32 Violet = C(192, 151, 243);
        private static readonly Color32 Coral = C(248, 116, 109);
        private static readonly Color32[] Tiers =
        {
            C(77, 97, 108), C(98, 123, 132), C(124, 132, 141), C(141, 132, 109),
            C(119, 145, 156), C(150, 127, 120), C(139, 132, 164), C(119, 160, 150)
        };

        public static Sprite WorldTile
        {
            get
            {
                const string key = "basalt";
                if (Sprites.TryGetValue(key, out var sprite)) return sprite;
                const int size = 512;
                var pixels = new Color32[size * size];
                var random = new System.Random(8517);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1), v = y / (float)(size - 1);
                    float broad = TileNoise(u, v, 4.5f, 2.1f);
                    float fine = TileNoise(u, v, 21f, 11.4f);
                    int shade = Mathf.RoundToInt(broad * 22 + fine * 8 + random.Next(-2, 3));
                    pixels[y * size + x] = C((byte)(24 + shade), (byte)(31 + shade), (byte)(37 + shade));
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear
                };
                texture.SetPixels32(pixels);
                texture.Apply(true, true);
                sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size / 8f, 0, SpriteMeshType.FullRect);
                Sprites.Add(key, sprite);
                return sprite;
            }
        }

        private static float TileNoise(float u, float v, float frequency, float offset)
        {
            float x = u * frequency + offset, y = v * frequency + offset;
            return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - frequency, y), u),
                Mathf.Lerp(Mathf.PerlinNoise(x, y - frequency), Mathf.PerlinNoise(x - frequency, y - frequency), u), v);
        }

        public static Sprite TerrainDetail(int variant) => Get($"terrain-{variant}", a =>
        {
            var random = new System.Random(5287 + variant);
            for (int i = 0; i < 18; i++)
            {
                int x = random.Next(8, 116), y = random.Next(9, 116), size = random.Next(1, 5);
                Color32 stone = i % 4 == 0 ? C(68, 76, 77) : C(37, 44, 47);
                a.Poly(stone, x - size, y, x, y + size, x + size * 2, y + size, x + size, y - size);
            }
            int x0 = random.Next(8, 26), y0 = random.Next(27, 55);
            int x1 = random.Next(34, 53), y1 = y0 + random.Next(-10, 13);
            int x2 = random.Next(58, 79), y2 = y1 + random.Next(-13, 15);
            int x3 = random.Next(95, 119), y3 = y2 + random.Next(-16, 24);
            if (variant != 3)
            {
                a.Stroke(x0, y0, x1, y1, 1.6f, C(24, 31, 36));
                a.Stroke(x1, y1, x2, y2, 1.9f, C(24, 31, 36));
                a.Stroke(x2, y2, x3, y3, 1.3f, C(27, 35, 40));
                if (variant % 2 == 0)
                    a.Stroke(x2, y2, x2 + 12, y2 - 17, 1.1f, C(27, 35, 40));
                a.Stroke(x1, y1 - 2, x2, y2 - 2, .6f, C(62, 72, 74));
            }
            else
            {
                a.Poly(C(48, 64, 66), 24, 82, 47, 104, 76, 101, 86, 81, 66, 68, 44, 71);
                a.Stroke(47, 90, 63, 94, 1.2f, C(78, 94, 88));
            }
        });

        public static Sprite Deposit(ResourceKind kind, int variant = 0) => Get($"deposit-{kind}-{variant}", a =>
        {
            if (kind == ResourceKind.Wood)
            {
                a.Poly(Ink, 6, 55, 17, 31, 33, 32, 37, 12, 61, 17, 80, 8, 95, 32, 116, 39, 123, 66,
                    104, 76, 106, 100, 77, 118, 60, 109, 36, 119, 21, 91, 7, 84);
                a.Poly(C(67, 79, 65), 12, 57, 26, 36, 45, 36, 43, 19, 62, 24, 79, 15, 89, 35, 111, 43,
                    116, 62, 96, 73, 99, 97, 77, 109, 61, 101, 39, 110, 29, 88, 13, 81);
                Branch(a, 54, 47, 28, 77, 23, 93);
                Branch(a, 61, 52, 59, 80, 43, 106);
                Branch(a, 68, 55, 81, 83, 88, 102);
                Branch(a, 77, 54, 97, 65, 111, 60);
                Branch(a, 58, 48, 47, 30, 44, 22);
                Branch(a, 66, 43, 75, 29, 81, 19);
                a.Poly(C(138, 106, 69), 40, 50, 54, 69, 77, 73, 91, 59, 77, 41, 55, 37);
                a.Poly(C(187, 146, 91), 47, 52, 56, 64, 75, 66, 84, 58, 73, 45, 56, 44);
                a.Annulus(65, 55, 11, 2, C(117, 85, 55));
                a.Stroke(57, 50, 66, 60, 2, C(117, 85, 55));
                a.Lobed(31, 80, 12 + variant, 5, .25f, C(122, 139, 85));
                a.Lobed(93, 85, 10, 5, .25f, C(104, 127, 81));
                return;
            }

            Color32 body = kind switch
            {
                ResourceKind.Iron => C(104, 82, 75),
                ResourceKind.Coal => C(51, 57, 68),
                _ => C(96, 111, 116)
            };
            Color32 facet = kind switch
            {
                ResourceKind.Iron => C(187, 116, 83),
                ResourceKind.Coal => C(109, 125, 145),
                _ => C(144, 156, 155)
            };
            int shift = variant * 3;
            a.Poly(Ink, 5, 47, 19, 19, 55, 6, 91, 12, 119, 37, 124, 76, 102, 108, 62, 122, 29, 108, 9, 79);
            a.Poly(body, 11, 48, 24, 24, 55, 12, 89, 17, 113, 41, 117, 75, 99, 103, 62, 116, 33, 102, 15, 77);
            a.Poly(Dark(body, .67f), 11, 48, 31, 58, 57, 33, 87, 37, 113, 41, 89, 17, 55, 12, 24, 24);
            a.Poly(facet, 16, 78, 39, 98, 63, 109, 58 + shift, 83, 31, 58);
            a.Poly(Dark(facet, .82f), 63, 109, 97, 98, 110, 76, 80 + shift, 65, 58 + shift, 83);
            a.Poly(Dark(body, .81f), 31, 58, 58 + shift, 83, 80 + shift, 65, 85, 36, 57, 33);
            if (kind == ResourceKind.Iron)
            {
                a.Stroke(28, 61, 52, 92, 10, Amber);
                a.Stroke(56, 51, 81, 77, 11, C(218, 139, 105));
                a.Stroke(82, 50, 103, 65, 6, C(221, 153, 104));
            }
            else if (kind == ResourceKind.Coal)
            {
                a.Poly(Recess, 20, 50, 35, 80, 52, 66, 41, 40);
                a.Poly(Ink, 57, 50, 77, 80, 99, 63, 86, 43);
                a.Stroke(36, 80, 51, 67, 2.7f, Edge);
                a.Stroke(76, 80, 98, 64, 2.7f, Alloy);
            }
            else
            {
                a.Stroke(33, 69, 49, 88, 2.7f, Ivory);
                a.Stroke(63, 83, 86, 91, 2.3f, Edge);
                a.Stroke(69, 45, 87, 54, 3, Edge);
            }
        });

        private static void Branch(Canvas a, int x1, int y1, int x2, int y2, int x3, int y3)
        {
            a.Stroke(x1, y1, x2, y2, 11, C(103, 88, 65));
            a.Stroke(x2, y2, x3, y3, 7, C(129, 111, 72));
            a.Stroke(x1 - 1, y1 + 2, x2 - 1, y2 + 1, 2, C(169, 144, 92));
        }

        public static Sprite MapDeposit(ResourceKind kind) => Get($"map-{kind}", a =>
        {
            switch (kind)
            {
                case ResourceKind.Wood: a.Poly(C(179, 193, 128), 64, 111, 117, 21, 11, 21); break;
                case ResourceKind.Stone: a.Disc(64, 64, 47, C(189, 207, 211)); break;
                case ResourceKind.Iron: a.Poly(Amber, 64, 8, 120, 64, 64, 120, 8, 64); break;
                case ResourceKind.Coal:
                    a.Box(12, 12, 116, 116, 1, C(171, 184, 209));
                    a.Box(33, 33, 95, 95, 1, Recess);
                    break;
            }
        });

        public static Sprite MapBoundary(bool dashed) => Get(dashed ? "map-air-dashed" : "map-air-edge", a =>
        {
            if (!dashed)
            {
                a.Annulus(64, 64, 63.5f, 2.7f, Paper);
                return;
            }
            for (int i = 0; i < 16; i++)
            {
                float start = i * Mathf.PI * 2 / 16, end = start + .20f;
                a.Stroke(64 + 62 * Mathf.Cos(start), 64 + 62 * Mathf.Sin(start),
                    64 + 62 * Mathf.Cos(end), 64 + 62 * Mathf.Sin(end), 2.2f, Paper);
            }
        });

        public static Sprite AirFill => Get("air-fill", a => a.Disc(64, 64, 63.8f, Paper), 512);
        public static Sprite AirBoundary(bool dashed) => Get(dashed ? "air-dashed" : "air-edge", a =>
        {
            if (!dashed)
            {
                a.Annulus(64, 64, 63.8f, .62f, Paper);
                return;
            }
            for (int index = 0; index < 60; index++)
            {
                float start = index * Mathf.PI * 2 / 60, end = start + .049f;
                a.Stroke(64 + 63.5f * Mathf.Cos(start), 64 + 63.5f * Mathf.Sin(start),
                    64 + 63.5f * Mathf.Cos(end), 64 + 63.5f * Mathf.Sin(end), .66f, Paper);
            }
        }, 512);

        public static Sprite Shadow => Get("shadow", a =>
        {
            for (int radius = 62; radius > 28; radius -= 5)
                a.Disc(64, 64, radius, new Color32(4, 9, 14, (byte)(76 + (62 - radius) * 4)));
        });

        public static Sprite Suit => Get("suit", a =>
        {
            Plate(a, 19, 12, 109, 100, 21, Ink);
            Plate(a, 23, 15, 105, 94, 18, C(110, 132, 135));
            Plate(a, 40, 8, 88, 42, 8, Recess);
            a.Box(46, 15, 81, 26, 2, Alloy);
            a.Stroke(51, 17, 78, 17, 2.5f, Cyan);
            Plate(a, 8, 49, 34, 80, 7, Ink);
            Plate(a, 94, 49, 120, 80, 7, Ink);
            Plate(a, 13, 52, 31, 77, 5, Ivory);
            Plate(a, 97, 52, 115, 77, 5, Ivory);
            Plate(a, 33, 36, 95, 116, 17, Ink);
            Plate(a, 38, 42, 90, 112, 15, Ivory);
            Plate(a, 41, 68, 87, 104, 9, Recess);
            Plate(a, 45, 75, 83, 101, 7, C(65, 136, 153));
            a.Stroke(49, 94, 75, 95, 2.6f, Cyan);
            a.Box(53, 47, 75, 54, 1, C(140, 162, 155));
            a.Box(15, 54, 29, 59, 1, Amber);
        });

        public static Sprite Infected(int variant, bool boss) => Get($"infected-{variant}-{boss}", a =>
        {
            Color32 fabric = variant switch
            {
                0 => C(177, 115, 103),
                1 => C(170, 133, 109),
                _ => C(157, 104, 106)
            };
            a.Poly(Ink, 9, 66, 15, 92, 29, 97, 40, 109, 73, 114, 92, 100, 111, 86, 120, 61,
                108, 43, 91, 48, 84, 22, 67, 8, 38, 13, 29, 36, 13, 45);
            a.Poly(fabric, 15, 66, 20, 88, 36, 90, 47, 106, 73, 108, 87, 94, 106, 82, 113, 62,
                104, 50, 88, 55, 79, 24, 65, 15, 43, 20, 34, 43, 19, 49);
            a.Poly(Dark(fabric, .63f), 33, 44, 40, 26, 65, 16, 80, 34, 77, 53, 60, 67);
            a.Poly(Recess, 42, 63, 48, 96, 71, 103, 89, 85, 80, 63, 57, 52);
            a.Poly(C(179, 168, 134), 50, 65, 50, 88, 66, 97, 81, 84, 74, 67, 58, 59);
            a.Stroke(51, 83, 73, 88, 8, C(255, 143, 111));
            a.Stroke(21, 86, 35, 92, 3.8f, C(229, 148, 119));
            a.Stroke(87, 89, 105, 78, 3, C(226, 132, 115));
            a.Stroke(30, 54, 41, 72, 3, C(206, 177, 132));
            a.Stroke(93, 62, 106, 74, 4, Coral);
            if (!boss) return;
            a.Poly(Ink, 7, 42, 10, 85, 35, 98, 44, 76, 36, 42, 20, 35);
            a.Poly(Ink, 83, 41, 82, 79, 94, 105, 115, 89, 125, 55, 111, 41);
            a.Poly(Violet, 14, 45, 16, 82, 33, 91, 36, 75, 30, 47, 21, 41);
            a.Poly(C(133, 107, 168), 89, 48, 88, 77, 95, 96, 109, 84, 119, 57, 109, 47);
            a.Stroke(50, 79, 78, 85, 7, C(255, 181, 214));
        });

        public static Sprite Limb => Get("limb", a =>
        {
            Plate(a, 22, 12, 106, 114, 25, Ink);
            Plate(a, 28, 20, 99, 107, 18, Paper);
        });

        public static Sprite Hat => Get("helmet-crest", a =>
        {
            a.Poly(Ink, 23, 36, 15, 92, 40, 73, 64, 112, 88, 73, 113, 92, 105, 36);
            a.Poly(Amber, 30, 44, 25, 76, 43, 62, 64, 97, 85, 62, 103, 76, 98, 44);
            a.Box(40, 42, 88, 50, 2, Ivory);
        });

        public static Sprite HostileMarker => Get("hostile", a =>
        {
            a.Poly(Ink, 12, 105, 116, 105, 64, 17);
            a.Poly(C(255, 146, 131), 26, 94, 102, 94, 64, 31);
        });

        public static Sprite FootprintShadow => Get("structure-shadow", a =>
        {
            Plate(a, 1, 1, 127, 127, 15, new Color32(4, 9, 14, 39));
            Plate(a, 5, 5, 123, 123, 13, new Color32(4, 9, 14, 73));
            Plate(a, 10, 10, 118, 118, 11, new Color32(4, 9, 14, 128));
        });

        public static Sprite ThreatMarker => Get("threat-marker", a =>
        {
            a.Poly(Ink, 64, 122, 124, 64, 64, 6, 4, 64);
            a.Poly(Coral, 64, 110, 112, 64, 64, 18, 16, 64);
            a.Box(59, 57, 69, 89, 1, Ink);
            a.Box(59, 39, 69, 50, 1, Ink);
        });

        public static Sprite Base(BuildingKind kind, int tier) => Get($"base-{kind}-{tier}", a =>
        {
            Color32 metal = Tiers[tier - 1];
            Plate(a, 2, 2, 126, 126, 13, Ink);
            Plate(a, 6, 10, 122, 123, 12, Dark(metal, .67f));
            Plate(a, 7, 17, 121, 122, 11, metal);
            Plate(a, 13, 23, 115, 116, 8, Recess);
            a.Stroke(19, 119, 109, 119, 2, Edge);
            a.Stroke(19, 13, 109, 13, 2, Ink);
            foreach (int x in new[] { 17, 111 })
            foreach (int y in new[] { 20, 109 })
            {
                a.Disc(x, y, 5, Ink);
                a.Disc(x, y + 1, 2.1f, Edge);
            }

            switch (kind)
            {
                case BuildingKind.Wall:
                    Plate(a, 20, 27, 108, 107, 8, Alloy);
                    a.Stroke(24, 96, 105, 96, 4, Edge);
                    for (int y = 39; y < 89; y += 15) a.Box(28, y, 100, y + 4, 1, Recess);
                    break;
                case BuildingKind.Door:
                    Plate(a, 23, 21, 105, 111, 8, Dark(Alloy, .88f));
                    a.Box(60, 24, 68, 107, 1, Ink);
                    a.Stroke(36, 82, 55, 65, 6, Cyan);
                    a.Stroke(93, 82, 74, 65, 6, Cyan);
                    break;
                case BuildingKind.SlowTrap:
                    foreach (int x in new[] { 38, 90 })
                    foreach (int y in new[] { 40, 90 })
                    {
                        a.Disc(x, y, 15, Alloy);
                        a.Annulus(x, y, 11, 3, Violet);
                    }
                    break;
                case BuildingKind.Generator:
                    a.Stroke(27, 64, 27, 101, 9, Alloy);
                    a.Stroke(101, 37, 101, 71, 9, Alloy);
                    a.Stroke(25, 76, 25, 98, 3, C(101, 171, 175));
                    a.Stroke(103, 43, 103, 69, 3, Edge);
                    a.Ring(64, 69, 43, 6, Alloy);
                    a.Ring(64, 69, 33, 5, C(65, 124, 134));
                    a.Disc(64, 69, 23, C(73, 183, 180));
                    a.Disc(61, 73, 16, Cyan);
                    a.Stroke(50, 78, 64, 84, 3, Ivory);
                    a.Box(42, 21, 86, 28, 2, C(64, 98, 107));
                    break;
                case BuildingKind.Smelter:
                    for (int y = 49; y <= 96; y += 12)
                    {
                        a.Stroke(17, y, 32, y, 5, Alloy);
                        a.Stroke(97, y, 111, y, 5, Alloy);
                    }
                    Plate(a, 25, 31, 103, 106, 10, Alloy);
                    Plate(a, 32, 42, 96, 98, 7, Ink);
                    a.Poly(Amber, 40, 48, 53, 79, 64, 66, 77, 90, 88, 48);
                    a.Stroke(37, 35, 91, 35, 4, Edge);
                    break;
                case BuildingKind.Assembler:
                    Plate(a, 22, 31, 106, 104, 8, Alloy);
                    a.Stroke(23, 68, 23, 94, 4, Edge);
                    a.Stroke(106, 37, 106, 61, 4, Edge);
                    a.Box(29, 37, 98, 60, 3, Recess);
                    a.Box(40, 43, 84, 54, 2, Cyan);
                    a.Stroke(35, 89, 58, 76, 14, Ink);
                    a.Stroke(35, 89, 58, 76, 8, Amber);
                    a.Stroke(59, 76, 79, 65, 8, Amber);
                    a.Stroke(79, 65, 87, 73, 4, Edge);
                    break;
                case BuildingKind.Miner:
                    Plate(a, 21, 27, 107, 108, 7, Alloy);
                    a.Box(24, 31, 43, 103, 3, Ink);
                    a.Box(85, 31, 104, 103, 3, Ink);
                    for (int y = 37; y < 99; y += 12)
                    {
                        a.Box(28, y, 40, y + 4, 1, Edge);
                        a.Box(89, y, 101, y + 4, 1, Edge);
                    }
                    a.Poly(Amber, 52, 44, 76, 44, 76, 77, 64, 98, 52, 77);
                    a.Stroke(54, 59, 75, 68, 3, Recess);
                    a.Stroke(54, 70, 75, 79, 3, Recess);
                    break;
                default:
                    a.Ring(64, 66, 39, 4, Dark(metal, .7f));
                    a.Annulus(64, 66, 32, 2, Edge);
                    a.Box(40, 27, 88, 31, 2, TowerAccent(kind));
                    break;
            }

            for (int index = 0; index < tier - 1; index++)
                a.Box(34 + index * 9, 14, 39 + index * 9, 18, 1, Ivory);
        });

        public static Sprite Turret(BuildingKind kind, int tier) => Get($"turret-{kind}-{tier}", a =>
        {
            Color32 accent = TowerAccent(kind);
            Color32 metal = Tiers[tier - 1];
            a.Ring(64, 58, 29, 5, metal);
            Plate(a, 45, 33, 83, 75, 7, Recess);
            switch (kind)
            {
                case BuildingKind.Arrow:
                    a.Stroke(42, 41, 42, 102, 10, Ink);
                    a.Stroke(86, 41, 86, 102, 10, Ink);
                    a.Stroke(42, 52, 42, 98, 4, accent);
                    a.Stroke(86, 52, 86, 98, 4, accent);
                    a.Stroke(44, 68, 84, 68, 7, Alloy);
                    Barrel(a, 55, 65, 73, 119, accent);
                    break;
                case BuildingKind.Cannon:
                    Barrel(a, 48, 62, 80, 114, accent);
                    a.Box(52, 99, 76, 112, 1, Ink);
                    break;
                case BuildingKind.Melee:
                    a.Stroke(25, 67, 103, 67, 10, Ink);
                    a.Stroke(25, 67, 103, 67, 4, accent);
                    a.Poly(Ink, 49, 67, 51, 109, 64, 124, 77, 109, 79, 67);
                    a.Poly(Ivory, 55, 70, 56, 107, 64, 116, 72, 107, 73, 70);
                    break;
                case BuildingKind.Bomb:
                    for (int x = 49; x <= 79; x += 15)
                    {
                        a.Ring(x, 80, 14, 4, accent);
                        a.Disc(x, 80, 6, Ink);
                    }
                    break;
                case BuildingKind.Mage:
                    a.Poly(Ink, 29, 79, 39, 113, 63, 100, 89, 113, 100, 79, 64, 58);
                    a.Poly(accent, 36, 80, 42, 105, 60, 91, 89, 105, 93, 81, 64, 65);
                    a.Disc(64, 86, 9, Ivory);
                    break;
            }
            a.Box(53, 43, 75, 49, 2, Edge);
        });

        private static Color32 TowerAccent(BuildingKind kind) => kind switch
        {
            BuildingKind.Arrow => Cyan, BuildingKind.Cannon => Amber, BuildingKind.Melee => Ivory,
            BuildingKind.Bomb => Coral, _ => Violet
        };

        private static void Barrel(Canvas a, int left, int bottom, int right, int top, Color32 accent)
        {
            Plate(a, left, bottom, right, top, 5, Ink);
            Plate(a, left + 4, bottom + 4, right - 4, top - 4, 3, Alloy);
            a.Stroke(left + 5, top - 10, right - 5, top - 10, 3, accent);
        }

        public static Sprite Item(ItemKind item, int tier = 1) => item == ItemKind.Hat ? Hat : Get($"item-{item}-{tier}", a =>
        {
            Color32 accent = tier == 1 ? Cyan : Tiers[Mathf.Min(tier - 1, Tiers.Length - 1)];
            switch (item)
            {
                case ItemKind.Pickaxe:
                    a.Stroke(40, 12, 72, 92, 20, Ink);
                    a.Stroke(41, 15, 70, 89, 10, Alloy);
                    a.Poly(Ink, 13, 62, 28, 90, 67, 111, 111, 103, 119, 79, 91, 88, 57, 84);
                    a.Poly(Ivory, 22, 72, 33, 87, 68, 105, 106, 98, 111, 87, 92, 94, 55, 90);
                    a.Stroke(53, 91, 76, 99, 4, accent);
                    break;
                case ItemKind.Spear:
                    a.Stroke(24, 13, 90, 97, 14, Ink);
                    a.Stroke(24, 14, 90, 96, 6, Alloy);
                    a.Poly(Ink, 72, 71, 79, 108, 119, 122, 108, 83);
                    a.Poly(accent, 78, 79, 85, 104, 112, 115, 103, 88);
                    break;
                case ItemKind.Bow:
                    a.Stroke(27, 30, 101, 90, 8, Ink);
                    a.Stroke(29, 32, 100, 89, 3, Cyan);
                    a.Stroke(27, 29, 33, 69, 15, Ink);
                    a.Stroke(33, 69, 65, 101, 15, Ink);
                    a.Stroke(65, 101, 102, 92, 15, Ink);
                    a.Stroke(28, 30, 34, 68, 7, Alloy);
                    a.Stroke(34, 68, 65, 100, 7, Alloy);
                    a.Stroke(65, 100, 101, 91, 7, Alloy);
                    a.Stroke(41, 41, 84, 100, 6, Ivory);
                    break;
                case ItemKind.Bomb:
                    Plate(a, 24, 20, 104, 99, 20, Ink);
                    Plate(a, 31, 27, 97, 92, 16, Alloy);
                    a.Box(44, 83, 85, 112, 6, Recess);
                    a.Box(42, 53, 86, 69, 2, Coral);
                    break;
                case ItemKind.Shield:
                    a.Poly(Ink, 12, 104, 64, 122, 116, 104, 105, 44, 64, 8, 23, 44);
                    a.Poly(Alloy, 20, 98, 64, 113, 108, 98, 98, 48, 64, 18, 30, 48);
                    a.Poly(Recess, 31, 90, 64, 101, 97, 90, 89, 55, 64, 32, 39, 55);
                    a.Stroke(48, 72, 61, 58, 7, Cyan);
                    a.Stroke(61, 58, 80, 81, 7, Cyan);
                    break;
                case ItemKind.HealthPotion:
                case ItemKind.PetPotion:
                    Plate(a, 32, 12, 96, 113, 11, Ink);
                    Plate(a, 39, 21, 89, 104, 8, Ivory);
                    a.Box(40, 41, 88, 79, 2, item == ItemKind.HealthPotion ? Coral : Violet);
                    a.Box(59, 47, 69, 74, 1, Ink);
                    a.Box(50, 55, 78, 65, 1, Ink);
                    break;
                case ItemKind.Carl:
                    a.Ring(64, 64, 51, 7, Alloy);
                    a.Ring(64, 64, 33, 5, Recess);
                    a.Disc(64, 74, 11, Cyan);
                    a.Box(35, 56, 43, 70, 2, Ivory);
                    a.Box(85, 56, 93, 70, 2, Ivory);
                    break;
                case ItemKind.Woody:
                    Plate(a, 15, 17, 113, 93, 14, Ink);
                    Plate(a, 21, 24, 107, 87, 10, C(123, 133, 92));
                    a.Box(25, 33, 42, 78, 2, Recess);
                    a.Box(85, 33, 102, 78, 2, Recess);
                    a.Stroke(41, 76, 41, 112, 11, Ink);
                    a.Stroke(87, 76, 87, 112, 11, Ink);
                    a.Stroke(41, 76, 41, 108, 5, Ivory);
                    a.Stroke(87, 76, 87, 108, 5, Ivory);
                    a.Box(52, 61, 76, 76, 2, Amber);
                    break;
            }
        });

        public static Sprite Shot(ShotKind kind) => Get($"shot-{kind}", a =>
        {
            Color32 accent = kind switch { ShotKind.Arrow => Cyan, ShotKind.Magic => Violet, _ => Amber };
            if (kind == ShotKind.Arrow)
            {
                a.Poly(accent, 48, 20, 50, 86, 64, 122, 78, 86, 80, 20, 64, 45);
                a.Stroke(64, 48, 64, 101, 5, Ivory);
                return;
            }
            a.Disc(64, 64, 45, accent);
            a.Disc(64, 72, 27, Ivory);
            if (kind == ShotKind.Bomb) a.Annulus(64, 64, 61, 5, Coral);
        });

        public static Sprite Rotor => Get("rotor", a =>
        {
            a.Annulus(64, 64, 59, 4, Paper);
            for (int index = 0; index < 4; index++)
            {
                float angle = index * Mathf.PI * .5f;
                a.Stroke(64 + Mathf.Cos(angle) * 28, 64 + Mathf.Sin(angle) * 28,
                    64 + Mathf.Cos(angle) * 58, 64 + Mathf.Sin(angle) * 58, 7, Paper);
            }
        });

        public static Sprite Glow => Get("glow", a =>
        {
            for (int radius = 63; radius > 0; radius--)
            {
                float fade = 1 - radius / 64f;
                a.Disc(64, 64, radius, new Color32(255, 255, 255, (byte)(fade * fade * 255)));
            }
        }, 64);

        public static Sprite Circle => Get("circle", a => a.Disc(64, 64, 61, Paper));
        public static Sprite Outline => Get("outline", a => a.Annulus(64, 64, 60, 4, Paper));
        public static Sprite RangeOutline => AirBoundary(false);
        public static Sprite Pixel => Get("pixel", a => a.Box(0, 0, 128, 128, 0, Paper));
        public static Sprite ShopIcon => Get("shop-icon", a =>
        {
            Plate(a, 11, 22, 117, 91, 9, Paper);
            Plate(a, 20, 32, 108, 81, 4, new Color32(0, 0, 0, 0));
            a.Stroke(40, 91, 48, 107, 7, Paper);
            a.Stroke(48, 107, 80, 107, 7, Paper);
            a.Stroke(80, 107, 88, 91, 7, Paper);
            a.Box(52, 52, 76, 60, 1, Paper);
        });
        public static Sprite GearIcon => Get("gear-icon", a =>
        {
            a.Annulus(64, 64, 41, 16, Paper);
            foreach (int x in new[] { 29, 64, 99 })
            foreach (int y in new[] { 29, 64, 99 })
                if (x != 64 || y != 64) a.Box(x - 9, y - 9, x + 9, y + 9, 2, Paper);
        });
        public static Sprite ClockIcon => Get("clock-icon", a =>
        {
            a.Annulus(64, 64, 52, 9, Paper);
            a.Stroke(64, 64, 64, 95, 8, Paper);
            a.Stroke(64, 64, 86, 49, 8, Paper);
        });
        public static Sprite HealIcon => Get("heal-icon", a =>
        {
            a.Box(52, 15, 76, 113, 2, Paper);
            a.Box(15, 52, 113, 76, 2, Paper);
        });
        public static Sprite Whistle => Get("whistle", a =>
        {
            a.Annulus(48, 62, 28, 8, Paper);
            a.Stroke(71, 70, 108, 86, 10, Paper);
            a.Stroke(73, 55, 112, 62, 9, Paper);
        });

        public static bool IsTower(BuildingKind kind) => kind >= BuildingKind.Arrow && kind <= BuildingKind.Mage;

        private static void Plate(Canvas a, float l, float b, float r, float t, float cut, Color32 color) =>
            a.Poly(color, l + cut, b, r - cut, b, r, b + cut, r, t - cut, r - cut, t, l + cut, t, l, t - cut, l, b + cut);
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
                        red += p.r * p.a;
                        green += p.g * p.a;
                        blue += p.b * p.a;
                        alpha += p.a;
                    }
                    if (alpha > 0)
                        result[y * resolution + x] = new Color32((byte)(red / alpha), (byte)(green / alpha), (byte)(blue / alpha), (byte)(alpha / 4));
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
