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
