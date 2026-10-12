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
            for (int i = 0; i < 5 && host.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
            var fight = sim.FindRoom(RoomKind.Combat);
            sim.MoveTeamTo(fight);
            for (int i = 0; i < 3; i++) { host.InvulnerableTimer = 9f; sim.Step(1f / 30f); }
            SetTeamLevel(sim, 4);
            host.Position = fight.Origin + new Vector2(3f, 2f);

            var ranger = sim.JoinMidRun(9, new Loadout { classId = ClassId.Ranger, playerKey = "late" }, out bool returning);
            Assert.IsFalse(returning);
            Assert.AreEqual(4, ranger.Level);
            Assert.AreEqual(3, ranger.PicksLeft, "one pick per level gained before joining, chosen between rooms");
            Assert.IsNull(ranger.PendingOffer, "not mid-fight");
            Assert.AreEqual(ranger.MaxHp, ranger.Hp, 0.01f);
            Assert.Less((ranger.Position - host.Position).magnitude, 2f);
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
