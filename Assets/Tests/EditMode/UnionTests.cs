using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Union techniques: two players' skills at the same moment, close together.</summary>
    public class UnionTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static int Spawn(RunSimulation sim, EnemyType type, Vector2 at)
        {
            sim.Enemies.Spawn(type, at, 10f, 1f, false);
            int i = sim.Enemies.Count - 1;
            sim.Enemies[i].Cooldown = 99f;
            return i;
        }

        private static int _presses;

        private static void Skill(RunSimulation sim, SimPlayer p, Vector2 aim) =>
            sim.SetPlayerInput(p.ClientId, new PlayerInput { Aim = aim, Skill2Count = (byte)++_presses });

        private static List<SimEvent> Unions(IEnumerable<SimEvent> events) =>
            events.Where(e => e.Type == SimEventType.Combo && (ComboKind)e.Int == ComboKind.Union).ToList();

        private static (RunSimulation, SimPlayer, SimPlayer) Pair(ClassId a, ClassId b, float apart)
        {
            var sim = new RunSimulation(41);
            var p = sim.AddPlayer(0, new Loadout { classId = a });
            var q = sim.AddPlayer(1, new Loadout { classId = b });
            p.Position = new Vector2(-apart / 2f, 0f);
            q.Position = new Vector2(apart / 2f, 0f);
            return (sim, p, q);
        }

        [Test]
        public void Catalog_HasANameForEveryPair()
        {
            Assert.AreEqual(36, UnionCatalog.PairCount);
            var seen = new HashSet<string>();
            for (int a = 0; a < UnionCatalog.ClassCount; a++)
            for (int b = 0; b < UnionCatalog.ClassCount; b++)
            {
                int pair = UnionCatalog.Pair((ClassId)a, (ClassId)b);
                Assert.AreEqual(pair, UnionCatalog.Pair((ClassId)b, (ClassId)a), "order does not matter");
                var (x, y) = UnionCatalog.Classes(pair);
                Assert.AreEqual(Mathf.Min(a, b), (int)x);
                Assert.AreEqual(Mathf.Max(a, b), (int)y);
                seen.Add(UnionCatalog.Name(pair));
            }
            Assert.AreEqual(36, seen.Count, "every pair has its own name");
        }

        [Test]
        public void TwoSkillsTogether_MakeAUnion_ThatCarriesBothTouches()
        {
            var (sim, warrior, mage) = Pair(ClassId.Warrior, ClassId.Mage, 3f);
            int enemy = Spawn(sim, EnemyType.Skeleton, new Vector2(0f, 0.5f));
            float hp = sim.Enemies[enemy].Health;
            Skill(sim, warrior, Vector2.up);
            Skill(sim, mage, Vector2.up);
            var events = Run(sim, 0.5f);
            var unions = Unions(events);
            Assert.AreEqual(1, unions.Count, "one union");
            Assert.AreEqual(UnionCatalog.Pair(ClassId.Warrior, ClassId.Mage), unions[0].Id);
            Assert.Less(sim.Enemies[enemy].Health, hp, "it hurts");
            Assert.Greater(warrior.Shield, 0f, "the two are shielded");
            Assert.Greater(mage.Shield, 0f);
        }

        [Test]
        public void TooFarApart_OrAlone_OrTooSoon_NoUnion()
        {
            var (far, a, b) = Pair(ClassId.Ranger, ClassId.Alchemist, RunSimulation.UnionRange + 2f);
            Skill(far, a, Vector2.up);
            Skill(far, b, Vector2.up);
            Assert.AreEqual(0, Unions(Run(far, 0.5f)).Count, "too far apart");

            var solo = new RunSimulation(42);
            var p = solo.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            Skill(solo, p, Vector2.up);
            Run(solo, 0.3f);
            Skill(solo, p, Vector2.up);
            Assert.AreEqual(0, Unions(Run(solo, 0.5f)).Count, "one player never makes a union");

            var (late, c, d) = Pair(ClassId.Guardian, ClassId.Paladin, 2f);
            Skill(late, c, Vector2.up);
            Run(late, RunSimulation.UnionWindow + 0.4f);
            Skill(late, d, Vector2.up);
            Assert.AreEqual(0, Unions(Run(late, 0.5f)).Count, "too long apart in time");
        }

        [Test]
        public void Union_HasACooldown()
        {
            var (sim, a, b) = Pair(ClassId.Deathcaller, ClassId.Assassin, 2f);
            Skill(sim, a, Vector2.up);
            Skill(sim, b, Vector2.up);
            Assert.AreEqual(1, Unions(Run(sim, 0.5f)).Count);
            a.Skill2Cooldown = b.Skill2Cooldown = 0f;
            Skill(sim, a, Vector2.up);
            Skill(sim, b, Vector2.up);
            Assert.AreEqual(0, Unions(Run(sim, 0.5f)).Count, "not again right away");
        }
    }
}
