using UnityEngine;

namespace Game.Core
{
    /// <summary>Names, map marks, colours and pedestal texts of the room kinds on an area map.</summary>
    public static class RoomCatalog
    {
        public static string Name(RoomKind k) => k switch
        {
            RoomKind.Start => "エントランス",
            RoomKind.Combat => "バトルルーム",
            RoomKind.Elite => "エリートネスト",
            RoomKind.Shrine => "シュライン",
            RoomKind.Whetstone => "グラインドストーン",
            RoomKind.Spring => "ライフスプリング",
            RoomKind.CurseAltar => "カースオルター",
            RoomKind.Merchant => "ダークマーチャント",
            RoomKind.Campfire => "ボンファイア",
            RoomKind.Boss => "ボスルーム",
            RoomKind.MiniBoss => "チャンピオンルーム",
            RoomKind.Puzzle => "パズルルーム",
            RoomKind.Treasure => "トレジャリー",
            RoomKind.Secret => "シークレットルーム",
            RoomKind.Spirit => "スピリットシュライン",
            RoomKind.Encounter => "イベントルーム",
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
            RoomKind.MiniBoss => "将",
            RoomKind.Puzzle => "仕",
            RoomKind.Treasure => "宝",
            RoomKind.Secret => "隠",
            RoomKind.Spirit => "霊",
            RoomKind.Encounter => "遇",
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
            RoomKind.Treasure or RoomKind.Secret => RoomReward.Treasure,
            RoomKind.Spirit => RoomReward.Boon, // a god's blessing (drawn with the blessing's sunburst)
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
            RoomKind.MiniBoss => new Color(1f, 0.45f, 0.15f),
            RoomKind.Puzzle => new Color(0.5f, 0.85f, 1f),
            RoomKind.Spirit => new Color(0.62f, 0.92f, 1f),
            RoomKind.Encounter => new Color(0.95f, 0.78f, 0.55f),
            _ => RewardCatalog.Color(Reward(k)),
        };

        /// <summary>What standing on the pedestal does (shown under it and on the HUD).</summary>
        /// <summary>A few words for the label on the pedestal (the codex has the rest).</summary>
        public static string ShortText(RoomKind k) => k switch
        {
            RoomKind.Shrine => "パークをひとつ授かる",
            RoomKind.Whetstone => "パークをひとつ研ぐ",
            RoomKind.Spring => $"最大HP +{RunSimulation.VigorPerStack * 100f:0}%・全回復",
            RoomKind.CurseAltar => "パークを2つ　代償にカース",
            RoomKind.Merchant => "ソウルシャードで買う",
            RoomKind.Treasure => "レアな装備",
            RoomKind.Secret => "オールドマップと装備",
            RoomKind.Spirit => "最大HPの1割でブレッシング",
            _ => "",
        };

        public static string PedestalText(RoomKind k) => k switch
        {
            RoomKind.Shrine => "上に立つと、新しいパークを1つ選べる",
            RoomKind.Whetstone => "上に立つと、持っているパークを1つ強化できる",
            RoomKind.Spring => $"上に立つと、最大HP +{RunSimulation.VigorPerStack * 100f:0}%・全回復",
            RoomKind.CurseAltar => $"上に立つと、パークを{RewardCatalog.CursedBoons}回選べる。代償にカースが1つつく（ランの間ずっと）",
            RoomKind.Merchant => "上に立つと、ソウルシャードで1つ買える（全員の取り分から払う）",
            RoomKind.Treasure => "上に立つと、レア以上の装備を1つ得る",
            RoomKind.Secret => "上に立つと、オールドマップ（このエリアの全部屋が見える）と装備を得る",
            RoomKind.Spirit => "上に立つと、最大HPの1割を捧げて（ランの間ずっと）神のブレッシングを1つ授かる",
            _ => "",
        };
    }
}
