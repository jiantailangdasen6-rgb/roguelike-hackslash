using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Combination techniques between classes, curse doors and the merchant.</summary>
    public class Round4Tests
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

        private static int Spawn(RunSimulation sim, EnemyType type, Vector2 at, float hpMul = 10f)
        {
            sim.Enemies.Spawn(type, at, hpMul, 1f, false);
            int i = sim.Enemies.Count - 1;
            sim.Enemies[i].Cooldown = 99f;
            return i;
        }

        private static void Press(RunSimulation sim, SimPlayer p, Vector2 aim, int skill1 = 0, int skill2 = 0) =>
            sim.SetPlayerInput(p.ClientId, new PlayerInput { Aim = aim, Skill1Count = (byte)skill1, Skill2Count = (byte)skill2 });

        private static bool HasCombo(IEnumerable<SimEvent> events, ComboKind kind) =>
            events.Any(e => e.Type == SimEventType.Combo && (ComboKind)e.Int == kind);

        // ------------------------------------------------------------------ Combination techniques

        [Test]
        public void FrostNovaOnBurningGround_BurstsIntoSteam()
        {
            var sim = new RunSimulation(31);
            var alchemist = sim.AddPlayer(0, new Loadout { classId = ClassId.Alchemist });
            var mage = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            alchemist.Position = new Vector2(0f, -4f);
            mage.Position = new Vector2(-5f, 0f);
            int enemy = Spawn(sim, EnemyType.Skeleton, Vector2.zero);

            Press(sim, alchemist, new Vector2(0f, 4f), skill1: 1);
            Run(sim, 1f);
            Press(sim, mage, new Vector2(5f, 0f), skill1: 1);
            var events = Run(sim, 1.2f);

            Assert.That(HasCombo(events, ComboKind.SteamBurst), "steam burst");
            Assert.Greater(sim.Enemies[enemy].Stunned, 0.5f, "steam stuns");
            Assert.LessOrEqual(sim.Enemies[enemy].Frozen, 0f, "the ice melted into steam");
        }

        [Test]
        public void ShadowStepOntoMarkedEnemy_Executes_AndTheStepIsReadyAgain()
        {
            var sim = new RunSimulation(32);
            var assassin = sim.AddPlayer(0, new Loadout { classId = ClassId.Assassin });
            var ranger = sim.AddPlayer(1, new Loadout { classId = ClassId.Ranger });
            assassin.Position = Vector2.zero;
            ranger.Position = new Vector2(-6f, 0f);
            int marked = Spawn(sim, EnemyType.Skeleton, new Vector2(5f, 0f), 40f);
            int plain = Spawn(sim, EnemyType.Skeleton, new Vector2(5f, 0.6f), 40f);
            sim.ApplyStatus(marked, StatusKind.Marked, 8f, ranger);
            float markedHp = sim.Enemies[marked].Health, plainHp = sim.Enemies[plain].Health;

            Press(sim, assassin, new Vector2(5f, 0f), skill1: 1);
            sim.Step(Dt);
            assassin.Position = new Vector2(4.6f, 0.3f); // the owner arrived (moves itself)
            var events = Run(sim, 0.3f, s => Press(s, assassin, Vector2.right, skill1: 1));

            Assert.That(HasCombo(events, ComboKind.ShadowExecution), "execution");
            Assert.LessOrEqual(assassin.Skill1Cooldown, 0.001f, "shadow step ready again");
            Assert.Greater(markedHp - sim.Enemies[marked].Health, (plainHp - sim.Enemies[plain].Health) * 1.5f, "the marked one is cut far deeper");
        }

        [Test]
        public void WarCryInsideHolyGround_ShieldsEveryoneInIt()
        {
            var sim = new RunSimulation(33);
            var guardian = sim.AddPlayer(0, new Loadout { classId = ClassId.Guardian });
            var warrior = sim.AddPlayer(1, new Loadout { classId = ClassId.Warrior });
            guardian.Position = Vector2.zero;
            warrior.Position = new Vector2(1f, 0f);
            Spawn(sim, EnemyType.Skeleton, new Vector2(12f, 6f)); // keeps the room from counting as cleared

            Press(sim, guardian, Vector2.right, skill2: 1);
            Run(sim, Dt * 2);
            guardian.Shield = 0f;
            Press(sim, warrior, Vector2.right, skill2: 1);
            var events = Run(sim, Dt * 2);

            Assert.That(HasCombo(events, ComboKind.HolyWarCry), "holy war cry");
            Assert.GreaterOrEqual(guardian.Shield, RunSimulation.HolyWarCryShield - 0.01f, "the guardian on the holy ground is shielded too");
        }

        [Test]
        public void ShieldChargeThroughBurningGround_SetsTheHitAflame()
        {
            var sim = new RunSimulation(34);
            var alchemist = sim.AddPlayer(0, new Loadout { classId = ClassId.Alchemist });
            var guardian = sim.AddPlayer(1, new Loadout { classId = ClassId.Guardian });
            alchemist.Position = new Vector2(0f, -4f);
            guardian.Position = new Vector2(8f, 6f);
            int target = Spawn(sim, EnemyType.Skeleton, new Vector2(0.8f, 0f)); // also keeps the room from clearing (which puts out fires)
            Press(sim, alchemist, new Vector2(0f, 4f), skill1: 1);
            Run(sim, 1f);

            guardian.Position = Vector2.zero; // standing in the fire
            sim.Enemies[target].Position = new Vector2(0.8f, 0f); // it wandered off while the flask flew
            Press(sim, guardian, Vector2.right * 3f, skill1: 1);
            var events = Run(sim, 0.2f, s => Press(s, guardian, Vector2.right * 3f, skill1: 1));

            Assert.That(HasCombo(events, ComboKind.FlameCharge), "flame charge");
            Assert.Greater(sim.Enemies[target].Burning, 1f, "the bashed enemy burns");
        }

        [Test]
        public void ComboCatalog_ListsOnlyWhatThePartyCanDo()
        {
            var party = new List<ClassId> { ClassId.Mage, ClassId.Alchemist, ClassId.Warrior };
            var names = ComboCatalog.Available(party).Select(c => c.Kind).ToList();
            CollectionAssert.AreEquivalent(new[] { ComboKind.SteamBurst }, names);
            Assert.IsEmpty(ComboCatalog.Available(new List<ClassId> { ClassId.Warrior }));
            foreach (ComboKind kind in System.Enum.GetValues(typeof(ComboKind)))
                Assert.IsNotEmpty(ComboCatalog.Name(kind), kind.ToString());
        }

        // ------------------------------------------------------------------ Curse doors and the merchant

        /// <summary>Everyone waits at the first door until the next room starts.</summary>
        [Test]
        public void DoorPack_KeepsTheCurse()
        {
            var doors = new[] { new Door(RoomReward.Boon, true), new Door(RoomReward.Cursed, false, CurseKind.Thirst), new Door(RoomReward.Shop, false) };
            var back = Door.Unpack(Door.Pack(doors));
            Assert.AreEqual(3, back.Length);
            Assert.AreEqual(RoomReward.Cursed, back[1].Reward);
            Assert.AreEqual(CurseKind.Thirst, back[1].Curse);
            Assert.AreEqual(RoomReward.Shop, back[2].Reward);
            Assert.IsTrue(back[0].Trial);
        }

    }
}
