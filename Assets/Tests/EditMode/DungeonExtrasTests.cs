using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The map's extras: keys and the treasure room, the champion, the puzzles, the secret room and its old map.</summary>
    public class DungeonExtrasTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> eachTick = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                eachTick?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static void AnswerPicks(RunSimulation sim)
        {
            foreach (var p in sim.Players)
                for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(p.ClientId, 0);
        }

        /// <summary>A run (one player, started) whose first area has a room like this.</summary>
        private static RunSimulation WithRoom(RoomKind kind, out MapRoom room, out SimPlayer player, System.Func<MapRoom, bool> also = null)
        {
            for (ulong seed = 1; seed < 300; seed++)
            {
                var sim = new RunSimulation(seed);
                room = sim.Map.Rooms.Find(r => r.Kind == kind && (also == null || also(r)));
                if (room == null) continue;
                player = sim.AddPlayer(0);
                sim.Start();
                AnswerPicks(sim);
                return sim;
            }
            Assert.Fail($"no {kind} room on any map");
            room = null;
            player = null;
            return null;
        }

        private static void GiveKey(RunSimulation sim) =>
            typeof(RunSimulation).GetMethod("GainKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(sim, new object[] { sim.RoomStates[0], Vector2.zero });

        [Test]
        public void Maps_HaveAKeySource_ALockedDeadEndTreasureRoom_AndUsuallyASecret()
        {
            int secrets = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            for (int area = 0; area < RunSimulation.AreaCount; area++)
            {
                var map = DungeonMap.Generate(seed, area);
                Assert.That(map.Rooms.Any(r => r.Kind is RoomKind.MiniBoss or RoomKind.Puzzle), $"seed {seed}: a source of keys");
                var treasure = map.Rooms.Find(r => r.Kind == RoomKind.Treasure);
                if (treasure != null) Assert.AreEqual(1, treasure.Neighbor.Count(n => n >= 0), "the treasure room is a dead end");
                var secret = map.Rooms.Find(r => r.Kind == RoomKind.Secret);
                if (secret == null) continue;
                secrets++;
                Assert.AreEqual(1, secret.Neighbor.Count(n => n >= 0), "the secret room hangs off one room");
            }
            Assert.Greater(secrets, 90, "most maps hide a room");
        }

        [Test]
        public void TreasureDoor_OpensWithAKey_AndTheRoomGivesRareGear()
        {
            var sim = WithRoom(RoomKind.Treasure, out var treasure, out var p);
            Vector2 spot = sim.UnlockSpot(treasure);
            Run(sim, 1f, s => s.SetPlayerPosition(0, spot));
            Assert.IsFalse(sim.RoomStates[treasure.Id].Unlocked, "no key, no entry");

            GiveKey(sim);
            Assert.AreEqual(1, sim.Keys);
            var events = Run(sim, 0.5f, s => s.SetPlayerPosition(0, spot));
            Assert.IsTrue(sim.RoomStates[treasure.Id].Unlocked);
            Assert.AreEqual(0, sim.Keys, "the key is used up");
            Assert.That(events.Any(e => e.Type == SimEventType.DoorOpened && e.Int == 2));

            int bag = p.Bag.Count;
            Run(sim, RunSimulation.PedestalHoldTime + 0.3f, s => s.SetPlayerPosition(0, RunSimulation.PedestalPosition(treasure.Bounds)));
            Assert.AreEqual(bag + 1, p.Bag.Count);
            Assert.GreaterOrEqual(p.Bag[p.Bag.Count - 1].rarity, Rarity.Rare);
        }

        [Test]
        public void Champion_IsTougher_AndDropsTheKey()
        {
            var sim = WithRoom(RoomKind.MiniBoss, out var lair, out var p);
            Run(sim, 2f, s => s.SetPlayerPosition(0, lair.Origin + new Vector2(0f, -5f)));
            int champion = -1;
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Champion) champion = i;
            Assert.GreaterOrEqual(champion, 0, "the champion came");
            Assert.IsTrue(sim.Enemies[champion].Elite);
            for (int round = 0; round < 10 && sim.Keys == 0; round++)
            {
                for (int i = 0; i < sim.Enemies.Count; i++)
                    if (sim.Enemies[i].RoomId == lair.Id && !sim.Enemies[i].Def.Prop) sim.DamageEnemy(i, 1e7f, p, HitFlags.None, Vector2.zero, 0f);
                Run(sim, 0.5f, s => s.SetPlayerPosition(0, lair.Origin + new Vector2(0f, -5f)));
            }
            Assert.AreEqual(1, sim.Keys);
            Assert.IsTrue(sim.RoomStates[lair.Id].Cleared);
        }

        [Test]
        public void PressurePlates_LitTogether_GiveAKey()
        {
            var sim = WithRoom(RoomKind.Puzzle, out var hall, out _, r => r.Puzzle == PuzzleKind.Plates);
            Run(sim, 0.3f, s => s.SetPlayerPosition(0, hall.Origin + new Vector2(0f, -6f)));
            Assert.IsTrue(sim.RoomStates[hall.Id].Fighting);
            var events = new List<SimEvent>();
            foreach (var spot in RunSimulation.PlateSpots)
                events.AddRange(Run(sim, RunSimulation.PlateHold + 0.2f, s => s.SetPlayerPosition(0, hall.Origin + spot)));
            Assert.That(events.Any(e => e.Type == SimEventType.PuzzleEnded && e.Int == 1), "solved");
            Assert.AreEqual(1, sim.Keys);
            Assert.IsTrue(sim.RoomStates[hall.Id].Cleared);
        }

        [Test]
        public void WardCrystal_Survived_GivesAKey_Broken_GivesNothing()
        {
            foreach (bool guard in new[] { true, false })
            {
                var sim = WithRoom(RoomKind.Puzzle, out var room, out _, r => r.Puzzle == PuzzleKind.Crystal);
                var events = Run(sim, RunSimulation.CrystalSeconds + 3f, s =>
                {
                    s.SetPlayerPosition(0, room.Origin + new Vector2(0f, -6f));
                    for (int i = 0; i < s.Enemies.Count; i++)
                    {
                        ref var e = ref s.Enemies[i];
                        if (e.RoomId != room.Id || e.Dead) continue;
                        if (guard && !e.Def.Prop) e.Dead = true;                          // a perfect defence
                        if (!guard && e.Type == EnemyType.WardCrystal) e.Health -= 40f;  // torn apart
                    }
                });
                Assert.That(events.Any(e => e.Type == SimEventType.PuzzleEnded), $"guard={guard}: the puzzle ended");
                Assert.AreEqual(guard ? 1 : 0, sim.Keys, $"guard={guard}");
                Assert.IsTrue(sim.RoomStates[room.Id].Cleared);
            }
        }

        [Test]
        public void Monsters_GoForTheCrystal()
        {
            var sim = WithRoom(RoomKind.Puzzle, out var room, out _, r => r.Puzzle == PuzzleKind.Crystal);
            Run(sim, 12f, s => s.SetPlayerPosition(0, room.Origin + new Vector2(-12f, -7f)));
            Assert.Less(sim.CrystalHealth(sim.RoomStates[room.Id]), 1f, "the crystal took damage while the player stood far away");
        }

        [Test]
        public void TreasureImp_Flees_CaughtGivesAKey_EscapedGivesNothing()
        {
            foreach (bool catchIt in new[] { true, false })
            {
                var sim = WithRoom(RoomKind.Puzzle, out var room, out var p, r => r.Puzzle == PuzzleKind.Thief);
                Vector2 stand = room.Origin + new Vector2(0f, -1f);
                Run(sim, 1f, s => s.SetPlayerPosition(0, stand));
                int imp = -1;
                for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Type == EnemyType.TreasureImp) imp = i;
                Assert.GreaterOrEqual(imp, 0, "the imp is there");
                Assert.Greater((sim.Enemies[imp].Position - stand).magnitude, 2.5f, "and running away");
                if (catchIt) sim.DamageEnemy(imp, 1e6f, p, HitFlags.None, Vector2.zero, 0f);
                var events = Run(sim, RunSimulation.ThiefSeconds + 1f, s => s.SetPlayerPosition(0, stand));
                Assert.That(events.Any(e => e.Type == SimEventType.PuzzleEnded && e.Int == (catchIt ? 1 : 0)), $"caught={catchIt}");
                Assert.AreEqual(catchIt ? 1 : 0, sim.Keys);
                Assert.IsTrue(sim.RoomStates[room.Id].Cleared);
            }
        }

        [Test]
        public void Gauntlet_ThePlateInTheMiddleGivesAKey_AndTheTrapsRun()
        {
            var sim = WithRoom(RoomKind.Puzzle, out var room, out _, r => r.Puzzle == PuzzleKind.Gauntlet);
            var events = Run(sim, 4f, s => s.SetPlayerPosition(0, room.Origin + new Vector2(0f, -6f)));
            Assert.That(events.Any(e => e.Type == SimEventType.HazardWarn), "the traps are working");
            events = Run(sim, RunSimulation.PlateHold + 0.3f, s => s.SetPlayerPosition(0, room.Origin + RunSimulation.GauntletSpot));
            Assert.That(events.Any(e => e.Type == SimEventType.PuzzleEnded && e.Int == 1));
            Assert.AreEqual(1, sim.Keys);
        }

        [Test]
        public void CrackedWall_HidesTheSecretRoom_WhichHoldsTheOldMap()
        {
            var sim = WithRoom(RoomKind.Secret, out var secret, out var p);
            var outside = sim.Map[secret.Neighbor.First(n => n >= 0)];
            Run(sim, 0.5f, s => s.SetPlayerPosition(0, outside.Origin + new Vector2(0f, -4f)));
            int wall = -1;
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Type == EnemyType.CrackedWall) wall = i;
            Assert.GreaterOrEqual(wall, 0, "a cracked wall stands in the room beside it");
            Assert.IsFalse(sim.RoomStates[secret.Id].Unlocked);

            sim.DamageEnemy(wall, 1e6f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.3f, s => s.SetPlayerPosition(0, outside.Origin + new Vector2(0f, -4f)));
            Assert.IsTrue(sim.RoomStates[secret.Id].Unlocked);
            Assert.That(events.Any(e => e.Type == SimEventType.DoorOpened && e.Int == 3));

            Assert.IsFalse(sim.MapRevealed);
            Run(sim, RunSimulation.PedestalHoldTime + 0.3f, s => s.SetPlayerPosition(0, RunSimulation.PedestalPosition(secret.Bounds)));
            Assert.IsTrue(sim.MapRevealed, "the old map");
        }
    }
}
