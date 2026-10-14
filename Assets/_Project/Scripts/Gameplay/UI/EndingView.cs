using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The true ending (the Ashen King fell, RunEnded Int = 3): the screen goes black, the ending's words rise one line
    /// at a time with embers drifting up, then the staff roll scrolls by. It sits over the result screen and fades
    /// away to it; after a few seconds any key, click or touch skips it. Every peer plays it for itself.
    /// </summary>
    public sealed class EndingView : MonoBehaviour
    {
        private const float LineGap = 3.4f, Fade = 1.4f, RollSpeed = 46f;

        private CanvasGroup _group;
        private Image _black;
        private readonly List<Text> _lines = new();
        private RectTransform _roll;
        private readonly List<(RectTransform Rect, Image Image, float Speed, float Phase)> _embers = new();
        private float _start = -1f;
        private float _end = -1f;

        public static bool Showing { get; private set; }

        private void Awake()
        {
            var canvas = UIKit.CreateCanvas("Ending", 70, transform).transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _black = UIKit.Panel(canvas, "Black", Color.black);
            _black.rectTransform.Stretch();
            for (int i = 0; i < 26; i++)
            {
                var e = UIKit.Panel(canvas, "Ember", new Color(1f, 0.6f, 0.2f, 0f));
                e.raycastTarget = false;
                float size = Random.Range(3f, 7f);
                e.rectTransform.Place(new Vector2(Random.value, 0f), new Vector2(size, size), new Vector2(0f, -20f));
                _embers.Add((e.rectTransform, e, Random.Range(30f, 80f), Random.value * 10f));
            }
            for (int i = 0; i < Lore.Ending.Length; i++)
            {
                bool last = i == Lore.Ending.Length - 1;
                var t = last ? UIKit.Title(canvas, Lore.Ending[i], 54, TextAnchor.MiddleCenter, UIKit.AccentColor)
                             : UIKit.Label(canvas, Lore.Ending[i], 34, TextAnchor.MiddleCenter, new Color(0.92f, 0.88f, 0.8f));
                t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1600f, 70f), new Vector2(0f, 230f - i * 82f - (last ? 20f : 0f)));
                t.color = new Color(t.color.r, t.color.g, t.color.b, 0f);
                _lines.Add(t);
            }
            _roll = UIKit.Rect("Roll", canvas).Place(new Vector2(0.5f, 0f), new Vector2(1400f, 1800f), new Vector2(0f, -1800f));
            var title = UIKit.Title(_roll, "Labyrinth Survivor", 84, TextAnchor.UpperCenter, UIKit.AccentColor);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1400f, 120f), new Vector2(0f, 0f));
            var sub = UIKit.Label(_roll, "― 灯火を継ぐ者 ―", 34, TextAnchor.UpperCenter, UIKit.MutedText);
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1400f, 60f), new Vector2(0f, -120f));
            var credits = UIKit.Label(_roll, Credits, 28, TextAnchor.UpperCenter, new Color(0.9f, 0.86f, 0.78f));
            credits.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1400f, 1500f), new Vector2(0f, -230f));
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            canvas.gameObject.SetActive(false);
            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy()
        {
            RunDirector.EventReceived -= OnEvent;
            Showing = false;
        }

        /// <summary>The staff roll (the credits of Docs/credits.md, in short).</summary>
        private const string Credits =
            "<color=#e0b45a>企画・監督</color>\nこの迷宮を思い描いた人\n\n" +
            "<color=#e0b45a>実装・ドット絵・効果の加工</color>\nClaude（Anthropic）\n\n" +
            "<color=#e0b45a>音楽</color>\nOf Far Different Nature（LOOP BOX #3）\nProjectHelmet（Philémon Weber）\n\n" +
            "<color=#e0b45a>効果音</color>\nJC Sounds ・ leohpaz ・ Kenney\nartisticdude ・ StarNinjas ・ Iwan 'qubodup' Gabovitch\n\n" +
            "<color=#e0b45a>フォント</color>\nNoto Serif JP（Google / Adobe）\n解星 徳明（Font-Kai）\n\n" +
            "<color=#e0b45a>エンジン</color>\nUnity\n\n\n" +
            "そして、ともに迷宮へ潜った仲間たちへ。\n\n<size=40><color=#ffd890>遊んでくれて、ありがとう。</color></size>";

        private void OnEvent(SimEvent e)
        {
            if (e.Type != SimEventType.RunEnded || e.Int != 3) return;
            _group.gameObject.SetActive(true);
            _start = Time.unscaledTime;
            _end = -1f;
            Showing = true;
            _group.blocksRaycasts = true;
        }

        private float LinesDone => 1.5f + Lore.Ending.Length * LineGap + 3f;
        private float RollTime => (1800f + 1080f) / RollSpeed;

        private void Update()
        {
            if (_start < 0f) return;
            float t = Time.unscaledTime - _start;

            // Skip: any key, click or touch after a few seconds; the roll then fades out at once.
            bool pressed = (Keyboard.current?.anyKey.wasPressedThisFrame ?? false) || (Mouse.current?.leftButton.wasPressedThisFrame ?? false)
                           || (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false) || (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame ?? false);
            if (_end < 0f && (t > LinesDone + RollTime || (pressed && t > 4f))) _end = Time.unscaledTime;

            float alpha = Mathf.Clamp01(t / 1.5f);
            if (_end >= 0f)
            {
                alpha = 1f - Mathf.Clamp01((Time.unscaledTime - _end) / 2f);
                if (alpha <= 0f)
                {
                    _start = -1f;
                    Showing = false;
                    _group.blocksRaycasts = false;
                    _group.gameObject.SetActive(false);
                    return;
                }
            }
            _group.alpha = alpha;

            // The words, one line at a time; they fade away together before the roll.
            for (int i = 0; i < _lines.Count; i++)
            {
                float lt = t - 1.5f - i * LineGap;
                float a = Mathf.Clamp01(lt / Fade) * (1f - Mathf.Clamp01((t - LinesDone + 1.5f) / 1.5f));
                var c = _lines[i].color;
                _lines[i].color = new Color(c.r, c.g, c.b, a);
                _lines[i].rectTransform.anchoredPosition = new Vector2(0f, 230f - i * 82f - (i == _lines.Count - 1 ? 20f : 0f) + (1f - Mathf.Clamp01(lt / Fade)) * -16f);
            }
            float roll = Mathf.Max(0f, t - LinesDone);
            _roll.anchoredPosition = new Vector2(0f, -1800f + roll * RollSpeed);

            // Embers rising from the bottom of the screen.
            foreach (var (rect, image, speed, phase) in _embers)
            {
                float y = Mathf.Repeat(t * speed + phase * 100f, 1200f) - 40f;
                rect.anchoredPosition = new Vector2(Mathf.Sin(t * 0.7f + phase) * 30f, y);
                image.color = new Color(1f, 0.55f + 0.2f * Mathf.Sin(t * 3f + phase), 0.2f, 0.7f * (1f - y / 1200f));
            }
        }
    }
}
