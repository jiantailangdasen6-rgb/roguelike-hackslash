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

        /// <summary>
        /// Titles worn before the names went katakana (2026-10-17) are kept by achievement id, so a saved title
        /// written the old way comes back as the new one.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> OldTitles = new()
        {
            ["駆け出し"] = "a_kill1", ["魔物狩り"] = "a_kill2", ["屍山の主"] = "a_kill3", ["氷砕き"] = "a_shatter", ["急所狙い"] = "a_crit",
            ["宝探し"] = "a_chest", ["火遊び好き"] = "a_keg", ["精鋭殺し"] = "a_elite", ["宝箱を疑う者"] = "a_mimic",
            ["納骨堂の解放者"] = "a_king", ["血を断つ者"] = "a_bishop", ["冥王殺し"] = "a_lich", ["水底を鎮めし者"] = "a_saint",
            ["霜を割る者"] = "a_queen", ["炉を鎮めし者"] = "a_colossus", ["眠りを払う者"] = "a_mother", ["頁を閉じる者"] = "a_archivist",
            ["灯火を継ぐ者"] = "a_heart", ["迷宮踏破者"] = "a_win1", ["迷宮の覇者"] = "a_win5", ["深淵を覗く者"] = "a_deep",
            ["奈落の旅人"] = "a_deep3", ["神に愛されし者"] = "a_favor", ["火の番人"] = "a_rest", ["契約者"] = "a_pact5",
            ["血盟の者"] = "a_pact10", ["奈落と契りし者"] = "a_pact20",
        };

        /// <summary>The current name of a saved title (an old one is renamed; anything else is returned as it is).</summary>
        public static string RenamedTitle(string title)
        {
            if (string.IsNullOrEmpty(title) || !OldTitles.TryGetValue(title, out var id)) return title;
            foreach (var a in All) if (a.Id == id) return a.UnlockName;
            return title;
        }

        public static readonly QuestDef[] All =
        {
            A("a_kill1", "ファーストブラッド", "モンスターを50体倒す", QuestGoal.Kill, 50, "ルーキーハンター"),
            A("a_kill2", "ハンドレッドスレイヤー", "モンスターを500体倒す", QuestGoal.Kill, 500, "モンスターハンター"),
            A("a_kill3", "コープスマウンテン", "モンスターを3000体倒す", QuestGoal.Kill, 3000, "キング・オブ・コープス"),
            A("a_shatter", "アイスブレイク", "凍った敵を50回砕く", QuestGoal.Shatter, 50, "アイスブレイカー"),
            A("a_crit", "ウィークスポット", "クリティカルの一撃を500回当てる", QuestGoal.Crit, 500, "スナイパー"),
            A("a_chest", "トレジャーハント", "ボンファイアのチェストを30個壊す", QuestGoal.BreakChest, 30, "トレジャーハント"),
            A("a_keg", "プレイウィズファイア", "パウダーケグを50個爆発させる", QuestGoal.ExplodeKeg, 50, "パイロマニア"),
            A("a_elite", "エリートハント", "エリートネストを20回制圧する", QuestGoal.Trial, 20, "エリートスレイヤー"),
            A("a_mimic", "スケプティック", "ミミックを5体倒す", QuestGoal.Kill, 5, "ミミックハンター", EnemyType.Mimic),
            A("a_king", "クリプトリベレーション", "スケルトンキングを討つ", QuestGoal.KillBoss, 1, "クリプトリベレーター", EnemyType.BoneKing),
            A("a_bishop", "ブラッドセヴァー", "ブラッドビショップを討つ", QuestGoal.KillBoss, 1, "ブラッドセヴァラー", EnemyType.BloodBishop),
            A("a_lich", "リッチスレイヤー", "リッチキングを討つ", QuestGoal.KillBoss, 1, "リッチスレイヤー", EnemyType.Lich),
            A("a_saint", "ディープレクイエム", "ドラウンドセイントを討つ", QuestGoal.KillBoss, 1, "ディープピースメイカー", EnemyType.DrownedSaint),
            A("a_queen", "コーリングスプリング", "フロストクイーンを討つ", QuestGoal.KillBoss, 1, "フロストブレイカー", EnemyType.FrostQueen),
            A("a_colossus", "フォージクエンチ", "フォージコロッサスを討つ", QuestGoal.KillBoss, 1, "フォージクエンチャー", EnemyType.ForgeColossus),
            A("a_mother", "アウェイクニング", "マイセリアマザーを討つ", QuestGoal.KillBoss, 1, "ドリームブレイカー", EnemyType.MyceliumMother),
            A("a_archivist", "ラストページ", "グランドライブラリアンを討つ", QuestGoal.KillBoss, 1, "ページクローザー", EnemyType.Archivist),
            A("a_heart", "トーチベアラー", "ラビリンスハートでアッシュキングを討つ", QuestGoal.KillBoss, 1, "トーチベアラー", EnemyType.AshenKing),
            A("a_win1", "クリア", "ラビリンスを踏破する", QuestGoal.Victory, 1, "ラビリンスクリアラー"),
            A("a_win5", "チャンピオン", "ラビリンスを5回踏破する", QuestGoal.Victory, 5, "ラビリンスチャンピオン"),
            A("a_deep", "アビス", "ディープにたどり着く", QuestGoal.ReachArea, RunSimulation.AreaCount, "アビスゲイザー"),
            A("a_deep3", "アビスボトム", "ディープ 第3階にたどり着く", QuestGoal.ReachArea, RunSimulation.AreaCount + 2, "アビスウォーカー"),
            A("a_favor", "ディヴァインフェイバー", "神のフェイバーを目覚めさせる（仲間でもよい）", QuestGoal.Favor, 1, "ゴッズフェイバリット"),
            A("a_rest", "ファイアキーパー", "ボンファイアに30回たどり着く", QuestGoal.Rest, 30, "ファイアキーパー"),
            A("a_pact5", "コントラクトオース", "コントラクトのランク5以上でラビリンスを踏破する", QuestGoal.Contract, 5, "コントラクター"),
            A("a_pact10", "ブラッドパクト", "コントラクトのランク10以上でラビリンスを踏破する", QuestGoal.Contract, 10, "ブラッドボンド"),
            A("a_pact20", "アビスパクト", "コントラクトのランク20以上でラビリンスを踏破する", QuestGoal.Contract, 20, "アビスパクトキーパー"),
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
