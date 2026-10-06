using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>In-run HUD: XP/level, room objective, HP, teammates, boss bar, banners and toasts.</summary>
    public sealed class HudView : MonoBehaviour
    {
        public static HudView Instance { get; private set; }

        private static readonly string[] AreaNames = { "苔むした遺跡", "地下墓所", "溶岩の深淵" };

        private UIKit.Bar _xpBar;
        private Text _levelText;
        private Text _roomText;
        private Text _objectiveText;
        private UIKit.Bar _hpBar;
        private Text _hpText;
        private Text _teamText;
        private Text _timeText;
        private UIKit.Bar _bossBar;
        private Text _centerText;
        private Text _bannerText;
        private float _bannerUntil;
        private Text _toastText;
        private readonly List<(string Text, float Until)> _toasts = new();
        private readonly StringBuilder _sb = new();

        private void Awake()
        {
            Instance = this;
            var root = UIKit.CreateCanvas("HUD", 20, transform).transform;

            _xpBar = UIKit.CreateBar(root, "XpBar", new Color(0.35f, 0.7f, 1f), new Color(0f, 0f, 0f, 0.6f));
            var xp = _xpBar.Root;
            xp.anchorMin = new Vector2(0f, 1f);
            xp.anchorMax = new Vector2(1f, 1f);
            xp.pivot = new Vector2(0.5f, 1f);
            xp.sizeDelta = new Vector2(0f, 20f);
            xp.anchoredPosition = Vector2.zero;

            _levelText = UIKit.Label(root, "Lv 1", 34, TextAnchor.UpperLeft);
            _levelText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 50f), new Vector2(24f, -30f));

            _roomText = UIKit.Label(root, "", 28, TextAnchor.UpperLeft);
            _roomText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(600f, 40f), new Vector2(24f, -80f));
            _objectiveText = UIKit.Label(root, "", 30, TextAnchor.UpperLeft, UIKit.AccentColor);
            _objectiveText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(760f, 40f), new Vector2(24f, -120f));

            _hpBar = UIKit.CreateBar(root, "HpBar", new Color(0.9f, 0.25f, 0.3f), new Color(0f, 0f, 0f, 0.6f));
            _hpBar.Root.Place(new Vector2(0f, 1f), new Vector2(380f, 26f), new Vector2(24f, -170f));
            _hpText = UIKit.Label(_hpBar.Root, "", 20, TextAnchor.MiddleCenter);
            _hpText.rectTransform.Stretch();

            _teamText = UIKit.Label(root, "", 24, TextAnchor.UpperLeft);
            _teamText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(420f, 120f), new Vector2(24f, -206f));

            _timeText = UIKit.Label(root, "", 30, TextAnchor.UpperCenter);
            _timeText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(300f, 44f), new Vector2(0f, -30f));

            _bossBar = UIKit.CreateBar(root, "BossBar", new Color(0.75f, 0.2f, 0.9f), new Color(0f, 0f, 0f, 0.7f));
            _bossBar.Root.Place(new Vector2(0.5f, 1f), new Vector2(820f, 26f), new Vector2(0f, -80f));
            var bossLabel = UIKit.Label(_bossBar.Root, "キングスライム", 20, TextAnchor.MiddleCenter);
            bossLabel.rectTransform.Stretch();

            var bag = UIKit.Button(root, "バッグ", 30, () => InventoryView.Instance?.Toggle());
            bag.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(180f, 72f), new Vector2(-24f, -34f));

            _centerText = UIKit.Label(root, "", 40, TextAnchor.MiddleCenter);
            _centerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1200f, 200f), new Vector2(0f, 160f));

            _bannerText = UIKit.Label(root, "", 56, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _bannerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 100f), new Vector2(0f, 300f));

            _toastText = UIKit.Label(root, "", 28, TextAnchor.LowerCenter);
            _toastText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1100f, 200f), new Vector2(0f, 40f));

            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy()
        {
            RunDirector.EventReceived -= OnEvent;
            if (Instance == this) Instance = null;
        }

        public void Toast(string text)
        {
            _toasts.Add((text, Time.time + 4f));
            if (_toasts.Count > 4) _toasts.RemoveAt(0);
        }

        public void Banner(string text, float seconds = 2.5f)
        {
            _bannerText.text = text;
            _bannerUntil = Time.time + seconds;
        }

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.RoomStarted:
                    int area = e.Int / RunSimulation.RoomsPerArea;
                    bool boss = e.Int % RunSimulation.RoomsPerArea == RunSimulation.RoomsPerArea - 1;
                    Banner(boss ? "ボス部屋" : $"{AreaNames[Mathf.Min(area, AreaNames.Length - 1)]}  {e.Int % RunSimulation.RoomsPerArea + 1}部屋目");
                    break;
                case SimEventType.RoomCleared:
                    Banner("クリア！ 全員で出口へ");
                    break;
                case SimEventType.BossSpawned:
                    Banner("キングスライムが現れた！");
                    break;
                case SimEventType.PlayerDowned:
                    if (NetPlayer.Local != null && e.ClientId != NetPlayer.Local.OwnerClientId) Toast("仲間がダウン！ 近くに立って助けよう");
                    break;
            }
        }

        private void Update()
        {
            var director = RunDirector.Instance;
            var local = NetPlayer.Local;
            if (director == null || !director.IsSpawned) return;

            _xpBar.Fill = director.XpFraction.Value;
            _levelText.text = $"Lv {director.TeamLevel.Value}";
            _timeText.text = UIKit.FormatTime(director.Elapsed.Value);

            int room = director.RoomIndex.Value;
            int areaIndex = Mathf.Min(room / RunSimulation.RoomsPerArea, AreaNames.Length - 1);
            _roomText.text = $"{AreaNames[areaIndex]}  部屋 {room % RunSimulation.RoomsPerArea + 1}/{RunSimulation.RoomsPerArea}   （全{RunSimulation.TotalRooms}部屋中 {room + 1}）";
            _objectiveText.text = ObjectiveText(director);

            float boss = director.BossHealth.Value;
            UIKit.Toggle(_bossBar.Root.gameObject, boss >= 0f);
            if (boss >= 0f) _bossBar.Fill = boss;

            if (local != null)
            {
                _hpBar.Fill = local.MaxHp.Value > 0f ? local.Hp.Value / local.MaxHp.Value : 0f;
                _hpText.text = $"{local.Hp.Value:0} / {local.MaxHp.Value:0}";
            }

            _sb.Clear();
            foreach (var p in NetPlayer.All)
            {
                if (p == local) continue;
                string state = p.Downed.Value ? "<color=#FF6060>ダウン</color>" : $"{p.Hp.Value:0}/{p.MaxHp.Value:0}";
                _sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.DisplayName}</color>  {state}\n");
            }
            _teamText.text = _sb.ToString();

            _centerText.text = CenterMessage(director, local);
            _bannerText.enabled = Time.time < _bannerUntil;

            _toasts.RemoveAll(t => t.Until < Time.time);
            _sb.Clear();
            foreach (var t in _toasts) _sb.Append(t.Text).Append('\n');
            _toastText.text = _sb.ToString();
        }

        private static string ObjectiveText(RunDirector d)
        {
            if (d.RoomCleared.Value)
            {
                string hold = d.ExitHold.Value > 0f ? $"  移動中… {d.ExitHold.Value / RunSimulation.ExitHoldTime:P0}" : "";
                return $"クリア！ 全員で上の出口に入ろう{hold}";
            }
            return d.Objective.Value switch
            {
                RoomObjective.Survive => $"生き残れ  残り {UIKit.FormatTime(d.Target.Value - d.Progress.Value)}",
                RoomObjective.Kill => $"敵を倒せ  {d.Progress.Value:0} / {d.Target.Value:0}",
                _ => "ボスを倒せ",
            };
        }

        private static string CenterMessage(RunDirector d, NetPlayer local)
        {
            if (d.Phase.Value != RunPhase.Playing) return "";
            if (local == null) return d.Started.Value ? "" : "仲間を待っています…";
            if (local.Downed.Value)
                return $"ダウン中… 仲間が近くにいると復活します  {local.ReviveProgress.Value:P0}";
            if (d.Paused.Value && !LevelUpView.IsShowing) return "仲間がレベルアップの強化を選んでいます…";
            return "";
        }
    }
}
