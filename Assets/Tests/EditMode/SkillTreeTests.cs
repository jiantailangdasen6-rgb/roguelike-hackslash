using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>The master's sphere board: round boards per class, spheres opened next to open ones, seals and keys, arts, refunds.</summary>
    public class SkillTreeTests
    {
        private const float Dt = 1f / 30f;

        /// <summary>Opens spheres one after another along the shortest way to <paramref name="target"/>.</summary>
        private static void WalkTo(ProfileData p, MasteryNode target)
        {
            var prev = new Dictionary<string, string>();
            var queue = new Queue<string>();
            queue.Enqueue(SphereBoard.Core);
            prev[SphereBoard.Core] = null;
            var nodes = MetaCatalog.NodesFor(target.Class).ToList();
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (var n in nodes)
                    if (!prev.ContainsKey(n.Id) && n.Links.Contains(at)) { prev[n.Id] = at; queue.Enqueue(n.Id); }
            }
            var path = new List<string>();
            for (string id = target.Id; id != SphereBoard.Core; id = prev[id]) path.Add(id);
            path.Reverse();
            foreach (var id in path)
                if (!p.unlocked.Contains(id)) Assert.IsTrue(p.TryLearn(MetaCatalog.GetNode(id), out string why), $"{id}: {why}");
        }

        [Test]
        public void EveryClass_HasARoundBoard_ThreeRoadsTwoRings_ArtsAtTheEnds()
        {
            Assert.AreEqual(MetaCatalog.Mastery.Length, MetaCatalog.Mastery.Select(n => n.Id).Distinct().Count(), "ids are unique");
            foreach (ClassId cls in System.Enum.GetValues(typeof(ClassId)))
            {
                var nodes = MetaCatalog.NodesFor(cls).ToList();
                Assert.AreEqual(45, nodes.Count, $"{cls}");
                Assert.AreEqual(3, nodes.Count(n => n.Kind == MasteryKind.Keystone));
                Assert.AreEqual(3, nodes.Count(n => n.Kind == MasteryKind.Gate && n.Keys == 1));
                Assert.AreEqual(3, nodes.Count(n => n.Links.Contains(SphereBoard.Core)), "one first sphere per road");
                foreach (var n in nodes)
                    foreach (var id in n.Links.Where(id => id != SphereBoard.Core))
                    {
                        var other = MetaCatalog.GetNode(id);
                        Assert.IsNotNull(other, id);
                        Assert.AreEqual(cls, other.Class);
                        Assert.Contains(n.Id, other.Links, "links go both ways");
                    }
                foreach (var art in nodes.Where(n => n.Kind == MasteryKind.Keystone))
                {
                    Assert.AreEqual(SphereBoard.RoadLength, art.Pos.magnitude, 0.01f, "arts sit at the end of a road");
                    Assert.That(art.Skill, Is.InRange(1, 2));
                    Assert.AreNotEqual(RuneId.None, art.Rune);
                }
                foreach (var grant in nodes.Where(n => n.Kind == MasteryKind.GrantPerk))
                {
                    Assert.AreEqual(cls, PerkCatalog.Get(grant.Perk).Class, $"{grant.Id} grants another class's perk");
                    Assert.IsFalse(PerkCatalog.Get(grant.Perk).Locked, grant.Id);
                }
                for (int b = 0; b < MetaCatalog.BranchCount; b++) Assert.IsNotEmpty(MetaCatalog.BranchName(cls, b));
            }
        }

        [Test]
        public void Spheres_OpenNextToOpenOnes_SealsTakeAKey_AndRespecGivesEverythingBack()
        {
            var p = new ProfileData { spheres = 500 };
            var nodes = MetaCatalog.NodesFor(ClassId.Warrior).ToList();
            var first = nodes.First(n => n.Links.Contains(SphereBoard.Core));
            var far = nodes.First(n => n.Kind == MasteryKind.GrantPerk);
            Assert.IsFalse(p.TryLearn(far, out string why), "not next to anything open");
            StringAssert.Contains("隣", why);
            Assert.IsTrue(p.TryLearn(first, out _));
            Assert.AreEqual(500 - first.Cost, p.spheres);

            var gate = nodes.First(n => n.Kind == MasteryKind.Gate && n.Branch == first.Branch);
            var before = nodes.First(n => n.Links.Contains(gate.Id) && n.Pos.magnitude < gate.Pos.magnitude);
            WalkTo(p, before);
            Assert.IsFalse(p.TryLearn(gate, out why), "a seal takes a key");
            StringAssert.Contains("鍵", why);
            p.keys = 1;
            Assert.IsTrue(p.TryLearn(gate, out _));
            Assert.AreEqual(0, p.keys);

            int spent = p.SpheresSpent(ClassId.Warrior);
            Assert.AreEqual(500, p.spheres + spent);
            Assert.AreEqual(spent, p.Respec(ClassId.Warrior));
            Assert.AreEqual(500, p.spheres);
            Assert.AreEqual(1, p.keys, "the key comes back too");
            Assert.AreEqual(0, p.SpheresSpent(ClassId.Warrior));

            var poor = new ProfileData();
            Assert.IsFalse(poor.TryLearn(first, out why));
            StringAssert.Contains("足りない", why);
        }

        [Test]
        public void Arts_AllCanBeLearnt_OneIsWorn_AndOnlyThatOneGoesIntoTheRun()
        {
            var p = new ProfileData { spheres = 1000, keys = 3, selectedClass = ClassId.Warrior };
            var arts = MetaCatalog.NodesFor(ClassId.Warrior).Where(n => n.Kind == MasteryKind.Keystone).ToList();
            WalkTo(p, arts[0]);
            WalkTo(p, arts[1]);
            Assert.IsTrue(p.unlocked.Contains(arts[0].Id) && p.unlocked.Contains(arts[1].Id));
            Assert.AreSame(arts[0], p.WornArt(ClassId.Warrior), "the first art is worn at once");
            var loadout = p.ToLoadout();
            Assert.Contains(arts[0].Id, loadout.mastery);
            Assert.IsFalse(loadout.mastery.Contains(arts[1].Id));
            p.WearArt(arts[1]);
            Assert.AreSame(arts[1], p.WornArt(ClassId.Warrior));
            var player = new SimPlayer(0, Vector2.zero, p.ToLoadout());
            Assert.AreEqual(arts[1].Rune, player.InnateRune);
            Assert.AreEqual(arts[1].Skill, player.InnateSkill);
        }

        [Test]
        public void AnOldProfile_KeepsItsSpheres_WearsItsArt_AndGetsAFirstHandful()
        {
            string json = "{\"version\":1,\"shards\":5,\"unlocked\":[\"w_might\",\"w_k_storm\"]}";
            var p = ProfileData.FromJson(json);
            Assert.AreEqual(2, p.version);
            Assert.AreEqual(10, p.spheres);
            Assert.IsTrue(p.unlocked.Contains("w_might"));
            Assert.AreEqual("w_k_storm", p.WornArt(ClassId.Warrior)?.Id);
            Assert.AreEqual(10, ProfileData.FromJson(p.ToJson()).spheres, "only once");
            Assert.AreEqual(10, ProfileData.Default().spheres);
        }

        [Test]
        public void Runs_GiveSpheres_AndAKeyForEachBoss()
        {
            Assert.AreEqual(2, SphereBoard.SpheresForRun(0, 0, false));
            Assert.Greater(SphereBoard.SpheresForRun(8, 2, false), SphereBoard.SpheresForRun(8, 0, false));
            Assert.Greater(SphereBoard.SpheresForRun(12, 3, true), SphereBoard.SpheresForRun(12, 3, false));
            Assert.AreEqual(2, SphereBoard.KeysForRun(2));
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
