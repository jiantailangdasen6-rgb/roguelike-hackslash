using UnityEngine;

namespace Game.Core
{
    /// <summary>Names, descriptions and colours of door rewards (shared by the door visuals, HUD and tests),
    /// the curses of curse doors and the merchant's prices.</summary>
    public static class RewardCatalog
    {
        public static readonly Color TrialColor = new(1f, 0.22f, 0.18f);

        public static string Name(RoomReward r) => r switch
        {
            RoomReward.Boon => "祝福",
            RoomReward.Upgrade => "研磨",
            RoomReward.Vigor => "生命",
            RoomReward.Shards => "魂の欠片",
            RoomReward.Treasure => "宝物",
            RoomReward.Cursed => "呪い",
            RoomReward.Shop => "闇商人",
            _ => "",
        };

        /// <param name="area">Area of the room behind the door (shard amount grows deeper in).</param>
        public static string Describe(RoomReward r, int area, bool trial)
        {
            int times = trial ? 2 : 1;
            return r switch
            {
                RoomReward.Boon => times > 1 ? "新しい力を2回選ぶ" : "新しい力を1つ選ぶ",
                RoomReward.Upgrade => times > 1 ? "持っている力を2回強化" : "持っている力を1段階強化",
                RoomReward.Vigor => $"最大HP +{RunSimulation.VigorPerStack * 100f * times:0}%・全回復",
                RoomReward.Shards => $"魂の欠片 +{RunSimulation.ShardReward(area) * times}",
                RoomReward.Treasure => times > 1 ? "希少以上の装備を2つ" : "希少以上の装備を1つ",
                RoomReward.Cursed => $"新しい力を{CursedBoons}回選ぶ",
                RoomReward.Shop => "魂の欠片で1人1つ買える",
                _ => "",
            };
        }

        /// <summary>The door's label text under its name (a curse door also says what it costs).</summary>
        public static string Describe(Door door, int area)
        {
            string text = Describe(door.Reward, area, door.Trial);
            if (door.Reward == RoomReward.Cursed) text += $"\n<color=#d88cff>代償：{CurseName(door.Curse)}（{CurseText(door.Curse)}）</color>";
            return text;
        }

        public static Color Color(RoomReward r) => r switch
        {
            RoomReward.Boon => new Color(1f, 0.8f, 0.36f),
            RoomReward.Upgrade => new Color(0.55f, 0.85f, 1f),
            RoomReward.Vigor => new Color(0.45f, 1f, 0.55f),
            RoomReward.Shards => new Color(0.78f, 0.58f, 1f),
            RoomReward.Treasure => new Color(1f, 0.65f, 0.25f),
            RoomReward.Cursed => new Color(0.75f, 0.35f, 1f),
            RoomReward.Shop => new Color(0.95f, 0.82f, 0.45f),
            _ => UnityEngine.Color.white,
        };

        /// <summary>Icon frame in the "icons_reward" sheet: rewards in enum order (Boon = 0), the trial skull, then the curse eye and the merchant's pouch.</summary>
        public static int IconIndex(RoomReward r) => r switch
        {
            RoomReward.Cursed => 6,
            RoomReward.Shop => 7,
            _ => Mathf.Max(0, (int)r - 1),
        };
        public const int TrialIconIndex = 5;

        // ------------------------------------------------------------------ Curses

        public const int CursedBoons = 2;
        public const float FrailtyPerStack = 0.2f;
        public const float WearinessPerStack = 0.5f;
        public const float ThirstFactor = 0.5f;
        public const float HuntedPerStack = 0.2f;
        public const int CurseKinds = 4;

        public static string CurseName(CurseKind c) => c switch
        {
            CurseKind.Frailty => "脆さ",
            CurseKind.Weariness => "重い足",
            CurseKind.Thirst => "渇き",
            CurseKind.Hunted => "狙われる",
            _ => "",
        };

        public static string CurseText(CurseKind c) => c switch
        {
            CurseKind.Frailty => $"最大HP −{FrailtyPerStack * 100f:0}%",
            CurseKind.Weariness => $"回避の再使用 +{WearinessPerStack * 100f:0}%",
            CurseKind.Thirst => $"回復量 −{(1f - ThirstFactor) * 100f:0}%",
            CurseKind.Hunted => $"受けるダメージ +{HuntedPerStack * 100f:0}%",
            _ => "",
        };

        /// <summary>Curse stacks for the HUD: 4 bits per curse.</summary>
        public static int PackCurses(int[] stacks)
        {
            int packed = 0;
            for (int i = 0; i < CurseKinds; i++) packed |= Mathf.Min(stacks[i], 15) << (i * 4);
            return packed;
        }

        public static int CurseStack(int packed, CurseKind c) => (packed >> ((int)c * 4)) & 0xF;

        // ------------------------------------------------------------------ The merchant

        /// <summary>Price in soul shards (taken from the team's payout for the run).</summary>
        public static int Price(PerkOption option, int area) =>
            option.IsShopTreasure ? 24 + 8 * area
            : option.IsShopVigor ? 16 + 6 * area
            : 18 + 6 * area;

        /// <summary>Soul shards the team would take home if the run ended now (what the merchant can be paid from).</summary>
        public static int Purse(int roomsCleared, int bossesKilled, int kills, Difficulty difficulty, int bonusShards, int contractRank = 0) =>
            Mathf.Max(0, MetaCatalog.ShardsForRun(roomsCleared, bossesKilled, kills, false, difficulty, contractRank) + bonusShards);
    }
}
