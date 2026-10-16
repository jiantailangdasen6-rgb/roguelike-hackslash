using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Gear sets and the blacksmith (forging, rerolling).</summary>
    public class SetForgeTests
    {
        private static ItemInstance Piece(string baseId, int set, Rarity rarity = Rarity.Rare)
        {
            var item = new ItemInstance { uid = baseId + set, baseId = baseId, rarity = rarity, itemLevel = 5, classId = ClassId.Warrior, setId = set };
            item.AddAffix(ItemCatalog.GetAffix("maxhp"), 60);
            item.AddAffix(ItemCatalog.GetAffix("might"), 90);
            return item;
        }

        [Test]
        public void SetAndForge_SurviveTheWire_AndOldItemsReadNone()
        {
            var item = Piece("chainmail", 4);
            item.forge = 3;
            var back = ItemCodec.Unpack(ItemCodec.Pack(item));
            Assert.AreEqual(4, back.setId);
            Assert.AreEqual(3, back.forge);
            var plain = Piece("copper_ring", 0);
            back = ItemCodec.Unpack(ItemCodec.Pack(plain));
            Assert.AreEqual(0, back.setId);
            Assert.AreEqual(0, back.forge);
        }

        [Test]
        public void TwoPieces_GiveTheFirstBonus_ThreeTheSecond()
        {
            var p = new SimPlayer(0, UnityEngine.Vector2.zero, new Loadout { classId = ClassId.Ranger });
            float crit = p.Stats.Get(StatType.CritChance);
            float marked = p.Stats.Get(StatType.MarkedDamage);
            p.Bag.Add(Piece("chainmail", 1));
            p.Equip(0);
            Assert.AreEqual(crit, p.Stats.Get(StatType.CritChance), 0.001f, "one piece: nothing yet");
            p.Bag.Add(Piece("moon_charm", 1));
            p.Equip(0);
            Assert.AreEqual(crit + 0.08f, p.Stats.Get(StatType.CritChance), 0.001f, "two pieces");
            Assert.AreEqual(marked, p.Stats.Get(StatType.MarkedDamage), 0.001f);
            p.Bag.Add(Piece("silver_ring", 1));
            p.Equip(0);
            Assert.AreEqual(marked + 0.35f, p.Stats.Get(StatType.MarkedDamage), 0.001f, "three pieces");
            p.Bag.Add(Piece("leather_armor", 2));
            p.Equip(0);
            Assert.AreEqual(crit + 0.08f, p.Stats.Get(StatType.CritChance), 0.001f, "back to two");
            Assert.AreEqual(marked, p.Stats.Get(StatType.MarkedDamage), 0.001f);
        }

        [Test]
        public void Forging_Strengthens_UpToFive()
        {
            var item = Piece("chainmail", 0);
            float before = item.GetModifiers().Sum(m => m.Value);
            var forged = Blacksmith.Forged(item);
            Assert.AreEqual(1, forged.forge);
            Assert.AreEqual(before * 1.08f, forged.GetModifiers().Sum(m => m.Value), 0.01f);
            for (int i = 0; i < 10; i++) forged = Blacksmith.CanForge(forged) ? Blacksmith.Forged(forged) : forged;
            Assert.AreEqual(Blacksmith.MaxForge, forged.forge);
            Assert.Greater(Blacksmith.ForgeCost(Piece("chainmail", 0, Rarity.Legendary)), Blacksmith.ForgeCost(Piece("chainmail", 0, Rarity.Common)));
            Assert.IsFalse(Blacksmith.CanForge(new ItemInstance { baseId = "chainmail", rarity = Rarity.Rare, itemLevel = 1, loaner = true }), "loaners are not ours to forge");
        }

        [Test]
        public void Reroll_ChangesOneProperty_WithoutDoubles()
        {
            var item = Piece("chainmail", 0); // implicit: armour (its properties are max HP and might)
            for (ulong seed = 1; seed < 40; seed++)
            {
                var re = Blacksmith.Rerolled(item, seed);
                Assert.AreEqual(item.affixes.Count, re.affixes.Count);
                Assert.AreEqual(re.affixes.Count, re.affixes.Select(a => a.affixId).Distinct().Count(), "no property twice");
                int same = re.affixes.Count(a => item.affixes.Any(b => b.affixId == a.affixId && b.roll == a.roll));
                Assert.GreaterOrEqual(same, item.affixes.Count - 1, "only one changes");
                Assert.IsFalse(re.affixes.Any(a => ItemCatalog.GetAffix(a.affixId).Stat == item.Base.Implicit.Stat), "not the implicit stat again");
            }
            Assert.IsFalse(Blacksmith.CanReroll(Piece("chainmail", 0, Rarity.Common).WithoutAffixes()), "nothing to reroll");
        }

        [Test]
        public void SetPieces_DropAsRareArmourAmuletsAndRings()
        {
            int sets = 0;
            for (ulong seed = 1; seed < 3000; seed++)
            {
                var item = ItemGenerator.Generate(seed, 10, 3f);
                if (item.setId == 0) continue;
                sets++;
                Assert.AreEqual(Rarity.Rare, item.rarity);
                Assert.IsTrue(GearSetCatalog.CanBeSetPiece(item.Slot), item.Slot.ToString());
                Assert.IsNotNull(GearSetCatalog.Get(item.setId));
                StringAssert.StartsWith(GearSetCatalog.Get(item.setId).Prefix, ItemText.Name(item));
            }
            Assert.Greater(sets, 20);
        }
    }

    internal static class ItemTestExtensions
    {
        public static ItemInstance WithoutAffixes(this ItemInstance item)
        {
            item.affixes.Clear();
            return item;
        }
    }
}
