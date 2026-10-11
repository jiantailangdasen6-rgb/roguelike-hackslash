using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Title screen: play alone, make a room for friends (room code / invite link), join one, or (desktop) LAN.
    /// Every choice leads to the camp (<see cref="CampDirector"/>), where the party prepares together.
    /// Command line for automated tests: -autosolo | -autohost &lt;players&gt; | -autojoin &lt;ip&gt; |
    /// -onlinehost &lt;players&gt; -codefile &lt;path&gt; | -onlinejoinfile &lt;path&gt; | -onlinejoin &lt;code&gt; [-class name].
    /// </summary>
    public sealed class LobbyView : MonoBehaviour
    {
        private InputField _address;
        private InputField _code;
        private bool _busy;
        private bool _joining;
        private string _autoJoinCodeFile;
        private Text _status;
        private GameObject _statusBar;
        private Button _soloButton;
        private string _autoJoin;
        private float _nextAutoJoin;

        private static ProfileData Profile => ProfileStore.Current;

        private void Awake()
        {
            GameSession.EnsureNetworkManager();
            BuildUi();
            _status.text = GameSession.LobbyMessage ?? "";
            // Dropped out of a room: its code is ready, so "コードで参加" puts them back in (with their character).
            if (!string.IsNullOrEmpty(GameSession.LastJoinCode))
            {
                _code.text = GameSession.LastJoinCode;
                if (GameSession.LobbyMessage != null && !GameSession.Refused) _status.text += "　―　「コードで参加」で戻れます";
            }
            GameSession.LobbyMessage = null;
            if (BagView.LastBankSummary != null)
            {
                _status.text = string.IsNullOrEmpty(_status.text) ? BagView.LastBankSummary : _status.text + "　―　" + BagView.LastBankSummary;
                BagView.LastBankSummary = null;
            }
        }

        private System.Collections.IEnumerator Start()
        {
            // Netcode registers its message types after the first scene's Awake; starting a session earlier fails in builds.
            yield return null;
            // Opened from an invite link (?join=CODE): fill in the code and join.
            string invited = CommandLine.GetValue("-join");
            if (!string.IsNullOrEmpty(invited))
            {
                _code.text = invited.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(_status.text)) _status.text = $"招待されました（ルームコード {_code.text}）　「コードで参加」を押そう";
            }
            if (CommandLine.Has("-openvault")) VaultView.Open(); // screenshots of the stash
            // Failed connections retry (the host may not be up yet), but a refusal is final.
            if (GameSession.Refused) yield break;
            if (CommandLine.Has("-autosolo")) GameSession.StartSolo();
            else if (CommandLine.Has("-autohost")) HostLan();
            else if (CommandLine.Has("-autojoin")) _autoJoin = CommandLine.GetValue("-autojoin");
            else if (CommandLine.Has("-onlinehost")) HostOnline();
            else if (CommandLine.Has("-onlinejoinfile")) _autoJoinCodeFile = CommandLine.GetValue("-onlinejoinfile");
            else if (CommandLine.GetValue("-onlinejoin") != null) JoinOnline(CommandLine.GetValue("-onlinejoin"));
        }

        // ------------------------------------------------------------------ Layout

        private void BuildUi()
        {
            var root = UIKit.CreateCanvas("Lobby", 10, transform).transform;
            var bg = UIKit.Panel(root, "Background", new Color(0.035f, 0.03f, 0.04f));
            bg.rectTransform.Stretch();

            var title = UIKit.Title(root, "LABYRINTH SURVIVOR", 110);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1400f, 160f), new Vector2(0f, -120f));
            var sub = UIKit.Label(root, "ラビリンス・サバイバー　─　仲間と挑む、闇の迷宮", 30, TextAnchor.MiddleCenter, UIKit.MutedText);
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1200f, 50f), new Vector2(0f, -280f));

            // The heroes this player has, idling side by side.
            var heroes = System.Array.FindAll(ClassCatalog.All, c => Profile.IsClassOpen(c.Id));
            for (int i = 0; i < heroes.Length; i++)
            {
                var def = heroes[i];
                var rect = UIKit.Rect("Hero", root).Place(new Vector2(0.5f, 0.5f), new Vector2(220f, 220f),
                    new Vector2((i - (heroes.Length - 1) * 0.5f) * 190f, 90f));
                var img = rect.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                rect.gameObject.AddComponent<UISpriteAnimator>().Play(SkinCatalog.Sheet(def.Id, Profile.SkinFor(def.Id)), "idle");
            }
            var shards = UIKit.Label(root, $"魂の欠片 {Profile.shards}", 24, TextAnchor.MiddleCenter, new Color(0.6f, 0.85f, 1f));
            shards.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(600f, 36f), new Vector2(0f, -40f));

            _soloButton = UIKit.Button(root, "ひとりで遊ぶ", 36, () => GameSession.StartSolo(), UIKit.BloodColor);
            _soloButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(380f, 90f), new Vector2(-210f, 250f));
            var hostOnline = UIKit.Button(root, "仲間を誘う（部屋を作る）", 30, HostOnline);
            hostOnline.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(380f, 90f), new Vector2(210f, 250f));
            _code = CreateInputField(root, "", "ルームコード");
            _code.characterLimit = 8;
            _code.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(380f, 80f), new Vector2(-210f, 150f));
            var joinOnline = UIKit.Button(root, "コードで参加", 30, () => JoinOnline(_code.text));
            joinOnline.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(380f, 80f), new Vector2(210f, 150f));

            if (!GameSession.IsBrowser)
            {
                var lan = UIKit.Label(root, "同じWi-Fi（LAN）：", 22, TextAnchor.MiddleRight, UIKit.MutedText);
                lan.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(260f, 50f), new Vector2(-330f, 60f));
                var host = UIKit.Button(root, "LANで部屋を作る", 22, HostLan);
                host.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(230f, 50f), new Vector2(-80f, 60f));
                _address = CreateInputField(root, PlayerPrefs.GetString("lastAddress", "192.168."), "ホストのIP");
                _address.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(240f, 50f), new Vector2(170f, 60f));
                var join = UIKit.Button(root, "IPで参加", 22, () => JoinLan(_address.text));
                join.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(160f, 50f), new Vector2(380f, 60f));
            }

            // Status line on a dark strip, so long messages (refusals) stay readable.
            var statusBar = UIKit.Panel(root, "StatusBar", new Color(0.06f, 0.02f, 0.03f, 0.92f));
            statusBar.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1500f, 46f), new Vector2(0f, -340f));
            statusBar.raycastTarget = false;
            _statusBar = statusBar.gameObject;
            _status = UIKit.Label(statusBar.transform, "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.55f));
            _status.rectTransform.Stretch();

            // Build stamp: friends can only play together on the same version.
            var version = UIKit.Label(root, $"ver {GameSession.BuildStamp}", 18, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.25f));
            version.rectTransform.Place(new Vector2(1f, 0f), new Vector2(300f, 30f), new Vector2(-16f, 8f));

            EventSystem.current?.SetSelectedGameObject(_soloButton.gameObject);
        }

        private static InputField CreateInputField(Transform parent, string initial, string hint)
        {
            var bg = UIKit.Framed(parent, "Address", new Color(0.12f, 0.1f, 0.1f, 1f));
            var text = UIKit.Label(bg.transform, "", 30, TextAnchor.MiddleLeft, Color.white);
            text.supportRichText = false;
            text.rectTransform.Stretch(14f);
            var placeholder = UIKit.Label(bg.transform, hint, 26, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.35f));
            placeholder.rectTransform.Stretch(14f);
            var field = bg.Outer().gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = initial;
            return field;
        }

        // ------------------------------------------------------------------ Session

        private void HostLan()
        {
            if (!GameSession.StartLanHost())
                _status.text = "部屋を作れませんでした（ポート7777が使用中かもしれません）";
        }

        private void JoinLan(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            Debug.Log($"[Lobby] Joining {address}");
            PlayerPrefs.SetString("lastAddress", address);
            if (!GameSession.StartLanClient(address))
            {
                _status.text = "接続を開始できませんでした";
                return;
            }
            _joining = true;
            _status.text = "接続しています…";
        }

        private async void HostOnline()
        {
            if (_busy) return;
            _busy = true;
            _status.text = "部屋を準備しています…";
            try
            {
                string code = await GameSession.StartOnlineHostAsync();
                string codeFile = CommandLine.GetValue("-codefile");
                if (codeFile != null) System.IO.File.WriteAllText(codeFile, code);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Lobby] Online host failed: {ex}");
                _status.text = $"部屋を作れませんでした：{ex.Message}";
                if (GameSession.Network.IsListening) GameSession.Network.Shutdown();
            }
            finally
            {
                _busy = false;
            }
        }

        private async void JoinOnline(string code)
        {
            if (_busy || string.IsNullOrWhiteSpace(code)) return;
            _busy = true;
            _status.text = "接続しています…";
            try
            {
                await GameSession.StartOnlineClientAsync(code);
                _joining = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Lobby] Online join failed: {ex}");
                _status.text = "参加できませんでした。コードを確かめてください";
                if (GameSession.Network.IsClient) GameSession.Network.Shutdown();
            }
            finally
            {
                _busy = false;
            }
        }

        private void Update()
        {
            var nm = GameSession.Network;
            bool idle = !_joining && !_busy && !nm.IsListening && !nm.ShutdownInProgress;

            if (_autoJoin != null && idle && Time.time >= _nextAutoJoin)
            {
                _nextAutoJoin = Time.time + 2f;
                JoinLan(_autoJoin);
            }
            // Automated online test: join with the code the host instance wrote to a file.
            if (_autoJoinCodeFile != null && idle && Time.time >= _nextAutoJoin)
            {
                _nextAutoJoin = Time.time + 3f;
                if (System.IO.File.Exists(_autoJoinCodeFile)) JoinOnline(System.IO.File.ReadAllText(_autoJoinCodeFile));
            }
            // A failed connection attempt ends without a scene change: allow trying again.
            if (_joining && !nm.IsClient && !nm.ShutdownInProgress)
            {
                _joining = false;
                if (_status.text == "接続しています…") _status.text = "接続できませんでした";
            }
            if (_joining && nm.IsConnectedClient) _status.text = "接続しました。拠点へ向かっています…";

            _statusBar.SetActive(!string.IsNullOrEmpty(_status.text));
        }
    }
}
