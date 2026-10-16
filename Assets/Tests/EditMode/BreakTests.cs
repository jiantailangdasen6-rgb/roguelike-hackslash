using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Breaking a stance: heavy blows wear it faster, broken monsters reel and take more, then steady.</summary>
    public class BreakTests
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

        private static (RunSimulation, SimPlayer, int) Elite()
        {
            var sim = new RunSimulation(23);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            p.Position = new Vector2(-10f, 0f);
            int id = sim.Enemies.Spawn(EnemyType.Skeleton, Vector2.zero, 10f, 1f, true);
            int i = sim.Enemies.IndexOf(id);
            sim.Enemies[i].Cooldown = 99f;
            Run(sim, Dt);
            return (sim, p, id);
        }

        [Test]
        public void OnlyTheStrongHaveAStance()
        {
            var (sim, _, id) = Elite();
            Assert.Greater(sim.Enemies[sim.Enemies.IndexOf(id)].PoiseMax, 0f, "elites");
            int plain = sim.Enemies.Spawn(EnemyType.Skeleton, Vector2.one, 1f, 1f, false);
            Run(sim, Dt);
            Assert.AreEqual(0f, sim.Enemies[sim.Enemies.IndexOf(plain)].PoiseMax, "not the common ones");
        }

        [Test]
        public void HeavyBlows_BreakIt_AndTheBrokenTakeMore()
        {
            var (sim, p, id) = Elite();
            int i = sim.Enemies.IndexOf(id);
            float poise = sim.Enemies[i].PoiseMax;
            sim.DamageEnemy(i, poise / (RunSimulation.HeavyPoise * 2f), p, HitFlags.Heavy, Vector2.zero, 0f);
            Assert.AreEqual(poise * 0.5f, sim.Enemies[i].Poise, poise * 0.02f, "a heavy blow wears two and a half times");
            var events = new List<SimEvent>(sim.Events);
            sim.DamageEnemy(i, poise, p, HitFlags.Heavy, Vector2.zero, 0f);
            events.AddRange(sim.Events);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.EnemyBroken && e.Id == id));
            Assert.Greater(sim.Enemies[i].Broken, 0f);
            Assert.Greater(sim.Enemies[i].Stunned, 0f, "it reels");

            float hp = sim.Enemies[i].Health;
            sim.DamageEnemy(i, 10f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(10f * (1f + RunSimulation.BreakBonus), hp - sim.Enemies[i].Health, 0.5f, "broken: more damage");

            Run(sim, RunSimulation.BreakTime + 0.2f);
            i = sim.Enemies.IndexOf(id);
            Assert.LessOrEqual(sim.Enemies[i].Broken, 0f);
            Assert.AreEqual(sim.Enemies[i].PoiseMax, sim.Enemies[i].Poise, 0.01f, "back on its feet, steady");
        }

        [Test]
        public void TheStance_SteadiesWhenLeftAlone()
        {
            var (sim, p, id) = Elite();
            int i = sim.Enemies.IndexOf(id);
            float poise = sim.Enemies[i].PoiseMax;
            sim.DamageEnemy(i, poise * 0.5f, p, HitFlags.None, Vector2.zero, 0f);
            float worn = sim.Enemies[i].Poise;
            Run(sim, 1f);
            Assert.AreEqual(worn, sim.Enemies[sim.Enemies.IndexOf(id)].Poise, 0.01f, "not right away");
            Run(sim, RunSimulation.PoiseSteadyDelay + 2f);
            Assert.Greater(sim.Enemies[sim.Enemies.IndexOf(id)].Poise, worn);
        }
    }
}
