using System;
using System.IO;
using UnityEngine;

namespace Zombs
{
    [Serializable]
    internal sealed class LocalProfile
    {
        public string nickname = "Player";
        public int bestWave, bestScore;
        public bool muted, fullscreen, hints = true;
        private static string FilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        public static LocalProfile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<LocalProfile>(File.ReadAllText(FilePath)) ?? new LocalProfile();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Debug.LogWarning($"Could not read the local player profile: {error.Message}");
            }
            return new LocalProfile();
        }

        public void Save()
        {
            string path = FilePath;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, JsonUtility.ToJson(this, true));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"Could not save the local player profile: {error.Message}");
                try { File.Delete(temporary); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}
