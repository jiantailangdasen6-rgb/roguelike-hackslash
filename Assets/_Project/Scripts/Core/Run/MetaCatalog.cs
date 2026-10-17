using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    public enum MasteryKind : byte
    {
        UnlockPerk,
        HealthGrowth,  // +Value max HP per level
        MightGrowth,   // +Value damage per level
        StartPerk,     // a free perk choice when the run starts
        Stat,          // permanent stat modifier
        GrantPerk,     // the run starts with this class perk already learnt
        Keystone,      // secret art (奥義): an innate rune in one skill; one per class
    }

    /// <summary>Out-of-run growth for one class, bought with soul shards.</summary>
    public sealed class MasteryNode
    {
        public string Id;
        public ClassId Class;
        public string Name;
        public string Text;
        public int Cost;
        public MasteryKind Kind;
        public float Value;
        public PerkId Perk;
        public StatModifier Modifier;
        /// <summary>Place in the master's tree: branch 0-2, step 1-4 from the top.</summary>
        public int Branch;
        public int Tier;
        /// <summary>Secret art: the skill (1 or 2) that carries the rune.</summary>
        public int Skill;
        public RuneId Rune;
    }

    /// <summary>A fixed-effect charm chosen before a run (two slots).</summary>
    public sealed class TrinketDef
    {
        public string Id;
        public string Name;
        public string Text;
        public int Cost; // 0 = owned from the start
        public StatModifier[] Modifiers = Array.Empty<StatModifier>();
        public bool Phoenix;      // auto-revive once per run
        public float HealOnKill;
    }

    public static class MetaCatalog
    {
        public const int TrinketSlots = 2;

        private static StatModifier M(StatType s, ModifierKind k, float v) => new(s, k, v);

        public const int BranchCount = 3;
        public const int GrantCost = 120;
        public const int KeystoneCost = 200;
        /// <summary>How strongly a secret art's rune works (a rare-and-a-bit rune, ItemCatalog.RunePower).</summary>
        public const float KeystonePower = 1.6f;

        private static readonly string[][] BranchNames =
        {
            new[] { "ブレード", "アイアン", "ブラッド" },          // Warrior
            new[] { "アーケイン", "ガード", "ウィズダム" },      // Mage
            new[] { "アーチャー", "ハンター", "ゲイル" },        // Ranger
            new[] { "ハンマー", "ウォール", "ホーリー" },        // Guardian
            new[] { "ブレード", "ヴァイタル", "シャドウ" },      // Assassin
            new[] { "ブリュー", "フラスク", "シークレット" },    // Alchemist
            new[] { "ネクロ", "ヘル", "フォービドゥン" },        // Deathcaller
            new[] { "ソード", "シジル", "オース" },              // Paladin
        };

        public static string BranchName(ClassId c, int branch) => (int)c < BranchNames.Length ? BranchNames[(int)c][branch] : "";

        private static MasteryNode G(string id, ClassId c, int branch, PerkId perk)
        {
            var def = PerkCatalog.Get(perk);
            return new MasteryNode
            {
                Id = id, Class = c, Branch = branch, Tier = 3, Cost = GrantCost, Kind = MasteryKind.GrantPerk, Perk = perk,
                Name = $"{def.Name}スタイル", Text = $"「{def.Name}」を持って始まる：{def.Describe(1)}",
            };
        }

        private static MasteryNode K(string id, ClassId c, int branch, string name, int skill, RuneId rune, string text) => new()
        {
            Id = id, Class = c, Branch = branch, Tier = 4, Cost = KeystoneCost, Kind = MasteryKind.Keystone, Skill = skill, Rune = rune,
            Name = name, Text = text,
        };

        // The master's tree: three branches per class, four steps each (a step opens once the one above it is learnt).
        // Steps 1-2 are the old mastery nodes (ids kept for saves); step 3 starts every run with a class perk; step 4 is
        // the branch's secret art (奥義): it changes one of the skills (an innate rune, RunSimulation.Runes). Only one
        // secret art per class; everything can be unlearnt for a full refund (ProfileData.Respec).
        public static readonly MasteryNode[] Mastery =
        {
            // Warrior: 剣 / 鉄 / 血
            new() { Id = "w_might", Class = ClassId.Warrior, Branch = 0, Tier = 1, Name = "ソードトレーニング", Text = "レベルごとの攻撃力成長 +1%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.01f },
            new() { Id = "w_crusher", Class = ClassId.Warrior, Branch = 0, Tier = 2, Name = "スキル：クラッシャー", Text = "パーク「クラッシャー」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Crusher },
            G("w_g_whirl", ClassId.Warrior, 0, PerkId.Whirlwind),
            K("w_k_ember", ClassId.Warrior, 0, "フレイムラッシュ", 1, RuneId.Ember, "チャージスラッシュの通り道に燃える床が残る"),
            new() { Id = "w_hp", Class = ClassId.Warrior, Branch = 1, Tier = 1, Name = "トレインドボディ", Text = "レベルごとの最大HP成長 +4", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 4f },
            new() { Id = "w_armor", Class = ClassId.Warrior, Branch = 1, Tier = 2, Name = "イモービル", Text = "防御 +2", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Armor, ModifierKind.Flat, 2f) },
            G("w_g_bulwark", ClassId.Warrior, 1, PerkId.Bulwark),
            K("w_k_storm", ClassId.Warrior, 1, "サンダークライ", 2, RuneId.Storm, "ウォークライから近くの敵へ雷が飛ぶ"),
            new() { Id = "w_blood", Class = ClassId.Warrior, Branch = 2, Tier = 1, Name = "スキル：ブラッドラスト", Text = "パーク「ブラッドラスト」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Bloodlust },
            new() { Id = "w_start", Class = ClassId.Warrior, Branch = 2, Tier = 2, Name = "ベテランズエッジ", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("w_g_rush", ClassId.Warrior, 2, PerkId.Rush),
            K("w_k_shadow", ClassId.Warrior, 2, "シャドウランジ", 1, RuneId.Shadow, "チャージスラッシュのあとドッジがすぐ戻り、少しのあいだ攻撃がすべてクリティカル"),

            // Mage: 秘術 / 守護 / 叡智
            new() { Id = "m_might", Class = ClassId.Mage, Branch = 0, Tier = 1, Name = "アーケイントレーニング", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "m_chain", Class = ClassId.Mage, Branch = 0, Tier = 2, Name = "スキル：チェインボルト", Text = "パーク「チェインボルト」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.ChainBolt },
            G("m_g_multi", ClassId.Mage, 0, PerkId.Multicast),
            K("m_k_storm", ClassId.Mage, 0, "サンダーフロスト", 1, RuneId.Storm, "フロストノヴァから近くの敵へ雷が飛ぶ"),
            new() { Id = "m_hp", Class = ClassId.Mage, Branch = 1, Tier = 1, Name = "マナシールド", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "m_cd", Class = ClassId.Mage, Branch = 1, Tier = 2, Name = "クイックキャスト", Text = "スキルのクールダウン −8%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Cooldown, ModifierKind.PercentAdd, -0.08f) },
            G("m_g_armor", ClassId.Mage, 1, PerkId.FrostArmor),
            K("m_k_echo", ClassId.Mage, 1, "エコープレイヤー", 2, RuneId.Echo, "ヒーリングライトが、ときどきすぐにもう一度使える"),
            new() { Id = "m_ignite", Class = ClassId.Mage, Branch = 2, Tier = 1, Name = "スキル：イグナイト", Text = "パーク「イグナイト」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Ignite },
            new() { Id = "m_start", Class = ClassId.Mage, Branch = 2, Tier = 2, Name = "ローアマスター", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("m_g_frost", ClassId.Mage, 2, PerkId.Permafrost),
            K("m_k_soul", ClassId.Mage, 2, "ソウルスキャッター", 1, RuneId.Soul, "フロストノヴァからソウルが四方へ飛び散り、敵を打つ"),

            // Ranger: 弓 / 狩り / 疾風
            new() { Id = "r_might", Class = ClassId.Ranger, Branch = 0, Tier = 1, Name = "アーチェリートレーニング", Text = "レベルごとの攻撃力成長 +1.2%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.012f },
            new() { Id = "r_venom", Class = ClassId.Ranger, Branch = 0, Tier = 2, Name = "スキル：ヴェノムアロー", Text = "パーク「ヴェノムアロー」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Venom },
            G("r_g_spread", ClassId.Ranger, 0, PerkId.Spread),
            K("r_k_storm", ClassId.Ranger, 0, "サンダーショット", 1, RuneId.Storm, "ピアシングショットの通り道から近くの敵へ雷が飛ぶ"),
            new() { Id = "r_hp", Class = ClassId.Ranger, Branch = 1, Tier = 1, Name = "ワイルドボディ", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "r_speed", Class = ClassId.Ranger, Branch = 1, Tier = 2, Name = "フリートフット", Text = "移動速度 +6%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.06f) },
            G("r_g_mark", ClassId.Ranger, 1, PerkId.DeepMark),
            K("r_k_frost", ClassId.Ranger, 1, "フロストマーク", 2, RuneId.Frost, "ハンターズマークを付けた所の敵が凍る"),
            new() { Id = "r_evasive", Class = ClassId.Ranger, Branch = 2, Tier = 1, Name = "スキル：イベイシブ", Text = "パーク「イベイシブ」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Evasive },
            new() { Id = "r_start", Class = ClassId.Ranger, Branch = 2, Tier = 2, Name = "ハンターズインスティンクト", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("r_g_ricochet", ClassId.Ranger, 2, PerkId.Ricochet),
            K("r_k_shadow", ClassId.Ranger, 2, "シャドウハント", 2, RuneId.Shadow, "ハンターズマークのあとドッジがすぐ戻り、少しのあいだ攻撃がすべてクリティカル"),

            // Guardian: 鎚 / 城壁 / 聖
            new() { Id = "g_might", Class = ClassId.Guardian, Branch = 0, Tier = 1, Name = "ハンマートレーニング", Text = "レベルごとの攻撃力成長 +1%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.01f },
            new() { Id = "g_hammer", Class = ClassId.Guardian, Branch = 0, Tier = 2, Name = "スキル：ホーリーハンマー", Text = "パーク「ホーリーハンマー」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.HolyHammer },
            G("g_g_retri", ClassId.Guardian, 0, PerkId.Retribution),
            K("g_k_storm", ClassId.Guardian, 0, "サンダーシールド", 1, RuneId.Storm, "シールドバッシュの通り道から近くの敵へ雷が飛ぶ"),
            new() { Id = "g_hp", Class = ClassId.Guardian, Branch = 1, Tier = 1, Name = "スチールボーン", Text = "レベルごとの最大HP成長 +5", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 5f },
            new() { Id = "g_armor", Class = ClassId.Guardian, Branch = 1, Tier = 2, Name = "ゲートスタンス", Text = "防御 +3", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Armor, ModifierKind.Flat, 3f) },
            G("g_g_wall", ClassId.Guardian, 1, PerkId.ShieldWall),
            K("g_k_vital", ClassId.Guardian, 1, "ヒーリンググラウンド", 2, RuneId.Vital, "サンクチュアリを張ると、自分と近くの仲間がすぐ回復する"),
            new() { Id = "g_aegis", Class = ClassId.Guardian, Branch = 2, Tier = 1, Name = "スキル：イージス", Text = "パーク「イージス」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Aegis },
            new() { Id = "g_start", Class = ClassId.Guardian, Branch = 2, Tier = 2, Name = "オースソード", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("g_g_stead", ClassId.Guardian, 2, PerkId.Steadfast),
            K("g_k_frost", ClassId.Guardian, 2, "フロストバッシュ", 1, RuneId.Frost, "シールドバッシュの通り道の敵が凍る"),

            // Assassin: 刃 / 急所 / 影
            new() { Id = "a_might", Class = ClassId.Assassin, Branch = 0, Tier = 1, Name = "ブレードトレーニング", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "a_veil", Class = ClassId.Assassin, Branch = 0, Tier = 2, Name = "スキル：スモークヴェール", Text = "パーク「スモークヴェール」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.SmokeVeil },
            G("a_g_finish", ClassId.Assassin, 0, PerkId.Finisher),
            K("a_k_soul", ClassId.Assassin, 0, "ソウルハント", 1, RuneId.Soul, "シャドウステップで降りた所からソウルが四方へ飛び散り、敵を打つ"),
            new() { Id = "a_hp", Class = ClassId.Assassin, Branch = 1, Tier = 1, Name = "サプルボディ", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "a_crit", Class = ClassId.Assassin, Branch = 1, Tier = 2, Name = "ヴァイタルストライク", Text = "クリティカル率 +8%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.CritChance, ModifierKind.Flat, 0.08f) },
            G("a_g_poison", ClassId.Assassin, 1, PerkId.PoisonBlades),
            K("a_k_frost", ClassId.Assassin, 1, "フロストスモーク", 2, RuneId.Frost, "スモークボムで周りの敵が凍る"),
            new() { Id = "a_twin", Class = ClassId.Assassin, Branch = 2, Tier = 1, Name = "スキル：ツインファング", Text = "パーク「ツインファング」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Twinfang },
            new() { Id = "a_start", Class = ClassId.Assassin, Branch = 2, Tier = 2, Name = "ダークマナー", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("a_g_dance", ClassId.Assassin, 2, PerkId.ShadowDance),
            K("a_k_echo", ClassId.Assassin, 2, "シャドウエコー", 1, RuneId.Echo, "シャドウステップが、ときどきすぐにもう一度使える"),

            // Alchemist: 調合 / 大瓶 / 秘伝
            new() { Id = "x_might", Class = ClassId.Alchemist, Branch = 0, Tier = 1, Name = "ブリューイングトレーニング", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "x_catalyst", Class = ClassId.Alchemist, Branch = 0, Tier = 2, Name = "スキル：カタリスト", Text = "パーク「カタリスト」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Catalyst },
            G("x_g_volatile", ClassId.Alchemist, 0, PerkId.VolatileMix),
            K("x_k_storm", ClassId.Alchemist, 0, "サンダーブリュー", 1, RuneId.Storm, "ファイアフラスクの割れた所から近くの敵へ雷が飛ぶ"),
            new() { Id = "x_hp", Class = ClassId.Alchemist, Branch = 1, Tier = 1, Name = "トニック", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "x_area", Class = ClassId.Alchemist, Branch = 1, Tier = 2, Name = "ビッグフラスク", Text = "スキルと攻撃の範囲 +10%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Area, ModifierKind.PercentAdd, 0.1f) },
            G("x_g_remedy", ClassId.Alchemist, 1, PerkId.Remedy),
            K("x_k_frost", ClassId.Alchemist, 1, "フロストブリュー", 2, RuneId.Frost, "ヒールポーションの割れた所の敵が凍る"),
            new() { Id = "x_twin", Class = ClassId.Alchemist, Branch = 2, Tier = 1, Name = "スキル：ツインフラスク", Text = "パーク「ツインフラスク」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.TwinFlask },
            new() { Id = "x_start", Class = ClassId.Alchemist, Branch = 2, Tier = 2, Name = "シークレットレシピ", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("x_g_napalm", ClassId.Alchemist, 2, PerkId.Napalm),
            K("x_k_echo", ClassId.Alchemist, 2, "ダブルトス", 1, RuneId.Echo, "ファイアフラスクを、ときどきすぐにもう一度投げられる"),

            // Deathcaller: 死霊 / 冥府 / 禁術
            new() { Id = "d_might", Class = ClassId.Deathcaller, Branch = 0, Tier = 1, Name = "ネクロマンシートレーニング", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "d_plague", Class = ClassId.Deathcaller, Branch = 0, Tier = 2, Name = "スキル：プレイグカース", Text = "パーク「プレイグカース」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.PlagueCurse },
            G("d_g_knell", ClassId.Deathcaller, 0, PerkId.DeathKnell),
            K("d_k_soul", ClassId.Deathcaller, 0, "ソウルカース", 2, RuneId.Soul, "デスカースの所からソウルが四方へ飛び散り、敵を打つ"),
            new() { Id = "d_hp", Class = ClassId.Deathcaller, Branch = 1, Tier = 1, Name = "コープスパクト", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "d_cd", Class = ClassId.Deathcaller, Branch = 1, Tier = 2, Name = "ヘルゲート", Text = "スキルのクールダウン −8%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Cooldown, ModifierKind.PercentAdd, -0.08f) },
            G("d_g_siphon", ClassId.Deathcaller, 1, PerkId.SoulSiphon),
            K("d_k_vital", ClassId.Deathcaller, 1, "ライフドレインボーン", 1, RuneId.Vital, "ボーンサーヴァントを呼ぶと、自分と近くの仲間が回復する"),
            new() { Id = "d_storm", Class = ClassId.Deathcaller, Branch = 2, Tier = 1, Name = "スキル：ボーンストーム", Text = "パーク「ボーンストーム」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Bonestorm },
            new() { Id = "d_start", Class = ClassId.Deathcaller, Branch = 2, Tier = 2, Name = "フォービドゥンブック", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("d_g_legion", ClassId.Deathcaller, 2, PerkId.BoneLegion),
            K("d_k_ember", ClassId.Deathcaller, 2, "インフェルノカース", 2, RuneId.Ember, "デスカースの所に燃える床が残る"),

            // Paladin: 剣 / 聖印 / 叙任
            new() { Id = "p_might", Class = ClassId.Paladin, Branch = 0, Tier = 1, Name = "ソードオース", Text = "レベルごとの攻撃力成長 +1.2%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.012f },
            new() { Id = "p_martyr", Class = ClassId.Paladin, Branch = 0, Tier = 2, Name = "スキル：マーター", Text = "パーク「マーター」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Martyr },
            G("p_g_zeal", ClassId.Paladin, 0, PerkId.Zeal),
            K("p_k_storm", ClassId.Paladin, 0, "サンダージャッジメント", 1, RuneId.Storm, "ジャッジメントから近くの敵へ雷が飛ぶ"),
            new() { Id = "p_hp", Class = ClassId.Paladin, Branch = 1, Tier = 1, Name = "ホーリーボディ", Text = "レベルごとの最大HP成長 +4", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 4f },
            new() { Id = "p_armor", Class = ClassId.Paladin, Branch = 1, Tier = 2, Name = "シジルアーマー", Text = "防御 +2", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Armor, ModifierKind.Flat, 2f) },
            G("p_g_shield", ClassId.Paladin, 1, PerkId.LightShield),
            K("p_k_vital", ClassId.Paladin, 1, "ヒーリングディヴォーション", 2, RuneId.Vital, "ディヴォーションを使うと、自分と近くの仲間が回復する"),
            new() { Id = "p_radiance", Class = ClassId.Paladin, Branch = 2, Tier = 1, Name = "スキル：レディアンス", Text = "パーク「レディアンス」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Radiance },
            new() { Id = "p_start", Class = ClassId.Paladin, Branch = 2, Tier = 2, Name = "ナイトフッド", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
            G("p_g_steel", ClassId.Paladin, 2, PerkId.BlessedSteel),
            K("p_k_echo", ClassId.Paladin, 2, "リピートジャッジメント", 1, RuneId.Echo, "ジャッジメントが、ときどきすぐにもう一度使える"),
        };

        public static readonly TrinketDef[] Trinkets =
        {
            new() { Id = "charm_hp", Name = "トラベラーズチャーム", Text = "最大HP +15%", Cost = 0, Modifiers = new[] { M(StatType.MaxHealth, ModifierKind.PercentAdd, 0.15f) } },
            new() { Id = "wolf_fang", Name = "ウルフファング", Text = "与えるダメージ +10%", Cost = 0, Modifiers = new[] { M(StatType.Might, ModifierKind.PercentAdd, 0.1f) } },
            new() { Id = "feather", Name = "スウィフトフェザー", Text = "移動速度 +10%", Cost = 40, Modifiers = new[] { M(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.1f) } },
            new() { Id = "iron_oath", Name = "アイアンオース", Text = "防御 +3", Cost = 50, Modifiers = new[] { M(StatType.Armor, ModifierKind.Flat, 3f) } },
            new() { Id = "saint_ash", Name = "セインツアッシュ", Text = "仲間の復活速度 +60%", Cost = 60, Modifiers = new[] { M(StatType.ReviveSpeed, ModifierKind.PercentAdd, 0.6f) } },
            new() { Id = "soul_ring", Name = "ソウルリング", Text = "敵を倒すとHPが2回復", Cost = 70, HealOnKill = 2f },
            new() { Id = "hourglass", Name = "アワーグラス", Text = "スキルのクールダウン −12%", Cost = 80, Modifiers = new[] { M(StatType.Cooldown, ModifierKind.PercentAdd, -0.12f) } },
            new() { Id = "phoenix", Name = "フェニックスフェザー", Text = "ランごとに1回、倒れてもその場でHP50%で復活", Cost = 120, Phoenix = true },
            new() { Id = "soul_lantern", Name = "ソウルランタン", Text = "ソウルを拾う範囲 +60%", Cost = 40, Modifiers = new[] { M(StatType.PickupRange, ModifierKind.PercentAdd, 0.6f) } },
            new() { Id = "bone_dice", Name = "ボーンダイス", Text = "クリティカル率 +10%（クリティカルはダメージ2倍）", Cost = 70, Modifiers = new[] { M(StatType.CritChance, ModifierKind.Flat, 0.1f) } },
            new() { Id = "elder_tome", Name = "エルダートーム", Text = "スキルと攻撃の範囲 +15%", Cost = 90, Modifiers = new[] { M(StatType.Area, ModifierKind.PercentAdd, 0.15f) } },
            new() { Id = "blood_chalice", Name = "ブラッドチャリス", Text = "与えるダメージ +25%、ただし最大HP −15%", Cost = 100, Modifiers = new[] { M(StatType.Might, ModifierKind.PercentAdd, 0.25f), M(StatType.MaxHealth, ModifierKind.PercentAdd, -0.15f) } },
        };

        public static MasteryNode GetNode(string id) => Mastery.FirstOrDefault(n => n.Id == id);
        public static TrinketDef GetTrinket(string id) => Trinkets.FirstOrDefault(t => t.Id == id);
        public static IEnumerable<MasteryNode> NodesFor(ClassId c) => Mastery.Where(n => n.Class == c);

        /// <summary>One branch of a class's tree, top step first.</summary>
        public static List<MasteryNode> Branch(ClassId c, int branch) => Mastery.Where(n => n.Class == c && n.Branch == branch).OrderBy(n => n.Tier).ToList();

        /// <summary>The step a node needs learnt first (null for the top of a branch).</summary>
        public static MasteryNode Requires(MasteryNode node) => node.Tier <= 1 ? null
            : Mastery.FirstOrDefault(n => n.Class == node.Class && n.Branch == node.Branch && n.Tier == node.Tier - 1);

        /// <summary>The secret art held among these ids for a class (null for none).</summary>
        public static MasteryNode KeystoneOf(ClassId c, IEnumerable<string> ids) =>
            ids.Select(GetNode).FirstOrDefault(n => n != null && n.Class == c && n.Kind == MasteryKind.Keystone);

        /// <summary>Soul shards earned for a run.</summary>
        public static int ShardsForRun(int roomsCleared, int bossesKilled, int kills, bool victory, Difficulty difficulty = Difficulty.Normal, int contractRank = 0) =>
            Mathf.RoundToInt((roomsCleared * 6 + bossesKilled * 20 + kills / 8 + (victory ? 40 : 0)) * DifficultyCatalog.Get(difficulty).Shards * (1f + ContractCatalog.ShardsPerRank * contractRank));
    }

    /// <summary>What a player brings into a run. Sent to the host when connecting.</summary>
    [Serializable]
    public sealed class Loadout
    {
        public ClassId classId;
        public int skin;
        public List<string> mastery = new();
        public List<string> trinkets = new();
        /// <summary>Stable per-profile id: lets a player who dropped out get their character back.</summary>
        public string playerKey = "";
        /// <summary>Worn gear when setting out (packed hex, see GearVault).</summary>
        public List<string> gear = new();
        /// <summary>Levels of the camp's facilities (CampUpgrade order), grown at the hearth.</summary>
        public List<int> camp = new();
        /// <summary>The lords' memories this player carries (Lore bits): all six open the way to the heart.</summary>
        public int memories;
        /// <summary>The title worn next to the name (an earned achievement's; empty for none).</summary>
        public string title = "";

        public string ToJson() => JsonUtility.ToJson(this);

        public static Loadout FromJson(string json)
        {
            try
            {
                var l = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Loadout>(json);
                if (l == null) return new Loadout();
                l.mastery ??= new List<string>();
                l.trinkets ??= new List<string>();
                l.camp ??= new List<int>();
                l.title ??= "";
                if (l.title.Length > 16) l.title = l.title.Substring(0, 16);
                for (int i = 0; i < l.camp.Count; i++) l.camp[i] = i < CampUpgrades.All.Length ? Mathf.Clamp(l.camp[i], 0, CampUpgrades.All[i].MaxLevel) : 0;
                // Drop unknown ids and trinkets beyond the slot count.
                l.mastery.RemoveAll(id => MetaCatalog.GetNode(id) == null);
                var arts = new HashSet<ClassId>(); // one secret art per class
                l.mastery.RemoveAll(id => MetaCatalog.GetNode(id).Kind == MasteryKind.Keystone && !arts.Add(MetaCatalog.GetNode(id).Class));
                l.trinkets.RemoveAll(id => MetaCatalog.GetTrinket(id) == null);
                if (l.trinkets.Count > MetaCatalog.TrinketSlots) l.trinkets.RemoveRange(MetaCatalog.TrinketSlots, l.trinkets.Count - MetaCatalog.TrinketSlots);
                if (!Enum.IsDefined(typeof(ClassId), l.classId)) l.classId = ClassId.Warrior;
                if (l.skin < 0 || l.skin >= SkinCatalog.PerClass) l.skin = 0;
                return l;
            }
            catch (Exception)
            {
                return new Loadout();
            }
        }
    }

    /// <summary>This device's persistent progress.</summary>
    [Serializable]
    public sealed class ProfileData
    {
        public int version = 1;
        public int shards;
        public ClassId selectedClass;
        public List<string> unlocked = new();
        public List<string> equippedTrinkets = new();
        /// <summary>Selected skin index per class (indexed by ClassId).</summary>
        public List<int> skins = new();
        public Difficulty difficulty = Difficulty.Normal;
        /// <summary>Highest difficulty won so far (-1 = none).</summary>
        public int bestVictory = -1;
        /// <summary>The contract signed at the tablet (ContractCatalog bits), and the best contract rank won with (-1 = none).</summary>
        public int contract;
        public int bestContract = -1;
        /// <summary>Random id made once per profile (see Loadout.playerKey).</summary>
        public string playerKey = "";
        /// <summary>Requests from the camp's people (Quests.cs).</summary>
        public QuestLog quests = new();
        /// <summary>Gear kept between runs: worn set and stash (see GearVault).</summary>
        public GearVaultData gear = new();
        /// <summary>First-run hints already learnt (GuideHints): they stop showing.</summary>
        public List<string> guideSeen = new();
        /// <summary>Codex entries met so far (CodexCatalog ids).</summary>
        public List<string> codexSeen = new();

        public bool IsUnlocked(string id)
        {
            var trinket = MetaCatalog.GetTrinket(id);
            if (trinket != null && trinket.Cost == 0) return true;
            var skin = SkinCatalog.GetById(id);
            if (skin != null && skin.Cost == 0) return true;
            return unlocked.Contains(id);
        }

        /// <summary>Starter classes always; the others once a request has unlocked them.</summary>
        public bool IsClassOpen(ClassId c) => ClassCatalog.IsStarter(c) || unlocked.Contains(ClassCatalog.UnlockId(c));

        public int SkinFor(ClassId c) => (int)c < skins.Count ? skins[(int)c] : 0;

        public void SetSkin(ClassId c, int index)
        {
            while (skins.Count <= (int)c) skins.Add(0);
            skins[(int)c] = index;
        }

        public bool IsDifficultyOpen(Difficulty d)
        {
            var def = DifficultyCatalog.Get(d);
            return !def.UnlockedBy.HasValue || bestVictory >= (int)def.UnlockedBy.Value;
        }

        public void RecordVictory(Difficulty d) => bestVictory = Math.Max(bestVictory, (int)d);

        /// <summary>The lords' memories won (Lore bits, 1 &lt;&lt; land) and how many times the heart was lit (the true ending).</summary>
        public int memories;
        public int heartLit;

        /// <summary>Lines of the camp's people already told (CampStories).</summary>
        public List<string> storyRead = new();

        /// <summary>Achievements and the title worn next to the name (AchievementCatalog).</summary>
        public AchievementLog achievements = new();
        public string title = "";

        /// <summary>Levels of the camp's facilities (CampUpgrade order).</summary>
        public List<int> camp = new();

        public int CampLevel(CampUpgrade id) => CampUpgrades.Level(camp, id);

        /// <summary>Builds the next level of a facility when the shards are there.</summary>
        public bool TryUpgradeCamp(CampUpgrade id)
        {
            var def = CampUpgrades.Get(id);
            int level = CampLevel(id);
            if (level >= def.MaxLevel || shards < def.Costs[level]) return false;
            shards -= def.Costs[level];
            while (camp.Count <= (int)id) camp.Add(0);
            camp[(int)id] = level + 1;
            return true;
        }

        /// <summary>Set out into this week's labyrinth (the host's choice) instead of a fresh one.</summary>
        public bool weekly;
        /// <summary>This device's best in a week's labyrinth (Weekly): which week, how deep, how fast, cleared or not.</summary>
        public int weeklyWeek = -1;
        public int weeklyBestRoom = -1;
        public float weeklyBestTime;
        public bool weeklyCleared;

        /// <summary>A run of week <paramref name="week"/> ended: true when it is this week's new best.</summary>
        public bool RecordWeekly(int week, bool cleared, int room, float time)
        {
            if (weeklyWeek != week)
            {
                weeklyWeek = week;
                weeklyBestRoom = -1;
                weeklyCleared = false;
            }
            if (!Weekly.Better(cleared, room, time, weeklyCleared, weeklyBestRoom, weeklyBestTime)) return false;
            weeklyBestRoom = room;
            weeklyBestTime = time;
            weeklyCleared = cleared;
            return true;
        }

        public bool TryBuy(string id, int cost)
        {
            if (IsUnlocked(id) || shards < cost) return false;
            shards -= cost;
            unlocked.Add(id);
            return true;
        }

        /// <summary>Soul shards spent in a class's tree (what unlearning gives back).</summary>
        public int MasterySpent(ClassId c) => MetaCatalog.NodesFor(c).Where(n => unlocked.Contains(n.Id)).Sum(n => n.Cost);

        /// <summary>Whether a step of the tree can be learnt now (the step above is learnt, the one secret art is free).</summary>
        public bool CanLearn(MasteryNode node, out string why)
        {
            why = null;
            if (unlocked.Contains(node.Id)) why = "習得済み";
            else if (MetaCatalog.Requires(node) is { } above && !unlocked.Contains(above.Id)) why = $"先に「{above.Name}」を習得する";
            else if (node.Kind == MasteryKind.Keystone && MetaCatalog.KeystoneOf(node.Class, unlocked) is { } held)
                why = $"アーツは1つだけ（いまは「{held.Name}」。振り直すと選び直せる）";
            return why == null;
        }

        public bool TryLearn(MasteryNode node, out string why)
        {
            if (!CanLearn(node, out why)) return false;
            if (shards < node.Cost)
            {
                why = "ソウルシャードが足りない";
                return false;
            }
            shards -= node.Cost;
            unlocked.Add(node.Id);
            return true;
        }

        /// <summary>Unlearns a class's whole tree and gives every shard back.</summary>
        public int Respec(ClassId c)
        {
            int back = MasterySpent(c);
            foreach (var n in MetaCatalog.NodesFor(c)) unlocked.Remove(n.Id);
            shards += back;
            return back;
        }

        public void ToggleTrinket(string id)
        {
            if (!IsUnlocked(id)) return;
            if (equippedTrinkets.Remove(id)) return;
            if (equippedTrinkets.Count >= MetaCatalog.TrinketSlots) equippedTrinkets.RemoveAt(0);
            equippedTrinkets.Add(id);
        }

        public Loadout ToLoadout() => new()
        {
            classId = IsClassOpen(selectedClass) ? selectedClass : ClassId.Warrior,
            skin = IsUnlocked(SkinCatalog.Get(selectedClass, SkinFor(selectedClass)).Id) ? SkinFor(selectedClass) : 0,
            mastery = MetaCatalog.NodesFor(selectedClass).Where(n => IsUnlocked(n.Id)).Select(n => n.Id).ToList(),
            trinkets = equippedTrinkets.Where(IsUnlocked).Take(MetaCatalog.TrinketSlots).ToList(),
            playerKey = playerKey,
            gear = GearVault.LoadoutGear(this.gear = GearVault.Normalize(this.gear)),
            camp = new List<int>(camp ?? new List<int>()),
            memories = memories,
            title = !string.IsNullOrEmpty(title) && achievements != null && achievements.Titles().Contains(title) ? title : "",
        };

        public string ToJson() => JsonUtility.ToJson(this);

        public static ProfileData FromJson(string json)
        {
            try
            {
                var p = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ProfileData>(json);
                if (p == null) return Default();
                p.unlocked ??= new List<string>();
                p.equippedTrinkets ??= new List<string>();
                p.camp ??= new List<int>();
                p.skins ??= new List<int>();
                p.quests ??= new QuestLog();
                p.quests.Repair();
                p.achievements ??= new AchievementLog();
                p.storyRead ??= new List<string>();
                p.achievements.Repair();
                p.title = AchievementCatalog.RenamedTitle(p.title ?? "");
                if (!Enum.IsDefined(typeof(Difficulty), p.difficulty)) p.difficulty = Difficulty.Normal;
                // Classes added later are the rewards of requests some players have already turned in.
                if (p.quests.Step(QuestGiver.Crone) >= 5 && !p.unlocked.Contains(ClassCatalog.UnlockId(ClassId.Deathcaller))) p.unlocked.Add(ClassCatalog.UnlockId(ClassId.Deathcaller));
                if (p.quests.Step(QuestGiver.Master) >= 5 && !p.unlocked.Contains(ClassCatalog.UnlockId(ClassId.Paladin))) p.unlocked.Add(ClassCatalog.UnlockId(ClassId.Paladin));
                return p;
            }
            catch (Exception)
            {
                return Default();
            }
        }

        public static ProfileData Default() => new() { equippedTrinkets = new List<string> { "charm_hp" } };
    }
}
