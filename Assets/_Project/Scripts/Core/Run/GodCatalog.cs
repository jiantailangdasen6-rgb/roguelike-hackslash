using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public enum GodId : byte
    {
        Flame,
        Frost,
        Storm,
        Life,
    }

    /// <summary>What a <see cref="SimEventType.GodSign"/> shows.</summary>
    public enum GodSignKind : byte
    {
        Lightning, // a bolt from above onto Pos (Value = radius of a big one, 0 = a single target)
        Taken,     // a blessing granted (Value = PerkId)
        Favor,     // two blessings of one god: its favour awakens
        Veil,      // the holy veil's shield
        Rise,      // got up again by the prayer of rebirth
        Frost,     // a burst of frost (radius in Value)
        Fire,      // a burst of flame (radius in Value)
    }

    /// <summary>
    /// A god of the labyrinth. Their blessings (perks with <see cref="PerkDef.God"/> set) are granted after a champion
    /// or a lord falls; two blessings of the same god awaken its favour (a set bonus).
    /// </summary>
    public sealed class GodDef
    {
        public GodId Id;
        public string Name;
        public string FavorName;
        public string FavorText;
        public Color Color;
    }

    public static class GodCatalog
    {
        /// <summary>Blessings of one god needed for its favour.</summary>
        public const int FavorPieces = 2;

        public static readonly GodDef[] All =
        {
            new() { Id = GodId.Flame, Name = "焔神アグナ", FavorName = "焔の寵愛", FavorText = "燃えている敵へのダメージ +30%", Color = new Color(1f, 0.45f, 0.2f) },
            new() { Id = GodId.Frost, Name = "氷姫ニヴァ", FavorName = "霜の寵愛", FavorText = "凍った敵を倒すと冷気が弾け、周りの敵も凍る", Color = new Color(0.55f, 0.85f, 1f) },
            new() { Id = GodId.Storm, Name = "雷神トラス", FavorName = "雷の寵愛", FavorText = "雷が1体多く落ち、打たれた敵は気絶する", Color = new Color(1f, 0.88f, 0.35f) },
            new() { Id = GodId.Life, Name = "慈母セレナ", FavorName = "命の寵愛", FavorText = "近くの仲間と自分のHPが毎秒 1.5 回復", Color = new Color(0.5f, 1f, 0.62f) },
        };

        public static GodDef Get(GodId id) => All[(int)id];

        public const float FavorFlameBonus = 0.3f;
        public const float FavorLifeRegen = 1.5f;
        public const float FavorLifeRadius = 6f;

        private static List<PerkDef>[] _blessings;

        /// <summary>The blessings a god grants (catalog order).</summary>
        public static IReadOnlyList<PerkDef> Blessings(GodId god)
        {
            if (_blessings == null)
            {
                _blessings = new List<PerkDef>[All.Length];
                for (int i = 0; i < All.Length; i++) _blessings[i] = new List<PerkDef>();
                foreach (var def in PerkCatalog.All)
                    if (def.God.HasValue) _blessings[(int)def.God.Value].Add(def);
            }
            return _blessings[(int)god];
        }

        /// <summary>Blessings of <paramref name="god"/> among perk levels (per PerkId).</summary>
        public static int Pieces(IReadOnlyList<int> perkLevels, GodId god)
        {
            int n = 0;
            foreach (var def in Blessings(god)) if (perkLevels[(int)def.Id] > 0) n++;
            return n;
        }
    }
}
