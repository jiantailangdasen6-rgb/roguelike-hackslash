using System.Collections.Generic;
using System.Linq;

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
            EquipSlot.Weapon => "ウェポン",
            EquipSlot.Armor => "アーマー",
            EquipSlot.Amulet => "アミュレット",
            EquipSlot.Rune1 => "スキル1ルーン",
            EquipSlot.Rune2 => "スキル2ルーン",
            _ => "リング",
        };

        /// <summary>The skill a rune slot belongs to, by class ("突進斬り").</summary>
        public static string RuneSkill(EquipSlot slot, ClassId cls) =>
            ClassCatalog.AbilityName(cls, slot == EquipSlot.Rune1 ? AbilitySlot.Skill1 : AbilitySlot.Skill2);

        /// <summary>What sets this piece apart beyond its numbers: a rune's effect, or the other weapon's attack.</summary>
        public static IEnumerable<string> SpecialLines(ItemInstance item)
        {
            var rune = ItemCatalog.GetRune(item.rune);
            if (rune != null)
            {
                yield return $"<color=#c9a8ff>◇ 「{RuneSkill(item.Slot, item.classId)}」に宿る：{rune.Text}</color>";
                yield return $"<color=#bfb6aa>効き目 ×{ItemCatalog.RunePower(item.rarity):0.0}（珍しいほど強い）</color>";
            }
            if (item.baseId == "weapon_alt")
            {
                var w = ClassCatalog.AltWeapon(item.classId);
                yield return $"<color=#9fe0ff>◇ 通常攻撃が「{w.BasicName}」に：{w.Text}</color>";
            }
        }

        public static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "最大HP",
            StatType.MoveSpeed => "移動速度",
            StatType.Might => "与えるダメージ",
            StatType.Cooldown => "スキルの再使用時間",
            StatType.Area => "攻撃範囲",
            StatType.PickupRange => "ソウルを拾う範囲",
            StatType.Armor => "防御",
            StatType.Regen => "自然回復",
            StatType.AttackSpeed => "攻撃速度",
            StatType.CritChance => "クリティカル率",
            StatType.ReviveSpeed => "仲間の復活速度",
            StatType.FrozenDamage => "凍った敵へのダメージ",
            StatType.MarkedDamage => "マークの敵へのダメージ",
            StatType.FreezeDuration => "フリーズ時間",
            StatType.MarkDuration => "マークの持続",
            StatType.KillHeal => "敵を倒すとHP回復",
            StatType.KillShield => "敵を倒すとシールド",
            StatType.AllyDamage => "近くの仲間1人ごとにダメージ",
            StatType.LastStand => "HP半分以下で受けるダメージ",
            StatType.DodgeStrike => "ドッジ直後の一撃のダメージ",
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
            var rune = ItemCatalog.GetRune(item.rune);
            if (rune != null) return rune.Name;
            string name = def.ClassNames != null && (int)item.classId < def.ClassNames.Length ? def.ClassNames[(int)item.classId] : def.Name;
            var legendary = ItemCatalog.GetLegendary(item.legendary);
            var set = GearSetCatalog.Get(item.setId);
            if (legendary != null) name = legendary.Name + name;
            else if (set != null) name = set.Prefix + name;
            return item.forge > 0 ? $"{name} +{item.forge}" : name;
        }

        /// <summary>"[希少] 鎖帷子" in the rarity's colour (uGUI rich text).</summary>
        public static string ColoredTitle(ItemInstance item) =>
            $"<color={(GearSetCatalog.Get(item.setId) != null ? SetColor : RarityColor(item.rarity))}>[{RarityName(item.rarity)}] {Name(item)}</color>" + (item.loaner ? "<color=#bfb6aa>（貸し出し）</color>" : "");

        /// <param name="levelCap">Show values as they work in the current room (gear from home is held back early on).</param>
        public static IEnumerable<string> PropertyLines(ItemInstance item, int levelCap = int.MaxValue)
        {
            foreach (var line in SpecialLines(item)) yield return line;
            foreach (var mod in item.GetModifiers(levelCap)) yield return Describe(mod);
            if (item.itemLevel > levelCap)
                yield return $"<color=#bfb6aa>（この部屋ではLv{levelCap}相当のパーク。深く進むと本来のパークに）</color>";
            if (item.loaner) yield return "<color=#bfb6aa>空き枠の貸し出し品：持ち帰れない・渡せない</color>";
            var legendary = ItemCatalog.GetLegendary(item.legendary);
            if (legendary != null) yield return $"<color={RarityColor(Rarity.Legendary)}>◆ {legendary.Text}</color>";
            var set = GearSetCatalog.Get(item.setId);
            if (set != null)
            {
                yield return $"<color={SetColor}>◇ セット装備「{set.Name}」（鎧・アミュレット・リング）</color>";
                yield return $"<color={SetColor}>　2点：{set.TwoText}</color>";
                yield return $"<color={SetColor}>　3点：{set.ThreeText}</color>";
            }
            if (item.forge > 0) yield return $"<color=#bfb6aa>ブラックスミスで +{item.forge}（性能 +{item.forge * 8}%）</color>";
        }

        public const string SetColor = "#6FE39A";
        public const string BetterColor = "#7dff9a";
        public const string WorseColor = "#ff7a6a";

        /// <summary>Which way is good for a stat (shorter cooldowns are better; everything else, more is better).</summary>
        private static bool HigherIsBetter(StatType stat) => stat != StatType.Cooldown;

        /// <summary>
        /// What changes if <paramref name="candidate"/> replaces <paramref name="worn"/> (null = empty slot): one line per
        /// stat that moves, green for better and red for worse, plus legendary effects gained or lost.
        /// </summary>
        public static List<string> CompareLines(ItemInstance candidate, ItemInstance worn, int levelCap = int.MaxValue)
        {
            var totals = new Dictionary<(StatType Stat, ModifierKind Kind), float>();
            void Add(ItemInstance item, float sign)
            {
                if (item == null) return;
                foreach (var mod in item.GetModifiers(levelCap))
                {
                    var key = (mod.Stat, mod.Kind);
                    totals[key] = (totals.TryGetValue(key, out float v) ? v : 0f) + sign * mod.Value;
                }
            }
            Add(candidate, 1f);
            Add(worn, -1f);

            var lines = new List<(bool Better, string Text)>();
            foreach (var ((stat, kind), delta) in totals)
            {
                if (System.Math.Abs(delta) < 0.0005f) continue;
                bool better = HigherIsBetter(stat) ? delta > 0f : delta < 0f;
                string text = Describe(new StatModifier(stat, kind, delta));
                lines.Add((better, $"<color={(better ? BetterColor : WorseColor)}>{(better ? "▲" : "▼")} {text}</color>"));
            }
            var result = lines.OrderByDescending(l => l.Better).Select(l => l.Text).ToList();

            var gained = ItemCatalog.GetLegendary(candidate?.legendary ?? LegendaryId.None);
            var lost = ItemCatalog.GetLegendary(worn?.legendary ?? LegendaryId.None);
            if (gained != null && gained != lost) result.Add($"<color={BetterColor}>▲ ◆ {gained.Text}</color>");
            if (lost != null && lost != gained) result.Add($"<color={WorseColor}>▼ ◆ {lost.Text}（失う）</color>");
            // A different rune or weapon kind: say what changes.
            if (candidate != null && (candidate.rune != (worn?.rune ?? RuneId.None) || (candidate.baseId == "weapon_alt") != (worn?.baseId == "weapon_alt")))
                foreach (var line in SpecialLines(candidate)) result.Insert(0, line);
            if (candidate != null && candidate.Slot == EquipSlot.Weapon && candidate.baseId != "weapon_alt" && worn?.baseId == "weapon_alt")
                result.Insert(0, $"<color=#9fe0ff>◇ 通常攻撃が「{ClassCatalog.Get(candidate.classId).BasicName}」に戻る</color>");
            if (result.Count == 0) result.Add("<color=#bfb6aa>性能は変わらない</color>");
            return result;
        }
    }
}
