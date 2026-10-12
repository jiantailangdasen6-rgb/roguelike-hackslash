using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Perk offers, new perks and trinkets.</summary>
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

        /// <summary>Everyone waits at one door until the next room starts; level-up picks before that are answered.</summary>
        /// <summary>Stops wave spawns so combat tests only see the player's own effects.</summary>
        private static void Quiet(RunSimulation sim) => sim.Room.Cleared = true;

        // ------------------------------------------------------------------ Perk offers

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
        }

        // ------------------------------------------------------------------ New perks and trinkets

        [Test]
        public void Executioner_HitsWoundedEnemiesHarder()
        {
            var sim = new RunSimulation(18);
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
            var sim = new RunSimulation(19);
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
        public void FrostArmor_ShieldsAlliesNearTheNova()
        {
            var sim = new RunSimulation(21);
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
            var sim = new RunSimulation(22);
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
            var sim = new RunSimulation(23);
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
