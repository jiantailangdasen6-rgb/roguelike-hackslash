using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Achievements and titles; the camp residents' stories.</summary>
    public class AchievementStoryTests
    {
        [Test]
        public void Achievements_CountDeeds_AndGrantTitles()
        {
            var log = new AchievementLog();
            var lich = AchievementCatalog.Get("a_lich");
            var died = new SimEvent(SimEventType.EnemyDied) { Int = (int)EnemyType.Lich };
            Assert.IsTrue(log.Add(lich, QuestCatalog.Amount(lich, died)));
            Assert.IsTrue(log.IsDone("a_lich"));
            Assert.Contains("リッチスレイヤー", log.Titles().ToList());

            var favor = AchievementCatalog.Get("a_favor");
            Assert.AreEqual(1, QuestCatalog.Amount(favor, new SimEvent(SimEventType.GodSign) { Int = (int)GodSignKind.Favor }));
            Assert.AreEqual(0, QuestCatalog.Amount(favor, new SimEvent(SimEventType.GodSign) { Int = (int)GodSignKind.Taken }));

            var kills = AchievementCatalog.Get("a_kill1");
            Assert.IsFalse(log.Add(kills, 49));
            Assert.IsTrue(log.Add(kills, 1));
            Assert.AreEqual(kills.Target, log.Progress(kills));
        }

        [Test]
        public void OnlyAnEarnedTitle_GoesIntoTheLoadout()
        {
            var p = new ProfileData { title = "リッチスレイヤー" };
            Assert.AreEqual("", p.ToLoadout().title, "not earned yet");
            p.achievements.Add(AchievementCatalog.Get("a_lich"), 1);
            Assert.AreEqual("リッチスレイヤー", Loadout.FromJson(p.ToLoadout().ToJson()).title);
        }

        [Test]
        public void ATitleSavedUnderItsOldName_ComesBackRenamed()
        {
            var json = new ProfileData { title = "冥王殺し" }.ToJson();
            Assert.AreEqual("リッチスレイヤー", ProfileData.FromJson(json).title);
            Assert.AreEqual("トーチベアラー", AchievementCatalog.RenamedTitle("灯火を継ぐ者"));
            Assert.AreEqual("なにか", AchievementCatalog.RenamedTitle("なにか"));
        }

        [Test]
        public void Residents_TellMoreAsTheProfileGrows_AndEachLineOnce()
        {
            var p = new ProfileData();
            Assert.AreEqual(1, CampStories.Unread(p, QuestGiver.Master).Count, "a greeting");
            p.storyRead.Add("m0");
            Assert.AreEqual(0, CampStories.Unread(p, QuestGiver.Master).Count);
            p.achievements.Add(AchievementCatalog.Get("a_king"), 1);
            Assert.AreEqual(1, CampStories.Unread(p, QuestGiver.Master).Count, "the bone king's story");
            p.bestVictory = 0;
            Assert.Greater(CampStories.Open(p, QuestGiver.Gravekeeper).Count, 1);
            foreach (var line in CampStories.All) Assert.Less(line.Text.Length, 90, $"{line.Id} fits the speech box");
        }
    }
}
