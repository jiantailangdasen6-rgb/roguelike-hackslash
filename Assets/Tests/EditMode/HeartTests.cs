using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The story's end: the way to the heart, the Ashen King's trials and the true ending.</summary>
    public class HeartTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> eachTick = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                eachTick?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static void AnswerPicks(RunSimulation sim)
        {
            foreach (var p in sim.Players)
                for (int i = 0; i < 10 && p.PendingOffer != null && p.OfferKind != ChoiceKind.Shop; i++) sim.ChoosePerk(p.ClientId, 0);
        }

        private static int Boss(RunSimulation sim)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Def.Boss) return i;
            return -1;
        }

        private static void Kill(RunSimulation sim, System.Predicate<Enemy> which, SimPlayer by)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && which(sim.Enemies[i])) sim.DamageEnemy(i, 1e7f, by, HitFlags.None, Vector2.zero, 0f);
        }

        [Test]
        public void TheHeart_IsNeverOnAnOrdinaryRoute()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = new RunSimulation(seed);
                sim.UseVariedRoute();
                Assert.AreNotEqual(Biome.Heart, sim.Biome);
                Assert.AreNotEqual(Biome.Heart, sim.WayLand(0));
                Assert.AreNotEqual(Biome.Heart, sim.WayLand(1));
            }
        }

        [Test]
        public void SixMemories_OpenTheMiddleWay_ToTheHeartAndTheAshenKing()
        {
            var sim = new RunSimulation(31);
            sim.UseVariedRoute(Biome.Crypt, Biome.Chapel, Biome.Abyss);
            sim.HeartOpen = true;
            var a = sim.AddPlayer(0);
            sim.Start(RunSimulation.TotalRooms - 1); // the third lord's hall
            AnswerPicks(sim);
            Run(sim, 2f);
            for (int round = 0; round < 4 && !sim.ExitOpen; round++)
            {
                Kill(sim, e => e.Def.Boss || e.Type == EnemyType.SoulVessel, a);
                Run(sim, 0.3f);
            }
            Assert.IsTrue(sim.ExitOpen);
            Assert.IsTrue(sim.OffersHeart(sim.Area));
            Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => { if (s.Area == RunSimulation.AreaCount - 1) a.Position = s.HeartPosition; });
            Assert.AreEqual(Biome.Heart, sim.Biome);
            Assert.AreEqual(EnemyType.AshenKing, sim.AreaBoss);
            Assert.LessOrEqual(sim.Map.Rooms.Count, 10, "the heart is a short land");
        }

        [Test]
        public void AshenKing_GoesThroughTheLordsTrials_AndHisFallLightsTheHeart()
        {
            var sim = new RunSimulation(31);
            sim.UseVariedRoute(Biome.Crypt, Biome.Chapel, Biome.Abyss);
            sim.HeartOpen = true;
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            a.Stats.SetBase(StatType.MaxHealth, 100000f); // the test watches the king, not the hero
            a.Hp = 100000f;
            sim.Start(RunSimulation.TotalRooms - 1);
            AnswerPicks(sim);
            Run(sim, 2f);
            for (int round = 0; round < 4 && !sim.ExitOpen; round++)
            {
                Kill(sim, e => e.Def.Boss || e.Type == EnemyType.SoulVessel, a);
                Run(sim, 0.3f);
            }
            Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => { if (s.Area == RunSimulation.AreaCount - 1) a.Position = s.HeartPosition; });
            sim.MoveTeamTo(sim.Map.Boss); // straight to the heart's lord
            AnswerPicks(sim);
            Run(sim, 2.5f);
            AnswerPicks(sim);
            int b = Boss(sim);
            Assert.GreaterOrEqual(b, 0);
            Assert.AreEqual(EnemyType.AshenKing, sim.Enemies[b].Type);

            void SetHp(float f) { ref var e = ref sim.Enemies[Boss(sim)]; e.Health = e.MaxHealth * f; }
            SetHp(0.74f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.BoneWard, sim.ActiveTrial, "first the Bone King's ward");
            Kill(sim, e => e.Type == EnemyType.BoneBrazier, a);
            Run(sim, 0.2f);
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);

            SetHp(0.49f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.BloodRitual, sim.ActiveTrial, "then the bishop's ritual");
            Run(sim, 20f);
            SetHp(0.24f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Collapse, sim.ActiveTrial, "then the abyss's falling floor");
            Run(sim, RunSimulation.CollapseWarnTime + 0.5f);

            // His last breath goes into the ashen heart; break it and he falls for good.
            var events = new List<SimEvent>();
            for (int round = 0; round < 4 && sim.Phase == RunPhase.Playing; round++)
            {
                Kill(sim, e => e.Def.Boss || e.Type == EnemyType.SoulVessel, a);
                events.AddRange(Run(sim, 0.3f));
            }
            Assert.AreEqual(RunPhase.Victory, sim.Phase);
            Assert.IsTrue(sim.TrueEnding);
            Assert.That(events.Any(e => e.Type == SimEventType.RunEnded && e.Int == 3), "the true ending");
        }

        [Test]
        public void Lore_HasAnInscriptionAndAMemoryForEveryLand_AndCodexEntries()
        {
            foreach (var land in BiomeCatalog.Routable)
            {
                Assert.IsNotNull(Lore.Get(Lore.InscriptionId(land.Id)), land.Name);
                Assert.IsNotNull(Lore.Get(Lore.MemoryId(land.Id)), land.Name);
                Assert.IsNotNull(CodexCatalog.Get(Lore.MemoryId(land.Id)));
                Assert.AreNotEqual(0, Lore.MemoryBit(land.Id));
            }
            Assert.AreEqual(Lore.AllMemories, BiomeCatalog.Routable.Aggregate(0, (m, l) => m | Lore.MemoryBit(l.Id)));
        }
    }
}
