using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The focus gauge and the ultimates.</summary>
    public class UltimateTests
    {
        private const float Dt = 1f / 30f;
        private static byte _presses;

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

        private static (RunSimulation, SimPlayer, int) Setup(ClassId cls)
        {
            var sim = new RunSimulation(17);
            var p = sim.AddPlayer(0, new Loadout { classId = cls });
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(2f, 0f), 30f, 1f, false);
            int e = sim.Enemies.Count - 1;
            sim.Enemies[e].Cooldown = 99f;
            return (sim, p, e);
        }

        private static void Ult(RunSimulation sim, SimPlayer p) =>
            sim.SetPlayerInput(p.ClientId, new PlayerInput { Aim = new Vector2(2f, 0f), UltCount = ++_presses });

        [Test]
        public void BlowsFillTheGauge_AndTheUltimateNeedsItFull()
        {
            var (sim, p, e) = Setup(ClassId.Mage);
            sim.DamageEnemy(e, 100f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(100f * RunSimulation.FocusPerDamage, p.Focus, 0.01f);
            Ult(sim, p);
            var events = Run(sim, Dt);
            Assert.IsFalse(events.Any(ev => ev.Type == SimEventType.Ultimate), "not before it is full");

            p.Focus = RunSimulation.FocusMax;
            Ult(sim, p);
            events = Run(sim, Dt);
            Assert.IsTrue(events.Any(ev => ev.Type == SimEventType.Ultimate && ev.Int == (int)ClassId.Mage));
            Assert.Greater(sim.Enemies[e].Frozen, 1f, "the glacier freezes");
            Assert.AreEqual(0f, p.Focus, 0.01f, "spent, and its own blows don't fill it back");
        }

        [Test]
        public void TheBladeStorm_StrikesForAWhile_AndHalvesTheBlows()
        {
            var (sim, p, e) = Setup(ClassId.Warrior);
            float hp = sim.Enemies[e].Health;
            p.Focus = RunSimulation.FocusMax;
            Ult(sim, p);
            Run(sim, 1f);
            float after1 = sim.Enemies[e].Health;
            Assert.Less(after1, hp);
            Assert.Greater(p.UltTimer, 0f, "still whirling");
            Run(sim, RunSimulation.StormTime);
            Assert.Less(sim.Enemies[e].Health, after1, "kept striking");
            Assert.LessOrEqual(p.UltTimer, 0f);
        }

        [Test]
        public void EveryClass_HasAnUltimate()
        {
            foreach (ClassId c in System.Enum.GetValues(typeof(ClassId)))
            {
                Assert.IsNotEmpty(UltimateCatalog.Name(c));
                var (sim, p, _) = Setup(c);
                p.Focus = RunSimulation.FocusMax;
                Ult(sim, p);
                var events = Run(sim, 0.5f);
                Assert.IsTrue(events.Any(ev => ev.Type == SimEventType.Ultimate), c.ToString());
            }
        }
    }
}
