using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Rolls items deterministically from a seed, so the host only has to send
    /// (seed, item level, rarity boost) and each client rebuilds the same item.
    /// </summary>
    public static class ItemGenerator
    {
        // Weights for Common / Magic / Rare. Legendary is not implemented yet.
        private static readonly float[] BaseRarityWeights = { 60f, 30f, 10f };

        /// <param name="rarityBoost">0 = normal enemy, 1 = elite, 3 = boss. Shifts weight toward higher rarities.</param>
        public static ItemInstance Generate(ulong seed, int itemLevel, float rarityBoost = 0f)
        {
            var rng = new GameRandom(seed);
            var rarity = RollRarity(rng, rarityBoost);
            var baseDef = ItemCatalog.Bases[rng.Range(0, ItemCatalog.Bases.Length)];

            var item = new ItemInstance
            {
                uid = seed.ToString("x16"),
                baseId = baseDef.Id,
                rarity = rarity,
                itemLevel = itemLevel,
            };

            int affixCount = rarity switch
            {
                Rarity.Common => rng.Range(0, 2),
                Rarity.Magic => rng.Range(1, 3),
                _ => rng.Range(3, 5),
            };
            RollAffixes(rng, item, affixCount);
            return item;
        }

        private static Rarity RollRarity(GameRandom rng, float boost)
        {
            float common = BaseRarityWeights[0] / (1f + boost);
            float magic = BaseRarityWeights[1] * (1f + boost * 0.5f);
            float rare = BaseRarityWeights[2] * (1f + boost * 1.5f);
            float roll = rng.Value() * (common + magic + rare);
            if (roll < common) return Rarity.Common;
            if (roll < common + magic) return Rarity.Magic;
            return Rarity.Rare;
        }

        private static void RollAffixes(GameRandom rng, ItemInstance item, int count)
        {
            var pool = new List<AffixDef>(ItemCatalog.Affixes);
            float scale = ItemCatalog.LevelScale(item.itemLevel);

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
                float value = (affix.Min + (affix.Max - affix.Min) * rng.Value()) * scale;
                item.affixes.Add(new RolledAffix { affixId = affix.Id, value = value });
            }
        }
    }
}
