using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The charged strike: hold the attack, let go.</summary>
    public class ChargeTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Hold(RunSimulation sim, SimPlayer p, float seconds, bool attack)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                p.InvulnerableTimer = 999f;
                sim.SetPlayerInput(p.ClientId, new PlayerInput { Aim = Vector2.right * 2f, Attack = attack });
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static (RunSimulation, SimPlayer, int) Setup(ClassId cls)
        {
            var sim = new RunSimulation(31);
            var p = sim.AddPlayer(0, new Loadout { classId = cls });
            sim.Start(); // a room around them: shots fly only inside rooms and corridors
            while (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
            sim.Enemies.Spawn(EnemyType.Skeleton, p.Position + new Vector2(1.6f, 0f), 50f, 1f, false);
            int e = sim.Enemies.Count - 1;
            sim.Enemies[e].Cooldown = 99f;
            sim.Enemies[e].RoomId = sim.Map.Start.Id;
            return (sim, p, e);
        }

        private static int Level(List<SimEvent> events) =>
            events.Where(e => e.Type == SimEventType.ChargedStrike).Select(e => e.Int).DefaultIfEmpty(0).Max();

        [Test]
        public void AShortHold_IsJustAttacks_ALongOne_EndsInAChargedStrike()
        {
            var (sim, p, _) = Setup(ClassId.Warrior);
            Hold(sim, p, 0.6f, true);
            Assert.AreEqual(0, Level(Hold(sim, p, 0.1f, false)), "too short");
            Hold(sim, p, RunSimulation.ChargeOne + 0.1f, true);
            Assert.AreEqual(1, Level(Hold(sim, p, 0.1f, false)));
            Hold(sim, p, RunSimulation.ChargeTwo + 0.1f, true);
            Assert.AreEqual(2, Level(Hold(sim, p, 0.1f, false)), "full charge");
        }

        [Test]
        public void TheChargedStrike_HitsHard_CloseOrFar()
        {
            var (sim, p, e) = Setup(ClassId.Guardian);
            Hold(sim, p, RunSimulation.ChargeTwo + 0.1f, true);
            float before = sim.Enemies[e].Health;
            Hold(sim, p, 0.1f, false);
            Assert.Greater(before - sim.Enemies[e].Health, p.Class.BasicDamage * 4f, "a melee cleave");

            var (far, ranger, target) = Setup(ClassId.Ranger);
            Hold(far, ranger, RunSimulation.ChargeOne + 0.1f, true);
            float hp = far.Enemies[target].Health;
            Hold(far, ranger, 1f, false);
            Assert.Less(far.Enemies[target].Health, hp, "the wave reaches");
        }
    }
}
