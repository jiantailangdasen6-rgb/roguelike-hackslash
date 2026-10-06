using System;
using System.Collections.Generic;

namespace Game.Core
{
    [Serializable]
    public struct RolledAffix
    {
        public string affixId;
        public float value;
    }

    /// <summary>A concrete dropped item. Field names are lowercase for JsonUtility persistence.</summary>
    [Serializable]
    public sealed class ItemInstance
    {
        public string uid;
        public string baseId;
        public Rarity rarity;
        public int itemLevel;
        public List<RolledAffix> affixes = new();

        public ItemBaseDef Base => ItemCatalog.GetBase(baseId);
        public EquipSlot Slot => Base.Slot;

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
