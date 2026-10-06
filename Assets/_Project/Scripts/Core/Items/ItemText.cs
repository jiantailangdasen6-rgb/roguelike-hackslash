using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Display strings for items and stats (Japanese UI).</summary>
    public static class ItemText
    {
        public static string RarityName(Rarity rarity) => rarity switch
        {
            Rarity.Common => "コモン",
            Rarity.Magic => "マジック",
            Rarity.Rare => "レア",
            _ => "レジェンダリー",
        };

        /// <summary>Hex color used for the rarity in rich text.</summary>
        public static string RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Common => "#E6E6E6",
            Rarity.Magic => "#6FA8FF",
            Rarity.Rare => "#FFD84A",
            _ => "#FF9A3C",
        };

        public static string SlotName(EquipSlot slot) => slot switch
        {
            EquipSlot.Helm => "兜",
            EquipSlot.Armor => "鎧",
            EquipSlot.Ring => "指輪",
            _ => "首飾り",
        };

        public static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "最大HP",
            StatType.MoveSpeed => "移動速度",
            StatType.Might => "攻撃力",
            StatType.Cooldown => "クールダウン",
            StatType.Area => "攻撃範囲",
            StatType.PickupRange => "回収範囲",
            StatType.Armor => "防御",
            StatType.Regen => "自然回復",
            _ => "発射数",
        };

        public static string Describe(StatModifier mod)
        {
            string name = StatName(mod.Stat);
            string sign = mod.Value >= 0 ? "+" : "−";
            float abs = System.Math.Abs(mod.Value);
            return mod.Kind == ModifierKind.Flat
                ? $"{name} {sign}{FormatFlat(mod.Stat, abs)}"
                : $"{name} {sign}{abs * 100f:0}%";
        }

        private static string FormatFlat(StatType stat, float value) =>
            stat == StatType.Regen ? $"{value:0.0}/秒" : value < 10f ? $"{value:0.#}" : $"{value:0}";

        public static string Name(ItemInstance item) => item.Base?.Name ?? item.baseId;

        /// <summary>"[レア] 鉄の兜 iLv3" with rarity color (uGUI rich text).</summary>
        public static string ColoredTitle(ItemInstance item) =>
            $"<color={RarityColor(item.rarity)}>[{RarityName(item.rarity)}] {Name(item)}</color>  iLv{item.itemLevel}";

        public static IEnumerable<string> PropertyLines(ItemInstance item)
        {
            foreach (var mod in item.GetModifiers()) yield return Describe(mod);
        }
    }
}
