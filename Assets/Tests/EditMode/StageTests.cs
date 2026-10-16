using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>The lords' stages: rage at 60%, last stand at 25%, the hall floor bursting, never during a trial.</summary>
    public class StageTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> each = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                each?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static RunSimulation Hall(Biome land, EnemyType lord)
        {
            var sim = new RunSimulation(73);
            sim.ForcedLord = lord;
            sim.UseVariedRoute(land);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(RunSimulation.RoomsPerArea - 1);
            for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
            Run(sim, 2f);
            Assert.AreEqual(lord, sim.Enemies[Boss(sim)].Type);
            return sim;
        }

        private static int Boss(RunSimulation sim)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Def.Boss) return i;
            return -1;
        }

        private static void SetHp(RunSimulation sim, float f)
        {
            ref var b = ref sim.Enemies[Boss(sim)];
            b.Health = b.MaxHealth * f;
        }

        private static bool Moment(List<SimEvent> events, TrialMoment m) =>
            events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == m);

        private static int Quakes(List<SimEvent> events) =>
            events.Count(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.HallQuake);

        [Test]
        public void Lord_RagesThenMakesItsLastStand_AndTheFloorBursts()
        {
            // The Great Wolf's own trials start at 65% / 30%; stages wait for them, so use a lord whose trial is simple to pass.
            var sim = Hall(Biome.Forge, EnemyType.ForgeColossus);
            Assert.AreEqual(1, sim.BossStage);
            Assert.AreEqual(0, Quakes(Run(sim, 6f)), "no bursts before the rage");

            SetHp(sim, 0.58f);
            var events = Run(sim, 0.5f);
            Assert.IsTrue(Moment(events, TrialMoment.StageRage));
            Assert.AreEqual(2, sim.BossStage);
            events = Run(sim, 8f, s => SetHp(s, 0.58f));
            Assert.GreaterOrEqual(Quakes(events), 2, "the hall floor bursts in the rage");

            SetHp(sim, 0.2f);
            // The colossus overheats at 50%: the last stand waits until that trial is over.
            events = Run(sim, 30f, s => { if (s.ActiveTrial == BossTrial.None) SetHp(s, 0.2f); });
            Assert.IsTrue(Moment(events, TrialMoment.StageLast));
            Assert.AreEqual(3, sim.BossStage);
        }

        [Test]
        public void Stages_WaitForTheTrial_AndNoBurstsDuringIt()
        {
            var sim = Hall(Biome.Crypt, EnemyType.BoneKing);
            SetHp(sim, 0.45f); // the Bone King's ward at 50% comes first
            var events = Run(sim, 1f);
            Assert.AreEqual(BossTrial.BoneWard, sim.ActiveTrial);
            Assert.IsFalse(Moment(events, TrialMoment.StageRage), "no rage while the ward is up");
            events = Run(sim, 6f);
            Assert.AreEqual(0, Quakes(events), "no bursts while a trial runs");

            // Smash the braziers: the ward falls and then the rage comes.
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (sim.Enemies[i].Type == EnemyType.BoneBrazier) sim.Enemies[i].Dead = true;
            events = Run(sim, 2f);
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.StageRage));
        }

        [Test]
        public void Stage_ResetsWhenTheLordFalls()
        {
            var sim = Hall(Biome.Chapel, EnemyType.ChoirMistress);
            SetHp(sim, 0.59f);
            Run(sim, 1f);
            sim.Enemies[Boss(sim)].Dead = true;
            Run(sim, 2f);
            Assert.AreEqual(0, sim.BossStage);
        }
    }
}
