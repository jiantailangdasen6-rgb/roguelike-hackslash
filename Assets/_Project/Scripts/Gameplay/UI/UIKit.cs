using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Builds uGUI hierarchies from code (no prefabs to wire up). Text uses an OS font so Japanese
    /// renders without bundling a font file.
    /// </summary>
    public static class UIKit
    {
        // Dark-fantasy palette: near-black panels, tarnished gold trim, blood red accents.
        public static readonly Color PanelColor = new(0.055f, 0.045f, 0.06f, 0.96f);
        public static readonly Color ButtonColor = new(0.24f, 0.14f, 0.15f, 1f);
        public static readonly Color AccentColor = new(0.88f, 0.7f, 0.34f, 1f);
        public static readonly Color GoldTrim = new(0.55f, 0.42f, 0.2f, 1f);
        public static readonly Color BloodColor = new(0.62f, 0.1f, 0.1f, 1f);
        public static readonly Color MutedText = new(0.68f, 0.64f, 0.6f, 1f);

        private static Font _font;
        private static Font _titleFont;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                // Bundled subset fonts (Tools/SubsetFonts.ps1) — browsers have no OS fonts to fall back on.
                _font = Resources.Load<Font>("Fonts/Body");
                if (_font != null) return _font;
                _font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic",
                    "Hiragino Sans", "Hiragino Kaku Gothic ProN",
                    "Noto Sans CJK JP", "Noto Sans JP", "Droid Sans Fallback", "Arial",
                }, 32);
                return _font;
            }
        }

        /// <summary>Serif (Mincho) face for titles and numbers — closer to a gothic, dark-fantasy feel.</summary>
        public static Font TitleFont
        {
            get
            {
                if (_titleFont != null) return _titleFont;
                _titleFont = Resources.Load<Font>("Fonts/Title");
                if (_titleFont != null) return _titleFont;
                _titleFont = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Yu Mincho Demibold", "Yu Mincho", "游明朝", "MS PMincho", "MS Mincho",
                    "Hiragino Mincho ProN", "Noto Serif CJK JP", "Noto Serif JP", "Times New Roman",
                }, 32);
                return _titleFont;
            }
        }

        /// <summary>
        /// A dark panel with a gold border, a soft top-to-bottom shade, an inner shadow and studded corners;
        /// returns the inner panel to put content in. The outer image keeps the border colour (selection tints it).
        /// </summary>
        public static Image Framed(Transform parent, string name, Color? fill = null, Color? border = null)
        {
            var outer = Panel(parent, name, border ?? GoldTrim);
            outer.sprite = FrameSprite;
            outer.type = Image.Type.Sliced;
            var inner = Panel(outer.transform, "Inner", fill ?? PanelColor);
            inner.sprite = ShadeSprite;
            inner.rectTransform.Stretch(10f);
            return inner;
        }

        /// <summary>Inner shadow, bevel and corner studs over a panel or button (doesn't catch clicks).</summary>
        public static Image AddTrim(Transform parent, Color studs)
        {
            var trim = Panel(parent, "Trim", studs);
            trim.sprite = TrimSprite;
            trim.type = Image.Type.Sliced;
            trim.raycastTarget = false;
            trim.rectTransform.Stretch();
            return trim;
        }

        public static RectTransform Outer(this Image framedInner) => (RectTransform)framedInner.transform.parent;

        public static Text Title(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        {
            var label = Label(parent, text, size, align, color ?? AccentColor);
            label.font = TitleFont;
            return label;
        }

        private static readonly List<CanvasScaler> Scalers = new();
        private static bool _scaleHooked;

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080) / GameSettings.UiScale;
            Scalers.Add(scaler);
            if (!_scaleHooked)
            {
                _scaleHooked = true;
                GameSettings.Changed += () =>
                {
                    Scalers.RemoveAll(s => s == null);
                    foreach (var s in Scalers) s.referenceResolution = new Vector2(1920, 1080) / GameSettings.UiScale;
                };
            }
            // Expand: the whole 1920x1080 layout always fits (wide browser windows only add room at the sides,
            // tall ones at the top/bottom) instead of spilling off-screen and overlapping.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors a rect at <paramref name="anchor"/> (0..1) with a pixel size and offset.</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        public static RectTransform Stretch(this RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var label = Rect("Text", parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? Color.white;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        /// <summary>A bevelled button with a gold rim: the fill (tinted <paramref name="color"/>) brightens on hover.</summary>
        public static Button Button(Transform parent, string text, int fontSize, UnityAction onClick, Color? color = null)
        {
            var image = Panel(parent, "Button", color ?? ButtonColor);
            image.sprite = BevelSprite;
            image.type = Image.Type.Sliced;
            var rim = Panel(image.transform, "Rim", GoldTrim);
            rim.sprite = RimSprite;
            rim.type = Image.Type.Sliced;
            rim.raycastTarget = false;
            rim.rectTransform.Stretch();
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.selectedColor = new Color(1.35f, 1.3f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            AddSounds(button);
            button.onClick.AddListener(onClick);
            Label(image.transform, text, fontSize, TextAnchor.MiddleCenter).rectTransform.Stretch(8f);
            return button;
        }

        /// <summary>Hover and click sounds on a button (pointer or selection).</summary>
        public static void AddSounds(Button button)
        {
            button.onClick.AddListener(() => UiSound.Play(UiCue.Click));
            var trigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            foreach (var id in new[] { UnityEngine.EventSystems.EventTriggerType.PointerEnter, UnityEngine.EventSystems.EventTriggerType.Select })
            {
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = id };
                entry.callback.AddListener(_ => { if (button.interactable) UiSound.Play(UiCue.Hover); });
                trigger.triggers.Add(entry);
            }
        }

        /// <summary>A small key cap (a bevelled dark key with a thin metal rim) with its label; returns its rect.</summary>
        public static RectTransform KeyCap(Transform parent, string key, int fontSize, out Text label)
        {
            var image = Panel(parent, "Key", new Color(0.22f, 0.18f, 0.17f, 1f));
            image.sprite = BevelSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var rim = Panel(image.transform, "Rim", GoldTrim);
            rim.sprite = RimSprite;
            rim.type = Image.Type.Sliced;
            rim.raycastTarget = false;
            rim.rectTransform.Stretch();
            label = Label(image.transform, key, fontSize, TextAnchor.MiddleCenter, new Color(0.96f, 0.92f, 0.84f));
            label.rectTransform.Stretch();
            return image.rectTransform;
        }

        /// <summary>A horizontal bar; set <see cref="Bar.Fill"/> to 0..1.</summary>
        public static Bar CreateBar(Transform parent, string name, Color fill, Color background)
        {
            var bg = Panel(parent, name, background);
            var fillImage = Panel(bg.transform, "Fill", fill);
            fillImage.rectTransform.anchorMin = Vector2.zero;
            fillImage.rectTransform.anchorMax = Vector2.one;
            fillImage.rectTransform.offsetMin = Vector2.zero;
            fillImage.rectTransform.offsetMax = Vector2.zero;
            return new Bar(bg.rectTransform, fillImage.rectTransform);
        }

        public sealed class Bar
        {
            public readonly RectTransform Root;
            private readonly RectTransform _fill;

            public Bar(RectTransform root, RectTransform fill)
            {
                Root = root;
                _fill = fill;
            }

            public float Fill
            {
                set => _fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            }
        }

        // ------------------------------------------------------------------ Runtime-generated UI shapes

        // ------------------------------------------------------------------ Frame sprites (generated, pixel-art sized: 1 texel = 2 UI px)

        private const float FramePixelsPerUnit = 50f;
        private static Sprite _shade, _trim, _bevel, _rim;

        /// <summary>Vertical shade for panel fills: full colour at the top, a little darker at the bottom.</summary>
        public static Sprite ShadeSprite => _shade != null ? _shade : _shade = PixelSprite(4, 32, 0, (x, y, w, h) =>
        {
            float v = Mathf.Lerp(0.8f, 1f, y / (h - 1f));
            return new Color(v, v, v, 1f);
        });

        private static Sprite _frame;

        /// <summary>
        /// Panel border (tinted by the border colour): a dark outline, a bevelled double metal line lit from the top-left,
        /// an inner shadow, and a diamond stud with a little bracket in each corner. The centre is clear for the fill.
        /// </summary>
        public static Sprite FrameSprite => _frame != null ? _frame : _frame = PixelSprite(24, 24, 8, (x, y, w, h) =>
        {
            int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y), d = Mathf.Min(dx, dy);
            bool topLeft = (x <= w / 2 && dx == d) || (y >= h / 2 && dy == d);
            if (dx <= 6 && dy <= 6)
            {
                int cx = Mathf.Abs(dx - 3), cy = Mathf.Abs(dy - 3);
                if (cx + cy == 0) return Color.white;
                if (cx + cy == 1) return new Color(1.15f, 1.1f, 0.95f, 1f);
                if (cx + cy == 2) return new Color(0.02f, 0.01f, 0.01f, 1f);
            }
            if (d == 0) return new Color(0.02f, 0.01f, 0.01f, 1f);
            if (d == 1) return topLeft ? new Color(1.1f, 1.05f, 0.95f, 1f) : new Color(0.62f, 0.58f, 0.55f, 1f);
            if (d == 2) return new Color(0.82f, 0.8f, 0.78f, 1f);
            if (d == 3) return new Color(0.08f, 0.05f, 0.04f, 1f);
            if (d == 4) return topLeft ? new Color(0.7f, 0.66f, 0.6f, 1f) : new Color(0.45f, 0.42f, 0.4f, 1f);
            if (d == 5) return new Color(0.02f, 0.01f, 0.01f, 0.9f);
            return Color.clear;
        });

        /// <summary>Panel overlay: transparent centre, inner shadow and bevel, studded corners (tinted gold).</summary>
        public static Sprite TrimSprite => _trim != null ? _trim : _trim = PixelSprite(24, 24, 7, (x, y, w, h) =>
        {
            int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y), d = Mathf.Min(dx, dy);
            bool corner = dx <= 4 && dy <= 4;
            if (corner)
            {
                // A diamond stud with an L-shaped bracket around it.
                int cx = Mathf.Abs(dx - 2), cy = Mathf.Abs(dy - 2);
                if (cx + cy <= 1) return Color.white;
                if (cx + cy == 2) return new Color(0f, 0f, 0f, 0.8f);
                if ((dx == 0 || dy == 0) && dx + dy <= 5) return new Color(0.75f, 0.75f, 0.75f, 1f);
            }
            if (d == 1) return new Color(0f, 0f, 0f, 0.55f);              // shadow just inside the rim
            if (d == 2) return y > h / 2 ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.2f); // bevel
            return Color.clear;
        });

        /// <summary>Button fill: lit top edge, shaded bottom edge (tinted by the button colour).</summary>
        public static Sprite BevelSprite => _bevel != null ? _bevel : _bevel = PixelSprite(16, 16, 4, (x, y, w, h) =>
        {
            // Texels can't go above 1, so the body sits a little below full colour and the top edge lights up to it.
            int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y);
            if (dx == 0 && dy == 0) return Color.clear; // clipped corner (matches the rim)
            float v = Mathf.Lerp(0.66f, 0.86f, y / (h - 1f));
            if (y == h - 2) v = 1f;    // highlight under the rim
            if (y == 1) v = 0.42f;     // shadow above the rim
            return new Color(v, v, v, 1f);
        });

        /// <summary>Button rim: dark outline, a gold line, notched corners.</summary>
        public static Sprite RimSprite => _rim != null ? _rim : _rim = PixelSprite(16, 16, 4, (x, y, w, h) =>
        {
            int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y), d = Mathf.Min(dx, dy);
            if (dx == 0 && dy == 0) return Color.clear;                    // clipped corner
            if (d == 0) return new Color(0.05f, 0.03f, 0.03f, 0.95f);
            if (d == 1) return dx == 1 && dy == 1 ? new Color(1.2f, 1.2f, 1.1f, 1f) : Color.white;
            return Color.clear;
        });

        /// <summary>A point-filtered sprite drawn texel by texel; <paramref name="border"/> texels on each side for 9-slicing.</summary>
        private static Sprite PixelSprite(int w, int h, int border, Func<int, int, int, int, Color> paint)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = paint(x, y, w, h);
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), FramePixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite _white;
        /// <summary>Plain white (filled images: radial cooldowns, bars).</summary>
        public static Sprite WhiteSprite => _white != null ? _white : _white = PixelSprite(4, 4, 1, (x, y, w, h) => Color.white);

        /// <summary>A bar fill with a glossy top band and a darker bottom (tinted by the bar colour).</summary>
        public static Sprite GlossSprite => _gloss != null ? _gloss : _gloss = PixelSprite(4, 16, 0, (x, y, w, h) =>
        {
            float v = y >= h - 3 ? 1f : y >= h - 5 ? 0.92f : Mathf.Lerp(0.55f, 0.8f, y / (h - 5f));
            return new Color(v, v, v, 1f);
        });
        private static Sprite _gloss;

        private static Sprite _ring;
        private static Sprite _arrow;
        private static Sprite _disc;

        /// <summary>A thick ring (for radial progress with Image.Type.Filled).</summary>
        public static Sprite RingSprite => _ring != null ? _ring : _ring = ShapeSprite(64, (x, y, h) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return d <= h && d >= h * 0.68f;
        });

        /// <summary>A chevron pointing right (off-screen markers).</summary>
        public static Sprite ArrowSprite => _arrow != null ? _arrow : _arrow = ShapeSprite(32, (x, y, h) =>
        {
            // Filled triangle with a notch at the back.
            float u = (x + h) / (2f * h); // 0 back .. 1 tip
            float halfWidth = (1f - u) * h * 0.9f;
            return Mathf.Abs(y) <= halfWidth && !(u < 0.3f && Mathf.Abs(y) < (0.3f - u) * h * 2.5f);
        });

        private static Sprite _thinRing;
        /// <summary>A thin ring (portrait frames).</summary>
        public static Sprite ThinRingSprite => _thinRing != null ? _thinRing : _thinRing = ShapeSprite(128, (x, y, h) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return d <= h && d >= h * 0.9f;
        });

        private static Sprite _glow;
        /// <summary>A soft round glow: full in the middle, fading smoothly to nothing at the edge.</summary>
        public static Sprite GlowSprite
        {
            get
            {
                if (_glow != null) return _glow;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f));
                    float a = (1f - d) * (1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                return _glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        public static Sprite DiscSprite => _disc != null ? _disc : _disc = ShapeSprite(32, (x, y, h) => x * x + y * y <= h * h);

        private static Sprite ShapeSprite(int size, Func<float, float, float, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 4x supersampling for smooth edges.
                int hits = 0;
                for (int s = 0; s < 4; s++)
                    if (inside(x + 0.25f + (s % 2) * 0.5f - h, y + 0.25f + (s / 2) * 0.5f - h, h)) hits++;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Stacks two text blocks (description, then comparison) from <paramref name="top"/> down, shrinking the type
        /// until both end above <paramref name="bottom"/> - long legendary texts never run under the buttons.
        /// Both rects must be anchored top-left with their width set.
        /// </summary>
        public static void Flow(Text first, Text second, float top, float bottom, int firstSize, int secondSize, float gap = 14f)
        {
            for (int shrink = 0; shrink <= 8; shrink++)
            {
                first.fontSize = Mathf.Max(14, firstSize - shrink);
                second.fontSize = Mathf.Max(13, secondSize - shrink);
                float h1 = first.preferredHeight, h2 = string.IsNullOrEmpty(second.text) ? 0f : second.preferredHeight;
                var p1 = first.rectTransform.anchoredPosition;
                first.rectTransform.anchoredPosition = new Vector2(p1.x, top);
                first.rectTransform.sizeDelta = new Vector2(first.rectTransform.sizeDelta.x, h1);
                var p2 = second.rectTransform.anchoredPosition;
                second.rectTransform.anchoredPosition = new Vector2(p2.x, top - h1 - gap);
                second.rectTransform.sizeDelta = new Vector2(second.rectTransform.sizeDelta.x, h2);
                if (top - h1 - gap - h2 >= bottom) return;
            }
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        public static void Toggle(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }

        public static void SafeInvoke(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
