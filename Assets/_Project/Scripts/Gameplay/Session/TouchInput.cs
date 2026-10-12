using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    public enum TouchButton : byte
    {
        Skill1,
        Skill2,
        Dodge,
        Bag,
        Map,
        Ping,
        Codex,
        Menu,
        Interact,
    }

    /// <summary>
    /// What the on-screen touch controls (TouchControls) report this frame: the virtual stick, the attack button
    /// held, and one-frame button presses. Readers (PlayerInputReader, GameSettings.Pressed, the menu, the camp) fold
    /// it in next to the keyboard and the pad.
    /// </summary>
    public static class TouchInput
    {
        /// <summary>The touch controls are in use (a phone or tablet, or the last input was a touch).</summary>
        public static bool Active;
        public static Vector2 Move;
        public static bool AttackHeld;

        private static readonly Dictionary<TouchButton, int> PressedFrame = new();

        public static bool Pressed(TouchButton b) => PressedFrame.TryGetValue(b, out int f) && f == Time.frameCount;

        public static void Press(TouchButton b) => PressedFrame[b] = Time.frameCount;

        public static TouchButton? For(KeyAction a) => a switch
        {
            KeyAction.Bag => TouchButton.Bag,
            KeyAction.Map => TouchButton.Map,
            KeyAction.Ping => TouchButton.Ping,
            KeyAction.Codex => TouchButton.Codex,
            _ => null,
        };
    }
}
