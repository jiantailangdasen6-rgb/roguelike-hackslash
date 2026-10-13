using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>The keys a player can change in the settings.</summary>
    public enum KeyAction : byte
    {
        Skill1,
        Skill2,
        Dodge,
        Bag,
        Map,
        Ping,
        Codex,
    }

    /// <summary>
    /// Settings of this device (PlayerPrefs): screen shake, fewer flashes, interface size and the keyboard keys of
    /// the skills and panels. Volumes live in <see cref="AudioPrefs"/>. <see cref="Changed"/> fires on every change.
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        private static readonly Dictionary<KeyAction, Key> Defaults = new()
        {
            [KeyAction.Skill1] = Key.Q,
            [KeyAction.Skill2] = Key.E,
            [KeyAction.Dodge] = Key.Space,
            [KeyAction.Bag] = Key.I,
            [KeyAction.Map] = Key.M,
            [KeyAction.Ping] = Key.F,
            [KeyAction.Codex] = Key.B,
        };

        public static string ActionName(KeyAction a) => a switch
        {
            KeyAction.Skill1 => "スキル1",
            KeyAction.Skill2 => "スキル2",
            KeyAction.Dodge => "回避",
            KeyAction.Bag => "袋",
            KeyAction.Map => "地図",
            KeyAction.Ping => "合図",
            _ => "図鑑",
        };

        /// <summary>Screen shake strength, 0..1 (default full).</summary>
        public static float Shake
        {
            get => PlayerPrefs.GetFloat("set_shake", 1f);
            set { PlayerPrefs.SetFloat("set_shake", Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        /// <summary>Softer big flashes (explosions, holy light, the black blink at doors).</summary>
        public static bool ReduceFlashes
        {
            get => PlayerPrefs.GetInt("set_noflash", 0) == 1;
            set { PlayerPrefs.SetInt("set_noflash", value ? 1 : 0); Changed?.Invoke(); }
        }

        /// <summary>Interface size: 1, 1.15 or 1.3.</summary>
        public static float UiScale
        {
            get => PlayerPrefs.GetFloat("set_uiscale", 1f);
            set { PlayerPrefs.SetFloat("set_uiscale", Mathf.Clamp(value, 1f, 1.3f)); Changed?.Invoke(); }
        }

        /// <summary>
        /// Picture quality: high shades the art with the lights (normal maps), casts shadows and draws the drifting
        /// fog; low keeps the flat look for slow machines. Phones start low.
        /// </summary>
        public static bool HighQuality
        {
            get => CommandLine.Has("-lowquality") ? false : CommandLine.Has("-highquality") || PlayerPrefs.GetInt("set_quality", Application.isMobilePlatform ? 0 : 1) == 1;
            set { PlayerPrefs.SetInt("set_quality", value ? 1 : 0); Changed?.Invoke(); }
        }

        /// <summary>Flash strength to multiply effects by (1, or a soft 0.35 with fewer flashes).</summary>
        public static float FlashScale => ReduceFlashes ? 0.35f : 1f;

        public static Key KeyFor(KeyAction a) =>
            Enum.TryParse(PlayerPrefs.GetString("key_" + a, ""), out Key k) && k != Key.None ? k : Defaults[a];

        public static void SetKey(KeyAction a, Key key)
        {
            // A key does one thing: whatever had it takes this action's old key.
            var old = KeyFor(a);
            foreach (KeyAction other in Enum.GetValues(typeof(KeyAction)))
                if (other != a && KeyFor(other) == key) PlayerPrefs.SetString("key_" + other, old.ToString());
            PlayerPrefs.SetString("key_" + a, key.ToString());
            Changed?.Invoke();
        }

        public static void ResetKeys()
        {
            foreach (KeyAction a in Enum.GetValues(typeof(KeyAction))) PlayerPrefs.DeleteKey("key_" + a);
            Changed?.Invoke();
        }

        /// <summary>The key was pressed this frame (false without a keyboard).</summary>
        public static bool Pressed(KeyAction a)
        {
            if (TouchInput.For(a) is TouchButton b && TouchInput.Pressed(b)) return true; // the on-screen button
            var kb = Keyboard.current;
            return kb != null && kb[KeyFor(a)].wasPressedThisFrame;
        }

        /// <summary>Short label for a key cap ("Q", "Space", "Shift"...).</summary>
        public static string Label(KeyAction a) => KeyLabel(KeyFor(a));

        public static string KeyLabel(Key k) => k switch
        {
            Key.Space => "Space",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LeftCtrl or Key.RightCtrl => "Ctrl",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Backquote => "`",
            >= Key.Digit1 and <= Key.Digit0 => k == Key.Digit0 ? "0" : ((int)k - (int)Key.Digit1 + 1).ToString(),
            _ => k.ToString().ToUpperInvariant(),
        };

        /// <summary>Input System control path of a keyboard key ("&lt;Keyboard&gt;/q").</summary>
        public static string Path(Key k)
        {
            var kb = Keyboard.current;
            string name = kb != null ? kb[k].name : char.ToLowerInvariant(k.ToString()[0]) + k.ToString().Substring(1);
            return "<Keyboard>/" + name;
        }
    }
}
