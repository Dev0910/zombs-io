using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Zombs
{
    internal static class IndustrialIconBaker
    {
        private const string Output = "Assets/Game/Resources/Presentation/IndustrialIcons";
        private const int Resolution = 128;

        [MenuItem("Zombs/Bake Industrial Icons")]
        public static void Bake()
        {
            Directory.CreateDirectory(Output);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Industrial icon studio");
            SceneManager.MoveGameObjectToScene(root, scene);
            var camera = new GameObject("Icon camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.transform.rotation = Quaternion.Euler(-39, -19, 0);
            AddLight(root.transform, "Key", new Color(1, .94f, .83f), .93f, new Vector3(21, 32, -7));
            AddLight(root.transform, "Fill", new Color(.65f, .83f, 1), .43f, new Vector3(-43, -36, 0));
            AddLight(root.transform, "Rim", new Color(.65f, .87f, 1), .48f, new Vector3(38, -58, 0));

            var render = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = render;
            IndustrialMachines machines = null;
            IndustrialActors actors = null;
            try
            {
                machines = new IndustrialMachines(root.transform);
                actors = new IndustrialActors(root.transform);
                actors.BeginFrame();
                actors.EndFrame();
                foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
                    SaveIcon(camera, pixels, render, machines.CreateIconModel(kind), $"building-{kind}");
                foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
                    SaveIcon(camera, pixels, render, actors.CreateIconModel(kind), $"item-{kind}");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                actors?.Dispose();
                machines?.Dispose(immediate: true);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(render);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.Refresh();
            Debug.Log($"Baked industrial HUD icons to {Output}");
        }

        private static void AddLight(Transform root, string name, Color color, float intensity, Vector3 angles)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.rotation = Quaternion.Euler(angles);
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static void SaveIcon(Camera camera, Texture2D pixels, RenderTexture render, Transform model, string name)
        {
            if (model == null) return;
            model.SetParent(camera.transform.parent, false);
            model.gameObject.SetActive(true);
            try
            {
                Quaternion rotation = camera.transform.rotation;
                Quaternion inverse = Quaternion.Inverse(rotation);
                Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = -min;
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = inverse * filter.transform.TransformPoint(vertex);
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
                Vector3 center = rotation * ((min + max) * .5f);
                camera.transform.position = center - camera.transform.forward * 8;
                camera.orthographicSize = Mathf.Max(max.x - min.x, max.y - min.y) * .59f;
                camera.Render();
                RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                pixels.Apply(false);
                File.WriteAllBytes($"{Output}/{name}.png", pixels.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(model.gameObject);
            }
        }
    }
}
