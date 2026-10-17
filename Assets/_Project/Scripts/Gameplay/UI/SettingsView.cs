using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Settings (from the menu and the title screen): volumes, screen shake, fewer flashes, interface size and the
    /// keyboard keys. Everything applies at once and is saved on this device.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static int ClosedFrame { get; private set; } = -1;
        private static SettingsView _instance;

        private GameObject _root;
        private readonly List<Action> _refresh = new();
        private KeyAction? _capturing;
        private Text _note;

        public static void Open()
        {
            if (_instance == null) return;
            UiSound.Play(UiCue.Open);
            _instance._root.SetActive(true);
            IsOpen = true;
            _instance.Refresh();
        }

        private void Awake()
        {
            _instance = this;
            Build();
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            IsOpen = false;
        }

        private void Start()
        {
            if (CommandLine.Has("-opensettings")) Open(); // screenshots
        }

        private void Build()
        {
            var canvas = UIKit.CreateCanvas("Settings", 59, transform).transform;
            var dim = UIKit.Panel(canvas, "Dim", new Color(0f, 0f, 0f, 0.72f));
            dim.rectTransform.Stretch();
            _root = dim.gameObject;
            var panel = UIKit.Framed(dim.transform, "Settings", new Color(0.065f, 0.05f, 0.06f, 0.98f));
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1180f, 980f), Vector2.zero);
            var p = panel.transform;
            UIKit.Title(p, "設定", 60, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 80f), new Vector2(44f, -20f));

            float y = -120f;
            Header(p, "音", ref y);
            Volume(p, "音楽", () => AudioPrefs.Music, v => AudioPrefs.Music = v, ref y);
            Volume(p, "効果音", () => AudioPrefs.Sfx, v => AudioPrefs.Sfx = v, ref y);
            Header(p, "画面", ref y);
            Choice(p, "画面の揺れ", new[] { "なし", "弱", "強" }, new[] { 0f, 0.5f, 1f }, () => GameSettings.Shake, v => GameSettings.Shake = v, ref y);
            Choice(p, "光の点滅", new[] { "通常", "弱める" }, new[] { 0f, 1f }, () => GameSettings.ReduceFlashes ? 1f : 0f, v => GameSettings.ReduceFlashes = v > 0.5f, ref y);
            Choice(p, "明るさ", new[] { "暗め", "ノーマル", "明るめ", "最大" }, new[] { 0.85f, 1f, 1.2f, 1.45f }, () => GameSettings.Brightness, v => GameSettings.Brightness = v, ref y);
            Choice(p, "画質",new[] { "高", "低" }, new[] { 1f, 0f }, () => GameSettings.HighQuality ? 1f : 0f, v => GameSettings.HighQuality = v > 0.5f, ref y);
            Choice(p, "表示の大きさ", new[] { "100%", "115%", "130%" }, new[] { 1f, 1.15f, 1.3f }, () => GameSettings.UiScale, v => GameSettings.UiScale = v, ref y);
            Header(p, "キーの割り当て（キーボード）", ref y);
            var actions = (KeyAction[])Enum.GetValues(typeof(KeyAction));
            for (int i = 0; i < actions.Length; i++)
            {
                var a = actions[i];
                float x = i % 2 == 0 ? 60f : 600f;
                float ry = y - (i / 2) * 52f;
                UIKit.Label(p, GameSettings.ActionName(a), 26, TextAnchor.MiddleLeft, Color.white).rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 50f), new Vector2(x, ry));
                var button = UIKit.Button(p, "", 26, () => StartCapture(a));
                button.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(200f, 50f), new Vector2(x + 210f, ry));
                var label = button.GetComponentInChildren<Text>();
                _refresh.Add(() => label.text = _capturing == a ? "<color=#e0b45a>キーを押す…</color>" : GameSettings.Label(a));
            }
            // The bottom row: reset the keys on the left (with a note beside it), close on the right.
            var reset = UIKit.Button(p, "キーを初期設定に戻す", 22, () => { GameSettings.ResetKeys(); Refresh(); });
            reset.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(300f, 52f), new Vector2(60f, 28f));
            _note = UIKit.Label(p, "", 20, TextAnchor.MiddleLeft, UIKit.MutedText);
            _note.rectTransform.Place(new Vector2(0f, 0f), new Vector2(460f, 52f), new Vector2(380f, 28f));

            var close = UIKit.Button(p, "閉じる（Esc）", 26, Close);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(260f, 60f), new Vector2(-40f, 24f));
        }

        private static void Header(Transform p, string text, ref float y)
        {
            var h = UIKit.Title(p, text, 30, TextAnchor.MiddleLeft);
            h.rectTransform.Place(new Vector2(0f, 1f), new Vector2(700f, 44f), new Vector2(44f, y));
            var rule = UIKit.Panel(p, "Rule", UIKit.GoldTrim);
            rule.raycastTarget = false;
            rule.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1090f, 2f), new Vector2(44f, y - 46f));
            y -= 52f;
        }

        /// <summary>A volume row: − / ten segments / +.</summary>
        private void Volume(Transform p, string name, Func<float> get, Action<float> set, ref float y)
        {
            UIKit.Label(p, name, 26, TextAnchor.MiddleLeft, Color.white).rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 50f), new Vector2(60f, y));
            var minus = UIKit.Button(p, "−", 30, () => { set(Mathf.Round(get() * 10f - 1f) / 10f); Refresh(); });
            minus.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(56f, 50f), new Vector2(270f, y));
            var segments = new Image[10];
            for (int i = 0; i < 10; i++)
            {
                segments[i] = UIKit.Panel(p, "Seg", Color.white);
                segments[i].sprite = UIKit.GlossSprite;
                segments[i].raycastTarget = false;
                segments[i].rectTransform.Place(new Vector2(0f, 1f), new Vector2(40f, 30f), new Vector2(340f + i * 46f, y - 10f));
            }
            var plus = UIKit.Button(p, "+", 30, () => { set(Mathf.Round(get() * 10f + 1f) / 10f); Refresh(); });
            plus.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(56f, 50f), new Vector2(810f, y));
            var value = UIKit.Label(p, "", 24, TextAnchor.MiddleLeft, UIKit.MutedText);
            value.rectTransform.Place(new Vector2(0f, 1f), new Vector2(120f, 50f), new Vector2(880f, y));
            _refresh.Add(() =>
            {
                int n = Mathf.RoundToInt(get() * 10f);
                for (int i = 0; i < 10; i++) segments[i].color = i < n ? new Color(0.86f, 0.66f, 0.3f) : new Color(0.16f, 0.13f, 0.12f);
                value.text = $"{n * 10}%";
            });
            y -= 54f;
        }

        /// <summary>A row of options; the current one is lit.</summary>
        private void Choice(Transform p, string name, string[] labels, float[] values, Func<float> get, Action<float> set, ref float y)
        {
            UIKit.Label(p, name, 26, TextAnchor.MiddleLeft, Color.white).rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 50f), new Vector2(60f, y));
            var fills = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                float v = values[i];
                var b = UIKit.Button(p, labels[i], 24, () => { set(v); Refresh(); });
                b.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(150f, 50f), new Vector2(270f + i * 162f, y));
                fills[i] = b.GetComponent<Image>();
            }
            _refresh.Add(() =>
            {
                float now = get();
                for (int i = 0; i < fills.Length; i++)
                    fills[i].color = Mathf.Abs(now - values[i]) < 0.01f ? new Color(0.62f, 0.38f, 0.16f) : UIKit.ButtonColor;
            });
            y -= 54f;
        }

        private void StartCapture(KeyAction a)
        {
            _capturing = a;
            EventSystem.current?.SetSelectedGameObject(null); // Space or Enter must not press the button again
            _note.text = "割り当てたいキーを押してください（Esc でやめる）";
            Refresh();
        }

        private void Refresh()
        {
            foreach (var r in _refresh) r();
        }

        private void Close()
        {
            UiSound.Play(UiCue.Close);
            _capturing = null;
            _root.SetActive(false);
            IsOpen = false;
            ClosedFrame = Time.frameCount;
        }

        // Keys a player can't take for these: moving, the menu.
        private static readonly HashSet<Key> Reserved = new()
        {
            Key.W, Key.A, Key.S, Key.D, Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.Escape,
        };

        private void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (_capturing.HasValue)
            {
                if (kb == null) return;
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    _capturing = null;
                    _note.text = "";
                    Refresh();
                    return;
                }
                foreach (var control in kb.allKeys)
                {
                    if (control == null || !control.wasPressedThisFrame) continue;
                    if (Reserved.Contains(control.keyCode))
                    {
                        _note.text = "そのキーは移動・メニュー用なので割り当てられません";
                        return;
                    }
                    GameSettings.SetKey(_capturing.Value, control.keyCode);
                    _capturing = null;
                    _note.text = "";
                    Refresh();
                    EventSystem.current?.SetSelectedGameObject(null);
                    return;
                }
                return;
            }
            var pad = Gamepad.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame) Close();
        }
    }
}
