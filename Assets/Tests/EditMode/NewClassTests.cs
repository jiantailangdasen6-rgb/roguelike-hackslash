using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Guardian, assassin and alchemist: their abilities, unlocking, and that every class is complete.</summary>
    public class NewClassTests
    {
        private const float Dt = 1f / 30f;

        private static RunSimulation NewSim(ClassId cls, out SimPlayer player)
        {
            var sim = new RunSimulation(7);
            player = sim.AddPlayer(0, new Loadout { classId = cls });
            player.Position = Vector2.zero;
            player.Aim = Vector2.right * 3f;
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

        private static int Spawn(RunSimulation sim, EnemyType type, Vector2 at, float hpMul = 10f)
        {
            sim.Enemies.Spawn(type, at, hpMul, 1f, false);
            int i = sim.Enemies.Count - 1;
            sim.Enemies[i].Cooldown = 99f; // passive: the test only sees what the player does
            return i;
        }

        private static void Press(RunSimulation sim, SimPlayer p, Vector2 aim, bool attack = false, int skill1 = 0, int skill2 = 0)
        {
            sim.SetPlayerInput(p.ClientId, new PlayerInput { Aim = aim, Attack = attack, Skill1Count = (byte)skill1, Skill2Count = (byte)skill2 });
        }

        private static float Hp(RunSimulation sim, int i) => sim.Enemies[i].Health;

        [Test]
        public void EveryClass_HasArtSkinsMasteryAndPerks()
        {
            foreach (ClassId cls in System.Enum.GetValues(typeof(ClassId)))
            {
                var def = ClassCatalog.Get(cls);
                Assert.AreEqual(cls, def.Id);
                Assert.IsNotEmpty(def.Sheet);
                Assert.AreEqual(SkinCatalog.PerClass, SkinCatalog.For(cls).Count(), $"{def.Name} skins");
                Assert.AreEqual(12, MetaCatalog.NodesFor(cls).Count(), $"{def.Name} mastery");
                Assert.GreaterOrEqual(PerkCatalog.All.Count(p => p.Class == cls && !p.Locked), 3, $"{def.Name} open perks");
                foreach (var node in MetaCatalog.NodesFor(cls).Where(n => n.Kind == MasteryKind.UnlockPerk))
                    Assert.AreEqual(cls, PerkCatalog.Get(node.Perk).Class, $"{node.Id} unlocks another class's perk");
            }
            Assert.AreEqual(PerkCatalog.Count, PerkCatalog.All.Length, "every PerkId needs a definition");
        }

        [Test]
        public void NewClasses_AreLockedUntilTheirRequestIsClaimed()
        {
            var profile = ProfileData.Default();
            profile.selectedClass = ClassId.Guardian;
            Assert.IsFalse(profile.IsClassOpen(ClassId.Guardian));
            Assert.AreEqual(ClassId.Warrior, profile.ToLoadout().classId, "a locked class can't be taken into a run");

            foreach (ClassId cls in new[] { ClassId.Guardian, ClassId.Assassin, ClassId.Alchemist })
            {
                var quest = QuestCatalog.Chains.SelectMany(c => c).FirstOrDefault(q => q.Unlock == ClassCatalog.UnlockId(cls));
                Assert.IsNotNull(quest, $"no request unlocks {cls}");
            }
            profile.unlocked.Add(ClassCatalog.UnlockId(ClassId.Guardian));
            Assert.IsTrue(profile.IsClassOpen(ClassId.Guardian));
            Assert.AreEqual(ClassId.Guardian, profile.ToLoadout().classId);
        }

        [Test]
        public void Guardian_HammerIsHeavy_BashStuns_SanctuaryProtects()
        {
            var sim = NewSim(ClassId.Guardian, out var p);
            int front = Spawn(sim, EnemyType.Skeleton, new Vector2(1.3f, 0f));
            float before = Hp(sim, front);
            var events = Run(sim, 0.3f, s => Press(s, p, Vector2.right * 3f, attack: true));
            Assert.Less(Hp(sim, front), before);
            Assert.That(events.Any(e => e.Type == SimEventType.EnemyHit && ((HitFlags)e.Int & HitFlags.Heavy) != 0), "hammer blows are heavy (they shatter ice)");

            // Shield bash: the owner moves itself; the sim sweeps the path and stuns.
            var sim2 = NewSim(ClassId.Guardian, out var g);
            int target = Spawn(sim2, EnemyType.Skeleton, new Vector2(2f, 0f));
            Press(sim2, g, Vector2.right * 3f, skill1: 1);
            sim2.Step(Dt);
            g.Position = new Vector2(2.5f, 0f);
            Run(sim2, 0.2f, s => Press(s, g, Vector2.right * 3f, skill1: 1));
            Assert.Greater(sim2.Enemies[target].Stunned, 0.3f, "the bash stuns what it hits");

            // Holy ground: damage taken inside is reduced.
            var sim3 = NewSim(ClassId.Guardian, out var h);
            Spawn(sim3, EnemyType.Skeleton, new Vector2(12f, 6f)); // keeps the (unstarted) room from counting as cleared
            var ally = sim3.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            ally.Position = new Vector2(1f, 0f);
            float hp0 = ally.Hp;
            sim3.HurtPlayer(ally, 40f);
            float plain = hp0 - ally.Hp;
            ally.Hp = hp0;
            ally.InvulnerableTimer = 0f;
            Press(sim3, h, Vector2.right, skill2: 1);
            var zone = Run(sim3, Dt * 2, s => Press(s, h, Vector2.right, skill2: 1));
            Assert.That(zone.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.Sanctuary && e.Size.x > 1f));
            ally.InvulnerableTimer = 0f;
            sim3.HurtPlayer(ally, 40f);
            Assert.Less(hp0 - ally.Hp, plain * 0.8f, "less damage on holy ground");
        }

        [Test]
        public void Assassin_ShadowStepStrikesOnArrival_AndAlwaysCritsCrippledEnemies()
        {
            var sim = NewSim(ClassId.Assassin, out var p);
            int far = Spawn(sim, EnemyType.Skeleton, new Vector2(5f, 0f));
            float before = Hp(sim, far);
            Press(sim, p, new Vector2(5f, 0f), skill1: 1);
            sim.Step(Dt);
            p.Position = new Vector2(4.6f, 0f); // the owner arrived (moves itself)
            var events = Run(sim, 0.3f, s => Press(s, p, new Vector2(1f, 0f), skill1: 1));
            Assert.Less(Hp(sim, far), before, "arrival slash");
            Assert.That(events.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.ShadowStep));

            // Every blow on a stunned enemy is critical.
            var sim2 = NewSim(ClassId.Assassin, out var a);
            a.Stats.SetBase(StatType.CritChance, 0f);
            int stunned = Spawn(sim2, EnemyType.Skeleton, new Vector2(1.1f, 0f));
            sim2.ApplyStatus(stunned, StatusKind.Stunned, 5f);
            var hits = Run(sim2, 0.5f, s => Press(s, a, Vector2.right * 3f, attack: true)).Where(e => e.Type == SimEventType.EnemyHit).ToList();
            Assert.IsNotEmpty(hits);
            Assert.That(hits.All(e => ((HitFlags)e.Int & HitFlags.Crit) != 0), "crippled enemies always take critical hits");

            // Smoke bomb: stuns around, and crits for a while on anything.
            var sim3 = NewSim(ClassId.Assassin, out var s3);
            s3.Stats.SetBase(StatType.CritChance, 0f);
            int near = Spawn(sim3, EnemyType.Skeleton, new Vector2(1.5f, 0f));
            Press(sim3, s3, Vector2.right, skill2: 1);
            sim3.Step(Dt);
            Assert.Greater(sim3.Enemies[near].Stunned, 0.5f);
            Assert.Greater(s3.SureCritTimer, 1f);
        }

        [Test]
        public void Alchemist_FlaskBursts_FirePoolBurnsAndLightsKegs_PotionHeals()
        {
            var sim = NewSim(ClassId.Alchemist, out var p);
            int a = Spawn(sim, EnemyType.Skeleton, new Vector2(4f, 0f));
            int b = Spawn(sim, EnemyType.Skeleton, new Vector2(4f, 0.8f));
            float ha = Hp(sim, a), hb = Hp(sim, b);
            var events = Run(sim, 0.8f, s => Press(s, p, new Vector2(4f, 0f), attack: true));
            Assert.Less(Hp(sim, a), ha);
            Assert.Less(Hp(sim, b), hb, "the burst splashes neighbours");
            Assert.That(events.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.FlaskBurst));

            // Fire flask: lobbed over a monster in the way, burns where it lands, sets kegs off.
            var sim2 = NewSim(ClassId.Alchemist, out var f);
            int inTheWay = Spawn(sim2, EnemyType.Skeleton, new Vector2(2f, 0f));
            int burning = Spawn(sim2, EnemyType.Skeleton, new Vector2(5f, 0f));
            sim2.Enemies.Spawn(EnemyType.PowderKeg, new Vector2(5.8f, 0.5f), 1f, 1f, false);
            float hIn = Hp(sim2, inTheWay), hB = Hp(sim2, burning);
            var fire = Run(sim2, 2f, s => Press(s, f, new Vector2(5f, 0f), skill1: 1));
            Assert.AreEqual(hIn, Hp(sim2, inTheWay), 0.01f, "lobbed flasks fly over");
            Assert.Less(Hp(sim2, burning), hB, "burning ground hurts");
            Assert.That(fire.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.FirePool));
            Assert.That(fire.Any(e => e.Type == SimEventType.HazardWarn && (GimmickKind)e.Int == GimmickKind.PowderKeg), "the keg caught fire");

            // Healing potion thrown at a hurt ally.
            var sim3 = NewSim(ClassId.Alchemist, out var h);
            var ally = sim3.AddPlayer(1, new Loadout { classId = ClassId.Warrior });
            ally.Position = new Vector2(4f, 0f);
            ally.Hp = ally.MaxHp * 0.4f;
            Run(sim3, 1f, s => Press(s, h, new Vector2(4f, 0f), skill2: 1));
            Assert.Greater(ally.Hp, ally.MaxHp * 0.5f);
        }

        [Test]
        public void NewClassPerks_OnlyOfferedToTheirClass()
        {
            var rng = new GameRandom(9);
            foreach (ClassId cls in new[] { ClassId.Guardian, ClassId.Assassin, ClassId.Alchemist })
            {
                var p = new SimPlayer(0, Vector2.zero, new Loadout { classId = cls });
                for (int i = 0; i < 150; i++)
                    foreach (var o in PerkRoller.Roll(rng, p))
                    {
                        if (o.IsHeal) continue;
                        var def = PerkCatalog.Get((PerkId)o.Perk);
                        Assert.That(!def.Class.HasValue || def.Class == cls, $"{def.Name} offered to {cls}");
                        Assert.IsFalse(def.Locked);
                    }
            }
        }
    }
}
