using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Display strings for items and stats (Japanese UI).</summary>
    public static class ItemText
    {
        public static string RarityName(Rarity rarity) => rarity switch
        {
            Rarity.Common => "並",
            Rarity.Magic => "上質",
            Rarity.Rare => "希少",
            _ => "伝説",
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
            EquipSlot.Weapon => "武器",
            EquipSlot.Armor => "防具",
            EquipSlot.Amulet => "首飾り",
            _ => "指輪",
        };

        public static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "最大HP",
            StatType.MoveSpeed => "移動速度",
            StatType.Might => "与えるダメージ",
            StatType.Cooldown => "スキルの再使用時間",
            StatType.Area => "攻撃範囲",
            StatType.PickupRange => "魂を拾う範囲",
            StatType.Armor => "防御",
            StatType.Regen => "自然回復",
            StatType.AttackSpeed => "攻撃速度",
            StatType.CritChance => "会心率",
            StatType.ReviveSpeed => "仲間の復活速度",
            StatType.FrozenDamage => "凍った敵へのダメージ",
            StatType.MarkedDamage => "印の敵へのダメージ",
            StatType.FreezeDuration => "凍結時間",
            StatType.MarkDuration => "印の持続",
            StatType.KillHeal => "敵を倒すとHP回復",
            StatType.KillShield => "敵を倒すとシールド",
            StatType.AllyDamage => "近くの仲間1人ごとにダメージ",
            StatType.LastStand => "HP半分以下で受けるダメージ",
            StatType.DodgeStrike => "回避直後の一撃のダメージ",
            _ => stat.ToString(),
        };

        /// <summary>Stats whose flat values read as percentages (fractions on a zero base).</summary>
        private static bool FlatIsPercent(StatType stat) => stat is StatType.CritChance or StatType.FrozenDamage or StatType.MarkedDamage
            or StatType.FreezeDuration or StatType.MarkDuration or StatType.AllyDamage or StatType.LastStand or StatType.DodgeStrike;

        public static string Describe(StatModifier mod)
        {
            string name = StatName(mod.Stat);
            float value = mod.Stat == StatType.LastStand ? -mod.Value : mod.Value; // shown as damage taken going down
            string sign = value >= 0 ? "+" : "−";
            float abs = System.Math.Abs(value);
            if (mod.Kind != ModifierKind.Flat || FlatIsPercent(mod.Stat)) return $"{name} {sign}{abs * 100f:0}%";
            return $"{name} {sign}{FormatFlat(mod.Stat, abs)}";
        }

        private static string FormatFlat(StatType stat, float value) =>
            stat == StatType.Regen ? $"{value:0.0}/秒" : value < 10f ? $"{value:0.#}" : $"{value:0}";

        public static string Name(ItemInstance item)
        {
            var def = item.Base;
            if (def == null) return item.baseId;
            string name = def.ClassNames != null && (int)item.classId < def.ClassNames.Length ? def.ClassNames[(int)item.classId] : def.Name;
            var legendary = ItemCatalog.GetLegendary(item.legendary);
            return legendary != null ? legendary.Name + name : name;
        }

        /// <summary>"[希少] 鎖帷子" in the rarity's colour (uGUI rich text).</summary>
        public static string ColoredTitle(ItemInstance item) =>
            $"<color={RarityColor(item.rarity)}>[{RarityName(item.rarity)}] {Name(item)}</color>";

        public static IEnumerable<string> PropertyLines(ItemInstance item)
        {
            foreach (var mod in item.GetModifiers()) yield return Describe(mod);
            var legendary = ItemCatalog.GetLegendary(item.legendary);
            if (legendary != null) yield return $"<color={RarityColor(Rarity.Legendary)}>◆ {legendary.Text}</color>";
        }
    }
}
