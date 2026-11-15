using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zombs
{
    internal sealed class IndustrialDeposits
    {
        private const int Variants = 4;
        private const int PaletteSize = 8;
        private const int Cell = 64;
        private const int Slate = 0, Coal = 1, Rust = 2, Ore = 3, Bark = 4, Crown = 5, Sap = 6;

        private static readonly Color32[] Palette =
        {
            new(66, 69, 72, 255),
            new(31, 30, 29, 255),
            new(79, 50, 37, 255),
            new(125, 77, 41, 255),
            new(70, 46, 31, 255),
            new(73, 71, 34, 255),
            new(198, 132, 54, 255)
        };

        private readonly Transform parent;
        private readonly Material surface;
        private readonly Texture2D albedo, emission;
        private readonly Mesh[,] deposits = new Mesh[4, Variants];
        private readonly List<(MeshRenderer renderer, MeshFilter filter)> pool = new();
        private readonly MaterialPropertyBlock tint = new();
        private int used;

        public IndustrialDeposits(Transform parent)
        {
            this.parent = parent;
            albedo = Texture(false);
            emission = Texture(true);
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            surface = new Material(shader)
            {
                name = "Basalt, mineral veins and bark",
                mainTexture = albedo,
                enableInstancing = true
            };
            if (surface.HasProperty("_Glossiness")) surface.SetFloat("_Glossiness", .22f);
            if (surface.HasProperty("_Metallic")) surface.SetFloat("_Metallic", .04f);
            if (surface.HasProperty("_EmissionMap"))
            {
                surface.SetTexture("_EmissionMap", emission);
                surface.SetColor("_EmissionColor", Color.white);
                surface.EnableKeyword("_EMISSION");
            }
        }

        public void BeginFrame() => used = 0;

        public void Draw(ResourceNode node)
        {
            if (node.Depleted) return;
            if (used == pool.Count)
            {
                var model = new GameObject("Resource deposit", typeof(MeshFilter), typeof(MeshRenderer));
                model.transform.SetParent(parent, false);
                var renderer = model.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = surface;
                pool.Add((renderer, model.GetComponent<MeshFilter>()));
            }

            var (visible, filter) = pool[used++];
            if (!visible.gameObject.activeSelf) visible.gameObject.SetActive(true);
            int hash = unchecked(Mathf.RoundToInt(node.Position.x * 64) * 73856093 ^
                Mathf.RoundToInt(node.Position.y * 64) * 19349663) & int.MaxValue;
            int variant = hash % Variants, kind = (int)node.Kind;
            Mesh mesh = deposits[kind, variant] ??= Build(node.Kind, variant);
            if (filter.sharedMesh != mesh) filter.sharedMesh = mesh;
            float hit = Mathf.Clamp01(node.Flash * 7);
            visible.transform.SetLocalPositionAndRotation(new Vector3(node.Position.x, node.Position.y, -.035f),
                Quaternion.Euler(0, 0, hash % 360));
            visible.transform.localScale = new Vector3(node.Radius, node.Radius, node.Radius * (1 - hit * .04f));
            if (hit > 0)
            {
                tint.SetColor("_Color", new Color(1 + hit * .20f, 1 + hit * .11f, 1, 1));
                visible.SetPropertyBlock(tint);
            }
            else visible.SetPropertyBlock(null);
        }

        public void EndFrame()
        {
            for (int index = used; index < pool.Count; index++)
                if (pool[index].renderer.gameObject.activeSelf) pool[index].renderer.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            foreach (Mesh mesh in deposits)
                if (mesh != null) Object.Destroy(mesh);
            Object.Destroy(albedo);
            Object.Destroy(emission);
            Object.Destroy(surface);
        }

        private static Mesh Build(ResourceKind kind, int variant)
        {
            var shape = new DepositMesh(73937 + variant * 653 + (int)kind * 1123);
            switch (kind)
            {
                case ResourceKind.Wood:
                    BuildTree(shape);
                    break;
                case ResourceKind.Stone:
                    shape.Rock(Vector3.zero, new Vector3(.86f, .76f, 1.03f), Slate, 9);
                    shape.Rock(new Vector3(-.51f, .17f, 0), new Vector3(.33f, .39f, .38f), Slate, 6);
                    break;
                case ResourceKind.Coal:
                    shape.Rock(new Vector3(-.22f, .11f, 0), new Vector3(.63f, .68f, .68f), Coal, 7);
                    shape.Rock(new Vector3(.38f, -.04f, .01f), new Vector3(.52f, .52f, .49f), Coal, 8);
                    shape.Rock(new Vector3(.04f, -.49f, 0), new Vector3(.44f, .32f, .31f), Slate, 6);
                    break;
                case ResourceKind.Iron:
                    shape.Rock(new Vector3(-.12f, .08f, 0), new Vector3(.77f, .74f, .74f), Rust, 8);
                    shape.Rock(new Vector3(.48f, -.13f, 0), new Vector3(.47f, .45f, .48f), Slate, 6);
                    shape.Crystal(new Vector3(-.36f, -.29f, .29f), new Vector3(-.42f, -.26f, .94f), .19f, Ore);
                    shape.Crystal(new Vector3(.14f, -.30f, .33f), new Vector3(.30f, -.39f, 1.10f), .22f, Ore);
                    shape.Crystal(new Vector3(.07f, .27f, .40f), new Vector3(.14f, .40f, .81f), .14f, Ore);
                    break;
            }

            int rubble = kind == ResourceKind.Wood ? 6 : 4;
            for (int i = 0; i < rubble; i++)
            {
                float angle = (i + shape.Range(-.26f, .26f)) * Mathf.PI * 2 / rubble;
                float radius = shape.Range(.62f, .84f), size = shape.Range(.08f, .17f);
                shape.Rock(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0),
                    new Vector3(size, size * .72f, size * .62f), kind == ResourceKind.Coal ? Coal : Slate, 5);
            }
            return shape.Finish($"{kind} deposit {variant}");
        }

        private static void BuildTree(DepositMesh shape)
        {
            Vector3 basePoint = new(-.08f, .01f, .11f);
            Vector3 knee = new(.13f, -.02f, .46f);
            Vector3 shoulder = new(-.04f, .04f, .87f);
            Vector3 fork = new(.12f, .12f, 1.18f);
            shape.Limb(basePoint, knee, .27f, .21f, Bark, 7);
            shape.Limb(knee, shoulder, .23f, .17f, Bark, 7);
            shape.Limb(shoulder, fork, .18f, .11f, Bark, 7);
            for (int index = 0; index < 6; index++)
            {
                float angle = (index + shape.Range(-.24f, .24f)) * Mathf.PI / 3;
                Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector3 foot = direction * shape.Range(.72f, .92f);
                Vector3 bent = direction * .44f + new Vector3(.06f, .02f, .15f);
                shape.Limb(knee, bent, shape.Range(.09f, .15f), .095f, Bark, 5);
                shape.Limb(bent, foot, .095f, .018f, Bark, 5);
            }

            var tips = new[]
            {
                new Vector3(-.60f, .06f, 1.04f), new Vector3(.61f, .17f, 1.20f),
                new Vector3(-.29f, .53f, 1.36f), new Vector3(.12f, -.42f, 1.18f),
                new Vector3(.12f, .12f, 1.53f)
            };
            for (int index = 0; index < tips.Length; index++)
            {
                Vector3 start = index < 2 ? shoulder : fork;
                Vector3 end = tips[index];
                Vector3 elbow = Vector3.Lerp(start, end, .53f) + new Vector3(0, 0, -.08f);
                shape.Limb(start, elbow, .12f, .075f, Bark, 6);
                shape.Limb(elbow, end, .08f, .032f, Bark, 5);
                shape.Rock(end + new Vector3(0, 0, -.10f),
                    new Vector3(shape.Range(.22f, .34f), shape.Range(.20f, .32f), shape.Range(.21f, .36f)),
                    Crown, 6);
            }

            shape.Crystal(new Vector3(.19f, -.17f, .35f), new Vector3(.23f, -.24f, .68f), .076f, Sap);
            shape.Crystal(new Vector3(-.21f, .02f, .62f), new Vector3(-.25f, .02f, .90f), .062f, Sap);
            shape.Crystal(new Vector3(.23f, .23f, .61f), new Vector3(.22f, .23f, .89f), .069f, Sap);
        }

        private static Texture2D Texture(bool emissive)
        {
            int size = Cell * PaletteSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = emissive ? "Mineral emission" : "Deposit surface atlas",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            var pixels = new Color32[size * size];
            var noise = new System.Random(11689);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int family = y / Cell, shade = x / Cell;
                if (family >= Palette.Length) continue;
                if (emissive)
                {
                    if (family == Sap) pixels[y * size + x] = new Color32(125, 55, 9, 255);
                    continue;
                }
                float u = x % Cell, v = y % Cell;
                float graining = family == Bark
                    ? Mathf.PerlinNoise(u * .28f + 13, v * .05f)
                    : Mathf.PerlinNoise(u * .18f + 13, v * .20f);
                float detail = Mathf.PerlinNoise(u * .065f + shade * 6, v * .068f + family * 6);
                float value = .73f + shade * .048f + .22f * graining + .19f * detail + noise.Next(-9, 10) * .009f;
                Color32 color = Palette[family];
                pixels[y * size + x] = new Color32((byte)Mathf.Min(255, color.r * value),
                    (byte)Mathf.Min(255, color.g * value), (byte)Mathf.Min(255, color.b * value), 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private sealed class DepositMesh
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector2> uvs = new();
            private readonly List<int> triangles = new();
            private readonly System.Random random;

            public DepositMesh(int seed) => random = new System.Random(seed);

            public float Range(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());

            public void Rock(Vector3 center, Vector3 scale, int family, int sides)
            {
                var points = new Vector3[4, sides];
                float phase = Range(0, Mathf.PI * 2);
                float leaningX = Range(-.16f, .16f), leaningY = Range(-.15f, .15f);
                for (int side = 0; side < sides; side++)
                {
                    float angle = phase + (side + Range(-.15f, .15f)) * Mathf.PI * 2 / sides;
                    for (int ring = 0; ring < 4; ring++)
                    {
                        float radius = (ring == 0 ? .76f : ring == 1 ? 1 : ring == 2 ? .78f : .43f) * Range(.87f, 1.07f);
                        float height = ring == 0 ? -.015f : ring == 1 ? Range(.15f, .37f) : ring == 2 ? Range(.50f, .74f) : Range(.78f, .98f);
                        float offset = angle + (ring == 2 ? .16f : 0);
                        var point = new Vector3(Mathf.Cos(offset) * radius + leaningX * height,
                            Mathf.Sin(offset) * radius + leaningY * height, height);
                        points[ring, side] = center + Vector3.Scale(point, scale);
                    }
                }
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    for (int ring = 0; ring < 3; ring++)
                    {
                        Vector3 a = points[ring, side], b = points[ring + 1, side];
                        Vector3 c = points[ring + 1, next], d = points[ring, next];
                        int shade = random.Next(ring == 0 ? 0 : 2, ring == 0 ? 5 : 8);
                        Triangle(a, b, c, family, shade);
                        Triangle(a, c, d, family, Mathf.Clamp(shade + random.Next(-1, 2), 0, 7));
                    }
                    var peak = center + Vector3.Scale(new Vector3(leaningX, leaningY, 1), scale);
                    Triangle(points[3, side], peak, points[3, next], family, random.Next(4, 8));
                }
            }

            public void Limb(Vector3 a, Vector3 b, float radiusA, float radiusB, int family, int sides)
            {
                Vector3 direction = (b - a).normalized;
                Vector3 right = Vector3.Cross(direction, Mathf.Abs(direction.z) > .9f ? Vector3.right : Vector3.forward).normalized;
                Vector3 across = Vector3.Cross(direction, right);
                Vector3 first = right;
                for (int i = 0; i < sides; i++)
                {
                    float angle = (i + 1) * Mathf.PI * 2 / sides;
                    Vector3 second = right * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                    Vector3 v0 = a + first * radiusA, v1 = b + first * radiusB;
                    Vector3 v2 = b + second * radiusB, v3 = a + second * radiusA;
                    int shade = random.Next(0, 8);
                    Triangle(v0, v1, v2, family, shade);
                    Triangle(v0, v2, v3, family, shade);
                    Triangle(v1, b, v2, family, shade);
                    first = second;
                }
            }

            public void Crystal(Vector3 start, Vector3 tip, float radius, int family)
            {
                Vector3 shoulder = Vector3.Lerp(start, tip, .69f);
                Limb(start, shoulder, radius * .72f, radius, family, 5);
                Limb(shoulder, tip, radius, .003f, family, 5);
            }

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int family, int shade)
            {
                int first = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, -a.z));
                vertices.Add(new Vector3(b.x, b.y, -b.z));
                vertices.Add(new Vector3(c.x, c.y, -c.z));
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                const float inset = .105f;
                uvs.Add(new Vector2(shade + inset, family + inset) / PaletteSize);
                uvs.Add(new Vector2(shade + .51f, family + (1 - inset)) / PaletteSize);
                uvs.Add(new Vector2(shade + (1 - inset), family + inset) / PaletteSize);
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                return mesh;
            }
        }
    }
}
