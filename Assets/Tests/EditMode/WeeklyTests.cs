using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>This week's labyrinth: one seed per week, and a best that only improves.</summary>
    public class WeeklyTests
    {
        [Test]
        public void TheWeekRunsMondayToSunday_AndEachWeekHasItsOwnSeed()
        {
            var monday = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
            int week = Weekly.WeekOf(monday);
            Assert.AreEqual(week, Weekly.WeekOf(monday.AddDays(6.9)));
            Assert.AreEqual(week + 1, Weekly.WeekOf(monday.AddDays(7)));
            Assert.AreEqual(week - 1, Weekly.WeekOf(monday.AddHours(-1)));
            Assert.AreEqual("10月12日〜18日", Weekly.Label(week));
            Assert.AreEqual(Weekly.Seed(week), Weekly.Seed(week));
            Assert.AreNotEqual(Weekly.Seed(week), Weekly.Seed(week + 1));
        }

        [Test]
        public void TheWeeksBest_ClearingBeatsDepth_DepthBeatsTime_AndANewWeekStartsOver()
        {
            var p = new ProfileData();
            Assert.IsTrue(p.RecordWeekly(100, false, 5, 600f), "first try");
            Assert.IsFalse(p.RecordWeekly(100, false, 4, 300f), "shallower");
            Assert.IsTrue(p.RecordWeekly(100, false, 7, 900f), "deeper");
            Assert.IsTrue(p.RecordWeekly(100, true, 11, 1200f), "cleared");
            Assert.IsFalse(p.RecordWeekly(100, false, 11, 600f), "not cleared");
            Assert.IsTrue(p.RecordWeekly(100, true, 11, 1000f), "cleared faster");
            Assert.IsTrue(p.RecordWeekly(101, false, 0, 60f), "a new week");
            Assert.AreEqual(101, p.weeklyWeek);
            Assert.IsFalse(p.weeklyCleared);
        }
    }
}
