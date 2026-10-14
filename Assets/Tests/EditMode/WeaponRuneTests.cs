using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Phase 4: each class's other weapon (its basic attack) and the runes set into the skills.</summary>
    public class WeaponRuneTests
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
            sim.Enemies[i].Cooldown = 99f;
            return i;
        }

        private static ItemInstance Make(string baseId, ClassId cls, RuneId rune = RuneId.None, Rarity rarity = Rarity.Rare) =>
            new() { uid = "t", baseId = baseId, classId = cls, rarity = rarity, itemLevel = 3, rune = rune };

        private static void Wear(SimPlayer p, ItemInstance item)
        {
            p.Bag.Add(item);
            Assert.IsTrue(p.Equip(p.Bag.Count - 1));
        }

        [Test]
        public void Items_PackRunesAndTheOtherWeapon_AndOldItemsReadTheSame()
        {
            var rune = Make("rune_2", ClassId.Mage, RuneId.Soul, Rarity.Legendary);
            var back = ItemCodec.Unpack(ItemCodec.Pack(rune));
            Assert.AreEqual(RuneId.Soul, back.rune);
            Assert.AreEqual(EquipSlot.Rune2, back.Slot);
            var axe = ItemCodec.Unpack(ItemCodec.Pack(Make("weapon_alt", ClassId.Warrior)));
            Assert.AreEqual("weapon_alt", axe.baseId);
            var ring = Make("copper_ring", ClassId.Ranger);
            Assert.AreEqual(RuneId.None, ItemCodec.Unpack(ItemCodec.Pack(ring)).rune);
            Assert.Less(ItemCodec.Pack(ring), 1ul << 47, "an item without a rune packs as it always did");
        }

        [Test]
        public void Loot_RollsRunes_WithAnEffectAndNoProperties()
        {
            int runes = 0, alt = 0;
            for (ulong seed = 1; seed < 600; seed++)
            {
                var item = ItemGenerator.Generate(seed, 5, 1f, ClassId.Guardian);
                if (item.Slot is EquipSlot.Rune1 or EquipSlot.Rune2)
                {
                    runes++;
                    Assert.AreNotEqual(RuneId.None, item.rune);
                    Assert.IsEmpty(item.affixes);
                    Assert.IsNotNull(ItemCatalog.GetRune(item.rune));
                }
                if (item.baseId == "weapon_alt") alt++;
            }
            Assert.Greater(runes, 40);
            Assert.Greater(alt, 20);
        }

        [Test]
        public void EveryClass_HasItsOtherWeapon_WithItsOwnAttack()
        {
            foreach (ClassId cls in System.Enum.GetValues(typeof(ClassId)))
            {
                var sim = NewSim(cls, out var p);
                Wear(p, Make("weapon_alt", cls));
                Assert.AreEqual(1, p.WeaponKind);
                int target = Spawn(sim, EnemyType.Skeleton, new Vector2(1.8f, 0f));
                float before = sim.Enemies[target].Health;
                sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right * 3f, Attack = true });
                var events = Run(sim, 0.9f);
                Assert.IsTrue(events.Any(e => e.Type == SimEventType.PlayerAttack), $"{cls}: it attacks");
                Assert.Less(sim.Enemies[target].Health, before, $"{cls}: the other weapon hits");
            }
        }

        [Test]
        public void Tome_LoosesThreeBolts_Axe_SwingsSlowerAndHeavier()
        {
            var sim = NewSim(ClassId.Mage, out var mage);
            Wear(mage, Make("weapon_alt", ClassId.Mage));
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right * 3f, Attack = true });
            var events = Run(sim, Dt * 2f);
            Assert.AreEqual(3, events.Count(e => e.Type == SimEventType.ProjectileSpawn));

            sim = NewSim(ClassId.Warrior, out var warrior);
            Wear(warrior, Make("weapon_alt", ClassId.Warrior));
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right * 3f, Attack = true });
            events = Run(sim, 1.6f);
            int swings = events.Count(e => e.Type == SimEventType.PlayerAttack);
            Assert.That(swings, Is.InRange(2, 3), "about one heavy swing every 0.8 s");
        }

        [Test]
        public void EmberRune_LeavesATrailOfFire_AlongTheDash()
        {
            var sim = NewSim(ClassId.Warrior, out var p);
            Wear(p, Make("rune_1", ClassId.Warrior, RuneId.Ember));
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right * 3f, Skill1Count = 1 });
            var events = Run(sim, 0.2f);
            Assert.AreEqual(3, events.Count(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.FirePool), "three pools along the way");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.RuneFlare));
        }

        [Test]
        public void FrostRune_Freezes_SwiftRune_ShortensTheWait()
        {
            var sim = NewSim(ClassId.Ranger, out var p);
            Wear(p, Make("rune_2", ClassId.Ranger, RuneId.Frost));
            int target = Spawn(sim, EnemyType.Skeleton, new Vector2(3f, 0f));
            sim.SetPlayerInput(0, new PlayerInput { Aim = new Vector2(3f, 0f), Skill2Count = 1 });
            Run(sim, 0.1f);
            Assert.Greater(sim.Enemies[target].Frozen, 0f);

            var plain = NewSim(ClassId.Ranger, out var a);
            plain.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Skill1Count = 1 });
            Run(plain, Dt);
            var swift = NewSim(ClassId.Ranger, out var b);
            Wear(b, Make("rune_1", ClassId.Ranger, RuneId.Swift));
            swift.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Skill1Count = 1 });
            Run(swift, Dt);
            Assert.Less(b.Skill1Cooldown, a.Skill1Cooldown * 0.8f);
        }

        [Test]
        public void VitalRune_Heals_ShadowRune_ReadiesTheDodge_StormRune_Strikes()
        {
            var sim = NewSim(ClassId.Guardian, out var p);
            Wear(p, Make("rune_2", ClassId.Guardian, RuneId.Vital));
            p.Hp = p.MaxHp * 0.5f;
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Skill2Count = 1 });
            Run(sim, Dt);
            Assert.Greater(p.Hp, p.MaxHp * 0.55f);

            sim = NewSim(ClassId.Assassin, out p);
            Wear(p, Make("rune_2", ClassId.Assassin, RuneId.Shadow));
            p.DodgeCooldown = 5f;
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right, Skill2Count = 1 });
            Run(sim, Dt);
            Assert.LessOrEqual(p.DodgeCooldown, 0f);
            Assert.Greater(p.SureCritTimer, 0f);

            sim = NewSim(ClassId.Paladin, out p);
            Wear(p, Make("rune_1", ClassId.Paladin, RuneId.Storm));
            var a = Spawn(sim, EnemyType.Skeleton, new Vector2(4f, 1f));
            var b = Spawn(sim, EnemyType.Skeleton, new Vector2(4f, -1f));
            float ha = sim.Enemies[a].Health, hb = sim.Enemies[b].Health;
            sim.SetPlayerInput(0, new PlayerInput { Aim = new Vector2(4f, 0f), Skill1Count = 1 });
            var events = Run(sim, Dt);
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.Lightning));
            Assert.Less(sim.Enemies[a].Health, ha);
            Assert.Less(sim.Enemies[b].Health, hb);
        }

        [Test]
        public void Runes_AreNeverLent()
        {
            var sim = new RunSimulation(3);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start();
            Assert.IsNotNull(p.Equipped[(int)EquipSlot.Weapon], "the weapon slot gets a loaner");
            Assert.IsNull(p.Equipped[(int)EquipSlot.Rune1]);
            Assert.IsNull(p.Equipped[(int)EquipSlot.Rune2]);
        }
    }
}
