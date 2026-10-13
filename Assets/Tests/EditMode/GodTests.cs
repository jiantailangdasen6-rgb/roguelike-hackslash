using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Gods' blessings: how they are offered, what they do, and the favour of two from one god.</summary>
    public class GodTests
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

        private static int Spawn(RunSimulation sim, EnemyType type, Vector2 at, float hpMul = 10f)
        {
            sim.Enemies.Spawn(type, at, hpMul, 1f, false);
            int i = sim.Enemies.Count - 1;
            sim.Enemies[i].Cooldown = 99f;
            return i;
        }

        private static int Signs(IEnumerable<SimEvent> events, GodSignKind kind) =>
            events.Count(e => e.Type == SimEventType.GodSign && (GodSignKind)e.Int == kind);

        [Test]
        public void EveryGodHasThreeBlessings_AndLevelUpsNeverOfferThem()
        {
            foreach (var god in GodCatalog.All) Assert.AreEqual(3, GodCatalog.Blessings(god.Id).Count, god.Name);
            var rng = new GameRandom(5);
            var p = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Mage });
            for (int i = 0; i < 200; i++)
                foreach (var o in PerkRoller.Roll(rng, p))
                    Assert.IsFalse(o.IsPerk && PerkCatalog.Get((PerkId)o.Perk).God.HasValue, "a blessing in a level-up");
        }

        [Test]
        public void BlessingOffer_ShowsThreeDifferentGods_AndFavoursAGodHalfWayThere()
        {
            var rng = new GameRandom(9);
            var p = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Ranger });
            var offer = PerkRoller.RollBlessing(rng, p);
            Assert.AreEqual(3, offer.Length);
            var gods = offer.Select(o => PerkCatalog.Get((PerkId)o.Perk).God.Value).ToList();
            Assert.AreEqual(3, gods.Distinct().Count(), "one card per god");

            p.AddPerk(PerkId.StormFist);
            int storm = 0;
            for (int i = 0; i < 300; i++)
                if (PerkRoller.RollBlessing(rng, p).Any(o => PerkCatalog.Get((PerkId)o.Perk).God == GodId.Storm)) storm++;
            Assert.Greater(storm, 270, "the god one blessing away from its favour shows up almost always");

            // Everything taken: nothing left to offer.
            foreach (var def in PerkCatalog.All) if (def.God.HasValue) p.AddPerk(def.Id);
            Assert.IsNull(PerkRoller.RollBlessing(rng, p));
        }

        [Test]
        public void StockedBlessing_IsOfferedAsBlessing_AndTakingTheSecondAwakensTheFavour()
        {
            var sim = new RunSimulation(3);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            p.AddPerk(PerkId.FlameBrand);
            p.StockedChoices.Enqueue(ChoiceKind.Blessing);
            var events = Run(sim, 0.2f);
            var offer = events.First(e => e.Type == SimEventType.PerkOffer);
            Assert.AreEqual(ChoiceKind.Blessing, (ChoiceKind)offer.Int);

            var options = PerkOption.Unpack(offer.Seed);
            int flame = System.Array.FindIndex(options, o => PerkCatalog.Get((PerkId)o.Perk).God == GodId.Flame);
            Assume.That(flame >= 0, "the flame god is offered (weighted towards the favour)");
            sim.ChoosePerk(0, flame);
            Assert.IsTrue(p.Favored(GodId.Flame));
            Assert.AreEqual(1, Signs(sim.Events, GodSignKind.Favor));
        }

        [Test]
        public void FlameBrand_SetsMonstersAlight_AndTheFlameFavourHitsBurningOnesHarder()
        {
            var sim = new RunSimulation(4);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            p.Position = new Vector2(0f, -1.2f);
            int a = Spawn(sim, EnemyType.Skeleton, Vector2.zero, 50f);
            p.AddPerk(PerkId.FlameBrand);
            sim.DamageEnemy(a, 10f, p, HitFlags.None, Vector2.zero, 0f);
            Assert.Greater(sim.Enemies[a].Burning, 0f, "branded");

            float before = sim.Enemies[a].Health;
            sim.DamageEnemy(a, 10f, p, HitFlags.DamageOverTime, Vector2.zero, 0f);
            float plain = before - sim.Enemies[a].Health;

            p.AddPerk(PerkId.FlamePyre);
            before = sim.Enemies[a].Health;
            sim.DamageEnemy(a, 10f, p, HitFlags.DamageOverTime, Vector2.zero, 0f);
            float favored = before - sim.Enemies[a].Health;
            Assert.AreEqual(plain * (1f + GodCatalog.FavorFlameBonus), favored, 0.01f);
        }

        [Test]
        public void FlamePyre_BurningMonstersExplodeIntoTheirNeighbours()
        {
            var sim = new RunSimulation(6);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            p.Position = new Vector2(0f, -5f);
            p.AddPerk(PerkId.FlamePyre);
            int a = Spawn(sim, EnemyType.Skeleton, Vector2.zero, 1f);
            int b = Spawn(sim, EnemyType.Skeleton, new Vector2(1.2f, 0f), 50f);
            sim.ApplyStatus(a, StatusKind.Burning, 3f);
            float before = sim.Enemies[b].Health;
            sim.DamageEnemy(a, 999f, p, HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 0.3f);
            Assert.AreEqual(1, Signs(events, GodSignKind.Fire));
            int bi = sim.Enemies.IndexOf(sim.Enemies[b].Id);
            Assert.Less(sim.Enemies[bi].Health, before, "the neighbour was burnt");
        }

        [Test]
        public void StormFist_EveryFourthBlowCallsLightning()
        {
            var sim = new RunSimulation(7);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Ranger });
            p.Position = new Vector2(-4f, 0f);
            p.AddPerk(PerkId.StormFist);
            Spawn(sim, EnemyType.Skeleton, Vector2.zero, 50f);
            var events = Run(sim, 2f, s => s.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Attack = true }));
            int shots = events.Count(e => e.Type == SimEventType.PlayerAttack && (AttackAnim)e.Int == AttackAnim.BasicA);
            Assert.GreaterOrEqual(shots, 4);
            Assert.AreEqual(shots / RunSimulation.StormFistEvery, Signs(events, GodSignKind.Lightning));
        }

        [Test]
        public void FrostWard_FreezesAttackersOnceInAWhile()
        {
            var sim = new RunSimulation(8);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            p.Position = Vector2.zero;
            p.AddPerk(PerkId.FrostWard);
            int a = Spawn(sim, EnemyType.Skeleton, new Vector2(1f, 0f), 50f);
            sim.HurtPlayer(p, 5f);
            Assert.Greater(sim.Enemies[a].Frozen, 0f);
            sim.Enemies[a].Frozen = 0f;
            p.InvulnerableTimer = 0f;
            sim.HurtPlayer(p, 5f);
            Assert.AreEqual(0f, sim.Enemies[a].Frozen, "still cooling down");
        }

        [Test]
        public void LifeRise_GetsUpAloneOncePerArea()
        {
            var sim = new RunSimulation(10);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var mate = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            mate.Position = new Vector2(10f, 0f); // too far to help
            p.AddPerk(PerkId.LifeRise);
            sim.HurtPlayer(p, 9999f);
            Assert.IsFalse(p.Alive);
            var events = Run(sim, RunSimulation.RiseDelay + 0.3f);
            Assert.IsTrue(p.Alive, "got up alone");
            Assert.AreEqual(1, Signs(events, GodSignKind.Rise));
            Assert.AreEqual(p.MaxHp * RunSimulation.RiseHealth, p.Hp, 1f);

            p.InvulnerableTimer = 0f;
            sim.HurtPlayer(p, 9999f);
            Run(sim, RunSimulation.RiseDelay + 0.3f);
            Assert.IsFalse(p.Alive, "only once per area");
        }

        [Test]
        public void LifeFavour_HealsTeammatesNearby()
        {
            var sim = new RunSimulation(11);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            var mate = sim.AddPlayer(1, new Loadout { classId = ClassId.Warrior });
            p.Position = Vector2.zero;
            mate.Position = new Vector2(2f, 0f);
            p.AddPerk(PerkId.LifeShare);
            p.AddPerk(PerkId.LifeVeil);
            mate.Hp = 50f;
            Run(sim, 2f);
            Assert.AreEqual(50f + GodCatalog.FavorLifeRegen * 2f, mate.Hp, 0.3f);
            Assert.Greater(p.Shield, 0f, "the holy veil");
        }
    }
}
