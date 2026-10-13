using System;

namespace Game.Core
{
    /// <summary>
    /// This week's labyrinth: everyone who sets out this week (Monday to Sunday, UTC) walks the same maps, on the
    /// standard difficulty, and each device keeps its best for the week (ProfileData.weekly*).
    /// </summary>
    public static class Weekly
    {
        private static readonly DateTime FirstMonday = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Weeks since 2024-01-01 (a Monday).</summary>
        public static int WeekOf(DateTime utc) => (int)Math.Floor((utc.Date - FirstMonday).TotalDays / 7.0);

        public static int Current => WeekOf(DateTime.UtcNow);

        public static DateTime StartOf(int week) => FirstMonday.AddDays(week * 7);

        /// <summary>The run seed of a week (well mixed, so neighbouring weeks look nothing alike).</summary>
        public static ulong Seed(int week)
        {
            ulong x = (ulong)(week + 1) * 0x9E3779B97F4A7C15ul;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9ul;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBul;
            return x ^ (x >> 31);
        }

        /// <summary>"10月12日〜18日" (the week's Monday to Sunday).</summary>
        public static string Label(int week)
        {
            var from = StartOf(week);
            var to = from.AddDays(6);
            return from.Month == to.Month ? $"{from.Month}月{from.Day}日〜{to.Day}日" : $"{from.Month}月{from.Day}日〜{to.Month}月{to.Day}日";
        }

        /// <summary>Is a result better than the week's best so far? Clearing beats not; then deeper; then faster.</summary>
        public static bool Better(bool cleared, int room, float time, bool bestCleared, int bestRoom, float bestTime)
        {
            if (bestRoom < 0) return true;
            if (cleared != bestCleared) return cleared;
            if (cleared) return time < bestTime;
            if (room != bestRoom) return room > bestRoom;
            return time < bestTime;
        }
    }
}
