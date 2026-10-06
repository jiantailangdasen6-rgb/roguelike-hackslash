using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Title/lobby: play solo, host for friends on the same network, or join by IP. Also opens the bag.
    /// Command line for automated tests: -autosolo | -autohost &lt;players&gt; | -autojoin &lt;ip&gt;.
    /// </summary>
    public sealed class LobbyView : MonoBehaviour
    {
        private enum State { Menu, Hosting, Joining }

        private State _state;
        private GameObject _menu;
        private GameObject _waiting;
        private InputField _address;
        private Text _status;
        private Text _waitingText;
        private Button _startButton;
        private Button _soloButton;
        private int _autoHostPlayers;
        private string _autoJoin;
        private float _nextAutoJoin;

        private void Awake()
        {
            GameSession.EnsureNetworkManager();
            BuildUi();

            _status.text = GameSession.LobbyMessage ?? "";
            GameSession.LobbyMessage = null;
        }

        private System.Collections.IEnumerator Start()
        {
            // Netcode registers its message types in a RuntimeInitializeOnLoadMethod that runs after the
            // first scene's Awake, so starting a session from Awake fails in builds. Wait a frame.
            yield return null;

            if (CommandLine.Has("-autosolo")) GameSession.StartSolo();
            else if (CommandLine.Has("-autohost"))
            {
                _autoHostPlayers = Mathf.Clamp((int)CommandLine.GetFloat("-autohost", 2), 1, GameSession.MaxPlayers);
                Host();
            }
            else if (CommandLine.Has("-autojoin")) _autoJoin = CommandLine.GetValue("-autojoin");
        }

        private void BuildUi()
        {
            var root = UIKit.CreateCanvas("Lobby", 10, transform).transform;
            var bg = UIKit.Panel(root, "Background", new Color(0.06f, 0.06f, 0.09f));
            bg.rectTransform.Stretch();

            var title = UIKit.Label(root, "迷宮サバイバー（仮）", 96, TextAnchor.MiddleCenter, UIKit.AccentColor);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1400f, 140f), new Vector2(0f, -90f));
            var subtitle = UIKit.Label(root, "群れを薙ぎ払い、レア装備を掘れ ── 最大3人協力", 34, TextAnchor.MiddleCenter);
            subtitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1400f, 60f), new Vector2(0f, -220f));
            var cls = UIKit.Label(root, "クラス：戦士（大剣）", 30, TextAnchor.MiddleCenter);
            cls.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(800f, 50f), new Vector2(0f, -290f));

            _menu = UIKit.Rect("Menu", root).Place(new Vector2(0.5f, 0.5f), new Vector2(800f, 520f), new Vector2(0f, -120f)).gameObject;
            var menu = _menu.transform;

            _soloButton = UIKit.Button(menu, "ソロで遊ぶ", 38, () => GameSession.StartSolo());
            _soloButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(560f, 90f), new Vector2(0f, 0f));

            var host = UIKit.Button(menu, "部屋を作る（ホスト）", 38, Host);
            host.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(560f, 90f), new Vector2(0f, -110f));

            _address = CreateInputField(menu, PlayerPrefs.GetString("lastAddress", "192.168."));
            _address.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(330f, 90f), new Vector2(-115f, -220f));
            var join = UIKit.Button(menu, "参加する", 38, () => Join(_address.text));
            join.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(210f, 90f), new Vector2(175f, -220f));

            var bag = UIKit.Button(menu, "バッグ（装備）", 34, () => InventoryView.Instance?.Toggle());
            bag.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(560f, 80f), new Vector2(0f, -340f));

            _waiting = UIKit.Rect("Waiting", root).Place(new Vector2(0.5f, 0.5f), new Vector2(1000f, 520f), new Vector2(0f, -120f)).gameObject;
            _waitingText = UIKit.Label(_waiting.transform, "", 36, TextAnchor.UpperCenter);
            _waitingText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1000f, 240f), Vector2.zero);
            _startButton = UIKit.Button(_waiting.transform, "ゲーム開始", 40, GameSession.StartGame, new Color(0.25f, 0.45f, 0.3f));
            _startButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(480f, 90f), new Vector2(0f, -260f));
            var cancel = UIKit.Button(_waiting.transform, "やめる", 32, () => GameSession.ReturnToLobby());
            cancel.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(300f, 76f), new Vector2(0f, -370f));
            _waiting.SetActive(false);

            _status = UIKit.Label(root, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.5f));
            _status.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1400f, 60f), new Vector2(0f, 60f));

            EventSystem.current?.SetSelectedGameObject(_soloButton.gameObject);
        }

        private static InputField CreateInputField(Transform parent, string initial)
        {
            var bg = UIKit.Panel(parent, "Address", new Color(1f, 1f, 1f, 0.92f));
            var text = UIKit.Label(bg.transform, "", 32, TextAnchor.MiddleLeft, Color.black);
            text.supportRichText = false;
            text.rectTransform.Stretch(14f);
            var placeholder = UIKit.Label(bg.transform, "ホストのIP", 32, TextAnchor.MiddleLeft, new Color(0f, 0f, 0f, 0.4f));
            placeholder.rectTransform.Stretch(14f);
            var field = bg.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = initial;
            return field;
        }

        private void Host()
        {
            if (!GameSession.StartHost())
            {
                _status.text = "部屋を作れませんでした（ポート7777が使用中かもしれません）";
                return;
            }
            _state = State.Hosting;
        }

        private void Join(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            PlayerPrefs.SetString("lastAddress", address);
            if (!GameSession.StartClient(address))
            {
                _status.text = "接続を開始できませんでした";
                return;
            }
            _state = State.Joining;
        }

        private void Update()
        {
            var nm = GameSession.Network;

            if (_autoJoin != null && _state == State.Menu && Time.time >= _nextAutoJoin && !nm.ShutdownInProgress)
            {
                _nextAutoJoin = Time.time + 2f;
                Join(_autoJoin);
            }
            if (_state == State.Joining && !nm.IsClient && !nm.ShutdownInProgress) _state = State.Menu; // connection failed

            _menu.SetActive(_state == State.Menu);
            _waiting.SetActive(_state != State.Menu);
            _startButton.gameObject.SetActive(_state == State.Hosting);

            switch (_state)
            {
                case State.Hosting:
                    int count = nm.ConnectedClientsIds.Count;
                    _waitingText.text = $"部屋を作りました\n\nあなたのIP： <color=#FFD84A>{GameSession.LocalAddress()}</color>\n" +
                                        $"同じWi-Fiの仲間はこのIPで参加できます\n\n参加者 {count} / {GameSession.MaxPlayers}";
                    if (_autoHostPlayers > 0 && count >= _autoHostPlayers) GameSession.StartGame();
                    break;
                case State.Joining:
                    _waitingText.text = nm.IsConnectedClient
                        ? "接続しました！\nホストがゲームを開始するのを待っています…"
                        : "接続中…";
                    break;
            }
        }
    }
}
