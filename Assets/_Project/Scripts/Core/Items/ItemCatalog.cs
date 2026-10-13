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
        private static readonly string[] WornWeapon = { "古びた長剣", "古びた杖", "古びた弓", "古びた戦鎚", "古びた双剣", "使い古しの薬瓶", "古びた骨の杖", "古びた片手剣" };
        private static readonly string[] KeenWeapon = { "鋭い戦斧", "水晶の杖", "狩人の長弓", "聖なる戦鎚", "影の双剣", "賢者の薬瓶", "魂火の杖", "聖剣" };

        /// <summary>Two bases per slot. Order is part of the wire format.</summary>
        public static readonly ItemBaseDef[] Bases =
        {
            new("weapon_worn", "古びた武器", EquipSlot.Weapon, new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.06f), WornWeapon),
            new("weapon_keen", "鋭い武器", EquipSlot.Weapon, new StatModifier(StatType.CritChance, ModifierKind.Flat, 0.04f), KeenWeapon),
            new("leather_armor", "革の鎧", EquipSlot.Armor, new StatModifier(StatType.MaxHealth, ModifierKind.Flat, 15f)),
            new("chainmail", "鎖帷子", EquipSlot.Armor, new StatModifier(StatType.Armor, ModifierKind.Flat, 1.5f)),
            new("bone_necklace", "骨の首飾り", EquipSlot.Amulet, new StatModifier(StatType.Regen, ModifierKind.Flat, 0.4f)),
            new("moon_charm", "月の護符", EquipSlot.Amulet, new StatModifier(StatType.Cooldown, ModifierKind.PercentAdd, -0.05f)),
            new("copper_ring", "銅の指輪", EquipSlot.Ring, new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.04f)),
            new("silver_ring", "銀の指輪", EquipSlot.Ring, new StatModifier(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.04f)),
        };

        public static readonly LegendaryDef[] Legendaries =
        {
            new() { Id = LegendaryId.WhirlwindBlade, Class = ClassId.Warrior, Name = "旋風の", Text = "突進斬りの終わりに回転斬りを放つ" },
            new() { Id = LegendaryId.WarDrum, Class = ClassId.Warrior, Name = "鬨の", Text = "鬨の声の範囲が2倍、聞いた仲間の攻撃速度+20%" },
            new() { Id = LegendaryId.FrostCrown, Class = ClassId.Mage, Name = "霜の王の", Text = "氷結陣が自分についてくる" },
            new() { Id = LegendaryId.ThunderStaff, Class = ClassId.Mage, Name = "雷鳴の", Text = "魔弾が当たると近くの敵へ連鎖する" },
            new() { Id = LegendaryId.ShadowQuiver, Class = ClassId.Ranger, Name = "影の", Text = "貫通の一矢が扇状に3本になる" },
            new() { Id = LegendaryId.HuntGodMark, Class = ClassId.Ranger, Name = "狩神の", Text = "狩人の印が近くのもう1体にも付く" },
            new() { Id = LegendaryId.Unbreakable, Class = ClassId.Guardian, Name = "不落の", Text = "盾の突進の終わりに衝撃波を放ち、周りの敵も気絶させる" },
            new() { Id = LegendaryId.Hallowed, Class = ClassId.Guardian, Name = "聖域の", Text = "守りの陣が1.4倍の広さになり、3秒長く続き、回復も1.5倍" },
            new() { Id = LegendaryId.BlackBlade, Class = ClassId.Assassin, Name = "黒刃の", Text = "影渡りの斬撃が2回になる" },
            new() { Id = LegendaryId.Mist, Class = ClassId.Assassin, Name = "霧の", Text = "煙玉の範囲が2倍、必中会心の時間+2秒" },
            new() { Id = LegendaryId.Inferno, Class = ClassId.Alchemist, Name = "業火の", Text = "火炎瓶の燃える床が1.6倍の広さになり、2秒長く燃える" },
            new() { Id = LegendaryId.BlessedRain, Class = ClassId.Alchemist, Name = "慈雨の", Text = "回復薬を浴びた仲間に25のシールドも与える" },
            new() { Id = LegendaryId.BoneThrone, Class = ClassId.Deathcaller, Name = "屍王の", Text = "骨の兵の体力と攻撃が2倍になる" },
            new() { Id = LegendaryId.Underworld, Class = ClassId.Deathcaller, Name = "冥界の", Text = "死の呪いの範囲が1.6倍、呪われた敵は倒れると必ず骨の兵になる" },
            new() { Id = LegendaryId.Scales, Class = ClassId.Paladin, Name = "天秤の", Text = "献身で2人の仲間とつながる" },
            new() { Id = LegendaryId.Sunburst, Class = ClassId.Paladin, Name = "日輪の", Text = "裁きの光が3回続けて落ちる" },
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
