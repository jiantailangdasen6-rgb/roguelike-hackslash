using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class UpgradeTests
    {
        [Test]
        public void PackUnpack_RoundTrips()
        {
            var options = new[]
            {
                new UpgradeOption(UpgradeKind.NewWeapon, 2, 1),
                new UpgradeOption(UpgradeKind.Passive, 7, 5),
                new UpgradeOption(UpgradeKind.WeaponLevel, 0, 4),
            };
            var unpacked = UpgradeOption.UnpackAll(UpgradeOption.Pack(options));
            CollectionAssert.AreEqual(options, unpacked);
        }

        [Test]
        public void Roll_GivesThreeDistinctValidOptions()
        {
            var rng = new GameRandom(5);
            var player = new SimPlayer(0, Vector2.zero);
            // 30 picks: well below the ~43 upgrades available, so 3 options always exist.
            for (int i = 0; i < 30; i++)
            {
                var options = UpgradeRoller.Roll(rng, player);
                Assert.AreEqual(3, options.Length);
                Assert.AreEqual(3, options.Distinct().Count());
                UpgradeRoller.Apply(player, options[0]);
            }
            Assert.That(player.Weapons.All(w => w.Level <= WeaponCatalog.Get(w.Id).MaxLevel));
            Assert.That(player.PassiveLevels.All(l => l <= PassiveCatalog.MaxLevel));
        }

        [Test]
        public void EverythingMaxed_OffersHeal()
        {
            var player = new SimPlayer(0, Vector2.zero);
            foreach (var w in WeaponCatalog.All) player.AddWeapon(w.Id);
            foreach (var w in player.Weapons) w.Level = WeaponCatalog.Get(w.Id).MaxLevel;
            foreach (var p in PassiveCatalog.All) player.SetPassiveLevel(p.Id, PassiveCatalog.MaxLevel);

            var options = UpgradeRoller.Roll(new GameRandom(1), player);
            Assert.AreEqual(UpgradeKind.Heal, options.Single().Kind);
        }
    }

    public class EnemySimulationTests
    {
        [Test]
        public void Slime_MovesTowardPlayer()
        {
            var sim = new EnemySimulation(RunSimulation.DefaultBounds);
            sim.Spawn(EnemyType.Slime, new Vector2(10f, 0f), 1f, false);
            var players = new List<SimPlayer> { new(0, Vector2.zero) };

            for (int i = 0; i < 30; i++) sim.Step(1f / 30f, players, new List<HostileShot>());

            Assert.Less(sim[0].Position.x, 9f);
        }

        [Test]
        public void QueryCircle_FindsOnlyNearbyEnemies_AndRemoveDeadCompacts()
        {
            var sim = new EnemySimulation(RunSimulation.DefaultBounds);
            sim.Spawn(EnemyType.Slime, new Vector2(1f, 0f), 1f, false);
            sim.Spawn(EnemyType.Slime, new Vector2(15f, 0f), 1f, false);
            var hits = new List<int>();

            sim.QueryCircle(Vector2.zero, 1f, hits);
            Assert.AreEqual(1, hits.Count);

            sim[hits[0]].Dead = true;
            sim.RemoveDead();
            Assert.AreEqual(1, sim.Count);
            Assert.AreEqual(15f, sim[0].Position.x, 0.01f);
        }

        [Test]
        public void Archer_FiresAtPlayer()
        {
            var sim = new EnemySimulation(RunSimulation.DefaultBounds);
            sim.Spawn(EnemyType.GoblinArcher, new Vector2(6f, 0f), 1f, false);
            var players = new List<SimPlayer> { new(0, Vector2.zero) };
            var shots = new List<HostileShot>();

            for (int i = 0; i < 90 && shots.Count == 0; i++) sim.Step(1f / 30f, players, shots);

            Assert.IsNotEmpty(shots);
            Assert.Less(shots[0].Velocity.x, 0f); // toward the player
        }
    }

    public class RunSimulationTests
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

        private static void KeepAlive(RunSimulation sim)
        {
            foreach (var p in sim.Players) p.InvulnerableTimer = 1f;
        }

        private static void AutoPick(RunSimulation sim)
        {
            foreach (var p in sim.Players)
                if (p.PendingOffer != null) sim.ChooseUpgrade(p.ClientId, 0);
        }

        [Test]
        public void SoloRun_SpawnsEnemies_KillsThem_AndLevelsUp()
        {
            var sim = new RunSimulation(1);
            sim.AddPlayer(0);
            sim.Start();

            var events = Run(sim, 40f, s => { KeepAlive(s); AutoPick(s); });

            Assert.Greater(events.Count(e => e.Type == SimEventType.EnemyDied), 10);
            Assert.Greater(events.Count(e => e.Type == SimEventType.LevelUpOffer), 0);
            Assert.Greater(sim.TeamLevel, 1);
        }

        [Test]
        public void LevelUp_PausesUntilEveryPlayerChooses()
        {
            var sim = new RunSimulation(2);
            sim.AddPlayer(0);
            sim.AddPlayer(1);
            sim.Start();

            // Run until the first offer, without picking.
            while (!sim.AwaitingChoices) { KeepAlive(sim); sim.Step(Dt); }

            float elapsed = sim.Elapsed;
            sim.Step(Dt);
            Assert.AreEqual(elapsed, sim.Elapsed, "simulation should be paused");

            sim.ChooseUpgrade(0, 0);
            Assert.IsTrue(sim.AwaitingChoices, "still waiting for player 1");
            sim.ChooseUpgrade(1, 0);
            Assert.IsTrue(!sim.AwaitingChoices || sim.PendingLevelUps > 0);
        }

        [Test]
        public void LevelUp_AutoPicksAfterTimeout()
        {
            var sim = new RunSimulation(3);
            sim.AddPlayer(0);
            sim.Start();
            while (!sim.AwaitingChoices) { KeepAlive(sim); sim.Step(Dt); }

            Run(sim, RunSimulation.ChoiceTimeout + 1f);

            Assert.IsNull(sim.Players[0].PendingOffer);
        }

        [Test]
        public void DownedPlayer_IsRevivedByNearbyTeammate()
        {
            var sim = new RunSimulation(4);
            var downed = sim.AddPlayer(0);
            var helper = sim.AddPlayer(1);
            sim.Start();
            downed.Hp = 0f;
            downed.Downed = true;
            helper.Position = downed.Position + new Vector2(0.5f, 0f);

            var events = Run(sim, RunSimulation.ReviveTime + 0.5f, s => s.GetPlayer(1).InvulnerableTimer = 1f);

            Assert.IsFalse(downed.Downed);
            Assert.That(events.Any(e => e.Type == SimEventType.PlayerRevived && e.ClientId == 0));
        }

        [Test]
        public void AllPlayersDowned_EndsRunInDefeat()
        {
            var sim = new RunSimulation(5);
            sim.AddPlayer(0);
            sim.Start();
            sim.Players[0].Hp = 1f;

            // Drop enemies on top of the player.
            for (int i = 0; i < 5; i++) sim.Enemies.Spawn(EnemyType.Slime, new Vector2(0.2f * i, 0f), 100f, false);
            var events = Run(sim, 1f);

            Assert.AreEqual(RunPhase.Defeat, sim.Phase);
            Assert.That(events.Any(e => e.Type == SimEventType.RunEnded && e.Int == 0));
        }

        [Test]
        public void SurviveRoom_Clears_ThenExitLeadsToNextRoom()
        {
            var sim = new RunSimulation(6);
            sim.AddPlayer(0);
            sim.Start();
            Assert.AreEqual(RoomObjective.Survive, sim.Room.Objective);

            var events = Run(sim, sim.Room.Target + 2f, s => { KeepAlive(s); AutoPick(s); });
            Assert.IsTrue(sim.Room.Cleared);
            Assert.AreEqual(0, sim.Enemies.Count, "remaining enemies are purged on clear");
            Assert.That(events.Any(e => e.Type == SimEventType.RoomCleared));

            events = Run(sim, RunSimulation.ExitHoldTime + 0.5f, s =>
            {
                KeepAlive(s);
                AutoPick(s);
                if (s.Room.Index == 0) s.SetPlayerPosition(0, s.Room.ExitPosition);
            });

            Assert.AreEqual(1, sim.Room.Index);
            Assert.AreEqual(RoomObjective.Kill, sim.Room.Objective);
            Assert.That(events.Any(e => e.Type == SimEventType.Teleport && e.ClientId == 0));
        }

        [Test]
        public void Greatsword_HitsEnemyBehindAMovingPlayer()
        {
            var sim = new RunSimulation(7);
            sim.AddPlayer(0);
            sim.Start();
            sim.SetPlayerPosition(0, new Vector2(0f, -1f)); // moving down -> facing down
            sim.SetPlayerPosition(0, new Vector2(0f, -1.1f));
            sim.Enemies.Spawn(EnemyType.Slime, new Vector2(0f, 0.5f), 1f, false); // behind (above) the player

            var events = Run(sim, 0.2f);

            Assert.That(events.Any(e => e.Type == SimEventType.EnemyDied));
        }

        [Test]
        public void MorePlayers_MeansTougherEnemies()
        {
            Assert.Greater(RunSimulation.HealthMultiplier(0, 3), RunSimulation.HealthMultiplier(0, 1));
            Assert.Greater(RunSimulation.XpRequired(5, 3), RunSimulation.XpRequired(5, 1));
        }
    }
}
