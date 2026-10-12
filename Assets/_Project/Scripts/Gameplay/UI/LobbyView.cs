using System.Collections.Generic;
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
                if (GameSession.LobbyMessage != null && !GameSession.Refused && GameSession.Suspended == null) _status.text += "　―　「コードで参加」で戻れます";
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
            // -autoresume (tests): a host whose connection broke opens a new room into the same run.
            if (GameSession.Suspended != null && CommandLine.Has("-autoresume")) ResumeHost();
            else if (CommandLine.Has("-autosolo")) GameSession.StartSolo();
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
            var titleShadow = title.gameObject.AddComponent<Outline>();
            titleShadow.effectColor = new Color(0.25f, 0.06f, 0.02f, 0.9f);
            titleShadow.effectDistance = new Vector2(4f, -4f);
            var sub = UIKit.Label(root, "ラビリンス・サバイバー　─　仲間と挑む、闇の迷宮", 30, TextAnchor.MiddleCenter, UIKit.MutedText);
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1200f, 50f), new Vector2(0f, -280f));
            // Gold rules ending in diamonds on both sides of the subtitle.
            foreach (float side in new[] { -1f, 1f })
            {
                var rule = UIKit.Panel(root, "Rule", UIKit.GoldTrim);
                rule.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(200f, 3f), new Vector2(side * 400f, -304f));
                rule.raycastTarget = false;
                var gem = UIKit.Panel(root, "Gem", UIKit.AccentColor);
                gem.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(12f, 12f), new Vector2(side * 292f, -299f));
                gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                gem.raycastTarget = false;
            }

            // A campfire in the dark with its warm glow; the heroes this player has gather round it, embers rise.
            var warm = UIKit.Panel(root, "Glow", new Color(1f, 0.45f, 0.15f, 0.35f));
            warm.sprite = UIKit.GlowSprite;
            warm.raycastTarget = false;
            warm.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 900f), new Vector2(0f, 40f));
            var core = UIKit.Panel(root, "GlowCore", new Color(1f, 0.62f, 0.25f, 0.4f));
            core.sprite = UIKit.GlowSprite;
            core.raycastTarget = false;
            core.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(620f, 380f), new Vector2(0f, 50f));
            _fireGlow = core;
            var fire = UIKit.Rect("Fire", root).Place(new Vector2(0.5f, 0.5f), new Vector2(170f, 170f), new Vector2(0f, 80f));
            var fireImg = fire.gameObject.AddComponent<Image>();
            fireImg.preserveAspect = true;
            fireImg.raycastTarget = false;
            fire.gameObject.AddComponent<UISpriteAnimator>().Play("camp_fire", "default");
            BuildEmbers(root);
            var heroes = System.Array.FindAll(ClassCatalog.All, c => Profile.IsClassOpen(c.Id));
            for (int i = 0; i < heroes.Length; i++)
            {
                var def = heroes[i];
                // Half on each side of the fire, facing it.
                int half = (heroes.Length + 1) / 2;
                bool left = i < half;
                int k = left ? half - 1 - i : i - half;
                float x = (left ? -1f : 1f) * (170f + k * 175f);
                var rect = UIKit.Rect("Hero", root).Place(new Vector2(0.5f, 0.5f), new Vector2(220f, 220f), new Vector2(x, 90f));
                if (!left) rect.localScale = new Vector3(-1f, 1f, 1f);
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

            // The host of a run cut off by a broken connection: carry on in a new room.
            if (GameSession.Suspended != null)
            {
                var run = GameSession.Suspended.Sim;
                var resume = UIKit.Button(root, $"続きから部屋を作る（エリア{run.Area + 1}・{UIKit.FormatTime(run.Elapsed)}）", 30, ResumeHost, UIKit.BloodColor);
                resume.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(800f, 90f), new Vector2(0f, 360f));
                _resumeButton = resume;
            }

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

            // The codex is open from the title too (what you have met so far).
            gameObject.AddComponent<CodexView>();
            var codex = UIKit.Button(root, "図鑑（B）", 26, () => CodexView.Open());
            codex.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(220f, 60f), new Vector2(-28f, -28f));
            gameObject.AddComponent<SettingsView>();
            var settings = UIKit.Button(root, "設定", 26, SettingsView.Open);
            settings.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(220f, 60f), new Vector2(-28f, -98f));

            // Build stamp: friends can only play together on the same version.
            var version = UIKit.Label(root, $"ver {GameSession.BuildStamp}", 18, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.25f));
            version.rectTransform.Place(new Vector2(1f, 0f), new Vector2(300f, 30f), new Vector2(-16f, 8f));

            EventSystem.current?.SetSelectedGameObject((_resumeButton != null ? _resumeButton : _soloButton).gameObject);
        }

        private Image _fireGlow;
        private readonly List<(RectTransform Rect, Image Image, float Speed, float Phase)> _embers = new();

        /// <summary>Sparks drifting up from the fire (respawn at its base when they fade).</summary>
        private void BuildEmbers(Transform root)
        {
            for (int i = 0; i < 26; i++)
            {
                var img = UIKit.Panel(root, "Ember", new Color(1f, 0.7f, 0.3f, 0f));
                img.raycastTarget = false;
                var rect = img.rectTransform;
                rect.Place(new Vector2(0.5f, 0.5f), new Vector2(5f, 5f), Vector2.zero);
                _embers.Add((rect, img, Random.Range(40f, 110f), Random.value * 10f));
                Respawn(_embers.Count - 1, Random.value);
            }
        }

        private void Respawn(int i, float age)
        {
            var e = _embers[i];
            e.Rect.anchoredPosition = new Vector2(Random.Range(-50f, 50f), 70f + age * 420f);
        }

        private void UpdateFire()
        {
            if (_fireGlow != null) _fireGlow.color = new Color(1f, 0.62f, 0.25f, 0.32f + 0.14f * Mathf.PerlinNoise(Time.time * 3f, 0.3f));
            for (int i = 0; i < _embers.Count; i++)
            {
                var (rect, img, speed, phase) = _embers[i];
                var p = rect.anchoredPosition + new Vector2(Mathf.Sin(Time.time * 1.7f + phase) * 18f * Time.deltaTime, speed * Time.deltaTime);
                rect.anchoredPosition = p;
                float life = Mathf.InverseLerp(70f, 520f, p.y);
                img.color = new Color(1f, Mathf.Lerp(0.8f, 0.35f, life), 0.25f, (1f - life) * 0.85f * (0.6f + 0.4f * Mathf.Sin(Time.time * 9f + phase)));
                if (life >= 1f) Respawn(i, 0f);
            }
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

        private Button _resumeButton;

        /// <summary>A new room (online or LAN, as before) that opens straight into the suspended run.</summary>
        private async void ResumeHost()
        {
            var run = GameSession.Suspended;
            if (run == null || _busy) return;
            if (!run.Online)
            {
                if (!GameSession.StartLanHost(resume: true)) _status.text = "部屋を作れませんでした（ポート7777が使用中かもしれません）";
                return;
            }
            _busy = true;
            _status.text = "部屋を準備しています…";
            try
            {
                string code = await GameSession.StartOnlineHostAsync(resume: true);
                string codeFile = CommandLine.GetValue("-codefile");
                if (codeFile != null) System.IO.File.WriteAllText(codeFile, code);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Lobby] Resume host failed: {ex}");
                _status.text = $"部屋を作れませんでした：{ex.Message}";
                if (GameSession.Network.IsListening) GameSession.Network.Shutdown();
            }
            finally
            {
                _busy = false;
            }
        }

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
            UpdateFire();
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
