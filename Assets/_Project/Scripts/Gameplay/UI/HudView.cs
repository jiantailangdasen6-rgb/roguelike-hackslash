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

        private static readonly string[] LayerNames = { "第一層", "第二層", "第三層" };

        /// <summary>The land an area of this run is in (the classic order outside a run).</summary>
        public static string LandName(int area)
        {
            var d = RunDirector.Instance;
            return BiomeCatalog.Get(d != null && d.IsSpawned ? d.BiomeOf(area) : BiomeCatalog.Classic(area)).Name;
        }

        /// <summary>An area's name: its land, and past the third lord the deep layer too ("深層2・凍てつく霊廟").</summary>
        public static string AreaName(int area) => area < RunSimulation.AreaCount ? LandName(area) : $"深層{area - RunSimulation.AreaCount + 1}・{LandName(area)}";

        public static string LayerName(int area) => area < LayerNames.Length ? LayerNames[Mathf.Max(0, area)] : $"深層 第{area - LayerNames.Length + 1}階";

        private sealed class SkillSlot
        {
            public RectTransform Root;
            public Image Icon;
            public Image Cool;
            public Image Flash;
            public Text Time;
            public Text Key;
            public RectTransform KeyCap;
            public float Last;
            public float FlashAt = -10f;
        }

        private static string[] MouseKeys => new[] { "クリック", GameSettings.Label(KeyAction.Skill1), GameSettings.Label(KeyAction.Skill2), GameSettings.Label(KeyAction.Dodge) };
        private string[] _mouseKeys; // set in Awake (PlayerPrefs can't be read from a field initializer)
        private static readonly string[] PadKeys = { "RT", "RB", "LB", "A" };

        private UIKit.Bar _hpTrail;
        private float _trailValue = 1f, _trailHoldUntil, _lastHp = 1f;
        private Image _portrait;
        private UISpriteAnimator _portraitAnim;
        private string _portraitSheet;

        /// <summary>Bar fills get a glossy band on top.</summary>
        private static void Gloss(RectTransform bar)
        {
            var fill = bar.Find("Fill")?.GetComponent<Image>();
            if (fill != null) fill.sprite = UIKit.GlossSprite;
        }

        /// <summary>The hero in a round gold frame, the level on a tag under it.</summary>
        private void BuildPortrait(Transform root)
        {
            var holder = UIKit.Rect("Portrait", root).Place(new Vector2(0f, 1f), new Vector2(140f, 140f), new Vector2(14f, -18f));
            // The face: a bust cut out by a round mask (the sprite is scaled up so head and shoulders fill it).
            var disc = UIKit.Panel(holder, "Disc", new Color(0.1f, 0.075f, 0.09f, 1f));
            disc.sprite = UIKit.DiscSprite;
            disc.rectTransform.Stretch(4f);
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var glow = UIKit.Panel(disc.transform, "Glow", new Color(1f, 0.68f, 0.32f, 0.3f));
            glow.sprite = UIKit.GlowSprite;
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(110f, 110f), new Vector2(0f, 6f));
            _portrait = UIKit.Panel(disc.transform, "Hero", Color.white);
            _portrait.preserveAspect = true;
            _portrait.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(230f, 230f), new Vector2(10f, -6f));
            _portraitAnim = _portrait.gameObject.AddComponent<UISpriteAnimator>();
            var dark = UIKit.Panel(holder, "RimShadow", new Color(0.03f, 0.02f, 0.02f, 1f));
            dark.sprite = UIKit.ThinRingSprite;
            dark.rectTransform.Stretch(-2f);
            var ring = UIKit.Panel(holder, "Rim", new Color(0.88f, 0.7f, 0.34f, 1f));
            ring.sprite = UIKit.ThinRingSprite;
            ring.rectTransform.Stretch(2f);
            foreach (var img in new[] { disc, glow, _portrait, dark, ring }) img.raycastTarget = false;
            var tag = UIKit.Framed(holder, "Level", new Color(0.32f, 0.08f, 0.08f, 1f));
            tag.Outer().Place(new Vector2(0.5f, 0f), new Vector2(84f, 36f), new Vector2(0f, -12f));
            _levelText = UIKit.Title(tag.transform, "Lv 1", 24, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));
            _levelText.rectTransform.Stretch();
        }

        private SkillSlot CreateSkillSlot(Transform root, int i)
        {
            var slot = UIKit.Framed(root, "Skill" + i, new Color(0.03f, 0.025f, 0.03f, 0.95f));
            var rect = slot.Outer();
            rect.Place(new Vector2(0.5f, 0f), new Vector2(104f, 104f), new Vector2((i - 1.5f) * 126f, 40f));
            var icon = UIKit.Panel(slot.transform, "Icon", Color.white);
            icon.rectTransform.Stretch(4f);
            icon.raycastTarget = false;
            // The wait drains clockwise from the top like a clock hand.
            var cool = UIKit.Panel(slot.transform, "Cool", new Color(0f, 0f, 0f, 0.68f));
            cool.sprite = UIKit.WhiteSprite;
            cool.type = Image.Type.Filled;
            cool.fillMethod = Image.FillMethod.Radial360;
            cool.fillOrigin = (int)Image.Origin360.Top;
            cool.fillClockwise = false;
            cool.fillAmount = 0f;
            cool.rectTransform.Stretch(4f);
            cool.raycastTarget = false;
            var flash = UIKit.Panel(slot.transform, "Flash", new Color(1f, 0.92f, 0.6f, 0f));
            flash.rectTransform.Stretch(2f);
            flash.raycastTarget = false;
            var time = UIKit.Title(slot.transform, "", 40, TextAnchor.MiddleCenter, Color.white);
            time.rectTransform.Stretch();
            var outline = time.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            // A key cap sitting on the bottom edge.
            var capRect = UIKit.KeyCap(rect, MouseKeys[i], i == 0 || i == 3 ? 17 : 20, out var key);
            capRect.Place(new Vector2(0.5f, 0f), new Vector2(i == 0 || i == 3 ? 86f : 44f, 30f), new Vector2(0f, -18f));
            return new SkillSlot { Root = rect, Icon = icon, Cool = cool, Flash = flash, Time = time, Key = key, KeyCap = capRect };
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
            public UISpriteAnimator Face;
            public string FaceSheet;
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
        private readonly (RectTransform Root, Text Text, CanvasGroup Group)[] _toastPlates = new (RectTransform, Text, CanvasGroup)[4];
        private bool _toastsHidden;
        private RectTransform _perkRow;
        private int _perksShown = -1;
        private readonly SkillSlot[] _skills = new SkillSlot[4];
        private readonly TeamFrame[] _team = new TeamFrame[GameSession.MaxPlayers - 1];
        private readonly List<(string Text, float Until)> _toasts = new();
        private GuideHints _guide;
        private Cinematics _cinematics;
        private readonly StringBuilder _sb = new();
        private ClassId? _shownClass;
        private CanvasGroup _curtain;
        private Text _curtainText;

        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<PingView>();
            _mouseKeys = MouseKeys;
            var root = UIKit.CreateCanvas("HUD", 20, transform).transform;

            _xpBar = UIKit.CreateBar(root, "XpBar", new Color(0.45f, 0.75f, 1f), new Color(0f, 0f, 0f, 0.7f));
            var xp = _xpBar.Root;
            xp.anchorMin = new Vector2(0f, 1f);
            xp.anchorMax = new Vector2(1f, 1f);
            xp.pivot = new Vector2(0.5f, 1f);
            xp.sizeDelta = new Vector2(0f, 10f);
            xp.anchoredPosition = Vector2.zero;

            // Top-left: own panel. A round portrait of the hero with the level on a tag, the class name, a glossy HP bar
            // (lost health trails off behind it) and the perks taken.
            var panel = UIKit.Framed(root, "PlayerPanel", new Color(0.04f, 0.03f, 0.05f, 0.88f));
            panel.Outer().Place(new Vector2(0f, 1f), new Vector2(430f, 118f), new Vector2(96f, -30f));
            _classText = UIKit.Title(panel.transform, "", 28, TextAnchor.UpperLeft);
            _classText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 38f), new Vector2(64f, -6f));
            _hpTrail = UIKit.CreateBar(panel.transform, "HpTrail", new Color(1f, 0.85f, 0.6f, 0.9f), new Color(0.1f, 0.025f, 0.035f));
            _hpTrail.Root.Place(new Vector2(0f, 1f), new Vector2(350f, 28f), new Vector2(64f, -46f));
            _hpBar = UIKit.CreateBar(_hpTrail.Root, "Hp", new Color(0.82f, 0.14f, 0.16f), Color.clear);
            _hpBar.Root.Stretch();
            Gloss(_hpBar.Root);
            _shieldBar = UIKit.CreateBar(_hpTrail.Root, "Shield", new Color(0.95f, 0.82f, 0.45f, 0.8f), new Color(0f, 0f, 0f, 0f));
            _shieldBar.Root.Stretch();
            _shieldBar.Root.offsetMax = new Vector2(0f, -20f);
            var hpRim = UIKit.Panel(_hpTrail.Root, "Rim", UIKit.GoldTrim);
            hpRim.sprite = UIKit.RimSprite;
            hpRim.type = Image.Type.Sliced;
            hpRim.raycastTarget = false;
            hpRim.rectTransform.Stretch(-2f);
            _hpText = UIKit.Label(_hpTrail.Root, "", 20, TextAnchor.MiddleCenter);
            _hpText.rectTransform.Stretch();
            _perkRow = UIKit.Rect("Perks", panel.transform).Place(new Vector2(0f, 1f), new Vector2(350f, 34f), new Vector2(64f, -80f));
            BuildPortrait(root);

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
            // The room you stand in and what to do there, on a plate under the map.
            var plate = UIKit.Framed(root, "RoomPlate", new Color(0.04f, 0.03f, 0.04f, 0.88f));
            plate.Outer().Place(new Vector2(1f, 1f), new Vector2(460f, 96f), new Vector2(-24f, below));
            _roomText = UIKit.Title(plate.transform, "", 25, TextAnchor.UpperRight, new Color(0.95f, 0.88f, 0.76f));
            _roomText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(430f, 36f), new Vector2(-16f, -8f));
            _objectiveText = UIKit.Label(plate.transform, "", 22, TextAnchor.UpperRight, UIKit.AccentColor);
            _objectiveText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(430f, 52f), new Vector2(-16f, -44f));
            _codexNotice = BuildCodexNotice(root, below - 110f);

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

            // Bottom: the skill bar, icon slots with their key under them.
            for (int i = 0; i < 4; i++) _skills[i] = CreateSkillSlot(root, i);

            _centerText = UIKit.Title(root, "", 38, TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 0.8f));
            _centerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1300f, 160f), new Vector2(0f, 170f));

            _bannerText = UIKit.Title(root, "", 64, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _bannerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 100f), new Vector2(0f, 300f));
            _bannerSub = UIKit.Label(root, "", 28, TextAnchor.MiddleCenter, UIKit.MutedText);
            _bannerSub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 50f), new Vector2(0f, 240f));

            // Toasts: small plates stacked above the skill bar, newest at the bottom.
            for (int i = 0; i < _toastPlates.Length; i++)
            {
                var t = UIKit.Framed(root, "Toast", new Color(0.04f, 0.03f, 0.04f, 0.85f));
                var r = t.Outer();
                r.Place(new Vector2(0.5f, 0f), new Vector2(600f, 44f), new Vector2(0f, 180f + i * 50f));
                var g = r.gameObject.AddComponent<CanvasGroup>();
                g.blocksRaycasts = false;
                var text = UIKit.Label(t.transform, "", 24, TextAnchor.MiddleCenter);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.rectTransform.Stretch(6f);
                _toastPlates[i] = (r, text, g);
                r.gameObject.SetActive(false);
            }
            _guide = new GuideHints(root);

            BuildKeyLegend(root);

            _cinematics = new Cinematics(transform);
            BuildCurtain();
            gameObject.AddComponent<AllyOverlay>();
            gameObject.AddComponent<BagView>();
            gameObject.AddComponent<LootView>();
            gameObject.AddComponent<LegendaryFxView>();
            gameObject.AddComponent<DarkHallView>();
            gameObject.AddComponent<TetherView>();
            Codex.Recorded += OnCodexRecorded;
            GameSettings.Changed += OnSettingsChanged;
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
            frame.Outer().Place(new Vector2(0f, 1f), new Vector2(390f, 62f), new Vector2(20f, -196f - index * 72f));
            var edge = UIKit.Panel(frame.transform, "Edge", Color.white);
            edge.rectTransform.anchorMin = Vector2.zero;
            edge.rectTransform.anchorMax = new Vector2(0f, 1f);
            edge.rectTransform.pivot = new Vector2(0f, 0.5f);
            edge.rectTransform.sizeDelta = new Vector2(6f, 0f);
            // A small round portrait of the teammate.
            var face = UIKit.Panel(frame.transform, "Face", new Color(0.1f, 0.08f, 0.1f, 1f));
            face.sprite = UIKit.DiscSprite;
            face.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(54f, 54f), new Vector2(10f, 0f));
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var hero = UIKit.Panel(face.transform, "Hero", Color.white);
            hero.preserveAspect = true;
            hero.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(92f, 92f), new Vector2(4f, -2f));
            var faceRim = UIKit.Panel(face.transform.parent, "Rim", new Color(0.86f, 0.68f, 0.32f));
            faceRim.sprite = UIKit.ThinRingSprite;
            faceRim.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(58f, 58f), new Vector2(8f, 0f));
            foreach (var img in new[] { face, hero, faceRim }) img.raycastTarget = false;
            var anim = hero.gameObject.AddComponent<UISpriteAnimator>();
            var name = UIKit.Label(frame.transform, "", 20, TextAnchor.UpperLeft);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 26f), new Vector2(72f, -4f));
            var hp = UIKit.CreateBar(frame.transform, "Hp", new Color(0.82f, 0.14f, 0.16f), new Color(0.12f, 0.03f, 0.04f));
            hp.Root.Place(new Vector2(0f, 0f), new Vector2(300f, 20f), new Vector2(72f, 8f));
            Gloss(hp.Root);
            var shield = UIKit.CreateBar(hp.Root, "Shield", new Color(0.85f, 0.75f, 0.4f, 0.75f), new Color(0f, 0f, 0f, 0f));
            shield.Root.Stretch();
            shield.Root.offsetMax = new Vector2(0f, -12f);
            var text = UIKit.Label(hp.Root, "", 16, TextAnchor.MiddleCenter);
            text.rectTransform.Stretch();
            var ping = UIKit.Label(frame.transform, "", 16, TextAnchor.UpperRight, UIKit.MutedText);
            ping.rectTransform.Place(new Vector2(1f, 1f), new Vector2(120f, 24f), new Vector2(-12f, -5f));
            frame.Outer().gameObject.SetActive(false);
            return new TeamFrame { Root = frame.Outer().gameObject, Edge = edge, Name = name, Hp = hp, Shield = shield, HpText = text, Ping = ping, Face = anim };
        }

        private void OnDestroy()
        {
            RunDirector.EventReceived -= OnEvent;
            Codex.Recorded -= OnCodexRecorded;
            GameSettings.Changed -= OnSettingsChanged;
            if (Instance == this) Instance = null;
        }

        public void Toast(string text)
        {
            _toasts.Add((text, Time.time + 4.5f));
            if (_toasts.Count > 4) _toasts.RemoveAt(0);
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
            int area = index / RunSimulation.RoomsPerArea;
            // Outside a run (the camp's records) the depth says more than a land.
            var d = RunDirector.Instance;
            string name = d != null && d.IsSpawned ? AreaName(area) : LayerName(area);
            return index % RunSimulation.RoomsPerArea == RunSimulation.RoomsPerArea - 1 ? $"{name} ─ 主の間" : name;
        }

        public static string RoomTitle(MapRoom room) =>
            $"{AreaName(room.Index / RunSimulation.RoomsPerArea)} ─ {RoomCatalog.Name(room.Kind)}";

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
                BossTrial.Flood => $"<color=#8affe0>封じの栓を壊して水を引かせろ</color>　<color=#ffffff>{done}/{total}　残り {seconds}秒</color>",
                BossTrial.Mirror => $"<color=#bfe4ff>影のない写し身を砕け</color>　<color=#ffffff>{done}/{total}　残り {seconds}秒</color>",
                BossTrial.Overheat => $"<color=#ffb070>水甕を壊し、巨人を水へ誘い込め</color>　<color=#ffffff>残り {seconds}秒</color>",
                _ => "",
            };
        }

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.RoomStarted:
                {
                    // A new area: its name, then the map takes over.
                    int area = e.Int / RunSimulation.RoomsPerArea;
                    // The land comes with the event (the synced route may arrive a moment later).
                    _cinematics.Area(LayerName(area), BiomeCatalog.Get((Biome)Mathf.RoundToInt(e.Value)).Name);
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
                    if (director != null && director.LocalRoom.Id == e.Id) Banner("制圧", "", 1.8f);
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
                        RoomKind.Spirit => $"最大HPの{e.Value:0}%を捧げた。神の加護を授かる",
                        _ => "新たな力を授かる",
                    };
                    Toast($"<color=#{hex}>{RoomCatalog.Name(kind)}</color>：{text}");
                    break;
                }
                case SimEventType.BellRung:
                {
                    var who = NetPlayer.All.Find(p => p.OwnerClientId == e.ClientId);
                    string name = who != null ? $"<color=#{ColorUtility.ToHtmlStringRGB(who.Color)}>{who.DisplayName}（{who.ClassDef.Name}）</color>" : "仲間";
                    Debug.Log($"[Bell] rung by client {e.ClientId}, called {e.Int}");
                    Toast($"{name}が集合の鐘を鳴らした" + (e.Int > 0 ? $"：{e.Int}人が焚き火の間へ" : "（来られる仲間はいなかった）"));
                    break;
                }
                case SimEventType.DoorOpened:
                    if (e.Int == 0) Banner("主の扉が開いた", "覚悟はいいか", 2.4f);
                    else if (e.Int == 2) Banner("宝物庫の扉が開いた", "", 2f);
                    else if (e.Int == 3) Banner("隠し部屋を見つけた", "", 2.4f);
                    else if (e.Int == 4) Banner("主を討った", "帰還するか、さらに深層へ", 3.2f);
                    else Banner("主を討った", "左右の道から次の地を選べ", 3.2f);
                    break;
                case SimEventType.KeyGained:
                    Banner("鍵を手に入れた", "", 2.4f);
                    break;
                case SimEventType.PuzzleEnded:
                    if (RunDirector.Instance != null && RunDirector.Instance.LocalRoom.Id == e.Id)
                        Banner(e.Int == 1 ? "仕掛けを解いた" : "しくじった…", e.Int == 1 ? "" : "鍵は手に入らなかった", 2.4f);
                    break;
                case SimEventType.BossSpawned:
                {
                    var def = EnemyCatalog.Get((EnemyType)e.Int);
                    _cinematics.Lord(def.Name, def.Title ?? "");
                    break;
                }
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

        private sealed class CodexCard
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Icon;
            public Text Mark;
            public Text Name;
        }

        private CodexCard _codexNotice;
        private readonly Queue<CodexCatalog.Entry> _codexQueue = new();
        private float _codexShownAt = -10f;
        private const float CodexCardTime = 3.2f;

        /// <summary>"図鑑に記録" card that slides in under the room plate when something new is met.</summary>
        private static CodexCard BuildCodexNotice(Transform root, float y)
        {
            var card = UIKit.Framed(root, "CodexNotice", new Color(0.08f, 0.05f, 0.04f, 0.94f), UIKit.AccentColor);
            var rect = card.Outer();
            rect.Place(new Vector2(1f, 1f), new Vector2(360f, 84f), new Vector2(-24f, y));
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;
            var box = UIKit.Panel(card.transform, "IconBack", new Color(0f, 0f, 0f, 0.45f));
            box.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(10f, 0f));
            var icon = UIKit.Panel(box.transform, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.Stretch(4f);
            var mark = UIKit.Title(box.transform, "", 40);
            mark.rectTransform.Stretch();
            var head = UIKit.Label(card.transform, "図鑑に記録", 18, TextAnchor.UpperLeft, UIKit.AccentColor);
            head.rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 26f), new Vector2(86f, -8f));
            var keyHint = UIKit.Label(card.transform, "B で開く", 16, TextAnchor.UpperRight, UIKit.MutedText);
            keyHint.rectTransform.Place(new Vector2(1f, 1f), new Vector2(120f, 24f), new Vector2(-12f, -9f));
            var name = UIKit.Title(card.transform, "", 28, TextAnchor.LowerLeft, Color.white);
            name.rectTransform.Place(new Vector2(0f, 0f), new Vector2(260f, 44f), new Vector2(86f, 8f));
            return new CodexCard { Root = rect, Group = group, Icon = icon, Mark = mark, Name = name };
        }

        private void UpdateCodexNotice()
        {
            float t = Time.time - _codexShownAt;
            if (t > CodexCardTime && _codexQueue.Count > 0)
            {
                var e = _codexQueue.Dequeue();
                var sprite = Codex.Picture(e);
                _codexNotice.Icon.enabled = sprite != null;
                _codexNotice.Icon.sprite = sprite;
                _codexNotice.Mark.text = sprite == null ? e.Mark ?? "" : "";
                _codexNotice.Name.text = e.Name;
                _codexShownAt = Time.time;
                t = 0f;
                SfxPlayer.Instance?.Play("ping", null, 1.5f, 0.5f);
            }
            float alpha = t < 0.25f ? t / 0.25f : t > CodexCardTime - 0.4f ? Mathf.Clamp01((CodexCardTime - t) / 0.4f) : 1f;
            _codexNotice.Group.alpha = alpha;
            _codexNotice.Root.anchoredPosition = new Vector2(-24f + (1f - Mathf.Clamp01(t / 0.25f)) * 60f, _codexNotice.Root.anchoredPosition.y);
        }

        private void UpdateToasts()
        {
            for (int i = 0; i < _toastPlates.Length; i++)
            {
                var (root, text, group) = _toastPlates[i];
                int index = _toasts.Count - 1 - i; // newest at the bottom
                bool on = index >= 0 && !_toastsHidden;
                root.gameObject.SetActive(on);
                if (!on) continue;
                var t = _toasts[index];
                if (text.text != t.Text)
                {
                    text.text = t.Text;
                    root.sizeDelta = new Vector2(Mathf.Clamp(text.preferredWidth + 48f, 220f, 1500f), 44f);
                }
                group.alpha = Mathf.Clamp01((t.Until - Time.time) / 0.6f);
            }
        }

        private void OnSettingsChanged()
        {
            _mouseKeys = MouseKeys;
            if (_legend != null) BuildKeyLegend(_legend.parent);
        }

        private void OnCodexRecorded(CodexCatalog.Entry e) => _codexQueue.Enqueue(e);

        /// <summary>Key caps for the panels you can open (how to play lives in the codex).</summary>
        private RectTransform _legend;

        private void BuildKeyLegend(Transform root)
        {
            _legend ??= UIKit.Rect("Keys", root).Place(new Vector2(0f, 0f), new Vector2(700f, 34f), new Vector2(24f, 22f));
            var row = _legend;
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            float x = 0f;
            foreach (var (key, label) in new[] { (GameSettings.Label(KeyAction.Bag), "袋"), (GameSettings.Label(KeyAction.Map), "地図"), (GameSettings.Label(KeyAction.Ping), "合図"), (GameSettings.Label(KeyAction.Codex), "図鑑"), ("Esc", "メニュー") })
            {
                float w = Mathf.Max(32f, 18f + key.Length * 11f);
                UIKit.KeyCap(row, key, 17, out _).Place(new Vector2(0f, 0.5f), new Vector2(w, 30f), new Vector2(x, 0f));
                var text = UIKit.Label(row, label, 19, TextAnchor.MiddleLeft, UIKit.MutedText);
                text.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(90f, 30f), new Vector2(x + w + 6f, 0f));
                x += w + 6f + label.Length * 20f + 18f;
            }
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
            _levelText.text = $"Lv {director.TeamLevel.Value}";
            if (local != null)
                _classText.text = local.ClassDef.Name + (banked > 0 && director.LocalFighting ? $"　<size=22><color=#e0b45a>★{banked}</color></size>" : "");
            _timeText.text = UIKit.FormatTime(director.Elapsed.Value);
            string ping = local != null && !director.IsServer && local.Ping.Value > 0 ? $"通信 {PingText(local.Ping.Value)}　" : "";
            _difficultyText.text = CurseText(local != null ? local.Curses.Value : 0) + ping + (director.WeeklyWeek.Value >= 0 ? "<color=#9fd8ff>今週の迷宮</color>" : $"難易度：{DifficultyCatalog.Get(director.DifficultyLevel.Value).Name}");
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
                    var icons = SpriteSheet.Load("icons_skill")?.Sprites("default");
                    if (icons != null)
                    {
                        for (int s = 0; s < 3; s++) _skills[s].Icon.sprite = icons[Mathf.Min(ClassCatalog.SkillIcon(def.Id, s), icons.Length - 1)];
                        _skills[3].Icon.sprite = icons[Mathf.Min(ClassCatalog.DodgeIcon, icons.Length - 1)];
                    }
                }
                float max = Mathf.Max(1f, local.MaxHp.Value);
                _hpBar.Fill = local.Hp.Value / max;
                // Lost health lingers as a pale trail for a moment, then drains after it.
                float hp = local.Hp.Value / max;
                if (hp >= _trailValue) _trailValue = hp;
                else if (Time.time >= _trailHoldUntil) _trailValue = Mathf.MoveTowards(_trailValue, hp, Time.deltaTime * 0.6f);
                if (hp < _lastHp - 0.001f) _trailHoldUntil = Time.time + 0.45f;
                _lastHp = hp;
                _hpTrail.Fill = _trailValue;
                string sheet = SkinCatalog.Sheet(def.Id, local.Skin.Value);
                if (sheet != _portraitSheet)
                {
                    _portraitSheet = sheet;
                    _portraitAnim.Play(sheet, "idle");
                }
                _shieldBar.Fill = local.Shield.Value / max;
                _hpText.text = local.Shield.Value > 0 ? $"{local.Hp.Value:0} / {max:0}  (+{local.Shield.Value:0})" : $"{local.Hp.Value:0} / {max:0}";
                SetCooldown(_skills[1], local.Skill1Cooldown.Value, def.Skill1Cooldown);
                SetCooldown(_skills[2], local.Skill2Cooldown.Value, def.Skill2Cooldown);
                SetCooldown(_skills[3], local.DodgeCooldown.Value, ClassCatalog.DodgeCooldown);
                SetCooldown(_skills[0], 0f, 1f);
                // The key caps follow the device in use.
                var reader = local.GetComponent<PlayerInputReader>();
                var keys = reader != null && !reader.UsingMouse ? PadKeys : _mouseKeys;
                for (int s = 0; s < 4; s++)
                {
                    if (_skills[s].Key.text == keys[s]) continue;
                    _skills[s].Key.text = keys[s];
                    _skills[s].KeyCap.sizeDelta = new Vector2(keys[s].Length > 2 ? 86f : keys[s].Length == 2 ? 56f : 44f, 30f);
                }
            }

            UpdatePerkRow();
            UpdateTeam(local);

            _centerText.text = CenterMessage(director, local);
            // While a choice is on screen, only the cards speak (banners and toasts wait behind them).
            bool choosing = PerkView.IsShowing || (_mapRoot != null && _mapRoot.activeSelf); // the big map hides them too
            _bannerText.enabled = _bannerSub.enabled = !choosing && Time.time < _bannerUntil;
            _centerText.enabled = !choosing;
            _toastsHidden = choosing;
            _guide.Update(director, local, choosing || _cinematics.Playing);
            UpdateCodexNotice();
            // With the touch controls the key caps and the skill bar step aside (the on-screen buttons show the same).
            if (_legend != null) _legend.gameObject.SetActive(!TouchInput.Active);
            foreach (var s in _skills) s.Root.gameObject.SetActive(!TouchInput.Active);
            _cinematics.Update();

            _toasts.RemoveAll(t => t.Until < Time.time);
            UpdateToasts();
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
                string faceSheet = SkinCatalog.Sheet(p.Class.Value, p.Skin.Value);
                if (f.FaceSheet != faceSheet)
                {
                    f.FaceSheet = faceSheet;
                    f.Face.Play(faceSheet, "idle");
                }
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
                    f.Name.text = $"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.DisplayName}</color> {p.ClassDef.Name}<size=18>{p.TitleText}</size>";
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
            // Gods' blessings lead the row; a god whose favour is awake glows behind its blessings.
            order.Sort((a, b) => (PerkCatalog.Get((PerkId)a.Perk).God.HasValue ? 0 : 1).CompareTo(PerkCatalog.Get((PerkId)b.Perk).God.HasValue ? 0 : 1));
            for (int i = 0; i < order.Count && i < 15; i++)
            {
                var rect = UIKit.Rect("Perk", _perkRow).Place(new Vector2(0f, 0.5f), new Vector2(32f, 32f), new Vector2(i * 34f, 0f));
                var god = PerkCatalog.Get((PerkId)order[i].Perk).God;
                if (god.HasValue && PerkView.GodPieces(god.Value) >= GodCatalog.FavorPieces)
                {
                    var glow = UIKit.Panel(rect, "Favor", GodCatalog.Get(god.Value).Color * new Color(1f, 1f, 1f, 0.75f));
                    glow.sprite = UIKit.GlowSprite;
                    glow.raycastTarget = false;
                    glow.rectTransform.Stretch(-12f);
                }
                var img = UIKit.Rect("Icon", rect).gameObject.AddComponent<Image>();
                img.rectTransform.Stretch();
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
            slot.Cool.fillAmount = cooling ? Mathf.Clamp01(remaining / Mathf.Max(0.1f, max)) : 0f;
            slot.Time.text = cooling ? remaining.ToString(remaining < 1f ? "0.0" : "0") : "";
            slot.Icon.color = cooling ? new Color(0.55f, 0.55f, 0.6f) : Color.white;
            // Ready again: a flash and a little pop.
            if (!cooling && slot.Last > 0.05f) slot.FlashAt = Time.time;
            slot.Last = remaining;
            float k = Mathf.Clamp01((Time.time - slot.FlashAt) / 0.35f);
            slot.Flash.color = new Color(1f, 0.92f, 0.6f, (1f - k) * 0.7f);
            slot.Root.localScale = Vector3.one * (1f + (1f - k) * 0.08f);
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
                        return $"{(room.Dark ? "闇の群れ" : room.Kind == RoomKind.Elite ? "精鋭の群れ" : "魔物の群れ")}  第{Mathf.Max(1, d.WaveOf(room.Id))}波 / {RunSimulation.WaveCountFor(room.Kind, d.Area.Value)}{(room.Dark ? (d.LanternHolder.Value == 0 ? "　<color=#ffd08a>灯りを拾え</color>" : "　<color=#ffd08a>灯りのそばで戦え</color>") : "")}";
                    return d.IsCleared(room.Id) ? "<color=#bfb6aa>制圧済み</color>" : "";
                case RoomKind.Boss:
                    if (d.ExitOpen.Value) return Gather(RunSimulation.OffersHome(d.Area.Value) ? (team ? "全員で帰還か深層へ" : "帰還か深層へ") : team ? "全員で次の道を選ぶ" : "次の道を選ぶ");
                    return d.IsFighting(room.Id) ? $"{EnemyCatalog.Get(d.BossType.Value).Name}を討て" : "";
                case RoomKind.MiniBoss:
                    if (d.IsFighting(room.Id)) return "精鋭の主を倒せ";
                    return d.IsCleared(room.Id) ? "<color=#bfb6aa>制圧済み</color>" : "";
                case RoomKind.Puzzle:
                    if (d.IsFighting(room.Id))
                        return room.Puzzle switch
                        {
                            PuzzleKind.Plates => $"押し板を灯せ　<color=#ffffff>{Mathf.RoundToInt(d.PuzzleValue.Value)}/{RunSimulation.PlateSpots.Length}</color>",
                            PuzzleKind.Thief => $"小鬼を捕まえろ　<color=#ffffff>残り {Mathf.RoundToInt(d.PuzzleTime.Value)}秒</color>",
                            PuzzleKind.Gauntlet => "中央の押し板を踏め",
                            PuzzleKind.Twin => $"2枚を同時に踏め　<color=#ffffff>{d.PuzzleValue.Value:P0}</color>",
                            _ => $"結晶を守れ　<color=#ffffff>残り {Mathf.RoundToInt(d.PuzzleTime.Value)}秒　結晶 {d.PuzzleValue.Value:P0}</color>",
                        };
                    return d.IsCleared(room.Id) ? "<color=#bfb6aa>仕掛けは終わった</color>" : "";
                case RoomKind.Campfire:
                    if (d.BossDoorOpen.Value) return "主の間へ";
                    return Gather(team ? "全員で主の扉へ" : "主の扉へ");
                case RoomKind.Start:
                    return "<color=#bfb6aa>探索せよ</color>";
                default:
                    bool used = NetPlayer.Local != null && (NetPlayer.Local.UsedPedestals.Value & (1 << room.Id)) != 0;
                    return used ? "<color=#bfb6aa>使用済み</color>" : RoomCatalog.ShortText(room.Kind);
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
            bool toggle = GameSettings.Pressed(KeyAction.Map) || pad != null && pad.selectButton.wasPressedThisFrame;
            if (toggle && !PerkView.IsShowing && !PauseMenuView.IsOpen && !BagView.IsOpen && !CodexView.IsOpen && !SettingsView.IsOpen)
            {
                _mapRoot.SetActive(!_mapRoot.activeSelf);
                UiSound.Play(UiCue.Page);
            }
            if (PerkView.IsShowing || PauseMenuView.IsOpen || BagView.IsOpen) _mapRoot.SetActive(false);
            if (_mapRoot.activeSelf)
            {
                _bigMap.Update(d);
                int area = d.Area.Value;
                string keys = d.Keys.Value > 0 ? $"　<size=28><color=#e6c766>鍵 ×{d.Keys.Value}</color></size>" : "";
                string boss = d.BossDoorOpen.Value ? "" : "　<size=24><color=#bfb6aa>主の扉は全員がそろうと開く</color></size>";
                _mapTitle.text = AreaName(area) + keys + boss;
            }
            float k = (Time.time - _fadeStart) / FadeTime;
            _fade.color = new Color(0f, 0f, 0f, (k >= 1f ? 0f : 1f - k * k) * GameSettings.FlashScale);
        }

        /// <summary>The first time you step into a room of this area: its name.</summary>
        private void AnnounceRoom(RunDirector d, MapRoom room)
        {
            var map = (d.MapSeed.Value, d.Area.Value);
            if (room.Id == _shownRoom && map == _shownMap) return;
            if (map == _shownMap && _shownRoom >= 0) _fadeStart = Time.time; // through a door: a blink of darkness
            _shownRoom = room.Id;
            _shownMap = map;
            // The codex records rooms, their traps and (in co-op) the campfire bell; it holds what they do.
            Codex.EnterRoom(room.Kind);
            foreach (var g in room.Layout.Gimmicks) Codex.Record(CodexCatalog.GimmickEntry(g.Kind));
            if (room.Kind == RoomKind.Campfire && NetPlayer.All.Count > 1) Codex.Record(CodexCatalog.GimmickId("Bell"));
            if (room.Dark) Codex.Record(CodexCatalog.GimmickId("DarkHall"));
            if (d.Area.Value >= RunSimulation.AreaCount) Codex.Record("room:Deep");
            if (room.Kind == RoomKind.Puzzle && room.Puzzle == PuzzleKind.Twin) Codex.Record(CodexCatalog.GimmickId("TwinPlates"));
            if (room.Kind == RoomKind.Start || !_roomsBannered.Add((d.Area.Value, room.Id))) return;
            string sub = room.Kind == RoomKind.Puzzle
                ? room.Puzzle switch { PuzzleKind.Plates => "押し板の仕掛け", PuzzleKind.Thief => "逃げる宝の小鬼", PuzzleKind.Gauntlet => "罠の通路", PuzzleKind.Twin => "双子の押し板", _ => "結晶を守れ" }
                : CodexCatalog.Get(CodexCatalog.RoomId(room.Kind))?.Sub ?? "";
            if (room.Dark) Banner("闇の間", "灯りを持つ者のそばで戦え", 2.4f);
            else if (room.Kind != RoomKind.Boss) Banner(RoomCatalog.Name(room.Kind), sub, 2f);
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
                    : "倒れた…\n<size=26>幽霊のまま戦える。仲間が亡骸のそばに立つと起き上がる</size>";
            return "";
        }
    }
}
