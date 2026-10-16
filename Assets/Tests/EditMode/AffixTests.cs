using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Elite affixes: how many, and what each one does.</summary>
    public class AffixTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static (RunSimulation, int) Elite(EliteAffix affixes, Difficulty difficulty = Difficulty.Normal, EnemyType type = EnemyType.Skeleton)
        {
            var sim = new RunSimulation(51, difficulty);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            p.Position = new Vector2(-12f, 0f);
            int id = sim.Enemies.Spawn(type, Vector2.zero, 1f, 1f, true);
            int i = sim.Enemies.IndexOf(id);
            sim.Enemies[i].Cooldown = 99f;
            if (affixes != EliteAffix.None)
            {
                sim.Enemies[i].Affixes = affixes;
                if ((affixes & EliteAffix.Shielded) != 0) sim.Enemies[i].AffixShield = sim.Enemies[i].MaxHealth * RunSimulation.ShieldShare;
            }
            return (sim, id);
        }

        private static int Bits(EliteAffix a)
        {
            int n = 0;
            for (int v = (int)a; v != 0; v &= v - 1) n++;
            return n;
        }

        [Test]
        public void Elites_GetOneAffix_TwoOnHard_ThreeInNightmare()
        {
            foreach (var (difficulty, count) in new[] { (Difficulty.Normal, 1), (Difficulty.Hard, 2), (Difficulty.Nightmare, 3) })
            {
                var (sim, id) = Elite(EliteAffix.None, difficulty);
                sim.GiveAffixes(id);
                var e = sim.Enemies[sim.Enemies.IndexOf(id)];
                Assert.AreEqual(count, Bits(e.Affixes), difficulty.ToString());
                Assert.That(sim.Events.Any(ev => ev.Type == SimEventType.EliteAffixes && ev.Id == id && ev.Int == (int)e.Affixes), "clients are told");
            }
            var (plain, plainId) = Elite(EliteAffix.None);
            int k = plain.Enemies.Spawn(EnemyType.Skeleton, Vector2.one, 1f, 1f, false);
            plain.GiveAffixes(k);
            Assert.AreEqual(EliteAffix.None, plain.Enemies[plain.Enemies.IndexOf(k)].Affixes, "only elites");
        }

        [Test]
        public void Shielded_SoaksBlows_AndComesBackWhenLeftAlone()
        {
            var (sim, id) = Elite(EliteAffix.Shielded);
            int i = sim.Enemies.IndexOf(id);
            float hp = sim.Enemies[i].Health;
            float shield = sim.Enemies[i].AffixShield;
            sim.DamageEnemy(i, shield * 0.5f, sim.Players[0], HitFlags.None, Vector2.right, 0f);
            Assert.AreEqual(hp, sim.Enemies[i].Health, 0.01f, "the shield took it");
            sim.DamageEnemy(i, shield, sim.Players[0], HitFlags.None, Vector2.right, 0f);
            Assert.Less(sim.Enemies[i].Health, hp, "through the shield");
            Assert.LessOrEqual(sim.Enemies[i].AffixShield, 0f);
            var events = Run(sim, RunSimulation.ShieldBack + 0.5f);
            i = sim.Enemies.IndexOf(id);
            Assert.Greater(sim.Enemies[i].AffixShield, 0f, "back after a quiet while");
            Assert.That(events.Any(e => e.Type == SimEventType.EliteShield && e.Int == 1));
        }

        [Test]
        public void Regenerating_HealsWhenLeftAlone()
        {
            var (sim, id) = Elite(EliteAffix.Regenerating);
            int i = sim.Enemies.IndexOf(id);
            sim.DamageEnemy(i, sim.Enemies[i].MaxHealth * 0.5f, sim.Players[0], HitFlags.None, Vector2.right, 0f);
            float hurt = sim.Enemies[i].Health;
            Run(sim, 1f);
            Assert.AreEqual(hurt, sim.Enemies[sim.Enemies.IndexOf(id)].Health, 0.01f, "not right away");
            Run(sim, 3f);
            Assert.Greater(sim.Enemies[sim.Enemies.IndexOf(id)].Health, hurt);
        }

        [Test]
        public void Volatile_Bursts_AndSplitting_LeavesTwo()
        {
            var (sim, id) = Elite(EliteAffix.Volatile | EliteAffix.Splitting);
            int i = sim.Enemies.IndexOf(id);
            sim.DamageEnemy(i, 99999f, sim.Players[0], HitFlags.None, Vector2.right, 0f);
            var events = Run(sim, 1.5f);
            Assert.That(events.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.Explosion), "it bursts");
            int skeletons = 0;
            for (int k = 0; k < sim.Enemies.Count; k++)
                if (!sim.Enemies[k].Dead && sim.Enemies[k].Type == EnemyType.Skeleton)
                {
                    skeletons++;
                    Assert.IsFalse(sim.Enemies[k].Elite, "the halves are plain");
                }
            Assert.AreEqual(2, skeletons);
        }

        [Test]
        public void Summoner_CallsHelp_UpToTwo()
        {
            var (sim, id) = Elite(EliteAffix.Summoner);
            Run(sim, RunSimulation.AffixSummonEvery * 4f);
            int others = 0;
            for (int k = 0; k < sim.Enemies.Count; k++)
                if (!sim.Enemies[k].Dead && sim.Enemies[k].Id != id) others++;
            Assert.AreEqual(RunSimulation.AffixSummonCap, others);
        }
    }
}
