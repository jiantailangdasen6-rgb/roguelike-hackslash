using UnityEngine;

namespace Game.Core
{
    /// <summary>Names, map marks, colours and pedestal texts of the room kinds on an area map.</summary>
    public static class RoomCatalog
    {
        public static string Name(RoomKind k) => k switch
        {
            RoomKind.Start => "入口",
            RoomKind.Combat => "戦いの間",
            RoomKind.Elite => "精鋭の巣",
            RoomKind.Shrine => "祈りの祠",
            RoomKind.Whetstone => "研磨台",
            RoomKind.Spring => "生命の泉",
            RoomKind.CurseAltar => "呪いの祭壇",
            RoomKind.Merchant => "闇商人",
            RoomKind.Campfire => "焚き火の間",
            RoomKind.Boss => "主の間",
            _ => "",
        };

        /// <summary>One character for the minimap.</summary>
        public static string Mark(RoomKind k) => k switch
        {
            RoomKind.Start => "入",
            RoomKind.Combat => "戦",
            RoomKind.Elite => "精",
            RoomKind.Shrine => "祠",
            RoomKind.Whetstone => "研",
            RoomKind.Spring => "泉",
            RoomKind.CurseAltar => "呪",
            RoomKind.Merchant => "商",
            RoomKind.Campfire => "火",
            RoomKind.Boss => "主",
            _ => "",
        };

        /// <summary>The reward a pedestal room gives (icons and colours are shared with the old reward doors).</summary>
        public static RoomReward Reward(RoomKind k) => k switch
        {
            RoomKind.Shrine => RoomReward.Boon,
            RoomKind.Whetstone => RoomReward.Upgrade,
            RoomKind.Spring => RoomReward.Vigor,
            RoomKind.CurseAltar => RoomReward.Cursed,
            RoomKind.Merchant => RoomReward.Shop,
            _ => RoomReward.None,
        };

        public static bool HasPedestal(RoomKind k) => Reward(k) != RoomReward.None;

        public static Color Color(RoomKind k) => k switch
        {
            RoomKind.Combat => new Color(0.85f, 0.42f, 0.36f),
            RoomKind.Elite => new Color(1f, 0.55f, 0.2f),
            RoomKind.Campfire => new Color(1f, 0.72f, 0.36f),
            RoomKind.Boss => new Color(1f, 0.25f, 0.25f),
            RoomKind.Start => new Color(0.72f, 0.7f, 0.68f),
            _ => RewardCatalog.Color(Reward(k)),
        };

        /// <summary>What standing on the pedestal does (shown under it and on the HUD).</summary>
        public static string PedestalText(RoomKind k) => k switch
        {
            RoomKind.Shrine => "上に立つと、新しい力を1つ選べる",
            RoomKind.Whetstone => "上に立つと、持っている力を1つ強化できる",
            RoomKind.Spring => $"上に立つと、最大HP +{RunSimulation.VigorPerStack * 100f:0}%・全回復",
            RoomKind.CurseAltar => $"上に立つと、力を{RewardCatalog.CursedBoons}回選べる。代償に呪いが1つつく（ランの間ずっと）",
            RoomKind.Merchant => "上に立つと、魂の欠片で1つ買える（全員の取り分から払う）",
            _ => "",
        };
    }
}
