using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Rolls loot: base (any slot), rarity, affixes (common 0, fine 1, rare 2, legendary 2 + a unique effect for the
    /// owner's class). Deterministic for a seed.
    /// </summary>
    public static class ItemGenerator
    {
        // Common / Magic / Rare / Legendary.
        // 2026-10-12 (second pass): legendaries had become an every-run thing. Now about 0.4% from a plain monster,
        // ~2% from an elite, ~5% from a boss; deeper areas and harder difficulties (rarity boost) raise the odds.
        private static readonly float[] BaseRarityWeights = { 62f, 30f, 7f, 0.4f };

        /// <param name="rarityBoost">0 = normal enemy, 1 = elite, 2.5 = boss / treasure, plus area and difficulty. Shifts weight toward higher rarities.</param>
        /// <param name="minRarity">Floor for guaranteed drops (treasure doors).</param>
        public static ItemInstance Generate(ulong seed, int itemLevel, float rarityBoost = 0f, ClassId owner = ClassId.Warrior, Rarity minRarity = Rarity.Common, EquipSlot? slot = null)
        {
            var rng = new GameRandom(seed);
            var rarity = RollRarity(rng, rarityBoost);
            if (rarity < minRarity) rarity = minRarity;
            var bases = slot.HasValue ? System.Array.FindAll(ItemCatalog.Bases, b => b.Slot == slot.Value) : ItemCatalog.Bases;
            var baseDef = bases[rng.Range(0, bases.Length)];

            var item = new ItemInstance
            {
                uid = seed.ToString("x16"),
                baseId = baseDef.Id,
                rarity = rarity,
                itemLevel = UnityEngine.Mathf.Clamp(itemLevel, 1, ItemCodec.MaxItemLevel),
                classId = owner,
            };

            int affixCount = rarity switch
            {
                Rarity.Common => 0,
                Rarity.Magic => 1,
                _ => 2,
            };
            RollAffixes(rng, item, affixCount);

            if (rarity == Rarity.Legendary)
            {
                var options = new List<LegendaryDef>();
                foreach (var def in ItemCatalog.Legendaries) if (def.Class == owner) options.Add(def);
                if (options.Count > 0) item.legendary = options[rng.Range(0, options.Count)].Id;
            }
            return item;
        }

        private static Rarity RollRarity(GameRandom rng, float boost)
        {
            boost = UnityEngine.Mathf.Max(-0.5f, boost);
            float common = BaseRarityWeights[0] / (1f + boost * 0.6f);
            float magic = BaseRarityWeights[1] * (1f + boost * 0.4f);
            float rare = BaseRarityWeights[2] * (1f + boost * 1.1f);
            float legendary = BaseRarityWeights[3] * (1f + boost) * (1f + boost);
            float roll = rng.Value() * (common + magic + rare + legendary);
            if (roll < common) return Rarity.Common;
            if (roll < common + magic) return Rarity.Magic;
            if (roll < common + magic + rare) return Rarity.Rare;
            return Rarity.Legendary;
        }

        private static void RollAffixes(GameRandom rng, ItemInstance item, int count)
        {
            var pool = new List<AffixDef>(ItemCatalog.Affixes);
            // The implicit property isn't rolled again as an affix.
            var implicitStat = item.Base.Implicit.Stat;
            pool.RemoveAll(a => a.Stat == implicitStat);

            for (int n = 0; n < count && pool.Count > 0; n++)
            {
                int totalWeight = 0;
                foreach (var a in pool) totalWeight += a.Weight;

                int pick = rng.Range(0, totalWeight);
                int index = 0;
                while (pick >= pool[index].Weight)
                {
                    pick -= pool[index].Weight;
                    index++;
                }

                var affix = pool[index];
                pool.RemoveAt(index);
                item.AddAffix(affix, rng.Range(0, ItemInstance.MaxRoll + 1));
            }
        }
    }

    /// <summary>
    /// One item in 64 bits, so a drop fits in a single event:
    /// base 4 | rarity 2 | class 2 | legendary 3 | item level 5 | affix count 2 | 2 x (affix 5 + roll 7) | loaner 1
    /// | class high bit 1 | legendary high bit 1 | base high bit 1. The high bits were added later (six classes,
    /// more legendaries and bases); old saves have them at 0, so they still read the same.
    /// </summary>
    public static class ItemCodec
    {
        public const int MaxItemLevel = 31;
        public const int MaxAffixes = 2;

        public static ulong Pack(ItemInstance item)
        {
            ulong v = 0;
            int shift = 0;
            void Put(int value, int bits)
            {
                v |= ((ulong)value & ((1ul << bits) - 1)) << shift;
                shift += bits;
            }

            int baseIndex = ItemCatalog.BaseIndex(item.baseId);
            Put(baseIndex, 4);
            Put((int)item.rarity, 2);
            Put((int)item.classId, 2);
            Put((int)item.legendary, 3);
            Put(item.itemLevel, 5);
            int count = System.Math.Min(item.affixes.Count, MaxAffixes);
            Put(count, 2);
            for (int i = 0; i < count; i++)
            {
                Put(ItemCatalog.AffixIndex(item.affixes[i].affixId), 5);
                Put(item.affixes[i].roll, 7);
            }
            for (int i = count; i < MaxAffixes; i++) Put(0, 12); // fixed layout: the loaner bit always sits at bit 42
            Put(item.loaner ? 1 : 0, 1);
            Put((int)item.classId >> 2, 1);
            Put((int)item.legendary >> 3, 1);
            Put(baseIndex >> 4, 1);
            return v;
        }

        /// <summary>The item, or null if the bits don't describe one (empty slot = 0).</summary>
        public static ItemInstance Unpack(ulong v, string uid = null)
        {
            if (v == 0) return null;
            int shift = 0;
            int Take(int bits)
            {
                int value = (int)((v >> shift) & ((1ul << bits) - 1));
                shift += bits;
                return value;
            }

            int baseLow = Take(4);
            var rarity = (Rarity)Take(2);
            int classLow = Take(2);
            int legendaryLow = Take(3);
            int itemLevel = Take(5);
            var affixes = new List<(int Affix, int Roll)>();
            int count = Take(2);
            for (int i = 0; i < MaxAffixes; i++)
            {
                int affix = Take(5);
                int roll = Take(7);
                if (i < count) affixes.Add((affix, roll));
            }
            bool loaner = Take(1) != 0;
            int classId = classLow | Take(1) << 2;
            int legendary = legendaryLow | Take(1) << 3;
            int baseIndex = baseLow | Take(1) << 4;
            if (baseIndex >= ItemCatalog.Bases.Length) return null;

            var item = new ItemInstance
            {
                uid = uid ?? v.ToString("x16"),
                baseId = ItemCatalog.Bases[baseIndex].Id,
                rarity = rarity,
                classId = (ClassId)classId,
                legendary = (LegendaryId)legendary,
                itemLevel = itemLevel,
                loaner = loaner,
            };
            foreach (var (affix, roll) in affixes)
                if (affix < ItemCatalog.Affixes.Length) item.AddAffix(ItemCatalog.Affixes[affix], roll);
            return item;
        }
    }
}
