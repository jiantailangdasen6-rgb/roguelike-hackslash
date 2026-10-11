using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Reward doors (room choice), trial rooms, new perks and trinkets.</summary>
    public class Round3Tests
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

        private static void ClearCurrentRoom(RunSimulation sim)
        {
            for (int i = 0; i < 40 && !sim.Room.Cleared; i++)
            {
                for (int k = 0; k < sim.Enemies.Count; k++)
                    sim.DamageEnemy(k, 999999f, sim.Players[0], HitFlags.None, Vector2.zero, 0f);
                if (sim.Room.Objective == RoomObjective.Survive) Run(sim, 10f, KeepAliveAndPick);
                else Run(sim, 1.2f, KeepAliveAndPick);
            }
            Assert.IsTrue(sim.Room.Cleared, "room should clear");
        }

        private static void SetDoors(RunSimulation sim, params Door[] doors)
        {
            sim.Room.Doors.Clear();
            sim.Room.Doors.AddRange(doors);
        }

        private static void StandAt(RunSimulation sim, int door, int count)
        {
            foreach (var p in sim.Players) sim.SetPlayerPosition(p.ClientId, RunSimulation.DoorPosition(door, count, sim.Bounds));
        }

        /// <summary>Everyone waits at one door until the next room starts; level-up picks before that are answered.</summary>
        private static List<SimEvent> WalkThrough(RunSimulation sim, int door, int count)
        {
            var events = new List<SimEvent>();
            int room = sim.Room.Index;
            for (int i = 0; i < 200 && sim.Room.Index == room; i++)
            {
                StandAt(sim, door, count);
                foreach (var p in sim.Players) p.InvulnerableTimer = 1f;
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

        /// <summary>Stops wave spawns so combat tests only see the player's own effects.</summary>
        private static void Quiet(RunSimulation sim) => sim.Room.Cleared = true;

        // ------------------------------------------------------------------ Doors

        [Test]
        public void DoorPack_RoundTrips()
        {
            var doors = new[] { new Door(RoomReward.Vigor, false), new Door(RoomReward.Shards, true), new Door(RoomReward.Upgrade, false) };
            var back = Door.Unpack(Door.Pack(doors));
            Assert.AreEqual(3, back.Length);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(doors[i].Reward, back[i].Reward);
                Assert.AreEqual(doors[i].Trial, back[i].Trial);
            }
            Assert.AreEqual(0, Door.Unpack(0).Length);
        }

        [Test]
        public void DoorSpots_AreClearInEveryLayout()
        {
            foreach (var layout in Arena.Layouts)
                for (int count = 2; count <= Door.MaxCount; count++)
                    for (int i = 0; i < count; i++)
                    {
                        var at = RunSimulation.DoorPosition(i, count, RunSimulation.DefaultBounds);
                        Assert.IsFalse(Arena.Overlaps(layout, at, RunSimulation.ExitRadius), $"{layout.Name}: door {i}/{count} blocked");
                        Assert.IsTrue(RunSimulation.DefaultBounds.Contains(at));
                    }
        }

        [Test]
        public void ClearingARoom_OpensTwoOrThreeDistinctRewardDoors()
        {
            var sim = new RunSimulation(11);
            sim.AddPlayer(0);
            sim.Start();
            ClearCurrentRoom(sim);
            Assert.That(sim.Room.Doors.Count, Is.InRange(2, 3));
            Assert.AreEqual(sim.Room.Doors.Count, sim.Room.Doors.Select(d => d.Reward).Distinct().Count(), "rewards differ between doors");
            Assert.IsFalse(sim.Room.Doors.Any(d => d.Reward == RoomReward.Upgrade), "nothing to upgrade before any perk");
        }

        [Test]
        public void BeforeABoss_OneDoorAlwaysHeals_AndNoTrial()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var sim = new RunSimulation(seed);
                sim.AddPlayer(0);
                sim.Start(2); // the room before the first boss
                ClearCurrentRoom(sim);
                Assert.That(sim.Room.Doors.Any(d => d.Reward == RoomReward.Vigor), $"seed {seed}");
                Assert.IsFalse(sim.Room.Doors.Any(d => d.Trial), $"seed {seed}");
            }
        }

        [Test]
        public void SplitTeam_DoesNotLeave_SameDoorDoes()
        {
            var sim = new RunSimulation(12);
            sim.AddPlayer(0);
            sim.AddPlayer(1);
            sim.Start();
            ClearCurrentRoom(sim);
            SetDoors(sim, new Door(RoomReward.Shards, false), new Door(RoomReward.Vigor, false));

            Run(sim, RunSimulation.ExitHoldTime + 0.5f, s =>
            {
                KeepAliveAndPick(s);
                s.SetPlayerPosition(0, RunSimulation.DoorPosition(0, 2, s.Bounds));
                s.SetPlayerPosition(1, RunSimulation.DoorPosition(1, 2, s.Bounds));
            });
            Assert.AreEqual(0, sim.Room.Index, "players at different doors stay");
            Assert.AreEqual(-1, sim.Room.HoldDoor);

            var events = Run(sim, RunSimulation.ExitHoldTime + 0.3f, s =>
            {
                KeepAliveAndPick(s);
                if (s.Room.Index == 0) StandAt(s, 1, 2);
            });
            Assert.AreEqual(1, sim.Room.Index);
            Assert.That(events.Any(e => e.Type == SimEventType.RewardGranted && e.Int == (int)RoomReward.Vigor));
        }

        [Test]
        public void ShardDoor_AddsBonusShards_TrialDoubles_AndToughensTheRoom()
        {
            var sim = new RunSimulation(13);
            sim.AddPlayer(0);
            sim.Start();
            ClearCurrentRoom(sim);
            float normalHealth = sim.EnemyHealthScale;
            SetDoors(sim, new Door(RoomReward.Shards, true), new Door(RoomReward.Boon, false));
            var events = Run(sim, RunSimulation.ExitHoldTime + 0.3f, s =>
            {
                KeepAliveAndPick(s);
                if (s.Room.Index == 0) StandAt(s, 0, 2);
            });
            Assert.AreEqual(1, sim.Room.Index);
            Assert.IsTrue(sim.Room.Trial);
            Assert.AreEqual(RunSimulation.ShardReward(0) * 2, sim.BonusShards);
            Assert.That(events.Any(e => e.Type == SimEventType.RoomStarted && e.Value > 0.5f), "the room start says it's a trial");
            // Room 1 is tougher than room 0 anyway; the trial adds its own factor on top.
            Assert.AreEqual(RunSimulation.HealthMultiplier(1, 1) * RunSimulation.TrialHealth, sim.EnemyHealthScale, 0.001f);
            Assert.Greater(sim.EnemyHealthScale, normalHealth);
            Assert.Greater(sim.EliteChance, sim.DifficultyDef.EliteChance);
        }

        [Test]
        public void VigorDoor_RaisesMaxHp_AndHealsFully()
        {
            var sim = new RunSimulation(14);
            var p = sim.AddPlayer(0);
            sim.Start();
            ClearCurrentRoom(sim);
            float before = p.MaxHp;
            p.Hp = 10f;
            SetDoors(sim, new Door(RoomReward.Vigor, false), new Door(RoomReward.Shards, false));
            Run(sim, RunSimulation.ExitHoldTime + 0.3f, s =>
            {
                KeepAliveAndPick(s);
                if (s.Room.Index == 0) StandAt(s, 0, 2);
            });
            Assert.AreEqual(1, p.VigorStacks);
            Assert.Greater(p.MaxHp, before);
            Assert.AreEqual(p.MaxHp, p.Hp, 0.01f);
        }

        [Test]
        public void BoonDoor_OffersAPerkToEveryone_TaggedAsBoon()
        {
            var sim = new RunSimulation(15);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.AddPlayer(1, new Loadout { classId = ClassId.Ranger });
            sim.Start();
            ClearCurrentRoom(sim);
            SetDoors(sim, new Door(RoomReward.Boon, false), new Door(RoomReward.Shards, false));
            var events = WalkThrough(sim, 0, 2);
            var offers = events.Where(e => e.Type == SimEventType.PerkOffer).ToList();
            Assert.AreEqual(2, offers.Count, "one offer per player");
            Assert.That(offers.All(e => e.Int == (int)ChoiceKind.Boon), "door rewards come before any level-up");
            Assert.IsTrue(sim.AwaitingChoices, "the run pauses for the pick");
        }

        [Test]
        public void UpgradeOffer_OnlyRaisesOwnedPerks_OrFallsBackToABoon()
        {
            var rng = new GameRandom(5);
            var p = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Mage });
            Assert.IsNull(PerkRoller.RollUpgrade(rng, p), "no perks yet");
            p.AddPerk(PerkId.Fury);
            p.AddPerk(PerkId.SecondWind); // max level 1: can't be upgraded
            var offer = PerkRoller.RollUpgrade(rng, p);
            Assert.AreEqual(1, offer.Length);
            Assert.AreEqual((byte)PerkId.Fury, offer[0].Perk);
            Assert.AreEqual(2, offer[0].Level);

            var sim = new RunSimulation(16);
            sim.AddPlayer(0);
            sim.Start();
            ClearCurrentRoom(sim);
            SetDoors(sim, new Door(RoomReward.Upgrade, false), new Door(RoomReward.Shards, false));
            System.Array.Clear(sim.Players[0].PerkLevels, 0, PerkCatalog.Count); // forget level-up picks made while clearing
            var events = WalkThrough(sim, 0, 2);
            var o = events.FirstOrDefault(e => e.Type == SimEventType.PerkOffer);
            Assert.AreEqual(SimEventType.PerkOffer, o.Type);
            Assert.AreEqual((int)ChoiceKind.Boon, o.Int, "a player with nothing to upgrade gets a new perk instead");
        }

        [Test]
        public void TrialBoon_GivesTwoChoicesInARow()
        {
            var sim = new RunSimulation(17);
            sim.AddPlayer(0);
            sim.Start(1);
            ClearCurrentRoom(sim);
            SetDoors(sim, new Door(RoomReward.Boon, true), new Door(RoomReward.Vigor, false));
            int perksBefore = sim.Players[0].PerkLevels.Sum();
            int offers = 0;
            for (int i = 0; i < 200; i++)
            {
                if (sim.Room.Index == 1) StandAt(sim, 0, 2);
                sim.Players[0].InvulnerableTimer = 1f;
                sim.Step(Dt);
                foreach (var e in sim.Events)
                    if (e.Type == SimEventType.PerkOffer && e.Int == (int)ChoiceKind.Boon) offers++;
                sim.Events.Clear();
                if (sim.Players[0].PendingOffer != null) sim.ChoosePerk(0, 0);
            }
            Assert.AreEqual(2, offers);
            Assert.GreaterOrEqual(sim.Players[0].PerkLevels.Sum(), perksBefore + 2);
        }

        // ------------------------------------------------------------------ New perks and trinkets

        [Test]
        public void Executioner_HitsWoundedEnemiesHarder()
        {
            var sim = new RunSimulation(18, new Rect(-10f, -10f, 20f, 20f));
            var p = sim.AddPlayer(0);
            p.Stats.SetBase(StatType.CritChance, 0f);
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(3f, 0f), 50f, 1f, false);
            ref var e = ref sim.Enemies[0];
            e.Health = e.MaxHealth * 0.2f;
            float before = e.Health;
            sim.DamageEnemy(0, 10f, p, HitFlags.None, Vector2.zero, 0f);
            float plain = before - sim.Enemies[0].Health;

            p.AddPerk(PerkId.Executioner);
            before = sim.Enemies[0].Health;
            sim.DamageEnemy(0, 10f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(plain * 1.3f, before - sim.Enemies[0].Health, 0.01f);
        }

        [Test]
        public void Comradery_GrowsWithNearbyAllies()
        {
            var sim = new RunSimulation(19, new Rect(-10f, -10f, 20f, 20f));
            var a = sim.AddPlayer(0);
            var b = sim.AddPlayer(1);
            a.Stats.SetBase(StatType.CritChance, 0f);
            a.AddPerk(PerkId.Comradery);
            a.Position = Vector2.zero;
            b.Position = new Vector2(2f, 0f);
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(3f, 0f), 50f, 1f, false);
            float before = sim.Enemies[0].Health;
            sim.DamageEnemy(0, 10f, a, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(11f, before - sim.Enemies[0].Health, 0.01f);

            b.Position = new Vector2(9f, 0f);
            before = sim.Enemies[0].Health;
            sim.DamageEnemy(0, 10f, a, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(10f, before - sim.Enemies[0].Health, 0.01f, "a far-away ally doesn't count");
        }

        [Test]
        public void Blessing_ShieldsOnEveryRoomStart()
        {
            var sim = new RunSimulation(20);
            var p = sim.AddPlayer(0);
            p.AddPerk(PerkId.Blessing);
            p.AddPerk(PerkId.Blessing);
            sim.Start();
            Assert.AreEqual(RunSimulation.BlessingShield * 2, p.Shield, 0.01f);
        }

        [Test]
        public void FrostArmor_ShieldsAlliesNearTheNova()
        {
            var sim = new RunSimulation(21, new Rect(-10f, -10f, 20f, 20f));
            var mage = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            var ally = sim.AddPlayer(1, new Loadout { classId = ClassId.Warrior });
            Quiet(sim);
            mage.AddPerk(PerkId.FrostArmor);
            mage.Position = Vector2.zero;
            ally.Position = new Vector2(3f, 0f);
            sim.SetPlayerInput(0, new PlayerInput { Aim = new Vector2(3f, 0f), Skill1Count = 1 });
            Run(sim, 1f);
            Assert.AreEqual(15f, ally.Shield, 0.01f);
        }

        [Test]
        public void Earthshaker_StunsAroundTheEndOfTheDash()
        {
            var sim = new RunSimulation(22, new Rect(-10f, -10f, 20f, 20f));
            var w = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            Quiet(sim);
            w.AddPerk(PerkId.Earthshaker);
            w.Position = Vector2.zero;
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(0f, 2f), 50f, 1f, false); // beside, not in the dash path
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Skill1Count = 1 });
            var events = Run(sim, 0.6f, s => s.Players[0].InvulnerableTimer = 1f);
            Assert.That(events.Any(e => e.Type == SimEventType.AreaEffect && e.Int == (int)AreaKind.Slam && e.ClientId == 0));
            Assert.That(events.Any(e => e.Type == SimEventType.StatusApplied && e.Int == (int)StatusKind.Stunned));
        }

        [Test]
        public void Ricochet_BouncesToASecondEnemy()
        {
            var sim = new RunSimulation(23, new Rect(-12f, -10f, 24f, 20f));
            var r = sim.AddPlayer(0, new Loadout { classId = ClassId.Ranger });
            Quiet(sim);
            r.AddPerk(PerkId.Ricochet);
            r.Position = Vector2.zero;
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(4f, 0f), 50f, 1f, false);
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(5f, 3f), 50f, 1f, false);
            float second = sim.Enemies[1].Health;
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Attack = true });
            Run(sim, 0.12f, s => s.Players[0].InvulnerableTimer = 1f);
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right });
            Run(sim, 0.8f, s => s.Players[0].InvulnerableTimer = 1f);
            Assert.Less(sim.Enemies[1].Health, second, "the arrow bounced into the second skeleton");
        }

        [Test]
        public void NewTrinkets_ApplyTheirStats()
        {
            var plain = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Warrior });
            var chalice = new SimPlayer(1, Vector2.zero, new Loadout { classId = ClassId.Warrior, trinkets = new List<string> { "blood_chalice", "elder_tome" } });
            Assert.AreEqual(plain.MaxHp * 0.85f, chalice.MaxHp, 0.01f);
            Assert.AreEqual(plain.Stats.Get(StatType.Might) * 1.25f, chalice.Stats.Get(StatType.Might), 0.001f);
            Assert.AreEqual(1.15f, chalice.Stats.Get(StatType.Area), 0.001f);
            Assert.AreEqual(12, MetaCatalog.Trinkets.Length);
            Assert.AreEqual(MetaCatalog.Trinkets.Length, MetaCatalog.Trinkets.Select(t => t.Id).Distinct().Count());
        }

        [Test]
        public void EveryPerkHasTextAndFitsTheOfferPacking()
        {
            foreach (var def in PerkCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(def.Name));
                Assert.DoesNotThrow(() => def.Describe(def.MaxLevel));
                Assert.Less((int)def.Id, PerkOption.HealId);
            }
            Assert.AreEqual(PerkCatalog.Count, PerkCatalog.All.Length, "every PerkId has a definition");
        }
    }
}
