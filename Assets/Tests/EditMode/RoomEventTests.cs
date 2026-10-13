using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Room surprises: the mimic, the captive adventurer, the wandering spirit.</summary>
    public class RoomEventTests
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

        private static int Find(RunSimulation sim, EnemyType type, System.Func<Enemy, bool> also = null)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Type == type && (also == null || also(sim.Enemies[i]))) return i;
            return -1;
        }

        [Test]
        public void AMimicChestWakesWhenHit_AndLeavesAHoard()
        {
            var sim = new RunSimulation(5) { MimicOdds = 1f };
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(2); // the campfire
            Run(sim, 0.2f);
            int chest = Find(sim, EnemyType.TreasureChest, e => e.Disguised);
            Assert.GreaterOrEqual(chest, 0, "one chest is a mimic");

            sim.DamageEnemy(chest, 5f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.2f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.RoomEvent && (RoomEventKind)e.Int == RoomEventKind.MimicRevealed));
            int mimic = Find(sim, EnemyType.Mimic);
            Assert.GreaterOrEqual(mimic, 0);
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.LootDropped), "the false chest held nothing");
            Assert.IsTrue(sim.RoomStates[sim.Map.Campfire.Id].Fighting, "a fight until it falls");

            sim.DamageEnemy(mimic, 99999f, p, HitFlags.DamageOverTime, Vector2.zero, 0f);
            events = Run(sim, 0.2f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.RoomEvent && (RoomEventKind)e.Int == RoomEventKind.MimicSlain));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.LootDropped && (Rarity)e.Int >= Rarity.Rare), "rare gear");
            Assert.IsFalse(sim.RoomStates[sim.Map.Campfire.Id].Fighting);
        }

        [Test]
        public void EveryAreaHasACaptive_AndBreakingTheCageSetsThemFree()
        {
            for (ulong seed = 1; seed < 20; seed++)
                Assert.AreEqual(1, DungeonMap.Generate(seed, 0).Rooms.Count(r => r.Captive), $"seed {seed}");

            var sim = new RunSimulation(8);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start();
            var room = sim.Map.Rooms.Find(r => r.Captive);
            Assert.IsFalse(room.Dark, "not the dark hall");
            sim.MoveTeamTo(room);
            Run(sim, 0.2f, s => p.Hp = p.MaxHp);
            int cage = Find(sim, EnemyType.Captive);
            Assert.GreaterOrEqual(cage, 0);
            sim.DamageEnemy(cage, 9999f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.2f, s => p.Hp = p.MaxHp);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.RoomEvent && (RoomEventKind)e.Int == RoomEventKind.CaptiveFreed));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.LootDropped && e.ClientId == p.ClientId), "a thank-you gift");
        }

        [Test]
        public void TheSpiritTakesATenthOfMaxHp_ForABlessing()
        {
            RunSimulation sim = null;
            MapRoom room = null;
            for (ulong seed = 1; seed < 300 && room == null; seed++)
            {
                sim = new RunSimulation(seed);
                var map = DungeonMap.Generate(sim.MapSeed, 1);
                room = map.Rooms.Find(r => r.Kind == RoomKind.Spirit);
            }
            Assert.IsNotNull(room, "some area-2 maps have a spirit");
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            sim.Start(RunSimulation.RoomsPerArea); // area 2
            room = sim.Map.Rooms.Find(r => r.Kind == RoomKind.Spirit);
            float before = p.MaxHp;
            sim.MoveTeamTo(room);
            var events = Run(sim, 2f, s => p.Position = RunSimulation.PedestalPosition(room.Bounds));
            Assert.AreEqual(1, p.Offerings);
            Assert.Less(p.MaxHp, before * 0.95f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.PerkOffer && (ChoiceKind)e.Int == ChoiceKind.Blessing));
        }
    }
}
