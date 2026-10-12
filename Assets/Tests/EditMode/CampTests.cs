using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The camp's layout, the run's pacing (rest rooms) and the camp's requests.</summary>
    public class CampTests
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

        private static void KeepAliveAndPick(RunSimulation sim)
        {
            foreach (var p in sim.Players)
            {
                p.InvulnerableTimer = 1f;
                if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
            }
        }

        // ------------------------------------------------------------------ Camp

        [Test]
        public void Camp_StationsAreReachable_AndNotInsideObstacles()
        {
            foreach (var s in Camp.Stations)
            {
                Assert.IsTrue(Camp.Bounds.Contains(s.Front), $"{s.Name} stands outside the camp");
                Assert.IsFalse(Arena.Overlaps(Camp.Layout, s.Front, 0.35f), $"{s.Name}: the spot to use it is blocked");
                if (s.Id == CampStation.Gate) Assert.IsTrue(Camp.InGate(s.Front));
                else Assert.AreEqual(s.Id, Camp.StationAt(s.Front)?.Id, $"{s.Name} can't be used from its own spot");
            }
            for (int slot = 0; slot < 3; slot++)
            {
                var spawn = Camp.SpawnPoint(slot);
                Assert.IsFalse(Arena.Overlaps(Camp.Layout, spawn, 0.35f), $"slot {slot} spawns inside something");
                Assert.IsNull(Camp.StationAt(spawn), "players shouldn't appear on a station");
                Assert.IsFalse(Camp.InGate(spawn));
            }
        }

        // ------------------------------------------------------------------ Pace

        // ------------------------------------------------------------------ Requests

        [Test]
        public void Quests_CountTheRightEvents()
        {
            var bones = QuestCatalog.Chains[(int)QuestGiver.Master][0];
            var skeletonDied = new SimEvent(SimEventType.EnemyDied) { Int = (int)EnemyType.Skeleton };
            var crumbled = new SimEvent(SimEventType.EnemyDied) { Int = (int)EnemyType.Skeleton, Value = 1f };
            var ghoulDied = new SimEvent(SimEventType.EnemyDied) { Int = (int)EnemyType.Ghoul };
            Assert.AreEqual(1, QuestCatalog.Amount(bones, skeletonDied));
            Assert.AreEqual(0, QuestCatalog.Amount(bones, crumbled));
            Assert.AreEqual(0, QuestCatalog.Amount(bones, ghoulDied));

            var shatter = QuestCatalog.Chains[(int)QuestGiver.Master][1];
            Assert.AreEqual(1, QuestCatalog.Amount(shatter, new SimEvent(SimEventType.EnemyHit) { Int = (int)(HitFlags.Shatter | HitFlags.Heavy) }));
            Assert.AreEqual(0, QuestCatalog.Amount(shatter, new SimEvent(SimEventType.EnemyHit) { Int = (int)HitFlags.Heavy }));

            var rest = QuestCatalog.Chains[(int)QuestGiver.Gravekeeper][0];
            Assert.AreEqual(1, QuestCatalog.Amount(rest, new SimEvent(SimEventType.RoomEntered) { Value = (int)RoomKind.Campfire }));
            Assert.AreEqual(0, QuestCatalog.Amount(rest, new SimEvent(SimEventType.RoomEntered) { Value = (int)RoomKind.Combat }));
            var chapel = QuestCatalog.Chains[(int)QuestGiver.Gravekeeper][1];
            Assert.AreEqual(0, QuestCatalog.Amount(chapel, new SimEvent(SimEventType.RoomStarted) { Int = 3 }));
            Assert.AreEqual(1, QuestCatalog.Amount(chapel, new SimEvent(SimEventType.RoomStarted) { Int = 4 }));
        }

        [Test]
        public void QuestLog_CompletesClaimsAndMovesAlong_ThenRepeatsForever()
        {
            var log = new QuestLog();
            var first = QuestCatalog.Current(log, QuestGiver.Master);
            Assert.AreEqual("m1", first.Id);
            Assert.IsFalse(log.Add(first, first.Target - 1));
            Assert.IsTrue(log.Add(first, 5), "should complete");
            Assert.AreEqual(first.Target, log.Progress(first), "progress is capped");
            Assert.IsFalse(log.Add(first, 1), "done requests don't count further");

            log.Claim(first);
            Assert.AreEqual("m2", QuestCatalog.Current(log, QuestGiver.Master).Id);

            // Run through the whole chain: requests never run out, and each repeat starts from zero.
            for (int i = 0; i < 20; i++)
            {
                var q = QuestCatalog.Current(log, QuestGiver.Master);
                Assert.AreEqual(0, log.Progress(q), q.Id);
                log.Add(q, q.Target);
                log.Claim(q);
            }
            Assert.IsNotNull(QuestCatalog.Current(log, QuestGiver.Master));
            Assert.AreEqual(3, QuestCatalog.Active(log).Count());
        }

        [Test]
        public void QuestLog_SurvivesTheSaveFile()
        {
            var profile = ProfileData.Default();
            var q = QuestCatalog.Current(profile.quests, QuestGiver.Crone);
            profile.quests.Add(q, 2);
            profile.quests.Claim(QuestCatalog.Current(profile.quests, QuestGiver.Gravekeeper));
            var loaded = ProfileData.FromJson(profile.ToJson());
            Assert.AreEqual(2, loaded.quests.Progress(q));
            Assert.AreEqual(1, loaded.quests.Step(QuestGiver.Gravekeeper));

            // Old saves without requests load fine.
            var old = ProfileData.FromJson("{\"shards\":12}");
            Assert.IsNotNull(old.quests);
            Assert.AreEqual(0, old.quests.Step(QuestGiver.Master));
        }
    }
}
