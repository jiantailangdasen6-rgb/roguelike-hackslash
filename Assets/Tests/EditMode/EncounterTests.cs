using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Encounter rooms: the team votes by standing in a circle; each encounter's two choices.</summary>
    public class EncounterTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> each = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                each?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        /// <summary>A run whose map has an encounter room, the team standing in it.</summary>
        private static (RunSimulation, MapRoom) Encounter(EncounterKind kind, int players = 2)
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                var sim = new RunSimulation(seed);
                for (int i = 0; i < players; i++) sim.AddPlayer((ulong)i, new Loadout { classId = ClassId.Warrior });
                sim.Start();
                var room = sim.FindRoom(RoomKind.Encounter);
                if (room == null) continue;
                room.Encounter = kind;
                sim.MoveTeamTo(room);
                Run(sim, 0.5f);
                foreach (var p in sim.Players) while (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
                return (sim, room);
            }
            Assert.Fail("no map with an encounter room");
            return default;
        }

        private static void Stand(RunSimulation sim, SimPlayer p, Vector2 at)
        {
            p.Position = at;
            p.BodyPosition = at;
        }

        private static bool Resolved(List<SimEvent> events, int choice) =>
            events.Any(e => e.Type == SimEventType.EncounterResolved && e.Int == choice);

        [Test]
        public void Maps_HaveEncounterRooms_WithEveryKind()
        {
            var kinds = new HashSet<EncounterKind>();
            int maps = 0, with = 0;
            for (ulong seed = 1; seed < 120; seed++)
            for (int area = 0; area < 3; area++)
            {
                var map = DungeonMap.Generate(seed, area);
                maps++;
                foreach (var r in map.Rooms)
                    if (r.Kind == RoomKind.Encounter)
                    {
                        with++;
                        kinds.Add(r.Encounter);
                    }
            }
            Assert.Greater(with, maps / 4, "encounters are common");
            Assert.AreEqual(RunSimulation.EncounterCount, kinds.Count, "every kind shows up");
        }

        [Test]
        public void TheTeam_MustAgree_ThenTheChoiceIsMade()
        {
            var (sim, room) = Encounter(EncounterKind.LostSoul);
            var a = sim.Players[0];
            var b = sim.Players[1];
            a.Hp = b.Hp = 10f;
            var events = Run(sim, 2f, s =>
            {
                Stand(s, a, RunSimulation.ChoiceSpot(room.Bounds, 0));
                Stand(s, b, RunSimulation.ChoiceSpot(room.Bounds, 1));
            });
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.EncounterResolved), "split vote: nothing happens");
            events = Run(sim, 2f, s =>
            {
                Stand(s, a, RunSimulation.ChoiceSpot(room.Bounds, 0));
                Stand(s, b, RunSimulation.ChoiceSpot(room.Bounds, 0));
            });
            Assert.IsTrue(Resolved(events, 0));
            Assert.AreEqual(a.MaxHp, a.Hp, 0.01f, "helping the lost soul heals everyone there");
            Assert.AreEqual(b.MaxHp, b.Hp, 0.01f);
            events = Run(sim, 2f, s =>
            {
                Stand(s, a, RunSimulation.ChoiceSpot(room.Bounds, 1));
                Stand(s, b, RunSimulation.ChoiceSpot(room.Bounds, 1));
            });
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.EncounterResolved), "only once");
        }

        [Test]
        public void SmashingTheStatue_LetsOutTwoElitesWithAffixes()
        {
            var (sim, room) = Encounter(EncounterKind.Statue, players: 1);
            var p = sim.Players[0];
            var events = Run(sim, 2f, s => Stand(s, p, RunSimulation.ChoiceSpot(room.Bounds, 1)));
            Assert.IsTrue(Resolved(events, 1));
            int elites = 0;
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Elite && sim.Enemies[i].RoomId == room.Id)
                {
                    elites++;
                    Assert.AreNotEqual(EliteAffix.None, sim.Enemies[i].Affixes);
                }
            Assert.AreEqual(2, elites);
        }

        [Test]
        public void DecliningTheGambler_GivesShards_AndCleansingTheFont_LiftsACurse()
        {
            var (sim, room) = Encounter(EncounterKind.Gambler, players: 1);
            var p = sim.Players[0];
            int purse = sim.BonusShards;
            Run(sim, 2f, s => Stand(s, p, RunSimulation.ChoiceSpot(room.Bounds, 1)));
            Assert.AreEqual(purse + RunSimulation.DeclineShards, sim.BonusShards);

            var (font, fontRoom) = Encounter(EncounterKind.BloodFont, players: 1);
            var q = font.Players[0];
            q.CurseStacks[0] = 2;
            q.SetCurses(q.CurseStacks);
            Run(font, 2f, s => Stand(s, q, RunSimulation.ChoiceSpot(fontRoom.Bounds, 1)));
            Assert.AreEqual(1, q.CurseStacks[0]);
        }
    }
}
