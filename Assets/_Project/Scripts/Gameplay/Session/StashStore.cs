using System;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Loads/saves the local player's stash on this device. Use -profile &lt;name&gt; to keep separate saves (e.g. two instances on one PC).</summary>
    public static class StashStore
    {
        private static Stash _current;

        public static Stash Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Load();
                _current.Changed += Save;
                return _current;
            }
        }

        private static string FilePath
        {
            get
            {
                string profile = CommandLine.GetValue("-profile");
                string name = string.IsNullOrEmpty(profile) ? "stash.json" : $"stash-{profile}.json";
                return Path.Combine(Application.persistentDataPath, name);
            }
        }

        private static Stash Load()
        {
            try
            {
                return File.Exists(FilePath) ? Stash.FromJson(File.ReadAllText(FilePath)) : new Stash();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StashStore] Could not load stash, starting empty: {ex.Message}");
                return new Stash();
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
                Debug.LogWarning($"[StashStore] Could not save stash: {ex.Message}");
            }
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
