using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Gear sets (組装備): an armour, an amulet and a ring that belong together. Wearing two pieces of a set gives its
    /// first bonus, all three its second as well. Set pieces drop as rare gear (ItemGenerator); the set travels in
    /// the item's bits (ItemCodec), so old saves read "no set".
    /// </summary>
    public sealed class GearSetDef
    {
        public int Id;            // 1.. (0 = no set)
        public string Name;       // "狩人王の装い"
        public string Prefix;     // "狩人王の" (put before the item's name)
        public StatModifier[] Two;
        public StatModifier[] Three;
        public string TwoText;
        public string ThreeText;
    }

    public static class GearSetCatalog
    {
        public const int PiecesPerSet = 3;
        /// <summary>Share of rare armours, amulets and rings that belong to a set.</summary>
        public const float SetChance = 0.25f;

        private static StatModifier M(StatType stat, ModifierKind kind, float value) => new(stat, kind, value);

        public static readonly GearSetDef[] All =
        {
            new()
            {
                Id = 1, Name = "狩人王の装い", Prefix = "狩人王の",
                Two = new[] { M(StatType.CritChance, ModifierKind.Flat, 0.08f) }, TwoText = "会心率 +8%",
                Three = new[] { M(StatType.MarkedDamage, ModifierKind.Flat, 0.35f), M(StatType.MarkDuration, ModifierKind.Flat, 0.3f) },
                ThreeText = "印の敵へのダメージ +35%・印が30%長く続く",
            },
            new()
            {
                Id = 2, Name = "氷葬の礼装", Prefix = "氷葬の",
                Two = new[] { M(StatType.Cooldown, ModifierKind.PercentAdd, -0.1f) }, TwoText = "技の待ち時間 -10%",
                Three = new[] { M(StatType.FrozenDamage, ModifierKind.Flat, 0.4f), M(StatType.FreezeDuration, ModifierKind.Flat, 0.3f) },
                ThreeText = "凍った敵へのダメージ +40%・凍結が30%長く続く",
            },
            new()
            {
                Id = 3, Name = "不屈の鎧", Prefix = "不屈の",
                Two = new[] { M(StatType.MaxHealth, ModifierKind.PercentAdd, 0.15f) }, TwoText = "最大HP +15%",
                Three = new[] { M(StatType.DamageTaken, ModifierKind.PercentAdd, -0.12f), M(StatType.LastStand, ModifierKind.Flat, 0.2f) },
                ThreeText = "受けるダメージ -12%・HPが半分以下でさらに -20%",
            },
            new()
            {
                Id = 4, Name = "盟友の誓い", Prefix = "盟友の",
                Two = new[] { M(StatType.ReviveSpeed, ModifierKind.PercentAdd, 0.5f) }, TwoText = "仲間を起こす速さ +50%",
                Three = new[] { M(StatType.AllyDamage, ModifierKind.Flat, 0.12f), M(StatType.KillShield, ModifierKind.Flat, 5f) },
                ThreeText = "近くの仲間1人ごとにダメージ +12%・倒すたびに盾 +5",
            },
            new()
            {
                Id = 5, Name = "疾風の装束", Prefix = "疾風の",
                Two = new[] { M(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.1f) }, TwoText = "移動速度 +10%",
                Three = new[] { M(StatType.DodgeStrike, ModifierKind.Flat, 0.8f), M(StatType.AttackSpeed, ModifierKind.PercentAdd, 0.1f) },
                ThreeText = "回避直後の一撃 +80%・攻撃速度 +10%",
            },
            new()
            {
                Id = 6, Name = "業火の遺物", Prefix = "業火の",
                Two = new[] { M(StatType.Might, ModifierKind.PercentAdd, 0.1f) }, TwoText = "与えるダメージ +10%",
                Three = new[] { M(StatType.Area, ModifierKind.PercentAdd, 0.2f), M(StatType.KillHeal, ModifierKind.Flat, 3f) },
                ThreeText = "技の範囲 +20%・倒すたびにHP +3",
            },
        };

        public static GearSetDef Get(int id) => id >= 1 && id <= All.Length ? All[id - 1] : null;

        public static bool CanBeSetPiece(EquipSlot slot) => slot is EquipSlot.Armor or EquipSlot.Amulet or EquipSlot.Ring;

        /// <summary>How many pieces of each set are worn (set id → count).</summary>
        public static Dictionary<int, int> Count(IEnumerable<ItemInstance> worn)
        {
            var counts = new Dictionary<int, int>();
            foreach (var item in worn)
                if (item != null && item.setId > 0 && Get(item.setId) != null && CanBeSetPiece(item.Slot))
                    counts[item.setId] = (counts.TryGetValue(item.setId, out int n) ? n : 0) + 1;
            return counts;
        }

        /// <summary>The set bonuses that the worn gear earns.</summary>
        public static List<StatModifier> Bonuses(IEnumerable<ItemInstance> worn)
        {
            var mods = new List<StatModifier>();
            foreach (var (id, n) in Count(worn))
            {
                var set = Get(id);
                if (n >= 2) mods.AddRange(set.Two);
                if (n >= 3) mods.AddRange(set.Three);
            }
            return mods;
        }
    }
}
