using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Gear: item rolls, the 64-bit wire format, equipping, personal drops and the bag actions.</summary>
    public class GearTests
    {
        private const float Dt = 1f / 30f;

        private static ItemInstance ItemFor(EquipSlot slot, ulong startSeed, ClassId cls = ClassId.Warrior)
        {
            for (ulong seed = startSeed; ; seed++)
            {
                var item = ItemGenerator.Generate(seed, 3, 1f, cls);
                if (item.Slot == slot) return item;
            }
        }

        [Test]
        public void Catalog_HasTwentyAffixes_AndTwoLegendariesPerClass()
        {
            Assert.AreEqual(20, ItemCatalog.Affixes.Length);
            Assert.AreEqual(20, ItemCatalog.Affixes.Select(a => a.Id).Distinct().Count());
            Assert.AreEqual(12, ItemCatalog.Legendaries.Length);
            foreach (ClassId c in System.Enum.GetValues(typeof(ClassId)))
                Assert.AreEqual(2, ItemCatalog.Legendaries.Count(l => l.Class == c), $"{c} has two legendaries");
            foreach (var a in ItemCatalog.Affixes)
                Assert.That(ItemText.StatName(a.Stat), Does.Not.Contain(a.Stat.ToString()), $"{a.Id} needs a Japanese name");
        }

        [Test]
        public void Codec_RoundTripsEveryRoll()
        {
            for (ulong seed = 1; seed < 3000; seed++)
            {
                var item = ItemGenerator.Generate(seed, (int)(seed % 12) + 1, (seed % 4), (ClassId)(seed % 3));
                ulong packed = ItemCodec.Pack(item);
                Assert.AreNotEqual(0ul, packed);
                var back = ItemCodec.Unpack(packed);
                Assert.AreEqual(item.baseId, back.baseId);
                Assert.AreEqual(item.rarity, back.rarity);
                Assert.AreEqual(item.classId, back.classId);
                Assert.AreEqual(item.legendary, back.legendary);
                Assert.AreEqual(item.itemLevel, back.itemLevel);
                Assert.AreEqual(item.affixes.Count, back.affixes.Count);
                for (int i = 0; i < item.affixes.Count; i++)
                {
                    Assert.AreEqual(item.affixes[i].affixId, back.affixes[i].affixId);
                    Assert.AreEqual(item.affixes[i].value, back.affixes[i].value, 1e-5f);
                }
            }
            Assert.IsNull(ItemCodec.Unpack(0));
        }

        [Test]
        public void Codec_HighBits_CarrySixClassesAndMoreLegendaries_OldSavesReadTheSame()
        {
            var item = ItemGenerator.Generate(42, 7, 1f, ClassId.Ranger);
            ulong packed = ItemCodec.Pack(item);
            Assert.AreEqual(0ul, packed >> 43, "existing classes and legendaries leave the new high bits clear, so old saves are unchanged");

            item.classId = (ClassId)5;
            item.legendary = (LegendaryId)12;
            var back = ItemCodec.Unpack(ItemCodec.Pack(item));
            Assert.AreEqual((ClassId)5, back.classId);
            Assert.AreEqual((LegendaryId)12, back.legendary);
            Assert.AreEqual(item.affixes.Count, back.affixes.Count);
        }

        [Test]
        public void Legendary_CarriesAUniqueForTheOwnersClass()
        {
            int found = 0;
            for (ulong seed = 1; seed < 4000 && found < 20; seed++)
            {
                var item = ItemGenerator.Generate(seed, 5, 3f, ClassId.Mage);
                if (item.rarity != Rarity.Legendary) continue;
                found++;
                Assert.AreEqual(ClassId.Mage, ItemCatalog.GetLegendary(item.legendary).Class);
                Assert.That(ItemText.Name(item), Does.StartWith(ItemCatalog.GetLegendary(item.legendary).Name));
            }
            Assert.Greater(found, 0);
        }

        [Test]
        public void Equip_AppliesStats_AndSwapsTheOldPieceBackIntoTheBag()
        {
            var p = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Warrior });
            float hp = p.MaxHp;
            float armor = p.Stats.Get(StatType.Armor);
            var armor1 = ItemFor(EquipSlot.Armor, 1);
            var armor2 = ItemFor(EquipSlot.Armor, 5000);
            p.Bag.Add(armor1);
            p.Bag.Add(armor2);

            Assert.IsTrue(p.Equip(0));
            Assert.AreSame(armor1, p.Equipped[(int)EquipSlot.Armor]);
            Assert.AreEqual(1, p.Bag.Count);
            Assert.IsTrue(p.MaxHp > hp || p.Stats.Get(StatType.Armor) > armor, "the armour's implicit (HP or armour) applies");

            Assert.IsTrue(p.Equip(0)); // armor2 in, armor1 back to the bag
            Assert.AreSame(armor2, p.Equipped[(int)EquipSlot.Armor]);
            Assert.AreSame(armor1, p.Bag[0]);

            Assert.IsTrue(p.Unequip(EquipSlot.Armor));
            Assert.IsNull(p.Equipped[(int)EquipSlot.Armor]);
            Assert.AreEqual(2, p.Bag.Count);
            Assert.AreEqual(hp, p.MaxHp, 0.01f, "taking everything off restores the base stats");
        }

        [Test]
        public void Drops_ArePersonal_AndPickedUpByWalkingOver()
        {
            var sim = new RunSimulation(80);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var b = sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            var item = ItemGenerator.Generate(7, 1, 0f, ClassId.Warrior);
            sim.Loot.Add(new LootDrop { Id = 99, Owner = 0, Item = item, Position = new Vector2(3f, 0f) });

            b.Position = new Vector2(3f, 0f);
            a.Position = new Vector2(-5f, 0f);
            for (int i = 0; i < 5; i++)
            {
                sim.SetPlayerPosition(1, new Vector2(3f, 0f));
                sim.SetPlayerPosition(0, new Vector2(-5f, 0f));
                foreach (var p in sim.Players) p.InvulnerableTimer = 1f;
                sim.Step(Dt);
            }
            Assert.AreEqual(0, b.Bag.Count, "someone else's drop can't be taken");
            Assert.AreEqual(1, sim.Loot.Count);

            sim.SetPlayerPosition(0, new Vector2(3f, 0f));
            sim.Step(Dt);
            Assert.AreEqual(1, a.Bag.Count);
            Assert.AreEqual(0, sim.Loot.Count);
            Assert.That(sim.Events.Any(e => e.Type == SimEventType.GearSlot && e.ClientId == 0 && e.Int == RunSimulation.BagSlotOffset && e.Seed == ItemCodec.Pack(item)));
        }

        [Test]
        public void BagActions_OnlyBetweenFights()
        {
            var sim = new RunSimulation(81);
            var a = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            var b = sim.AddPlayer(1, new Loadout { classId = ClassId.Ranger });
            sim.Start();
            a.Bag.Add(ItemFor(EquipSlot.Weapon, 1));
            a.Bag.Add(ItemFor(EquipSlot.Ring, 1));
            a.Bag.Add(ItemFor(EquipSlot.Amulet, 1));

            Assert.IsFalse(sim.EquipFromBag(0, 0), "not mid-fight");
            sim.Room.Cleared = true;
            Assert.IsTrue(sim.EquipFromBag(0, 1));
            Assert.IsNotNull(a.Equipped[(int)EquipSlot.Ring]);

            Assert.IsTrue(sim.GiveItem(0, 0, 1));
            Assert.AreEqual(1, b.Bag.Count);
            Assert.AreEqual(ClassId.Ranger, b.Bag[0].classId, "a weapon reshapes for its new owner");
            Assert.That(ItemText.Name(b.Bag[0]), Does.Contain("弓"));

            int shards = sim.BonusShards;
            var amulet = a.Bag[0];
            Assert.IsTrue(sim.SalvageItem(0, 0));
            Assert.AreEqual(shards + RunSimulation.SalvageValue(amulet.rarity), sim.BonusShards);
            Assert.AreEqual(0, a.Bag.Count);
        }

        [Test]
        public void RestRoomChest_DropsFineOrBetterGearForEachPlayer()
        {
            var sim = new RunSimulation(88);
            var a = sim.AddPlayer(0);
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Enemies.Spawn(EnemyType.TreasureChest, new Vector2(2f, 2f), 1f, 1f, false);
            sim.DamageEnemy(0, 999f, a, HitFlags.None, Vector2.zero, 0f);
            for (int i = 0; i < 2; i++)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 1f;
                sim.SetPlayerPosition(0, new Vector2(-12f, -6f));
                sim.SetPlayerPosition(1, new Vector2(12f, -6f));
                sim.Step(Dt);
            }
            Assert.AreEqual(2, sim.Loot.Count, "one personal drop per player");
            Assert.That(sim.Loot.Select(l => l.Owner).Distinct().Count(), Is.EqualTo(2));
            Assert.That(sim.Loot.All(l => l.Item.rarity >= Rarity.Magic));
        }

        [Test]
        public void TreasureDoor_GivesEveryoneRareOrBetter()
        {
            var sim = new RunSimulation(82);
            sim.AddPlayer(0);
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            sim.Room.Cleared = true;
            sim.Room.Doors.Clear();
            sim.Room.Doors.Add(new Door(RoomReward.Treasure, false));
            sim.Room.Doors.Add(new Door(RoomReward.Vigor, false));
            for (int i = 0; i < 120 && sim.Room.Index == 0; i++)
            {
                foreach (var p in sim.Players)
                {
                    p.InvulnerableTimer = 1f;
                    sim.SetPlayerPosition(p.ClientId, RunSimulation.DoorPosition(0, 2, sim.Bounds));
                    if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
                }
                sim.Step(Dt);
            }
            Assert.AreEqual(1, sim.Room.Index);
            foreach (var p in sim.Players)
            {
                Assert.AreEqual(1, p.Bag.Count);
                Assert.GreaterOrEqual(p.Bag[0].rarity, Rarity.Rare);
            }
        }

        // ------------------------------------------------------------------ Keeping gear between runs

        [Test]
        public void Vault_BanksTheRun_LoanersStayBehind_OverflowBecomesShards()
        {
            var data = GearVault.Normalize(new GearVaultData());
            var worn = new ItemInstance[SimPlayer.SlotCount];
            worn[(int)EquipSlot.Ring] = ItemFor(EquipSlot.Ring, 1);
            var loaner = ItemFor(EquipSlot.Armor, 1);
            loaner.loaner = true;
            worn[(int)EquipSlot.Armor] = loaner;
            var bag = new List<ItemInstance> { ItemFor(EquipSlot.Weapon, 1), ItemFor(EquipSlot.Amulet, 1) };

            var result = GearVault.BankRun(data, worn, bag);
            Assert.AreEqual(3, result.Kept);
            Assert.IsNotNull(GearVault.Equipped(data, EquipSlot.Ring));
            Assert.IsNull(GearVault.Equipped(data, EquipSlot.Armor), "the loaner was handed back");
            Assert.AreEqual(2, data.items.Count);

            data.capacity = GearVault.BaseCapacity;
            var many = Enumerable.Range(0, 35).Select(i => ItemFor(EquipSlot.Ring, (ulong)(100 + i * 50))).ToList();
            var overflow = GearVault.BankRun(data, worn, many);
            Assert.AreEqual(GearVault.BaseCapacity, data.items.Count);
            Assert.AreEqual(7, overflow.OverflowCount);
            Assert.Greater(overflow.OverflowShards, 0);
        }

        [Test]
        public void Vault_EquipSwapsWithTheStash_AndSurvivesASaveRoundTrip()
        {
            var data = GearVault.Normalize(new GearVaultData());
            data.items.Add(GearVault.Pack(ItemFor(EquipSlot.Armor, 1)));
            data.items.Add(GearVault.Pack(ItemFor(EquipSlot.Armor, 900)));
            Assert.IsTrue(GearVault.Equip(data, 0));
            Assert.IsTrue(GearVault.Equip(data, 0)); // the second armour in, the first back to the stash
            Assert.AreEqual(1, data.items.Count);
            Assert.AreEqual(1, GearVault.LoadoutGear(data).Count);

            var profile = new ProfileData { gear = data };
            var back = ProfileData.FromJson(profile.ToJson());
            back.gear = GearVault.Normalize(back.gear);
            Assert.AreEqual(data.equipped[(int)EquipSlot.Armor], back.gear.equipped[(int)EquipSlot.Armor]);
            Assert.AreEqual(1, back.ToLoadout().gear.Count);
        }

        [Test]
        public void BroughtGear_IsWornAtTheStart_HeldBackEarly_EmptySlotsGetLoaners()
        {
            var strong = ItemFor(EquipSlot.Ring, 1);
            strong.itemLevel = 12;
            strong.affixes.Clear();
            var sim = new RunSimulation(87);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage, gear = new List<string> { GearVault.Pack(strong) } });
            sim.Start();

            Assert.AreEqual(12, p.Equipped[(int)EquipSlot.Ring].itemLevel);
            Assert.IsFalse(p.Equipped[(int)EquipSlot.Ring].loaner);
            for (int s = 0; s < SimPlayer.SlotCount; s++)
                if (s != (int)EquipSlot.Ring) Assert.IsTrue(p.Equipped[s].loaner, $"slot {s} gets a loaner");
            Assert.AreEqual(1 + RunSimulation.GearLevelGrace, p.GearLevelCap);

            float early = strong.GetModifiers(p.GearLevelCap).First().Value;
            float full = strong.GetModifiers().First().Value;
            Assert.Less(early, full, "held back in the first room");

            // Replacing a loaner hands it back instead of filling the bag.
            p.Bag.Add(ItemFor(EquipSlot.Weapon, 1, ClassId.Mage));
            sim.Room.Cleared = true;
            Assert.IsTrue(sim.EquipFromBag(0, 0));
            Assert.AreEqual(0, p.Bag.Count);
        }

        private static void WearLegendary(SimPlayer p, LegendaryId id)
        {
            var item = ItemFor(EquipSlot.Ring, 1, p.ClassId);
            item.rarity = Rarity.Legendary;
            item.legendary = id;
            p.Bag.Add(item);
            p.Equip(p.Bag.Count - 1);
            Assert.IsTrue(p.HasLegendary(id));
            Assert.IsTrue(p.AnyLegendaryEquipped);
        }

        private static void PressSkill(RunSimulation sim, SimPlayer p, bool second)
        {
            var input = p.Input;
            input.Aim = new Vector2(5f, 0f);
            if (second) input.Skill2Count++;
            else input.Skill1Count++;
            sim.SetPlayerInput(p.ClientId, input);
            sim.Step(Dt);
        }

        [Test]
        public void ShadowQuiver_FiresAFanOfThree()
        {
            int Arrows(bool legendary)
            {
                var sim = new RunSimulation(84);
                var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Ranger });
                sim.Room.Cleared = true; // no waves
                if (legendary) WearLegendary(p, LegendaryId.ShadowQuiver);
                PressSkill(sim, p, false);
                return sim.Projectiles.Count(pr => pr.Kind == ProjectileKind.PierceArrow);
            }
            Assert.AreEqual(1, Arrows(false));
            Assert.AreEqual(3, Arrows(true));
        }

        [Test]
        public void FrostCrown_FreezesAroundTheCaster()
        {
            var sim = new RunSimulation(85);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            sim.Room.Cleared = true;
            WearLegendary(p, LegendaryId.FrostCrown);
            sim.Enemies.Spawn(EnemyType.Skeleton, p.Position + new Vector2(0.8f, 0f), 50f, 1f, false);
            PressSkill(sim, p, false);
            for (int i = 0; i < 40; i++) sim.Step(Dt);
            Assert.Greater(sim.Enemies[0].Frozen, 0f, "the enemy next to the mage is frozen though the aim was far away");
        }

        [Test]
        public void HuntGodMark_MarksASecondMonster()
        {
            int Marked(bool legendary)
            {
                var sim = new RunSimulation(86);
                var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Ranger });
                sim.Room.Cleared = true;
                if (legendary) WearLegendary(p, LegendaryId.HuntGodMark);
                sim.Enemies.Spawn(EnemyType.Skeleton, p.Position + new Vector2(5f, 0f), 50f, 1f, false); // under the aimed circle
                sim.Enemies.Spawn(EnemyType.Skeleton, p.Position + new Vector2(5f, 5f), 50f, 1f, false); // outside it, close by
                PressSkill(sim, p, true);
                int marked = 0;
                for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Marked > 0f) marked++;
                return marked;
            }
            Assert.AreEqual(1, Marked(false));
            Assert.AreEqual(2, Marked(true));
        }

        [Test]
        public void Mist_LengthensSureCrits_BlackBlade_CutsTwice()
        {
            float SureCrit(bool legendary)
            {
                var sim = new RunSimulation(89);
                var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Assassin });
                sim.Room.Cleared = true;
                if (legendary) WearLegendary(p, LegendaryId.Mist);
                PressSkill(sim, p, true);
                return p.SureCritTimer;
            }
            Assert.AreEqual(SureCrit(false) + 2f, SureCrit(true), 0.05f);

            int Cuts(bool legendary)
            {
                var sim = new RunSimulation(90);
                var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Assassin });
                sim.Room.Cleared = true;
                if (legendary) WearLegendary(p, LegendaryId.BlackBlade);
                PressSkill(sim, p, false);
                int cuts = 0;
                for (int i = 0; i < 15; i++)
                {
                    sim.Step(Dt);
                    cuts += sim.Events.Count(e => e.Type == SimEventType.AreaEffect && e.Int == (int)AreaKind.ShadowStep);
                    sim.Events.Clear();
                }
                return cuts;
            }
            Assert.AreEqual(1, Cuts(false));
            Assert.AreEqual(2, Cuts(true));
        }

        [Test]
        public void FrozenDamageGear_HitsFrozenEnemiesHarder()
        {
            float Hit(float frozenBonus)
            {
                var sim = new RunSimulation(83);
                var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
                if (frozenBonus > 0f) p.Stats.AddModifier(new StatModifier(StatType.FrozenDamage, ModifierKind.Flat, frozenBonus), new object());
                p.Stats.SetBase(StatType.CritChance, 0f);
                sim.Enemies.Spawn(EnemyType.Skeleton, new Vector2(3f, 0f), 100f, 1f, false);
                sim.ApplyStatus(0, StatusKind.Frozen, 5f);
                float before = sim.Enemies[0].Health;
                sim.DamageEnemy(0, 10f, p, HitFlags.None, Vector2.zero, 0f);
                return before - sim.Enemies[0].Health;
            }
            Assert.AreEqual(Hit(0f) * 1.5f, Hit(0.5f), 0.01f);
        }
    }
}
