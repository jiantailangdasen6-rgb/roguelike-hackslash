namespace Game.Core
{
    public enum CampUpgrade : byte
    {
        Hearth,     // 炉: max HP
        Forge,      // 鍛冶場: better loaned gear
        Apothecary, // 薬棚: more healing when a room is cleared
        Altar,      // 小祭壇: a power to pick at the start of every run
        Urn,        // 魂の壺: more soul shards from every run
    }

    public sealed class CampUpgradeDef
    {
        public CampUpgrade Id;
        public string Name;
        public int[] Costs; // per level (its length is the max level)
        public System.Func<int, string> Text; // what a level gives
        public int MaxLevel => Costs.Length;
    }

    /// <summary>
    /// Growing the camp (拠点を育てる): soul shards spent at the hearth on facilities that help every run after.
    /// Each device grows its own camp (ProfileData.camp); the levels travel to the host in the Loadout.
    /// </summary>
    public static class CampUpgrades
    {
        public const float HearthHealth = 0.05f;
        public const float ApothecaryHeal = 0.05f;
        public const float UrnShards = 0.1f;

        public static readonly CampUpgradeDef[] All =
        {
            new() { Id = CampUpgrade.Hearth, Name = "炉", Costs = new[] { 60, 140, 260 }, Text = l => $"最大HP +{HearthHealth * 100f * l:0}%" },
            new() { Id = CampUpgrade.Forge, Name = "鍛冶場", Costs = new[] { 120, 300 }, Text = l => l >= 2 ? "空いた枠に貸し出す装備が希少になる" : "空いた枠に貸し出す装備が上質になる" },
            new() { Id = CampUpgrade.Apothecary, Name = "薬棚", Costs = new[] { 80, 180, 320 }, Text = l => $"部屋を制圧したときの回復 +{ApothecaryHeal * 100f * l:0}%（基本20%）" },
            new() { Id = CampUpgrade.Altar, Name = "小祭壇", Costs = new[] { 350 }, Text = _ => "ランの始めに力をひとつ選べる" },
            new() { Id = CampUpgrade.Urn, Name = "魂の壺", Costs = new[] { 100, 220, 400 }, Text = l => $"ランで得る魂の欠片 +{UrnShards * 100f * l:0}%" },
        };

        public static CampUpgradeDef Get(CampUpgrade id) => All[(int)id];

        /// <summary>Level of an upgrade in a list of levels (missing = 0).</summary>
        public static int Level(System.Collections.Generic.IReadOnlyList<int> levels, CampUpgrade id) =>
            levels != null && (int)id < levels.Count ? levels[(int)id] : 0;
    }
}
