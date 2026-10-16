using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// On-screen controls for phones and tablets. A stick appears wherever a thumb lands on the left half; the right
    /// side has attack (hold), the two skills and the dodge with their icons and cooldowns; a row at the top opens
    /// the bag, map, ping, codex and menu. In the camp the attack button becomes "調べる". Shown on mobile platforms,
    /// or once the screen is touched (hidden again when a mouse or keyboard is used). Runs before the input readers.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class TouchControls : MonoBehaviour
    {
        private const float StickRadius = 120f;

        private sealed class Pad
        {
            public RectTransform Rect;
            public float Radius;
            public TouchButton? Press;   // one-frame press
            public bool Attack;          // held
            public Image Icon;
            public Image Cool;
            public Text Label;
        }

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _stickBase, _stickKnob;
        private Vector2 _stickHome = new(260f, 260f);
        private int _stickTouch = -1;
        private Vector2 _stickOrigin;
        private readonly List<Pad> _pads = new();
        private readonly Dictionary<int, Pad> _held = new();
        private Pad _attack, _skill1, _skill2, _dodge, _interact;
        private string _iconsFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("TouchControls");
            DontDestroyOnLoad(go);
            go.AddComponent<TouchControls>();
        }

        private void Awake()
        {
            _canvas = UIKit.CreateCanvas("Touch", 35, transform);
            _root = (RectTransform)_canvas.transform;
            TouchInput.Active = Application.isMobilePlatform || CommandLine.Has("-touchui");

            _stickBase = Circle(_root, "StickBase", 260f, new Color(1f, 1f, 1f, 0.12f), UIKit.ThinRingSprite);
            _stickKnob = Circle(_stickBase, "Knob", 110f, new Color(0.95f, 0.85f, 0.65f, 0.35f), UIKit.DiscSprite);
            _stickBase.anchorMin = _stickBase.anchorMax = Vector2.zero;
            _stickBase.anchoredPosition = _stickHome;

            _attack = AddPad(new Vector2(-210f, 230f), 100f, null, true, "攻撃");
            _skill1 = AddPad(new Vector2(-440f, 170f), 70f, TouchButton.Skill1, false, null);
            _skill2 = AddPad(new Vector2(-380f, 390f), 70f, TouchButton.Skill2, false, null);
            _dodge = AddPad(new Vector2(-180f, 470f), 64f, TouchButton.Dodge, false, null);
            _interact = AddPad(new Vector2(-210f, 230f), 100f, TouchButton.Interact, false, "調べる");
            var row = new[] { (TouchButton.Bag, "袋"), (TouchButton.Map, "地図"), (TouchButton.Ping, "合図"), (TouchButton.Codex, "図鑑"), (TouchButton.Menu, "≡") };
            for (int i = 0; i < row.Length; i++)
            {
                var pad = AddPad(Vector2.zero, 44f, row[i].Item1, false, row[i].Item2);
                pad.Rect.anchorMin = pad.Rect.anchorMax = new Vector2(0.5f, 1f);
                pad.Rect.anchoredPosition = new Vector2((i - (row.Length - 1) * 0.5f) * 104f, -64f);
            }
        }

        private static RectTransform Circle(Transform parent, string name, float size, Color color, Sprite sprite)
        {
            var img = UIKit.Panel(parent, name, color);
            img.sprite = sprite;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            return r;
        }

        private Pad AddPad(Vector2 fromBottomRight, float radius, TouchButton? press, bool attack, string label)
        {
            var back = Circle(_root, "Pad", radius * 2f, new Color(0.08f, 0.06f, 0.07f, 0.55f), UIKit.DiscSprite);
            back.anchorMin = back.anchorMax = new Vector2(1f, 0f);
            back.anchoredPosition = fromBottomRight;
            var rim = Circle(back, "Rim", radius * 2f, new Color(0.86f, 0.68f, 0.32f, 0.8f), UIKit.ThinRingSprite);
            var icon = UIKit.Panel(back, "Icon", Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(radius * 1.25f, radius * 1.25f);
            icon.enabled = false;
            var cool = UIKit.Panel(back, "Cool", new Color(0f, 0f, 0f, 0.6f));
            cool.sprite = UIKit.DiscSprite;
            cool.type = Image.Type.Filled;
            cool.fillMethod = Image.FillMethod.Radial360;
            cool.fillOrigin = (int)Image.Origin360.Top;
            cool.fillClockwise = false;
            cool.fillAmount = 0f;
            cool.raycastTarget = false;
            cool.rectTransform.anchorMin = cool.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cool.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            var text = UIKit.Title(back, label ?? "", label != null && label.Length > 1 ? (int)(radius * 0.5f) : (int)(radius * 0.7f), TextAnchor.MiddleCenter, Color.white);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            var pad = new Pad { Rect = back, Radius = radius, Press = press, Attack = attack, Icon = icon, Cool = cool, Label = text };
            _pads.Add(pad);
            return pad;
        }

        private static bool PanelOpen => PauseMenuView.IsOpen || BagView.IsOpen || CodexView.IsOpen || SettingsView.IsOpen || PerkView.IsShowing || CampView.PanelOpen;

        private void Update()
        {
            DetectDevice();
            var local = NetPlayer.Local;
            bool inPlay = local != null && (PlayArea.InCamp || RunDirector.Instance != null && RunDirector.Instance.IsRunning);
            bool show = TouchInput.Active && inPlay && !PanelOpen;
            _root.gameObject.SetActive(show);
            TouchInput.Move = Vector2.zero;
            TouchInput.AttackHeld = false;
            if (!show)
            {
                _stickTouch = -1;
                _held.Clear();
                return;
            }

            bool camp = PlayArea.InCamp;
            int topIndex = 0;
            foreach (var p in _pads)
            {
                bool combat = p == _attack || p == _skill1 || p == _skill2 || p == _dodge;
                bool runOnly = p.Press is TouchButton.Bag or TouchButton.Map or TouchButton.Ping;
                bool on = p == _interact ? camp : combat ? !camp : !(camp && runOnly);
                p.Rect.gameObject.SetActive(on);
                if (!on || combat || p == _interact) continue;
                // The small buttons: a row at the top centre in a run; in the camp (codex, menu) the top-right corner, clear of its signs.
                p.Rect.anchorMin = p.Rect.anchorMax = camp ? new Vector2(1f, 1f) : new Vector2(0.5f, 1f);
                p.Rect.anchoredPosition = camp ? new Vector2(-70f - (1 - topIndex) * 104f, -70f) : new Vector2((topIndex - 2) * 104f, -64f);
                topIndex++;
            }
            UpdateIcons(local);
            ReadTouches();
        }

        /// <summary>A touch switches to these controls; a mouse move or a key press switches back (desktops with touch screens).</summary>
        private static void DetectDevice()
        {
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) TouchInput.Active = true;
            if (Application.isMobilePlatform || Forced) return;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame) && (ts == null || !ts.primaryTouch.press.isPressed)) TouchInput.Active = false;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) TouchInput.Active = false;
        }

        private static bool? _forced;
        /// <summary>-touchui (screenshots on a desktop): the controls stay on.</summary>
        private static bool Forced => _forced ??= CommandLine.Has("-touchui");

        private void UpdateIcons(NetPlayer local)
        {
            string key = local.Class.Value.ToString();
            if (key != _iconsFor)
            {
                _iconsFor = key;
                var icons = SpriteSheet.Load("icons_skill")?.Sprites("default");
                if (icons != null)
                {
                    var cls = local.Class.Value;
                    Set(_attack, icons[Mathf.Min(ClassCatalog.SkillIcon(cls, 0), icons.Length - 1)]);
                    Set(_skill1, icons[Mathf.Min(ClassCatalog.SkillIcon(cls, 1), icons.Length - 1)]);
                    Set(_skill2, icons[Mathf.Min(ClassCatalog.SkillIcon(cls, 2), icons.Length - 1)]);
                    Set(_dodge, icons[Mathf.Min(ClassCatalog.DodgeIcon, icons.Length - 1)]);
                }
            }
            var def = local.ClassDef;
            Cool(_skill1, local.Skill1Cooldown.Value, def.Skill1Cooldown);
            Cool(_skill2, local.Skill2Cooldown.Value, def.Skill2Cooldown);
            Cool(_dodge, local.DodgeCharges.Value > 0 ? 0f : local.DodgeCooldown.Value, ClassCatalog.DodgeRechargeTime); // greys out only with no dodge left
        }

        private static void Set(Pad p, Sprite s)
        {
            p.Icon.sprite = s;
            p.Icon.enabled = s != null;
            if (s != null) p.Label.text = "";
        }

        private static void Cool(Pad p, float remaining, float max) => p.Cool.fillAmount = remaining > 0.05f ? Mathf.Clamp01(remaining / Mathf.Max(0.1f, max)) : 0f;

        private void ReadTouches()
        {
            var ts = Touchscreen.current;
            float scale = _canvas.scaleFactor;
            if (ts != null)
                foreach (TouchControl touch in ts.touches)
                {
                    int id = touch.touchId.ReadValue();
                    Vector2 pos = touch.position.ReadValue();
                    bool down = touch.press.isPressed;
                    if (touch.press.wasPressedThisFrame) Begin(id, pos, scale);
                    if (!down)
                    {
                        if (id == _stickTouch) _stickTouch = -1;
                        _held.Remove(id);
                        continue;
                    }
                    if (id == _stickTouch)
                    {
                        Vector2 d = (pos - _stickOrigin) / scale;
                        TouchInput.Move = Vector2.ClampMagnitude(d / StickRadius, 1f);
                        _stickKnob.anchoredPosition = Vector2.ClampMagnitude(d, StickRadius);
                    }
                    if (_held.TryGetValue(id, out var pad) && pad.Attack) TouchInput.AttackHeld = true;
                }
            if (_stickTouch < 0)
            {
                _stickBase.anchoredPosition = _stickHome;
                _stickKnob.anchoredPosition = Vector2.zero;
            }
            foreach (var p in _pads)
                p.Rect.localScale = Vector3.one * (_held.ContainsValue(p) ? 0.92f : 1f);
        }

        private void Begin(int id, Vector2 pos, float scale)
        {
            foreach (var p in _pads)
            {
                if (!p.Rect.gameObject.activeInHierarchy) continue;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, p.Rect.position);
                if ((pos - center).sqrMagnitude > p.Radius * scale * p.Radius * scale * 1.2f) continue;
                _held[id] = p;
                if (p.Press.HasValue) TouchInput.Press(p.Press.Value);
                return;
            }
            if (pos.x < Screen.width * 0.45f && _stickTouch < 0)
            {
                _stickTouch = id;
                _stickOrigin = pos;
                _stickBase.anchoredPosition = pos / scale;
            }
        }
    }
}
