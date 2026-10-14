using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The fungal grotto and the cursed library: their traps, champions and the four lords' trials.</summary>
    public class Lands2Tests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> each = null, bool invulnerable = true)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                if (invulnerable) foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                each?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static (RunSimulation, SimPlayer) Hall(Biome land, EnemyType lord)
        {
            var sim = new RunSimulation(61);
            sim.ForcedLord = lord;
            sim.UseVariedRoute(land);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(RunSimulation.RoomsPerArea - 1);
            for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
            Run(sim, 2f);
            Assert.AreEqual(lord, sim.Enemies[Boss(sim)].Type);
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

        private static void SetHp(RunSimulation sim, float f)
        {
            ref var b = ref sim.Enemies[Boss(sim)];
            b.Health = b.MaxHealth * f;
        }

        private static bool Moment(List<SimEvent> events, TrialMoment m) =>
            events.Any(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == m);

        [Test]
        public void NewLands_AreOnTheRoutes_WithTheirOwnRooms()
        {
            foreach (var land in new[] { Biome.Grotto, Biome.Library })
            {
                Assert.Contains(BiomeCatalog.Get(land), BiomeCatalog.Routable);
                foreach (int id in BiomeCatalog.Get(land).FightLayouts) Assert.AreEqual(id, Arena.Get(id).Id, $"{land}: layout {id} exists");
                var sim = new RunSimulation(5);
                sim.UseVariedRoute(land);
                Assert.AreEqual(land, sim.Biome);
                Assert.That(BiomeCatalog.Get(land).Lords, Does.Contain(sim.AreaBoss));
            }
        }

        [Test]
        public void MyceliumMother_Fog_HurtsAwayFromTheCaps_AndPassesWhenOutlasted()
        {
            var (sim, p) = Hall(Biome.Grotto, EnemyType.MyceliumMother);
            SetHp(sim, 0.64f);
            var events = Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Fog, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.FogStart));
            Assert.AreEqual(3, Living(sim, EnemyType.Glowcap).Count);

            // Out in the fog it hurts; by a cap it does not (nothing else about: no minions, the mother reeling).
            void Quiet(RunSimulation s)
            {
                s.PendingSpawns.Clear();
                for (int i = 0; i < s.Enemies.Count; i++)
                    if (!s.Enemies[i].Def.Boss && !s.Enemies[i].Def.Prop) s.Enemies[i].Dead = true;
                s.Enemies[Boss(s)].Stunned = 5f;
            }
            var caps = Living(sim, EnemyType.Glowcap).Select(i => sim.Enemies[i].Position).ToList();
            Vector2 o = sim.Map.Boss.Origin;
            Vector2 far = new[] { o + new Vector2(-11f, -5f), o + new Vector2(11f, -5f), o + new Vector2(-11f, 4f), o + new Vector2(11f, 4f), o + new Vector2(0f, -6f) }
                .OrderByDescending(c => caps.Min(k => (k - c).sqrMagnitude)).First();
            p.InvulnerableTimer = 0f;
            float hp = p.Hp;
            Run(sim, 2.2f, s => { Quiet(s); s.SetPlayerPosition(0, far); }, invulnerable: false);
            Assert.Less(p.Hp, hp, "the fog hurts");
            p.Hp = p.MaxHp;
            p.InvulnerableTimer = 0f;
            Run(sim, 1.5f, s => { Quiet(s); s.SetPlayerPosition(0, s.Enemies[Living(s, EnemyType.Glowcap)[0]].Position + new Vector2(0.6f, 0f)); }, invulnerable: false);
            Assert.AreEqual(p.MaxHp, p.Hp, 0.01f, "clean air by a cap");

            events = Run(sim, RunSimulation.FogTime + 3f);
            Assert.IsTrue(Moment(events, TrialMoment.CapWilt), "the caps wilt and sprout elsewhere");
            Assert.IsTrue(Moment(events, TrialMoment.FogClears));
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
        }

        [Test]
        public void RotGiant_Parasite_IsCuredAtALily()
        {
            var (sim, p) = Hall(Biome.Grotto, EnemyType.RotGiant);
            SetHp(sim, 0.59f);
            Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Parasite, sim.ActiveTrial);
            Assert.That(sim.ParasiteHosts, Does.Contain(0ul));
            var lily = Living(sim, EnemyType.Lily);
            Assert.AreEqual(2, lily.Count);
            Vector2 at = sim.Enemies[lily[0]].Position;
            var events = Run(sim, RunSimulation.CureTime + 0.4f, s => s.SetPlayerPosition(0, at + new Vector2(0.5f, 0f)));
            Assert.IsTrue(Moment(events, TrialMoment.ParasiteCured));
            Assert.IsTrue(Moment(events, TrialMoment.ParasiteGone), "every host cured: he reels");
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f);
        }

        [Test]
        public void RotGiant_Parasite_JumpsToAFriendStandingClose()
        {
            var sim = new RunSimulation(61);
            sim.ForcedLord = EnemyType.RotGiant;
            sim.UseVariedRoute(Biome.Grotto);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.AddPlayer(2, new Loadout { classId = ClassId.Ranger });
            sim.Start(RunSimulation.RoomsPerArea - 1);
            for (int k = 0; k < 30; k++) foreach (var q in sim.Players) if (q.PendingOffer != null) sim.ChoosePerk(q.ClientId, 0);
            Run(sim, 2f);
            SetHp(sim, 0.59f);
            Run(sim, 0.1f);
            int hosts = sim.ParasiteHosts.Count;
            Assert.AreEqual(2, hosts, "half the party (rounded up) carries it");
            Vector2 spot = sim.Map.Boss.Origin + new Vector2(0f, -5f);
            var events = Run(sim, RunSimulation.ParasiteJumpEvery + 0.3f, s =>
            {
                for (int k = 0; k < 3; k++) s.SetPlayerPosition((ulong)k, spot + new Vector2(k * 0.5f, 0f));
            });
            Assert.IsTrue(Moment(events, TrialMoment.ParasiteJump));
            Assert.AreEqual(3, sim.ParasiteHosts.Count);
        }

        [Test]
        public void Archivist_Tomes_StruckInTheirOrder_ExposeHim()
        {
            var (sim, p) = Hall(Biome.Library, EnemyType.Archivist);
            SetHp(sim, 0.65f);
            var shown = Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Sequence, sim.ActiveTrial);
            shown.AddRange(Run(sim, 6f));
            var order = shown.Where(e => e.Type == SimEventType.TrialEvent && (TrialMoment)e.Int == TrialMoment.TomeShine).OrderBy(e => e.Id).Select(e => e.Pos).ToList();
            Assert.AreEqual(4, order.Count, "each tome shines once");

            int TomeAt(Vector2 pos) => Living(sim, EnemyType.ForbiddenTome).First(i => (sim.Enemies[i].Position - pos).sqrMagnitude < 0.01f);
            // A wrong one first: it bursts and the order shines again.
            sim.DamageEnemy(TomeAt(order[1]), 10f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.1f);
            Assert.IsTrue(Moment(events, TrialMoment.TomeWrong));
            Run(sim, 6f);
            foreach (var pos in order)
            {
                sim.DamageEnemy(TomeAt(pos), 10f, p, HitFlags.None, Vector2.zero, 0f);
                events = Run(sim, 0.4f);
            }
            Assert.IsTrue(Moment(events, TrialMoment.TomesRead));
            Assert.AreEqual(BossTrial.None, sim.ActiveTrial);
        }

        [Test]
        public void InkBeast_Dive_EndsWhenTheCandlesAreLit()
        {
            var (sim, _) = Hall(Biome.Library, EnemyType.InkBeast);
            SetHp(sim, 0.59f);
            var events = Run(sim, 0.1f);
            Assert.AreEqual(BossTrial.Dive, sim.ActiveTrial);
            Assert.IsTrue(Moment(events, TrialMoment.InkDive));
            events = Run(sim, 2.5f);
            Assert.That(events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.Id < 0), "it bursts up under players");
            foreach (int c in Living(sim, EnemyType.Candelabra))
            {
                Vector2 at = sim.Enemies[c].Position;
                events = Run(sim, RunSimulation.CandleTime + 0.2f, s => s.SetPlayerPosition(0, at + new Vector2(0.5f, 0f)));
            }
            Assert.IsTrue(Moment(events, TrialMoment.InkDriven));
            Assert.Greater(sim.Enemies[Boss(sim)].Stunned, 3f);
        }

        [Test]
        public void Bookworm_SplitsInTwo_GreatCap_PuffsWhenStruck()
        {
            var sim = new RunSimulation(9);
            sim.UseVariedRoute(Biome.Library);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start();
            for (int i = 0; i < 10 && p.PendingOffer != null; i++) sim.ChoosePerk(0, 0);
            int id = sim.Enemies.Spawn(EnemyType.Bookworm, p.Position + new Vector2(3f, 0f), 1f, 1f, false);
            sim.DamageEnemy(sim.Enemies.IndexOf(id), 1e6f, p, HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.2f);
            Assert.AreEqual(2, Living(sim, EnemyType.Wormling).Count);

            int cap = sim.Enemies.Spawn(EnemyType.GreatCap, p.Position + new Vector2(-3f, 0f), 50f, 1f, false);
            sim.Events.Clear();
            sim.DamageEnemy(sim.Enemies.IndexOf(cap), 5f, p, HitFlags.None, Vector2.zero, 0f);

            sim.DamageEnemy(sim.Enemies.IndexOf(cap), 5f, p, HitFlags.None, Vector2.zero, 0f);

            Assert.AreEqual(1, sim.Events.Count(e => e.Type == SimEventType.EnemyTelegraph && e.Id < 0), "one puff, then a pause");
        }
    }
}
