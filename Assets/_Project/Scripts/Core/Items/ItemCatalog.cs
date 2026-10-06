using System.Collections.Generic;

namespace Game.Core
{
    public enum Rarity : byte
    {
        Common,
        Magic,
        Rare,
        Legendary,
    }

    public enum EquipSlot : byte
    {
        Helm,
        Armor,
        Ring,
        Amulet,
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

    /// <summary>An item type (e.g. "Iron Helm"), with a fixed implicit property.</summary>
    public sealed class ItemBaseDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly EquipSlot Slot;
        public readonly StatModifier Implicit;

        public ItemBaseDef(string id, string name, EquipSlot slot, StatModifier @implicit)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Implicit = @implicit;
        }
    }

    public static class ItemCatalog
    {
        /// <summary>Item values grow by this fraction per item level.</summary>
        public const float ScalingPerItemLevel = 0.06f;

        public static float LevelScale(int itemLevel) => 1f + (itemLevel - 1) * ScalingPerItemLevel;

        public static readonly AffixDef[] Affixes =
        {
            new("might", StatType.Might, ModifierKind.PercentAdd, 0.04f, 0.08f),
            new("maxhp", StatType.MaxHealth, ModifierKind.Flat, 8f, 16f),
            new("speed", StatType.MoveSpeed, ModifierKind.PercentAdd, 0.03f, 0.06f),
            new("cooldown", StatType.Cooldown, ModifierKind.PercentAdd, -0.06f, -0.03f),
            new("area", StatType.Area, ModifierKind.PercentAdd, 0.04f, 0.08f),
            new("pickup", StatType.PickupRange, ModifierKind.PercentAdd, 0.10f, 0.20f),
            new("armor", StatType.Armor, ModifierKind.Flat, 0.5f, 1.5f, 6),
            new("regen", StatType.Regen, ModifierKind.Flat, 0.2f, 0.5f, 6),
        };

        public static readonly ItemBaseDef[] Bases =
        {
            new("leather_cap", "革の帽子", EquipSlot.Helm, new StatModifier(StatType.MaxHealth, ModifierKind.Flat, 10f)),
            new("iron_helm", "鉄の兜", EquipSlot.Helm, new StatModifier(StatType.Armor, ModifierKind.Flat, 1f)),
            new("cloth_tunic", "布の服", EquipSlot.Armor, new StatModifier(StatType.MaxHealth, ModifierKind.Flat, 15f)),
            new("chainmail", "鎖帷子", EquipSlot.Armor, new StatModifier(StatType.Armor, ModifierKind.Flat, 2f)),
            new("copper_ring", "銅の指輪", EquipSlot.Ring, new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.05f)),
            new("silver_ring", "銀の指輪", EquipSlot.Ring, new StatModifier(StatType.Cooldown, ModifierKind.PercentAdd, -0.04f)),
            new("wood_charm", "木の護符", EquipSlot.Amulet, new StatModifier(StatType.PickupRange, ModifierKind.PercentAdd, 0.15f)),
            new("bone_necklace", "骨の首飾り", EquipSlot.Amulet, new StatModifier(StatType.Regen, ModifierKind.Flat, 0.3f)),
        };

        private static Dictionary<string, AffixDef> _affixById;
        private static Dictionary<string, ItemBaseDef> _baseById;

        public static AffixDef GetAffix(string id)
        {
            _affixById ??= BuildIndex(Affixes, a => a.Id);
            return _affixById.TryGetValue(id, out var def) ? def : null;
        }

        public static ItemBaseDef GetBase(string id)
        {
            _baseById ??= BuildIndex(Bases, b => b.Id);
            return _baseById.TryGetValue(id, out var def) ? def : null;
        }

        private static Dictionary<string, T> BuildIndex<T>(T[] defs, System.Func<T, string> key)
        {
            var map = new Dictionary<string, T>(defs.Length);
            foreach (var def in defs) map.Add(key(def), def);
            return map;
        }
    }
}
