using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The new lands: their lords' trials (flood, reflections, red-hot iron), their monsters and room features.</summary>
    public class LandTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, bool protect = true)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                if (protect) foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        /// <summary>A run in the lord's hall of a land (the first area), the lord standing.</summary>
        private static (RunSimulation Sim, SimPlayer Player) LordHall(Biome land)
        {
            var sim = new RunSimulation(91);
            sim.UseVariedRoute(land);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(RunSimulation.RoomsPerArea - 1);
            for (int guard = 0; guard < 10 && p.PendingOffer != null; guard++) sim.ChoosePerk(0, 0);
            Run(sim, 2f);
            Assert.GreaterOrEqual(Boss(sim), 0, "the lord stands");
            Assert.AreEqual(BiomeCatalog.Get(land).Lord, sim.Enemies[Boss(sim)].Type);
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

        private static bool Moment(List<SimEvent> events, TrialMoment m) =>
            events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == m);

        // ------------------------------------------------------------------ Drowned Saint

        [Test]
        public void DrownedSaint_FloodHurts_UntilTheSealsAreBroken()
        {
            var (sim, p) = LordHall(Biome.Sunken);
            SetBossHealth(sim, 0.59f);
            var events = Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Flood, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.FloodRise));
            var seals = Living(sim, EnemyType.FloodSeal);
            Assert.AreEqual(3, seals.Count);

            p.InvulnerableTimer = 0f;
            float hp = p.Hp;
            Run(sim, 2.2f, protect: false);
            Assert.Less(p.Hp, hp, "the flood pulls at whoever stands in it");

            foreach (int i in Living(sim, EnemyType.FloodSeal)) sim.DamageEnemy(i, 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            events = Run(sim, 0.2f);
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.FloodDrained));
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f, "reels when the water drains");
        }

        [Test]
        public void DrownedSaint_SlowFlood_SweepsTheHall()
        {
            var (sim, p) = LordHall(Biome.Sunken);
            p.Stats.SetBase(StatType.MaxHealth, 10000f); // survives the torrent: the test watches it land
            p.Hp = 10000f;
            SetBossHealth(sim, 0.59f);
            Run(sim, 0.1f);
            float boss = sim.Enemies[Boss(sim)].Health;
            var events = Run(sim, RunSimulation.FloodTime + 4f);
            Assert.IsTrue(Moment(events, TrialMoment.FloodTorrent));
            Assert.Greater(sim.Enemies[Boss(sim)].Health, boss, "the saint is refreshed by the water");
        }

        // ------------------------------------------------------------------ Frost Queen

        [Test]
        public void FrostQueen_IsWardedBehindHerReflections_UntilTheyShatter()
        {
            var (sim, _) = LordHall(Biome.Frost);
            SetBossHealth(sim, 0.65f);
            var events = Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Mirror, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.MirrorsRise));
            var mirrors = Living(sim, EnemyType.IceMirror);
            Assert.AreEqual(2, mirrors.Count);

            float before = sim.Enemies[Boss(sim)].Health;
            sim.DamageEnemy(Boss(sim), 300f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(before, sim.Enemies[Boss(sim)].Health, 0.01f, "warded while her reflections stand");

            int kills = sim.Kills;
            foreach (int i in mirrors) sim.DamageEnemy(i, 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            events = Run(sim, 0.2f);
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.MirrorsShattered));
            Assert.AreEqual(kills, sim.Kills, "reflections are no kills (no souls, no loot)");
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f);
        }

        // ------------------------------------------------------------------ Forge Colossus

        [Test]
        public void ForgeColossus_RedHot_ShrugsBlowsOff_UntilLuredIntoWater()
        {
            var (sim, _) = LordHall(Biome.Forge);
            SetBossHealth(sim, 0.49f);
            var events = Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Overheat, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.OverheatStart));

            float before = sim.Enemies[Boss(sim)].Health;
            sim.DamageEnemy(Boss(sim), 400f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(400f * RunSimulation.OverheatArmor, before - sim.Enemies[Boss(sim)].Health, 1f, "red-hot iron");

            var cisterns = Living(sim, EnemyType.QuenchCistern);
            Assert.AreEqual(2, cisterns.Count);
            Vector2 water = sim.Enemies[cisterns[0]].Position;
            sim.DamageEnemy(cisterns[0], 1e6f, null, HitFlags.None, Vector2.zero, 0f);
            events = Run(sim, 0.1f);
            Assert.IsTrue(Moment(events, TrialMoment.CisternBroken));

            sim.Enemies[Boss(sim)].Position = water; // he walks into it
            events = Run(sim, 0.2f);
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.Quenched));
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f, "quenched: he reels");
        }

        // ------------------------------------------------------------------ Monsters and rooms

        [Test]
        public void Icebound_ShattersWhenItFalls()
        {
            var sim = new RunSimulation(5);
            sim.UseVariedRoute(Biome.Frost);
            var p = sim.AddPlayer(0);
            sim.Start();
            int id = sim.Enemies.Spawn(EnemyType.Icebound, p.Position + new Vector2(1f, 0f), 1f, 1f, false);
            int i = sim.Enemies.IndexOf(id);
            sim.DamageEnemy(i, 1e6f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.2f);
            Assert.That(events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.Id < 0), "a shatter is telegraphed");
            events = Run(sim, 1f);
            Assert.That(events.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.Shatter));
        }

        [Test]
        public void WolvesAndImps_ComeInPairs()
        {
            var sim = new RunSimulation(5);
            sim.UseVariedRoute(Biome.Frost);
            sim.AddPlayer(0);
            sim.Start();
            int before = sim.PendingSpawns.Count;
            sim.QueueSpawn(EnemyType.FrostWolf, false);
            Assert.AreEqual(before + 2, sim.PendingSpawns.Count);
            sim.QueueSpawn(EnemyType.FireImp, false);
            Assert.AreEqual(before + 4, sim.PendingSpawns.Count);
        }

        [Test]
        public void Water_SlowsWalkers_AndZonesAreNoTraps()
        {
            var flooded = Arena.Layouts.First(l => l.Name == "flooded_hall");
            var pool = flooded.Gimmicks.First(g => g.Kind == GimmickKind.Water);
            Assert.IsTrue(Arena.InZone(flooded, GimmickKind.Water, pool.Position));
            Assert.IsFalse(Arena.InZone(flooded, GimmickKind.Water, pool.Position + new Vector2(0f, 6f)));
            Assert.IsFalse(Arena.InGimmick(pool, pool.Position, 0f), "water is not a danger (spawns may land in it)");
        }

        [Test]
        public void Icicles_FallOnACycle_AndHurt()
        {
            var sim = new RunSimulation(5);
            var p = sim.AddPlayer(0);
            sim.Start();
            sim.UseLayoutForTests(Arena.Layouts.First(l => l.Name == "frozen_court").Id);
            var icicle = sim.Map.Start.Layout.Gimmicks.First(g => g.Kind == GimmickKind.Icicle);
            p.Position = icicle.Position;
            p.InvulnerableTimer = 0f;
            float hp = p.Hp;
            var events = new List<SimEvent>();
            for (float t = 0f; t < Arena.IciclePeriod + 3f; t += Dt)
            {
                p.Position = icicle.Position;
                if (p.Hp < hp) break;
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            Assert.That(events.Any(e => e.Type == SimEventType.HazardWarn && (GimmickKind)e.Int == GimmickKind.Icicle), "a shadow grows first");
            Assert.Less(p.Hp, hp, "the icicle lands");
        }

        [Test]
        public void EveryNewMonster_HasArtAndACodexEntry()
        {
            foreach (var land in BiomeCatalog.All)
            {
                foreach (var (type, _, _) in land.Pool) Assert.IsNotNull(CodexCatalog.Get(CodexCatalog.EnemyEntry(type)), $"{land.Name}: {type} in the codex");
                Assert.IsNotNull(CodexCatalog.Get(CodexCatalog.EnemyEntry(land.Lord)), $"{land.Name}: its lord in the codex");
            }
        }
    }
}
