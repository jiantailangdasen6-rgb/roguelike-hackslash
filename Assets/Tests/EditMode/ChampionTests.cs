using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Each land's own champion and its trick (RunSimulation.Champions).</summary>
    public class ChampionTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, MapRoom lair)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                sim.SetPlayerPosition(0, lair.Origin + new Vector2(0f, -5f));
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        /// <summary>A run in the land, standing in its champion's lair until the champion has come.</summary>
        private static (RunSimulation, SimPlayer, MapRoom) Lair(Biome land)
        {
            for (ulong seed = 1; seed < 300; seed++)
            {
                var sim = new RunSimulation(seed);
                sim.UseVariedRoute(land);
                var lair = sim.Map.Rooms.Find(r => r.Kind == RoomKind.MiniBoss);
                if (lair == null) continue;
                var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
                sim.Start();
                for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
                Run(sim, 2f, lair);
                Assert.IsNotEmpty(Living(sim, BiomeCatalog.Get(land).Champion), "the champion came");
                return (sim, p, lair);
            }
            Assert.Fail("no champion's lair on any map");
            return default;
        }

        private static List<int> Living(RunSimulation sim, EnemyType type)
        {
            var list = new List<int>();
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (!sim.Enemies[i].Dead && sim.Enemies[i].Type == type) list.Add(i);
            return list;
        }

        private static void Kill(RunSimulation sim, SimPlayer p, int index) =>
            sim.DamageEnemy(index, 1e7f, p, HitFlags.None, Vector2.zero, 0f);

        [Test]
        public void EveryLand_HasAChampionOfItsOwn()
        {
            var champions = BiomeCatalog.Routable.Select(b => b.Champion).ToList();
            Assert.AreEqual(champions.Count, champions.Distinct().Count());
            foreach (var land in BiomeCatalog.Routable)
                Assert.IsFalse(land.Pool.Any(m => m.Type == land.Champion), $"{land.Name}: the champion is not a common monster");
        }

        [Test]
        public void BoneDigger_RisesFromItsBones_UnlessTheyAreSmashed()
        {
            var (sim, p, lair) = Lair(Biome.Crypt);
            int kills = sim.Kills;
            Kill(sim, p, Living(sim, EnemyType.BoneDigger)[0]);
            Run(sim, 0.2f, lair);
            Assert.AreEqual(1, Living(sim, EnemyType.BonePile).Count, "it falls into a pile of bones");
            Assert.AreEqual(kills, sim.Kills, "not yet a kill");
            Run(sim, RunSimulation.PileTime, lair);
            var risen = Living(sim, EnemyType.BoneDigger);
            Assert.AreEqual(1, risen.Count, "it stood up again");
            Assert.IsTrue(sim.Enemies[risen[0]].Champion);
            Kill(sim, p, risen[0]);
            Run(sim, 0.2f, lair);
            Assert.IsEmpty(Living(sim, EnemyType.BonePile), "only once");
            Assert.AreEqual(kills + 1, sim.Kills);

            // Smashing the pile ends it.
            (sim, p, lair) = Lair(Biome.Crypt);
            Kill(sim, p, Living(sim, EnemyType.BoneDigger)[0]);
            Run(sim, 0.2f, lair);
            Kill(sim, p, Living(sim, EnemyType.BonePile)[0]);
            Run(sim, RunSimulation.PileTime + 0.5f, lair);
            Assert.IsEmpty(Living(sim, EnemyType.BoneDigger));
        }

        [Test]
        public void Twins_RaiseEachOther_UnlessBothFall()
        {
            var (sim, p, lair) = Lair(Biome.Chapel);
            var twins = Living(sim, EnemyType.TwinAcolyte);
            Assert.AreEqual(2, twins.Count, "they come in twos");
            Kill(sim, p, twins[0]);
            Run(sim, RunSimulation.TwinReviveTime + 0.3f, lair);
            Assert.AreEqual(2, Living(sim, EnemyType.TwinAcolyte).Count, "her sister raised her");

            twins = Living(sim, EnemyType.TwinAcolyte);
            Kill(sim, p, twins[0]);
            Kill(sim, p, twins[1]);
            Run(sim, RunSimulation.TwinReviveTime + 0.3f, lair);
            Assert.IsEmpty(Living(sim, EnemyType.TwinAcolyte), "both fell together");
        }

        [Test]
        public void ShadeLord_LeavesDoubles_ThatDieToOneBlow()
        {
            var (sim, p, lair) = Lair(Biome.Abyss);
            Run(sim, RunSimulation.ShadeSplitEvery, lair);
            var decoys = Living(sim, EnemyType.ShadeDecoy);
            Assert.AreEqual(2, decoys.Count);
            int other = sim.Enemies[decoys[1]].Id;
            int kills = sim.Kills;
            sim.DamageEnemy(decoys[0], 5f, p, HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.1f, lair);
            Assert.AreEqual(1, Living(sim, EnemyType.ShadeDecoy).Count);
            Assert.AreEqual(kills, sim.Kills, "a double is no kill");
            Run(sim, RunSimulation.DecoyLife, lair);
            Assert.IsTrue(sim.Enemies.IndexOf(other) < 0 || sim.Enemies[sim.Enemies.IndexOf(other)].Dead, "doubles fade");
        }

        [Test]
        public void IceKnight_ShellTakesMost_UntilItCracks()
        {
            var (sim, p, lair) = Lair(Biome.Frost);
            int k = Living(sim, EnemyType.IceKnight)[0];
            float before = sim.Enemies[k].Health;
            sim.DamageEnemy(k, 100f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(15f, before - sim.Enemies[k].Health, 0.5f, "the shell takes most");
            for (int i = 1; i < RunSimulation.IceShellHits; i++) sim.DamageEnemy(k, 1f, p, HitFlags.None, Vector2.zero, 0f);
            before = sim.Enemies[k].Health;
            sim.DamageEnemy(k, 100f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(100f, before - sim.Enemies[k].Health, 0.5f, "cracked");
            Run(sim, RunSimulation.IceShellDown + 0.3f, lair);
            k = Living(sim, EnemyType.IceKnight)[0];
            before = sim.Enemies[k].Health;
            sim.DamageEnemy(k, 100f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(15f, before - sim.Enemies[k].Health, 0.5f, "the shell grew back");
        }

        [Test]
        public void Bombsmith_ThrowsLitKegs_ThatBurst()
        {
            var (sim, _, lair) = Lair(Biome.Forge);
            bool thrown = false, burst = false;
            for (int i = 0; i < 40 && !burst; i++)
            {
                var events = Run(sim, 0.5f, lair);
                thrown |= Living(sim, EnemyType.LitKeg).Count > 0;
                burst |= thrown && events.Any(e => e.Type == SimEventType.HazardFire && e.Int == (int)GimmickKind.PowderKeg);
            }
            Assert.IsTrue(thrown, "kegs thrown");
            Assert.IsTrue(burst, "and they burst by themselves");
        }
    }
}
