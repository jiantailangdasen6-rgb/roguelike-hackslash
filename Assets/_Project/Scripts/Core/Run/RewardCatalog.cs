using UnityEngine;

namespace Game.Core
{
    /// <summary>Names, descriptions and colours of door rewards (shared by the door visuals, HUD and tests).</summary>
    public static class RewardCatalog
    {
        public static readonly Color TrialColor = new(1f, 0.22f, 0.18f);

        public static string Name(RoomReward r) => r switch
        {
            RoomReward.Boon => "祝福",
            RoomReward.Upgrade => "研磨",
            RoomReward.Vigor => "生命",
            RoomReward.Shards => "魂の欠片",
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
                _ => "",
            };
        }

        public static Color Color(RoomReward r) => r switch
        {
            RoomReward.Boon => new Color(1f, 0.8f, 0.36f),
            RoomReward.Upgrade => new Color(0.55f, 0.85f, 1f),
            RoomReward.Vigor => new Color(0.45f, 1f, 0.55f),
            RoomReward.Shards => new Color(0.78f, 0.58f, 1f),
            _ => UnityEngine.Color.white,
        };

        /// <summary>Icon frame in the "icons_reward" sheet: rewards in enum order (Boon = 0), then the trial skull.</summary>
        public static int IconIndex(RoomReward r) => Mathf.Max(0, (int)r - 1);
        public const int TrialIconIndex = 4;
    }
}
