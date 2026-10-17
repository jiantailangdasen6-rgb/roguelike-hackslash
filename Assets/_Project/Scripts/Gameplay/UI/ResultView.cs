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
        private readonly Text[] _stats = new Text[3];
        private RectTransform _graph;
        private string _graphFor;
        private bool _logged;
        private Text _shardsText;
        private Text _wait;
        private Button _back;
        private Button _leave;
        private bool _victory;
        private int _shards = -1;
        private float _shownAt;
        private string _weeklyNote = "";

        private void Awake()
        {
            var root = UIKit.CreateCanvas("Result", 60, transform).transform;
            var dim = UIKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.78f));
            dim.rectTransform.Stretch();
            _panel = dim.gameObject;
            var book = UIKit.Framed(dim.transform, "Result", new Color(0.06f, 0.045f, 0.055f, 0.97f));
            book.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1180f, 820f), Vector2.zero);
            var panel = book;

            _title = UIKit.Title(panel.transform, "", 92);
            _title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1100f, 120f), new Vector2(0f, -20f));
            _summary = UIKit.Label(panel.transform, "", 26, TextAnchor.MiddleCenter, UIKit.MutedText);
            _summary.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1100f, 40f), new Vector2(0f, -136f));
            // Three tiles: monsters slain, time, times down.
            string[] heads = { "討伐", "時間", "倒れた回数" };
            for (int i = 0; i < 3; i++)
            {
                var tile = UIKit.Framed(panel.transform, "Stat", new Color(0.03f, 0.025f, 0.03f, 1f));
                tile.Outer().Place(new Vector2(0.5f, 1f), new Vector2(320f, 110f), new Vector2((i - 1) * 345f, -190f));
                UIKit.Label(tile.transform, heads[i], 20, TextAnchor.UpperCenter, UIKit.MutedText).rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(300f, 30f), new Vector2(0f, -6f));
                _stats[i] = UIKit.Title(tile.transform, "", 44, TextAnchor.MiddleCenter, Color.white);
                _stats[i].rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(300f, 60f), new Vector2(0f, 8f));
            }
            // Area times as bars (the lord's fight in red).
            _graph = UIKit.Rect("Graph", panel.transform).Place(new Vector2(0.5f, 1f), new Vector2(1000f, 150f), new Vector2(0f, -320f));
            _splits = UIKit.Label(panel.transform, "", 20, TextAnchor.MiddleCenter, UIKit.MutedText);
            _splits.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1000f, 30f), new Vector2(0f, -470f));
            _splits.text = "<color=#dba84a>■</color> 探索　<color=#c82a24>■</color> ボスとの戦い";
            _shardsText = UIKit.Title(panel.transform, "", 52, TextAnchor.MiddleCenter, new Color(0.62f, 0.86f, 1f));
            _shardsText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1100f, 150f), new Vector2(0f, -505f));

            _back = UIKit.Button(panel.transform, "キャンプへ戻る", 34, GameSession.ReturnToCamp, UIKit.BloodColor);
            _back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(400f, 84f), new Vector2(0f, 34f));
            _wait = UIKit.Label(panel.transform, "仲間（部屋を作った人）がキャンプへ戻るのを待っています…", 26, TextAnchor.MiddleCenter, Color.white);
            _wait.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1100f, 50f), new Vector2(0f, 100f));
            _leave = UIKit.Button(panel.transform, "パーティを抜ける", 24, () => GameSession.ReturnToLobby());
            _leave.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(300f, 56f), new Vector2(0f, 34f));

            _panel.SetActive(false);
            RunDirector.EventReceived += OnEvent;
            gameObject.AddComponent<EndingView>(); // the true ending plays over this screen first
        }

        private void OnDestroy() => RunDirector.EventReceived -= OnEvent;

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        private void OnEvent(SimEvent e)
        {
            if (e.Type != SimEventType.RunEnded) return;
            _victory = e.Int >= 1; // 2: fell in the deep layers after beating the Lich (still a conquest)
            _title.text = e.Int == 3 ? "心臓に火を灯した" : e.Int == 2 ? "ディープに散った" : _victory ? "ラビリンスを踏破した" : "力尽きた…";
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

        /// <summary>One bar per area: its length is the time spent there, the lord's fight drawn in red at its end.</summary>
        private void UpdateGraph(string text)
        {
            if (text == _graphFor) return;
            _graphFor = text;
            for (int i = _graph.childCount - 1; i >= 0; i--) Destroy(_graph.GetChild(i).gameObject);
            var (areas, _) = RunSimulation.ParseSplits(text);
            float longest = 300f;
            foreach (var a in areas) if (a[3] >= 0) longest = Mathf.Max(longest, a[3] - a[0]);
            const float barMax = 600f, left = 224f; // the land names on the left
            float step = Mathf.Min(46f, 150f / Mathf.Max(1, areas.Count)); // the deep layers squeeze the bars together
            float thick = Mathf.Min(26f, step - 8f);
            for (int i = 0; i < areas.Count; i++)
            {
                var a = areas[i];
                if (a[3] < 0) continue;
                float y = -i * step;
                var label = UIKit.Label(_graph, HudView.AreaName(i), step < 40f ? 16 : 20, TextAnchor.MiddleRight, UIKit.MutedText);
                label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(left - 14f, 36f), new Vector2(0f, y));
                var back = UIKit.Panel(_graph, "Back", new Color(0.12f, 0.1f, 0.1f, 1f));
                back.rectTransform.Place(new Vector2(0f, 1f), new Vector2(barMax, thick), new Vector2(left, y - 5f));
                float total = a[3] - a[0];
                var bar = UIKit.Panel(_graph, "Bar", new Color(0.86f, 0.66f, 0.3f, 1f));
                bar.sprite = UIKit.GlossSprite;
                bar.rectTransform.Place(new Vector2(0f, 1f), new Vector2(barMax * total / longest, thick), new Vector2(left, y - 5f));
                if (a[1] >= 0)
                {
                    float bossStart = a[1] - a[0], bossEnd = (a[2] >= a[1] ? a[2] : a[3]) - a[0];
                    var boss = UIKit.Panel(_graph, "Boss", new Color(0.78f, 0.16f, 0.14f, 1f));
                    boss.sprite = UIKit.GlossSprite;
                    boss.rectTransform.Place(new Vector2(0f, 1f), new Vector2(barMax * (bossEnd - bossStart) / longest, thick), new Vector2(left + barMax * bossStart / longest, y - 5f));
                }
                string bossText = a[1] >= 0 && a[2] >= a[1] ? $"<color=#ff8070>ボス {UIKit.FormatTime(a[2] - a[1])}</color>　" : "";
                var time = UIKit.Label(_graph, bossText + UIKit.FormatTime(total), 22, TextAnchor.MiddleLeft, Color.white);
                time.rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 36f), new Vector2(left + barMax + 14f, y));
            }
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
                _shards = MetaCatalog.ShardsForRun(d.RoomsCleared.Value, d.BossesKilled.Value, d.Kills.Value, _victory, d.DifficultyLevel.Value, d.ContractRank.Value) + d.BonusShards.Value;
                // The camp's soul urn adds its share.
                _shards = Mathf.RoundToInt(_shards * (1f + CampUpgrades.UrnShards * ProfileStore.Current.CampLevel(CampUpgrade.Urn)));
                if (_victory) ProfileStore.Current.RecordVictory(d.DifficultyLevel.Value);
                if (_victory && d.WeeklyWeek.Value < 0) ProfileStore.Current.bestContract = Mathf.Max(ProfileStore.Current.bestContract, d.ContractRank.Value);
                // This week's labyrinth: each device keeps its best of the week.
                _weeklyNote = "";
                if (d.WeeklyWeek.Value >= 0)
                    _weeklyNote = ProfileStore.Current.RecordWeekly(d.WeeklyWeek.Value, _victory, d.RoomIndex.Value, d.Elapsed.Value)
                        ? "　<color=#9fd8ff>今週のラビリンス・自己ベスト更新！</color>"
                        : "　<color=#9fd8ff>今週のラビリンス</color>";
                ProfileStore.Current.shards += _shards;
                ProfileStore.Save();
            }

            string splits = d.RunSplits.Value.ToString();
            UpdateGraph(splits);
            if (!_logged && d.Phase.Value != RunPhase.Playing)
            {
                _logged = true; // pace from real runs: read back from Player.log
                Debug.Log($"[RunStats] {(_victory ? "victory" : "defeat")} difficulty={d.DifficultyLevel.Value} players={NetPlayer.All.Count} elapsed={d.Elapsed.Value} splits={splits} kills={d.Kills.Value}");
            }
            _summary.text = $"{HudView.RoomTitle(d.RoomIndex.Value)}まで　（{DifficultyCatalog.Get(d.DifficultyLevel.Value).Name}）{_weeklyNote}";
            _stats[0].text = d.Kills.Value.ToString();
            _stats[1].text = UIKit.FormatTime(d.Elapsed.Value);
            _stats[2].text = RunSimulation.ParseSplits(splits).Downs.ToString();
            // One line about requests: what was finished, or the one closest to done.
            string quest = "";
            if (QuestTracker.JustCompleted.Count > 0)
                quest = $"\n<size=26><color=#e0b45a>クエスト達成：{string.Join("・", QuestTracker.JustCompleted.ConvertAll(n => $"「{n}」"))}　キャンプのボードで報酬</color></size>";
            else if (QuestTracker.Nearest() is { } near)
                quest = $"\n<size=24><color=#bfb6aa>クエスト「{near.Quest.Name}」 {near.Progress} / {near.Quest.Target}</color></size>";
            _shardsText.text = $"ソウルシャード +{Mathf.Max(0, _shards)}\n<size=26><color=#bfb6aa>所持 {ProfileStore.Current.shards}</color></size>{quest}";
        }
    }
}
