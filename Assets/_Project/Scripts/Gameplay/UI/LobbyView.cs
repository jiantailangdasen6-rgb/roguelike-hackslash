using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The camp (拠点): pick a class, equip trinkets, spend soul shards on class mastery, then play solo,
    /// host for friends on the same network, or join by IP.
    /// Command line for automated tests: -autosolo | -autohost &lt;players&gt; | -autojoin &lt;ip&gt; [-class name].
    /// </summary>
    public sealed class LobbyView : MonoBehaviour
    {
        private enum State { Menu, Hosting, Joining }

        private State _state;
        private GameObject _menu;
        private GameObject _waiting;
        private GameObject _mastery;
        private InputField _address;
        private InputField _code;
        private bool _busy;
        private string _autoJoinCodeFile;
        private Text _status;
        private Text _waitingText;
        private Button _startButton;
        private Button _soloButton;
        private Text _shardsText;
        private Text _detailName;
        private Text _detailBody;
        private UISpriteAnimator _detailPreview;
        private RectTransform _trinketList;
        private RectTransform _masteryList;
        private Text _masteryTitle;
        private readonly List<(ClassId Id, Image Frame)> _classCards = new();
        private int _autoHostPlayers;
        private string _autoJoin;
        private float _nextAutoJoin;

        private static ProfileData Profile => ProfileStore.Current;

        private void Awake()
        {
            GameSession.EnsureNetworkManager();
            BuildUi();
            Refresh();
            _status.text = GameSession.LobbyMessage ?? "";
            GameSession.LobbyMessage = null;
        }

        private System.Collections.IEnumerator Start()
        {
            // Netcode registers its message types after the first scene's Awake; starting a session earlier fails in builds.
            yield return null;
            if (CommandLine.Has("-autosolo")) GameSession.StartSolo();
            else if (CommandLine.Has("-autohost"))
            {
                _autoHostPlayers = Mathf.Clamp((int)CommandLine.GetFloat("-autohost", 2), 1, GameSession.MaxPlayers);
                HostLan();
            }
            else if (CommandLine.Has("-autojoin")) _autoJoin = CommandLine.GetValue("-autojoin");
            else if (CommandLine.Has("-onlinehost"))
            {
                _autoHostPlayers = Mathf.Clamp((int)CommandLine.GetFloat("-onlinehost", 2), 1, GameSession.MaxPlayers);
                HostOnline();
            }
            else if (CommandLine.Has("-onlinejoinfile")) _autoJoinCodeFile = CommandLine.GetValue("-onlinejoinfile");
            else if (CommandLine.GetValue("-onlinejoin") != null) JoinOnline(CommandLine.GetValue("-onlinejoin"));
        }

        // ------------------------------------------------------------------ Layout

        private void BuildUi()
        {
            var root = UIKit.CreateCanvas("Lobby", 10, transform).transform;
            var bg = UIKit.Panel(root, "Background", new Color(0.035f, 0.03f, 0.04f));
            bg.rectTransform.Stretch();

            var title = UIKit.Title(root, "冥府の迷宮", 104);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1200f, 140f), new Vector2(0f, -36f));
            var sub = UIKit.Label(root, "仲間と挑む、闇の迷宮（仮タイトル）", 28, TextAnchor.MiddleCenter, UIKit.MutedText);
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1200f, 44f), new Vector2(0f, -170f));

            _menu = UIKit.Rect("Menu", root).Stretch().gameObject;
            var menu = _menu.transform;

            // Left: classes
            var classHeader = UIKit.Title(menu, "クラス", 36, TextAnchor.MiddleLeft);
            classHeader.rectTransform.Place(new Vector2(0f, 1f), new Vector2(500f, 50f), new Vector2(70f, -230f));
            for (int i = 0; i < ClassCatalog.All.Length; i++)
            {
                var def = ClassCatalog.All[i];
                var card = UIKit.Framed(menu, def.Name, new Color(0.06f, 0.05f, 0.07f, 1f));
                card.Outer().Place(new Vector2(0f, 1f), new Vector2(520f, 150f), new Vector2(70f, -290f - i * 166f));
                var button = card.Outer().gameObject.AddComponent<Button>();
                button.targetGraphic = card.Outer().GetComponent<Image>();
                var id = def.Id;
                button.onClick.AddListener(() => SelectClass(id));

                var preview = UIKit.Rect("Preview", card.transform).Place(new Vector2(0f, 0.5f), new Vector2(150f, 150f), new Vector2(0f, 6f));
                var img = preview.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                preview.gameObject.AddComponent<UISpriteAnimator>().Play(def.Sheet, "idle");

                var name = UIKit.Title(card.transform, def.Name, 38, TextAnchor.UpperLeft, Color.white);
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(340f, 50f), new Vector2(160f, -18f));
                var role = UIKit.Label(card.transform, def.Role, 24, TextAnchor.UpperLeft, UIKit.MutedText);
                role.rectTransform.Place(new Vector2(0f, 1f), new Vector2(340f, 40f), new Vector2(160f, -72f));
                _classCards.Add((def.Id, card.Outer().GetComponent<Image>()));
            }

            // Centre: class detail
            var detail = UIKit.Framed(menu, "Detail", new Color(0.05f, 0.04f, 0.06f, 0.95f));
            detail.Outer().Place(new Vector2(0.5f, 1f), new Vector2(640f, 600f), new Vector2(-20f, -230f));
            var detailPreview = UIKit.Rect("Preview", detail.transform).Place(new Vector2(0f, 1f), new Vector2(200f, 200f), new Vector2(10f, 0f));
            var dimg = detailPreview.gameObject.AddComponent<Image>();
            dimg.preserveAspect = true;
            dimg.raycastTarget = false;
            _detailPreview = detailPreview.gameObject.AddComponent<UISpriteAnimator>();
            _detailName = UIKit.Title(detail.transform, "", 54, TextAnchor.UpperLeft, Color.white);
            _detailName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 70f), new Vector2(220f, -30f));
            _detailBody = UIKit.Label(detail.transform, "", 24, TextAnchor.UpperLeft);
            _detailBody.rectTransform.Place(new Vector2(0f, 1f), new Vector2(600f, 400f), new Vector2(22f, -200f));
            var masteryButton = UIKit.Button(detail.transform, "熟練（ラン外の成長）", 28, () => ToggleMastery(true), UIKit.BloodColor);
            masteryButton.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(330f, 64f), new Vector2(-20f, -110f));

            // Right: trinkets
            var trinketHeader = UIKit.Title(menu, "お守り（2つまで）", 36, TextAnchor.MiddleLeft);
            trinketHeader.rectTransform.Place(new Vector2(1f, 1f), new Vector2(560f, 50f), new Vector2(-70f, -230f));
            _shardsText = UIKit.Label(menu, "", 26, TextAnchor.MiddleRight, new Color(0.6f, 0.85f, 1f));
            _shardsText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(560f, 40f), new Vector2(-70f, -236f));
            _trinketList = UIKit.Rect("Trinkets", menu).Place(new Vector2(1f, 1f), new Vector2(560f, 620f), new Vector2(-70f, -290f));

            // Bottom: play. Online = Unity Relay with a room code (works in browsers); LAN = direct IP (desktop only).
            _soloButton = UIKit.Button(menu, "ひとりで挑む", 34, () => GameSession.StartSolo(), UIKit.BloodColor);
            _soloButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(340f, 86f), new Vector2(-570f, 96f));
            var hostOnline = UIKit.Button(menu, "仲間を誘う（部屋を作る）", 30, HostOnline);
            hostOnline.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(420f, 86f), new Vector2(-160f, 96f));
            _code = CreateInputField(menu, "", "ルームコード");
            _code.characterLimit = 8;
            _code.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(270f, 86f), new Vector2(210f, 96f));
            var joinOnline = UIKit.Button(menu, "コードで参加", 30, () => JoinOnline(_code.text));
            joinOnline.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(260f, 86f), new Vector2(490f, 96f));

            if (!GameSession.IsBrowser)
            {
                var lan = UIKit.Label(menu, "同じWi-Fi（LAN）：", 22, TextAnchor.MiddleRight, UIKit.MutedText);
                lan.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(260f, 50f), new Vector2(-330f, 26f));
                var host = UIKit.Button(menu, "LANで部屋を作る", 22, HostLan);
                host.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(230f, 50f), new Vector2(-80f, 26f));
                _address = CreateInputField(menu, PlayerPrefs.GetString("lastAddress", "192.168."), "ホストのIP");
                _address.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(240f, 50f), new Vector2(170f, 26f));
                var join = UIKit.Button(menu, "IPで参加", 22, () => JoinLan(_address.text));
                join.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(160f, 50f), new Vector2(380f, 26f));
            }

            _status = UIKit.Label(root, "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.5f));
            _status.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1400f, 30f), new Vector2(0f, 184f));

            BuildMastery(root);
            BuildWaiting(root);
            EventSystem.current?.SetSelectedGameObject(_soloButton.gameObject);
        }

        private void BuildMastery(Transform root)
        {
            var dim = UIKit.Panel(root, "MasteryDim", new Color(0f, 0f, 0f, 0.75f));
            dim.rectTransform.Stretch();
            _mastery = dim.gameObject;
            var panel = UIKit.Framed(dim.transform, "Mastery");
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1100f, 820f), Vector2.zero);
            _masteryTitle = UIKit.Title(panel.transform, "", 46);
            _masteryTitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1000f, 70f), new Vector2(0f, -20f));
            var hint = UIKit.Label(panel.transform, "魂の欠片で、このクラスの新しい技や成長を解放する（解放した効果は毎ラン有効）", 22, TextAnchor.MiddleCenter, UIKit.MutedText);
            hint.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1000f, 40f), new Vector2(0f, -92f));
            _masteryList = UIKit.Rect("Nodes", panel.transform).Place(new Vector2(0.5f, 1f), new Vector2(1020f, 600f), new Vector2(0f, -140f));
            var close = UIKit.Button(panel.transform, "閉じる", 28, () => ToggleMastery(false));
            close.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(240f, 64f), new Vector2(0f, 20f));
            _mastery.SetActive(false);
        }

        private void BuildWaiting(Transform root)
        {
            var dim = UIKit.Panel(root, "WaitingDim", new Color(0f, 0f, 0f, 0.8f));
            dim.rectTransform.Stretch();
            _waiting = dim.gameObject;
            var panel = UIKit.Framed(dim.transform, "Waiting");
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1000f, 560f), Vector2.zero);
            _waitingText = UIKit.Label(panel.transform, "", 32, TextAnchor.UpperCenter);
            _waitingText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(940f, 330f), new Vector2(0f, -30f));
            _startButton = UIKit.Button(panel.transform, "出発する", 38, GameSession.StartGame, UIKit.BloodColor);
            _startButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(400f, 86f), new Vector2(0f, 120f));
            var cancel = UIKit.Button(panel.transform, "やめる", 30, () => GameSession.ReturnToLobby());
            cancel.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(260f, 66f), new Vector2(0f, 30f));
            _waiting.SetActive(false);
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

        // ------------------------------------------------------------------ Content

        private void SelectClass(ClassId id)
        {
            Profile.selectedClass = id;
            ProfileStore.Save();
            Refresh();
        }

        private void Refresh()
        {
            var def = ClassCatalog.Get(Profile.selectedClass);
            foreach (var (id, frame) in _classCards) frame.color = id == def.Id ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f;

            _detailPreview.Play(def.Sheet, "attack");
            _detailName.text = def.Name;
            var owned = MetaCatalog.NodesFor(def.Id).Where(n => Profile.IsUnlocked(n.Id)).ToList();
            _detailBody.text =
                $"<color=#bfb6aa>{def.Role}</color>\n" +
                $"HP {def.Health:0}（+{def.HealthPerLevel:0}/Lv）　移動 {def.MoveSpeed:0.0}　防御 {def.Armor:0}\n\n" +
                $"<color=#e0b45a>◆{def.BasicName}</color>（通常攻撃・左クリック）\n" +
                $"<color=#e0b45a>◆{def.Skill1Name}</color>（Q）\n<size=22><color=#d8d0c4>{def.Skill1Text}</color></size>\n" +
                $"<color=#e0b45a>◆{def.Skill2Name}</color>（E）\n<size=22><color=#d8d0c4>{def.Skill2Text}</color></size>\n\n" +
                $"<color=#9fd8ff>熟練 {owned.Count}/{MetaCatalog.NodesFor(def.Id).Count()}</color>" +
                (owned.Count > 0 ? "：" + string.Join("、", owned.Select(n => n.Name)) : "");

            _shardsText.text = $"魂の欠片 {Profile.shards}";
            RebuildTrinkets();
            if (_mastery.activeSelf) RebuildMastery();
        }

        private void RebuildTrinkets()
        {
            for (int i = _trinketList.childCount - 1; i >= 0; i--) Destroy(_trinketList.GetChild(i).gameObject);
            int row = 0;
            foreach (var t in MetaCatalog.Trinkets)
            {
                bool unlocked = Profile.IsUnlocked(t.Id);
                bool equipped = unlocked && Profile.equippedTrinkets.Contains(t.Id);
                var item = UIKit.Framed(_trinketList, t.Id, equipped ? new Color(0.16f, 0.1f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.06f, 1f),
                    equipped ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                item.Outer().Place(new Vector2(0f, 1f), new Vector2(560f, 66f), new Vector2(0f, -row * 72f));
                var label = UIKit.Label(item.transform, $"<b>{t.Name}</b>\n<size=20><color=#bfb6aa>{t.Text}</color></size>", 24, TextAnchor.MiddleLeft,
                    unlocked ? Color.white : new Color(1f, 1f, 1f, 0.5f));
                label.rectTransform.Stretch(10f);
                label.rectTransform.offsetMax = new Vector2(-170f, -4f);

                string action = equipped ? "装備中" : unlocked ? "装備する" : $"解放 {t.Cost}";
                var color = equipped ? new Color(0.35f, 0.25f, 0.1f) : unlocked ? UIKit.ButtonColor : new Color(0.1f, 0.15f, 0.25f);
                var id = t.Id;
                var cost = t.Cost;
                var button = UIKit.Button(item.transform, action, 22, () =>
                {
                    if (!Profile.IsUnlocked(id))
                    {
                        if (!Profile.TryBuy(id, cost)) _status.text = "魂の欠片が足りない";
                    }
                    else
                    {
                        Profile.ToggleTrinket(id);
                    }
                    ProfileStore.Save();
                    Refresh();
                }, color);
                button.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(150f, 52f), new Vector2(-8f, 0f));
                button.interactable = unlocked || Profile.shards >= t.Cost;
                row++;
            }
        }

        private void ToggleMastery(bool show)
        {
            _mastery.SetActive(show);
            if (show) RebuildMastery();
        }

        private void RebuildMastery()
        {
            var def = ClassCatalog.Get(Profile.selectedClass);
            _masteryTitle.text = $"{def.Name}の熟練　　<size=30><color=#9fd8ff>魂の欠片 {Profile.shards}</color></size>";
            for (int i = _masteryList.childCount - 1; i >= 0; i--) Destroy(_masteryList.GetChild(i).gameObject);
            int row = 0;
            foreach (var node in MetaCatalog.NodesFor(def.Id))
            {
                bool owned = Profile.IsUnlocked(node.Id);
                var item = UIKit.Framed(_masteryList, node.Id, owned ? new Color(0.14f, 0.09f, 0.05f, 1f) : new Color(0.06f, 0.05f, 0.06f, 1f),
                    owned ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                item.Outer().Place(new Vector2(0f, 1f), new Vector2(1020f, 88f), new Vector2(0f, -row * 98f));
                string kind = node.Kind == MasteryKind.UnlockPerk ? "新しい技" : node.Kind == MasteryKind.StartPerk ? "開始時の加護" : "成長";
                var label = UIKit.Label(item.transform, $"<b>{node.Name}</b>  <size=20><color=#9fd8ff>{kind}</color></size>\n<size=22><color=#bfb6aa>{node.Text}</color></size>", 28, TextAnchor.MiddleLeft);
                label.rectTransform.Stretch(14f);
                label.rectTransform.offsetMax = new Vector2(-220f, -4f);
                var n = node;
                var button = UIKit.Button(item.transform, owned ? "習得済み" : $"習得 {node.Cost}", 24, () =>
                {
                    if (!Profile.TryBuy(n.Id, n.Cost)) _status.text = "魂の欠片が足りない";
                    ProfileStore.Save();
                    Refresh();
                }, owned ? new Color(0.35f, 0.25f, 0.1f) : new Color(0.1f, 0.15f, 0.25f));
                button.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(190f, 60f), new Vector2(-12f, 0f));
                button.interactable = !owned && Profile.shards >= node.Cost;
                row++;
            }
        }

        // ------------------------------------------------------------------ Session

        private void HostLan()
        {
            if (!GameSession.StartLanHost())
            {
                _status.text = "部屋を作れませんでした（ポート7777が使用中かもしれません）";
                return;
            }
            _state = State.Hosting;
        }

        private void JoinLan(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            PlayerPrefs.SetString("lastAddress", address);
            if (!GameSession.StartLanClient(address))
            {
                _status.text = "接続を開始できませんでした";
                return;
            }
            _state = State.Joining;
        }

        private async void HostOnline()
        {
            if (_busy) return;
            _busy = true;
            _status.text = "部屋を準備しています…";
            try
            {
                string code = await GameSession.StartOnlineHostAsync();
                _status.text = "";
                _state = State.Hosting;
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
                _status.text = "";
                _state = State.Joining;
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

            if (_autoJoin != null && _state == State.Menu && Time.time >= _nextAutoJoin && !nm.ShutdownInProgress)
            {
                _nextAutoJoin = Time.time + 2f;
                JoinLan(_autoJoin);
            }
            // Automated online test: join with the code the host instance wrote to a file.
            if (_autoJoinCodeFile != null && _state == State.Menu && !_busy && Time.time >= _nextAutoJoin && !nm.ShutdownInProgress)
            {
                _nextAutoJoin = Time.time + 3f;
                if (System.IO.File.Exists(_autoJoinCodeFile)) JoinOnline(System.IO.File.ReadAllText(_autoJoinCodeFile));
            }
            if (_state == State.Joining && !nm.IsClient && !nm.ShutdownInProgress) _state = State.Menu;

            _menu.SetActive(_state == State.Menu);
            _waiting.SetActive(_state != State.Menu);
            _startButton.gameObject.SetActive(_state == State.Hosting);

            switch (_state)
            {
                case State.Hosting:
                {
                    var names = nm.ConnectedClientsIds.Select(id => ClassCatalog.Get(GameSession.LoadoutFor(id).classId).Name);
                    int count = nm.ConnectedClientsIds.Count;
                    string invite = GameSession.JoinCode != null
                        ? $"ルームコード：<size=56><color=#e0b45a>{GameSession.JoinCode}</color></size>\n<size=24><color=#bfb6aa>仲間にこのコードを伝えて「コードで参加」してもらおう（PC・ブラウザどちらでも）</color></size>"
                        : $"あなたのIP：<color=#e0b45a>{GameSession.LocalAddress()}</color>\n<size=24><color=#bfb6aa>同じWi-Fiの仲間はこのIPで参加できます</color></size>";
                    _waitingText.text = $"部屋を作りました\n\n{invite}\n\n仲間 {count} / {GameSession.MaxPlayers}：{string.Join("・", names)}";
                    if (_autoHostPlayers > 0 && count >= _autoHostPlayers && !GameSession.InGame) GameSession.StartGame();
                    break;
                }
                case State.Joining:
                    _waitingText.text = nm.IsConnectedClient ? "接続しました\n\nホストの出発を待っています…" : "接続中…";
                    break;
            }
        }
    }
}
