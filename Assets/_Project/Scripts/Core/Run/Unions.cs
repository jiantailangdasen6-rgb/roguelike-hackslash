namespace Game.Core
{
    /// <summary>
    /// Union techniques (合体技): two players using a skill close together at the same moment. Every pair of classes
    /// (the same class twice too) has its own name; what it does is both classes' touch at once (RunSimulation.Unions).
    /// </summary>
    public static class UnionCatalog
    {
        public const int ClassCount = 8;
        public const int PairCount = ClassCount * (ClassCount + 1) / 2;

        /// <summary>Unordered pair of classes → 0..35.</summary>
        public static int Pair(ClassId a, ClassId b)
        {
            int i = (int)a, j = (int)b;
            if (i > j) (i, j) = (j, i);
            return j * (j + 1) / 2 + i;
        }

        public static (ClassId A, ClassId B) Classes(int pair)
        {
            int j = 0;
            while ((j + 1) * (j + 2) / 2 <= pair) j++;
            return ((ClassId)(pair - j * (j + 1) / 2), (ClassId)j);
        }

        public static string Name(int pair) => pair >= 0 && pair < Names.Length ? Names[pair] : "合体技";

        // Indexed by Pair(): for j = 0..7, i = 0..j (warrior, mage, ranger, guardian, assassin, alchemist, deathcaller, paladin).
        private static readonly string[] Names =
        {
            "双刃の嵐",                                                                  // W+W
            "氷砕きの大剣", "双星の氷華",                                                  // W+M, M+M
            "狩りの号令", "凍てつく星の矢", "千本の矢雨",                                  // +R
            "不落の突撃", "氷の聖壁", "盾越しの一斉射", "鉄壁の双盾",                      // +G
            "影と鋼の十字", "霜の暗殺", "必殺の印", "盾影の罠", "影縫いの双閃",            // +A
            "爆炎の一閃", "蒸気の大爆発", "火矢の雨", "炎の防壁", "毒煙の刃", "連鎖爆薬",  // +Al
            "骸の軍勢", "冥氷の棺", "呪われた矢", "骨の砦", "死の影", "腐敗の霧", "冥府の大行進", // +D
            "聖剣の大斬撃", "天の氷光", "光の矢", "聖域の守り", "裁きの刃", "浄化の炎", "魂の救済", "双光の審判", // +P
        };
    }
}
