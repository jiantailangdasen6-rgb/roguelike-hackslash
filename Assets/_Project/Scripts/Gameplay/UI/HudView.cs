using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// In-run HUD: level/XP, own HP/shield, chosen perks and skill cooldowns, teammate frames, room objective,
    /// boss bar, banners and one-time tips. Teammate markers in the world are drawn by <see cref="AllyOverlay"/>.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public static HudView Instance { get; private set; }

        public static readonly string[] AreaNames = { "朽ちた納骨堂", "血塗られた礼拝堂", "奈落の玉座" };

        private sealed class SkillSlot
        {
            public Text Key;
            public Text Name;
            public Image Shade;
            public Text Time;
        }

        private sealed class TeamFrame
        {
            public GameObject Root;
            public Image Edge;
            public Text Name;
            public UIKit.Bar Hp;
            public UIKit.Bar Shield;
            public Text HpText;
            public Text Ping;
        }

        private UIKit.Bar _xpBar;
        private Text _levelText;
        private Text _roomText;
        private Text _objectiveText;
        private Text _classText;
        private UIKit.Bar _hpBar;
        private UIKit.Bar _shieldBar;
        private Text _hpText;
        private Text _timeText;
        private Text _difficultyText;
        private UIKit.Bar _bossBar;
        private GameObject _bossRoot;
        private Text _bossName;
        private Text _trialText;
        private MinimapView _minimap;
        // The big map (M) and the short black fade when walking through a door.
        private GameObject _mapRoot;
        private MinimapView _bigMap;
        private Text _mapTitle;
        private Image _fade;
        private float _fadeStart = -10f;
        private const float FadeTime = 0.28f;
        private int _shownRoom = -1;
        private (ulong, int) _shownMap;
        private readonly HashSet<(int Area, int Room)> _roomsBannered = new();
        private Text _centerText;
        private Text _bannerText;
        private Text _bannerSub;
        private float _bannerUntil;
        private Text _toastText;
        private Text _hintText;
        private RectTransform _perkRow;
        private int _perksShown = -1;
        private readonly SkillSlot[] _skills = new SkillSlot[4];
        private readonly TeamFrame[] _team = new TeamFrame[GameSession.MaxPlayers - 1];
        private readonly List<(string Text, float Until)> _toasts = new();
        private readonly HashSet<string> _tipsShown = new();
        private readonly StringBuilder _sb = new();
        private ClassId? _shownClass;
        private CanvasGroup _curtain;
        private Text _curtainText;

        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<PingView>();
            var root = UIKit.CreateCanvas("HUD", 20, transform).transform;

            _xpBar = UIKit.CreateBar(root, "XpBar", new Color(0.45f, 0.75f, 1f), new Color(0f, 0f, 0f, 0.7f));
            var xp = _xpBar.Root;
            xp.anchorMin = new Vector2(0f, 1f);
            xp.anchorMax = new Vector2(1f, 1f);
            xp.pivot = new Vector2(0.5f, 1f);
            xp.sizeDelta = new Vector2(0f, 10f);
            xp.anchoredPosition = Vector2.zero;

            // Top-left: own panel (class, level, HP, perks)
            var panel = UIKit.Framed(root, "PlayerPanel", new Color(0.04f, 0.03f, 0.05f, 0.85f));
            panel.Outer().Place(new Vector2(0f, 1f), new Vector2(470f, 132f), new Vector2(20f, -24f));
            _classText = UIKit.Title(panel.transform, "", 30, TextAnchor.UpperLeft);
            _classText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 40f), new Vector2(16f, -8f));
            _levelText = UIKit.Title(panel.transform, "Lv 1", 30, TextAnchor.UpperRight);
            _levelText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(140f, 40f), new Vector2(-16f, -8f));
            _hpBar = UIKit.CreateBar(panel.transform, "Hp", new Color(0.72f, 0.12f, 0.14f), new Color(0.12f, 0.03f, 0.04f));
            _hpBar.Root.Place(new Vector2(0f, 1f), new Vector2(438f, 26f), new Vector2(16f, -54f));
            _shieldBar = UIKit.CreateBar(_hpBar.Root, "Shield", new Color(0.85f, 0.75f, 0.4f, 0.75f), new Color(0f, 0f, 0f, 0f));
            _shieldBar.Root.Stretch();
            _shieldBar.Root.offsetMax = new Vector2(0f, -18f);
            _hpText = UIKit.Label(_hpBar.Root, "", 20, TextAnchor.MiddleCenter);
            _hpText.rectTransform.Stretch();
            _perkRow = UIKit.Rect("Perks", panel.transform).Place(new Vector2(0f, 1f), new Vector2(438f, 36f), new Vector2(16f, -88f));

            // Teammates under it
            for (int i = 0; i < _team.Length; i++) _team[i] = CreateTeamFrame(root, i);

            // Top-right: time, difficulty, room, objective
            _timeText = UIKit.Title(root, "", 30, TextAnchor.UpperRight, UIKit.MutedText);
            _timeText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(240f, 44f), new Vector2(-28f, -22f));
            _difficultyText = UIKit.Label(root, "", 22, TextAnchor.UpperRight, UIKit.MutedText);
            _difficultyText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(900f, 32f), new Vector2(-28f, -64f));
            _minimap = new MinimapView(root, new Vector2(-24f, -98f));
            BuildBigMap();
            float below = -98f - _minimap.Root.sizeDelta.y - 10f;
            _roomText = UIKit.Title(root, "", 26, TextAnchor.UpperRight, UIKit.MutedText);
            _roomText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(700f, 40f), new Vector2(-28f, below));
            _objectiveText = UIKit.Label(root, "", 26, TextAnchor.UpperRight, UIKit.AccentColor);
            _objectiveText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(760f, 80f), new Vector2(-28f, below - 38f));

            // Boss bar
            var boss = UIKit.Framed(root, "Boss", new Color(0.03f, 0.02f, 0.03f, 0.9f));
            boss.Outer().Place(new Vector2(0.5f, 1f), new Vector2(900f, 62f), new Vector2(0f, -28f));
            _bossRoot = boss.Outer().gameObject;
            _bossName = UIKit.Title(boss.transform, "", 24, TextAnchor.UpperCenter);
            _bossName.rectTransform.Stretch(2f);
            _bossBar = UIKit.CreateBar(boss.transform, "BossHp", new Color(0.6f, 0.08f, 0.1f), new Color(0.1f, 0.02f, 0.03f));
            _bossBar.Root.Place(new Vector2(0.5f, 0f), new Vector2(860f, 14f), new Vector2(0f, 8f));
            // What the boss's trial asks of the team (or of you, shut in a cage).
            _trialText = UIKit.Title(root, "", 34, TextAnchor.UpperCenter, new Color(1f, 0.86f, 0.55f));
            _trialText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1200f, 50f), new Vector2(0f, -100f));

            // Bottom: skills
            string[] keys = { "左クリック", "Q", "E", "Space" };
            for (int i = 0; i < 4; i++)
            {
                var slot = UIKit.Framed(root, "Skill" + i, new Color(0.05f, 0.04f, 0.05f, 0.9f));
                slot.Outer().Place(new Vector2(0.5f, 0f), new Vector2(200f, 86f), new Vector2((i - 1.5f) * 214f, 22f));
                var key = UIKit.Label(slot.transform, keys[i], 18, TextAnchor.UpperLeft, UIKit.MutedText);
                key.rectTransform.Stretch(8f);
                var name = UIKit.Label(slot.transform, "", 22, TextAnchor.LowerCenter);
                name.rectTransform.Stretch(8f);
                var shade = UIKit.Panel(slot.transform, "Shade", new Color(0f, 0f, 0f, 0.65f));
                shade.rectTransform.anchorMin = Vector2.zero;
                shade.rectTransform.anchorMax = new Vector2(1f, 0f);
                shade.rectTransform.offsetMin = Vector2.zero;
                shade.rectTransform.offsetMax = Vector2.zero;
                // Seconds left sit in the top-right corner, clear of the skill's name.
                var time = UIKit.Title(slot.transform, "", 28, TextAnchor.UpperRight, new Color(1f, 0.92f, 0.75f));
                time.rectTransform.Stretch(8f);
                time.rectTransform.offsetMax = new Vector2(-10f, -2f);
                _skills[i] = new SkillSlot { Key = key, Name = name, Shade = shade, Time = time };
            }

            _centerText = UIKit.Title(root, "", 38, TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 0.8f));
            _centerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1300f, 160f), new Vector2(0f, 170f));

            _bannerText = UIKit.Title(root, "", 64, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _bannerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 100f), new Vector2(0f, 300f));
            _bannerSub = UIKit.Label(root, "", 28, TextAnchor.MiddleCenter, UIKit.MutedText);
            _bannerSub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 50f), new Vector2(0f, 240f));

            _toastText = UIKit.Label(root, "", 26, TextAnchor.LowerCenter);
            _toastText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1300f, 220f), new Vector2(0f, 130f));

            _hintText = UIKit.Label(root, "", 22, TextAnchor.LowerLeft, UIKit.MutedText);
            _hintText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1100f, 110f), new Vector2(24f, 120f));

            BuildCurtain();
            gameObject.AddComponent<AllyOverlay>();
            gameObject.AddComponent<BagView>();
            gameObject.AddComponent<LootView>();
            gameObject.AddComponent<LegendaryFxView>();
            RunDirector.EventReceived += OnEvent;
        }

        /// <summary>
        /// Covers the screen from the scene load until the local player exists: the host waits for every peer
        /// to finish loading before the run starts, and without a player the camera has nothing to follow.
        /// </summary>
        private void BuildCurtain()
        {
            var canvas = UIKit.CreateCanvas("StartCurtain", 50, transform);
            _curtain = canvas.gameObject.AddComponent<CanvasGroup>();
            _curtain.blocksRaycasts = false;
            UIKit.Panel(canvas.transform, "Background", new Color(0.035f, 0.03f, 0.04f)).rectTransform.Stretch();
            _curtainText = UIKit.Title(canvas.transform, "", 44, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _curtainText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 80f), new Vector2(0f, 20f));
            var sub = UIKit.Label(_curtainText.transform, "全員の準備ができたら始まります", 26, TextAnchor.MiddleCenter, UIKit.MutedText);
            sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 44f), new Vector2(0f, -64f));
        }

        private void UpdateCurtain(RunDirector director, NetPlayer local)
        {
            bool waiting = director == null || !director.IsSpawned || (local == null && director.Phase.Value == RunPhase.Playing);
            float target = waiting ? 1f : 0f;
            _curtain.alpha = waiting ? 1f : Mathf.MoveTowards(_curtain.alpha, target, Time.unscaledDeltaTime * 2.5f);
            _curtain.gameObject.SetActive(_curtain.alpha > 0.001f);
            // The words go at once; only the dark backdrop fades, so they never hang over the arena.
            _curtainText.gameObject.SetActive(waiting);
            if (waiting) _curtainText.text = "迷宮へ向かっています" + new string('・', 1 + (int)(Time.unscaledTime * 2f) % 3);
        }

        private TeamFrame CreateTeamFrame(Transform root, int index)
        {
            var frame = UIKit.Framed(root, "Ally" + index, new Color(0.04f, 0.03f, 0.05f, 0.82f));
            frame.Outer().Place(new Vector2(0f, 1f), new Vector2(360f, 58f), new Vector2(20f, -166f - index * 66f));
            var edge = UIKit.Panel(frame.transform, "Edge", Color.white);
            edge.rectTransform.anchorMin = Vector2.zero;
            edge.rectTransform.anchorMax = new Vector2(0f, 1f);
            edge.rectTransform.pivot = new Vector2(0f, 0.5f);
            edge.rectTransform.sizeDelta = new Vector2(6f, 0f);
            var name = UIKit.Label(frame.transform, "", 20, TextAnchor.UpperLeft);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(330f, 26f), new Vector2(16f, -3f));
            var hp = UIKit.CreateBar(frame.transform, "Hp", new Color(0.72f, 0.12f, 0.14f), new Color(0.12f, 0.03f, 0.04f));
            hp.Root.Place(new Vector2(0f, 0f), new Vector2(330f, 18f), new Vector2(16f, 7f));
            var shield = UIKit.CreateBar(hp.Root, "Shield", new Color(0.85f, 0.75f, 0.4f, 0.75f), new Color(0f, 0f, 0f, 0f));
            shield.Root.Stretch();
            shield.Root.offsetMax = new Vector2(0f, -12f);
            var text = UIKit.Label(hp.Root, "", 16, TextAnchor.MiddleCenter);
            text.rectTransform.Stretch();
            var ping = UIKit.Label(frame.transform, "", 16, TextAnchor.UpperRight, UIKit.MutedText);
            ping.rectTransform.Place(new Vector2(1f, 1f), new Vector2(120f, 24f), new Vector2(-12f, -5f));
            frame.Outer().gameObject.SetActive(false);
            return new TeamFrame { Root = frame.Outer().gameObject, Edge = edge, Name = name, Hp = hp, Shield = shield, HpText = text, Ping = ping };
        }

        private void OnDestroy()
        {
            RunDirector.EventReceived -= OnEvent;
            if (Instance == this) Instance = null;
        }

        public void Toast(string text)
        {
            _toasts.Add((text, Time.time + 4.5f));
            if (_toasts.Count > 4) _toasts.RemoveAt(0);
        }

        /// <summary>A short explanation the first time something new shows up in a run.</summary>
        private void Tip(string key, string text)
        {
            if (_tipsShown.Add(key)) Toast($"<color=#e0b45a>◆</color> {text}");
        }

        public void Banner(string text, string sub = "", float seconds = 2.8f)
        {
            _bannerText.text = text;
            _bannerSub.text = sub;
            _bannerUntil = Time.time + seconds;
        }

        /// <summary>How far a run got (result screen): the area, or its lord's hall.</summary>
        public static string RoomTitle(int index)
        {
            int area = Mathf.Min(index / RunSimulation.RoomsPerArea, AreaNames.Length - 1);
            return index % RunSimulation.RoomsPerArea == RunSimulation.RoomsPerArea - 1 ? $"{AreaNames[area]} ─ 主の間" : AreaNames[area];
        }

        public static string RoomTitle(MapRoom room) =>
            $"{AreaNames[Mathf.Clamp(room.Index / RunSimulation.RoomsPerArea, 0, AreaNames.Length - 1)]} ─ {RoomCatalog.Name(room.Kind)}";

        /// <summary>The banner under the boss bar: what to do now (empty when nothing special is going on).</summary>
        private static string TrialText(RunDirector d, NetPlayer local)
        {
            if (local != null && local.Caged.Value) return "<color=#ffd59a>檻に閉じ込められた！ 攻撃して壊せ</color>";
            int progress = d.TrialProgress.Value, done = progress & 0xFF, total = progress >> 8;
            int seconds = Mathf.CeilToInt(d.TrialTimer.Value);
            return d.Trial.Value switch
            {
                BossTrial.BoneWard => $"骨の燭台を壊して加護を破れ　<color=#ffffff>{done}/{total}</color>",
                BossTrial.BloodRitual => $"<color=#ff8a7a>血の紋章の上に立って、すべて灯せ</color>　<color=#ffffff>{done}/{total}　残り {seconds}秒</color>",
                BossTrial.Collapse => $"<color=#c8b0ff>床が崩れる！ 中央へ　{seconds}</color>",
                BossTrial.Phylactery => $"<color=#9ae8ff>魂の器を壊せ！</color>　<color=#ffffff>残り {seconds}秒</color>",
                _ => "",
            };
        }

        private static EnemyDef AreaBoss(int room) =>
            EnemyCatalog.Get(EnemyCatalog.AreaBosses[Mathf.Clamp(room / RunSimulation.RoomsPerArea, 0, EnemyCatalog.AreaBosses.Length - 1)]);

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.RoomStarted:
                {
                    // A new area: its name, then the map takes over.
                    int area = Mathf.Clamp(e.Int / RunSimulation.RoomsPerArea, 0, AreaNames.Length - 1);
                    Banner(AreaNames[area], "扉をくぐって迷宮を探索しよう　主の間は焚き火の間の奥", 3.2f);
                    Tip("map", "右上は地図（M で大きく開く）。入った部屋とその隣が見える。寄り道（祠・泉・精鋭）をするほど強くなる");
                    if (NetPlayer.All.Count > 1) Tip("split", "仲間とは別の部屋を探索できる。倒れた仲間は地図で赤く点滅する。主の扉は全員がそろうと開く");
                    if (NetPlayer.All.Count > 1) Tip("ping", "F キー（中クリック）で合図：敵なら「危ない」、祭壇や扉なら「ここ」、ほかは「来て」。M の地図で部屋をクリックすると「集合」");
                    break;
                }
                case SimEventType.TeamLevelUp:
                    // Between rooms the pick comes right away, so the reminder is only for level-ups mid-fight.
                    Toast(RunDirector.Instance != null && !RunDirector.Instance.LocalFighting
                        ? $"<color=#e0b45a>レベル {e.Int}！</color>"
                        : $"<color=#e0b45a>レベル {e.Int}！</color> 力は戦いが終わってから選べる");
                    break;
                case SimEventType.RoomCleared:
                {
                    var director = RunDirector.Instance;
                    if (director != null && director.LocalRoom.Id == e.Id) Banner("部屋を制圧した", "扉から次の部屋へ", 2f);
                    else Toast($"仲間が{RoomCatalog.Name(director != null ? director.Map[e.Id]?.Kind ?? RoomKind.Combat : RoomKind.Combat)}を制圧した");
                    break;
                }
                case SimEventType.PedestalUsed:
                {
                    var kind = (RoomKind)e.Int;
                    string hex = ColorUtility.ToHtmlStringRGB(RoomCatalog.Color(kind));
                    if (!IsMe(e.ClientId))
                    {
                        Toast($"{NameOf(e.ClientId)}が<color=#{hex}>{RoomCatalog.Name(kind)}</color>を使った");
                        break;
                    }
                    string text = kind switch
                    {
                        RoomKind.Spring => $"最大HP +{e.Value:0}%、傷が癒えた",
                        RoomKind.Whetstone => "持っている力を強化する",
                        RoomKind.CurseAltar => $"力を{RewardCatalog.CursedBoons}つ授かる。代償に「{RewardCatalog.CurseName((CurseKind)Mathf.RoundToInt(e.Value))}」（{RewardCatalog.CurseText((CurseKind)Mathf.RoundToInt(e.Value))}）がこのランの間つく",
                        RoomKind.Merchant => "魂の欠片で1つ買える（全員の取り分から払う）",
                        RoomKind.Treasure => "希少以上の装備を手に入れた（I で袋を開く）",
                        RoomKind.Secret => "古い地図を見つけた（このエリアの全部屋が地図に載った）。装備も手に入れた",
                        _ => "新たな力を授かる",
                    };
                    Toast($"<color=#{hex}>{RoomCatalog.Name(kind)}</color>：{text}");
                    break;
                }
                case SimEventType.DoorOpened:
                    if (e.Int == 0) Banner("主の扉が開いた", "覚悟はいいか", 2.4f);
                    else if (e.Int == 2) Banner("宝物庫の扉が開いた", "", 2f);
                    else if (e.Int == 3) Banner("隠し部屋を見つけた", "", 2.4f);
                    else Banner("奥への道が開いた", NetPlayer.All.Count > 1 ? "全員で光の上に立つと次のエリアへ" : "光の上に立つと次のエリアへ", 3f);
                    break;
                case SimEventType.KeyGained:
                    Banner("鍵を手に入れた", "宝物庫（地図の「錠」）の扉の前に立つと開く", 2.8f);
                    break;
                case SimEventType.PuzzleEnded:
                    if (RunDirector.Instance != null && RunDirector.Instance.LocalRoom.Id == e.Id)
                        Banner(e.Int == 1 ? "仕掛けを解いた" : "しくじった…", e.Int == 1 ? "" : "鍵は手に入らなかった", 2.4f);
                    break;
                case SimEventType.BossSpawned:
                {
                    var def = EnemyCatalog.Get((EnemyType)e.Int);
                    Banner(def.Name, def.Title ?? "", 3.5f);
                    if (def.Type == EnemyType.BoneKing) Tip("pillar", "王の突進を柱にぶつけると、しばらくよろめく");
                    break;
                }
                case SimEventType.SpawnWarning:
                    switch ((EnemyType)e.Int)
                    {
                        case EnemyType.DreadKnight: Tip("armor", "亡者の騎士は鎧で攻撃を防ぐ。凍結・気絶・印で鎧を崩してから叩け"); break;
                        case EnemyType.Bloater: Tip("bloater", "腐れ膨れは近づくと破裂する。離れて倒すか、魔物の群れの中で破裂させろ"); break;
                        case EnemyType.Wraith: Tip("wraith", "怨霊は姿を消して足元に現れる。光る円から離れろ"); break;
                        case EnemyType.Necromancer: Tip("necro", "死霊術師は骸骨を呼び続ける。先に仕留めろ"); break;
                    }
                    break;
                case SimEventType.WaveStarted when e.Int > 1 && RunDirector.Instance != null && RunDirector.Instance.LocalRoom.Id == e.Id:
                    Toast($"第{e.Int}波 襲来！");
                    break;
                case SimEventType.PlayerDowned:
                    if (NetPlayer.Local != null && e.ClientId != NetPlayer.Local.OwnerClientId)
                        Toast($"<color=#ff7060>{NameOf(e.ClientId)}が倒れた！</color> 矢印の先へ。そばに立てば助け起こせる");
                    break;
                case SimEventType.PlayerRevived:
                    if (NetPlayer.Local != null && e.ClientId != NetPlayer.Local.OwnerClientId) Toast($"<color=#9fff9f>{NameOf(e.ClientId)}が立ち上がった</color>");
                    break;
                case SimEventType.PlayerJoined:
                    Toast(IsMe(e.ClientId)
                        ? "<color=#e0b45a>◆</color> 途中参加：これまでのレベルの分だけ力を授かった"
                        : "<color=#9fd8ff>仲間が加わった！</color>");
                    break;
                case SimEventType.PlayerRejoined:
                    Toast(IsMe(e.ClientId) ? "<color=#9fd8ff>冒険に戻った</color>" : "<color=#9fd8ff>仲間が戻ってきた！</color>");
                    break;
                case SimEventType.ItemSalvaged when e.Value < 0f:
                    // Bought from the merchant: the whole team's payout shrinks, so everyone hears about it.
                    Toast($"<color=#f2d173>{(IsMe(e.ClientId) ? "闇商人から買った" : NameOf(e.ClientId) + "が闇商人から買った")}</color>（魂の欠片 {e.Value:0}）");
                    break;
                case SimEventType.PlayerLeft:
                    Toast($"<color=#ff9a80>{NameOf(e.ClientId)}が抜けた</color>（同じルームコードで戻ってこられる）");
                    break;
            }
        }

        /// <summary>"呪い：脆さ×2・渇き　" in violet before the difficulty, or nothing.</summary>
        private static string CurseText(int packed)
        {
            if (packed == 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < RewardCatalog.CurseKinds; i++)
            {
                int n = RewardCatalog.CurseStack(packed, (CurseKind)i);
                if (n > 0) parts.Add(RewardCatalog.CurseName((CurseKind)i) + (n > 1 ? $"×{n}" : ""));
            }
            return $"<color=#d88cff>呪い：{string.Join("・", parts)}</color>　";
        }

        private readonly List<ClassId> _party = new();
        private string _hintKey;

        /// <summary>Controls and teamwork tips at the start of a run, with the combination techniques this party can do.</summary>
        private void UpdateHint()
        {
            _party.Clear();
            foreach (var p in NetPlayer.All) _party.Add(p.Class.Value);
            string key = string.Join(",", _party);
            if (key == _hintKey) return;
            _hintKey = key;
            var text = "WASD 移動　マウス 照準　左クリック 攻撃　Q・E スキル　Space 回避　I 袋　M 地図　F 合図　Esc メニュー\n" +
                       "<color=#9fd8ff>連携：凍った敵は強打で「砕ける」／印の敵は全員の攻撃が通りやすい／倒れた仲間のそばに立つと復活</color>";
            var combos = ComboCatalog.Available(_party);
            for (int i = 0; i < combos.Count && i < 2; i++)
                text += $"\n<color=#ffd27a>連携技「{combos[i].Name}」：{combos[i].How}</color>";
            _hintText.text = text;
        }

        private static bool IsMe(ulong clientId) =>
            Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClientId == clientId;

        private static string NameOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return $"{p.DisplayName}（{p.ClassDef.Name}）";
            return "仲間";
        }

        private void Update()
        {
            var director = RunDirector.Instance;
            var local = NetPlayer.Local;
            UpdateCurtain(director, local);
            if (director == null || !director.IsSpawned) return;

            _xpBar.Fill = director.XpFraction.Value;
            // Banked picks show as stars next to the level until the room is cleared.
            int banked = local != null ? local.Picks.Value : 0;
            _levelText.text = banked > 0 && director.LocalFighting
                ? $"<size=24><color=#e0b45a>★{banked}</color></size>  Lv {director.TeamLevel.Value}"
                : $"Lv {director.TeamLevel.Value}";
            _timeText.text = UIKit.FormatTime(director.Elapsed.Value);
            string ping = local != null && !director.IsServer && local.Ping.Value > 0 ? $"通信 {PingText(local.Ping.Value)}　" : "";
            _difficultyText.text = CurseText(local != null ? local.Curses.Value : 0) + ping + $"難易度：{DifficultyCatalog.Get(director.DifficultyLevel.Value).Name}";
            var here = director.LocalRoom;
            _roomText.text = RoomTitle(here) + (director.Keys.Value > 0 ? $"　<color=#e6c766>鍵 ×{director.Keys.Value}</color>" : "");
            _objectiveText.text = ObjectiveText(director, here);
            _minimap.Update(director);
            UpdateBigMap(director);
            AnnounceRoom(director, here);

            float boss = director.BossHealth.Value;
            UIKit.Toggle(_bossRoot, boss >= 0f);
            if (boss >= 0f)
            {
                _bossBar.Fill = boss;
                var def = EnemyCatalog.Get(director.BossType.Value);
                _bossName.text = boss < 0.5f ? $"{def.Name}  <size=18><color=#ff7060>激昂</color></size>" : def.Name;
            }
            _trialText.text = TrialText(director, local);

            if (local != null)
            {
                var def = local.ClassDef;
                if (_shownClass != def.Id)
                {
                    _shownClass = def.Id;
                    _classText.text = def.Name;
                    _skills[0].Name.text = def.BasicName;
                    _skills[1].Name.text = def.Skill1Name;
                    _skills[2].Name.text = def.Skill2Name;
                    _skills[3].Name.text = "回避";
                }
                float max = Mathf.Max(1f, local.MaxHp.Value);
                _hpBar.Fill = local.Hp.Value / max;
                _shieldBar.Fill = local.Shield.Value / max;
                _hpText.text = local.Shield.Value > 0 ? $"{local.Hp.Value:0} / {max:0}  (+{local.Shield.Value:0})" : $"{local.Hp.Value:0} / {max:0}";
                SetCooldown(_skills[1], local.Skill1Cooldown.Value, def.Skill1Cooldown);
                SetCooldown(_skills[2], local.Skill2Cooldown.Value, def.Skill2Cooldown);
                SetCooldown(_skills[3], local.DodgeCooldown.Value, ClassCatalog.DodgeCooldown);
                SetCooldown(_skills[0], 0f, 1f);
            }

            UpdatePerkRow();
            UpdateTeam(local);

            _hintText.enabled = director.RoomIndex.Value == 0 && director.Elapsed.Value < 30f;
            if (_hintText.enabled) UpdateHint();
            _centerText.text = CenterMessage(director, local);
            // While a choice is on screen, only the cards speak (banners and toasts wait behind them).
            bool choosing = PerkView.IsShowing || (_mapRoot != null && _mapRoot.activeSelf); // the big map hides them too
            _bannerText.enabled = _bannerSub.enabled = !choosing && Time.time < _bannerUntil;
            _toastText.enabled = _centerText.enabled = !choosing;

            _toasts.RemoveAll(t => t.Until < Time.time);
            _sb.Clear();
            foreach (var t in _toasts) _sb.Append(t.Text).Append('\n');
            _toastText.text = _sb.ToString();
        }

        /// <summary>Round trip to the host, coloured by how it will feel (green fine, yellow a little late, red heavy).</summary>
        private static string PingText(ushort ms)
        {
            if (ms == 0) return "";
            string color = ms < 120 ? "8fd68f" : ms < 250 ? "e6c766" : "ff6050";
            return $"<color=#{color}>{ms}ms</color>";
        }

        private void UpdateTeam(NetPlayer local)
        {
            int slot = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == local || slot >= _team.Length) continue;
                var f = _team[slot++];
                UIKit.Toggle(f.Root, true);
                f.Edge.color = p.Color;
                f.Ping.text = p.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? "ホスト" : PingText(p.Ping.Value);
                float max = Mathf.Max(1f, p.MaxHp.Value);
                if (p.Downed.Value)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                    f.Name.text = $"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.DisplayName}</color> {p.ClassDef.Name}　<color=#ff6050>瀕死</color>";
                    f.Hp.Fill = p.ReviveProgress.Value;
                    f.Shield.Fill = 0f;
                    f.HpText.text = p.BeingRevived ? $"復活中 {p.ReviveProgress.Value:P0}" : "助けが必要";
                    f.HpText.color = Color.Lerp(new Color(1f, 0.5f, 0.45f), Color.white, pulse * 0.5f);
                }
                else
                {
                    f.Name.text = $"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.DisplayName}</color> {p.ClassDef.Name}";
                    f.Hp.Fill = p.Hp.Value / max;
                    f.Shield.Fill = p.Shield.Value / max;
                    f.HpText.text = $"{p.Hp.Value:0} / {max:0}";
                    f.HpText.color = Color.white;
                }
            }
            for (; slot < _team.Length; slot++) UIKit.Toggle(_team[slot].Root, false);
        }

        private void UpdatePerkRow()
        {
            var taken = PerkView.Taken;
            if (taken.Count == _perksShown) return;
            _perksShown = taken.Count;
            for (int i = _perkRow.childCount - 1; i >= 0; i--) Destroy(_perkRow.GetChild(i).gameObject);

            // One icon per perk (highest level shown as a small number).
            var levels = new Dictionary<byte, int>();
            var order = new List<PerkOption>();
            foreach (var o in taken)
            {
                if (!o.IsPerk) continue;
                if (!levels.ContainsKey(o.Perk)) order.Add(o);
                levels[o.Perk] = Mathf.Max(levels.TryGetValue(o.Perk, out int l) ? l : 0, o.Level);
            }
            for (int i = 0; i < order.Count && i < 13; i++)
            {
                var rect = UIKit.Rect("Perk", _perkRow).Place(new Vector2(0f, 0.5f), new Vector2(32f, 32f), new Vector2(i * 34f, 0f));
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = GameIcons.Perk(order[i]);
                img.raycastTarget = false;
                int level = levels[order[i].Perk];
                if (level > 1)
                {
                    var lv = UIKit.Label(rect, level.ToString(), 16, TextAnchor.LowerRight, UIKit.AccentColor);
                    lv.rectTransform.Stretch(-2f);
                }
            }
        }

        private static void SetCooldown(SkillSlot slot, float remaining, float max)
        {
            bool cooling = remaining > 0.05f;
            slot.Shade.rectTransform.anchorMax = new Vector2(1f, cooling ? Mathf.Clamp01(remaining / Mathf.Max(0.1f, max)) : 0f);
            slot.Time.text = cooling ? remaining.ToString(remaining < 1f ? "0.0" : "0") : "";
            slot.Name.color = cooling ? new Color(0.62f, 0.6f, 0.58f) : Color.white;
        }

        /// <summary>What to do in the room you stand in.</summary>
        private static string ObjectiveText(RunDirector d, MapRoom room)
        {
            bool team = NetPlayer.All.Count > 1;
            string Gather(string where) =>
                d.GatherHold.Value > 0.05f ? $"{where}…  {d.GatherHold.Value / RunSimulation.ExitHoldTime:P0}" : where;
            switch (room.Kind)
            {
                case RoomKind.Combat:
                case RoomKind.Elite:
                    if (d.IsFighting(room.Id))
                        return $"{(room.Kind == RoomKind.Elite ? "精鋭の群れ" : "魔物の群れ")}  第{Mathf.Max(1, d.WaveOf(room.Id))}波 / {RunSimulation.WaveCountFor(room.Kind, d.Area.Value)}";
                    return d.IsCleared(room.Id) ? "<color=#bfb6aa>制圧済み　扉から次の部屋へ</color>" : "";
                case RoomKind.Boss:
                    if (d.ExitOpen.Value) return Gather(team ? "奥への道（光）の上に全員で立つ" : "奥への道（光）の上に立つ");
                    return d.IsFighting(room.Id) ? $"{EnemyCatalog.Get(d.BossType.Value).Name}を討て" : "";
                case RoomKind.MiniBoss:
                    if (d.IsFighting(room.Id)) return "精鋭の主を倒せ　<color=#e6c766>鍵を落とす</color>";
                    return d.IsCleared(room.Id) ? "<color=#bfb6aa>制圧済み</color>" : "";
                case RoomKind.Puzzle:
                    if (d.IsFighting(room.Id))
                        return room.Puzzle switch
                        {
                            PuzzleKind.Plates => $"押し板をすべて光らせろ　<color=#ffffff>{Mathf.RoundToInt(d.PuzzleValue.Value)}/{RunSimulation.PlateSpots.Length}</color>　<size=20>（踏むとしばらく光る）</size>",
                            PuzzleKind.Thief => $"宝を抱えた小鬼を捕まえろ　<color=#ffffff>残り {Mathf.RoundToInt(d.PuzzleTime.Value)}秒</color>",
                            PuzzleKind.Gauntlet => "罠をくぐり抜けて、中央の押し板を踏め",
                            _ => $"結晶を守れ　<color=#ffffff>残り {Mathf.RoundToInt(d.PuzzleTime.Value)}秒　結晶 {d.PuzzleValue.Value:P0}</color>",
                        };
                    return d.IsCleared(room.Id) ? "<color=#bfb6aa>仕掛けは終わった</color>" : "";
                case RoomKind.Campfire:
                    if (d.BossDoorOpen.Value) return "主の間へ";
                    return Gather(team ? "焚き火で休み、全員で主の扉の前に立つ" : "焚き火で休み、主の扉の前に立つ");
                case RoomKind.Start:
                    return "<color=#bfb6aa>扉をくぐって探索しよう</color>";
                default:
                    bool used = NetPlayer.Local != null && (NetPlayer.Local.UsedPedestals.Value & (1 << room.Id)) != 0;
                    return used ? "<color=#bfb6aa>もう使った</color>" : RoomCatalog.PedestalText(room.Kind);
            }
        }

        /// <summary>The big map (M) is on screen (pings then call everyone to the room clicked).</summary>
        public bool BigMapOpen => _mapRoot != null && _mapRoot.activeSelf;

        /// <summary>The room of the big map under a screen point (null off the rooms or on one nobody knows yet).</summary>
        public MapRoom BigMapRoomAt(Vector2 screenPoint) => BigMapOpen ? _bigMap.RoomUnder(screenPoint) : null;

        private void BuildBigMap()
        {
            var canvas = UIKit.CreateCanvas("BigMap", 30, transform).transform;
            var dim = UIKit.Panel(canvas, "Dim", new Color(0f, 0f, 0f, 0.55f));
            dim.rectTransform.Stretch();
            dim.raycastTarget = false;
            _mapRoot = dim.gameObject;
            _bigMap = new MinimapView(dim.transform, new Vector2(0f, 10f), large: true);
            _mapTitle = UIKit.Title(dim.transform, "", 40, TextAnchor.MiddleCenter);
            _mapTitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 60f), new Vector2(0f, _bigMap.Root.sizeDelta.y * 0.5f + 46f));
            var hint = UIKit.Label(dim.transform, "M で閉じる　部屋をクリックで仲間に「集合！」　白い点＝自分　色の点＝仲間　赤く点滅＝倒れた仲間", 22, TextAnchor.MiddleCenter, UIKit.MutedText);
            hint.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 40f), new Vector2(0f, -_bigMap.Root.sizeDelta.y * 0.5f - 18f));
            _mapRoot.SetActive(CommandLine.Has("-openmap")); // screenshots of the big map

            var fadeCanvas = UIKit.CreateCanvas("RoomFade", 25, transform).transform;
            _fade = UIKit.Panel(fadeCanvas, "Fade", Color.clear);
            _fade.rectTransform.Stretch();
            _fade.raycastTarget = false;
        }

        private void UpdateBigMap(RunDirector d)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            bool toggle = kb != null && kb.mKey.wasPressedThisFrame || pad != null && pad.selectButton.wasPressedThisFrame;
            if (toggle && !PerkView.IsShowing && !PauseMenuView.IsOpen && !BagView.IsOpen) _mapRoot.SetActive(!_mapRoot.activeSelf);
            if (PerkView.IsShowing || PauseMenuView.IsOpen || BagView.IsOpen) _mapRoot.SetActive(false);
            if (_mapRoot.activeSelf)
            {
                _bigMap.Update(d);
                int area = Mathf.Clamp(d.Area.Value, 0, AreaNames.Length - 1);
                string keys = d.Keys.Value > 0 ? $"　<size=28><color=#e6c766>鍵 ×{d.Keys.Value}</color></size>" : "";
                string boss = d.BossDoorOpen.Value ? "" : "　<size=24><color=#bfb6aa>主の扉は全員がそろうと開く</color></size>";
                _mapTitle.text = AreaNames[area] + keys + boss;
            }
            float k = (Time.time - _fadeStart) / FadeTime;
            _fade.color = new Color(0f, 0f, 0f, k >= 1f ? 0f : 1f - k * k);
        }

        /// <summary>The first time you step into a room of this area: its name.</summary>
        private void AnnounceRoom(RunDirector d, MapRoom room)
        {
            var map = (d.MapSeed.Value, d.Area.Value);
            if (room.Id == _shownRoom && map == _shownMap) return;
            if (map == _shownMap && _shownRoom >= 0) _fadeStart = Time.time; // through a door: a blink of darkness
            _shownRoom = room.Id;
            _shownMap = map;
            if (room.Kind == RoomKind.Start || !_roomsBannered.Add((d.Area.Value, room.Id))) return;
            string sub = room.Kind switch
            {
                RoomKind.Combat => "魔物が来る",
                RoomKind.Elite => "精鋭が率いる群れ。倒すと良い装備が出やすい",
                RoomKind.MiniBoss => "精鋭の主とお供。倒すと鍵を落とす",
                RoomKind.Puzzle => room.Puzzle switch
                {
                    PuzzleKind.Plates => "押し板をすべて同時に光らせると鍵が手に入る",
                    PuzzleKind.Thief => "逃げる小鬼を捕まえると鍵と宝が手に入る",
                    PuzzleKind.Gauntlet => "罠の中央の押し板を踏むと鍵が手に入る",
                    _ => "結晶を守り抜くと鍵が手に入る",
                },
                RoomKind.Campfire => "火のそばで傷が癒える・宝箱を壊すと魂",
                RoomKind.Boss => "",
                _ => RoomCatalog.PedestalText(room.Kind),
            };
            if (room.Kind != RoomKind.Boss) Banner(RoomCatalog.Name(room.Kind), sub, 2f);
            foreach (var g in room.Layout.Gimmicks)
            {
                if (g.Kind == GimmickKind.PowderKeg) Tip("keg", "火薬樽は攻撃すると爆発する。魔物の群れを巻き込め（味方も巻き込むので注意）");
                if (g.Kind is GimmickKind.SpikeTrap or GimmickKind.FlameJet) Tip("trap", "罠は魔物にも効く。誘い込んで一網打尽に");
                if (g.Kind == GimmickKind.HealingFont) Tip("font", "癒しの泉：上に立つと周りの仲間ごと回復（部屋に1回）");
            }
        }

        private static string CenterMessage(RunDirector d, NetPlayer local)
        {
            if (d.Phase.Value != RunPhase.Playing) return "";
            if (local == null) return ""; // the start curtain covers this
            if (d.HostAway.Value) return "ホストが画面を離れています\n<size=26>戻ってくるまで一時停止中</size>";
            if (d.HostStalled) return "ホストの応答を待っています…";
            if (local.Downed.Value)
                return local.BeingRevived
                    ? $"仲間が助け起こしている…  {local.ReviveProgress.Value:P0}"
                    : NetPlayer.All.Count > 1 ? "倒れた… 仲間がそばに来れば立ち上がれる" : "倒れた…";
            return "";
        }
    }
}
