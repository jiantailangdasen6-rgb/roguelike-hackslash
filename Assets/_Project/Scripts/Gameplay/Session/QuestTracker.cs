using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Counts progress on this device's requests (<see cref="QuestLog"/>) from the run's events. Every peer receives
    /// every event, so the whole party's deeds count for everyone. Finished requests are turned in at the camp's
    /// notice board. Lives for the whole session.
    /// </summary>
    public sealed class QuestTracker : MonoBehaviour
    {
        /// <summary>Requests finished since the party last left the camp (shown on the result screen).</summary>
        public static readonly List<string> JustCompleted = new();

        private bool _dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("QuestTracker");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestTracker>();
        }

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        private void OnApplicationQuit() => SaveIfDirty();

        private void SaveIfDirty()
        {
            if (!_dirty) return;
            _dirty = false;
            ProfileStore.Save();
        }

        private void OnEvent(SimEvent e)
        {
            var log = ProfileStore.Current.quests;
            foreach (var q in QuestCatalog.Active(log))
            {
                int amount = QuestCatalog.Amount(q, e);
                if (amount <= 0 || log.IsDone(q)) continue;
                _dirty = true;
                if (!log.Add(q, amount)) continue;
                JustCompleted.Add(q.Name);
                HudView.Instance?.Toast($"クエスト「{q.Name}」達成！　キャンプのボードで報酬を受け取ろう");
                SfxPlayer.Instance?.Play("levelup", null, 1.15f);
            }
            // Achievements count the same deeds over a lifetime; each earns a title.
            var deeds = ProfileStore.Current.achievements;
            foreach (var a in AchievementCatalog.All)
            {
                int amount = QuestCatalog.Amount(a, e);
                if (amount <= 0 || deeds.IsDone(a.Id)) continue;
                _dirty = true;
                if (!deeds.Add(a, amount)) continue;
                Codex.Record(CodexCatalog.AchievementEntry(a.Id));
                HudView.Instance?.Toast($"<color=#e6c766>アチーブメント「{a.Name}」</color>　タイトル「{AchievementCatalog.TitleOf(a)}」を得た（キャンプのミラーで付けられる）");
                SfxPlayer.Instance?.Play("levelup", null, 0.95f);
            }
            RecordStory(e);
            if (e.Type is SimEventType.RoomCleared or SimEventType.RunEnded or SimEventType.RoomStarted) SaveIfDirty();
        }

        /// <summary>
        /// The story (Lore): a land's inscription when the team arrives in it, a lord's memory when it falls (kept in the
        /// profile: all six open the way to the heart), the heart and the ending.
        /// </summary>
        private void RecordStory(SimEvent e)
        {
            var profile = ProfileStore.Current;
            switch (e.Type)
            {
                case SimEventType.RoomStarted:
                {
                    var land = (Biome)Mathf.RoundToInt(e.Value);
                    Codex.Record(land == Biome.Heart ? Lore.HeartId : Lore.InscriptionId(land));
                    break;
                }
                case SimEventType.EnemyDied when e.Value < 0.5f && EnemyCatalog.Get((EnemyType)e.Int).Boss && (EnemyType)e.Int != EnemyType.AshenKing:
                {
                    var land = BiomeCatalog.OfLord((EnemyType)e.Int);
                    int bit = Lore.MemoryBit(land);
                    Codex.Record(Lore.MemoryIdOf((EnemyType)e.Int));
                    if (bit == 0 || (profile.memories & bit) != 0) break;
                    profile.memories |= bit;
                    _dirty = true;
                    int count = Lore.Count(profile.memories);
                    HudView.Instance?.Toast(count >= 6
                        ? "<color=#ffd890>六つのメモリーがそろった</color>　三つ目のボスの先に、ラビリンスハートへの道が開く"
                        : $"<color=#ffd890>{Lore.Get(Lore.MemoryIdOf((EnemyType)e.Int))?.Name}</color>を得た（メモリー {count}/6、コーデックスの「記録」）");
                    break;
                }
                case SimEventType.RunEnded when e.Int == 3:
                    profile.heartLit++;
                    _dirty = true;
                    Codex.Record(Lore.EndingId);
                    break;
            }
        }

        /// <summary>The unfinished request closest to done, for a one-line reminder (null when none has started).</summary>
        public static (QuestDef Quest, int Progress)? Nearest()
        {
            var log = ProfileStore.Current.quests;
            (QuestDef, int)? best = null;
            float bestFraction = 0f;
            foreach (var q in QuestCatalog.Active(log))
            {
                int p = log.Progress(q);
                if (p <= 0 || p >= q.Target) continue;
                float f = p / (float)q.Target;
                if (f > bestFraction)
                {
                    bestFraction = f;
                    best = (q, p);
                }
            }
            return best;
        }
    }
}
