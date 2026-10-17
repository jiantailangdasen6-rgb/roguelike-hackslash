namespace Game.Core
{
    /// <summary>The words of the encounter rooms (RunSimulation.Encounters): what is met, and its two choices.</summary>
    public static class EncounterCatalog
    {
        public static string Name(EncounterKind kind) => kind switch
        {
            EncounterKind.LostSoul => "ワンダラー",
            EncounterKind.Statue => "カースドスタチュー",
            EncounterKind.Gambler => "ギャンブラー",
            _ => "ブラッドファウンテン",
        };

        public static string Intro(EncounterKind kind) => kind switch
        {
            EncounterKind.LostSoul => "傷ついた冒険者がうずくまっている",
            EncounterKind.Statue => "パークを囁く像。台座に血の跡",
            EncounterKind.Gambler => "フードの商人が骰子を振っている",
            _ => "赤く脈打つ泉",
        };

        public static string Choice(EncounterKind kind, int choice) => (kind, choice) switch
        {
            (EncounterKind.LostSoul, 0) => "助ける",
            (EncounterKind.LostSoul, _) => "持ち物を奪う",
            (EncounterKind.Statue, 0) => "祈る",
            (EncounterKind.Statue, _) => "像を壊す",
            (EncounterKind.Gambler, 0) => "賭ける",
            (EncounterKind.Gambler, _) => "断る",
            (_, 0) => "飲む",
            _ => "清める",
        };

        public static string Effect(EncounterKind kind, int choice) => (kind, choice) switch
        {
            (EncounterKind.LostSoul, 0) => "全員のHPが全回復",
            (EncounterKind.LostSoul, _) => "全員に装備1つ・カース1つ",
            (EncounterKind.Statue, 0) => "全員にパーク1つ・最大HP-10%",
            (EncounterKind.Statue, _) => "特性付きのエリートが2体",
            (EncounterKind.Gambler, 0) => "半々：パークの強化2回／HP-25%",
            (EncounterKind.Gambler, _) => $"ソウルシャード +{RunSimulation.DeclineShards}（1人ごと）",
            (_, 0) => "全員にパークの強化1回・カース1つ",
            _ => "全員のカースが1つ消える",
        };
    }
}
