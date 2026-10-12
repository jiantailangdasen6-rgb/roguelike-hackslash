using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The gathering bell by the campfire, and the run's area times.</summary>
    public class BellAndStatsTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> eachTick = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players)
                {
                    p.InvulnerableTimer = 999f;
                    for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(p.ClientId, 0);
                }
                eachTick?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static RunSimulation Team(int players)
        {
            var sim = new RunSimulation(7);
            for (int i = 0; i < players; i++) sim.AddPlayer((ulong)i);
            sim.Start();
            return sim;
        }

        [Test]
        public void Bell_CallsTeammatesInQuietRooms_ButNotThoseFighting()
        {
            var sim = Team(3);
            var fire = sim.Map.Campfire;
            var quiet = sim.Map.Rooms.First(r => r.Kind is RoomKind.Shrine or RoomKind.Spring or RoomKind.Whetstone or RoomKind.Start && r != fire);
            var fight = sim.Map.Rooms.First(r => r.Kind == RoomKind.Combat);
            var (ringer, wanderer, fighter) = (sim.Players[0], sim.Players[1], sim.Players[2]);
            // Settle everyone in their rooms (the fight room wakes when the fighter walks in).
            Run(sim, 1f, s =>
            {
                s.SetPlayerPosition(0, fire.Bounds.center + new Vector2(-4f, -3f));
                s.SetPlayerPosition(1, quiet.Bounds.center);
                s.SetPlayerPosition(2, fight.Bounds.center);
            });
            Assert.IsTrue(sim.RoomOf(fighter).Fighting, "the fight room woke");

            var events = Run(sim, 1.2f, s =>
            {
                s.SetPlayerPosition(0, s.BellPosition);
                s.SetPlayerPosition(2, fight.Bounds.center);
            });
            var rung = events.FirstOrDefault(e => e.Type == SimEventType.BellRung);
            Assert.AreEqual(SimEventType.BellRung, rung.Type, "standing under the bell rings it");
            Assert.AreEqual(1, rung.Int, "one teammate called");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Teleport && e.ClientId == wanderer.ClientId), "the wanderer is moved");
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Teleport && e.ClientId == fighter.ClientId), "the fighter stays");
            Assert.AreEqual(fire, sim.Map.RoomAt(wanderer.Position), "the wanderer stands in the campfire room");

            // It needs a while before ringing again.
            var again = Run(sim, 2f, s => s.SetPlayerPosition(0, s.BellPosition));
            Assert.IsFalse(again.Any(e => e.Type == SimEventType.BellRung), "cooldown");
        }

        [Test]
        public void Bell_DoesNothingSolo()
        {
            var sim = Team(1);
            var events = Run(sim, 2f, s => s.SetPlayerPosition(0, s.BellPosition));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.BellRung));
        }

        [Test]
        public void Splits_RecordAreas_AndReadBack()
        {
            var sim = Team(1);
            Run(sim, 2f);
            sim.GoToNextArea();
            Run(sim, 1f);
            Assert.AreEqual(2, sim.Splits.Count, "one split per area");
            Assert.Greater(sim.Splits[0].End, 1.5f, "the first area closed when the second began");
            var (areas, downs) = RunSimulation.ParseSplits(sim.SplitsText());
            Assert.AreEqual(2, areas.Count);
            Assert.AreEqual(-1, areas[1][3], "the current area is still open");
            Assert.AreEqual(0, downs);

            sim.Players[0].InvulnerableTimer = 0f;
            sim.HurtPlayer(sim.Players[0], 99999f);
            Assert.AreEqual(1, sim.Downs, "a down is counted");
        }
    }
}
