using System;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Loads/saves this device's progress (soul shards, mastery, trinkets, selected class).
    /// Use -profile &lt;name&gt; to keep separate saves (e.g. several instances on one PC).</summary>
    public static class ProfileStore
    {
        private static ProfileData _current;

        public static event Action Changed;

        public static ProfileData Current => _current ??= Load();

        private static string FilePath
        {
            get
            {
                string profile = CommandLine.GetValue("-profile");
                string name = string.IsNullOrEmpty(profile) ? "profile.json" : $"profile-{profile}.json";
                return Path.Combine(Application.persistentDataPath, name);
            }
        }

        private static ProfileData Load()
        {
            try
            {
                var p = File.Exists(FilePath) ? ProfileData.FromJson(File.ReadAllText(FilePath)) : ProfileData.Default();
                // Automated runs can pick a class: -class warrior|mage|ranger
                string cls = CommandLine.GetValue("-class");
                if (cls != null && Enum.TryParse(cls, true, out ClassId id)) p.selectedClass = id;
                return p;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileStore] Could not load profile, starting fresh: {ex.Message}");
                return ProfileData.Default();
            }
        }

        public static void Save()
        {
            if (_current == null) return;
            try
            {
                File.WriteAllText(FilePath, _current.ToJson());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileStore] Could not save profile: {ex.Message}");
            }
            Changed?.Invoke();
        }
    }

    public static class CommandLine
    {
        public static bool Has(string flag) => Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;

        public static string GetValue(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static float GetFloat(string flag, float fallback) =>
            float.TryParse(GetValue(flag), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }
}
