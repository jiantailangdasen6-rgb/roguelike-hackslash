using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Each land's second lord and its trial.</summary>
    public class Lords2Tests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static (RunSimulation, SimPlayer) Hall(Biome land, EnemyType lord)
        {
            var sim = new RunSimulation(57);
            sim.ForcedLord = lord;
            sim.UseVariedRoute(land);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(RunSimulation.RoomsPerArea - 1);
            for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
            Run(sim, 2f);
            Assert.AreEqual(lord, sim.Enemies[Boss(sim)].Type);
            return (sim, p);
        }

        private static int Boss(RunSimulation sim)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Def.Boss) return i;
            return -1;
        }

        private static List<int> Living(RunSimulation sim, EnemyType type)
        {
            var list = new List<int>();
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Type == type) list.Add(i);
            return list;
        }

        private static void SetHp(RunSimulation sim, float f)
        {
            ref var b = ref sim.Enemies[Boss(sim)];
            b.Health = b.MaxHealth * f;
        }

        private static bool Moment(List<SimEvent> events, TrialMoment m) =>
            events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == m);

        [Test]
        public void EveryLand_OffersTwoLords_AndRunsMeetBoth()
        {
            foreach (var land in BiomeCatalog.Routable)
            {
                Assert.AreEqual(2, land.Lords.Length, land.Name);
                Assert.AreEqual(land.Id, BiomeCatalog.OfLord(land.AltLord.Value));
            }
            var seen = new HashSet<EnemyType>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = new RunSimulation(seed);
                sim.UseVariedRoute(Biome.Crypt);
                seen.Add(sim.AreaBoss);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { EnemyType.BoneKing, EnemyType.GraveReaper }), "either crypt lord turns up");
        }

        [Test]
        public void GraveReaper_HidesInACoffin_TheRightOneBringsHimOut()
        {
            var (sim, p) = Hall(Biome.Crypt, EnemyType.GraveReaper);
            SetHp(sim, 0.59f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Coffins, sim.ActiveTrial);
            var coffins = Living(sim, EnemyType.Coffin);
            Assert.AreEqual(4, coffins.Count);
            Vector2 at = sim.Enemies[Boss(sim)].Position;
            int right = coffins.First(i => (sim.Enemies[i].Position - at).sqrMagnitude < 0.01f);
            int wrong = coffins.First(i => i != right);
            int pending = sim.PendingSpawns.Count;
            sim.DamageEnemy(wrong, 1e6f, p, HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.1f);
            Assert.Greater(sim.PendingSpawns.Count, pending, "ghouls climb out of an empty coffin");
            right = Living(sim, EnemyType.Coffin).First(i => (sim.Enemies[i].Position - sim.Enemies[Boss(sim)].Position).sqrMagnitude < 0.01f);
            sim.DamageEnemy(right, 1e6f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.2f);
            Assert.IsTrue(Moment(events, TrialMoment.ReaperFound));
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f);
        }

        [Test]
        public void ChoirMistress_BeamsBurn_AndOutlastingThemExposesHer()
        {
            var (sim, p) = Hall(Biome.Chapel, EnemyType.ChoirMistress);
            SetHp(sim, 0.65f);
            var events = Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Beams, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.BeamsStart));
            events = Run(sim, RunSimulation.BeamTime + 0.5f);
            Assert.IsTrue(Moment(events, TrialMoment.BeamsEnd));
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
        }

        [Test]
        public void VoidEye_GazesFourTimes_ThenReels()
        {
            var (sim, _) = Hall(Biome.Abyss, EnemyType.VoidEye);
            SetHp(sim, 0.69f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Gaze, sim.ActiveTrial);
            var events = Run(sim, 14f);
            Assert.AreEqual(4, events.Count(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == TrialMoment.GazeFire));
            Assert.IsTrue(Moment(events, TrialMoment.GazeEnd));
        }

        [Test]
        public void GreatCrab_Shell_CracksOnlyToAKeg()
        {
            var (sim, p) = Hall(Biome.Sunken, EnemyType.GreatCrab);
            SetHp(sim, 0.59f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Shell, sim.ActiveTrial);
            float before = sim.Enemies[Boss(sim)].Health;
            sim.DamageEnemy(Boss(sim), 100f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(100f * RunSimulation.ShellArmor, before - sim.Enemies[Boss(sim)].Health, 1f, "shut in its shell");
            int keg = Living(sim, EnemyType.PowderKeg).First();
            sim.Enemies[Boss(sim)].Position = sim.Enemies[keg].Position + new Vector2(1f, 0f);
            sim.DamageEnemy(keg, 99f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 1.2f);
            Assert.IsTrue(Moment(events, TrialMoment.ShellCracked));
        }

        [Test]
        public void GreatWolf_Hunts_ChargesTheMarked()
        {
            var (sim, _) = Hall(Biome.Frost, EnemyType.GreatWolf);
            SetHp(sim, 0.64f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Hunt, sim.ActiveTrial);
            var events = Run(sim, 30f);
            Assert.IsTrue(Moment(events, TrialMoment.HuntMark));
            Assert.IsTrue(Moment(events, TrialMoment.HuntCrash) || Moment(events, TrialMoment.HuntOver), "the hunt ends");
        }

        [Test]
        public void Executioner_Rows_EndWhenThePostsBreak()
        {
            var (sim, p) = Hall(Biome.Forge, EnemyType.Executioner);
            SetHp(sim, 0.49f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Rows, sim.ActiveTrial);
            var events = Run(sim, 2f);
            Assert.That(events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.Id < 0), "rows of blades fall");
            foreach (int i in Living(sim, EnemyType.ChainPost)) sim.DamageEnemy(i, 1e6f, p, HitFlags.None, Vector2.zero, 0f);
            events = Run(sim, 0.2f);
            Assert.IsTrue(Moment(events, TrialMoment.RowsBroken));
        }
    }
}
