namespace Game.Core
{
    public enum Difficulty : byte
    {
        Easy,
        Normal,
        Hard,
        Nightmare,
    }

    /// <summary>Chosen by the host before a run. Scales the monsters and the soul-shard reward.</summary>
    public sealed class DifficultyDef
    {
        public Difficulty Id;
        public string Name;
        public string Text;
        public float EnemyHealth;
        public float EnemyDamage;
        public float EnemyCount;
        public float EliteChance;
        public float Shards;
        /// <summary>Needs a victory on this difficulty first (null = always open).</summary>
        public Difficulty? UnlockedBy;
    }

    public static class DifficultyCatalog
    {
        public static readonly DifficultyDef[] All =
        {
            new() { Id = Difficulty.Easy, Name = "見習い", Text = "魔物が弱く数も少ない。まずは迷宮に慣れたい人に", EnemyHealth = 0.7f, EnemyDamage = 0.6f, EnemyCount = 0.85f, EliteChance = 0.03f, Shards = 0.7f },
            new() { Id = Difficulty.Normal, Name = "標準", Text = "本来の迷宮", EnemyHealth = 1f, EnemyDamage = 1f, EnemyCount = 1f, EliteChance = 0.05f, Shards = 1f },
            new() { Id = Difficulty.Hard, Name = "熟練", Text = "魔物が硬く、痛く、多い。魂の欠片 1.5倍", EnemyHealth = 1.35f, EnemyDamage = 1.35f, EnemyCount = 1.15f, EliteChance = 0.08f, Shards = 1.5f },
            new() { Id = Difficulty.Nightmare, Name = "冥府", Text = "生きて帰った者はいない。魂の欠片 2.2倍（熟練を踏破すると解放）", EnemyHealth = 1.8f, EnemyDamage = 1.7f, EnemyCount = 1.3f, EliteChance = 0.12f, Shards = 2.2f, UnlockedBy = Difficulty.Hard },
        };

        public static DifficultyDef Get(Difficulty d) => All[(int)d < All.Length ? (int)d : 1];
    }
}
