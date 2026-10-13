using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Trials made for teams: the twin plates and the dark hall with its lantern.</summary>
    public class CoopTrialTests
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
            }
            return events;
        }

        /// <summary>A run whose first area has a twin-plate trial.</summary>
        private static RunSimulation WithTwinPlates(out MapRoom room)
        {
            for (ulong seed = 1; seed < 400; seed++)
            {
                var sim = new RunSimulation(seed);
                room = sim.Map.Rooms.Find(r => r.Kind == RoomKind.Puzzle && r.Puzzle == PuzzleKind.Twin);
                if (room != null) return sim;
            }
            room = null;
            Assert.Fail("no seed with twin plates");
            return null;
        }

        private static void Hold(RunSimulation sim, SimPlayer p, Vector2 spot) => p.Position = spot;

        [Test]
        public void EveryAreaHasOneDarkHall()
        {
            for (ulong seed = 1; seed < 30; seed++)
                for (int area = 0; area < RunSimulation.AreaCount; area++)
                    Assert.AreEqual(1, DungeonMap.Generate(seed, area).Rooms.Count(r => r.Dark), $"seed {seed} area {area}");
        }

        [Test]
        public void TwinPlates_OpenWhenBothAreHeldTogether()
        {
            var sim = WithTwinPlates(out var room);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Guardian });
            var b = sim.AddPlayer(1, new Loadout { classId = ClassId.Guardian });
            sim.Start();
            sim.MoveTeamTo(room);
            Run(sim, 0.3f);
            var state = sim.RoomStates[room.Id];
            Assert.IsTrue(state.Fighting, "the trial woke");

            // One plate alone does nothing.
            Run(sim, 3f, s => { Hold(s, a, room.Origin + RunSimulation.TwinSpots[0]); b.Position = room.Origin; a.Hp = a.MaxHp; b.Hp = b.MaxHp; });
            Assert.IsFalse(state.Cleared);
            Assert.Less(state.Progress, 0.1f);

            int keys = sim.Keys;
            Run(sim, sim.TwinHold + 0.5f, s =>
            {
                Hold(s, a, room.Origin + RunSimulation.TwinSpots[0]);
                Hold(s, b, room.Origin + RunSimulation.TwinSpots[1]);
                a.Hp = a.MaxHp;
                b.Hp = b.MaxHp;
            });
            Assert.IsTrue(state.Cleared, "solved");
            Assert.AreEqual(keys + 1, sim.Keys, "worth a key");
        }

        [Test]
        public void TwinPlates_AloneTheyLingerLongEnoughToRunAcross()
        {
            var sim = WithTwinPlates(out var room);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Ranger });
            sim.Start();
            sim.MoveTeamTo(room);
            Run(sim, 0.3f);
            Vector2 left = room.Origin + RunSimulation.TwinSpots[0], right = room.Origin + RunSimulation.TwinSpots[1];
            float t = 0f;
            Run(sim, 8f, s =>
            {
                // Stand on the left, then walk to the right at running speed.
                t += Dt;
                a.Position = t < 0.6f ? left : Vector2.MoveTowards(left, right, (t - 0.6f) * 5f);
                a.Hp = a.MaxHp;
            });
            Assert.IsTrue(sim.RoomStates[room.Id].Cleared, "solvable alone");
        }

        [Test]
        public void DarkHall_LanternIsCarried_AndMonstersOutsideItsLightShrugOffBlows()
        {
            var sim = new RunSimulation(21);
            var hall = sim.Map.DarkHall;
            Assert.IsNotNull(hall);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var b = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            sim.MoveTeamTo(hall);
            Run(sim, 0.3f);
            var state = sim.RoomStates[hall.Id];
            Assert.IsTrue(state.Dark && state.Fighting);

            // Pick the lantern up.
            var events = Run(sim, 0.2f, s => a.Position = state.LanternPos);
            Assert.AreEqual(a.ClientId + 1, state.LanternBy);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Lantern && e.Int == 1));

            // A monster far from the lantern takes little damage; next to it, the full blow.
            a.Position = hall.Origin + new Vector2(-10f, 0f);
            Run(sim, Dt);
            sim.Enemies.Spawn(EnemyType.Skeleton, hall.Origin + new Vector2(10f, 0f), 50f, 1f, false);
            int far = sim.Enemies.Count - 1;
            sim.Enemies[far].RoomId = hall.Id;
            float before = sim.Enemies[far].Health;
            sim.DamageEnemy(far, 10f, b, HitFlags.DamageOverTime, Vector2.zero, 0f);
            Assert.AreEqual(10f * RunSimulation.DarkDamageFactor, before - sim.Enemies[far].Health, 0.01f);

            sim.Enemies[far].Position = a.Position + new Vector2(1f, 0f);
            before = sim.Enemies[far].Health;
            sim.DamageEnemy(far, 10f, b, HitFlags.DamageOverTime, Vector2.zero, 0f);
            Assert.AreEqual(10f, before - sim.Enemies[far].Health, 0.01f);

            // The carrier falls: the lantern drops where they were.
            Vector2 at = a.Position;
            a.InvulnerableTimer = 0f;
            sim.HurtPlayer(a, 99999f);
            events = Run(sim, Dt);
            Assert.AreEqual(0ul, state.LanternBy);
            Assert.Less((state.LanternPos - at).magnitude, 0.5f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Lantern && e.Int == 0));
        }
    }
}
