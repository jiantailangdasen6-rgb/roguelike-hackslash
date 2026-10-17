using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The master's sphere board (SphereBoard) drawn in the camp panel: the class in the middle, its spheres joined by
    /// lines, open ones lit, reachable ones breathing. Drag to move, wheel or the +/− buttons to zoom; a sphere chosen
    /// shows on the right with its price and a button to open it (or to wear an art). Rebuilt whenever the profile
    /// changes, so where the board was looked at, and what was chosen, live in statics.
    /// </summary>
    public sealed class SphereBoardView : MonoBehaviour
    {
        private const float Spacing = 38f;      // pixels per board unit at zoom 1
        private const float BoardHalf = 440f;   // the content reaches this far from the centre

        private static Vector2 _scroll = new(0.5f, 0.5f);
        private static float _zoom = 0.64f; // the whole board fits the window
        private static string _chosen;
        private static string _justOpened;
        private static ClassId _lastClass = (ClassId)255;

        private ProfileData _profile;
        private ClassDef _class;
        private Action<string> _save;
        private Action<string> _say;

        private BoardScrollRect _scrollRect;
        private RectTransform _content;
        private readonly Dictionary<string, Image> _rings = new();
        private readonly List<(Image Image, float Phase)> _breathing = new();
        private Image _burst;
        private float _burstAt = -10f;
        private RectTransform _chosenMark;

        private Text _detailName, _detailKind, _detailText, _detailPrice, _summary;
        private Button _action;
        private Text _actionLabel;

        private static readonly Color Dark = new(0.13f, 0.12f, 0.14f, 1f);
        private static readonly Color LineOff = new(0.3f, 0.27f, 0.26f, 1f);
        private static readonly Color LineHalf = new(0.5f, 0.4f, 0.22f, 1f);
        private static readonly Color LineOn = new(0.95f, 0.78f, 0.4f, 1f);

        public static void Build(RectTransform body, ProfileData profile, ClassDef cls, Action<string> save, Action<string> say)
        {
            var go = UIKit.Rect("SphereBoard", body).Stretch().gameObject;
            var view = go.AddComponent<SphereBoardView>();
            view._profile = profile;
            view._class = cls;
            view._save = save;
            view._say = say;
            if (_lastClass != cls.Id)
            {
                _lastClass = cls.Id;
                _scroll = new Vector2(0.5f, 0.5f);
                _chosen = null;
            }
            view.Draw();
        }

        // ------------------------------------------------------------------ Drawing

        private void Draw()
        {
            // Left: the board in a masked window.
            var window = UIKit.Framed(transform, "Window", new Color(0.02f, 0.02f, 0.03f, 1f), UIKit.GoldTrim * 0.6f);
            window.Outer().Place(new Vector2(0f, 1f), new Vector2(780f, 600f), Vector2.zero);
            var viewport = UIKit.Rect("Viewport", window.transform).Stretch(4f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f); // takes the drags
            _content = UIKit.Rect("Board", viewport).Place(new Vector2(0.5f, 0.5f), new Vector2(BoardHalf * 2f, BoardHalf * 2f), Vector2.zero);
            _content.localScale = Vector3.one * _zoom;
            _scrollRect = viewport.gameObject.AddComponent<BoardScrollRect>();
            _scrollRect.Zoomed = dy => Zoom(dy > 0f ? 1.1f : 1f / 1.1f);
            _scrollRect.content = _content;
            _scrollRect.viewport = viewport;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 0f; // the wheel zooms instead
            _scrollRect.inertia = true;

            DrawBackdrop();
            var nodes = new List<MasteryNode>(MetaCatalog.NodesFor(_class.Id));
            DrawLines(nodes);
            DrawCentre();
            foreach (var n in nodes) DrawNode(n);
            _chosenMark = UIKit.Rect("Chosen", _content);
            var mark = _chosenMark.gameObject.AddComponent<Image>();
            mark.sprite = UIKit.ThinRingSprite;
            mark.color = Color.white;
            mark.raycastTarget = false;
            _burst = UIKit.Rect("Burst", _content).gameObject.AddComponent<Image>();
            _burst.sprite = UIKit.RingSprite;
            _burst.raycastTarget = false;
            _burst.color = new Color(1f, 0.85f, 0.5f, 0f);

            // Zoom buttons over the board's corner.
            var plus = UIKit.Button(window.transform, "+", 30, () => Zoom(1.2f));
            plus.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(48f, 48f), new Vector2(-12f, -12f));
            var minus = UIKit.Button(window.transform, "−", 30, () => Zoom(1f / 1.2f));
            minus.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(48f, 48f), new Vector2(-12f, -66f));
            var legend = UIKit.Label(window.transform, "<color=#f2c66d>●</color> 開放済み　<color=#8f8577>○</color> 開放できる　<color=#c9a6ff>◆</color> アーツ　<color=#e6c766>◇</color> シール（鍵）", 17, TextAnchor.LowerLeft, UIKit.MutedText);
            legend.rectTransform.Place(new Vector2(0f, 0f), new Vector2(700f, 30f), new Vector2(14f, 8f));
            legend.raycastTarget = false;

            DrawDetail();
            Canvas.ForceUpdateCanvases();
            _scrollRect.normalizedPosition = _scroll;
            _scrollRect.onValueChanged.AddListener(v => _scroll = v);
            Choose(_chosen != null && MetaCatalog.GetNode(_chosen)?.Class == _class.Id ? _chosen : FirstReachable());
            if (_justOpened != null && _rings.ContainsKey(_justOpened))
            {
                _burst.rectTransform.anchoredPosition = Pos(MetaCatalog.GetNode(_justOpened));
                _burstAt = Time.unscaledTime;
            }
            _justOpened = null;
        }

        private void DrawBackdrop()
        {
            // Faint orbits under the board (thin segments, so they stay crisp at any size), so it reads as one round thing.
            foreach (float r in new[] { 5f, 10.9f })
                for (int k = 0; k < 72; k++)
                {
                    float a0 = k * Mathf.PI * 2f / 72f, a1 = (k + 1) * Mathf.PI * 2f / 72f;
                    Vector2 from = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r * Spacing, to = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r * Spacing;
                    var seg = UIKit.Rect("Orbit", _content).gameObject.AddComponent<Image>();
                    seg.sprite = UIKit.WhiteSprite;
                    seg.color = new Color(0.6f, 0.48f, 0.3f, 0.18f);
                    seg.raycastTarget = false;
                    seg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2((to - from).magnitude + 1f, 2f), (from + to) * 0.5f);
                    seg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                }
            for (int b = 0; b < MetaCatalog.BranchCount; b++)
            {
                var name = UIKit.Title(_content, $"{MetaCatalog.BranchName(_class.Id, b)}ルート", 22, TextAnchor.MiddleCenter, new Color(0.9f, 0.75f, 0.45f, 0.75f));
                Vector2 at = SphereBoard.RoadDir(b) * SphereBoard.RoadLength * Spacing + (b == 0 ? new Vector2(130f, -4f) : new Vector2(0f, -50f));
                name.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(220f, 30f), at);
                name.raycastTarget = false;
            }
        }

        private void DrawLines(List<MasteryNode> nodes)
        {
            var drawn = new HashSet<string>();
            foreach (var a in nodes)
                foreach (var id in a.Links)
                {
                    bool core = id == SphereBoard.Core;
                    var b = core ? null : MetaCatalog.GetNode(id);
                    if (!core && b == null) continue;
                    string key = string.CompareOrdinal(a.Id, id) < 0 ? a.Id + "|" + id : id + "|" + a.Id;
                    if (!drawn.Add(key)) continue;
                    Vector2 from = Pos(a), to = core ? Vector2.zero : Pos(b);
                    int lit = (Open(a) ? 1 : 0) + (core || Open(b) ? 1 : 0);
                    var line = UIKit.Rect("Line", _content).gameObject.AddComponent<Image>();
                    line.sprite = UIKit.WhiteSprite;
                    line.raycastTarget = false;
                    line.color = lit == 2 ? LineOn : lit == 1 ? LineHalf : LineOff;
                    var rt = line.rectTransform;
                    rt.Place(new Vector2(0.5f, 0.5f), new Vector2((to - from).magnitude, lit == 2 ? 5f : 3f), (from + to) * 0.5f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                }
        }

        private void DrawCentre()
        {
            var back = UIKit.Rect("Centre", _content).gameObject.AddComponent<Image>();
            back.sprite = UIKit.DiscSprite;
            back.color = new Color(0.2f, 0.14f, 0.08f, 1f);
            back.raycastTarget = false;
            back.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(76f, 76f), Vector2.zero);
            var rim = UIKit.Rect("Rim", back.transform).Stretch(-3f).gameObject.AddComponent<Image>();
            rim.sprite = UIKit.ThinRingSprite;
            rim.color = UIKit.AccentColor;
            rim.raycastTarget = false;
            var sheet = SpriteSheet.Load(_class.Sheet, quiet: true);
            var portrait = UIKit.Rect("Class", back.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), new Vector2(0f, 6f)).gameObject.AddComponent<Image>();
            portrait.sprite = sheet != null && sheet.TryGetClip("idle", out _) ? sheet.Sprites("idle")[0] : sheet?.First;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.enabled = portrait.sprite != null;
        }

        private void DrawNode(MasteryNode n)
        {
            bool open = Open(n), reach = !open && _profile.IsReachable(n);
            bool art = n.Kind == MasteryKind.Keystone, gate = n.Kind == MasteryKind.Gate;
            float size = art ? 58f : gate ? 40f : n.Major ? 40f : 28f;
            Color hue = Hue(n);

            var root = UIKit.Rect(n.Id, _content).Place(new Vector2(0.5f, 0.5f), Vector2.one * (size + 14f), Pos(n));
            if (open)
            {
                var glow = UIKit.Rect("Glow", root).Place(new Vector2(0.5f, 0.5f), Vector2.one * size * 1.7f, Vector2.zero).gameObject.AddComponent<Image>();
                glow.sprite = UIKit.DiscSprite;
                glow.color = new Color(hue.r, hue.g, hue.b, 0.16f);
                glow.raycastTarget = false;
            }
            var body = UIKit.Rect("Sphere", root).Place(new Vector2(0.5f, 0.5f), Vector2.one * size, Vector2.zero).gameObject.AddComponent<Image>();
            body.sprite = gate ? UIKit.WhiteSprite : UIKit.DiscSprite;
            if (gate) body.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            if (gate) body.rectTransform.sizeDelta = Vector2.one * size * 0.72f;
            body.color = open ? hue : reach ? Color.Lerp(Dark, hue, 0.38f) : Dark;
            body.raycastTarget = true;

            var ring = UIKit.Rect("Ring", root).Place(new Vector2(0.5f, 0.5f), Vector2.one * (size + 8f), Vector2.zero).gameObject.AddComponent<Image>();
            ring.sprite = UIKit.ThinRingSprite;
            ring.color = open ? Color.Lerp(hue, Color.white, 0.5f) : reach ? hue : new Color(hue.r, hue.g, hue.b, 0.25f);
            ring.raycastTarget = false;
            _rings[n.Id] = ring;
            if (reach) _breathing.Add((ring, n.Pos.x * 0.7f + n.Pos.y));

            Sprite icon = art ? GameIcons.Rune(n.Rune) : n.Kind is MasteryKind.UnlockPerk or MasteryKind.GrantPerk ? GameIcons.Perk(n.Perk) : null;
            if (icon != null)
            {
                var img = UIKit.Rect("Icon", root).Place(new Vector2(0.5f, 0.5f), Vector2.one * size * 0.78f, Vector2.zero).gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.raycastTarget = false;
                img.color = open ? Color.white : reach ? new Color(0.75f, 0.75f, 0.78f, 1f) : new Color(0.35f, 0.35f, 0.4f, 0.8f);
            }
            else if (n.Kind is MasteryKind.HealthGrowth or MasteryKind.MightGrowth or MasteryKind.StartPerk || gate)
            {
                var inner = UIKit.Rect("Inner", root).Place(new Vector2(0.5f, 0.5f), Vector2.one * size * (gate ? 0.32f : 0.4f), Vector2.zero).gameObject.AddComponent<Image>();
                inner.sprite = gate ? UIKit.WhiteSprite : UIKit.DiscSprite;
                if (gate) inner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                inner.color = open ? Color.white : new Color(1f, 1f, 1f, reach ? 0.45f : 0.15f);
                inner.raycastTarget = false;
            }
            if (art && open && _profile.WornArt(_class.Id) == n)
            {
                var worn = UIKit.Title(root, "装備中", 15, TextAnchor.MiddleCenter, new Color(0.85f, 0.75f, 1f));
                worn.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(90f, 20f), new Vector2(0f, -16f));
                worn.raycastTarget = false;
            }

            var button = body.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            string id = n.Id;
            button.onClick.AddListener(() => Choose(id));
            UIKit.AddSounds(button);
        }

        /// <summary>Colour by what the sphere gives: offence, defence, technique, the rings, growth, perks, arts, seals.</summary>
        private static Color Hue(MasteryNode n) => n.Kind switch
        {
            MasteryKind.Keystone => new Color(0.72f, 0.48f, 0.95f),
            MasteryKind.Gate => new Color(0.9f, 0.76f, 0.3f),
            MasteryKind.UnlockPerk or MasteryKind.GrantPerk or MasteryKind.StartPerk => new Color(0.95f, 0.72f, 0.3f),
            MasteryKind.HealthGrowth or MasteryKind.MightGrowth => new Color(0.45f, 0.85f, 0.5f),
            _ => n.Modifier.Stat switch
            {
                StatType.Might or StatType.CritChance => new Color(0.92f, 0.42f, 0.32f),
                StatType.MaxHealth or StatType.Armor => new Color(0.4f, 0.62f, 0.95f),
                StatType.Cooldown or StatType.AttackSpeed => new Color(0.4f, 0.85f, 0.75f),
                _ => new Color(0.75f, 0.72f, 0.66f),
            },
        };

        private static Vector2 Pos(MasteryNode n) => n.Pos * Spacing;
        private bool Open(MasteryNode n) => n != null && _profile.unlocked.Contains(n.Id);

        private string FirstReachable()
        {
            foreach (var n in MetaCatalog.NodesFor(_class.Id))
                if (!Open(n) && _profile.IsReachable(n)) return n.Id;
            return null;
        }

        // ------------------------------------------------------------------ The chosen sphere

        private void DrawDetail()
        {
            var detail = UIKit.Framed(transform, "Detail", new Color(0.05f, 0.04f, 0.05f, 1f), UIKit.GoldTrim * 0.6f);
            detail.Outer().Place(new Vector2(1f, 1f), new Vector2(364f, 600f), Vector2.zero);
            var t = detail.transform;
            _detailName = UIKit.Title(t, "", 34, TextAnchor.UpperLeft, Color.white).Fit(20);
            _detailName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(330f, 48f), new Vector2(16f, -16f));
            _detailKind = UIKit.Label(t, "", 20, TextAnchor.UpperLeft, new Color(0.62f, 0.85f, 1f));
            _detailKind.rectTransform.Place(new Vector2(0f, 1f), new Vector2(330f, 30f), new Vector2(16f, -66f));
            _detailText = UIKit.Label(t, "", 22, TextAnchor.UpperLeft, new Color(0.88f, 0.84f, 0.78f));
            _detailText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(330f, 170f), new Vector2(16f, -104f));
            _detailPrice = UIKit.Label(t, "", 22, TextAnchor.UpperLeft);
            _detailPrice.rectTransform.Place(new Vector2(0f, 1f), new Vector2(330f, 60f), new Vector2(16f, -282f));
            _action = UIKit.Button(t, "", 26, Act, new Color(0.1f, 0.15f, 0.25f));
            _action.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(320f, 60f), new Vector2(0f, -350f));
            _actionLabel = _action.GetComponentInChildren<Text>();

            int total = 0, open = 0;
            foreach (var n in MetaCatalog.NodesFor(_class.Id)) { total++; if (Open(n)) open++; }
            _summary = UIKit.Label(t, $"開放 {open} / {total}\n<size=18><color=#9a9288>ルートのスフィアはランの成績で、鍵はボスを倒すと手に入る</color></size>", 20, TextAnchor.UpperLeft, UIKit.MutedText);
            _summary.rectTransform.Place(new Vector2(0f, 0f), new Vector2(330f, 90f), new Vector2(16f, 84f));
            int spent = _profile.SpheresSpent(_class.Id);
            var respec = UIKit.Button(t, spent > 0 ? $"振り直す（{spent} 戻る）" : "振り直す", 22, () =>
            {
                int back = _profile.Respec(_class.Id);
                _chosen = null;
                _save(back > 0 ? $"盤を白紙に戻した（スフィア {back} が戻った）" : null);
            }, UIKit.BloodColor);
            respec.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(320f, 52f), new Vector2(0f, 18f));
            respec.interactable = spent > 0;
        }

        private void Choose(string id)
        {
            _chosen = id;
            var n = MetaCatalog.GetNode(id);
            _chosenMark.gameObject.SetActive(n != null);
            if (n == null)
            {
                _detailName.text = "全部開いた";
                _detailKind.text = _detailText.text = _detailPrice.text = "";
                _action.gameObject.SetActive(false);
                return;
            }
            float size = n.Kind == MasteryKind.Keystone ? 58f : n.Major || n.Kind == MasteryKind.Gate ? 40f : 28f;
            _chosenMark.Place(new Vector2(0.5f, 0.5f), Vector2.one * (size + 22f), Pos(n));

            bool open = Open(n), reach = _profile.IsReachable(n);
            _detailName.text = n.Name;
            _detailKind.text = n.Kind switch
            {
                MasteryKind.Keystone => $"アーツ・{(n.Skill == 1 ? _class.Skill1Name : _class.Skill2Name)}が変わる",
                MasteryKind.Gate => "シール",
                MasteryKind.UnlockPerk => "新しいパーク",
                MasteryKind.GrantPerk => "始めから持つパーク",
                MasteryKind.StartPerk => "開始時のブレッシング",
                MasteryKind.HealthGrowth or MasteryKind.MightGrowth => "成長",
                _ => n.Major ? "大きなスフィア" : "スフィア",
            };
            _detailText.text = n.Kind == MasteryKind.Keystone
                ? n.Text + "\n\n<size=18><color=#9a9288>アーツは全部開けられる。出発するときに効くのは装備した1つ（ここで切り替え）</color></size>"
                : n.Text;
            string keys = n.Keys > 0 ? $"　<color=#e6c766>鍵 {n.Keys}</color>" : "";
            _detailPrice.text = open ? "<color=#f2c66d>開放済み</color>"
                : $"<color=#9fd8ff>スフィア {n.Cost}</color>{keys}\n<size=18><color=#9a9288>所持 スフィア {_profile.spheres}・鍵 {_profile.keys}</color></size>";

            _action.gameObject.SetActive(true);
            if (open && n.Kind == MasteryKind.Keystone)
            {
                bool worn = _profile.WornArt(_class.Id) == n;
                _actionLabel.text = worn ? "装備中" : "このアーツを装備";
                _action.interactable = !worn;
            }
            else if (open) _action.gameObject.SetActive(false);
            else
            {
                bool afford = _profile.spheres >= n.Cost && _profile.keys >= n.Keys;
                _actionLabel.text = !reach ? "まだ届かない" : afford ? "開放する" : "足りない";
                _action.interactable = reach && afford;
            }
        }

        private void Act()
        {
            var n = MetaCatalog.GetNode(_chosen);
            if (n == null) return;
            if (Open(n))
            {
                if (n.Kind != MasteryKind.Keystone) return;
                _profile.WearArt(n);
                _save($"アーツ「{n.Name}」を装備した");
                return;
            }
            if (_profile.TryLearn(n, out string why))
            {
                _justOpened = n.Id;
                // The next one along stays chosen, so a road can be walked with one button.
                foreach (var id in n.Links)
                    if (MetaCatalog.GetNode(id) is { } next && !Open(next)) { _chosen = id; break; }
                _save(n.Kind == MasteryKind.Keystone ? $"アーツ「{n.Name}」を会得した" : $"「{n.Name}」を開放した");
            }
            else _say(why);
        }

        // ------------------------------------------------------------------ Moving and zooming

        private void Zoom(float by)
        {
            _zoom = Mathf.Clamp(_zoom * by, 0.64f, 1.8f);
            _content.localScale = Vector3.one * _zoom;
        }


        private void Update()
        {
            float t = Time.unscaledTime;
            foreach (var (image, phase) in _breathing)
            {
                var c = image.color;
                c.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * 3.2f + phase));
                image.color = c;
            }
            if (_chosenMark != null)
                _chosenMark.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
            float since = t - _burstAt;
            if (_burst != null && since < 0.8f)
            {
                float k = since / 0.8f;
                _burst.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(30f, 150f, k);
                _burst.color = new Color(1f, 0.85f, 0.5f, 1f - k);
            }
        }
    }
}
