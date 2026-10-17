using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Picks mid-fight: offered at once, and no time-out until the room is quiet.</summary>
    public class PickTests
    {
        private const float Dt = 1f / 30f;

        [Test]
        public void ALevelUpMidFight_IsOfferedAtOnce_AndWaits()
        {
            var sim = new RunSimulation(43);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start();
            for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
            var fight = sim.FindRoom(RoomKind.Combat);
            sim.MoveTeamTo(fight);
            for (int i = 0; i < 10; i++) { p.InvulnerableTimer = 9f; sim.Step(Dt); }
            Assert.IsFalse(sim.CanChoose(p), "the fight is on");

            p.StockedChoices.Enqueue(ChoiceKind.LevelUp);
            for (int i = 0; i < 3; i++) { p.InvulnerableTimer = 9f; sim.Step(Dt); }
            Assert.IsNotNull(p.PendingOffer, "offered mid-fight");
            for (float t = 0; t < RunSimulation.ChoiceTimeout + 5f; t += Dt)
            {
                p.InvulnerableTimer = 9f;
                sim.Step(Dt);
                if (sim.CanChoose(p)) break;
            }
            if (!sim.CanChoose(p)) Assert.IsNotNull(p.PendingOffer, "no time-out while fighting");
        }
    }
}
