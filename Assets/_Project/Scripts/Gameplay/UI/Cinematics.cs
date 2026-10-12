using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Short screen moments drawn over the HUD (no time stops: it's online): the title card of a new area (a black
    /// blink that also hides the jump, then the layer and the area's name between gold rules), and a lord's entrance
    /// (black bars slide in, the name in large letters, a red edge pulse).
    /// </summary>
    public sealed class Cinematics
    {
        private readonly Image _black;
        private readonly Image _barTop, _barBottom;
        private readonly Image _redEdge;
        private readonly Text _title, _sub;
        private readonly Image[] _rules = new Image[2];
        private readonly CanvasGroup _card;

        private enum Kind { None, Area, Lord }
        private Kind _kind;
        private float _start;

        public Cinematics(Transform parent)
        {
            var canvas = UIKit.CreateCanvas("Cinematics", 45, parent).transform;
            _redEdge = UIKit.Panel(canvas, "RedEdge", new Color(0.6f, 0.02f, 0.02f, 0f));
            _redEdge.sprite = EdgeSprite;
            _redEdge.rectTransform.Stretch();
            _black = UIKit.Panel(canvas, "Black", new Color(0f, 0f, 0f, 0f));
            _black.rectTransform.Stretch();
            _barTop = Bar(canvas, true);
            _barBottom = Bar(canvas, false);
            var card = UIKit.Rect("Card", canvas).Place(new Vector2(0.5f, 0.5f), new Vector2(1600f, 300f), new Vector2(0f, 40f));
            _card = card.gameObject.AddComponent<CanvasGroup>();
            _card.alpha = 0f;
            _sub = UIKit.Label(card, "", 34, TextAnchor.MiddleCenter, UIKit.MutedText);
            _sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1600f, 50f), new Vector2(0f, 74f));
            _title = UIKit.Title(card, "", 104, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1600f, 140f), new Vector2(0f, -10f));
            var outline = _title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.04f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(4f, -4f);
            for (int i = 0; i < 2; i++)
            {
                _rules[i] = UIKit.Panel(card, "Rule", UIKit.AccentColor);
                _rules[i].raycastTarget = false;
                _rules[i].rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(0f, i == 0 ? 104f : -92f));
            }
            foreach (var img in new[] { _redEdge, _black, _barTop, _barBottom }) img.raycastTarget = false;
        }

        private static Image Bar(Transform canvas, bool top)
        {
            var bar = UIKit.Panel(canvas, top ? "BarTop" : "BarBottom", Color.black);
            var r = bar.rectTransform;
            r.anchorMin = new Vector2(0f, top ? 1f : 0f);
            r.anchorMax = new Vector2(1f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, top ? 0f : 1f); // starts just off screen
            r.sizeDelta = new Vector2(0f, 130f);
            r.anchoredPosition = Vector2.zero;
            return bar;
        }

        public bool Playing => _kind != Kind.None;

        /// <summary>A new area: black blink, then its layer and name.</summary>
        public void Area(string layer, string name)
        {
            _kind = Kind.Area;
            _start = Time.time;
            _sub.text = layer;
            _title.text = name;
            _title.color = UIKit.AccentColor;
        }

        /// <summary>A lord walks in: bars, name, red edge.</summary>
        public void Lord(string name, string title)
        {
            _kind = Kind.Lord;
            _start = Time.time;
            _sub.text = title;
            _title.text = name;
            _title.color = new Color(1f, 0.4f, 0.32f);
        }

        public void Update()
        {
            float t = Time.time - _start;
            float black = 0f, bars = 0f, card = 0f, red = 0f, rule = 0f;
            switch (_kind)
            {
                case Kind.Area:
                    // In fast, hold, then the picture comes back while the name stays a little longer.
                    black = t < 0.15f ? t / 0.15f : t < 0.55f ? 1f : Mathf.Clamp01(1f - (t - 0.55f) / 0.8f);
                    card = Mathf.Clamp01((t - 0.2f) / 0.35f) * Mathf.Clamp01((3.2f - t) / 0.6f);
                    rule = Mathf.Clamp01((t - 0.25f) / 0.5f);
                    if (t > 3.2f) _kind = Kind.None;
                    break;
                case Kind.Lord:
                    bars = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((3.2f - t) / 0.4f);
                    card = Mathf.Clamp01((t - 0.3f) / 0.4f) * Mathf.Clamp01((3f - t) / 0.5f);
                    rule = Mathf.Clamp01((t - 0.35f) / 0.5f);
                    red = Mathf.Clamp01(1f - t / 1.6f) * (0.6f + 0.4f * Mathf.Sin(t * 12f));
                    if (t > 3.2f) _kind = Kind.None;
                    break;
            }
            _black.color = new Color(0f, 0f, 0f, black); // the blink hides the jump: not softened by the flash setting
            _barTop.rectTransform.anchoredPosition = new Vector2(0f, -130f * bars);
            _barBottom.rectTransform.anchoredPosition = new Vector2(0f, 130f * bars);
            _card.alpha = card;
            _card.transform.localScale = Vector3.one * (1.06f - 0.06f * Mathf.Clamp01(card * 1.5f));
            _redEdge.color = new Color(0.6f, 0.02f, 0.02f, red * 0.55f * GameSettings.FlashScale);
            float ruleWidth = 900f * rule;
            foreach (var r in _rules) r.rectTransform.sizeDelta = new Vector2(ruleWidth, 3f);
        }

        private static Sprite _edge;

        /// <summary>Clear in the middle, opaque at the screen edges (a vignette).</summary>
        private static Sprite EdgeSprite
        {
            get
            {
                if (_edge != null) return _edge;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) - 0.55f) / 0.45f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(d * d * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                return _edge = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
