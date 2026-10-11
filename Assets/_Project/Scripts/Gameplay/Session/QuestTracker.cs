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
                HudView.Instance?.Toast($"依頼「{q.Name}」達成！　拠点の掲示板で報酬を受け取ろう");
                SfxPlayer.Instance?.Play("levelup", null, 1.15f);
            }
            if (e.Type is SimEventType.RoomCleared or SimEventType.RunEnded or SimEventType.RoomStarted) SaveIfDirty();
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
