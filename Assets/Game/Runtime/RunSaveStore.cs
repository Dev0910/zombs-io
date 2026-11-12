using System;
using System.IO;
using UnityEngine;

namespace Zombs
{
    internal sealed class RunSaveStore
    {
        private readonly string path, backup;
        private bool primaryValid;

        public RunSaveStore(string directory)
        {
            path = Path.Combine(directory, "survival-run.json");
            backup = path + ".bak";
        }

        public RunState Load(GameCatalog catalog, out bool recoveredBackup)
        {
            var state = Read(path, catalog);
            primaryValid = state != null;
            if (!primaryValid) state = Read(backup, catalog);
            recoveredBackup = state != null && !primaryValid;
            return state;
        }

        public bool Save(RunState run, bool newRun = false)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, JsonUtility.ToJson(run.Capture()));
                if (File.Exists(path)) File.Replace(temporary, path, primaryValid && !newRun ? backup : null);
                else File.Move(temporary, path);
                primaryValid = true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"Could not save the survival run: {error.Message}");
                Delete(temporary);
                return false;
            }

            if (newRun) Delete(backup);
            return true;
        }

        private static RunState Read(string file, GameCatalog catalog)
        {
            try
            {
                if (!File.Exists(file)) return null;
                var state = RunState.Restore(catalog, JsonUtility.FromJson<RunSnapshot>(File.ReadAllText(file)));
                if (state == null) Debug.LogWarning($"Invalid or incompatible survival checkpoint: {file}");
                return state;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Debug.LogWarning($"Could not load the survival checkpoint: {error.Message}");
                return null;
            }
        }

        private static void Delete(string file)
        {
            try { File.Delete(file); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"Could not remove an old checkpoint file: {error.Message}");
            }
        }
    }
}
