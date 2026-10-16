namespace Game.Core
{
    /// <summary>What an elite can carry (RunSimulation.Affixes). Flags: an elite has one to three.</summary>
    [System.Flags]
    public enum EliteAffix : byte
    {
        None = 0,
        Swift = 1,         // 俊足
        Regenerating = 2,  // 再生
        Volatile = 4,      // 爆発
        Splitting = 8,     // 分裂
        Summoner = 16,     // 召喚
        Shielded = 32,     // 守護
    }

    public static class EliteAffixCatalog
    {
        public static readonly EliteAffix[] All =
        {
            EliteAffix.Swift, EliteAffix.Regenerating, EliteAffix.Volatile, EliteAffix.Splitting, EliteAffix.Summoner, EliteAffix.Shielded,
        };

        public static string Name(EliteAffix a) => a switch
        {
            EliteAffix.Swift => "俊足",
            EliteAffix.Regenerating => "再生",
            EliteAffix.Volatile => "爆発",
            EliteAffix.Splitting => "分裂",
            EliteAffix.Summoner => "召喚",
            EliteAffix.Shielded => "守護",
            _ => "",
        };

        public static string Description(EliteAffix a) => a switch
        {
            EliteAffix.Swift => "動きも攻撃も速い",
            EliteAffix.Regenerating => "攻撃されない間、体力が戻っていく",
            EliteAffix.Volatile => "倒れたあと、その場が爆発する（円の予兆）",
            EliteAffix.Splitting => "倒れると小さな2体に分かれる",
            EliteAffix.Summoner => "ときどき仲間の魔物を呼ぶ（2体まで）",
            EliteAffix.Shielded => "体力の25%ぶんの守りの膜。しばらく攻撃されないと戻る",
            _ => "",
        };

        /// <summary>"俊足・分裂" for the label over the elite.</summary>
        public static string Label(EliteAffix bits)
        {
            string s = "";
            foreach (var a in All)
                if ((bits & a) != 0) s += (s.Length > 0 ? "・" : "") + Name(a);
            return s;
        }
    }
}
