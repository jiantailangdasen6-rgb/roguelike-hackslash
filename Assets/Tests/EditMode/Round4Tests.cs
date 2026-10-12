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

        private static void ClearCurrentRoom(RunSimulation sim)
        {
            for (int i = 0; i < 40 && !sim.Room.Cleared; i++)
            {
                for (int k = 0; k < sim.Enemies.Count; k++)
                    sim.DamageEnemy(k, 999999f, sim.Players[0], HitFlags.None, Vector2.zero, 0f);
                Run(sim, sim.Room.Objective == RoomObjective.Survive ? 10f : 1.2f, s =>
                {
                    foreach (var p in s.Players)
                    {
                        p.InvulnerableTimer = 1f;
                        if (p.PendingOffer != null) s.ChoosePerk(p.ClientId, p.OfferKind == ChoiceKind.Shop ? RunSimulation.ShopSkip : 0);
                    }
                });
            }
            Assert.IsTrue(sim.Room.Cleared, "room should clear");
        }

        /// <summary>Everyone waits at the first door until the next room starts.</summary>
        private static List<SimEvent> WalkThrough(RunSimulation sim, params Door[] doors)
        {
            sim.Room.Doors.Clear();
            sim.Room.Doors.AddRange(doors);
            var events = new List<SimEvent>();
            int room = sim.Room.Index;
            for (int i = 0; i < 200 && sim.Room.Index == room; i++)
            {
                foreach (var p in sim.Players)
                {
                    sim.SetPlayerPosition(p.ClientId, RunSimulation.DoorPosition(0, doors.Length, sim.Bounds));
                    p.InvulnerableTimer = 1f;
                }
                sim.Step(Dt);
                if (sim.Room.Index == room)
                {
                    foreach (var p in sim.Players)
                        if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
                    sim.Events.Clear();
                    continue;
                }
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            Assert.AreEqual(room + 1, sim.Room.Index, "walked through the door");
            return events;
        }

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

        [Test]
        public void CurseDoor_GivesTwoPicks_AndTheCurseLasts()
        {
            var sim = new RunSimulation(35);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            ClearCurrentRoom(sim);

            var events = WalkThrough(sim, new Door(RoomReward.Cursed, false, CurseKind.Frailty));

            float cursed = a.MaxHp;
            a.SetCurses(new int[RewardCatalog.CurseKinds]);
            float uncursed = a.MaxHp;
            a.SetCurses(sim.CurseStacks);
            Assert.AreEqual(uncursed * (1f - RewardCatalog.FrailtyPerStack), cursed, 0.5f, "frailty: max HP down");
            Assert.AreEqual(1, sim.CurseStacks[(int)CurseKind.Frailty]);
            Assert.That(events.Any(e => e.Type == SimEventType.RewardGranted && (RoomReward)e.Int == RoomReward.Cursed));
            int boons = a.StockedChoices.Count(k => k == ChoiceKind.Boon) + (a.PendingOffer != null && a.OfferKind == ChoiceKind.Boon ? 1 : 0);
            Assert.AreEqual(RewardCatalog.CursedBoons, boons, "two picks for the price");

            // Thirst halves healing.
            var p = new SimPlayer(5, Vector2.zero, new Loadout { classId = ClassId.Mage });
            p.SetCurses(new[] { 0, 0, 1, 0 });
            p.Hp = 10f;
            p.Heal(20f);
            Assert.AreEqual(20f, p.Hp, 0.01f);
        }

        [Test]
        public void Merchant_SellsForTheRunsShards_OrLetsYouWalkAway()
        {
            var sim = new RunSimulation(36);
            var buyer = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var saver = sim.AddPlayer(1, new Loadout { classId = ClassId.Ranger });
            sim.Start();
            ClearCurrentRoom(sim);
            WalkThrough(sim, new Door(RoomReward.Shards, false));
            ClearCurrentRoom(sim);

            var events = WalkThrough(sim, new Door(RoomReward.Shop, false));
            Assert.That(events.Any(e => e.Type == SimEventType.RewardGranted && (RoomReward)e.Int == RoomReward.Shop));
            // Any banked level-up picks shown first are answered; then the merchant waits for each player.
            foreach (var p in new[] { buyer, saver })
                for (int i = 0; i < 10 && p.PendingOffer != null && p.OfferKind != ChoiceKind.Shop; i++) sim.ChoosePerk(p.ClientId, 0);
            Assert.AreEqual(ChoiceKind.Shop, buyer.OfferKind);
            Assert.AreEqual(ChoiceKind.Shop, saver.OfferKind);

            int purse = sim.Purse;
            var ware = buyer.PendingOffer[0];
            int price = RewardCatalog.Price(ware, sim.Room.Area);
            Assert.GreaterOrEqual(purse, price, "two rooms and a shard door pay for one perk");
            int perksBefore = PerkCatalog.All.Sum(d => buyer.PerkLevel(d.Id));
            sim.ChoosePerk(buyer.ClientId, 0);
            Assert.AreEqual(purse - price, sim.Purse, "paid from the team's shards");
            if (ware.IsPerk) Assert.AreEqual(perksBefore + 1, PerkCatalog.All.Sum(d => buyer.PerkLevel(d.Id)));

            int left = sim.Purse;
            sim.ChoosePerk(saver.ClientId, RunSimulation.ShopSkip);
            Assert.AreEqual(left, sim.Purse, "walking away costs nothing");
            Assert.IsNull(saver.PendingOffer);
        }

        [Test]
        public void DealDoors_NeverInTheFirstArea()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = new RunSimulation(seed);
                sim.AddPlayer(0);
                sim.Start();
                ClearCurrentRoom(sim);
                Assert.That(sim.Room.Doors.All(d => d.Reward != RoomReward.Cursed && d.Reward != RoomReward.Shop), $"seed {seed}: none in the first area");
            }
        }
    }
}
