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
        public static readonly Color PanelColor = new(0.08f, 0.08f, 0.12f, 1f);
        public static readonly Color ButtonColor = new(0.22f, 0.24f, 0.32f, 1f);
        public static readonly Color AccentColor = new(0.95f, 0.75f, 0.3f, 1f);

        private static Font _font;

        public static Font Font
        {
            get
            {
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

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
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

        public static Button Button(Transform parent, string text, int fontSize, UnityAction onClick, Color? color = null)
        {
            var image = Panel(parent, "Button", color ?? ButtonColor);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.selectedColor = new Color(1.35f, 1.3f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Label(image.transform, text, fontSize, TextAnchor.MiddleCenter).rectTransform.Stretch(8f);
            return button;
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
