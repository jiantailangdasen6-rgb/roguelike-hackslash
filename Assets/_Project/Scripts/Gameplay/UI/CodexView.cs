using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The codex (図鑑), opened with B or from the menu, in the camp and in a run (it doesn't pause a run).
    /// Tabs per section; the left side is a grid of entries (unmet ones are dark silhouettes), the right side the
    /// picked entry: its picture moving, name, traits and how to deal with it.
    /// </summary>
    public sealed class CodexView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        /// <summary>The frame it closed on (Esc closing it must not open the menu).</summary>
        public static int ClosedFrame { get; private set; } = -1;
        private static CodexView _instance;

        private const int Columns = 5;
        private const float CellW = 150f, CellH = 172f;

        private GameObject _root;
        private readonly List<(Button Button, Image Fill, CodexSection Section)> _tabs = new();
        private RectTransform _grid;
        private Text _progress;
        private Image _picture;
        private UISpriteAnimator _animator;
        private Text _pictureMark;
        private Text _name;
        private Text _sub;
        private Text _traits;
        private Text _counter;
        private GameObject _counterBox;
        private CodexSection _section;
        private readonly List<(Button Button, CodexCatalog.Entry Entry)> _cells = new();

        public static void Open(CodexSection? section = null)
        {
            if (_instance == null) return;
            _instance.Show(section ?? _instance._section);
        }

        private void Awake()
        {
            _instance = this;
            Build();
            _root.SetActive(false);
        }

        // Screenshots: -opencodex <monsters|lords|gimmicks|rooms|teamwork|controls> [-opencodexat <s>] opens it.
        private float _openAt = -1f;

        private void Start()
        {
            if (!CommandLine.Has("-opencodex")) return;
            _openAt = Time.time + CommandLine.GetFloat("-opencodexat", 0.5f);
        }

        private void LateUpdate()
        {
            if (_openAt < 0f || Time.time < _openAt) return;
            _openAt = -1f;
            string s = CommandLine.GetValue("-opencodex") ?? "";
            Show(System.Enum.TryParse(s, true, out CodexSection section) ? section : CodexSection.Monsters);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            IsOpen = false;
        }

        private void Build()
        {
            var canvas = UIKit.CreateCanvas("Codex", 58, transform).transform;
            var dim = UIKit.Panel(canvas, "Dim", new Color(0f, 0f, 0f, 0.72f));
            dim.rectTransform.Stretch();
            _root = dim.gameObject;

            var panel = UIKit.Framed(dim.transform, "Book", new Color(0.07f, 0.055f, 0.06f, 0.98f));
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1700f, 940f), Vector2.zero);
            var p = panel.transform;

            var title = UIKit.Title(p, "図鑑", 64, TextAnchor.MiddleLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 90f), new Vector2(40f, -16f));
            _progress = UIKit.Label(p, "", 24, TextAnchor.MiddleLeft, UIKit.MutedText);
            _progress.rectTransform.Place(new Vector2(0f, 0f), new Vector2(520f, 50f), new Vector2(44f, 16f));

            // Section tabs.
            var sections = (CodexSection[])System.Enum.GetValues(typeof(CodexSection));
            float tabW = 200f, gap = 12f, x0 = 260f;
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                var tab = UIKit.Button(p, CodexCatalog.SectionName(s), 26, () => Show(s));
                tab.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(tabW, 58f), new Vector2(x0 + i * (tabW + gap), -32f));
                _tabs.Add((tab, tab.GetComponent<Image>(), s));
            }

            // Left: the grid of entries.
            _grid = UIKit.Rect("Grid", p);
            _grid.anchorMin = _grid.anchorMax = new Vector2(0f, 1f);
            _grid.pivot = new Vector2(0f, 1f);
            _grid.anchoredPosition = new Vector2(40f, -124f);
            _grid.sizeDelta = new Vector2(Columns * CellW, 760f);

            // Right: the page of the picked entry.
            var page = UIKit.Framed(p, "Page", new Color(0.035f, 0.03f, 0.035f, 0.95f));
            page.Outer().Place(new Vector2(1f, 1f), new Vector2(820f, 724f), new Vector2(-40f, -124f));
            var pg = page.transform;
            var pictureFrame = UIKit.Framed(pg, "Picture", new Color(0.09f, 0.07f, 0.07f, 1f));
            pictureFrame.Outer().Place(new Vector2(0f, 1f), new Vector2(260f, 260f), new Vector2(30f, -30f));
            var glow = UIKit.Panel(pictureFrame.transform, "Glow", new Color(1f, 0.75f, 0.4f, 0.22f));
            glow.sprite = UIKit.GlowSprite;
            glow.rectTransform.Stretch(20f);
            glow.raycastTarget = false;
            _picture = UIKit.Panel(pictureFrame.transform, "Sprite", Color.white);
            _picture.preserveAspect = true;
            _picture.raycastTarget = false;
            _picture.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(330f, 330f), new Vector2(0f, -24f));
            pictureFrame.gameObject.AddComponent<RectMask2D>();
            _animator = _picture.gameObject.AddComponent<UISpriteAnimator>();
            _pictureMark = UIKit.Title(pictureFrame.transform, "", 120);
            _pictureMark.rectTransform.Stretch();

            _name = UIKit.Title(pg, "", 52, TextAnchor.UpperLeft);
            _name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(480f, 70f), new Vector2(316f, -44f));
            _sub = UIKit.Label(pg, "", 26, TextAnchor.UpperLeft, UIKit.MutedText);
            _sub.rectTransform.Place(new Vector2(0f, 1f), new Vector2(480f, 80f), new Vector2(318f, -122f));

            var traitsHeader = UIKit.Title(pg, "特性", 30, TextAnchor.MiddleLeft);
            traitsHeader.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 40f), new Vector2(34f, -312f));
            _traits = UIKit.Label(pg, "", 27, TextAnchor.UpperLeft, new Color(0.92f, 0.88f, 0.82f));
            _traits.rectTransform.Place(new Vector2(0f, 1f), new Vector2(750f, 260f), new Vector2(34f, -360f));
            _traits.lineSpacing = 1.15f;

            var counter = UIKit.Framed(pg, "Counter", new Color(0.12f, 0.06f, 0.05f, 0.95f));
            counter.Outer().Place(new Vector2(0.5f, 0f), new Vector2(760f, 132f), new Vector2(0f, 28f));
            _counterBox = counter.Outer().gameObject;
            var counterHeader = UIKit.Title(counter.transform, "対処", 28, TextAnchor.UpperLeft);
            counterHeader.rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 40f), new Vector2(22f, -12f));
            _counter = UIKit.Label(counter.transform, "", 26, TextAnchor.UpperLeft, Color.white);
            _counter.rectTransform.Place(new Vector2(0f, 1f), new Vector2(710f, 80f), new Vector2(24f, -52f));

            var close = UIKit.Button(p, "閉じる（B・Esc）", 24, Hide);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(260f, 52f), new Vector2(-40f, 14f));
        }

        private void Show(CodexSection section)
        {
            _section = section;
            _root.SetActive(true);
            IsOpen = true;
            foreach (var (button, fill, s) in _tabs)
                fill.color = s == section ? new Color(0.62f, 0.38f, 0.16f) : UIKit.ButtonColor;
            var (known, total) = Codex.Progress();
            _progress.text = $"記録 <color=#e0b45a>{known}</color> / {total}";

            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
            _cells.Clear();
            var entries = CodexCatalog.In(section);
            for (int i = 0; i < entries.Count; i++) _cells.Add((CreateCell(entries[i], i), entries[i]));
            if (_cells.Count > 0)
            {
                Pick(_cells[0].Entry);
                EventSystem.current?.SetSelectedGameObject(_cells[0].Button.gameObject);
            }
        }

        private Button CreateCell(CodexCatalog.Entry entry, int index)
        {
            bool known = Codex.Known(entry);
            var frame = UIKit.Framed(_grid, "Entry", new Color(0.1f, 0.08f, 0.08f, 1f));
            var rect = frame.Outer();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(CellW - 14f, CellW - 14f);
            rect.anchoredPosition = new Vector2(index % Columns * CellW, -(index / Columns) * CellH);

            var sprite = Codex.Picture(entry);
            if (sprite != null)
            {
                var icon = UIKit.Panel(frame.transform, "Icon", known ? Color.white : new Color(0f, 0f, 0f, 0.92f));
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.rectTransform.Stretch(entry.FillsFrame ? 16f : -18f); // characters have room around them in their frames
                frame.gameObject.AddComponent<RectMask2D>();
            }
            else
            {
                var mark = UIKit.Title(frame.transform, known ? entry.Mark ?? "" : "", 64);
                mark.rectTransform.Stretch();
            }
            if (!known)
            {
                var q = UIKit.Title(frame.transform, "？", 54, TextAnchor.MiddleCenter, new Color(0.6f, 0.55f, 0.5f, 0.8f));
                q.rectTransform.Stretch();
            }
            var name = UIKit.Label(rect, known ? entry.Name : "？？？", 19, TextAnchor.UpperCenter, known ? Color.white : UIKit.MutedText);
            name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(CellW + 6f, 30f), new Vector2(0f, -30f));

            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.6f, 1.4f, 1f);
            colors.selectedColor = new Color(1.9f, 1.6f, 1f);
            button.colors = colors;
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => Pick(entry));
            // Moving the selection with keys or a pad shows the entry too.
            var trigger = rect.gameObject.AddComponent<EventTrigger>();
            var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            select.callback.AddListener(_ => Pick(entry));
            trigger.triggers.Add(select);
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ => Pick(entry));
            trigger.triggers.Add(hover);
            return button;
        }

        private void Pick(CodexCatalog.Entry entry)
        {
            bool known = Codex.Known(entry);
            var sprite = Codex.Picture(entry);
            _picture.enabled = sprite != null;
            _picture.rectTransform.sizeDelta = entry.FillsFrame ? new Vector2(180f, 180f) : new Vector2(330f, 330f);
            _picture.rectTransform.anchoredPosition = new Vector2(0f, entry.FillsFrame ? 0f : -24f);
            _pictureMark.text = sprite == null && known ? entry.Mark ?? "" : sprite == null ? "？" : "";
            if (sprite != null)
            {
                var sheet = string.IsNullOrEmpty(entry.Sheet) ? null : SpriteSheet.Load(entry.Sheet, quiet: true);
                if (known && !entry.FillsFrame && sheet != null && sheet.TryGetClip("idle", out _)) _animator.Play(entry.Sheet, "idle");
                else
                {
                    _animator.Stop();
                    _picture.sprite = sprite;
                }
                _picture.color = known ? Color.white : new Color(0f, 0f, 0f, 0.92f);
            }
            _name.text = known ? entry.Name : "？？？";
            _sub.text = known ? entry.Sub ?? "" : "まだ出会っていない";
            if (known)
            {
                var lines = new System.Text.StringBuilder();
                foreach (var t in entry.Traits) lines.Append("<color=#e0b45a>◆</color>").Append(t).Append('\n');
                _traits.text = lines.ToString();
                _counter.text = entry.Counter ?? "";
            }
            else
            {
                _traits.text = "<color=#8a8078>迷宮のどこかで出会うと記録される</color>";
                _counter.text = "";
            }
            _counterBox.SetActive(known && !string.IsNullOrEmpty(entry.Counter));
        }

        private void Hide()
        {
            _root.SetActive(false);
            IsOpen = false;
            ClosedFrame = Time.frameCount;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool toggle = kb != null && kb.bKey.wasPressedThisFrame;
            bool back = kb != null && kb.escapeKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame;
            if (IsOpen)
            {
                if (toggle || back) Hide();
                // Shoulder buttons / Q·E flip the tabs.
                int dir = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame) ? 1
                    : (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame) ? -1 : 0;
                if (dir != 0)
                {
                    int count = _tabs.Count;
                    Show((CodexSection)(((int)_section + dir + count) % count));
                }
                return;
            }
            if (toggle && !PauseMenuView.IsOpen && !PerkView.IsShowing && !BagView.IsOpen && !CampView.PanelOpen && !LobbyTyping()) Show(_section);
        }

        private static bool LobbyTyping()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<InputField>() != null;
        }
    }
}
