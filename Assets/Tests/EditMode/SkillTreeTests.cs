using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The master's skill tree: three branches per class, steps learnt from the top, one secret art, full refunds.</summary>
    public class SkillTreeTests
    {
        private const float Dt = 1f / 30f;

        [Test]
        public void EveryClass_HasThreeBranchesOfFourSteps_EndingInASecretArt()
        {
            Assert.AreEqual(MetaCatalog.Mastery.Length, MetaCatalog.Mastery.Select(n => n.Id).Distinct().Count(), "ids are unique");
            foreach (ClassId cls in System.Enum.GetValues(typeof(ClassId)))
                for (int b = 0; b < MetaCatalog.BranchCount; b++)
                {
                    var branch = MetaCatalog.Branch(cls, b);
                    CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, branch.Select(n => n.Tier), $"{cls} branch {b}");
                    Assert.IsNotEmpty(MetaCatalog.BranchName(cls, b));
                    var grant = branch[2];
                    Assert.AreEqual(MasteryKind.GrantPerk, grant.Kind);
                    Assert.AreEqual(cls, PerkCatalog.Get(grant.Perk).Class, $"{grant.Id} grants another class's perk");
                    Assert.IsFalse(PerkCatalog.Get(grant.Perk).Locked, $"{grant.Id}");
                    var art = branch[3];
                    Assert.AreEqual(MasteryKind.Keystone, art.Kind);
                    Assert.That(art.Skill, Is.InRange(1, 2));
                    Assert.AreNotEqual(RuneId.None, art.Rune);
                    Assert.IsNull(MetaCatalog.Requires(branch[0]));
                    Assert.AreSame(branch[2], MetaCatalog.Requires(art));
                }
        }

        [Test]
        public void Steps_OpenFromTheTop_OneSecretArt_AndRespecGivesEverythingBack()
        {
            var profile = ProfileData.Default();
            profile.shards = 2000;
            var sword = MetaCatalog.Branch(ClassId.Warrior, 0);
            var iron = MetaCatalog.Branch(ClassId.Warrior, 1);

            Assert.IsFalse(profile.TryLearn(sword[1], out string why), "the step above comes first");
            StringAssert.Contains(sword[0].Name, why);
            foreach (var n in sword) Assert.IsTrue(profile.TryLearn(n, out _), n.Id);
            for (int t = 0; t < 3; t++) Assert.IsTrue(profile.TryLearn(iron[t], out _), iron[t].Id);
            Assert.IsFalse(profile.TryLearn(iron[3], out why), "only one secret art");
            StringAssert.Contains("奥義", why);

            int spent = sword.Sum(n => n.Cost) + iron.Take(3).Sum(n => n.Cost);
            Assert.AreEqual(spent, profile.MasterySpent(ClassId.Warrior));
            Assert.AreEqual(2000 - spent, profile.shards);
            Assert.AreEqual(spent, profile.Respec(ClassId.Warrior));
            Assert.AreEqual(2000, profile.shards);
            Assert.AreEqual(0, profile.MasterySpent(ClassId.Warrior));
            Assert.IsTrue(profile.TryLearn(iron[0], out _));

            var poor = ProfileData.Default();
            Assert.IsFalse(poor.TryLearn(sword[0], out why));
            StringAssert.Contains("足りない", why);
        }

        [Test]
        public void Loadout_KeepsOneSecretArtPerClass()
        {
            var json = new Loadout { classId = ClassId.Warrior, mastery = new List<string> { "w_k_ember", "w_k_storm", "w_hp" } }.ToJson();
            CollectionAssert.AreEqual(new[] { "w_k_ember", "w_hp" }, Loadout.FromJson(json).mastery);
        }

        [Test]
        public void ThirdStep_StartsTheRunWithItsPerk()
        {
            var p = new SimPlayer(0, Vector2.zero, new Loadout { classId = ClassId.Mage, mastery = new List<string> { "m_g_multi", "w_g_whirl" } });
            Assert.AreEqual(1, p.PerkLevel(PerkId.Multicast));
            Assert.AreEqual(0, p.PerkLevel(PerkId.Whirlwind), "another class's steps do nothing");
        }

        [Test]
        public void SecretArt_ChangesTheSkill_LikeARuneThatIsAlwaysThere()
        {
            var sim = new RunSimulation(7);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior, mastery = new List<string> { "w_k_ember" } });
            p.Position = Vector2.zero;
            Assert.AreEqual(RuneId.Ember, p.InnateRune);
            Assert.AreEqual(1, p.InnateSkill);
            sim.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right * 3f, Skill1Count = 1 });
            var events = new List<SimEvent>();
            for (float t = 0; t < 0.2f; t += Dt)
            {
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            Assert.AreEqual(3, events.Count(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.FirePool), "the dash leaves fire along its way");

            var plain = new RunSimulation(7);
            plain.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            plain.SetPlayerInput(0, new PlayerInput { Aim = Vector2.right * 3f, Skill1Count = 1 });
            var none = new List<SimEvent>();
            for (float t = 0; t < 0.2f; t += Dt)
            {
                plain.Step(Dt);
                none.AddRange(plain.Events);
                plain.Events.Clear();
            }
            Assert.IsFalse(none.Any(e => e.Type == SimEventType.AreaEffect && (AreaKind)e.Int == AreaKind.FirePool));
        }
    }
}
