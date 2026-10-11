using System.Collections.Generic;

namespace Game.Core
{
    public enum PerkId : byte
    {
        // Common
        Vitality,
        Swiftness,
        Fury,
        Focus,
        Lifesteal,
        GuardianBond,
        Resonance,
        Rapid,
        SecondWind,

        // Warrior
        Whirlwind,
        SwordWave,
        Bulwark,
        Crusher,
        Rush,
        Bloodlust,

        // Mage
        SplitBolt,
        Multicast,
        Permafrost,
        ChainBolt,
        Mercy,
        Ignite,

        // Ranger
        Spread,
        Piercing,
        HunterEye,
        Venom,
        DeepMark,
        Evasive,

        // Added later (kept at the end: icons and saved offers index by id)
        Executioner,
        SoulHarvest,
        Comradery,
        Blessing,
        Earthshaker,
        FrostArmor,
        Ricochet,

        // Guardian
        ShieldWall,
        Retribution,
        Steadfast,
        HolyHammer,
        Aegis,

        // Assassin
        PoisonBlades,
        ShadowDance,
        Finisher,
        SmokeVeil,
        Twinfang,

        // Alchemist
        VolatileMix,
        Napalm,
        Remedy,
        Catalyst,
        TwinFlask,
    }

    /// <summary>
    /// A perk offered on level-up (Hades-boon style). Stat perks apply <see cref="PerLevel"/> automatically;
    /// behaviour perks are checked by the combat code through <see cref="SimPlayer.PerkLevel"/>.
    /// Perks with <see cref="Locked"/> only appear after being unlocked in the class mastery (out-of-run growth).
    /// </summary>
    public sealed class PerkDef
    {
        public PerkId Id;
        public string Name;
        public ClassId? Class;
        public int MaxLevel = 1;
        public bool Locked;
        public StatModifier? PerLevel;
        public string Text; // {0} is replaced with the level-scaled number when present
        public float Number;  // value per level used in the text

        public string Describe(int level) => string.Format(Text, Number * level);
    }

    public static class PerkCatalog
    {
        private static StatModifier Mod(StatType s, ModifierKind k, float v) => new(s, k, v);

        public static readonly PerkDef[] All =
        {
            // ---------------- Common
            new() { Id = PerkId.Vitality, Name = "鋼の肉体", MaxLevel = 3, PerLevel = Mod(StatType.MaxHealth, ModifierKind.PercentAdd, 0.2f), Text = "最大HP +{0:0}%", Number = 20 },
            new() { Id = PerkId.Swiftness, Name = "疾風", MaxLevel = 3, PerLevel = Mod(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.08f), Text = "移動速度 +{0:0}%", Number = 8 },
            new() { Id = PerkId.Fury, Name = "殺意", MaxLevel = 3, PerLevel = Mod(StatType.Might, ModifierKind.PercentAdd, 0.12f), Text = "与えるダメージ +{0:0}%", Number = 12 },
            new() { Id = PerkId.Focus, Name = "集中", MaxLevel = 3, PerLevel = Mod(StatType.Cooldown, ModifierKind.PercentAdd, -0.1f), Text = "スキルのクールダウン −{0:0}%", Number = 10 },
            new() { Id = PerkId.Rapid, Name = "連撃", MaxLevel = 3, PerLevel = Mod(StatType.AttackSpeed, ModifierKind.PercentAdd, 0.12f), Text = "通常攻撃の速度 +{0:0}%", Number = 12 },
            new() { Id = PerkId.Lifesteal, Name = "血の契約", MaxLevel = 3, Text = "敵を倒すとHPが {0:0} 回復", Number = 3 },
            new() { Id = PerkId.GuardianBond, Name = "守護の絆", MaxLevel = 2, PerLevel = Mod(StatType.ReviveSpeed, ModifierKind.PercentAdd, 0.6f), Text = "仲間の復活速度 +{0:0}%。復活させた仲間に短い無敵", Number = 60 },
            new() { Id = PerkId.Resonance, Name = "共鳴", MaxLevel = 2, Text = "凍結・印・気絶中の敵へのダメージ +{0:0}%（仲間との連携）", Number = 25 },
            new() { Id = PerkId.SecondWind, Name = "不屈", MaxLevel = 1, Text = "部屋ごとに1回、致命傷をHP1で耐える", Number = 0 },
            new() { Id = PerkId.Executioner, Name = "処刑人", MaxLevel = 2, Text = "HPが35%以下の敵へのダメージ +{0:0}%", Number = 30 },
            new() { Id = PerkId.SoulHarvest, Name = "魂喰らい", MaxLevel = 2, PerLevel = Mod(StatType.PickupRange, ModifierKind.PercentAdd, 0.4f), Text = "魂を拾う範囲 +{0:0}%。魂を拾うとHPが少し回復", Number = 40 },
            new() { Id = PerkId.Comradery, Name = "戦友", MaxLevel = 2, Text = "近く（6m）にいる仲間1人につき与えるダメージ +{0:0}%", Number = 10 },
            new() { Id = PerkId.Blessing, Name = "加護", MaxLevel = 2, Text = "部屋に入るたび {0:0} のシールドを得る", Number = 20 },

            // ---------------- Warrior
            new() { Id = PerkId.Whirlwind, Name = "旋風", Class = ClassId.Warrior, MaxLevel = 1, Text = "3撃目が周囲360°を斬る回転斬りになる", Number = 0 },
            new() { Id = PerkId.SwordWave, Name = "剣圧", Class = ClassId.Warrior, MaxLevel = 2, Text = "3撃目で前方に斬撃波を飛ばす（威力 {0:0}%）", Number = 60 },
            new() { Id = PerkId.Bulwark, Name = "鉄壁の号令", Class = ClassId.Warrior, MaxLevel = 2, Text = "鬨の声で仲間に {0:0} のシールドを与える", Number = 25 },
            new() { Id = PerkId.Rush, Name = "猛進", Class = ClassId.Warrior, MaxLevel = 2, Text = "突進斬りのクールダウン −{0:0}%、威力 +{0:0}%", Number = 20 },
            new() { Id = PerkId.Crusher, Name = "粉砕", Class = ClassId.Warrior, MaxLevel = 2, Locked = true, Text = "凍った敵を砕いたときのダメージ +{0:0}%、破片が広がる", Number = 50 },
            new() { Id = PerkId.Bloodlust, Name = "血の渇き", Class = ClassId.Warrior, MaxLevel = 1, Locked = true, Text = "攻撃を当てるたび攻撃力 +3%（最大30%、3秒で切れる）", Number = 0 },
            new() { Id = PerkId.Earthshaker, Name = "地割れ", Class = ClassId.Warrior, MaxLevel = 2, Text = "突進斬りの終わりに地面を叩き、周りの敵を気絶させる（威力 {0:0}）", Number = 12 },

            // ---------------- Mage
            new() { Id = PerkId.Multicast, Name = "多重詠唱", Class = ClassId.Mage, MaxLevel = 2, Text = "魔弾が +{0:0} 発", Number = 1 },
            new() { Id = PerkId.SplitBolt, Name = "分裂弾", Class = ClassId.Mage, MaxLevel = 1, Text = "魔弾が命中すると2つに分裂する", Number = 0 },
            new() { Id = PerkId.Permafrost, Name = "永久凍土", Class = ClassId.Mage, MaxLevel = 2, Text = "凍結時間 +{0:0}秒、凍結陣の範囲 +20%", Number = 1 },
            new() { Id = PerkId.Mercy, Name = "慈愛", Class = ClassId.Mage, MaxLevel = 2, Text = "癒しの光の回復量 +{0:0}%、回復した仲間の攻撃力も上がる", Number = 40 },
            new() { Id = PerkId.ChainBolt, Name = "連鎖する魔弾", Class = ClassId.Mage, MaxLevel = 2, Locked = true, Text = "魔弾が近くの敵 {0:0} 体に連鎖する", Number = 1 },
            new() { Id = PerkId.Ignite, Name = "燃焼", Class = ClassId.Mage, MaxLevel = 2, Locked = true, Text = "魔弾が敵を燃やす（毎秒 {0:0} ダメージ×3秒）", Number = 4 },
            new() { Id = PerkId.FrostArmor, Name = "氷の鎧", Class = ClassId.Mage, MaxLevel = 2, Text = "凍結陣の中と周りにいる仲間に {0:0} のシールドを与える", Number = 15 },

            // ---------------- Ranger
            new() { Id = PerkId.Spread, Name = "散射", Class = ClassId.Ranger, MaxLevel = 2, Text = "通常攻撃の矢が +{0:0} 本（扇状）", Number = 2 },
            new() { Id = PerkId.Piercing, Name = "貫通", Class = ClassId.Ranger, MaxLevel = 2, Text = "矢が敵を {0:0} 体貫通する", Number = 1 },
            new() { Id = PerkId.HunterEye, Name = "狩人の目", Class = ClassId.Ranger, MaxLevel = 2, PerLevel = Mod(StatType.CritChance, ModifierKind.Flat, 0.15f), Text = "会心率 +{0:0}%（会心はダメージ2倍）", Number = 15 },
            new() { Id = PerkId.DeepMark, Name = "深い印", Class = ClassId.Ranger, MaxLevel = 2, Text = "印の効果 +{0:0}%、印の範囲 +25%", Number = 15 },
            new() { Id = PerkId.Venom, Name = "毒矢", Class = ClassId.Ranger, MaxLevel = 2, Locked = true, Text = "矢が毒を与える（毎秒 {0:0} ダメージ×4秒）", Number = 3 },
            new() { Id = PerkId.Evasive, Name = "身軽", Class = ClassId.Ranger, MaxLevel = 1, Locked = true, Text = "回避のクールダウン −40%、回避後0.5秒は攻撃速度2倍", Number = 0 },
            new() { Id = PerkId.Ricochet, Name = "跳弾", Class = ClassId.Ranger, MaxLevel = 1, Text = "通常攻撃の矢が命中すると、近くの別の敵へ跳ね返る（威力70%）", Number = 0 },

            // ---------------- Guardian
            new() { Id = PerkId.ShieldWall, Name = "城壁", Class = ClassId.Guardian, MaxLevel = 2, Text = "守りの陣で減るダメージ +{0:0}%、陣の範囲 +20%", Number = 15 },
            new() { Id = PerkId.Retribution, Name = "報復", Class = ClassId.Guardian, MaxLevel = 2, Text = "盾の突進の威力 +{0:0}%、気絶 +0.5秒", Number = 40 },
            new() { Id = PerkId.Steadfast, Name = "不退転", Class = ClassId.Guardian, MaxLevel = 2, PerLevel = Mod(StatType.DamageTaken, ModifierKind.PercentAdd, -0.08f), Text = "受けるダメージ −{0:0}%", Number = 8 },
            new() { Id = PerkId.HolyHammer, Name = "聖鎚", Class = ClassId.Guardian, MaxLevel = 2, Locked = true, Text = "鎚打ち3回ごとに聖なる衝撃が周りを打つ（威力 {0:0}・強打）", Number = 14 },
            new() { Id = PerkId.Aegis, Name = "聖盾", Class = ClassId.Guardian, MaxLevel = 2, Locked = true, Text = "守りの陣を張ると、中の仲間に {0:0} のシールド", Number = 20 },

            // ---------------- Assassin
            new() { Id = PerkId.PoisonBlades, Name = "毒刃", Class = ClassId.Assassin, MaxLevel = 2, Text = "双剣が毒を与える（毎秒 {0:0} ダメージ×3秒）", Number = 3 },
            new() { Id = PerkId.ShadowDance, Name = "影舞", Class = ClassId.Assassin, MaxLevel = 2, Text = "影渡りのクールダウン −{0:0}%、斬撃の威力 +30%", Number = 20 },
            new() { Id = PerkId.Finisher, Name = "とどめ", Class = ClassId.Assassin, MaxLevel = 2, Text = "会心のダメージ倍率 +{0:0}%（2倍 → さらに上へ）", Number = 50 },
            new() { Id = PerkId.SmokeVeil, Name = "煙幕", Class = ClassId.Assassin, MaxLevel = 1, Locked = true, Text = "煙玉の範囲 +40%、中にいる仲間は1.5秒無敵", Number = 0 },
            new() { Id = PerkId.Twinfang, Name = "双牙", Class = ClassId.Assassin, MaxLevel = 1, Locked = true, Text = "双剣の3撃ごとに、もう一度斬る（威力80%）", Number = 0 },

            // ---------------- Alchemist
            new() { Id = PerkId.VolatileMix, Name = "揮発", Class = ClassId.Alchemist, MaxLevel = 2, Text = "薬瓶が弾ける範囲 +{0:0}%", Number = 25 },
            new() { Id = PerkId.Napalm, Name = "延焼", Class = ClassId.Alchemist, MaxLevel = 2, Text = "燃える床が +{0:0.#}秒長く、15%広くなる", Number = 1.5f },
            new() { Id = PerkId.Remedy, Name = "良薬", Class = ClassId.Alchemist, MaxLevel = 2, Text = "回復薬の回復量 +{0:0}%、浴びた仲間に10のシールド", Number = 40 },
            new() { Id = PerkId.Catalyst, Name = "触媒", Class = ClassId.Alchemist, MaxLevel = 2, Locked = true, Text = "燃えている・凍った敵へのダメージ +{0:0}%", Number = 20 },
            new() { Id = PerkId.TwinFlask, Name = "二本投げ", Class = ClassId.Alchemist, MaxLevel = 1, Locked = true, Text = "薬瓶を2本ずつ投げる", Number = 0 },
        };

        private static Dictionary<PerkId, PerkDef> _byId;

        public static PerkDef Get(PerkId id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<PerkId, PerkDef>();
                foreach (var p in All) _byId[p.Id] = p;
            }
            return _byId[id];
        }

        public static readonly int Count = System.Enum.GetValues(typeof(PerkId)).Length;
    }
}
