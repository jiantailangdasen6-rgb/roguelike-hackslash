using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Contracts (契約): clauses the host signs at the camp's tablet on top of the difficulty. Each clause has a
    /// level (2 bits each in <see cref="ProfileData.contract"/> and the run's contract word) and is worth points;
    /// the sum is the contract's rank (段位). The rank makes the labyrinth harder and the soul shards richer, and
    /// the best rank won is kept (titles at ranks 5, 10 and 20).
    /// </summary>
    public sealed class ContractClause
    {
        public int Index;
        public string Name;
        public string Text;      // what one level does
        public int MaxLevel;
        public int PointsPerLevel;
        public float Health;     // per level, added to the monsters' health factor
        public float Damage;
        public float Count;
        public float Elite;
        public DifficultyMod Mod; // a whole difficulty handicap (one level)
    }

    public static class ContractCatalog
    {
        public const float ShardsPerRank = 0.06f;

        public static readonly ContractClause[] All =
        {
            new() { Index = 0, Name = "鋭い牙", Text = "魔物の攻撃 +15%", MaxLevel = 3, PointsPerLevel = 1, Damage = 0.15f },
            new() { Index = 1, Name = "厚い皮", Text = "魔物の体力 +15%", MaxLevel = 3, PointsPerLevel = 1, Health = 0.15f },
            new() { Index = 2, Name = "大群", Text = "魔物の数 +12%", MaxLevel = 2, PointsPerLevel = 1, Count = 0.12f },
            new() { Index = 3, Name = "精鋭の道", Text = "精鋭が出やすい（+4%）", MaxLevel = 2, PointsPerLevel = 1, Elite = 0.04f },
            new() { Index = 4, Name = "獰猛", Text = "予兆が短い", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.Ferocity },
            new() { Index = 5, Name = "俊敏", Text = "魔物の足が速い", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.Swiftness },
            new() { Index = 6, Name = "怨念", Text = "倒れた魔物が弾ける", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.Vengeance },
            new() { Index = 7, Name = "鉄の守り", Text = "精鋭が鎧をまとう", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.IronGuard },
            new() { Index = 8, Name = "主の怒り", Text = "主の試練が厳しい", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.LordsWrath },
            new() { Index = 9, Name = "渇き", Text = "回復が弱い", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.Thirst },
            new() { Index = 10, Name = "衰え", Text = "最大HPが減る", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.Frailty },
            new() { Index = 11, Name = "疲れ", Text = "回避の戻りが遅い", MaxLevel = 1, PointsPerLevel = 2, Mod = DifficultyMod.Weariness },
        };

        public static int Level(int contract, int clause) => (contract >> (clause * 2)) & 3;

        public static int WithLevel(int contract, int clause, int level)
        {
            level = System.Math.Clamp(level, 0, All[clause].MaxLevel);
            return (contract & ~(3 << (clause * 2))) | (level << (clause * 2));
        }

        /// <summary>The contract's rank on top of a difficulty (a handicap the difficulty already has is worth nothing).</summary>
        public static int Rank(int contract, DifficultyDef difficulty)
        {
            int rank = 0;
            foreach (var c in All)
            {
                int level = Level(contract, c.Index);
                if (level == 0 || (c.Mod != DifficultyMod.None && difficulty.Has(c.Mod))) continue;
                rank += level * c.PointsPerLevel;
            }
            return rank;
        }

        /// <summary>The difficulty as the contract changes it (the run uses this one).</summary>
        public static DifficultyDef Apply(DifficultyDef d, int contract)
        {
            if (contract == 0) return d;
            var result = new DifficultyDef
            {
                Id = d.Id, Name = d.Name, Text = d.Text, EnemyHealth = d.EnemyHealth, EnemyDamage = d.EnemyDamage, EnemyCount = d.EnemyCount,
                EliteChance = d.EliteChance, Shards = d.Shards, LootBoost = d.LootBoost, Mods = d.Mods, UnlockedBy = d.UnlockedBy,
            };
            foreach (var c in All)
            {
                int level = Level(contract, c.Index);
                if (level == 0) continue;
                result.EnemyHealth *= 1f + c.Health * level;
                result.EnemyDamage *= 1f + c.Damage * level;
                result.EnemyCount *= 1f + c.Count * level;
                result.EliteChance += c.Elite * level;
                result.Mods |= c.Mod;
            }
            result.Shards *= 1f + ShardsPerRank * Rank(contract, d);
            return result;
        }

        public static int MaxRank
        {
            get
            {
                int n = 0;
                foreach (var c in All) n += c.MaxLevel * c.PointsPerLevel;
                return n;
            }
        }

        /// <summary>"鋭い牙2・俊敏" for the HUD and the result.</summary>
        public static string Summary(int contract)
        {
            var parts = new List<string>();
            foreach (var c in All)
            {
                int level = Level(contract, c.Index);
                if (level > 0) parts.Add(c.MaxLevel > 1 ? $"{c.Name}{level}" : c.Name);
            }
            return string.Join("・", parts);
        }
    }
}
