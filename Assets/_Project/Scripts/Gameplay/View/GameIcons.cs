using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Generated pixel icons: one frame per perk (in PerkId order, then the rest icon) and per trinket.</summary>
    public static class GameIcons
    {
        public static Sprite Perk(PerkOption option) => option.IsHeal ? Frame("icons_perk", PerkCatalog.Count) : Perk((PerkId)option.Perk);

        public static Sprite Perk(PerkId id) => Frame("icons_perk", (int)id);

        public static Sprite Trinket(string id)
        {
            for (int i = 0; i < MetaCatalog.Trinkets.Length; i++)
                if (MetaCatalog.Trinkets[i].Id == id) return Frame("icons_trinket", i);
            return null;
        }

        public static Sprite Reward(RoomReward reward) => Frame("icons_reward", RewardCatalog.IconIndex(reward));

        public static Sprite Trial => Frame("icons_reward", RewardCatalog.TrialIconIndex);

        /// <summary>Weapon pictures per base in IconArt.Items (one per class, six classes).</summary>
        private const int WeaponLooks = 6;

        /// <summary>Gear: the two weapon bases have a picture per class (see IconArt.Items), the rest one each.</summary>
        public static Sprite Item(ItemInstance item)
        {
            if (item == null) return null;
            int b = ItemCatalog.BaseIndex(item.baseId);
            int cls = Mathf.Clamp((int)item.classId, 0, WeaponLooks - 1);
            int frame = b switch
            {
                0 => cls,
                1 => WeaponLooks + cls,
                _ => 2 * WeaponLooks + (b - 2),
            };
            return Frame("icons_item", frame);
        }

        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.82f, 0.8f, 0.76f),
            Rarity.Magic => new Color(0.44f, 0.66f, 1f),
            Rarity.Rare => new Color(1f, 0.85f, 0.29f),
            _ => new Color(1f, 0.6f, 0.24f),
        };

        private static Sprite Frame(string sheet, int index)
        {
            var s = SpriteSheet.Load(sheet, quiet: true);
            var frames = s?.Sprites("default");
            return frames != null && index >= 0 && index < frames.Length ? frames[index] : null;
        }
    }
}
