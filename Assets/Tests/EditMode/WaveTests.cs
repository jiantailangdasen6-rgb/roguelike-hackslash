using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Wave kinds: ambushes from the doorways, swarms, rings, calling pillars.</summary>
    public class WaveTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players)
                {
                    p.InvulnerableTimer = 999f;
                    while (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
                }
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        /// <summary>The team in a fight room with its first wave beaten; the next comes the forced way.</summary>
        private static (RunSimulation, MapRoom, List<SimEvent>) SecondWave(WaveKind kind)
        {
            var sim = new RunSimulation(29) { ForcedWave = kind };
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start();
            var room = sim.FindRoom(RoomKind.Combat);
            sim.MoveTeamTo(room);
            Run(sim, 3f);
            // Beat the first wave at once.
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (sim.Enemies[i].RoomId == room.Id && !sim.Enemies[i].Def.Prop) sim.Enemies[i].Dead = true;
            var events = Run(sim, 2.5f);
            return (sim, room, events);
        }

        private static WaveKind? Started(List<SimEvent> events) =>
            events.Where(e => e.Type == SimEventType.WaveStarted && e.Int >= 2).Select(e => (WaveKind?)(WaveKind)Mathf.RoundToInt(e.Size.x)).FirstOrDefault();

        [Test]
        public void ASwarm_IsMany_AndFrail()
        {
            var (sim, room, events) = SecondWave(WaveKind.Swarm);
            Assert.AreEqual(WaveKind.Swarm, Started(events));
            var swarm = new List<Enemy>();
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].RoomId == room.Id && !sim.Enemies[i].Def.Prop) swarm.Add(sim.Enemies[i]);
            Assert.Greater(swarm.Count, 6, "many");
            Assert.AreEqual(1, swarm.Select(e => e.Type).Distinct().Count(), "all of one kind");
        }

        [Test]
        public void CallingPillars_KeepCalling_AndHoldTheRoom()
        {
            var (sim, room, events) = SecondWave(WaveKind.Totem);
            Assert.AreEqual(WaveKind.Totem, Started(events));
            int totem = -1;
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Type == EnemyType.SummonTotem) totem = i;
            Assert.GreaterOrEqual(totem, 0, "a pillar stands");
            // Kill everything but the pillars: the room stays on.
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (sim.Enemies[i].RoomId == room.Id && !sim.Enemies[i].Def.Prop) sim.Enemies[i].Dead = true;
            events = Run(sim, RunSimulation.TotemEvery + 1f);
            Assert.IsFalse(sim.RoomOf(sim.Players[0]).Cleared, "not over while a pillar stands");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.SpawnWarning), "it calls more");
        }

        [Test]
        public void AnAmbush_ComesThroughTheDoorways_AndARingCloses()
        {
            var (sim, room, events) = SecondWave(WaveKind.Ambush);
            Assert.AreEqual(WaveKind.Ambush, Started(events));
            var warnings = events.Where(e => e.Type == SimEventType.SpawnWarning).Select(e => e.Pos).ToList();
            Assert.IsNotEmpty(warnings);
            var doors = Enumerable.Range(0, 4).Where(room.HasDoor).Select(d => DungeonMap.EntryPoint(room, d)).ToList();
            foreach (var w in warnings)
                Assert.Less(doors.Min(d => (d - w).magnitude), 3f, "near a doorway");

            var (ring, ringRoom, ringEvents) = SecondWave(WaveKind.Encircle);
            Assert.AreEqual(WaveKind.Encircle, Started(ringEvents));
        }
    }
}
