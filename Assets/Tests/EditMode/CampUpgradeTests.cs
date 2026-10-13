using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Growing the camp: facilities bought with soul shards that help every run after.</summary>
    public class CampUpgradeTests
    {
        [Test]
        public void Facilities_CostShards_OneLevelAtATime_UpToTheirMax()
        {
            var p = new ProfileData { shards = 1000 };
            Assert.IsTrue(p.TryUpgradeCamp(CampUpgrade.Hearth));
            Assert.AreEqual(1000 - CampUpgrades.Get(CampUpgrade.Hearth).Costs[0], p.shards);
            Assert.AreEqual(1, p.CampLevel(CampUpgrade.Hearth));
            Assert.IsTrue(p.TryUpgradeCamp(CampUpgrade.Altar));
            Assert.IsFalse(p.TryUpgradeCamp(CampUpgrade.Altar), "the altar has one level");
            p.shards = 0;
            Assert.IsFalse(p.TryUpgradeCamp(CampUpgrade.Hearth), "not enough shards");
        }

        [Test]
        public void TheLevels_TravelInTheLoadout_AndWorkInTheRun()
        {
            var profile = new ProfileData();
            profile.camp = new List<int> { 2, 2, 1, 1, 0 };
            var loadout = Loadout.FromJson(profile.ToLoadout().ToJson());
            Assert.AreEqual(2, CampUpgrades.Level(loadout.camp, CampUpgrade.Hearth));

            var plain = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Warrior });
            var grown = new SimPlayer(1, Vector2.zero, loadout);
            Assert.AreEqual(plain.MaxHp * (1f + 2 * CampUpgrades.HearthHealth), grown.MaxHp, 0.5f, "the hearth");
            Assert.IsTrue(grown.HasStartPerk, "the altar");
            Assert.IsFalse(plain.HasStartPerk);

            // The forge lends rare pieces in the empty slots.
            var sim = new RunSimulation(3);
            var a = sim.AddPlayer(0, loadout);
            sim.Start();
            foreach (var item in a.Equipped)
                if (item != null && item.loaner) Assert.AreEqual(Rarity.Rare, item.rarity);
        }
    }
}
