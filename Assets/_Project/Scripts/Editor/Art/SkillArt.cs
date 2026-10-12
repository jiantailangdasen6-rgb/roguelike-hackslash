using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// 32x32 skill icons for the HUD's skill bar (sheet icons_skill): frame (int)class * 3 + slot (0 basic attack,
    /// 1 skill 1, 2 skill 2) for the six classes, then the dodge, then the bag. Each symbol sits on a dark
    /// rounded tile lit from inside in the class colour.
    /// </summary>
    public static class SkillArt
    {
        public const int Size = 32;
        public const int DodgeFrame = 18;
        public const int BagFrame = 19;

        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static readonly Color32 Ink = PixelRenderer.Outline;
        private static readonly Color32 White = C("#f6f1e8");
        private static readonly Color32 Steel = C("#b8c0ca");
        private static readonly Color32 SteelDark = C("#5c6370");
        private static readonly Color32 Gold = C("#ecc760");
        private static readonly Color32 GoldDark = C("#8a6420");
        private static readonly Color32 Red = C("#e2443c");
        private static readonly Color32 RedDark = C("#7a1a1e");
        private static readonly Color32 Blue = C("#8fd8ff");
        private static readonly Color32 BlueDark = C("#2b62a8");
        private static readonly Color32 Ice = C("#d8f6ff");
        private static readonly Color32 Green = C("#8cff9e");
        private static readonly Color32 GreenDark = C("#2c8a44");
        private static readonly Color32 Purple = C("#b884ff");
        private static readonly Color32 PurpleDark = C("#4a2a7a");
        private static readonly Color32 Amber = C("#ffb54a");
        private static readonly Color32 Wood = C("#8f6238");
        private static readonly Color32 WoodDark = C("#5a3a20");
        private static readonly Color32 Smoke = C("#8d8a94");
        private static readonly Color32 SmokeDark = C("#4d4a55");
        private static readonly Color32 Glass = C("#cfe9ef");

        private static Color32 ClassGlow(ClassId c) => c switch
        {
            ClassId.Warrior => C("#9a2a26"),
            ClassId.Mage => C("#2f5ea8"),
            ClassId.Ranger => C("#2f7a36"),
            ClassId.Guardian => C("#a08530"),
            ClassId.Assassin => C("#5a3290"),
            _ => C("#a85a1c"),
        };

        public static List<PixelCanvas> Icons()
        {
            var icons = new List<PixelCanvas>();
            foreach (ClassId cls in System.Enum.GetValues(typeof(ClassId)))
                for (int slot = 0; slot < 3; slot++)
                {
                    var c = Tile(ClassGlow(cls));
                    Draw(c, cls, slot);
                    Finish(c);
                    icons.Add(c);
                }
            var dodge = Tile(C("#3a4a5a"));
            Wing(dodge);
            Finish(dodge);
            icons.Add(dodge);
            var bag = Tile(C("#5a4020"));
            Pouch(bag);
            Finish(bag);
            icons.Add(bag);
            return icons;
        }

        // ------------------------------------------------------------------ Tile and finishing

        private static readonly Color32 Back = C("#141016");

        /// <summary>A dark rounded tile with a soft glow of <paramref name="glow"/> from the middle.</summary>
        private static PixelCanvas Tile(Color32 glow)
        {
            var c = new PixelCanvas(Size, Size);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int dx = Mathf.Min(x, Size - 1 - x), dy = Mathf.Min(y, Size - 1 - y);
                if (dx + dy < 3) continue; // rounded corners
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f;
                float t = Mathf.Clamp01(1f - d) * 0.55f;
                c.Set(x, y, Color32.Lerp(Back, glow, t));
            }
            return c;
        }

        /// <summary>Outlines the symbol (anything brighter than the tile) so it reads at a glance.</summary>
        private static void Finish(PixelCanvas c)
        {
            var copy = (Color32[])c.Pixels.Clone();
            bool IsSymbol(Color32 p) => p.a > 0 && p.r + p.g + p.b > 330;
            for (int y = 1; y < Size - 1; y++)
            for (int x = 1; x < Size - 1; x++)
            {
                var here = copy[y * Size + x];
                if (here.a == 0 || IsSymbol(here) || here.Equals(Ink)) continue;
                bool near = false;
                for (int k = 0; k < 4 && !near; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    near = IsSymbol(copy[ny * Size + nx]);
                }
                if (near) c.Set(x, y, Ink);
            }
        }

        private static void Draw(PixelCanvas c, ClassId cls, int slot)
        {
            switch (cls, slot)
            {
                case (ClassId.Warrior, 0): Slashes(c, White, 3); Sword(c, 9, 7, 23, 21); break;
                case (ClassId.Warrior, 1): SpeedLines(c, Steel); Sword(c, 6, 10, 24, 22); break;
                case (ClassId.Warrior, 2): WarCry(c); break;
                case (ClassId.Mage, 0): Orb(c, Blue, BlueDark); break;
                case (ClassId.Mage, 1): Snowflake(c); break;
                case (ClassId.Mage, 2): HolyLight(c); break;
                case (ClassId.Ranger, 0): Arrow(c, 5, 9, 23, 13); Arrow(c, 5, 15, 25, 18); Arrow(c, 5, 21, 23, 23); break;
                case (ClassId.Ranger, 1): PiercingArrow(c); break;
                case (ClassId.Ranger, 2): Reticle(c); break;
                case (ClassId.Guardian, 0): Hammer(c); break;
                case (ClassId.Guardian, 1): SpeedLines(c, Gold); Shield(c, 18, 15, 1f); break;
                case (ClassId.Guardian, 2): Sanctuary(c); break;
                case (ClassId.Assassin, 0): Dagger(c, 7, 7, 23, 24, false); Dagger(c, 25, 7, 9, 24, true); break;
                case (ClassId.Assassin, 1): ShadowStep(c); break;
                case (ClassId.Assassin, 2): SmokeBomb(c); break;
                case (ClassId.Alchemist, 0): Flask(c, 16, 12, Green, GreenDark); ThrowArc(c); break;
                case (ClassId.Alchemist, 1): Flask(c, 16, 10, Amber, Red); Flames(c, 16, 22); break;
                case (ClassId.Alchemist, 2): Flask(c, 16, 11, Red, RedDark); Plus(c, 16, 12, White); break;
            }
        }

        // ------------------------------------------------------------------ Symbols

        private static void Sword(PixelCanvas c, int x0, int y0, int x1, int y1)
        {
            // Blade from the hilt (x0,y0) to the tip (x1,y1), a crossguard and a grip.
            c.Line(x0, y0, x1, y1, Steel);
            c.Line(x0 + 1, y0, x1, y1 - 1, White);
            Vector2 dir = new Vector2(x1 - x0, y1 - y0).normalized;
            Vector2 side = new(-dir.y, dir.x);
            Vector2 guard = new Vector2(x0, y0) + dir * 2f;
            c.Line(guard.x - side.x * 4, guard.y - side.y * 4, guard.x + side.x * 4, guard.y + side.y * 4, Gold);
            c.Line(x0, y0, x0 - dir.x * 4, y0 - dir.y * 4, Wood);
            c.Disc(x0 - dir.x * 5, y0 - dir.y * 5, 1.2f, Gold);
        }

        private static void Slashes(PixelCanvas c, Color32 col, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float r = 9f + i * 3.5f;
                for (float a = 100f; a < 200f; a += 3f)
                {
                    float rad = a * Mathf.Deg2Rad;
                    c.Set(Mathf.RoundToInt(22 + Mathf.Cos(rad) * r * 0.9f), Mathf.RoundToInt(12 + Mathf.Sin(rad) * r * 0.6f), i == 0 ? col : C("#c9c2ba"));
                }
            }
        }

        private static void SpeedLines(PixelCanvas c, Color32 col)
        {
            foreach (var (y, len) in new[] { (8, 7), (13, 10), (19, 8), (24, 6) })
                c.Line(3, y, 3 + len, y, col);
        }

        private static void WarCry(PixelCanvas c)
        {
            // A horned helm shouting, with sound rings.
            c.Disc(12, 15, 6f, Steel);
            c.Rect(7, 9, 11, 4, SteelDark);
            c.Line(6, 20, 3, 26, White); c.Line(7, 20, 4, 26, White);
            c.Line(17, 20, 20, 26, White); c.Line(18, 20, 21, 26, White);
            c.Rect(10, 14, 5, 2, Ink);
            c.Rect(11, 10, 3, 3, Ink);
            for (int i = 0; i < 3; i++)
                for (float a = -40f; a <= 40f; a += 6f)
                {
                    float rad = a * Mathf.Deg2Rad, r = 5f + i * 3.5f;
                    c.Set(Mathf.RoundToInt(18 + Mathf.Cos(rad) * r), Mathf.RoundToInt(13 + Mathf.Sin(rad) * r), i == 0 ? Gold : i == 1 ? Amber : Red);
                }
        }

        private static void Orb(PixelCanvas c, Color32 col, Color32 dark)
        {
            // A bolt of magic with its trail.
            for (int i = 0; i < 9; i++) c.Disc(6 + i * 1.2f, 7 + i * 1.1f, 1.2f + i * 0.25f, Color32.Lerp(dark, col, i / 9f));
            c.Disc(19, 19, 6f, dark);
            c.Disc(19, 19, 4.8f, col);
            c.Disc(18, 20, 2.4f, Ice);
            c.Set(17, 21, White);
            foreach (var (x, y) in new[] { (26, 24), (25, 13), (13, 25) }) { c.Set(x, y, Ice); c.Set(x + 1, y, Blue); c.Set(x, y + 1, Blue); }
        }

        private static void Snowflake(PixelCanvas c)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad;
                float dx = Mathf.Cos(a) * 11f, dy = Mathf.Sin(a) * 11f;
                c.Line(16 - dx, 16 - dy, 16 + dx, 16 + dy, Ice);
                for (int s = -1; s <= 1; s += 2)
                {
                    float bx = 16 + dx * 0.6f * s, by = 16 + dy * 0.6f * s;
                    float b1 = a + 0.6f, b2 = a - 0.6f;
                    c.Line(bx, by, bx - Mathf.Cos(b1) * 3.5f * s, by - Mathf.Sin(b1) * 3.5f * s, Blue);
                    c.Line(bx, by, bx - Mathf.Cos(b2) * 3.5f * s, by - Mathf.Sin(b2) * 3.5f * s, Blue);
                }
            }
            c.Disc(16, 16, 2.4f, White);
        }

        private static void HolyLight(PixelCanvas c)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                c.Line(16 + Mathf.Cos(a) * 7f, 16 + Mathf.Sin(a) * 7f, 16 + Mathf.Cos(a) * 13f, 16 + Mathf.Sin(a) * 13f, k % 2 == 0 ? Gold : GoldDark);
            }
            c.Disc(16, 16, 6.5f, Green);
            Plus(c, 16, 16, White);
        }

        private static void Plus(PixelCanvas c, int cx, int cy, Color32 col)
        {
            c.Rect(cx - 1, cy - 4, 3, 9, col);
            c.Rect(cx - 4, cy - 1, 9, 3, col);
        }

        private static void Arrow(PixelCanvas c, int x0, int y0, int x1, int y1)
        {
            c.Line(x0, y0, x1, y1, Wood);
            Vector2 dir = new Vector2(x1 - x0, y1 - y0).normalized;
            Vector2 side = new(-dir.y, dir.x);
            Vector2 tip = new Vector2(x1, y1) + dir * 2f;
            c.Line(tip.x, tip.y, x1 - dir.x * 2 + side.x * 2.5f, y1 - dir.y * 2 + side.y * 2.5f, White);
            c.Line(tip.x, tip.y, x1 - dir.x * 2 - side.x * 2.5f, y1 - dir.y * 2 - side.y * 2.5f, Steel);
            c.Line(x0, y0, x0 - dir.x * 3 + side.x * 2, y0 - dir.y * 3 + side.y * 2, Red);
            c.Line(x0, y0, x0 - dir.x * 3 - side.x * 2, y0 - dir.y * 3 - side.y * 2, Red);
        }

        private static void PiercingArrow(PixelCanvas c)
        {
            // A glowing arrow tearing through, with a burst where it passes.
            for (int x = 3; x < 22; x++) { c.Set(x, 13, x % 3 == 0 ? Amber : Gold); c.Set(x, 19, x % 3 == 1 ? Amber : Gold); }
            Arrow(c, 4, 16, 26, 16);
            c.Line(5, 17, 25, 17, WoodDark);
            foreach (var (x, y) in new[] { (14, 9), (14, 23), (11, 10), (17, 22) }) { c.Set(x, y, White); c.Set(x + 1, y, Gold); }
        }

        private static void Reticle(PixelCanvas c)
        {
            c.Ring(16, 16, 8f, 9.5f, Red);
            c.Ring(16, 16, 3.5f, 4.6f, Gold);
            c.Rect(15, 3, 2, 7, White); c.Rect(15, 22, 2, 7, White);
            c.Rect(3, 15, 7, 2, White); c.Rect(22, 15, 7, 2, White);
            c.Disc(16, 16, 1.5f, Red);
        }

        private static void Hammer(PixelCanvas c)
        {
            c.Line(8, 6, 20, 22, Wood);
            c.Line(9, 6, 21, 22, WoodDark);
            // The head, square across the top of the haft.
            for (int i = -6; i <= 6; i++)
                for (int k = -3; k <= 3; k++)
                    c.Set(20 + i + k / 2, 22 + k - i / 2, k > 1 ? SteelDark : i < -4 || i > 4 ? Gold : Steel);
            c.Disc(7, 5, 1.6f, Gold);
        }

        private static void Shield(PixelCanvas c, int cx, int cy, float scale)
        {
            for (int y = -9; y <= 9; y++)
            {
                float t = (y + 9) / 18f; // 0 bottom .. 1 top
                int half = Mathf.RoundToInt((t < 0.35f ? Mathf.Lerp(1f, 7.5f, t / 0.35f) : 7.5f) * scale);
                for (int x = -half; x <= half; x++)
                    c.Set(cx + x, cy + y, Mathf.Abs(x) >= half - 1 || y >= 8 ? Gold : x < 0 ? Steel : SteelDark);
            }
            c.Rect(cx - 1, cy - 6, 2, 12, Gold);
            c.Rect(cx - 5, cy + 2, 10, 2, Gold);
        }

        private static void Sanctuary(PixelCanvas c)
        {
            // A dome of light over a rune circle.
            for (float a = 0f; a <= 180f; a += 2f)
            {
                float rad = a * Mathf.Deg2Rad;
                c.Set(Mathf.RoundToInt(16 + Mathf.Cos(rad) * 12f), Mathf.RoundToInt(10 + Mathf.Sin(rad) * 14f), Gold);
                c.Set(Mathf.RoundToInt(16 + Mathf.Cos(rad) * 10f), Mathf.RoundToInt(10 + Mathf.Sin(rad) * 12f), C("#fff1b8"));
            }
            for (int x = 3; x <= 29; x++) c.Set(x, 9, Gold);
            c.Ring(16, 9, 7f, 8f, Amber);
            Plus(c, 16, 15, White);
        }

        private static void Dagger(PixelCanvas c, int x0, int y0, int x1, int y1, bool flip)
        {
            c.Line(x0, y0, x1, y1, Steel);
            c.Line(x0 + (flip ? -1 : 1), y0, x1, y1 - 1, White);
            Vector2 dir = new Vector2(x1 - x0, y1 - y0).normalized;
            Vector2 side = new(-dir.y, dir.x);
            Vector2 guard = new Vector2(x0, y0) + dir * 3f;
            c.Line(guard.x - side.x * 3, guard.y - side.y * 3, guard.x + side.x * 3, guard.y + side.y * 3, Purple);
            c.Line(x0, y0, x0 - dir.x * 3, y0 - dir.y * 3, PurpleDark);
        }

        private static void ShadowStep(PixelCanvas c)
        {
            // An afterimage and the figure stepping out of a swirl.
            for (int i = 0; i < 2; i++)
            {
                int ox = 7 + i * 10;
                var col = i == 0 ? PurpleDark : Purple;
                c.Disc(ox + 2, 22, 2.6f, col);
                c.Rect(ox, 10, 5, 10, col);
                c.Line(ox, 10, ox - 2, 5, col); c.Line(ox + 4, 10, ox + 6, 5, col);
            }
            c.Ring(21, 15, 6f, 7f, C("#e0c8ff"));
            c.Set(23, 23, White); c.Set(22, 22, White);
        }

        private static void SmokeBomb(PixelCanvas c)
        {
            foreach (var (x, y, r) in new[] { (9, 22, 4.5f), (16, 25, 4f), (23, 21, 4.5f), (12, 27, 3f) })
            {
                c.Disc(x, y, r, SmokeDark);
                c.Disc(x - 0.5f, y + 0.5f, r - 1.2f, Smoke);
            }
            c.Disc(16, 13, 7f, C("#6a6080"));
            c.Disc(16, 13, 5.8f, C("#8a80a4"));
            c.Disc(14, 15, 2.2f, C("#d8d0ea"));
            c.Line(20, 17, 23, 21, Wood);
            c.Set(24, 22, Amber); c.Set(25, 23, Gold); c.Set(23, 23, Red);
        }

        private static void Flask(PixelCanvas c, int cx, int cy, Color32 liquid, Color32 dark)
        {
            c.Disc(cx, cy, 7.5f, Glass);
            c.Disc(cx, cy, 6.5f, dark);
            for (int y = -6; y <= 2; y++)
                for (int x = -6; x <= 6; x++)
                    if (x * x + y * y <= 42) c.Set(cx + x, cy + y, y < -1 ? dark : liquid);
            c.Rect(cx - 2, cy + 7, 5, 6, Glass);
            c.Rect(cx - 3, cy + 12, 7, 2, Wood);
            c.Set(cx - 3, cy + 3, White); c.Set(cx - 4, cy + 2, White);
        }

        private static void ThrowArc(PixelCanvas c)
        {
            for (float a = 20f; a < 110f; a += 6f)
            {
                float rad = a * Mathf.Deg2Rad;
                c.Set(Mathf.RoundToInt(16 + Mathf.Cos(rad) * 12f), Mathf.RoundToInt(16 + Mathf.Sin(rad) * 12f), a % 12 < 6 ? Green : GreenDark);
            }
        }

        private static void Flames(PixelCanvas c, int cx, int cy)
        {
            foreach (var (dx, h, col) in new[] { (-5, 6, Red), (0, 9, Amber), (5, 6, Red), (-2, 5, Gold), (3, 4, Gold) })
                for (int y = 0; y < h; y++)
                {
                    int half = Mathf.RoundToInt((1f - y / (float)h) * 2.2f);
                    for (int x = -half; x <= half; x++) c.Set(cx + dx + x, cy + y, col);
                }
        }

        private static void Wing(PixelCanvas c)
        {
            // A winged boot: quick on the feet.
            c.Rect(9, 7, 9, 4, C("#7a5636"));
            c.Rect(9, 11, 5, 9, C("#8f6a46"));
            c.Rect(8, 6, 12, 2, WoodDark);
            for (int i = 0; i < 4; i++) c.Line(14, 14 + i * 2, 25 - i, 18 + i * 3, i % 2 == 0 ? White : C("#c9d6e2"));
            foreach (var (y, len) in new[] { (9, 5), (13, 4) }) c.Line(2, y, 2 + len, y, Blue);
        }

        private static void Pouch(PixelCanvas c)
        {
            c.Disc(16, 13, 9f, C("#7a5230"));
            c.Disc(15, 14, 7.5f, C("#9a6a3c"));
            c.Rect(11, 21, 10, 3, C("#5a3a20"));
            c.Rect(13, 24, 6, 3, C("#7a5230"));
            c.Line(11, 22, 21, 22, Gold);
            c.Disc(16, 12, 2f, Gold);
            c.Set(12, 16, C("#c08a52")); c.Set(11, 15, C("#c08a52"));
        }
    }
}
