using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// The camp's blacksmith (in the vault screen): forging strengthens a piece by <see cref="ForgePerLevel"/> per
    /// level (up to +<see cref="MaxForge"/>), rerolling draws one of its properties anew. Both cost soul shards and
    /// return a changed copy (the vault stores items as their 64 bits).
    /// </summary>
    public static class Blacksmith
    {
        public const int MaxForge = 5;
        public const float ForgePerLevel = 0.08f;

        public static int ForgeCost(ItemInstance item) => (item.forge + 1) * (10 + 10 * (int)item.rarity);
        public static int RerollCost(ItemInstance item) => 15 + 15 * (int)item.rarity;

        public static bool CanForge(ItemInstance item) => item != null && !item.loaner && item.forge < MaxForge;
        public static bool CanReroll(ItemInstance item) => item != null && !item.loaner && item.affixes.Count > 0;

        private static ItemInstance Copy(ItemInstance item) => ItemCodec.Unpack(ItemCodec.Pack(item), item.uid);

        public static ItemInstance Forged(ItemInstance item)
        {
            var copy = Copy(item);
            copy.forge = System.Math.Min(MaxForge, copy.forge + 1);
            return copy;
        }

        /// <summary>One property (picked by the seed) replaced by another the item doesn't have yet, with a fresh roll.</summary>
        public static ItemInstance Rerolled(ItemInstance item, ulong seed)
        {
            var copy = Copy(item);
            if (copy.affixes.Count == 0) return copy;
            var rng = new GameRandom(seed);
            int index = rng.Range(0, copy.affixes.Count);
            var pool = new List<AffixDef>();
            var implicitStat = copy.Base.Implicit.Stat;
            foreach (var a in ItemCatalog.Affixes)
            {
                if (a.Stat == implicitStat) continue;
                bool taken = false;
                for (int k = 0; k < copy.affixes.Count; k++)
                    if (k != index && copy.affixes[k].affixId == a.Id) taken = true;
                if (!taken) pool.Add(a);
            }
            int total = 0;
            foreach (var a in pool) total += a.Weight;
            int pick = rng.Range(0, total);
            var chosen = pool[0];
            foreach (var a in pool)
            {
                if (pick < a.Weight)
                {
                    chosen = a;
                    break;
                }
                pick -= a.Weight;
            }
            int roll = rng.Range(0, ItemInstance.MaxRoll + 1);
            copy.affixes[index] = new RolledAffix { affixId = chosen.Id, roll = roll, value = ItemInstance.AffixValue(chosen, roll, copy.itemLevel) };
            return copy;
        }
    }
}
