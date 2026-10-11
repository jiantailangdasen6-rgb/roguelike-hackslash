using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class PerkTests
    {
        [Test]
        public void PackUnpack_RoundTrips()
        {
            var options = new[] { new PerkOption(3, 1), new PerkOption(20, 2), new PerkOption(PerkOption.HealId, 0) };
            var unpacked = PerkOption.Unpack(PerkOption.Pack(options));
            Assert.AreEqual(options.Length, unpacked.Length);
            for (int i = 0; i < options.Length; i++)
            {
                Assert.AreEqual(options[i].Perk, unpacked[i].Perk);
                Assert.AreEqual(options[i].Level, unpacked[i].Level);
            }
        }

        [Test]
        public void Roll_OffersOnlyOwnClassAndUnlockedPerks()
        {
            var rng = new GameRandom(1);
            var mage = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Mage });
            for (int i = 0; i < 200; i++)
            {
                foreach (var o in PerkRoller.Roll(rng, mage))
                {
                    var def = PerkCatalog.Get((PerkId)o.Perk);
                    Assert.That(!def.Class.HasValue || def.Class == ClassId.Mage, $"{def.Name} is not a mage perk");
                    Assert.IsFalse(def.Locked, $"{def.Name} is locked");
                }
            }
        }

        [Test]
        public void MasteryUnlock_MakesLockedPerkAvailable()
        {
            var rng = new GameRandom(2);
            var mage = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Mage, mastery = new List<string> { "m_chain" } });
            bool seen = false;
            for (int i = 0; i < 300 && !seen; i++)
                seen = PerkRoller.Roll(rng, mage).Any(o => o.Perk == (byte)PerkId.ChainBolt);
            Assert.IsTrue(seen);
        }

        [Test]
        public void Roll_GivesDistinctOptions_AndRespectsMaxLevel()
        {
            var rng = new GameRandom(3);
            var p = new SimPlayer(0, Vector2.zero);
            for (int i = 0; i < 25; i++)
            {
                var options = PerkRoller.Roll(rng, p);
                Assert.AreEqual(options.Length, options.Select(o => o.Perk).Distinct().Count());
                PerkRoller.Apply(p, options[0]);
            }
            foreach (var def in PerkCatalog.All) Assert.LessOrEqual(p.PerkLevel(def.Id), def.MaxLevel);
        }
    }

    public class MetaTests
    {
        [Test]
        public void ClassBaseStats_GrowthCurve_AndMasteryGrowth()
        {
            var plain = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Warrior });
            var trained = new SimPlayer(1, Vector2.zero, new Loadout { classId = ClassId.Warrior, mastery = new List<string> { "w_hp" } });
            Assert.AreEqual(ClassCatalog.Get(ClassId.Warrior).Health, plain.MaxHp, 0.01f);

            plain.SetLevel(5);
            trained.SetLevel(5);
            Assert.AreEqual(160f + 8f * 4, plain.MaxHp, 0.01f);
            Assert.AreEqual(160f + 12f * 4, trained.MaxHp, 0.01f);
            Assert.AreEqual(plain.MaxHp, plain.Hp, 0.01f, "levelling up heals the gained HP");
        }

        [Test]
        public void Trinkets_ApplyTheirEffects()
        {
            var p = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Ranger, trinkets = new List<string> { "charm_hp", "phoenix" } });
            Assert.AreEqual(115f * 1.15f, p.MaxHp, 0.01f);
            Assert.IsTrue(p.Phoenix);
        }

        [Test]
        public void Loadout_FromJson_DropsUnknownAndExtraTrinkets()
        {
            var json = new Loadout { classId = ClassId.Mage, mastery = new List<string> { "m_hp", "nope" }, trinkets = new List<string> { "charm_hp", "wolf_fang", "feather", "fake" } }.ToJson();
            var l = Loadout.FromJson(json);
            CollectionAssert.AreEqual(new[] { "m_hp" }, l.mastery);
            Assert.AreEqual(MetaCatalog.TrinketSlots, l.trinkets.Count);
            Assert.AreEqual(ClassId.Warrior, Loadout.FromJson("garbage{").classId);
        }

        [Test]
        public void Profile_BuyAndEquip()
        {
            var profile = ProfileData.Default();
            profile.shards = 100;
            Assert.IsTrue(profile.TryBuy("feather", 40));
            Assert.IsFalse(profile.TryBuy("feather", 40), "already owned");
            Assert.IsFalse(profile.TryBuy("phoenix", 120), "not enough shards");
            Assert.AreEqual(60, profile.shards);

            profile.ToggleTrinket("wolf_fang");
            profile.ToggleTrinket("feather"); // third one replaces the oldest
            Assert.AreEqual(2, profile.equippedTrinkets.Count);
            CollectionAssert.Contains(profile.ToLoadout().trinkets, "feather");
            Assert.That(ProfileData.FromJson(profile.ToJson()).shards, Is.EqualTo(60));
        }
    }

    public class CombatTests
    {
        private const float Dt = 1f / 30f;

        private static RunSimulation NewSim(ClassId cls, out SimPlayer player)
        {
            var sim = new RunSimulation(42);
            player = sim.AddPlayer(0, new Loadout { classId = cls });
            player.Position = Vector2.zero;
            return sim;
        }

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

        private static int Spawn(RunSimulation sim, EnemyType type, Vector2 at, float hpMul = 1f)
        {
            sim.Enemies.Spawn(type, at, hpMul, 1f, false);
            return sim.Enemies.Count - 1;
        }

        private static void Freeze(RunSimulation sim)
        {
            // Keep enemies passive so tests only see what the player does.
            for (int i = 0; i < sim.Enemies.Count; i++) sim.Enemies[i].Cooldown = 99f;
        }

        [Test]
        public void WarriorSlash_HitsInFront_NotBehind()
        {
            var sim = NewSim(ClassId.Warrior, out var p);
            int front = Spawn(sim, EnemyType.Skeleton, new Vector2(1.4f, 0f), 10f);
            int back = Spawn(sim, EnemyType.Skeleton, new Vector2(-1.4f, 0f), 10f);
            float frontHp = sim.Enemies[front].Health, backHp = sim.Enemies[back].Health;
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Attack = true });

            Run(sim, 0.1f, Freeze);

            Assert.Less(sim.Enemies[front].Health, frontHp);
            Assert.AreEqual(backHp, sim.Enemies[back].Health);
        }

        [Test]
        public void MageBolt_TravelsAndHits()
        {
            var sim = NewSim(ClassId.Mage, out _);
            int target = Spawn(sim, EnemyType.Skeleton, new Vector2(6f, 0f), 10f);
            float hp = sim.Enemies[target].Health;
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right * 6f, Attack = true });

            var events = Run(sim, 0.8f, Freeze);

            Assert.That(events.Any(e => e.Type == SimEventType.ProjectileSpawn && e.Int == (int)ProjectileKind.Bolt));
            Assert.Less(sim.Enemies[target].Health, hp);
        }

        [Test]
        public void FrostNova_Freezes_ThenWarriorShatters()
        {
            var sim = new RunSimulation(5);
            var mage = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            var warrior = sim.AddPlayer(1, new Loadout { classId = ClassId.Warrior });
            mage.Position = new Vector2(-5f, 0f);
            warrior.Position = new Vector2(-1.2f, 0f);
            int enemy = Spawn(sim, EnemyType.Skeleton, new Vector2(0f, 0f), 20f);

            sim.SetPlayerInput(0, new PlayerInput { Aim = new Vector2(5f, 0f), Skill1Count = 1 });
            var events = Run(sim, 0.5f, Freeze);
            Assert.Greater(sim.Enemies[enemy].Frozen, 0f, "enemy should be frozen");

            sim.SetPlayerInput(1, new PlayerInput { Aim = Vector2.right, Attack = true });
            events = Run(sim, 0.1f, Freeze);
            var shatter = events.FirstOrDefault(e => e.Type == SimEventType.EnemyHit && (e.Int & (int)HitFlags.Shatter) != 0);
            Assert.AreEqual(SimEventType.EnemyHit, shatter.Type, "a heavy hit on a frozen enemy shatters it");
            Assert.Greater(shatter.Value, ClassCatalog.Get(ClassId.Warrior).BasicDamage * 2f);
            Assert.That(events.Any(e => e.Type == SimEventType.AreaEffect && e.Int == (int)AreaKind.Shatter));
        }

        [Test]
        public void HunterMark_IncreasesDamageFromEveryone()
        {
            var sim = NewSim(ClassId.Ranger, out _);
            int marked = Spawn(sim, EnemyType.Skeleton, new Vector2(4f, 0f), 10f);
            sim.SetPlayerInput(0, new PlayerInput { Aim = new Vector2(4f, 0f), Skill2Count = 1 });
            Run(sim, 0.1f, Freeze);
            Assert.Greater(sim.Enemies[marked].Marked, 0f);

            float before = sim.Enemies[marked].Health;
            sim.DamageEnemy(marked, 10f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(10f * (1f + ClassCatalog.MarkDamageBonus), before - sim.Enemies[marked].Health, 0.01f);
        }

        [Test]
        public void WarCry_StunsEnemies_AndBuffsNearbyAllies()
        {
            var sim = new RunSimulation(6);
            var warrior = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var ally = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            warrior.Position = Vector2.zero;
            ally.Position = new Vector2(3f, 0f);
            int enemy = Spawn(sim, EnemyType.Skeleton, new Vector2(1.5f, 0f), 10f);

            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Skill2Count = 1 });
            Run(sim, 0.1f);

            Assert.Greater(sim.Enemies[enemy].Stunned, 0f);
            Assert.Greater(ally.ValorTimer, 0f);
            Assert.Greater(ally.Stats.Get(StatType.Might), 1.2f);
        }

        [Test]
        public void SkeletonTelegraphs_ThenHits_UnlessDodged()
        {
            var sim = NewSim(ClassId.Warrior, out var p);
            int e = Spawn(sim, EnemyType.Skeleton, new Vector2(1.2f, 0f));
            sim.Enemies[e].Cooldown = 0f;

            var events = Run(sim, 0.2f);
            var telegraph = events.FirstOrDefault(x => x.Type == SimEventType.EnemyTelegraph);
            Assert.AreEqual(SimEventType.EnemyTelegraph, telegraph.Type);
            Assert.AreEqual((int)AttackShape.Cone, telegraph.Int);
            Assert.IsFalse(events.Any(x => x.Type == SimEventType.PlayerHurt), "no damage during the windup");

            events = Run(sim, 0.6f);
            Assert.That(events.Any(x => x.Type == SimEventType.PlayerHurt), "the strike lands after the windup");

            // Second time: dodge right before the strike.
            var sim2 = NewSim(ClassId.Warrior, out var p2);
            int e2 = Spawn(sim2, EnemyType.Skeleton, new Vector2(1.2f, 0f));
            sim2.Enemies[e2].Cooldown = 0f;
            Run(sim2, 0.45f);
            sim2.SetPlayerInput(0, new PlayerInput { Aim = Vector2.left, DodgeCount = 1 });
            events = Run(sim2, 0.4f);
            Assert.IsFalse(events.Any(x => x.Type == SimEventType.PlayerHurt), "dodge i-frames avoid the hit");
        }

        [Test]
        public void GhoulLunges_AlongItsTelegraph()
        {
            var sim = NewSim(ClassId.Warrior, out _);
            int g = Spawn(sim, EnemyType.Ghoul, new Vector2(3.5f, 0f));
            sim.Enemies[g].Cooldown = 0f;
            float startX = sim.Enemies[g].Position.x;

            var events = Run(sim, 1.2f);

            Assert.That(events.Any(x => x.Type == SimEventType.EnemyTelegraph && x.Int == (int)AttackShape.Line));
            Assert.Less(sim.Enemies[g].Position.x, startX - 2f);
        }
    }

    public class RunFlowTests
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

        private static void KillAll(RunSimulation sim)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                sim.DamageEnemy(i, 99999f, sim.Players[0], HitFlags.None, Vector2.zero, 0f);
        }

        private static void KeepAliveAndPick(RunSimulation sim)
        {
            foreach (var p in sim.Players)
            {
                p.InvulnerableTimer = 1f;
                if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
            }
        }

        [Test]
        public void WaveRoom_SpawnsWithWarnings_AndClearsAfterLastWave()
        {
            var sim = new RunSimulation(1);
            sim.AddPlayer(0);
            sim.Start();
            Assert.AreEqual(RoomObjective.Waves, sim.Room.Objective);

            var events = Run(sim, 1.2f, KeepAliveAndPick);
            Assert.That(events.Any(e => e.Type == SimEventType.SpawnWarning));
            Assert.Greater(sim.Enemies.Count, 0, "enemies appear after the warning");

            for (int i = 0; i < 20 && !sim.Room.Cleared; i++)
            {
                KillAll(sim);
                events = Run(sim, 1.2f, KeepAliveAndPick);
            }
            Assert.IsTrue(sim.Room.Cleared);
            Assert.AreEqual(sim.Room.WaveCount, sim.Room.Wave);
        }

        [Test]
        public void Exit_LeadsToNextRoom_AndTeleports()
        {
            var sim = new RunSimulation(2);
            sim.AddPlayer(0);
            sim.Start();
            for (int i = 0; i < 20 && !sim.Room.Cleared; i++)
            {
                KillAll(sim);
                Run(sim, 1.2f, KeepAliveAndPick);
            }

            var events = Run(sim, RunSimulation.ExitHoldTime + 0.3f, s =>
            {
                KeepAliveAndPick(s);
                if (s.Room.Index == 0) s.SetPlayerPosition(0, RunSimulation.DoorPosition(0, s.Room.Doors.Count, s.Bounds));
            });
            Assert.AreEqual(1, sim.Room.Index);
            Assert.AreEqual(RoomObjective.Survive, sim.Room.Objective);
            Assert.That(events.Any(e => e.Type == SimEventType.Teleport));
        }

        [Test]
        public void LevelUp_BanksPicksDuringTheFight_AndOffersThemOnceTheRoomIsCleared()
        {
            var sim = new RunSimulation(3);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            float hpBefore = sim.Players[0].MaxHp;

            var events = new List<SimEvent>();
            while (sim.TeamLevel < 2 && !sim.Room.Cleared)
            {
                KillAll(sim);
                events.AddRange(Run(sim, 0.5f, s => { foreach (var p in s.Players) p.InvulnerableTimer = 1f; }));
                foreach (var soul in sim.Souls.ToArray()) sim.SetPlayerPosition(0, soul.Position);
            }
            Assert.Greater(sim.TeamLevel, 1, "leveled up during the fight");
            Assert.Greater(sim.Players[0].MaxHp, hpBefore, "growth curve raises max HP on level-up");

            if (!sim.Room.Cleared)
            {
                Assert.That(events.Any(e => e.Type == SimEventType.TeamLevelUp));
                Assert.IsNull(sim.Players[0].PendingOffer, "no pick screen mid-fight");
                Assert.Greater(sim.Players[0].PicksLeft, 0, "the pick is banked");
                Assert.IsFalse(sim.AwaitingChoices);
                float elapsed = sim.Elapsed;
                sim.Step(Dt);
                Assert.Greater(sim.Elapsed, elapsed, "the fight goes on");
            }

            for (int i = 0; i < 60 && !sim.Room.Cleared; i++)
            {
                KillAll(sim);
                Run(sim, 1f, s => { foreach (var p in s.Players) p.InvulnerableTimer = 1f; });
            }
            Assert.IsTrue(sim.Room.Cleared);
            Assert.IsNotNull(sim.Players[0].PendingOffer, "offered after the room");
            Assert.IsNotNull(sim.Players[1].PendingOffer);
            Assert.IsTrue(sim.AwaitingChoices);

            // Each player works through their own picks; the door waits for everyone.
            while (sim.Players[0].PendingOffer != null) sim.ChoosePerk(0, 0);
            Assert.IsTrue(sim.AwaitingChoices, "still waiting for player 1");
            foreach (var p in sim.Players) sim.SetPlayerPosition(p.ClientId, RunSimulation.DoorPosition(0, sim.Room.Doors.Count, sim.Bounds));
            Run(sim, 2f, s => { foreach (var p in s.Players) p.InvulnerableTimer = 1f; });
            Assert.AreEqual(0, sim.Room.Index, "the door stays shut while someone is choosing");
            while (sim.Players[1].PendingOffer != null) sim.ChoosePerk(1, 0);
            Assert.IsFalse(sim.AwaitingChoices);
        }

        [Test]
        public void StartPerkMastery_GivesAChoiceAtRunStart()
        {
            var sim = new RunSimulation(4);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Ranger, mastery = new List<string> { "r_start" } });
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();

            Assert.IsTrue(sim.AwaitingChoices);
            Assert.IsNotNull(sim.Players[0].PendingOffer);
            Assert.IsNull(sim.Players[1].PendingOffer);
        }

        [Test]
        public void DownedPlayer_IsRevivedFaster_WithMageHeal()
        {
            var sim = new RunSimulation(5);
            var downed = sim.AddPlayer(0);
            var mage = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            downed.Hp = 0f;
            downed.Downed = true;
            mage.Position = downed.Position + new Vector2(1f, 0f);

            sim.SetPlayerInput(1, new PlayerInput { Aim = Vector2.right, Skill2Count = 1 });
            var events = Run(sim, 1.8f, s => s.GetPlayer(1).InvulnerableTimer = 1f);

            Assert.IsFalse(downed.Downed, "heal (+50%) plus standing nearby revives in well under 3s");
            Assert.That(events.Any(e => e.Type == SimEventType.PlayerRevived && e.ClientId == 0));
        }

        [Test]
        public void AllPlayersDown_IsDefeat()
        {
            var sim = new RunSimulation(6);
            sim.AddPlayer(0);
            sim.Start();
            sim.HurtPlayer(sim.Players[0], 9999f);
            var events = Run(sim, 0.1f);
            Assert.AreEqual(RunPhase.Defeat, sim.Phase);
            Assert.That(events.Any(e => e.Type == SimEventType.RunEnded && e.Int == 0));
        }

        [Test]
        public void Phoenix_RevivesOnce()
        {
            var sim = new RunSimulation(7);
            var p = sim.AddPlayer(0, new Loadout { trinkets = new List<string> { "phoenix" } });
            sim.Start();
            sim.HurtPlayer(p, 9999f);
            Assert.IsFalse(p.Downed);
            p.InvulnerableTimer = 0f;
            sim.HurtPlayer(p, 9999f);
            Assert.IsTrue(p.Downed);
        }

        [Test]
        public void BossRoom_SpawnsTheBoneKing_AndKillingItClearsTheRoom()
        {
            var sim = new RunSimulation(8);
            sim.AddPlayer(0);
            sim.Start();
            // Jump to the first boss room.
            typeof(RunSimulation).GetMethod("StartRoom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(sim, new object[] { RunSimulation.RoomsPerArea - 1 });
            Assert.AreEqual(RoomObjective.Boss, sim.Room.Objective);

            var events = Run(sim, 2f, KeepAliveAndPick);
            Assert.That(events.Any(e => e.Type == SimEventType.BossSpawned));

            for (int i = 0; i < sim.Enemies.Count; i++)
                if (sim.Enemies[i].Def.Boss) sim.DamageEnemy(i, 999999f, sim.Players[0], HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.2f, KeepAliveAndPick);

            Assert.IsTrue(sim.Room.Cleared);
            Assert.AreEqual(1, sim.BossesKilled);
        }

        [Test]
        public void BotsCanPlayAFullRoom()
        {
            // Smoke test: three players holding attack toward the nearest enemy make progress without exceptions.
            var sim = new RunSimulation(9);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.AddPlayer(2, new Loadout { classId = ClassId.Ranger });
            sim.Start();
            byte press = 0;
            Run(sim, 120f, s =>
            {
                KeepAliveAndPick(s);
                press++;
                foreach (var p in s.Players)
                {
                    int n = s.Enemies.FindNearest(p.Position, 30f);
                    Vector2 aim = n >= 0 ? s.Enemies[n].Position - p.Position : Vector2.right;
                    if (p.ClassId == ClassId.Warrior && n >= 0 && aim.magnitude > 1.5f) p.Position += aim.normalized * 0.15f;
                    s.SetPlayerInput(p.ClientId, new PlayerInput { Aim = aim, Attack = true, Skill1Count = (byte)(press / 60), Skill2Count = (byte)(press / 90) });
                }
            });
            Assert.Greater(sim.Kills, 5);
            Assert.GreaterOrEqual(sim.RoomsCleared, 1);
        }
    }
}
