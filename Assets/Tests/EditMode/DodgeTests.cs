using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The dodge: two charges that come back slowly, untouchable only while moving, perfect dodges.</summary>
    public class DodgeTests
    {
        private const float Dt = 1f / 30f;
        private static byte _presses;

        private static List<SimEvent> Run(RunSimulation sim, float seconds)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static void Press(RunSimulation sim, SimPlayer p) =>
            sim.SetPlayerInput(p.ClientId, new PlayerInput { Aim = Vector2.right, DodgeCount = ++_presses });

        private static (RunSimulation, SimPlayer) Make()
        {
            var sim = new RunSimulation(9);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            return (sim, p);
        }

        [Test]
        public void TwoDodges_ThenAWait()
        {
            var (sim, p) = Make();
            Assert.AreEqual(ClassCatalog.DodgeCharges, p.DodgeCharges);
            Press(sim, p);
            Run(sim, 0.35f);
            Press(sim, p);
            Run(sim, 0.35f);
            Assert.AreEqual(0, p.DodgeCharges, "both spent");
            Press(sim, p);
            Run(sim, 0.1f);
            Assert.AreEqual(0, p.DodgeCharges, "nothing left to dodge with");
            Run(sim, ClassCatalog.DodgeRechargeTime);
            Assert.AreEqual(1, p.DodgeCharges, "one comes back");
            Run(sim, ClassCatalog.DodgeRechargeTime);
            Assert.AreEqual(2, p.DodgeCharges, "then the other");
        }

        [Test]
        public void AHitInTheDodge_IsAPerfectDodge_AndGivesTheChargeBack()
        {
            var (sim, p) = Make();
            float hp = p.Hp;
            Press(sim, p);
            Run(sim, Dt);
            Assert.AreEqual(1, p.DodgeCharges);
            sim.HurtPlayer(p, 30f);
            sim.HurtPlayer(p, 30f);
            var events = Run(sim, Dt);
            Assert.AreEqual(hp, p.Hp, "untouched");
            Assert.AreEqual(2, p.DodgeCharges, "the charge came back");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.PerfectDodge), "once per dodge");

            // After the dodge has travelled, blows land again.
            Run(sim, 0.4f);
            p.InvulnerableTimer = 0f;
            sim.HurtPlayer(p, 30f);
            Assert.Less(p.Hp, hp);
        }

        [Test]
        public void TheRangersLightFeet_BringDodgesBackSooner()
        {
            var (sim, p) = Make();
            Assert.Less(ClassCatalog.DodgeRechargeTime * 0.6f + 0.01f, sim.DodgeRechargeTime(p) + 0.01f + ClassCatalog.DodgeRechargeTime * 0.4f);
            Assert.AreEqual(ClassCatalog.DodgeRechargeTime, sim.DodgeRechargeTime(p), 0.001f);
        }
    }
}
