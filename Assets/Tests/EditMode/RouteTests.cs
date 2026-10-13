using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>The run's route through the lands: picked from the seed, chosen at the two ways after every lord.</summary>
    public class RouteTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> eachTick = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                eachTick?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static void AnswerPicks(RunSimulation sim)
        {
            foreach (var p in sim.Players)
                for (int i = 0; i < 10 && p.PendingOffer != null && p.OfferKind != ChoiceKind.Shop; i++) sim.ChoosePerk(p.ClientId, 0);
        }

        /// <summary>Into the current area's lord hall, the lord slain (and its vessel, for the Lich).</summary>
        private static void SlayTheLord(RunSimulation sim, SimPlayer p)
        {
            sim.MoveTeamTo(sim.Map.Boss);
            AnswerPicks(sim);
            Run(sim, 2.5f);
            for (int round = 0; round < 3 && !sim.ExitOpen; round++)
            {
                for (int i = 0; i < sim.Enemies.Count; i++)
                    if (!sim.Enemies[i].Dead && (sim.Enemies[i].Def.Boss || sim.Enemies[i].Type == EnemyType.SoulVessel))
                        sim.DamageEnemy(i, 1e7f, p, HitFlags.None, UnityEngine.Vector2.zero, 0f);
                Run(sim, 0.3f);
            }
            AnswerPicks(sim);
        }

        [Test]
        public void Classic_Route_IsCryptChapelAbyss()
        {
            var sim = new RunSimulation(3);
            Assert.AreEqual(Biome.Crypt, sim.Biome);
            Assert.AreEqual(EnemyType.BoneKing, sim.AreaBoss);
            Assert.AreEqual(Biome.Chapel, sim.WayLand(0));
            Assert.AreEqual(Biome.Chapel, sim.WayLand(1));
        }

        [Test]
        public void VariedRoute_FirstLandComesFromTheSeed_AndTheWaysOfferTwoOtherLands()
        {
            var firsts = new HashSet<Biome>();
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var sim = new RunSimulation(seed);
                sim.UseVariedRoute();
                firsts.Add(sim.Biome);
                Assert.AreEqual(BiomeCatalog.Get(sim.Biome).Lord, sim.AreaBoss, $"seed {seed}: the land's own lord");
                Assert.AreEqual(sim.Biome, sim.Map.Biome);
                Assert.AreNotEqual(sim.Biome, sim.WayLand(0), $"seed {seed}");
                Assert.AreNotEqual(sim.Biome, sim.WayLand(1), $"seed {seed}");
                Assert.AreNotEqual(sim.WayLand(0), sim.WayLand(1), $"seed {seed}: two different lands");
            }
            Assert.Greater(firsts.Count, 1, "runs begin in different lands");
        }

        [Test]
        public void TheWayTheTeamStandsIn_DecidesTheNextLand()
        {
            foreach (int side in new[] { 0, 1 })
            {
                var sim = new RunSimulation(21);
                sim.UseVariedRoute();
                var a = sim.AddPlayer(0);
                sim.Start();
                var first = sim.Biome;
                SlayTheLord(sim, a);
                Assert.IsTrue(sim.ExitOpen);
                var expected = sim.WayLand(side);
                var events = Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => { if (s.Area == 0) a.Position = side == 0 ? s.LeftWayPosition : s.ExitPosition; });
                Assert.AreEqual(1, sim.Area, $"side {side}");
                Assert.AreEqual(expected, sim.Biome, $"side {side}");
                Assert.AreEqual(BiomeCatalog.Get(expected).Lord, sim.AreaBoss);
                Assert.AreEqual(first, sim.BiomeOf(0), "the route remembers where the team has been");
                var started = events.First(e => e.Type == SimEventType.RoomStarted);
                Assert.AreEqual((int)expected, (int)started.Value, "the new area's event carries its land");
            }
        }

        [Test]
        public void ThreeLands_AreAllDifferent_AndTheThirdLordOffersHome()
        {
            var sim = new RunSimulation(5);
            sim.UseVariedRoute();
            var a = sim.AddPlayer(0);
            sim.Start();
            for (int area = 0; area < RunSimulation.AreaCount - 1; area++)
            {
                SlayTheLord(sim, a);
                // At least one way always leads to a land not seen yet: take it.
                int side = sim.Route.Contains(sim.WayLand(0)) ? 1 : 0;
                Assert.IsFalse(sim.Route.Contains(sim.WayLand(side)), $"area {area}: a new land is offered");
                Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => { if (s.Area == area) a.Position = side == 0 ? s.LeftWayPosition : s.ExitPosition; });
                Assert.AreEqual(area + 1, sim.Area);
            }
            Assert.AreEqual(3, sim.Route.Distinct().Count(), "a land is not visited twice in one descent");
            SlayTheLord(sim, a);
            Assert.IsTrue(sim.Conquered, "the third lord conquers the labyrinth, whoever it is");
            Run(sim, RunSimulation.ExitHoldTime + 0.5f, s => a.Position = s.LeftWayPosition);
            Assert.AreEqual(RunPhase.Victory, sim.Phase);
        }

        [Test]
        public void Omens_ComeWithTheVariedRoute_AndChangeTheRun()
        {
            Assert.AreEqual(Omen.None, new RunSimulation(4).Omen, "the classic route has none");
            var seen = new HashSet<Omen>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = new RunSimulation(seed);
                sim.UseVariedRoute();
                Assert.AreNotEqual(Omen.None, sim.Omen);
                seen.Add(sim.Omen);
            }
            Assert.Greater(seen.Count, 3, "runs bring different omens");

            var moon = new RunSimulation(4);
            moon.UseVariedRoute();
            moon.SetOmen(Omen.BloodMoon);
            Assert.Greater(moon.EliteChance, new RunSimulation(4).EliteChance, "more elites under the blood moon");

            var blessed = new RunSimulation(4);
            blessed.UseVariedRoute();
            blessed.SetOmen(Omen.Blessed);
            var p = blessed.AddPlayer(0);
            blessed.Start();
            int picks = 0;
            for (int i = 0; i < 10 && p.PendingOffer != null; i++, picks++) blessed.ChoosePerk(0, 0);
            Assert.GreaterOrEqual(picks, 1, "a power to choose at the start");
        }

        [Test]
        public void PlannedRoute_IsFollowed()
        {
            var sim = new RunSimulation(8);
            sim.UseVariedRoute(Biome.Abyss, Biome.Crypt);
            Assert.AreEqual(Biome.Abyss, sim.Biome);
            Assert.AreEqual(Biome.Crypt, sim.WayLand(0));
            Assert.AreEqual(EnemyType.Necromancer, BiomeCatalog.Get(sim.Biome).Champion);
        }

        [Test]
        public void EveryLand_HasMonstersFromTheFirstLayer_AndLayoutsThatExist()
        {
            foreach (var land in BiomeCatalog.All)
            {
                Assert.IsNotEmpty(BiomeCatalog.PoolAt(land.Id, 0), land.Name);
                Assert.IsTrue(EnemyCatalog.Get(land.Lord).Boss, $"{land.Name}: its lord is a boss");
                foreach (int id in land.FightLayouts) Assert.IsNotNull(Arena.Get(id), $"{land.Name}: layout {id}");
                foreach (var (type, _, _) in land.Pool) Assert.IsFalse(EnemyCatalog.Get(type).Boss || EnemyCatalog.Get(type).Prop, $"{land.Name}: {type}");
            }
        }
    }
}
