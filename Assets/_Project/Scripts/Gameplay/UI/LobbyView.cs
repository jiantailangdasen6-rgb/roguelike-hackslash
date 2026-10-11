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
        private GameObject _statusBar;
        private Text _waitingText;
        private Button _startButton;
        private Button _inviteButton;
        private Text _inviteLabel;
        private float _inviteCopiedUntil;
        private Button _soloButton;
        private Text _shardsText;
        private Text _detailName;
        private Text _detailBody;
        private UISpriteAnimator _detailPreview;
        private RectTransform _trinketList;
        private RectTransform _masteryList;
        private Text _masteryTitle;
        private readonly List<(ClassId Id, Image Frame)> _classCards = new();
        private readonly List<(ClassId Id, UISpriteAnimator Preview)> _classPreviews = new();
        private readonly List<(Difficulty Id, Button Button)> _difficultyButtons = new();
        private Text _difficultyText;
        private RectTransform _skinRow;
        private Text _skinName;
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
            // Opened from an invite link (?join=CODE): fill in the code, let them pick a class, then join.
            string invited = CommandLine.GetValue("-join");
            if (!string.IsNullOrEmpty(invited))
            {
                _code.text = invited.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(_status.text)) _status.text = $"招待されました（ルームコード {_code.text}）　クラスを選んで「コードで参加」を押そう";
            }
            if (CommandLine.Has("-openvault")) VaultView.Open(); // screenshots of the stash
            // Failed connections retry (the host may not be up yet), but a refusal is final.
            if (GameSession.Refused) yield break;
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

            var title = UIKit.Title(root, "LABYRINTH SURVIVOR", 96);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1200f, 140f), new Vector2(0f, -36f));
            var sub = UIKit.Label(root, "ラビリンス・サバイバー　─　仲間と挑む、闇の迷宮", 28, TextAnchor.MiddleCenter, UIKit.MutedText);
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
                var anim = preview.gameObject.AddComponent<UISpriteAnimator>();
                anim.Play(SkinCatalog.Sheet(def.Id, Profile.SkinFor(def.Id)), "idle");
                _classPreviews.Add((def.Id, anim));

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
            _detailName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 70f), new Vector2(220f, -20f));
            _detailBody = UIKit.Label(detail.transform, "", 24, TextAnchor.UpperLeft);
            _detailBody.rectTransform.Place(new Vector2(0f, 1f), new Vector2(600f, 330f), new Vector2(22f, -206f));
            var masteryButton = UIKit.Button(detail.transform, "熟練（ラン外の成長）", 26, () => ToggleMastery(true), UIKit.BloodColor);
            masteryButton.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(300f, 56f), new Vector2(-16f, 14f));
            // Gear kept between runs (the walkable camp will open this from its stash chest instead).
            var vaultButton = UIKit.Button(detail.transform, "装備・倉庫", 26, VaultView.Open);
            vaultButton.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(260f, 56f), new Vector2(16f, 14f));

            // Skins: four looks per class, bought with soul shards.
            _skinRow = UIKit.Rect("Skins", detail.transform).Place(new Vector2(0f, 1f), new Vector2(400f, 80f), new Vector2(220f, -88f));
            _skinName = UIKit.Label(detail.transform, "", 20, TextAnchor.UpperLeft, UIKit.MutedText);
            _skinName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 28f), new Vector2(222f, -170f));

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

            // Difficulty (the host's choice applies to everyone in the room).
            var diffLabel = UIKit.Title(menu, "難易度", 28, TextAnchor.MiddleLeft);
            diffLabel.rectTransform.Place(new Vector2(0f, 0f), new Vector2(120f, 50f), new Vector2(70f, 252f));
            for (int i = 0; i < DifficultyCatalog.All.Length; i++)
            {
                var d = DifficultyCatalog.All[i].Id;
                var b = UIKit.Button(menu, DifficultyCatalog.All[i].Name, 24, () => SelectDifficulty(d));
                b.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(104f, 50f), new Vector2(180f + i * 110f, 252f));
                _difficultyButtons.Add((d, b));
            }
            _difficultyText = UIKit.Label(menu, "", 20, TextAnchor.UpperLeft, UIKit.MutedText);
            _difficultyText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(560f, 56f), new Vector2(70f, 190f));

            // Status line on a dark strip over the subtitle, so long messages (refusals) stay readable.
            var statusBar = UIKit.Panel(root, "StatusBar", new Color(0.06f, 0.02f, 0.03f, 0.92f));
            statusBar.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1500f, 46f), new Vector2(0f, -176f));
            statusBar.raycastTarget = false;
            _statusBar = statusBar.gameObject;
            _status = UIKit.Label(statusBar.transform, "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.55f));
            _status.rectTransform.Stretch();

            // Build stamp: friends can only play together on the same version.
            var version = UIKit.Label(root, $"ver {GameSession.BuildStamp}", 18, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.25f));
            version.rectTransform.Place(new Vector2(1f, 0f), new Vector2(300f, 30f), new Vector2(-16f, 8f));

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
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1000f, 640f), Vector2.zero);
            _waitingText = UIKit.Label(panel.transform, "", 32, TextAnchor.UpperCenter);
            _waitingText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(940f, 330f), new Vector2(0f, -30f));
            _inviteButton = UIKit.Button(panel.transform, "招待リンクをコピー", 28, CopyInvite);
            _inviteButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(460f, 62f), new Vector2(0f, 216f));
            _inviteLabel = _inviteButton.GetComponentInChildren<Text>();
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

            _detailPreview.Play(SkinCatalog.Sheet(def.Id, Profile.SkinFor(def.Id)), "attack");
            RebuildSkins(def.Id);
            RefreshDifficulty();
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

        private void RebuildSkins(ClassId cls)
        {
            for (int i = _skinRow.childCount - 1; i >= 0; i--) Destroy(_skinRow.GetChild(i).gameObject);
            int selected = Profile.SkinFor(cls);
            foreach (var skin in SkinCatalog.For(cls))
            {
                bool owned = Profile.IsUnlocked(skin.Id);
                bool current = skin.Index == selected;
                var frame = UIKit.Framed(_skinRow, skin.Id, new Color(0.08f, 0.06f, 0.08f, 1f), current ? UIKit.AccentColor : UIKit.GoldTrim * 0.5f);
                frame.Outer().Place(new Vector2(0f, 1f), new Vector2(76f, 76f), new Vector2(skin.Index * 84f, 0f));
                var img = UIKit.Rect("Preview", frame.transform).Stretch(-6f).gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                var anim = img.gameObject.AddComponent<UISpriteAnimator>();
                anim.Play(SkinCatalog.Sheet(cls, skin.Index), "idle");
                if (!owned)
                {
                    img.color = new Color(0.35f, 0.35f, 0.4f, 0.9f);
                    var cost = UIKit.Label(frame.transform, $"{skin.Cost}", 20, TextAnchor.LowerCenter, new Color(0.6f, 0.85f, 1f));
                    cost.rectTransform.Stretch(2f);
                }
                var button = frame.Outer().gameObject.AddComponent<Button>();
                button.targetGraphic = frame.Outer().GetComponent<Image>();
                var s = skin;
                button.onClick.AddListener(() => ChooseSkin(s));
            }
            var cur = SkinCatalog.Get(cls, selected);
            _skinName.text = $"見た目：{cur.Name}";
        }

        private void ChooseSkin(SkinDef skin)
        {
            if (!Profile.IsUnlocked(skin.Id))
            {
                if (!Profile.TryBuy(skin.Id, skin.Cost))
                {
                    _status.text = $"魂の欠片が足りない（「{skin.Name}」は {skin.Cost}）";
                    return;
                }
                _status.text = $"「{skin.Name}」を手に入れた";
            }
            Profile.SetSkin(skin.Class, skin.Index);
            ProfileStore.Save();
            foreach (var (id, preview) in _classPreviews)
                if (id == skin.Class) preview.Play(SkinCatalog.Sheet(id, skin.Index), "idle");
            Refresh();
        }

        private void SelectDifficulty(Difficulty d)
        {
            if (!Profile.IsDifficultyOpen(d))
            {
                _status.text = $"「{DifficultyCatalog.Get(d).Name}」は熟練を踏破すると挑める";
                return;
            }
            Profile.difficulty = d;
            ProfileStore.Save();
            RefreshDifficulty();
        }

        private void RefreshDifficulty()
        {
            if (!Profile.IsDifficultyOpen(Profile.difficulty)) Profile.difficulty = Difficulty.Normal;
            foreach (var (id, button) in _difficultyButtons)
            {
                bool open = Profile.IsDifficultyOpen(id);
                button.GetComponent<Image>().color = id == Profile.difficulty ? UIKit.BloodColor : open ? UIKit.ButtonColor : new Color(0.08f, 0.08f, 0.1f);
                button.GetComponentInChildren<Text>().color = open ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            var def = DifficultyCatalog.Get(Profile.difficulty);
            _difficultyText.text = $"{def.Text}\n<size=17>※ 仲間と遊ぶときは部屋を作った人の難易度になります</size>";
        }

        private void RebuildTrinkets()
        {
            for (int i = _trinketList.childCount - 1; i >= 0; i--) Destroy(_trinketList.GetChild(i).gameObject);
            // Two columns of cards; clicking a card equips/unequips it, or buys it when locked.
            const float cardW = 276f, cardH = 96f, gapX = 8f, gapY = 7f;
            int slot = 0;
            foreach (var t in MetaCatalog.Trinkets)
            {
                bool unlocked = Profile.IsUnlocked(t.Id);
                bool equipped = unlocked && Profile.equippedTrinkets.Contains(t.Id);
                bool affordable = unlocked || Profile.shards >= t.Cost;
                var item = UIKit.Framed(_trinketList, t.Id, equipped ? new Color(0.2f, 0.13f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.06f, 1f),
                    equipped ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                var outer = item.Outer();
                outer.Place(new Vector2(0f, 1f), new Vector2(cardW, cardH), new Vector2((slot % 2) * (cardW + gapX), -(slot / 2) * (cardH + gapY)));

                var icon = UIKit.Rect("Icon", item.transform).Place(new Vector2(0f, 1f), new Vector2(48f, 48f), new Vector2(6f, -6f)).gameObject.AddComponent<Image>();
                icon.sprite = GameIcons.Trinket(t.Id);
                icon.enabled = icon.sprite != null;
                icon.raycastTarget = false;
                if (!unlocked) icon.color = new Color(0.4f, 0.4f, 0.45f, 0.8f);

                var name = UIKit.Label(item.transform, $"<b>{t.Name}</b>", 21, TextAnchor.MiddleLeft, unlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(cardW - 64f, 30f), new Vector2(60f, -4f));
                string state = equipped ? $"<color=#e6b85a>装備中</color>" : unlocked ? "<color=#8f877d>装備できる</color>"
                    : affordable ? $"<color=#9fd8ff>解放 {t.Cost}</color>" : $"<color=#6f7f90>解放 {t.Cost}</color>";
                var tag = UIKit.Label(item.transform, state, 17, TextAnchor.MiddleLeft);
                tag.rectTransform.Place(new Vector2(0f, 1f), new Vector2(cardW - 64f, 22f), new Vector2(60f, -32f));
                var text = UIKit.Label(item.transform, $"<color=#bfb6aa>{t.Text}</color>", 16, TextAnchor.UpperLeft);
                text.rectTransform.Place(new Vector2(0f, 1f), new Vector2(cardW - 14f, 40f), new Vector2(8f, -55f));

                var id = t.Id;
                var cost = t.Cost;
                var button = outer.gameObject.AddComponent<Button>();
                button.targetGraphic = outer.GetComponent<Image>();
                var colors = button.colors;
                colors.highlightedColor = new Color(1.6f, 1.5f, 1.2f);
                colors.disabledColor = Color.white;
                button.colors = colors;
                button.onClick.AddListener(() =>
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
                });
                button.interactable = affordable;
                slot++;
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
            Debug.Log($"[Lobby] Joining {address}");
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

        private void CopyInvite()
        {
            if (GameSession.JoinCode == null) return;
            BrowserBridge.CopyText(BrowserBridge.InviteLink(GameSession.JoinCode));
            _inviteCopiedUntil = Time.time + 3f;
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

            _statusBar.SetActive(!string.IsNullOrEmpty(_status.text));
            _menu.SetActive(_state == State.Menu);
            _waiting.SetActive(_state != State.Menu);
            _startButton.gameObject.SetActive(_state == State.Hosting);
            _inviteButton.gameObject.SetActive(_state == State.Hosting && GameSession.JoinCode != null);
            _inviteLabel.text = Time.time < _inviteCopiedUntil ? "コピーしました！ 仲間に送ろう" : "招待リンクをコピー";

            switch (_state)
            {
                case State.Hosting:
                {
                    var names = nm.ConnectedClientsIds.Select(id => ClassCatalog.Get(GameSession.LoadoutFor(id).classId).Name);
                    int count = nm.ConnectedClientsIds.Count;
                    string invite = GameSession.JoinCode != null
                        ? $"ルームコード：<size=56><color=#e0b45a>{GameSession.JoinCode}</color></size>\n<size=24><color=#bfb6aa>招待リンクを送るか、コードを伝えて「コードで参加」してもらおう</color></size>" +
                          (GameSession.IsBrowser ? "\n<size=22><color=#ff9a80>※ 遊んでいる間はこのタブを前面に（裏に回すと全員が一時停止します）</color></size>" : "")
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
