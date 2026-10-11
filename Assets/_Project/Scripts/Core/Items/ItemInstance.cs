using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct RolledAffix
    {
        public string affixId;
        /// <summary>Where the value fell in the affix's range, 0..<see cref="ItemInstance.MaxRoll"/> (what goes over the wire).</summary>
        public int roll;
        public float value;
    }

    /// <summary>
    /// A concrete item. Plain serializable data (lowercase fields for JsonUtility) so the codex can keep whole items
    /// and a later "keep your gear" mode can store them; <see cref="ItemCodec"/> packs one into 64 bits for the network.
    /// </summary>
    [Serializable]
    public sealed class ItemInstance
    {
        public const int MaxRoll = 127;

        public string uid;
        public string baseId;
        public Rarity rarity;
        public int itemLevel;
        public ClassId classId;
        public LegendaryId legendary;
        public List<RolledAffix> affixes = new();

        public ItemBaseDef Base => ItemCatalog.GetBase(baseId);
        public EquipSlot Slot => Base?.Slot ?? EquipSlot.Ring;

        public static float AffixValue(AffixDef affix, int roll, int itemLevel) =>
            Mathf.Lerp(affix.Min, affix.Max, Mathf.Clamp(roll, 0, MaxRoll) / (float)MaxRoll) * ItemCatalog.LevelScale(itemLevel);

        public void AddAffix(AffixDef affix, int roll) =>
            affixes.Add(new RolledAffix { affixId = affix.Id, roll = roll, value = AffixValue(affix, roll, itemLevel) });

        /// <summary>Implicit property (scaled by item level) followed by the rolled affixes.</summary>
        public IEnumerable<StatModifier> GetModifiers()
        {
            var def = Base;
            if (def == null) yield break;

            var imp = def.Implicit;
            yield return new StatModifier(imp.Stat, imp.Kind, imp.Value * ItemCatalog.LevelScale(itemLevel));

            foreach (var rolled in affixes)
            {
                var affix = ItemCatalog.GetAffix(rolled.affixId);
                if (affix != null) yield return new StatModifier(affix.Stat, affix.Kind, rolled.value);
            }
        }
    }
}
