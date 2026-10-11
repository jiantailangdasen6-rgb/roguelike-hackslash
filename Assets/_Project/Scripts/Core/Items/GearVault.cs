using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// This device's gear between runs (kept in <see cref="ProfileData.gear"/>): the four pieces worn when setting out
    /// and the stash. Items are stored packed (<see cref="ItemCodec"/>, hex) so the save stays small.
    /// </summary>
    [Serializable]
    public sealed class GearVaultData
    {
        /// <summary>Worn pieces by <see cref="EquipSlot"/> ("" = empty).</summary>
        public List<string> equipped = new();
        /// <summary>The stash (not worn).</summary>
        public List<string> items = new();
        public int capacity = GearVault.BaseCapacity;
    }

    /// <summary>
    /// Stash rules: everything a player carries at the end of a run comes home (loaners excepted); only the worn pieces
    /// go out on the next run. A full stash breaks the weakest extras down into soul shards.
    /// </summary>
    public static class GearVault
    {
        public const int BaseCapacity = 30;
        public const int CapacityStep = 10;
        public const int MaxCapacity = 80;

        /// <summary>Soul shards for the next +10 slots.</summary>
        public static int ExpandCost(int capacity) => 20 + (capacity - BaseCapacity) * 2;

        public static string Pack(ItemInstance item) => item == null ? "" : ItemCodec.Pack(item).ToString("x16");

        public static ItemInstance Unpack(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            return ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out ulong v) ? ItemCodec.Unpack(v, hex) : null;
        }

        /// <summary>Repairs a loaded (or old, or tampered) vault: four worn entries, valid items, sane capacity.</summary>
        public static GearVaultData Normalize(GearVaultData data)
        {
            data ??= new GearVaultData();
            data.equipped ??= new List<string>();
            data.items ??= new List<string>();
            while (data.equipped.Count < SimPlayer.SlotCount) data.equipped.Add("");
            if (data.equipped.Count > SimPlayer.SlotCount) data.equipped.RemoveRange(SimPlayer.SlotCount, data.equipped.Count - SimPlayer.SlotCount);
            for (int s = 0; s < SimPlayer.SlotCount; s++)
            {
                var item = Unpack(data.equipped[s]);
                if (item == null || (int)item.Slot != s || item.loaner) data.equipped[s] = "";
            }
            data.items.RemoveAll(h => Unpack(h) == null || Unpack(h).loaner);
            data.capacity = Math.Clamp(data.capacity <= 0 ? BaseCapacity : data.capacity, BaseCapacity, MaxCapacity);
            return data;
        }

        public static ItemInstance Equipped(GearVaultData data, EquipSlot slot) => Unpack(data.equipped[(int)slot]);

        /// <summary>The worn pieces as sent to the host when setting out.</summary>
        public static List<string> LoadoutGear(GearVaultData data) => data.equipped.Where(h => !string.IsNullOrEmpty(h)).ToList();

        /// <summary>Puts a stash piece on; the piece it replaces goes back into the stash.</summary>
        public static bool Equip(GearVaultData data, int stashIndex)
        {
            if (stashIndex < 0 || stashIndex >= data.items.Count) return false;
            var item = Unpack(data.items[stashIndex]);
            if (item == null) return false;
            int slot = (int)item.Slot;
            string old = data.equipped[slot];
            data.equipped[slot] = data.items[stashIndex];
            data.items.RemoveAt(stashIndex);
            if (!string.IsNullOrEmpty(old)) data.items.Insert(stashIndex, old);
            return true;
        }

        public static bool Unequip(GearVaultData data, EquipSlot slot)
        {
            string worn = data.equipped[(int)slot];
            if (string.IsNullOrEmpty(worn) || data.items.Count >= data.capacity) return false;
            data.equipped[(int)slot] = "";
            data.items.Add(worn);
            return true;
        }

        /// <summary>Breaks a stash piece down; returns the soul shards it gives.</summary>
        public static int Salvage(GearVaultData data, int stashIndex)
        {
            if (stashIndex < 0 || stashIndex >= data.items.Count) return 0;
            var item = Unpack(data.items[stashIndex]);
            data.items.RemoveAt(stashIndex);
            return item != null ? RunSimulation.SalvageValue(item.rarity) : 0;
        }

        /// <summary>Breaks down every common (white) piece in the stash.</summary>
        public static int SalvageCommons(GearVaultData data)
        {
            int shards = 0;
            for (int i = data.items.Count - 1; i >= 0; i--)
                if (Unpack(data.items[i])?.rarity == Rarity.Common) shards += Salvage(data, i);
            return shards;
        }

        /// <summary>Rarest first, then by slot and level.</summary>
        public static void Sort(GearVaultData data)
        {
            data.items = data.items
                .Select(h => (Hex: h, Item: Unpack(h)))
                .OrderByDescending(x => x.Item.rarity).ThenBy(x => x.Item.Slot).ThenByDescending(x => x.Item.itemLevel)
                .Select(x => x.Hex).ToList();
        }

        public sealed class BankResult
        {
            public int Kept;
            public int OverflowShards;
            public int OverflowCount;
        }

        /// <summary>
        /// End of a run: what the player wears becomes their worn set at home, the bag goes into the stash, loaners stay
        /// behind. Pieces brought out but given away or salvaged are gone. A stash past its capacity breaks the weakest
        /// extras down into shards (returned so the caller can pay them out).
        /// </summary>
        public static BankResult BankRun(GearVaultData data, IReadOnlyList<ItemInstance> worn, IEnumerable<ItemInstance> bag)
        {
            Normalize(data);
            var result = new BankResult();
            for (int s = 0; s < SimPlayer.SlotCount; s++)
            {
                var item = s < worn.Count ? worn[s] : null;
                data.equipped[s] = item != null && !item.loaner && (int)item.Slot == s ? Pack(item) : "";
                if (data.equipped[s].Length > 0) result.Kept++;
            }
            foreach (var item in bag)
            {
                if (item == null || item.loaner) continue;
                data.items.Add(Pack(item));
                result.Kept++;
            }

            while (data.items.Count > data.capacity)
            {
                int weakest = 0;
                for (int i = 1; i < data.items.Count; i++)
                {
                    var a = Unpack(data.items[i]);
                    var b = Unpack(data.items[weakest]);
                    if (a.rarity < b.rarity || (a.rarity == b.rarity && a.itemLevel < b.itemLevel)) weakest = i;
                }
                result.OverflowShards += Salvage(data, weakest);
                result.OverflowCount++;
            }
            return result;
        }
    }
}
