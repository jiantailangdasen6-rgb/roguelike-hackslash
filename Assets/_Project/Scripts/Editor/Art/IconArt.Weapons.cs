using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>Item icons of phase 4: each class's other weapon (ClassId order) and the eight runes (RuneId 1..8).</summary>
    public static partial class IconArt
    {
        /// <summary>The rune colours by RuneId (1..8): ember, frost, storm, vital, echo, swift, shadow, soul.</summary>
        public static readonly Color32[] RuneColors =
        {
            C("#ffffff"), C("#ff7a2a"), C("#8fe4ff"), C("#ffe066"), C("#7dff9a"), C("#c8a8ff"), C("#f4f0e0"), C("#9a7aff"), C("#9affd8"),
        };

        private static void AltWeapon(PixelCanvas c, int cls)
        {
            switch (cls)
            {
                case 0: // great axe
                    c.Line(5, 3, 17, 17, Wood);
                    c.Line(6, 3, 18, 17, C("#5a3c22"));
                    c.Disc(16f, 15f, 5.2f, SteelDark);
                    c.Disc(15.5f, 15.5f, 4.2f, Steel);
                    c.Rect(10, 13, 5, 6, new Color32(0, 0, 0, 0));
                    c.Line(19, 11, 20, 18, White);
                    break;
                case 1: // tome
                    c.Rect(5, 5, 14, 15, C("#4a1e22"));
                    c.Rect(6, 6, 12, 13, C("#6e2e2c"));
                    c.Rect(7, 7, 10, 11, C("#d8ceb4"));
                    c.Line(12, 7, 12, 17, C("#8a8270"));
                    for (int y = 9; y < 17; y += 2) { c.Line(8, y, 10, y, C("#7a6a9a")); c.Line(14, y, 16, y, C("#7a6a9a")); }
                    c.Disc(12f, 19f, 1.6f, Gold);
                    break;
                case 2: // crossbow
                    c.Line(4, 12, 19, 12, Wood);
                    c.Line(4, 11, 19, 11, C("#5a3c22"));
                    for (int a = -60; a <= 60; a += 4)
                    {
                        float r = a * Mathf.Deg2Rad;
                        c.Set(Mathf.RoundToInt(15 + Mathf.Cos(r) * 2.5f), Mathf.RoundToInt(12 + Mathf.Sin(r) * 8f), SteelDark);
                    }
                    c.Line(15, 4, 12, 12, White);
                    c.Line(15, 20, 12, 12, White);
                    Arrow(c, 9, 12, 21, 12);
                    break;
                case 3: // lance
                    c.Line(3, 3, 17, 17, Wood);
                    c.Line(4, 3, 18, 17, C("#5a3c22"));
                    c.Line(17, 17, 21, 21, Steel);
                    c.Line(16, 18, 21, 21, White);
                    c.Line(18, 16, 21, 21, SteelDark);
                    c.Rect(15, 14, 2, 2, Gold);
                    break;
                case 4: // throwing knives
                    for (int k = 0; k < 3; k++)
                    {
                        int o = k * 4;
                        c.Line(4 + o, 6, 10 + o, 18, Steel);
                        c.Set(10 + o, 18, White);
                        c.Line(3 + o, 4, 4 + o, 6, C("#3a3a4a"));
                    }
                    break;
                case 5: // bomb
                    c.Disc(11f, 10f, 6.5f, C("#2a2a30"));
                    c.Disc(10f, 11f, 5f, C("#3e3e46"));
                    c.Set(8, 13, C("#8a8a96")); c.Set(9, 14, C("#8a8a96"));
                    c.Line(15, 15, 18, 19, C("#8a6a3a"));
                    c.Set(18, 20, Amber); c.Set(19, 21, C("#fff3c0")); c.Set(17, 21, Amber);
                    break;
                case 6: // scythe
                    c.Line(6, 3, 12, 20, Wood);
                    c.Line(7, 3, 13, 20, C("#5a3c22"));
                    for (int a = 0; a <= 150; a += 4)
                    {
                        float r = a * Mathf.Deg2Rad;
                        c.Set(Mathf.RoundToInt(12 + Mathf.Cos(r) * 8f), Mathf.RoundToInt(19 + Mathf.Sin(r) * -3f + (a > 90 ? 0 : 0)), Steel);
                        c.Set(Mathf.RoundToInt(12 + Mathf.Cos(r) * 8f), Mathf.RoundToInt(18 + Mathf.Sin(r) * -3f), White);
                    }
                    break;
                default: // greatsword
                    c.Line(5, 5, 19, 19, Steel);
                    c.Line(6, 5, 19, 18, White);
                    c.Line(5, 6, 18, 19, SteelDark);
                    c.Line(7, 3, 3, 7, Gold);
                    c.Line(4, 4, 2, 2, Wood);
                    Sparks(c, C("#fff3c0"));
                    break;
            }
        }

        /// <summary>A rune: a dark stone slab with its sign glowing in the rune's colour.</summary>
        private static void RuneStone(PixelCanvas c, int rune)
        {
            var glow = RuneColors[rune];
            var deep = Color32.Lerp(glow, new Color32(0, 0, 0, 255), 0.45f);
            for (int y = 3; y < 21; y++)
            for (int x = 5; x < 19; x++)
            {
                float dx = (x + 0.5f - 12f) / 7f, dy = (y + 0.5f - 12f) / 9f;
                if (dx * dx + dy * dy > 1f) continue;
                c.Set(x, y, x < 9 ? C("#5a5468") : x > 15 ? C("#2a2634") : C("#423c50"));
            }
            switch (rune)
            {
                case 1: c.Line(12, 7, 9, 13, glow); c.Line(9, 13, 13, 12, glow); c.Line(13, 12, 11, 17, glow); break;      // ember: a flame stroke
                case 2: c.Line(12, 6, 12, 18, glow); c.Line(8, 9, 16, 15, glow); c.Line(16, 9, 8, 15, glow); break;        // frost: a star
                case 3: c.Line(14, 6, 10, 12, glow); c.Line(10, 12, 14, 12, glow); c.Line(14, 12, 10, 18, glow); break;   // storm: a bolt
                case 4: c.Line(12, 7, 12, 17, glow); c.Line(8, 12, 16, 12, glow); break;                                   // vital: a cross
                case 5: c.Ring(12f, 12f, 2f, 3f, glow); c.Ring(12f, 12f, 4.5f, 5.3f, deep); break;                         // echo: rings
                case 6: c.Line(8, 8, 12, 12, glow); c.Line(12, 12, 8, 16, glow); c.Line(12, 8, 16, 12, glow); c.Line(16, 12, 12, 16, glow); break; // swift: chevrons
                case 7: c.Disc(12f, 12f, 3.5f, glow); c.Disc(13.5f, 13f, 3f, C("#2a2634")); break;                         // shadow: a crescent
                default: c.Disc(12f, 11f, 2.4f, glow); c.Line(12, 13, 12, 18, deep); c.Line(10, 16, 14, 16, deep); break;  // soul: a wisp
            }
            c.Set(7, 15, Color32.Lerp(glow, Color.white, 0.5f));
        }
    }
}
