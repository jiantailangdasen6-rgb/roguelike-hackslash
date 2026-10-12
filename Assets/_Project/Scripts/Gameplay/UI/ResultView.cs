using Game.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Victory/defeat screen, kept short: the outcome, one line about the run, the soul shards earned (awarded to
    /// this device's profile) and the way back. The host takes the whole party back to the camp; joiners wait for
    /// that or leave the party.
    /// </summary>
    public sealed class ResultView : MonoBehaviour
    {
        private GameObject _panel;
        private Text _title;
        private Text _summary;
        private Text _splits;
        private bool _logged;
        private Text _shardsText;
        private Text _wait;
        private Button _back;
        private Button _leave;
        private bool _victory;
        private int _shards = -1;
        private float _shownAt;

        private void Awake()
        {
            var root = UIKit.CreateCanvas("Result", 60, transform).transform;
            var panel = UIKit.Panel(root, "Panel", new Color(0f, 0f, 0f, 0.82f));
            panel.rectTransform.Stretch();
            _panel = panel.gameObject;

            _title = UIKit.Title(panel.transform, "", 104);
            _title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 150f), new Vector2(0f, 230f));
            _summary = UIKit.Label(panel.transform, "", 28, TextAnchor.MiddleCenter, UIKit.MutedText);
            _summary.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 44f), new Vector2(0f, 140f));
            _splits = UIKit.Label(panel.transform, "", 24, TextAnchor.MiddleCenter, UIKit.MutedText);
            _splits.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 40f), new Vector2(0f, 100f));
            _shardsText = UIKit.Title(panel.transform, "", 56, TextAnchor.MiddleCenter, new Color(0.62f, 0.86f, 1f));
            _shardsText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 170f), new Vector2(0f, 0f));

            _back = UIKit.Button(panel.transform, "拠点へ戻る", 36, GameSession.ReturnToCamp, UIKit.BloodColor);
            _back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(420f, 92f), new Vector2(0f, -150f));
            _wait = UIKit.Label(panel.transform, "仲間（部屋を作った人）が拠点へ戻るのを待っています…", 28, TextAnchor.MiddleCenter, Color.white);
            _wait.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1200f, 60f), new Vector2(0f, -140f));
            _leave = UIKit.Button(panel.transform, "パーティを抜ける", 24, () => GameSession.ReturnToLobby());
            _leave.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(300f, 60f), new Vector2(0f, -240f));

            _panel.SetActive(false);
            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy() => RunDirector.EventReceived -= OnEvent;

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        private void OnEvent(SimEvent e)
        {
            if (e.Type != SimEventType.RunEnded) return;
            _victory = e.Int == 1;
            _title.text = _victory ? "迷宮を踏破した" : "力尽きた…";
            _title.color = _victory ? UIKit.AccentColor : new Color(0.75f, 0.15f, 0.15f);
            _shards = -1;
            _logged = false;
            _shownAt = Time.time;
            _panel.SetActive(true);
            _back.gameObject.SetActive(IsHost);
            _wait.enabled = !IsHost;
            _leave.gameObject.SetActive(!IsHost);
            EventSystem.current?.SetSelectedGameObject(IsHost ? _back.gameObject : _leave.gameObject);
        }

        /// <summary>"エリア1 4:32（主 1:05）／エリア2 …　倒れた回数 2" from the host's splits.</summary>
        private static string SplitsLine(string text)
        {
            var (areas, downs) = RunSimulation.ParseSplits(text);
            if (areas.Count == 0) return "";
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < areas.Count; i++)
            {
                var a = areas[i];
                if (a[3] < 0) continue;
                string boss = a[1] >= 0 && a[2] >= a[1] ? $"（主 {UIKit.FormatTime(a[2] - a[1])}）" : a[1] >= 0 ? "（主の間で）" : "";
                parts.Add($"エリア{i + 1} {UIKit.FormatTime(a[3] - a[0])}{boss}");
            }
            return string.Join("／", parts) + $"　倒れた回数 {downs}";
        }

        private void Update()
        {
            var d = RunDirector.Instance;
            if (!_panel.activeSelf || d == null) return;

            // Automated runs (-autoreturn): the host takes the party back to the camp after a few seconds.
            if (IsHost && CommandLine.Has("-autoreturn") && Time.time - _shownAt > 4f)
            {
                _shownAt = float.MaxValue;
                GameSession.ReturnToCamp();
                return;
            }

            // NetworkVariables can land a frame after the event, so award shards once they settle.
            if (_shards < 0 && d.Phase.Value != RunPhase.Playing)
            {
                _shards = MetaCatalog.ShardsForRun(d.RoomsCleared.Value, d.BossesKilled.Value, d.Kills.Value, _victory, d.DifficultyLevel.Value) + d.BonusShards.Value;
                if (_victory) ProfileStore.Current.RecordVictory(d.DifficultyLevel.Value);
                ProfileStore.Current.shards += _shards;
                ProfileStore.Save();
            }

            string splits = d.RunSplits.Value.ToString();
            _splits.text = SplitsLine(splits);
            if (!_logged && d.Phase.Value != RunPhase.Playing)
            {
                _logged = true; // pace from real runs: read back from Player.log
                Debug.Log($"[RunStats] {(_victory ? "victory" : "defeat")} difficulty={d.DifficultyLevel.Value} players={NetPlayer.All.Count} elapsed={d.Elapsed.Value} splits={splits} kills={d.Kills.Value}");
            }
            _summary.text = $"{HudView.RoomTitle(d.RoomIndex.Value)}まで　討伐 {d.Kills.Value}　{UIKit.FormatTime(d.Elapsed.Value)}　（{DifficultyCatalog.Get(d.DifficultyLevel.Value).Name}）";
            // One line about requests: what was finished, or the one closest to done.
            string quest = "";
            if (QuestTracker.JustCompleted.Count > 0)
                quest = $"\n<size=26><color=#e0b45a>依頼達成：{string.Join("・", QuestTracker.JustCompleted.ConvertAll(n => $"「{n}」"))}　拠点の掲示板で報酬</color></size>";
            else if (QuestTracker.Nearest() is { } near)
                quest = $"\n<size=24><color=#bfb6aa>依頼「{near.Quest.Name}」 {near.Progress} / {near.Quest.Target}</color></size>";
            _shardsText.text = $"魂の欠片 +{Mathf.Max(0, _shards)}\n<size=26><color=#bfb6aa>所持 {ProfileStore.Current.shards}</color></size>{quest}";
        }
    }
}
