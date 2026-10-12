using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// What this device has recorded in the codex (ProfileData.codexSeen). Monsters are recorded when they first
    /// show in the room you stand in, rooms when you walk in, traps when you meet them. Each new record raises
    /// <see cref="Recorded"/> (the HUD shows a small "図鑑に記録" card).
    /// </summary>
    public static class Codex
    {
        public static event Action<CodexCatalog.Entry> Recorded;

        private static HashSet<string> _seen;
        private static ProfileData _profile;

        private static HashSet<string> Seen
        {
            get
            {
                // Profiles can be switched (-profile, tests): rebuild when it changes.
                if (_seen == null || _profile != ProfileStore.Current)
                {
                    _profile = ProfileStore.Current;
                    _seen = new HashSet<string>(_profile.codexSeen);
                }
                return _seen;
            }
        }

        public static bool Known(CodexCatalog.Entry e) => e.AlwaysKnown || Seen.Contains(e.Id);

        public static (int Known, int Total) Progress()
        {
            int known = 0, total = 0;
            foreach (var e in CodexCatalog.All)
            {
                if (e.AlwaysKnown) continue;
                total++;
                if (Seen.Contains(e.Id)) known++;
            }
            return (known, total);
        }

        public static void Record(string id)
        {
            if (string.IsNullOrEmpty(id) || Seen.Contains(id)) return;
            var entry = CodexCatalog.Get(id);
            if (entry == null || entry.AlwaysKnown) return;
            Seen.Add(id);
            ProfileStore.Current.codexSeen.Add(id);
            ProfileStore.Save();
            Recorded?.Invoke(entry);
        }

        /// <summary>A monster or object drawn at <paramref name="at"/>: recorded once it is in the local player's room.</summary>
        public static void SeeEnemy(EnemyType type, bool elite, Vector2 at)
        {
            string id = CodexCatalog.EnemyEntry(type);
            bool eliteNew = elite && !Seen.Contains("enemy:Elite");
            if (Seen.Contains(id) && !eliteNew) return;
            var director = RunDirector.Instance;
            if (director == null || director.Map.RoomAt(at) != director.LocalRoom) return;
            Record(id);
            if (eliteNew) Record("enemy:Elite");
        }

        public static void EnterRoom(RoomKind kind)
        {
            Record(CodexCatalog.RoomId(kind));
            if (kind == RoomKind.MiniBoss) Record("enemy:Champion");
        }

        /// <summary>The sprite to show for an entry (its sheet's idle frame, or a reward icon), null for a text tile.</summary>
        public static Sprite Picture(CodexCatalog.Entry e)
        {
            if (e.IconSheet != null) return SpriteSheet.Load(e.IconSheet, quiet: true)?.Sprites("default") is { } frames && e.IconFrame < frames.Length ? frames[e.IconFrame] : null;
            if (e.RewardIcon >= 0) return GameIcons.Reward((RoomReward)e.RewardIcon);
            if (string.IsNullOrEmpty(e.Sheet)) return null;
            var sheet = SpriteSheet.Load(e.Sheet, quiet: true);
            if (sheet == null) return null;
            return sheet.TryGetClip("idle", out _) ? sheet.Sprites("idle")[0] : sheet.First;
        }
    }
}
