using System.Collections.Generic;

namespace Game.Core
{
    public enum Rarity : byte
    {
        Common,     // 並
        Magic,      // 上質
        Rare,       // 希少
        Legendary,  // 伝説
    }

    public enum EquipSlot : byte
    {
        Weapon,
        Armor,
        Amulet,
        Ring,
        Rune1, // a rune set into the first skill
        Rune2, // a rune set into the second skill
    }

    /// <summary>Unique effects of legendary gear: each changes one class skill.</summary>
    public enum LegendaryId : byte
    {
        None,
        WhirlwindBlade, // warrior: dash slash ends in a spin
        WarDrum,        // warrior: war cry twice as wide, allies attack faster
        FrostCrown,     // mage: the freeze field follows you
        ThunderStaff,   // mage: bolts chain to another enemy
        ShadowQuiver,   // ranger: piercing shot fires three arrows
        HuntGodMark,    // ranger: hunter's mark lands on two enemies
        Unbreakable,    // guardian: the shield charge ends in a stunning shockwave
        Hallowed,       // guardian: a wider, longer, stronger sanctuary
        BlackBlade,     // assassin: shadow step cuts twice
        Mist,           // assassin: smoke twice as wide, sure crits last longer
        Inferno,        // alchemist: a bigger, longer fire pool
        BlessedRain,    // alchemist: the healing potion also shields
        BoneThrone,     // deathcaller: servants twice as tough and strong
        Underworld,     // deathcaller: a wider curse, and every cursed monster rises as a servant
        Scales,         // paladin: devotion binds two teammates
        Sunburst,       // paladin: judgment falls three times
    }

    /// <summary>What a rune adds to the skill it is set into (RunSimulation.Runes). Order is part of the wire format.</summary>
    public enum RuneId : byte
    {
        None,
        Ember,  // 炎: the skill leaves burning ground where it lands (along a dash or a shot: a trail of fire)
        Frost,  // 氷: the skill freezes what it lands on
        Storm,  // 雷: lightning leaps from where it lands to monsters around
        Vital,  // 命: the skill heals you and those close by
        Echo,   // 響: now and then the skill is ready again at once
        Swift,  // 疾: the skill comes back much sooner
        Shadow, // 影: after the skill, the dodge is ready and every blow is critical for a moment
        Soul,   // 魂: souls burst out where the skill lands and fly at monsters
    }

    public sealed class RuneDef
    {
        public RuneId Id;
        public string Name;   // "炎のルーン"
        public string Mark;   // one kanji for the icon and the HUD
        public string Text;   // what it does
    }

    /// <summary>A random property an item can roll. Values scale up with item level.</summary>
    public sealed class AffixDef
    {
        public readonly string Id;
        public readonly StatType Stat;
        public readonly ModifierKind Kind;
        public readonly float Min;
        public readonly float Max;
        public readonly int Weight;

        public AffixDef(string id, StatType stat, ModifierKind kind, float min, float max, int weight = 10)
        {
            Id = id;
            Stat = stat;
            Kind = kind;
            Min = min;
            Max = max;
            Weight = weight;
        }
    }

    /// <summary>An item type (e.g. "鎖帷子"), with a fixed implicit property. Weapons are named after the owner's class.</summary>
    public sealed class ItemBaseDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly EquipSlot Slot;
        public readonly StatModifier Implicit;
        /// <summary>Weapon names per class (warrior, mage, ranger); null for other slots.</summary>
        public readonly string[] ClassNames;

        public ItemBaseDef(string id, string name, EquipSlot slot, StatModifier @implicit, string[] classNames = null)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Implicit = @implicit;
            ClassNames = classNames;
        }
    }

    public sealed class LegendaryDef
    {
        public LegendaryId Id;
        public ClassId Class;
        public string Name;
        public string Text;
    }

    public static class ItemCatalog
    {
        /// <summary>Item values grow by this fraction per item level (one level per room).</summary>
        public const float ScalingPerItemLevel = 0.05f;

        public static float LevelScale(int itemLevel) => 1f + (itemLevel - 1) * ScalingPerItemLevel;

        /// <summary>Random properties. 11 plain stats, then 9 that feed the class synergies. Order is part of the wire format.</summary>
        public static readonly AffixDef[] Affixes =
        {
            new("might", StatType.Might, ModifierKind.PercentAdd, 0.05f, 0.10f),
            new("maxhp", StatType.MaxHealth, ModifierKind.Flat, 10f, 22f),
            new("speed", StatType.MoveSpeed, ModifierKind.PercentAdd, 0.04f, 0.08f),
            new("haste", StatType.AttackSpeed, ModifierKind.PercentAdd, 0.05f, 0.10f),
            new("crit", StatType.CritChance, ModifierKind.Flat, 0.03f, 0.06f),
            new("cooldown", StatType.Cooldown, ModifierKind.PercentAdd, -0.08f, -0.04f),
            new("area", StatType.Area, ModifierKind.PercentAdd, 0.06f, 0.12f),
            new("armor", StatType.Armor, ModifierKind.Flat, 1f, 2.5f, 8),
            new("regen", StatType.Regen, ModifierKind.Flat, 0.3f, 0.8f, 8),
            new("pickup", StatType.PickupRange, ModifierKind.PercentAdd, 0.15f, 0.30f, 6),
            new("revive", StatType.ReviveSpeed, ModifierKind.PercentAdd, 0.20f, 0.40f, 6),
            new("frozendmg", StatType.FrozenDamage, ModifierKind.Flat, 0.15f, 0.30f),
            new("markeddmg", StatType.MarkedDamage, ModifierKind.Flat, 0.15f, 0.30f),
            new("freezetime", StatType.FreezeDuration, ModifierKind.Flat, 0.15f, 0.35f, 8),
            new("marktime", StatType.MarkDuration, ModifierKind.Flat, 0.20f, 0.40f, 8),
            new("killheal", StatType.KillHeal, ModifierKind.Flat, 1f, 3f),
            new("killshield", StatType.KillShield, ModifierKind.Flat, 2f, 5f),
            new("allydmg", StatType.AllyDamage, ModifierKind.Flat, 0.06f, 0.12f),
            new("laststand", StatType.LastStand, ModifierKind.Flat, 0.15f, 0.30f, 8),
            new("dodgestrike", StatType.DodgeStrike, ModifierKind.Flat, 0.30f, 0.60f, 8),
        };

        // By ClassId: warrior, mage, ranger, guardian, assassin, alchemist, deathcaller, paladin.
        private static readonly string[] WornWeapon = { "オールドロングソード", "オールドスタッフ", "オールドボウ", "オールドウォーハンマー", "オールドツインブレード", "オールドフラスク", "オールドボーンワンド", "オールドショートソード" };
        private static readonly string[] AltWeapon = { "ウォーアクス", "エンシェントグリモア", "ハンタークロスボウ", "ガーディアンランス", "スローイングダガー", "ボムフラスク", "リーパーサイズ", "ホーリーグレートソード" };
        private static readonly string[] KeenWeapon ={ "シャープバトルアクス", "クリスタルスタッフ", "ハンターロングボウ", "ホーリーウォーハンマー", "シャドウツインブレード", "セージフラスク", "ソウルファイアスタッフ", "ホーリーソード" };

        /// <summary>Two bases per slot. Order is part of the wire format.</summary>
        public static readonly ItemBaseDef[] Bases =
        {
            new("weapon_worn", "オールドウェポン", EquipSlot.Weapon, new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.06f), WornWeapon),
            new("weapon_keen", "シャープウェポン", EquipSlot.Weapon, new StatModifier(StatType.CritChance, ModifierKind.Flat, 0.04f), KeenWeapon),
            new("leather_armor", "レザーアーマー", EquipSlot.Armor, new StatModifier(StatType.MaxHealth, ModifierKind.Flat, 15f)),
            new("chainmail", "チェインメイル", EquipSlot.Armor, new StatModifier(StatType.Armor, ModifierKind.Flat, 1.5f)),
            new("bone_necklace", "ボーンネックレス", EquipSlot.Amulet, new StatModifier(StatType.Regen, ModifierKind.Flat, 0.4f)),
            new("moon_charm", "ムーンアミュレット", EquipSlot.Amulet, new StatModifier(StatType.Cooldown, ModifierKind.PercentAdd, -0.05f)),
            new("copper_ring", "カッパーリング", EquipSlot.Ring, new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.04f)),
            new("silver_ring", "シルバーリング", EquipSlot.Ring, new StatModifier(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.04f)),
            // Phase 4: each class's other weapon (WeaponCatalog: its basic attack changes) and runes for the two skills.
            new("weapon_alt", "エキゾチックウェポン", EquipSlot.Weapon, new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.05f), AltWeapon),
            new("rune_1", "スキル1ルーン", EquipSlot.Rune1, new StatModifier(StatType.Cooldown, ModifierKind.PercentAdd, -0.02f)),
            new("rune_2", "スキル2ルーン", EquipSlot.Rune2, new StatModifier(StatType.Cooldown, ModifierKind.PercentAdd, -0.02f)),
        };

        public static readonly RuneDef[] Runes =
        {
            new() { Id = RuneId.Ember, Name = "フレイムルーン", Mark = "炎", Text = "スキルの当たる場所に燃える床を残す（突進や矢なら通り道に火の道）" },
            new() { Id = RuneId.Frost, Name = "フロストルーン", Mark = "氷", Text = "スキルの当たる場所の敵を凍らせる（凍った敵は砕ける）" },
            new() { Id = RuneId.Storm, Name = "サンダールーン", Mark = "雷", Text = "スキルの当たる場所から近くの敵へ雷が飛ぶ" },
            new() { Id = RuneId.Vital, Name = "ライフルーン", Mark = "命", Text = "スキルを使うと自分と近くの仲間が回復する" },
            new() { Id = RuneId.Echo, Name = "エコールーン", Mark = "響", Text = "スキルを使うと、ときどきすぐにもう一度使える" },
            new() { Id = RuneId.Swift, Name = "スウィフトルーン", Mark = "疾", Text = "スキルの再使用時間が大きく縮む" },
            new() { Id = RuneId.Shadow, Name = "シャドウルーン", Mark = "影", Text = "スキルを使うとドッジがすぐ使え、少しのあいだ攻撃がすべてクリティカルになる" },
            new() { Id = RuneId.Soul, Name = "ソウルルーン", Mark = "魂", Text = "スキルの当たる場所からソウルが四方へ飛び散り、敵を打つ" },
        };

        public static RuneDef GetRune(RuneId id)
        {
            foreach (var def in Runes) if (def.Id == id) return def;
            return null;
        }

        /// <summary>How strong a rune works by its rarity (並 1, 上質 1.3, 希少 1.6, 伝説 1.9).</summary>
        public static float RunePower(Rarity rarity) => 1f + 0.3f * (int)rarity;

        public static readonly LegendaryDef[] Legendaries =
        {
            new() { Id = LegendaryId.WhirlwindBlade, Class = ClassId.Warrior, Name = "サイクロン・", Text = "チャージスラッシュの終わりに回転斬りを放つ" },
            new() { Id = LegendaryId.WarDrum, Class = ClassId.Warrior, Name = "ウォードラム・", Text = "ウォークライの範囲が2倍、聞いた仲間の攻撃速度+20%" },
            new() { Id = LegendaryId.FrostCrown, Class = ClassId.Mage, Name = "フロストクラウン・", Text = "フロストノヴァが自分についてくる" },
            new() { Id = LegendaryId.ThunderStaff, Class = ClassId.Mage, Name = "サンダークラップ・", Text = "マジックボルトが当たると近くの敵へ連鎖する" },
            new() { Id = LegendaryId.ShadowQuiver, Class = ClassId.Ranger, Name = "シャドウ・", Text = "ピアシングショットが扇状に3本になる" },
            new() { Id = LegendaryId.HuntGodMark, Class = ClassId.Ranger, Name = "ハントゴッド・", Text = "ハンターズマークが近くのもう1体にも付く" },
            new() { Id = LegendaryId.Unbreakable, Class = ClassId.Guardian, Name = "アンブレイカブル・", Text = "シールドバッシュの終わりにショックウェーブを放ち、周りの敵もスタンさせる" },
            new() { Id = LegendaryId.Hallowed, Class = ClassId.Guardian, Name = "ハロウド・", Text = "サンクチュアリが1.4倍の広さになり、3秒長く続き、回復も1.5倍" },
            new() { Id = LegendaryId.BlackBlade, Class = ClassId.Assassin, Name = "ブラックブレード・", Text = "シャドウステップの斬撃が2回になる" },
            new() { Id = LegendaryId.Mist, Class = ClassId.Assassin, Name = "ミスト・", Text = "スモークボムの範囲が2倍、必中クリティカルの時間+2秒" },
            new() { Id = LegendaryId.Inferno, Class = ClassId.Alchemist, Name = "インフェルノ・", Text = "ファイアフラスクの燃える床が1.6倍の広さになり、2秒長く燃える" },
            new() { Id = LegendaryId.BlessedRain, Class = ClassId.Alchemist, Name = "ブレストレイン・", Text = "ヒールポーションを浴びた仲間に25のシールドも与える" },
            new() { Id = LegendaryId.BoneThrone, Class = ClassId.Deathcaller, Name = "ボーンスローン・", Text = "ボーンサーヴァントの体力と攻撃が2倍になる" },
            new() { Id = LegendaryId.Underworld, Class = ClassId.Deathcaller, Name = "アンダーワールド・", Text = "デスカースの範囲が1.6倍、呪われた敵は倒れると必ずボーンサーヴァントになる" },
            new() { Id = LegendaryId.Scales, Class = ClassId.Paladin, Name = "スケイルズ・", Text = "ディヴォーションで2人の仲間とつながる" },
            new() { Id = LegendaryId.Sunburst, Class = ClassId.Paladin, Name = "サンバースト・", Text = "ジャッジメントが3回続けて落ちる" },
        };

        public static LegendaryDef GetLegendary(LegendaryId id)
        {
            foreach (var def in Legendaries) if (def.Id == id) return def;
            return null;
        }

        private static Dictionary<string, AffixDef> _affixById;
        private static Dictionary<string, ItemBaseDef> _baseById;

        public static AffixDef GetAffix(string id)
        {
            _affixById ??= BuildIndex(Affixes, a => a.Id);
            return id != null && _affixById.TryGetValue(id, out var def) ? def : null;
        }

        public static ItemBaseDef GetBase(string id)
        {
            _baseById ??= BuildIndex(Bases, b => b.Id);
            return id != null && _baseById.TryGetValue(id, out var def) ? def : null;
        }

        public static int AffixIndex(string id) => System.Array.FindIndex(Affixes, a => a.Id == id);
        public static int BaseIndex(string id) => System.Array.FindIndex(Bases, b => b.Id == id);

        private static Dictionary<string, T> BuildIndex<T>(T[] defs, System.Func<T, string> key)
        {
            var map = new Dictionary<string, T>(defs.Length);
            foreach (var def in defs) map.Add(key(def), def);
            return map;
        }
    }
}
