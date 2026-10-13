using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>The people in the camp who hand out requests (依頼). Each has a chain; one request at a time each.</summary>
    public enum QuestGiver : byte
    {
        Master,      // 師範: fighting
        Crone,       // お守り職人: chests, kegs, odd monsters
        Gravekeeper, // 墓守: going deeper
    }

    /// <summary>What a request counts. Progress comes from the run's events on every device (team effort).</summary>
    public enum QuestGoal : byte
    {
        Kill,        // monsters of Targets (none = any)
        KillBoss,    // the boss in Targets
        Shatter,     // frozen enemies shattered
        Crit,        // critical hits
        BreakChest,  // rest room chests opened
        ExplodeKeg,  // powder kegs set off
        Rest,        // campfire rooms reached
        Trial,       // elite nests cleared (once: trial doors; the id is kept for saved progress)
        ReachArea,   // reach area Count (1 = blood chapel, 2 = abyss); done once
        Victory,     // labyrinth cleared
    }

    public sealed class QuestDef
    {
        public string Id;
        public QuestGiver Giver;
        public string Name;
        public string Text;
        public QuestGoal Goal;
        public EnemyType[] Targets;
        public int Count;
        public int Shards;
        /// <summary>Optional unlock id given on claiming (a trinket id, or a class "class_xxx").</summary>
        public string Unlock;
        public string UnlockName;

        /// <summary>ReachArea is a yes/no goal: its Count is the area, the bar shows 0/1.</summary>
        public int Target => Goal == QuestGoal.ReachArea ? 1 : Count;
    }

    public static class QuestCatalog
    {
        public static readonly string[] GiverNames = { "師範", "お守り職人", "墓守" };

        private static QuestDef Q(string id, QuestGiver giver, string name, string text, QuestGoal goal, int count, int shards,
            EnemyType[] targets = null, string unlock = null, string unlockName = null) =>
            new() { Id = id, Giver = giver, Name = name, Text = text, Goal = goal, Count = count, Shards = shards, Targets = targets, Unlock = unlock, UnlockName = unlockName };

        private static EnemyType[] T(params EnemyType[] t) => t;

        /// <summary>Each giver's chain, in order. After the last one the giver hands out repeatable requests.</summary>
        public static readonly QuestDef[][] Chains =
        {
            new[]
            {
                Q("m1", QuestGiver.Master, "骨を砕け", "骸骨（兵・弓兵）を倒す", QuestGoal.Kill, 30, 40, T(EnemyType.Skeleton, EnemyType.SkeletonArcher)),
                Q("m2", QuestGiver.Master, "氷を割れ", "凍った敵を強打で砕く", QuestGoal.Shatter, 10, 50),
                Q("m3", QuestGiver.Master, "納骨堂の主", "骸骨の騎士王を討つ", QuestGoal.KillBoss, 1, 70, T(EnemyType.BoneKing), unlock: "class_guardian", unlockName: "新クラス「守護騎士」"),
                Q("m4", QuestGiver.Master, "鎧を崩せ", "亡者の騎士を倒す", QuestGoal.Kill, 6, 70, T(EnemyType.DreadKnight)),
                Q("m5", QuestGiver.Master, "血の司教", "血の司教を討つ", QuestGoal.KillBoss, 1, 100, T(EnemyType.BloodBishop)),
                Q("m6", QuestGiver.Master, "冥王", "冥王リッチを討つ", QuestGoal.KillBoss, 1, 150, T(EnemyType.Lich)),
            },
            new[]
            {
                Q("c1", QuestGiver.Crone, "宝箱をあけて", "焚き火の間の宝箱を壊す", QuestGoal.BreakChest, 3, 30, unlock: "feather", unlockName: "お守り「風切り羽」"),
                Q("c2", QuestGiver.Crone, "火遊び", "火薬樽を爆発させる", QuestGoal.ExplodeKeg, 6, 50),
                Q("c3", QuestGiver.Crone, "膨れの毒袋", "腐れ膨れを倒す", QuestGoal.Kill, 10, 60, T(EnemyType.Bloater), unlock: "class_alchemist", unlockName: "新クラス「錬金術師」"),
                Q("c4", QuestGiver.Crone, "急所を突け", "会心の一撃を当てる", QuestGoal.Crit, 80, 70, unlock: "soul_lantern", unlockName: "お守り「魂の灯籠」"),
                Q("c5", QuestGiver.Crone, "霊の残り香", "怨霊と死霊術師を倒す", QuestGoal.Kill, 12, 90, T(EnemyType.Wraith, EnemyType.Necromancer)),
            },
            new[]
            {
                Q("g1", QuestGiver.Gravekeeper, "火の番", "焚き火の間にたどり着く", QuestGoal.Rest, 2, 30),
                Q("g2", QuestGiver.Gravekeeper, "礼拝堂へ", "血塗られた礼拝堂にたどり着く", QuestGoal.ReachArea, 1, 50, unlock: "class_assassin", unlockName: "新クラス「暗殺者」"),
                Q("g3", QuestGiver.Gravekeeper, "精鋭狩り", "精鋭の巣を制圧する", QuestGoal.Trial, 3, 60),
                Q("g4", QuestGiver.Gravekeeper, "奈落へ", "奈落の玉座にたどり着く", QuestGoal.ReachArea, 2, 80),
                Q("g5", QuestGiver.Gravekeeper, "迷宮の果て", "迷宮を踏破する", QuestGoal.Victory, 1, 150),
            },
        };

        /// <summary>Endless requests once a chain is done (the n-th repeat asks a little more).</summary>
        private static readonly QuestDef[][] Repeats =
        {
            new[]
            {
                Q("mr_kill", QuestGiver.Master, "魔物狩り", "魔物を倒す", QuestGoal.Kill, 80, 40),
                Q("mr_shatter", QuestGiver.Master, "氷を割れ", "凍った敵を強打で砕く", QuestGoal.Shatter, 15, 40),
            },
            new[]
            {
                Q("cr_chest", QuestGiver.Crone, "宝探し", "焚き火の間の宝箱を壊す", QuestGoal.BreakChest, 4, 40),
                Q("cr_crit", QuestGiver.Crone, "急所を突け", "会心の一撃を当てる", QuestGoal.Crit, 100, 40),
            },
            new[]
            {
                Q("gr_rest", QuestGiver.Gravekeeper, "火の番", "焚き火の間にたどり着く", QuestGoal.Rest, 3, 40),
                Q("gr_trial", QuestGiver.Gravekeeper, "精鋭狩り", "精鋭の巣を制圧する", QuestGoal.Trial, 3, 45),
            },
        };

        /// <summary>The giver's current request for this log (never null: chains end in repeats).</summary>
        public static QuestDef Current(QuestLog log, QuestGiver giver)
        {
            var chain = Chains[(int)giver];
            int step = log.Step(giver);
            if (step < chain.Length) return chain[step];
            var pool = Repeats[(int)giver];
            int n = step - chain.Length;
            var t = pool[n % pool.Length];
            // A fresh id per repeat, so its progress starts from zero.
            return Q($"{t.Id}{n}", giver, t.Name, t.Text, t.Goal, t.Count + (n / pool.Length) * (t.Count / 4), t.Shards, t.Targets);
        }

        /// <summary>How much an event advances a request (every peer sees every event: the party's deeds count for all).</summary>
        public static int Amount(QuestDef q, in SimEvent e)
        {
            switch (q.Goal)
            {
                case QuestGoal.Kill:
                case QuestGoal.KillBoss:
                {
                    if (e.Type != SimEventType.EnemyDied || e.Value > 0.5f) return 0; // not the ones that crumbled with the room
                    var type = (EnemyType)e.Int;
                    if (EnemyCatalog.Get(type).Prop) return 0;
                    if (q.Targets == null || q.Targets.Length == 0) return q.Goal == QuestGoal.Kill ? 1 : 0;
                    return System.Array.IndexOf(q.Targets, type) >= 0 ? 1 : 0;
                }
                case QuestGoal.Shatter:
                    return e.Type == SimEventType.EnemyHit && ((HitFlags)e.Int & HitFlags.Shatter) != 0 ? 1 : 0;
                case QuestGoal.Crit:
                    return e.Type == SimEventType.EnemyHit && ((HitFlags)e.Int & HitFlags.Crit) != 0 ? 1 : 0;
                case QuestGoal.BreakChest:
                    return e.Type == SimEventType.EnemyDied && e.Value < 0.5f && (EnemyType)e.Int == EnemyType.TreasureChest ? 1 : 0;
                case QuestGoal.ExplodeKeg:
                    return e.Type == SimEventType.EnemyDied && e.Value < 0.5f && (EnemyType)e.Int == EnemyType.PowderKeg ? 1 : 0;
                case QuestGoal.Rest:
                    return e.Type == SimEventType.RoomEntered && (RoomKind)Mathf.RoundToInt(e.Value) == RoomKind.Campfire ? 1 : 0;
                case QuestGoal.Trial:
                    return e.Type == SimEventType.RoomCleared && (RoomKind)Mathf.RoundToInt(e.Value) == RoomKind.Elite ? 1 : 0;
                case QuestGoal.ReachArea:
                    return e.Type == SimEventType.RoomStarted && e.Int / RunSimulation.RoomsPerArea >= q.Count ? 1 : 0;
                case QuestGoal.Victory:
                    return e.Type == SimEventType.RunEnded && e.Int >= 1 ? 1 : 0;
                default:
                    return 0;
            }
        }

        public static IEnumerable<QuestDef> Active(QuestLog log)
        {
            foreach (QuestGiver g in Enum.GetValues(typeof(QuestGiver))) yield return Current(log, g);
        }
    }

    /// <summary>This device's requests: how far along each giver's chain it is, and the current counts.</summary>
    [Serializable]
    public sealed class QuestLog
    {
        /// <summary>Requests claimed per giver (indexed by QuestGiver).</summary>
        public List<int> steps = new();
        public List<string> progressIds = new();
        public List<int> progressCounts = new();

        public int Step(QuestGiver g) => (int)g < steps.Count ? steps[(int)g] : 0;

        public int Progress(QuestDef q)
        {
            int i = progressIds.IndexOf(q.Id);
            return i >= 0 ? progressCounts[i] : 0;
        }

        public bool IsDone(QuestDef q) => Progress(q) >= q.Target;

        /// <summary>Adds progress; returns true when this made the request complete.</summary>
        public bool Add(QuestDef q, int amount)
        {
            if (amount <= 0 || IsDone(q)) return false;
            int i = progressIds.IndexOf(q.Id);
            if (i < 0)
            {
                progressIds.Add(q.Id);
                progressCounts.Add(0);
                i = progressIds.Count - 1;
            }
            progressCounts[i] = Math.Min(q.Target, progressCounts[i] + amount);
            return progressCounts[i] >= q.Target;
        }

        /// <summary>Turns in a finished request: the giver moves on to the next one.</summary>
        public void Claim(QuestDef q)
        {
            int i = progressIds.IndexOf(q.Id);
            if (i >= 0)
            {
                progressIds.RemoveAt(i);
                progressCounts.RemoveAt(i);
            }
            while (steps.Count <= (int)q.Giver) steps.Add(0);
            steps[(int)q.Giver]++;
        }

        public void Repair()
        {
            steps ??= new List<int>();
            progressIds ??= new List<string>();
            progressCounts ??= new List<int>();
            if (progressCounts.Count != progressIds.Count)
            {
                progressIds.Clear();
                progressCounts.Clear();
            }
        }
    }
}
