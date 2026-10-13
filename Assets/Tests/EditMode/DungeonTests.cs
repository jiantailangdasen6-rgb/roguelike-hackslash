using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The area maps: their shape, rooms fought apart, pedestals, the boss door, the way down, the campfire.</summary>
    public class DungeonTests
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
                for (int i = 0; i < 10 && p.PendingOffer != null && p.OfferKind != ChoiceKind.Shop; i++) sim.ChoosePerk(p.ClientId, 0);
        }

        private static void Put(RunSimulation sim, SimPlayer p, MapRoom room, Vector2 offset = default) =>
            sim.SetPlayerPosition(p.ClientId, room.Origin + offset);

        // ------------------------------------------------------------------ Maps

        [Test]
        public void Maps_AreConnected_WithTheBossAtADeadEndBehindTheCampfire()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            for (int area = 0; area < RunSimulation.AreaCount; area++)
            {
                var map = DungeonMap.Generate(seed, area);
                string where = $"seed {seed} area {area}";
                Assert.That(map.Rooms.Count, Is.InRange(11, 14), where);
                Assert.AreEqual(Vector2.zero, map.Start.Origin, $"{where}: the start room sits on the old arena");
                Assert.AreEqual(1, map.Boss.Neighbor.Count(n => n >= 0), $"{where}: the boss is a dead end");
                Assert.That(map.Boss.Neighbor, Does.Contain(map.Campfire.Id), $"{where}: the campfire leads to the boss");
                Assert.GreaterOrEqual(map.Boss.Depth, 4, $"{where}: the boss is not next door");

                // Everything is reachable from the start, doors are two-way, rooms find themselves by position.
                var seen = new HashSet<int> { map.Start.Id };
                var queue = new Queue<MapRoom>(new[] { map.Start });
                while (queue.Count > 0)
                {
                    var r = queue.Dequeue();
                    Assert.AreSame(r, map.RoomAt(r.Bounds.center), where);
                    for (int d = 0; d < 4; d++)
                    {
                        if (!r.HasDoor(d)) continue;
                        var n = map[r.Neighbor[d]];
                        Assert.AreEqual(r.Id, n.Neighbor[Dir.Opposite(d)], $"{where}: door back");
                        Assert.AreEqual(r.Cell + Dir.Step(d), n.Cell, where);
                        if (seen.Add(n.Id)) queue.Enqueue(n);
                    }
                }
                Assert.AreEqual(map.Rooms.Count, seen.Count, $"{where}: every room reachable");

                var kinds = map.Rooms.Select(r => r.Kind).ToList();
                Assert.That(kinds.Contains(RoomKind.Shrine) || kinds.Contains(RoomKind.Spirit), where + ": a shrine (or, deeper in, the wandering spirit)");
                Assert.That(kinds.Count(k => k is RoomKind.Combat or RoomKind.Elite), Is.GreaterThanOrEqualTo(3), where);
                if (area >= 1) Assert.That(kinds.Any(k => k is RoomKind.CurseAltar or RoomKind.Merchant), where);
                Assert.AreEqual(1, kinds.Count(k => k == RoomKind.Boss), where);
            }
        }

        [Test]
        public void Maps_AreTheSameOnEveryPeer()
        {
            var a = DungeonMap.Generate(1234, 1);
            var b = DungeonMap.Generate(1234, 1);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
            {
                Assert.AreEqual(a.Rooms[i].Cell, b.Rooms[i].Cell);
                Assert.AreEqual(a.Rooms[i].Kind, b.Rooms[i].Kind);
                Assert.AreEqual(a.Rooms[i].Layout.Id, b.Rooms[i].Layout.Id);
            }
            var sim = new RunSimulation(99);
            var mine = DungeonMap.Generate(sim.MapSeed, 0);
            Assert.AreEqual(sim.Map.Boss.Cell, mine.Boss.Cell, "clients rebuild the host's map from the seed");
        }

        [Test]
        public void EntryPoints_AreInsideTheRoom_AndClearOfObstacles()
        {
            var map = DungeonMap.Generate(7, 2);
            foreach (var r in map.Rooms)
                for (int d = 0; d < 4; d++)
                {
                    var at = DungeonMap.EntryPoint(r, d);
                    Assert.IsTrue(r.Bounds.Contains(at));
                    Assert.IsFalse(Arena.Overlaps(r.Layout, at, 0.6f), $"{r.Layout.Name}: entry {d} blocked");
                }
        }

        // ------------------------------------------------------------------ Rooms fought apart

        [Test]
        public void AFightRoomWakes_OnlyForThoseInside_AndMonstersStayIn()
        {
            var sim = new RunSimulation(3);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var b = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            var fight = sim.FindRoom(RoomKind.Combat);
            float bHp = b.Hp;
            var events = Run(sim, 6f, s =>
            {
                Put(s, a, fight, new Vector2(0f, -4f));
                Put(s, b, s.Map.Start, new Vector2(0f, -4f));
                AnswerPicks(s);
            });
            Assert.IsTrue(sim.RoomOf(a).Fighting);
            Assert.IsFalse(sim.RoomOf(b).Fighting, "the start room stays quiet");
            Assert.That(events.Any(e => e.Type == SimEventType.RoomEntered && e.Id == fight.Id));
            for (int i = 0; i < sim.Enemies.Count; i++)
            {
                if (sim.Enemies[i].Dead || sim.Enemies[i].Def.Prop) continue;
                Assert.AreEqual(fight.Id, sim.Enemies[i].RoomId);
                Assert.IsTrue(fight.Bounds.Contains(sim.Enemies[i].Position), "monsters keep to their room");
            }
            Assert.AreEqual(bHp, b.Hp, 0.01f, "nothing reached the one who stayed behind");
        }

        [Test]
        public void TwoRoomsCanBeFoughtAtOnce_AndAnEmptyFightWaits()
        {
            var sim = new RunSimulation(4);
            var a = sim.AddPlayer(0);
            var b = sim.AddPlayer(1);
            sim.Start();
            var fights = sim.Map.Rooms.Where(r => r.Kind is RoomKind.Combat or RoomKind.Elite).Take(2).ToArray();
            Run(sim, 3f, s =>
            {
                Put(s, a, fights[0], new Vector2(0f, -4f));
                Put(s, b, fights[1], new Vector2(0f, -4f));
                AnswerPicks(s);
            });
            Assert.IsTrue(sim.RoomStates[fights[0].Id].Fighting);
            Assert.IsTrue(sim.RoomStates[fights[1].Id].Fighting);

            // Both step out: the fights wait for them (no new waves without anyone there).
            int waves = sim.RoomStates[fights[0].Id].Wave;
            Run(sim, 25f, s =>
            {
                Put(s, a, s.Map.Start, new Vector2(-2f, -4f));
                Put(s, b, s.Map.Start, new Vector2(2f, -4f));
                AnswerPicks(s);
            });
            Assert.IsTrue(sim.RoomStates[fights[0].Id].Fighting, "an unfinished room stays unfinished");
            Assert.AreEqual(waves, sim.RoomStates[fights[0].Id].Wave);
        }

        [Test]
        public void Picks_WaitForAQuietRoom()
        {
            var sim = new RunSimulation(5);
            var p = sim.AddPlayer(0);
            sim.Start();
            AnswerPicks(sim);
            var fight = sim.FindRoom(RoomKind.Combat);
            Run(sim, 0.5f, s => Put(s, p, fight, new Vector2(0f, -4f)));
            p.StockedChoices.Enqueue(ChoiceKind.LevelUp);
            Run(sim, 0.5f, s => Put(s, p, fight, new Vector2(0f, -4f)));
            Assert.IsNull(p.PendingOffer, "no cards in the middle of a fight");
            Run(sim, 0.3f, s => Put(s, p, s.Map.Start, new Vector2(0f, -4f)));
            Assert.IsNotNull(p.PendingOffer, "offered on stepping out");
        }

        // ------------------------------------------------------------------ Pedestals

        private static void UsePedestal(RunSimulation sim, SimPlayer p, MapRoom room) =>
            Run(sim, RunSimulation.PedestalHoldTime + 0.3f, s => s.SetPlayerPosition(p.ClientId, RunSimulation.PedestalPosition(room.Bounds)));

        [Test]
        public void Shrine_GivesEachPlayerOnePower_Once()
        {
            var sim = new RunSimulation(6);
            var a = sim.AddPlayer(0);
            var b = sim.AddPlayer(1);
            sim.Start();
            AnswerPicks(sim);
            var shrine = sim.FindRoom(RoomKind.Shrine);
            UsePedestal(sim, a, shrine);
            Assert.AreEqual(ChoiceKind.Boon, a.OfferKind);
            Assert.IsNotNull(a.PendingOffer);
            Assert.IsNull(b.PendingOffer, "only who stood on it");
            sim.ChoosePerk(0, 0);
            UsePedestal(sim, a, shrine);
            Assert.IsNull(a.PendingOffer, "once per player");
            UsePedestal(sim, b, shrine);
            Assert.IsNotNull(b.PendingOffer, "the others still may");
        }

        [Test]
        public void Spring_RaisesMaxHp_AndHeals()
        {
            for (ulong seed = 1; seed < 60; seed++)
            {
                var sim = new RunSimulation(seed);
                var spring = sim.FindRoom(RoomKind.Spring);
                if (spring == null) continue;
                var p = sim.AddPlayer(0);
                sim.Start();
                AnswerPicks(sim);
                float max = p.MaxHp;
                p.Hp = 5f;
                UsePedestal(sim, p, spring);
                Assert.Greater(p.MaxHp, max);
                Assert.AreEqual(p.MaxHp, p.Hp, 0.01f);
                return;
            }
            Assert.Fail("no spring on any map");
        }

        [Test]
        public void CurseAltar_CursesOnlyWhoUsedIt_ForTwoPowers()
        {
            for (ulong seed = 1; seed < 80; seed++)
            {
                var sim = new RunSimulation(seed);
                var a = sim.AddPlayer(0);
                var b = sim.AddPlayer(1);
                sim.Start();
                sim.GoToNextArea();
                var altar = sim.FindRoom(RoomKind.CurseAltar);
                if (altar == null) continue;
                AnswerPicks(sim);
                int picks = a.StockedChoices.Count + (a.PendingOffer != null ? 1 : 0);
                UsePedestal(sim, a, altar);
                Assert.AreEqual(1, a.CurseStacks.Sum(), "one curse");
                Assert.AreEqual(0, b.CurseStacks.Sum(), "a personal curse");
                Assert.AreEqual(picks + RewardCatalog.CursedBoons, a.StockedChoices.Count + (a.PendingOffer != null ? 1 : 0));
                return;
            }
            Assert.Fail("no curse altar on any map");
        }

        [Test]
        public void Merchant_OffersWares_AndLetsYouWalkAway()
        {
            for (ulong seed = 1; seed < 80; seed++)
            {
                var sim = new RunSimulation(seed);
                var p = sim.AddPlayer(0);
                sim.Start();
                sim.GoToNextArea();
                var merchant = sim.FindRoom(RoomKind.Merchant);
                if (merchant == null) continue;
                AnswerPicks(sim);
                UsePedestal(sim, p, merchant);
                Assert.AreEqual(ChoiceKind.Shop, p.OfferKind);
                int purse = sim.Purse;
                sim.ChoosePerk(0, RunSimulation.ShopSkip);
                Assert.IsNull(p.PendingOffer);
                Assert.AreEqual(purse, sim.Purse, "walking away costs nothing");
                return;
            }
            Assert.Fail("no merchant on any map");
        }

        // ------------------------------------------------------------------ The boss door and the way down

        [Test]
        public void BossDoor_OpensOnlyWhenEveryoneStandsAtIt()
        {
            var sim = new RunSimulation(8);
            var a = sim.AddPlayer(0);
            var b = sim.AddPlayer(1);
            sim.Start();
            AnswerPicks(sim);
            Vector2 door = sim.BossDoorPosition;
            Vector2 inside = door + (sim.Map.Campfire.Bounds.center - door).normalized * 1.5f;
            Run(sim, 3f, s =>
            {
                s.SetPlayerPosition(0, inside);
                Put(s, b, s.Map.Start, new Vector2(0f, -4f));
            });
            Assert.IsFalse(sim.BossDoorOpen, "one of the team is elsewhere");
            var events = Run(sim, 2f, s =>
            {
                s.SetPlayerPosition(0, inside);
                s.SetPlayerPosition(1, inside + new Vector2(0.5f, 0f));
            });
            Assert.IsTrue(sim.BossDoorOpen);
            Assert.That(events.Any(e => e.Type == SimEventType.DoorOpened && e.Int == 0));
        }

        [Test]
        public void AfterTheBoss_TheWayDownLeadsToTheNextArea_AndTheFallenGetUp()
        {
            var sim = new RunSimulation(9);
            var a = sim.AddPlayer(0);
            var b = sim.AddPlayer(1);
            sim.Start(RunSimulation.RoomsPerArea - 1);
            AnswerPicks(sim);
            Run(sim, 2f);
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (sim.Enemies[i].Def.Boss) sim.DamageEnemy(i, 1e7f, a, HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.3f);
            Assert.IsTrue(sim.ExitOpen);

            b.InvulnerableTimer = 0f;
            sim.HurtPlayer(b, 99999f);
            Assert.IsTrue(b.Downed);
            var events = Run(sim, 2f, s => { if (s.Area == 0) s.SetPlayerPosition(0, s.ExitPosition); });
            Assert.AreEqual(1, sim.Area);
            Assert.That(events.Any(e => e.Type == SimEventType.RoomStarted && e.Int == RunSimulation.RoomsPerArea));
            Assert.IsFalse(b.Downed, "everyone who makes it through gets back up");
            Assert.AreSame(sim.Map.Start, sim.Map.RoomAt(a.Position));
        }

        [Test]
        public void LastBoss_WinsTheRun()
        {
            var sim = new RunSimulation(10);
            var a = sim.AddPlayer(0);
            sim.Start(RunSimulation.TotalRooms - 1);
            AnswerPicks(sim);
            Run(sim, 2f);
            for (int round = 0; round < 3 && !sim.Conquered; round++)
            {
                for (int i = 0; i < sim.Enemies.Count; i++)
                    if (!sim.Enemies[i].Dead && (sim.Enemies[i].Def.Boss || sim.Enemies[i].Type == EnemyType.SoulVessel))
                        sim.DamageEnemy(i, 1e7f, a, HitFlags.None, Vector2.zero, 0f);
                Run(sim, 0.3f);
            }
            Assert.IsTrue(sim.Conquered);
            Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => a.Position = s.HomePosition); // the way home
            Assert.AreEqual(RunPhase.Victory, sim.Phase);
        }

        // ------------------------------------------------------------------ The campfire

        [Test]
        public void Campfire_Heals_GetsTheFallenUp_AndItsChestsAppearOnTheFirstVisit()
        {
            var sim = new RunSimulation(11);
            var a = sim.AddPlayer(0);
            var b = sim.AddPlayer(1);
            sim.Start();
            AnswerPicks(sim);
            var fire = sim.Map.Campfire;
            b.InvulnerableTimer = 0f;
            sim.HurtPlayer(b, 99999f);
            a.Hp = 10f;
            Run(sim, 2f, s =>
            {
                s.SetPlayerPosition(0, fire.Origin + new Vector2(1.5f, 0f));
                s.SetPlayerPosition(1, fire.Origin + new Vector2(-1.5f, 0f));
            });
            Assert.Greater(a.Hp, 10f + a.MaxHp * 0.15f);
            Assert.IsFalse(b.Downed);
            int chests = 0;
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Type == EnemyType.TreasureChest) chests++;
            Assert.Greater(chests, 0);
        }
    }
}
