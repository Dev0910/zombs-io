using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    internal sealed class IndustrialGround
    {
        private readonly Transform surface;
        private readonly Material material;
        private readonly Material debrisMaterial;
        private readonly Mesh mesh, debrisMesh;
        private readonly Transform[] debris = new Transform[9];

        public IndustrialGround(Transform parent)
        {
            surface = new GameObject("Lit basalt surface", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            surface.SetParent(parent, false);
            mesh = new Mesh { name = "Basalt ground plane" };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f), new Vector3(.5f, -.5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("Standard")) { mainTexture = SpriteArt.IndustrialWorldTile.texture };
            material.SetFloat("_Glossiness", .12f);
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            debrisMaterial = new Material(Shader.Find("Standard")) { color = new Color(.27f, .30f, .33f) };
            debrisMaterial.SetFloat("_Glossiness", .08f);
            debrisMesh = CreateDebris();
            for (int i = 0; i < debris.Length; i++)
            {
                var patch = new GameObject("Loose basalt chips", typeof(MeshFilter), typeof(MeshRenderer));
                patch.transform.SetParent(parent, false);
                patch.GetComponent<MeshFilter>().sharedMesh = debrisMesh;
                patch.GetComponent<MeshRenderer>().sharedMaterial = debrisMaterial;
                debris[i] = patch.transform;
            }
        }

        public void Hide()
        {
            surface.gameObject.SetActive(false);
            foreach (var patch in debris) patch.gameObject.SetActive(false);
        }

        public void Draw(Vector2 center, float halfWidth, float halfHeight, float night)
        {
            float tile = SpriteArt.IndustrialTileSpan;
            Vector2 span = new(Mathf.Ceil((halfWidth + tile) / tile) * 2, Mathf.Ceil((halfHeight + tile) / tile) * 2);
            surface.gameObject.SetActive(true);
            surface.position = new Vector3(Mathf.Floor(center.x / tile) * tile, Mathf.Floor(center.y / tile) * tile, .015f);
            surface.localScale = new Vector3(span.x * tile, span.y * tile, 1);
            if (material.mainTextureScale != span) material.mainTextureScale = span;
            material.color = Color.Lerp(new Color(.59f, .61f, .63f), new Color(.31f, .36f, .40f), night);
            debrisMaterial.color = Color.Lerp(new Color(.22f, .24f, .26f), new Color(.13f, .16f, .18f), night);
            int column = Mathf.FloorToInt(center.x / tile), row = Mathf.FloorToInt(center.y / tile);
            for (int i = 0; i < debris.Length; i++)
            {
                var patch = debris[i];
                int x = column + i % 3 - 1, y = row + i / 3 - 1;
                patch.position = new Vector3((x + .5f) * tile, (y + .5f) * tile);
                patch.rotation = Quaternion.Euler(0, 0, (unchecked(x * 1723 ^ y * 937) & 3) * 90);
                if (!patch.gameObject.activeSelf) patch.gameObject.SetActive(true);
            }
        }

        private static Mesh CreateDebris()
        {
            var random = new System.Random(5238);
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            float tile = SpriteArt.IndustrialTileSpan;
            for (int i = 0; i < 112; i++)
            {
                float x = ((float)random.NextDouble() - .5f) * tile;
                float y = ((float)random.NextDouble() - .5f) * tile;
                float size = Mathf.Lerp(.045f, .24f, Mathf.Pow((float)random.NextDouble(), 2));
                float rotation = (float)random.NextDouble() * Mathf.PI * 2;
                Vector3 tip = new(x + size * .22f, y - size * .09f, -size * .45f);
                for (int edge = 0; edge < 5; edge++)
                {
                    float angle = rotation + edge * Mathf.PI * .4f, next = angle + Mathf.PI * .4f;
                    indices.Add(vertices.Count);
                    indices.Add(vertices.Count + 1);
                    indices.Add(vertices.Count + 2);
                    vertices.Add(new Vector3(x + Mathf.Cos(next) * size, y + Mathf.Sin(next) * size * .7f, .002f));
                    vertices.Add(new Vector3(x + Mathf.Cos(angle) * size, y + Mathf.Sin(angle) * size * .7f, .002f));
                    vertices.Add(tip);
                }
            }
            var mesh = new Mesh { name = "Basalt chips" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        public void Dispose()
        {
            Object.Destroy(material);
            Object.Destroy(debrisMaterial);
            Object.Destroy(mesh);
            Object.Destroy(debrisMesh);
        }
    }
}
