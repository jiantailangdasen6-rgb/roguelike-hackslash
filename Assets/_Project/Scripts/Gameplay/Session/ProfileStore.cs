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

        // Browsers have no real file system; PlayerPrefs is persisted to IndexedDB by Unity.
        private static bool UsePrefs => Application.platform == RuntimePlatform.WebGLPlayer;
        private const string PrefsKey = "profile";

        private static ProfileData Load()
        {
            try
            {
                if (!UsePrefs) MigrateFromOldName();
                var p = UsePrefs
                    ? ProfileData.FromJson(PlayerPrefs.GetString(PrefsKey, ""))
                    : File.Exists(FilePath) ? ProfileData.FromJson(File.ReadAllText(FilePath)) : ProfileData.Default();
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

        /// <summary>The game used to be called "LabyrinthOfTheDead": bring saves over from that folder once.</summary>
        private static void MigrateFromOldName()
        {
            if (File.Exists(FilePath)) return;
            string dir = Path.GetDirectoryName(FilePath);
            string old = Path.Combine(Path.GetDirectoryName(dir) ?? "", "LabyrinthOfTheDead", Path.GetFileName(FilePath));
            if (!File.Exists(old)) return;
            Directory.CreateDirectory(dir);
            File.Copy(old, FilePath);
            Debug.Log($"[ProfileStore] Migrated profile from {old}");
        }

        public static void Save()
        {
            if (_current == null) return;
            try
            {
                if (UsePrefs)
                {
                    PlayerPrefs.SetString(PrefsKey, _current.ToJson());
                    PlayerPrefs.Save();
                }
                else
                {
                    File.WriteAllText(FilePath, _current.ToJson());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileStore] Could not save profile: {ex.Message}");
            }
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Test/automation flags. Desktop: command-line arguments ("-botmove", "-onlinejoin ABC123").
    /// Browser: the page's query string ("?botmove&amp;onlinejoin=ABC123").
    /// </summary>
    public static class CommandLine
    {
        private static System.Collections.Generic.Dictionary<string, string> _query;

        private static System.Collections.Generic.Dictionary<string, string> Query
        {
            get
            {
                if (_query != null) return _query;
                _query = new System.Collections.Generic.Dictionary<string, string>();
                if (Application.platform != RuntimePlatform.WebGLPlayer) return _query;
                string url = Application.absoluteURL ?? "";
                int q = url.IndexOf('?');
                if (q < 0) return _query;
                foreach (var part in url.Substring(q + 1).Split('&', '#'))
                {
                    if (part.Length == 0) continue;
                    int eq = part.IndexOf('=');
                    string key = Uri.UnescapeDataString(eq < 0 ? part : part.Substring(0, eq));
                    _query[key] = eq < 0 ? "" : Uri.UnescapeDataString(part.Substring(eq + 1));
                }
                return _query;
            }
        }

        public static bool Has(string flag) =>
            Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0 || Query.ContainsKey(flag.TrimStart('-'));

        public static string GetValue(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            return Query.TryGetValue(flag.TrimStart('-'), out var v) && v.Length > 0 ? v : null;
        }

        public static float GetFloat(string flag, float fallback) =>
            float.TryParse(GetValue(flag), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }
}
