using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Joining a run in progress and coming back after dropping out.</summary>
    public class SessionTests
    {
        private static void SetTeamLevel(RunSimulation sim, int level) =>
            typeof(RunSimulation).GetProperty(nameof(RunSimulation.TeamLevel)).GetSetMethod(true).Invoke(sim, new object[] { level });

        [Test]
        public void Rejoin_RestoresTheSameCharacter()
        {
            var sim = new RunSimulation(70);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior, playerKey = "host" });
            var mage = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage, playerKey = "friend" });
            sim.Start();
            PerkRoller.Apply(mage, PerkRoller.Roll(new GameRandom(1), mage)[0]);
            int perks = mage.PerkLevels.Sum();
            mage.Hp = 40f;

            sim.DetachPlayer(1, "friend");
            Assert.IsNull(sim.GetPlayer(1));
            Assert.AreEqual(1, sim.Players.Count);

            var back = sim.JoinMidRun(5, new Loadout { classId = ClassId.Mage, playerKey = "friend" }, out bool returning);
            Assert.IsTrue(returning);
            Assert.AreSame(mage, back);
            Assert.AreEqual(5ul, back.ClientId);
            Assert.AreEqual(perks, back.PerkLevels.Sum(), "perks are kept");
            Assert.AreEqual(40f, back.Hp, 0.01f, "HP is kept too");
            Assert.AreEqual(2, sim.Players.Count);
        }

        [Test]
        public void Newcomer_CatchesUpToTheTeamLevel_NextToATeammate()
        {
            var sim = new RunSimulation(71);
            var host = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior, playerKey = "host" });
            sim.Start();
            SetTeamLevel(sim, 4);
            host.Position = new Vector2(3f, 2f);

            var ranger = sim.JoinMidRun(9, new Loadout { classId = ClassId.Ranger, playerKey = "late" }, out bool returning);
            Assert.IsFalse(returning);
            Assert.AreEqual(4, ranger.Level);
            Assert.AreEqual(3, ranger.PicksLeft, "one pick per level gained before joining, chosen between rooms");
            Assert.IsNull(ranger.PendingOffer, "not mid-fight");
            Assert.AreEqual(ranger.MaxHp, ranger.Hp, 0.01f);
            Assert.Less((ranger.Position - host.Position).magnitude, 2f);
        }

        [Test]
        public void DoorReward_HoldsTheNextRoomUntilEveryoneHasPicked()
        {
            var sim = new RunSimulation(73);
            sim.AddPlayer(0);
            sim.AddPlayer(1);
            sim.Start();
            sim.Room.Cleared = true;
            sim.Room.Doors.Clear();
            sim.Room.Doors.Add(new Door(RoomReward.Boon, false));
            sim.Room.Doors.Add(new Door(RoomReward.Shards, false));
            for (int i = 0; i < 120 && sim.Room.Index == 0; i++)
            {
                foreach (var p in sim.Players)
                {
                    p.InvulnerableTimer = 1f;
                    sim.SetPlayerPosition(p.ClientId, RunSimulation.DoorPosition(0, 2, sim.Bounds));
                    if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
                }
                sim.Step(1f / 30f);
            }
            Assert.AreEqual(1, sim.Room.Index);
            Assert.IsTrue(sim.HoldingForPicks, "the boon is chosen before the fight");
            Assert.IsNotNull(sim.Players[0].PendingOffer);

            float elapsed = sim.Elapsed;
            for (int i = 0; i < 90; i++) sim.Step(1f / 30f);
            Assert.AreEqual(elapsed, sim.Elapsed, 0.0001f, "the room waits");
            Assert.AreEqual(0, sim.Enemies.HostileCount + sim.PendingSpawns.Count, "no monsters yet");

            foreach (var p in sim.Players) while (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
            sim.Step(1f / 30f);
            Assert.IsFalse(sim.HoldingForPicks);
            Assert.Greater(sim.Elapsed, elapsed, "and then the fight starts");
        }

        [Test]
        public void JoinMidRun_IsIdempotent()
        {
            var sim = new RunSimulation(72);
            sim.AddPlayer(0);
            sim.Start();
            var a = sim.JoinMidRun(3, new Loadout { playerKey = "x" }, out _);
            var b = sim.JoinMidRun(3, new Loadout { playerKey = "x" }, out _);
            Assert.AreSame(a, b);
            Assert.AreEqual(2, sim.Players.Count);
        }
    }
}
