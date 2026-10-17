using UnityEngine;

namespace Game.Core
{
    /// <summary>Something wrong with the whole labyrinth this run: a twist with a price and a reward.</summary>
    public enum Omen : byte
    {
        None,
        BloodMoon, // more elites; more souls
        Bounty,    // more gear; sturdier monsters
        Frenzy,    // faster monsters; frailer ones
        Swarm,     // bigger waves; frailer monsters
        Vengeance, // the fallen burst; more souls
        Blessed,   // a power more at the start; sturdier lords
    }

    public static class OmenCatalog
    {
        public static string Name(Omen o) => o switch
        {
            Omen.BloodMoon => "ブラッドムーン",
            Omen.Bounty => "ラビリンスオブプレンティ",
            Omen.Frenzy => "フレンジー",
            Omen.Swarm => "スウォームシーズン",
            Omen.Vengeance => "ヘイトレッド",
            Omen.Blessed => "ブレスドウィンド",
            _ => "",
        };

        /// <summary>What it does, in one line (the banner at the start, the codex).</summary>
        public static string Text(Omen o) => o switch
        {
            Omen.BloodMoon => "エリートが増える／ソウル（経験）が3割増し",
            Omen.Bounty => "装備が落ちやすい／モンスターの体力が増す",
            Omen.Frenzy => "モンスターが素早い／モンスターの体力が減る",
            Omen.Swarm => "群れが大きい／モンスターの体力が減る",
            Omen.Vengeance => "倒れたモンスターがソウルを弾けさせる／ソウル（経験）が増す",
            Omen.Blessed => "始めにパークをもう1つ選べる／ラビリンスのボスが手強い",
            _ => "",
        };

        public const int Count = 7;
    }

    /// <summary>The run's omen (chosen with the route from the seed; tests and the classic route have none).</summary>
    public sealed partial class RunSimulation
    {
        public Omen Omen { get; private set; }

        /// <summary>Screenshots and tests: this run's omen instead of the seed's.</summary>
        public void SetOmen(Omen omen) => Omen = omen;

        private void PickOmen()
        {
            var rng = new GameRandom(Seed ^ 0x0E1E2E3Eul);
            Omen = (Omen)rng.Range(1, OmenCatalog.Count);
        }

        private float OmenElite => Omen == Omen.BloodMoon ? 0.15f : 0f;
        private float OmenXp => Omen == Omen.BloodMoon ? 1.3f : Omen == Omen.Vengeance ? 1.2f : 1f;
        private float OmenLoot => Omen == Omen.Bounty ? 1.5f : 1f;
        private float OmenSpeed => Omen == Omen.Frenzy ? 1.18f : 1f;
        private float OmenWave => Omen == Omen.Swarm ? 1.35f : 1f;
        private bool OmenVengeance => Omen == Omen.Vengeance;

        /// <summary>Health of a monster arriving under this omen (lords and the rest).</summary>
        private float OmenHealth(EnemyDef def) => Omen switch
        {
            Omen.Bounty => def.Boss ? 1f : 1.15f,
            Omen.Frenzy => def.Boss ? 1f : 0.8f,
            Omen.Swarm => def.Boss ? 1f : 0.75f,
            Omen.Blessed => def.Boss ? 1.15f : 1f,
            _ => 1f,
        };

        /// <summary>A soul's worth under this omen.</summary>
        private int OmenSouls(int xp) => Mathf.Max(xp > 0 ? 1 : 0, Mathf.RoundToInt(xp * OmenXp));
    }
}
