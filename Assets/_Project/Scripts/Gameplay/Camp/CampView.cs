using System.Collections.Generic;
using Game.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The camp's screen: a small sign over each station (brighter, with the key to press, when you stand at it),
    /// the gate's party count and countdown, allies' names, and a corner card with the shards and the room code.
    /// F / Enter / gamepad X (or clicking the sign) opens the station's panel (<see cref="CampPanels"/>).
    /// </summary>
    public sealed class CampView : MonoBehaviour
    {
        public static bool PanelOpen => _instance != null && _instance._panels.Open != null;

        /// <summary>Esc closed a panel this frame: the pause menu must not open on the same press.</summary>
        public static bool ConsumedEscape => _instance != null && _instance._escapeFrame == Time.frameCount;

        private static CampView _instance;

        private sealed class Sign
        {
            public CampStationDef Station;
            public RectTransform Root;
            public Text Name;
            public Button Prompt;
            public Text PromptText;
        }

        private RectTransform _root;
        private CampPanels _panels;
        private readonly List<Sign> _signs = new();
        private Text _gateText;
        private Text _cornerText;
        private Button _invite;
        private Text _inviteLabel;
        private float _copiedUntil;
        private Text _hint;
        private readonly Dictionary<NetPlayer, Text> _names = new();
        private readonly List<NetPlayer> _gone = new();
        private Camera _camera;
        private int _escapeFrame = -1;
        private float _hintUntil;
        private SpriteRenderer _gateRing;

        private void Awake()
        {
            _instance = this;
            QuestTracker.JustCompleted.Clear();
            _root = (RectTransform)UIKit.CreateCanvas("Camp", 20, transform).transform;

            foreach (var station in Camp.Stations)
                if (station.Id != CampStation.Gate) _signs.Add(CreateSign(station));

            _gateText = UIKit.Title(_root, "", 26, TextAnchor.UpperCenter, Color.white);
            _gateText.rectTransform.sizeDelta = new Vector2(520f, 120f);
            _gateText.rectTransform.pivot = new Vector2(0.5f, 1f);

            // Where to stand to set out: a ring on the ground before the gate, brightening as the party gathers.
            var ring = new GameObject("GateRing");
            ring.transform.position = Camp.Get(CampStation.Gate).Front;
            ring.transform.localScale = Vector3.one * Camp.GateRadius; // fx_circle is 2 units across
            _gateRing = ring.AddComponent<SpriteRenderer>();
            _gateRing.sprite = SpriteSheet.Load("fx_circle")?.First;
            _gateRing.sortingOrder = -400;

            // Corner card: shards, and for online rooms the code and the invite link.
            var corner = UIKit.Framed(_root, "Corner", new Color(0.04f, 0.03f, 0.05f, 0.82f));
            corner.Outer().Place(new Vector2(0f, 1f), new Vector2(430f, 128f), new Vector2(24f, -24f));
            _cornerText = UIKit.Label(corner.transform, "", 24, TextAnchor.UpperLeft);
            _cornerText.rectTransform.Stretch(14f);
            _invite = UIKit.Button(corner.transform, "招待リンクをコピー", 20, CopyInvite);
            _invite.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(210f, 40f), new Vector2(-10f, 10f));
            _inviteLabel = _invite.GetComponentInChildren<Text>();

            _hint = UIKit.Label(_root, "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.8f));
            _hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1200f, 50f), new Vector2(0f, 40f));
            _hintUntil = Time.time + 12f;

            _panels = new CampPanels(_root, ClosePanel);
            ProfileStore.Changed += OnProfileChanged;
        }

        private void OnDestroy()
        {
            ProfileStore.Changed -= OnProfileChanged;
            if (_instance == this) _instance = null;
        }

        private void OnProfileChanged() => _panels.Rebuild();

        private Sign CreateSign(CampStationDef station)
        {
            var s = new Sign { Station = station, Root = UIKit.Rect(station.Name, _root) };
            s.Root.sizeDelta = new Vector2(10f, 10f);
            s.Name = UIKit.Title(s.Root, station.Name, 22, TextAnchor.LowerCenter, new Color(0.9f, 0.8f, 0.62f, 0.75f));
            s.Name.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(300f, 32f), Vector2.zero);

            s.Prompt = UIKit.Button(s.Root, "", 22, () => OpenPanel(station.Id), new Color(0.12f, 0.07f, 0.05f, 0.92f));
            s.Prompt.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(330f, 44f), new Vector2(0f, -26f));
            s.PromptText = s.Prompt.GetComponentInChildren<Text>();
            s.PromptText.text = $"<color=#e0b45a>[F]</color> {station.Action}";
            s.Prompt.gameObject.SetActive(false);
            return s;
        }

        // ------------------------------------------------------------------ Panels

        private void OpenPanel(CampStation station)
        {
            if (PauseMenuView.IsOpen) return;
            _panels.Show(station);
            SfxPlayer.Instance?.Play("click");
        }

        private void ClosePanel()
        {
            if (_panels.Open == null) return;
            _panels.Hide();
            SfxPlayer.Instance?.Play("click");
        }

        private void CopyInvite()
        {
            if (GameSession.JoinCode == null) return;
            BrowserBridge.CopyText(BrowserBridge.InviteLink(GameSession.JoinCode));
            _copiedUntil = Time.time + 3f;
        }

        // ------------------------------------------------------------------ Frame

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            var local = NetPlayer.Local;
            CampStationDef? here = local != null ? Camp.StationAt(local.transform.position) : null;

            if (_panels.Open != null)
            {
                bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                              || Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame);
                // Walking away (pushed, or a gamepad stick) also closes it.
                if (escape || here == null || here.Value.Id != _panels.Open.Value)
                {
                    _escapeFrame = Time.frameCount;
                    ClosePanel();
                }
            }
            else if (here != null && !PauseMenuView.IsOpen && InteractPressed())
            {
                OpenPanel(here.Value.Id);
            }

            UpdateSigns(here);
            UpdateGate();
            UpdateNames(local);
            UpdateCorner();

            bool panel = _panels.Open != null;
            _hint.enabled = !panel && Time.time < _hintUntil;
            _hint.text = "WASD で歩く　施設の前で F　準備ができたら全員で奥の門へ";
        }

        private static bool InteractPressed() =>
            Keyboard.current != null && (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
            || Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;

        private Vector3 ToScreen(Vector2 world) => _camera != null ? _camera.WorldToScreenPoint(world) : Vector3.zero;

        private void UpdateSigns(CampStationDef? here)
        {
            bool panel = _panels.Open != null;
            foreach (var s in _signs)
            {
                s.Root.gameObject.SetActive(!panel && _camera != null);
                if (!s.Root.gameObject.activeSelf) continue;
                float lift = s.Station.Id is CampStation.Mirror or CampStation.Tablet ? 2.8f : 2.2f;
                s.Root.position = ToScreen(s.Station.Position + Vector2.up * lift);
                bool near = here != null && here.Value.Id == s.Station.Id;
                // Finished requests wait at the board: a pulsing mark draws people over.
                bool waiting = s.Station.Id == CampStation.Board && CampPanels.Claimable > 0;
                s.Name.text = waiting ? $"{s.Station.Name} <color=#ffd860>！</color>" : s.Station.Name;
                s.Name.color = near || waiting ? UIKit.AccentColor : new Color(0.9f, 0.8f, 0.62f, 0.7f);
                s.Name.fontSize = near ? 26 : waiting ? 24 + Mathf.RoundToInt(Mathf.PingPong(Time.time * 4f, 2f)) : 22;
                if (s.Prompt != null) s.Prompt.gameObject.SetActive(near);
            }
        }

        private void UpdateGate()
        {
            var d = CampDirector.Instance;
            bool ready = d != null && d.IsSpawned;
            float departing = ready ? d.DepartProgress.Value / Camp.DepartSeconds : 0f;
            int inGate = ready ? d.InGate.Value : 0;
            if (_gateRing != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (departing > 0f ? 9f : 2.5f));
                var col = Color.Lerp(new Color(1f, 0.72f, 0.35f), new Color(1f, 0.92f, 0.6f), departing);
                col.a = 0.16f + 0.08f * pulse + (inGate > 0 ? 0.14f : 0f) + 0.3f * departing;
                _gateRing.color = col;
            }

            bool show = ready && _panels.Open == null && _camera != null;
            _gateText.enabled = show;
            if (!show) return;
            var gate = Camp.Get(CampStation.Gate);
            _gateText.rectTransform.position = ToScreen(gate.Front + Vector2.down * (Camp.GateRadius + 0.15f));
            int party = NetPlayer.All.Count;
            string difficulty = $"<size=20><color=#bfb6aa>難易度：{DifficultyCatalog.Get(d.DifficultyLevel.Value).Name}</color></size>";
            string status = departing > 0.02f
                ? $"<color=#e0b45a>出発まで {Mathf.Max(1, Mathf.CeilToInt(Camp.DepartSeconds - d.DepartProgress.Value))}</color>"
                : inGate > 0 ? $"<size=22>門の前 {inGate} / {party} 人</size>"
                : party > 1 ? "<size=22>全員で輪の中に立つと出発</size>" : "<size=22>輪の中に立つと出発</size>";
            _gateText.text = $"出発の門\n{status}\n{difficulty}";
        }

        private void UpdateNames(NetPlayer local)
        {
            bool panel = _panels.Open != null;
            foreach (var p in NetPlayer.All)
            {
                if (!_names.TryGetValue(p, out var label))
                {
                    label = UIKit.Label(_root, "", 20, TextAnchor.LowerCenter, p.Color);
                    label.rectTransform.sizeDelta = new Vector2(240f, 30f);
                    _names[p] = label;
                }
                label.enabled = !panel && _camera != null && NetPlayer.All.Count > 1;
                if (!label.enabled) continue;
                label.color = p.Color;
                label.text = $"{p.DisplayName} {p.ClassDef.Name}" + (p == local ? "（あなた）" : "");
                label.rectTransform.position = ToScreen((Vector2)p.transform.position + Vector2.up * 1.7f);
            }
            _gone.Clear();
            foreach (var kv in _names) if (kv.Key == null || !NetPlayer.All.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var p in _gone)
            {
                if (_names[p] != null) Destroy(_names[p].gameObject);
                _names.Remove(p);
            }
        }

        private void UpdateCorner()
        {
            var nm = NetworkManager.Singleton;
            string code = GameSession.JoinCode;
            string room = code != null ? $"ルームコード <color=#e0b45a><b>{code}</b></color>"
                : nm != null && nm.IsServer && !(nm.NetworkConfig.NetworkTransport is Unity.Netcode.Transports.SinglePlayer.SinglePlayerTransport)
                    ? $"LAN の部屋（IP {GameSession.LocalAddress()}）"
                    : nm != null && !nm.IsServer ? "仲間の部屋" : "ひとり旅";
            _cornerText.text = $"拠点　<color=#9fd8ff>魂の欠片 {ProfileStore.Current.shards}</color>\n<size=22>{room}　仲間 {NetPlayer.All.Count}/{GameSession.MaxPlayers}</size>";
            _invite.gameObject.SetActive(code != null);
            _inviteLabel.text = Time.time < _copiedUntil ? "コピーしました" : "招待リンクをコピー";
        }
    }
}
