using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Contracts: the rank, what it changes in a run, the reward and the titles.</summary>
    public class ContractTests
    {
        private static int Sign(params (int Clause, int Level)[] clauses)
        {
            int contract = 0;
            foreach (var (c, l) in clauses) contract = ContractCatalog.WithLevel(contract, c, l);
            return contract;
        }

        [Test]
        public void Rank_AddsUpTheClauses_AndHandicapsTheDifficultyHasCountNothing()
        {
            var normal = DifficultyCatalog.Get(Difficulty.Normal);
            int contract = Sign((0, 2), (5, 1)); // sharp fangs 2, swiftness
            Assert.AreEqual(2 + 2, ContractCatalog.Rank(contract, normal));
            Assert.AreEqual(2, ContractCatalog.Level(contract, 0));
            Assert.AreEqual(0, ContractCatalog.Level(Sign((0, 9)), 1), "levels stay in their own bits");
            Assert.AreEqual(3, ContractCatalog.Level(Sign((0, 9)), 0), "clamped to the clause's top");
            var hard = DifficultyCatalog.Get(Difficulty.Hard);
            foreach (var c in ContractCatalog.All)
                if (c.Mod != DifficultyMod.None && hard.Has(c.Mod))
                    Assert.AreEqual(0, ContractCatalog.Rank(Sign((c.Index, 1)), hard), $"{c.Name} is part of hard already");
            Assert.Greater(ContractCatalog.MaxRank, 20, "rank 20 can be reached");
        }

        [Test]
        public void ARunUnderContract_IsHarder_AndPaysMore()
        {
            int contract = Sign((0, 3), (1, 3), (5, 1));
            var plain = new RunSimulation(5);
            var signed = new RunSimulation(5, Difficulty.Normal, contract);
            plain.AddPlayer(0);
            signed.AddPlayer(0);
            plain.Start();
            signed.Start();
            Assert.AreEqual(8, signed.ContractRank);
            Assert.Greater(signed.EnemyHealthScale, plain.EnemyHealthScale * 1.4f);
            Assert.Greater(signed.EnemyDamageScale, plain.EnemyDamageScale * 1.4f);
            Assert.IsTrue(signed.DifficultyDef.Has(DifficultyMod.Swiftness));
            Assert.IsFalse(plain.DifficultyDef.Has(DifficultyMod.Swiftness));
            Assert.Greater(MetaCatalog.ShardsForRun(5, 1, 80, true, Difficulty.Normal, 8), MetaCatalog.ShardsForRun(5, 1, 80, true, Difficulty.Normal));
        }

        [Test]
        public void Titles_ComeWithAWinUnderAHighEnoughRank()
        {
            var pact10 = AchievementCatalog.Get("a_pact10");
            Assert.IsNotNull(pact10);
            var won8 = new SimEvent(SimEventType.RunEnded) { Int = 1, Id = 8 };
            var won12 = new SimEvent(SimEventType.RunEnded) { Int = 1, Id = 12 };
            var lost12 = new SimEvent(SimEventType.RunEnded) { Int = 0, Id = 12 };
            Assert.AreEqual(0, QuestCatalog.Amount(pact10, won8));
            Assert.AreEqual(1, QuestCatalog.Amount(pact10, won12));
            Assert.AreEqual(0, QuestCatalog.Amount(pact10, lost12));
            Assert.AreEqual(1, pact10.Target);
        }
    }
}
