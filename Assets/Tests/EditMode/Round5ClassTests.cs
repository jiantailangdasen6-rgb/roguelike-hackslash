using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The deathcaller (bone servants, the curse) and the paladin (judgment, devotion).</summary>
    public class Round5ClassTests
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
            sim.Enemies[i].RoomId = sim.Map.Start.Id;
            return i;
        }

        private static int Servants(RunSimulation sim)
        {
            int n = 0;
            for (int i = 0; i < sim.Enemies.Count; i++) if (!sim.Enemies[i].Dead && sim.Enemies[i].Type == EnemyType.BoneServant) n++;
            return n;
        }

        private static void Press(RunSimulation sim, SimPlayer p, Vector2 aim, int skill1 = 0, int skill2 = 0) =>
            sim.SetPlayerInput(p.ClientId, new PlayerInput { Aim = aim, Skill1Count = (byte)skill1, Skill2Count = (byte)skill2 });

        [Test]
        public void BoneServants_RiseAtTheAim_HuntMonsters_AndKeepToTheirCap()
        {
            var sim = new RunSimulation(41);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Deathcaller });
            sim.Start();
            Run(sim, 0.1f);
            Vector2 o = sim.Map.Start.Origin;
            p.Position = o + new Vector2(-4f, -2f);
            int monster = Spawn(sim, EnemyType.Skeleton, o + new Vector2(0f, -2f), 30f);
            float before = sim.Enemies[monster].Health;

            Press(sim, p, new Vector2(3f, 0f), skill1: 1);
            Run(sim, 3f, s => p.Hp = p.MaxHp);
            Assert.AreEqual(ClassCatalog.RaiseCount, Servants(sim));
            int m = sim.Enemies.IndexOf(sim.Enemies[monster].Id);
            Assert.Less(sim.Enemies[m < 0 ? monster : m].Health, before, "the servants cut at it");

            // Spell after spell: never more than the cap.
            for (int k = 2; k <= 4; k++)
            {
                p.Skill1Cooldown = 0f;
                Press(sim, p, new Vector2(3f, 0f), skill1: k);
                Run(sim, 0.6f, s => p.Hp = p.MaxHp);
            }
            Assert.LessOrEqual(Servants(sim), ClassCatalog.ServantCap);
        }

        [Test]
        public void TheCurse_MakesMonstersTakeMore_AndTheCursedRiseAsServants()
        {
            var sim = new RunSimulation(42);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Deathcaller });
            sim.Start();
            Run(sim, 0.1f);
            Vector2 o = sim.Map.Start.Origin;
            p.Position = o + new Vector2(-4f, -2f);
            int a = Spawn(sim, EnemyType.Skeleton, o + new Vector2(0f, -2f), 50f);

            float before = sim.Enemies[a].Health;
            sim.DamageEnemy(a, 10f, null, HitFlags.DamageOverTime, Vector2.zero, 0f);
            float plain = before - sim.Enemies[a].Health;

            Press(sim, p, new Vector2(4f, 0f), skill2: 1);
            Run(sim, 0.1f);
            Assert.Greater(sim.Enemies[a].Cursed, 0f);
            before = sim.Enemies[a].Health;
            sim.DamageEnemy(a, 10f, null, HitFlags.DamageOverTime, Vector2.zero, 0f);
            Assert.AreEqual(plain * (1f + ClassCatalog.CurseDamageBonus), before - sim.Enemies[a].Health, 0.01f);

            sim.DamageEnemy(a, 99999f, p, HitFlags.DamageOverTime, Vector2.zero, 0f);
            Run(sim, 1f, s => p.Hp = p.MaxHp);
            Assert.AreEqual(1, Servants(sim), "the cursed one rose on our side");
        }

        [Test]
        public void Devotion_TakesHalfOfATeammatesWound()
        {
            var sim = new RunSimulation(43);
            var paladin = sim.AddPlayer(0, new Loadout { classId = ClassId.Paladin });
            var mate = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            Run(sim, 0.1f);
            mate.Position = paladin.Position + new Vector2(2f, 0f);
            Press(sim, paladin, Vector2.right, skill2: 1);
            Run(sim, 0.1f);
            Assert.Greater(paladin.DevotionTimer, 0f);
            paladin.Shield = mate.Shield = 0f;
            float pHp = paladin.Hp, mHp = mate.Hp;
            mate.InvulnerableTimer = 0f;
            sim.HurtPlayer(mate, 40f);
            float taken = mHp - mate.Hp, shared = pHp - paladin.Hp;
            Assert.Greater(shared, 0f, "the paladin took a share");
            Assert.AreEqual(taken, shared, 0.6f, "half and half");
        }

        [Test]
        public void Judgment_StunsMonsters_AndHealsTeammatesInTheLight()
        {
            var sim = new RunSimulation(44);
            var paladin = sim.AddPlayer(0, new Loadout { classId = ClassId.Paladin });
            var mate = sim.AddPlayer(1, new Loadout { classId = ClassId.Ranger });
            sim.Start();
            Run(sim, 0.1f);
            Vector2 spot = paladin.Position + new Vector2(4f, 0f);
            int e = Spawn(sim, EnemyType.Skeleton, spot, 50f);
            mate.Position = spot + new Vector2(0.6f, 0f);
            mate.Hp = 30f;
            Press(sim, paladin, new Vector2(4f, 0f), skill1: 1);
            var events = Run(sim, ClassCatalog.JudgmentDelay + 0.2f, s => s.Enemies[e].Position = spot);
            Assert.IsTrue(events.Any(ev => ev.Type == SimEventType.AreaEffect && (AreaKind)ev.Int == AreaKind.Judgment));
            Assert.Greater(sim.Enemies[e].Stunned, 0f);
            Assert.Greater(mate.Hp, 30f);
        }

        [Test]
        public void NewClassGear_PacksAndUnpacks_WithItsLegendary()
        {
            var item = ItemGenerator.Generate(123ul, 5, 9f, ClassId.Paladin, Rarity.Legendary, EquipSlot.Weapon);
            item.legendary = LegendaryId.Sunburst;
            var back = ItemCodec.Unpack(ItemCodec.Pack(item));
            Assert.AreEqual(ClassId.Paladin, back.classId);
            Assert.AreEqual(LegendaryId.Sunburst, back.legendary);
        }

        [Test]
        public void PlayersWhoFinishedTheRequestsAlready_GetTheNewClasses()
        {
            var p = new ProfileData();
            p.quests.steps = new List<int> { 5, 5, 0 }; // master and crone past their fifth request
            var loaded = ProfileData.FromJson(p.ToJson());
            Assert.IsTrue(loaded.IsClassOpen(ClassId.Deathcaller));
            Assert.IsTrue(loaded.IsClassOpen(ClassId.Paladin));
        }
    }
}
