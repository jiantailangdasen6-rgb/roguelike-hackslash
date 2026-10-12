using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Boss trials (braziers, cages, sigils, collapse, phylactery), difficulty modifiers and drop rarity.</summary>
    public class BossTrialTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> eachTick = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f; // the tests watch the boss, not the players
                eachTick?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        /// <summary>A run started in the given room with every pending pick made, run until the boss stands.</summary>
        private static (RunSimulation Sim, SimPlayer Player) BossRoom(int room, Difficulty difficulty = Difficulty.Normal)
        {
            var sim = new RunSimulation(77, null, difficulty);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(room);
            for (int guard = 0; guard < 10 && p.PendingOffer != null; guard++) sim.ChoosePerk(0, 0);
            Run(sim, 2f);
            Assert.GreaterOrEqual(Boss(sim), 0, "boss spawned");
            return (sim, p);
        }

        private static int Boss(RunSimulation sim)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Def.Boss) return i;
            return -1;
        }

        private static List<int> Living(RunSimulation sim, EnemyType type)
        {
            var list = new List<int>();
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Type == type) list.Add(i);
            return list;
        }

        private static void SetBossHealth(RunSimulation sim, float fraction)
        {
            ref var b = ref sim.Enemies[Boss(sim)];
            b.Health = b.MaxHealth * fraction;
        }

        // ------------------------------------------------------------------ Bone King

        [Test]
        public void BoneKing_AtHalfHealth_IsWardedUntilTheBraziersAreSmashed()
        {
            var (sim, _) = BossRoom(3);
            SetBossHealth(sim, 0.49f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.BoneWard, sim.ActiveTrial);
            var braziers = Living(sim, EnemyType.BoneBrazier);
            Assert.AreEqual(3, braziers.Count);

            float before = sim.Enemies[Boss(sim)].Health;
            sim.DamageEnemy(Boss(sim), 500f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(before, sim.Enemies[Boss(sim)].Health, 0.01f, "warded: no damage");

            foreach (int i in braziers) sim.DamageEnemy(i, 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.2f);
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
            Assert.That(events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == TrialMoment.WardBroken));
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f, "reels after the ward breaks");

            before = sim.Enemies[Boss(sim)].Health;
            sim.DamageEnemy(Boss(sim), 100f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(150f, before - sim.Enemies[Boss(sim)].Health, 0.5f, "exposed: +50% damage");
        }

        [Test]
        public void BoneKing_CagesAPlayer_AndBreakingTheCageFreesThem()
        {
            var (sim, p) = BossRoom(3);
            var events = new List<SimEvent>();
            for (float t = 0f; p.CagedBy == 0 && t < 40f; t += 0.1f) events.AddRange(Run(sim, 0.1f));
            Assert.That(events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == TrialMoment.CageClosed), "the cage closed");
            Assert.AreNotEqual(0, p.CagedBy);
            int cage = sim.Enemies.IndexOf(p.CagedBy);
            sim.DamageEnemy(cage, 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.1f);
            Assert.AreEqual(0, p.CagedBy, "freed");
        }

        // ------------------------------------------------------------------ Blood Bishop

        [Test]
        public void BloodBishop_RitualIsBrokenByLightingEverySigil()
        {
            var (sim, p) = BossRoom(7);
            SetBossHealth(sim, 0.69f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.BloodRitual, sim.ActiveTrial);
            var sigils = Living(sim, EnemyType.BloodSigil);
            Assert.AreEqual(3, sigils.Count);

            var spots = sigils.Select(i => sim.Enemies[i].Position).ToList();
            var events = new List<SimEvent>();
            foreach (var spot in spots)
            {
                if (sim.ActiveTrial != BossTrial.BloodRitual) break;
                events.AddRange(Run(sim, 1.3f, s => p.Position = spot));
            }
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
            Assert.That(events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == TrialMoment.RitualBroken));
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f);
        }

        [Test]
        public void BloodBishop_UnfinishedRitual_HealsTheBishop()
        {
            var (sim, p) = BossRoom(7);
            SetBossHealth(sim, 0.69f);
            Run(sim, 0.1f);
            float before = sim.Enemies[Boss(sim)].Health;
            var events = Run(sim, 16f, s => p.Position = new Vector2(0f, -7f));
            Assert.That(events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == TrialMoment.RitualFailed));
            Assert.Greater(sim.Enemies[Boss(sim)].Health, before);
        }

        [Test]
        public void SigilsCannotBeHit()
        {
            var (sim, _) = BossRoom(7);
            SetBossHealth(sim, 0.69f);
            Run(sim, 0.1f);
            int sigil = Living(sim, EnemyType.BloodSigil)[0];
            sim.DamageEnemy(sigil, 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.IsFalse(sim.Enemies[sigil].Dead);
        }

        // ------------------------------------------------------------------ Lich

        [Test]
        public void Lich_FloorCollapses_AndPlayersAreKeptOnWhatsLeft()
        {
            var (sim, p) = BossRoom(11);
            SetBossHealth(sim, 0.65f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Collapse, sim.ActiveTrial);
            // Standing on the edge until it falls (afterwards the client applies the teleport).
            Run(sim, RunSimulation.CollapseWarnTime + 0.2f, s => { if (s.CollapseStage == 0) p.Position = new Vector2(-13f, -7f); });
            Assert.AreEqual(1, sim.CollapseStage);
            Run(sim, 0.1f);
            Assert.That(sim.PlayRect.Contains(p.Position), $"player moved onto the floor left ({p.Position})");
        }

        [Test]
        public void Lich_RisesFromAPhylactery_UnlessItIsBroken()
        {
            var (sim, _) = BossRoom(11);
            int lich = Boss(sim);
            sim.Enemies[lich].Health = 10f;
            sim.DamageEnemy(lich, 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.IsFalse(sim.Enemies[lich].Dead, "a deadly blow sends his soul away");
            Assert.AreEqual(BossTrial.Phylactery, sim.ActiveTrial);

            var vessel = Living(sim, EnemyType.SoulVessel);
            Assert.AreEqual(1, vessel.Count);
            sim.DamageEnemy(vessel[0], 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.3f);
            Assert.AreEqual(RunPhase.Victory, sim.Phase, "the vessel broke and the Lich fell (last room)");
        }

        [Test]
        public void Lich_WhenTheVesselLasts_ComesBackWithHealth()
        {
            var (sim, _) = BossRoom(11);
            int lich = Boss(sim);
            sim.Enemies[lich].Health = 10f;
            sim.DamageEnemy(lich, 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            Run(sim, RunSimulation.VesselTime + 0.5f);
            Assert.AreNotEqual(BossTrial.Phylactery, sim.ActiveTrial);
            Assert.Greater(sim.Enemies[Boss(sim)].Health, sim.Enemies[Boss(sim)].MaxHealth * 0.25f);
        }

        // ------------------------------------------------------------------ Difficulty modifiers

        [Test]
        public void Nightmare_LaysHandicapsOnPlayers()
        {
            var normal = new RunSimulation(1, null, Difficulty.Normal).AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var nightmare = new RunSimulation(1, null, Difficulty.Nightmare).AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            Assert.AreEqual(normal.MaxHp * (1f - DifficultyCatalog.FrailtyFactor), nightmare.MaxHp, 1f, "frailty");
            Assert.AreEqual(nightmare.MaxHp, nightmare.Hp, 0.01f, "starts at full health");
            Assert.Less(nightmare.HealFactor, normal.HealFactor, "thirst");
            Assert.Greater(nightmare.DodgeFactor, normal.DodgeFactor, "weariness");
        }

        [Test]
        public void IronGuard_ArmoursElites()
        {
            float Damage(Difficulty d)
            {
                var sim = new RunSimulation(2, null, d);
                sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
                sim.Enemies.Spawn(EnemyType.Skeleton, Vector2.zero, 100f, 1f, true);
                float before = sim.Enemies[0].Health;
                sim.DamageEnemy(0, 100f, null, HitFlags.None, Vector2.zero, 0f);
                return before - sim.Enemies[0].Health;
            }
            Assert.AreEqual(100f, Damage(Difficulty.Normal), 0.5f);
            Assert.AreEqual(100f * EnemyCatalog.ArmorFactor, Damage(Difficulty.Hard), 0.5f);
        }

        [Test]
        public void Vengeance_DeadMonstersBurst()
        {
            var sim = new RunSimulation(3, null, Difficulty.Nightmare);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(3f, 0f), 1f, 1f, false);
            sim.Enemies[0].Cooldown = 99f;
            sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(12f, 6f), 100f, 1f, false); // keeps the room from clearing
            sim.Enemies[1].Cooldown = 99f;
            sim.DamageEnemy(0, 1e6f, sim.Players[0], HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, DifficultyCatalog.VengeanceDelay + 0.3f);
            Assert.That(events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.Id < 0), "a telegraphed burst");
            Assert.That(events.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.SoulBurst), "it bursts");
        }

        // ------------------------------------------------------------------ Drops

        [Test]
        public void Legendaries_AreRare_AndBossesAndDepthImproveTheOdds()
        {
            float LegendaryRate(float boost)
            {
                int n = 20000, legendary = 0;
                for (int i = 0; i < n; i++)
                    if (ItemGenerator.Generate((ulong)(i * 2654435761L + 12345) | 1ul, 3, boost).rarity == Rarity.Legendary) legendary++;
                return legendary / (float)n;
            }
            float plain = LegendaryRate(0f), boss = LegendaryRate(2.5f), deepHardBoss = LegendaryRate(2.5f + 0.7f + 0.6f);
            Assert.Less(plain, 0.01f, "plain monsters almost never");
            Assert.That(boss > 0.02f && boss < 0.09f, $"a boss now and then ({boss:P1})");
            Assert.Greater(deepHardBoss, boss, "deeper and harder is better");
        }
    }
}
