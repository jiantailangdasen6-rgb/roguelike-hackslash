namespace Game.Core
{
    /// <summary>
    /// Reward numbers shared by the pedestal rooms (see RunSimulation.Dungeon): the spring's max HP, the blessing
    /// perk's shield, and soul shards earned on top of the run's payout.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float VigorPerStack = 0.1f;
        public const float BlessingShield = 20f;

        /// <summary>Soul shards added to the run's payout (and spent at the merchant).</summary>
        public int BonusShards { get; private set; }

        public static int ShardReward(int area) => 8 + 4 * area;
    }
}
