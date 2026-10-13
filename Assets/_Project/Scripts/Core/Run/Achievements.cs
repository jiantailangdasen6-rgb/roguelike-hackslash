using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Achievements (実績): lifetime deeds counted from the run's events (the same counting as requests), each earning
    /// a title (称号) to wear next to your name. Kept per device in ProfileData.achievements; shown in the codex.
    /// </summary>
    public static class AchievementCatalog
    {
        private static QuestDef A(string id, string name, string text, QuestGoal goal, int count, string title, params EnemyType[] targets) =>
            new() { Id = id, Name = name, Text = text, Goal = goal, Count = count, Targets = targets.Length > 0 ? targets : null, UnlockName = title };

        /// <summary>The title an achievement grants is kept in <see cref="QuestDef.UnlockName"/>.</summary>
        public static string TitleOf(QuestDef a) => a.UnlockName;

        public static readonly QuestDef[] All =
        {
            A("a_kill1", "初陣", "魔物を50体倒す", QuestGoal.Kill, 50, "駆け出し"),
            A("a_kill2", "百鬼斬り", "魔物を500体倒す", QuestGoal.Kill, 500, "魔物狩り"),
            A("a_kill3", "屍の山", "魔物を3000体倒す", QuestGoal.Kill, 3000, "屍山の主"),
            A("a_shatter", "砕氷", "凍った敵を50回砕く", QuestGoal.Shatter, 50, "氷砕き"),
            A("a_crit", "急所", "会心の一撃を500回当てる", QuestGoal.Crit, 500, "急所狙い"),
            A("a_chest", "宝探し", "焚き火の間の宝箱を30個壊す", QuestGoal.BreakChest, 30, "宝探し"),
            A("a_keg", "火遊び", "火薬樽を50個爆発させる", QuestGoal.ExplodeKeg, 50, "火遊び好き"),
            A("a_elite", "精鋭狩り", "精鋭の巣を20回制圧する", QuestGoal.Trial, 20, "精鋭殺し"),
            A("a_mimic", "疑り深い", "ミミックを5体倒す", QuestGoal.Kill, 5, "宝箱を疑う者", EnemyType.Mimic),
            A("a_king", "納骨堂の解放", "骸骨の騎士王を討つ", QuestGoal.KillBoss, 1, "納骨堂の解放者", EnemyType.BoneKing),
            A("a_bishop", "血を断つ", "血の司教を討つ", QuestGoal.KillBoss, 1, "血を断つ者", EnemyType.BloodBishop),
            A("a_lich", "冥王殺し", "冥王リッチを討つ", QuestGoal.KillBoss, 1, "冥王殺し", EnemyType.Lich),
            A("a_saint", "水底の鎮魂", "水底の聖女を討つ", QuestGoal.KillBoss, 1, "水底を鎮めし者", EnemyType.DrownedSaint),
            A("a_queen", "春を呼ぶ", "凍てつく女王を討つ", QuestGoal.KillBoss, 1, "霜を割る者", EnemyType.FrostQueen),
            A("a_colossus", "炉の火を消す", "炉の巨人を討つ", QuestGoal.KillBoss, 1, "炉を鎮めし者", EnemyType.ForgeColossus),
            A("a_win1", "踏破", "迷宮を踏破する", QuestGoal.Victory, 1, "迷宮踏破者"),
            A("a_win5", "覇者", "迷宮を5回踏破する", QuestGoal.Victory, 5, "迷宮の覇者"),
            A("a_deep", "深淵", "深層にたどり着く", QuestGoal.ReachArea, RunSimulation.AreaCount, "深淵を覗く者"),
            A("a_deep3", "深淵の底", "深層 第3階にたどり着く", QuestGoal.ReachArea, RunSimulation.AreaCount + 2, "奈落の旅人"),
            A("a_favor", "神の寵愛", "神の寵愛を目覚めさせる（仲間でもよい）", QuestGoal.Favor, 1, "神に愛されし者"),
            A("a_rest", "火の番人", "焚き火の間に30回たどり着く", QuestGoal.Rest, 30, "火の番人"),
        };

        public static QuestDef Get(string id) => Array.Find(All, a => a.Id == id);
    }

    /// <summary>This device's achievements: what is done, and how far along the rest are.</summary>
    [Serializable]
    public sealed class AchievementLog
    {
        public List<string> done = new();
        public List<string> ids = new();
        public List<int> counts = new();

        public bool IsDone(string id) => done.Contains(id);

        public int Progress(QuestDef a)
        {
            if (IsDone(a.Id)) return a.Target;
            int i = ids.IndexOf(a.Id);
            return i >= 0 ? counts[i] : 0;
        }

        /// <summary>Counts towards an achievement; true when this completes it.</summary>
        public bool Add(QuestDef a, int amount)
        {
            if (IsDone(a.Id) || amount <= 0) return false;
            int i = ids.IndexOf(a.Id);
            if (i < 0)
            {
                ids.Add(a.Id);
                counts.Add(0);
                i = ids.Count - 1;
            }
            counts[i] = Math.Min(a.Target, counts[i] + amount);
            if (counts[i] < a.Target) return false;
            done.Add(a.Id);
            return true;
        }

        /// <summary>Titles earned so far, in catalog order.</summary>
        public IEnumerable<string> Titles()
        {
            foreach (var a in AchievementCatalog.All) if (IsDone(a.Id)) yield return AchievementCatalog.TitleOf(a);
        }

        public void Repair()
        {
            done ??= new List<string>();
            ids ??= new List<string>();
            counts ??= new List<int>();
            while (counts.Count < ids.Count) counts.Add(0);
        }
    }
}
