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

        private static Sprite Frame(string sheet, int index)
        {
            var s = SpriteSheet.Load(sheet, quiet: true);
            var frames = s?.Sprites("default");
            return frames != null && index >= 0 && index < frames.Length ? frames[index] : null;
        }
    }
}
