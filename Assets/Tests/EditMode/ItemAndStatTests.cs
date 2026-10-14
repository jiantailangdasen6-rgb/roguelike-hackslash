using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class StatSheetTests
    {
        [Test]
        public void CombinesFlatPercentAndMultiplier()
        {
            var sheet = new StatSheet();
            sheet.SetBase(StatType.MaxHealth, 100f);
            var source = new object();
            sheet.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.Flat, 20f), source);
            sheet.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, 0.5f), source);
            sheet.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentMult, 0.1f), source);

            Assert.AreEqual((100f + 20f) * 1.5f * 1.1f, sheet.Get(StatType.MaxHealth), 0.001f);
        }

        [Test]
        public void RemovingSourceRestoresBase()
        {
            var sheet = new StatSheet();
            sheet.SetBase(StatType.Might, 1f);
            var ring = new object();
            sheet.AddModifier(new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.2f), ring);
            Assert.AreEqual(1.2f, sheet.Get(StatType.Might), 0.001f);

            sheet.RemoveModifiersFrom(ring);
            Assert.AreEqual(1f, sheet.Get(StatType.Might), 0.001f);
        }
    }

    public class ItemTests
    {
        [Test]
        public void SameSeed_GivesSameItem()
        {
            var a = ItemGenerator.Generate(123, 5, 1f);
            var b = ItemGenerator.Generate(123, 5, 1f);
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(a), UnityEngine.JsonUtility.ToJson(b));
        }

        [Test]
        public void AffixCount_MatchesRarity_AndAffixesAreUnique()
        {
            for (ulong seed = 1; seed < 500; seed++)
            {
                var item = ItemGenerator.Generate(seed, 3, 1f);
                if (item.rune != RuneId.None) continue; // runes carry their effect instead (WeaponRuneTests)
                int count = item.affixes.Count;
                switch (item.rarity)
                {
                    case Rarity.Common: Assert.AreEqual(0, count); break;
                    case Rarity.Magic: Assert.AreEqual(1, count); break;
                    default: Assert.AreEqual(2, count); break;
                }
                Assert.AreEqual(count, item.affixes.Select(a => a.affixId).Distinct().Count());
            }
        }

        [Test]
        public void RarityBoost_RaisesRareRate()
        {
            int RareCount(float boost) => Enumerable.Range(1, 2000)
                .Count(s => ItemGenerator.Generate((ulong)s, 1, boost).rarity >= Rarity.Rare);

            Assert.Greater(RareCount(3f), RareCount(0f) * 2);
        }

        [Test]
        public void HigherItemLevel_GivesStrongerImplicit()
        {
            var low = ItemGenerator.Generate(42, 1);
            var high = ItemGenerator.Generate(42, 20);
            Assert.Greater(high.GetModifiers().First().Value, low.GetModifiers().First().Value);
        }
    }

    public class StashTests
    {
        private static ItemInstance ItemFor(EquipSlot slot, ulong startSeed)
        {
            for (ulong seed = startSeed; ; seed++)
            {
                var item = ItemGenerator.Generate(seed, 1);
                if (item.Slot == slot) return item;
            }
        }

        [Test]
        public void FirstItemInSlot_IsAutoEquipped_SecondIsNot()
        {
            var stash = new Stash();
            var first = ItemFor(EquipSlot.Ring, 1);
            var second = ItemFor(EquipSlot.Ring, 1000);

            stash.Add(first);
            stash.Add(second);

            Assert.IsTrue(stash.IsEquipped(first.uid));
            Assert.IsFalse(stash.IsEquipped(second.uid));
        }

        [Test]
        public void Equip_ReplacesItemInSameSlot()
        {
            var stash = new Stash();
            var first = ItemFor(EquipSlot.Weapon, 1);
            var second = ItemFor(EquipSlot.Weapon, 1000);
            stash.Add(first);
            stash.Add(second);

            stash.Equip(second.uid);

            Assert.IsFalse(stash.IsEquipped(first.uid));
            Assert.AreSame(second, stash.GetEquipped(EquipSlot.Weapon));
        }

        [Test]
        public void Json_RoundTrips()
        {
            var stash = new Stash();
            stash.Add(ItemFor(EquipSlot.Armor, 1));
            stash.Add(ItemFor(EquipSlot.Amulet, 1));

            var restored = Stash.FromJson(stash.ToJson());

            Assert.AreEqual(2, restored.Items.Count);
            Assert.AreEqual(stash.GetEquippedModifiers().Count, restored.GetEquippedModifiers().Count);
        }

        [Test]
        public void FromJson_ToleratesGarbage()
        {
            Assert.AreEqual(0, Stash.FromJson("").Items.Count);
            Assert.AreEqual(0, Stash.FromJson("{\"items\":[{\"uid\":\"x\",\"baseId\":\"nope\"}]}").Items.Count);
        }
    }
}
