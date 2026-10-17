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

        public static string Name(int pair) => pair >= 0 && pair < Names.Length ? Names[pair] : "ユニオン";

        // Indexed by Pair(): for j = 0..7, i = 0..j (warrior, mage, ranger, guardian, assassin, alchemist, deathcaller, paladin).
        private static readonly string[] Names =
        {
            "ダブルテンペスト",                                                                  // W+W
            "アイスブレイカー", "ツインスターブルーム",                                                  // W+M, M+M
            "ハンティングコール", "フロストスターアロー", "サウザンドアロー",                                  // +R
            "アンストッパブルチャージ", "フロストバスティオン", "シールドボレー", "ツインシールド",                      // +G
            "シャドウスチールクロス", "フロストアサシネイト", "デスマーク", "シャドウトラップ", "シャドウステッチ",            // +A
            "ブラストスラッシュ", "スチームエクスプロージョン", "ファイアアローレイン", "フレイムウォール", "ヴェノムスモーク", "チェインボム",  // +Al
            "デッドレギオン", "ネザーアイスコフィン", "カースドアロー", "ボーンフォート", "デスシャドウ", "ロットミスト", "ネザーパレード", // +D
            "ホーリーグランドスラッシュ", "セレスティアルフロスト", "ライトアロー", "サンクチュアリガード", "ジャッジメントブレード", "ピュリファイフレイム", "ソウルサルベーション", "ツインライトジャッジメント", // +P
        };
    }
}
