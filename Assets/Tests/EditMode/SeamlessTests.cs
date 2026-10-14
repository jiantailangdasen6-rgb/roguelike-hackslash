using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Phase 5: the area map in one piece - corridors between neighbours, walked on foot.</summary>
    public class SeamlessTests
    {
        private const float R = 0.35f;

        [Test]
        public void EveryLink_HasACorridor_BetweenTheTwoWalls()
        {
            for (ulong seed = 1; seed < 30; seed++)
            {
                var map = DungeonMap.Generate(seed, (int)(seed % 3));
                int links = 0;
                foreach (var r in map.Rooms) foreach (int n in r.Neighbor) if (n >= 0) links++;
                Assert.AreEqual(links / 2, map.Corridors.Count, $"seed {seed}");
                foreach (var c in map.Corridors)
                {
                    Assert.IsNotNull(map.CorridorBetween(c.B, c.A));
                    Assert.Greater(c.Rect.width, 0f);
                    Assert.Greater(c.Rect.height, 0f);
                    foreach (var room in map.Rooms) Assert.IsFalse(room.Bounds.Overlaps(c.Rect) && room.Id != c.A && room.Id != c.B, "a corridor crosses another room");
                }
            }
        }

        [Test]
        public void WalkingDownACorridor_ReachesTheNextRoom_UnlessItIsShut()
        {
            var map = DungeonMap.Generate(5, 0);
            var c = map.Corridors[0];
            var a = map[c.A];
            var b = map[c.B];
            System.Func<MapRoom, Rect> floor = room => room.Bounds;
            Vector2 from = DungeonMap.EntryPoint(a, c.Dir), goal = DungeonMap.EntryPoint(b, Dir.Opposite(c.Dir));
            Vector2 Walk(System.Func<Corridor, bool> open)
            {
                Vector2 p = from;
                for (int i = 0; i < 400; i++) p = map.Step(p, Vector2.MoveTowards(p, goal, 0.15f), R, open, floor);
                return p;
            }
            Assert.Less((Walk(_ => true) - goal).magnitude, 0.2f, "through the open corridor");
            Vector2 stopped = Walk(_ => false);
            Assert.AreEqual(a, map.RoomAt(stopped), "a shut corridor keeps you in the room");
        }

        private static void Run(RunSimulation sim, float seconds, System.Action each = null)
        {
            for (float t = 0; t < seconds; t += 1f / 30f)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                each?.Invoke();
                sim.Step(1f / 30f);
                sim.Events.Clear();
            }
        }

        private static (RunSimulation Sim, SimPlayer P, Corridor C) InStartRoom()
        {
            var sim = new RunSimulation(5);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            sim.Start();
            for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
            var c = sim.Map.Corridors.Find(k => (k.A == sim.Map.Start.Id || k.B == sim.Map.Start.Id) && sim.CorridorOpen(k));
            Assert.IsNotNull(c, "the start room has a way out");
            return (sim, p, c);
        }

        /// <summary>A point down the corridor, <paramref name="depth"/> units out of the start room.</summary>
        private static Vector2 Out(RunSimulation sim, Corridor c, float depth)
        {
            var start = sim.Map.Start;
            int dir = c.A == start.Id ? c.Dir : Dir.Opposite(c.Dir);
            return DungeonMap.DoorPosition(start, dir) + (Vector2)Dir.Step(dir) * depth;
        }

        [Test]
        public void Shots_FlyDownTheCorridors()
        {
            var (sim, p, c) = InStartRoom();
            Vector2 at = Out(sim, c, 3f);
            Vector2 along = (Out(sim, c, 6f) - at).normalized;
            sim.SetPlayerPosition(0, at);
            sim.SetPlayerInput(0, new PlayerInput { Aim = along * 4f, Attack = true });
            int ended = 0, fired = 0;
            for (int i = 0; i < 6; i++)
            {
                sim.Step(1f / 30f);
                foreach (var e in sim.Events)
                {
                    if (e.Type == SimEventType.ProjectileSpawn) fired++;
                    if (e.Type == SimEventType.ProjectileEnd) ended++;
                }
                sim.Events.Clear();
            }
            Assert.Greater(fired, 0);
            Assert.AreEqual(0, ended, "a bolt fired in a corridor keeps flying");
        }

        [Test]
        public void Monsters_FollowALittleWayDownACorridor_ThenNoFurther()
        {
            var (sim, p, c) = InStartRoom();
            sim.Enemies.Spawn(EnemyType.Ghoul, DungeonMap.EntryPoint(sim.Map.Start, c.A == sim.Map.Start.Id ? c.Dir : Dir.Opposite(c.Dir)), 50f, 0f, false);
            int ghoul = sim.Enemies.Count - 1;
            Vector2 far = Out(sim, c, 11f);
            Run(sim, 4f, () => sim.SetPlayerPosition(0, far));
            Assert.AreEqual(sim.Map.Start.Id, p.RoomId, "still counted in the room left behind");
            var g = sim.Enemies[ghoul].Position;
            Assert.IsFalse(sim.Map.Start.Bounds.Contains(g), "it came out after the player");
            Assert.Less((g - Out(sim, c, 0f)).magnitude, RunSimulation.LeashDepth + 0.6f, "but only a few steps");
        }

        [Test]
        public void TheLordsHall_BringsInWhoeverLagsBehind_WhenItShuts()
        {
            var sim = new RunSimulation(5);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var b = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            for (int k = 0; k < 30; k++) foreach (var q in sim.Players) if (q.PendingOffer != null) sim.ChoosePerk(q.ClientId, 0);
            sim.MoveTeamTo(sim.Map.Boss); // opens the boss door
            sim.SetPlayerPosition(1, sim.Map.Campfire.Bounds.center); // one lags behind at the fire
            Run(sim, 0.3f);
            Assert.IsTrue(sim.RoomStates[sim.Map.Boss.Id].Fighting);
            Assert.IsTrue(sim.Map.Boss.Bounds.Contains(b.Position), "brought into the hall");
            Assert.IsFalse(sim.CorridorOpen(sim.Map.CorridorBetween(sim.Map.Boss.Id, sim.Map.Campfire.Id)), "and the gates are down");
        }

        [Test]
        public void Walls_Hold_OutsideTheCorridors()
        {
            var map = DungeonMap.Generate(9, 1);
            var room = map.Start;
            System.Func<MapRoom, Rect> floor = r => r.Bounds;
            // Into a wall away from any doorway: you stay on the floor.
            Vector2 p = room.Bounds.center + new Vector2(room.Bounds.width * 0.5f - 1f, 5f);
            for (int i = 0; i < 60; i++) p = map.Step(p, p + new Vector2(0.2f, 0f), R, _ => true, floor);
            Assert.LessOrEqual(p.x, room.Bounds.xMax - R + 0.001f);
            Assert.IsTrue(map.Walkable(room.Bounds.center, R, _ => true, floor));
            Assert.IsFalse(map.Walkable(room.Bounds.center + new Vector2(DungeonMap.PitchX * 0.5f, 6f), R, _ => true, floor), "the void between rooms");
        }
    }
}
