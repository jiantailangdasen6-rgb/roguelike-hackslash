using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Past the Lich: home (victory) or the deep layers, where the lords come back stronger.</summary>
    public class DeepTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> eachTick = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                eachTick?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
                foreach (var p in sim.Players) if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
            }
            return events;
        }

        /// <summary>The Lich's hall, the Lich slain.</summary>
        private static (RunSimulation, SimPlayer) LichDown()
        {
            var sim = new RunSimulation(12);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(RunSimulation.TotalRooms - 1);
            sim.BossStartHealth = 0.001f;
            Run(sim, 3f, s => { p.Hp = p.MaxHp; p.InvulnerableTimer = 1f; });
            // The Lich rises from his phylactery: break it and strike again.
            for (int round = 0; round < 4 && !sim.Conquered; round++)
            {
                for (int i = 0; i < sim.Enemies.Count; i++)
                    if (!sim.Enemies[i].Dead && (sim.Enemies[i].Def.Boss || sim.Enemies[i].Type == EnemyType.SoulVessel))
                        sim.DamageEnemy(i, 1e7f, p, HitFlags.DamageOverTime, Vector2.zero, 0f);
                Run(sim, 0.3f, s => { p.Hp = p.MaxHp; p.InvulnerableTimer = 1f; });
            }
            return (sim, p);
        }

        [Test]
        public void AfterTheLich_TheRunGoesOn_AndTheWayHomeEndsItInVictory()
        {
            var (sim, p) = LichDown();
            Assert.AreEqual(RunPhase.Playing, sim.Phase, "no instant victory: a choice");
            Assert.IsTrue(sim.Conquered);
            Assert.IsTrue(sim.ExitOpen);
            var events = Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => p.Position = s.HomePosition);
            Assert.AreEqual(RunPhase.Victory, sim.Phase);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.RunEnded && e.Int == 1));
        }

        [Test]
        public void TheWayDeeper_LeadsToAStrongerArea_WithALordAgain_AndFallingThereStillCountsAsConquest()
        {
            var (sim, p) = LichDown();
            Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => p.Position = s.ExitPosition);
            Assert.AreEqual(RunSimulation.AreaCount, sim.Area, "the first deep layer");
            Assert.AreEqual(1, sim.DeepLayer);
            Assert.AreEqual(EnemyType.BoneKing, sim.AreaBoss, "the lords take turns");
            Assert.Greater(sim.Map.Boss.Index, RunSimulation.TotalRooms - 1, "stronger than the Lich's hall");

            sim.Players.ForEach(x => x.InvulnerableTimer = 0f);
            sim.HurtPlayer(p, 999999f);
            var events = Run(sim, 0.2f);
            Assert.AreEqual(RunPhase.Defeat, sim.Phase);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.RunEnded && e.Int == 2), "fell after conquering");
        }
    }
}
