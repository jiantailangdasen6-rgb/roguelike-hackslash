using System.Collections.Generic;

namespace Game.Core
{
    public enum Difficulty : byte
    {
        Easy,
        Normal,
        Hard,
        Nightmare,
    }

    /// <summary>
    /// What a difficulty changes, named so the team can prepare for it: strengths given to the monsters and
    /// handicaps laid on the players. Shown at the camp's tablet and on the HUD.
    /// </summary>
    [System.Flags]
    public enum DifficultyMod
    {
        None = 0,
        // Monsters
        IronGuard = 1 << 0,  // elites are armoured: freeze, stun or mark them to break the guard
        Ferocity = 1 << 1,   // attacks come quicker (shorter telegraphs)
        Vengeance = 1 << 2,  // monsters burst a moment after dying
        Swiftness = 1 << 3,  // monsters move faster
        LordsWrath = 1 << 4, // bosses' trials are harsher
        // Players
        Thirst = 1 << 8,     // healing weaker
        Frailty = 1 << 9,    // max HP down
        Weariness = 1 << 10, // dodge recovers slower
        // Help on the easiest setting
        Mercy = 1 << 16,     // teammates get up faster
    }

    /// <summary>Chosen by the host before a run. Scales the monsters and the soul-shard reward.</summary>
    public sealed class DifficultyDef
    {
        public Difficulty Id;
        public string Name;
        public string Text;
        public float EnemyHealth;
        public float EnemyDamage;
        public float EnemyCount;
        public float EliteChance;
        public float Shards;
        /// <summary>Better gear: shifts drop rarity upward (0 = normal).</summary>
        public float LootBoost;
        public DifficultyMod Mods;
        /// <summary>Needs a victory on this difficulty first (null = always open).</summary>
        public Difficulty? UnlockedBy;

        public bool Has(DifficultyMod mod) => (Mods & mod) != 0;
    }

    public static class DifficultyCatalog
    {
        public const float FerocityWindup = 0.8f;
        public const float SwiftnessSpeed = 1.2f;
        public const float ThirstFactor = 0.6f;
        public const float FrailtyFactor = 0.25f;
        public const float WearinessFactor = 1.4f;
        public const float MercyRevive = 1.6f;
        public const float VengeanceRadius = 1.4f;
        public const float VengeanceDelay = 1.1f;
        public const float VengeanceDamage = 14f;

        private const DifficultyMod HardMods = DifficultyMod.IronGuard | DifficultyMod.Ferocity | DifficultyMod.Thirst;

        public static readonly DifficultyDef[] All =
        {
            new() { Id = Difficulty.Easy, Name = "ルーキー", Text = "モンスターが弱く数も少ない。まずはラビリンスに慣れたい人に", EnemyHealth = 0.7f, EnemyDamage = 0.6f, EnemyCount = 0.85f, EliteChance = 0.03f, Shards = 0.7f, LootBoost = -0.3f, Mods = DifficultyMod.Mercy },
            new() { Id = Difficulty.Normal, Name = "ノーマル", Text = "本来のラビリンス", EnemyHealth = 1f, EnemyDamage = 1f, EnemyCount = 1f, EliteChance = 0.05f, Shards = 1f },
            new() { Id = Difficulty.Hard, Name = "ベテラン", Text = "ソウルシャード 1.5倍\n良い装備が出やすい", EnemyHealth = 1.15f, EnemyDamage = 1.15f, EnemyCount = 1.1f, EliteChance = 0.08f, Shards = 1.5f, LootBoost = 0.6f, Mods = HardMods },
            new()
            {
                Id = Difficulty.Nightmare, Name = "ナイトメア", Text = "ソウルシャード 2.2倍\nレジェンダリーが出やすい\n（ベテランを踏破すると解放）", EnemyHealth = 1.3f, EnemyDamage = 1.3f, EnemyCount = 1.25f, EliteChance = 0.12f, Shards = 2.2f, LootBoost = 1.2f,
                Mods = HardMods | DifficultyMod.Vengeance | DifficultyMod.Swiftness | DifficultyMod.LordsWrath | DifficultyMod.Frailty | DifficultyMod.Weariness,
                UnlockedBy = Difficulty.Hard,
            },
        };

        public static DifficultyDef Get(Difficulty d) => All[(int)d < All.Length ? (int)d : 1];

        public static readonly DifficultyMod[] ModOrder =
        {
            DifficultyMod.IronGuard, DifficultyMod.Ferocity, DifficultyMod.Vengeance, DifficultyMod.Swiftness, DifficultyMod.LordsWrath,
            DifficultyMod.Thirst, DifficultyMod.Frailty, DifficultyMod.Weariness, DifficultyMod.Mercy,
        };

        /// <summary>Strengths of the monsters (red) as opposed to the players' handicaps (purple) and help (green).</summary>
        public static bool IsMonsterMod(DifficultyMod m) => (int)m < 1 << 8;
        public static bool IsHelp(DifficultyMod m) => (int)m >= 1 << 16;

        public static string ModName(DifficultyMod m) => m switch
        {
            DifficultyMod.IronGuard => "アイアンガード",
            DifficultyMod.Ferocity => "猛り",
            DifficultyMod.Vengeance => "グラッジ",
            DifficultyMod.Swiftness => "スウィフト",
            DifficultyMod.LordsWrath => "ボスレイジ",
            DifficultyMod.Thirst => "サースト",
            DifficultyMod.Frailty => "脆い身体",
            DifficultyMod.Weariness => "ヘヴィフット",
            DifficultyMod.Mercy => "女神の慈悲",
            _ => "",
        };

        public static string ModText(DifficultyMod m) => m switch
        {
            DifficultyMod.IronGuard => "エリートが鎧をまとう。フリーズ・スタン・マークで崩す",
            DifficultyMod.Ferocity => $"モンスターの攻撃の予兆が{(1f - FerocityWindup) * 100f:0}%短い",
            DifficultyMod.Vengeance => "倒したモンスターが少し後に爆ぜる",
            DifficultyMod.Swiftness => $"モンスターの足が{(SwiftnessSpeed - 1f) * 100f:0}%速い",
            DifficultyMod.LordsWrath => "ボスの試練が厳しくなる",
            DifficultyMod.Thirst => $"回復量 −{(1f - ThirstFactor) * 100f:0}%",
            DifficultyMod.Frailty => $"最大HP −{FrailtyFactor * 100f:0}%",
            DifficultyMod.Weariness => $"ドッジの再使用 +{(WearinessFactor - 1f) * 100f:0}%",
            DifficultyMod.Mercy => "倒れた仲間が早く起きる",
            _ => "",
        };

        public static IEnumerable<DifficultyMod> ModsOf(DifficultyDef def)
        {
            foreach (var m in ModOrder)
                if (def.Has(m)) yield return m;
        }
    }
}
