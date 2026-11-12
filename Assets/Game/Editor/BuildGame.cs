using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zombs
{
    internal static class BuildGame
    {
        private const string ScenePath = "Assets/Game/Survival.unity";
        private const string CatalogPath = "Assets/Game/Balance.asset";
        private const string BuildPath = "Builds/Windows/Zombs.io Offline.exe";

        [MenuItem("Zombs/Set Up Project")]
        public static void SetUp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = GameCatalog.CreateDefaults();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var gameObject = new GameObject("Game", typeof(Camera), typeof(AudioListener));
                gameObject.tag = "MainCamera";
                gameObject.transform.position = new Vector3(0, 0, -20);
                var camera = gameObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 11.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(111, 145, 64, 255);
                gameObject.AddComponent<GameSession>().SetCatalog(catalog);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Offline Games";
            PlayerSettings.productName = "Zombs.io Offline";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "local.zombs.offline");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Zombs/Build Windows x64")]
        public static void BuildWindows() => Build(BuildPath, BuildOptions.None);

        public static void BuildVerification() => Build("Builds/Verification/Zombs Verification.exe", BuildOptions.Development);

        [MenuItem("Zombs/Restore Default Balance")]
        public static void RestoreDefaultBalance()
        {
            var source = GameCatalog.CreateDefaults();
            var target = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (target == null) AssetDatabase.CreateAsset(source, CatalogPath);
            else
            {
                EditorUtility.CopySerialized(source, target);
                UnityEngine.Object.DestroyImmediate(source);
                EditorUtility.SetDirty(target);
            }
            AssetDatabase.SaveAssets();
        }

        public static void PrepareVerification()
        {
            RestoreDefaultBalance();
            BuildVerification();
            BuildWindows();
        }

        private static void Build(string path, BuildOptions options)
        {
            SetUp();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build {result.summary.result}, {result.summary.totalErrors} errors.");
            Debug.Log($"Windows x64 build succeeded: {Path.GetFullPath(path)}");
        }
    }
}
