using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// 24x24 pixel icons: one per perk (PerkId order, then "rest") and one per trinket (catalog order).
    /// Each sits on a dark medallion whose rim shows who can take it (gold common, red warrior, blue mage, green ranger).
    /// </summary>
    public static class IconArt
    {
        public const int Size = 24;
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);

        private static readonly Color32 Ink = PixelRenderer.Outline;
        private static readonly Color32 White = C("#f4efe6");
        private static readonly Color32 Steel = C("#a9b1bb");
        private static readonly Color32 SteelDark = C("#5a616d");
        private static readonly Color32 Gold = C("#e6c766");
        private static readonly Color32 GoldDark = C("#87641c");
        private static readonly Color32 Red = C("#d23a3a");
        private static readonly Color32 RedDark = C("#6a1018");
        private static readonly Color32 Blue = C("#8fd3ff");
        private static readonly Color32 BlueDark = C("#2a5c9a");
        private static readonly Color32 Green = C("#7dff9a");
        private static readonly Color32 GreenDark = C("#2a7a3e");
        private static readonly Color32 Amber = C("#ffb04a");
        private static readonly Color32 Wood = C("#8a5f36");
        private static readonly Color32 Toxic = C("#b6ff4a");

        public static List<PixelCanvas> Perks()
        {
            var icons = new List<PixelCanvas>();
            foreach (PerkId id in Enum.GetValues(typeof(PerkId)))
            {
                var def = PerkCatalog.Get(id);
                var c = Medallion(def.Class);
                DrawPerk(c, id);
                Finish(c);
                icons.Add(c);
            }
            var rest = Medallion(null);
            Campfire(rest);
            Finish(rest);
            icons.Add(rest);
            return icons;
        }

        public static List<PixelCanvas> Trinkets()
        {
            var icons = new List<PixelCanvas>();
            foreach (var t in MetaCatalog.Trinkets)
            {
                var c = Medallion(null, trinket: true);
                switch (t.Id)
                {
                    case "charm_hp": Amulet(c); break;
                    case "wolf_fang": Fang(c, White); break;
                    case "feather": Feather(c, C("#dfe8f0"), C("#8fa8c2")); break;
                    case "iron_oath": Shield(c, Steel, SteelDark, Gold); break;
                    case "saint_ash": Urn(c); break;
                    case "soul_ring": Ring(c); break;
                    case "hourglass": Hourglass(c); break;
                    case "phoenix": Feather(c, Amber, Red); Sparks(c, Gold); break;
                }
                Finish(c);
                icons.Add(c);
            }
            return icons;
        }

        // ------------------------------------------------------------------ Frame

        private static PixelCanvas Medallion(ClassId? cls, bool trinket = false)
        {
            var c = new PixelCanvas(Size, Size);
            Color32 rim = trinket ? C("#9a8a6a") : cls switch
            {
                ClassId.Warrior => C("#b0302c"),
                ClassId.Mage => C("#4a7cc8"),
                ClassId.Ranger => C("#4f9a4a"),
                _ => GoldDark,
            };
            c.Disc(12, 12, 11.5f, Ink);
            c.Disc(12, 12, 10.5f, rim);
            c.Disc(12, 12, 9.5f, C("#1a1419"));
            c.Disc(11.5f, 12.5f, 7.5f, C("#221b22"));
            return c;
        }

        /// <summary>Outline the symbol inside the medallion so it pops.</summary>
        private static void Finish(PixelCanvas c)
        {
            var copy = (Color32[])c.Pixels.Clone();
            var bg1 = C("#1a1419");
            var bg2 = C("#221b22");
            bool IsBg(Color32 p) => p.Equals(bg1) || p.Equals(bg2);
            for (int y = 1; y < Size - 1; y++)
            for (int x = 1; x < Size - 1; x++)
            {
                var p = copy[y * Size + x];
                if (!IsBg(p)) continue;
                bool touch = false;
                for (int k = 0; k < 4 && !touch; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    var q = copy[ny * Size + nx];
                    float dx = nx + 0.5f - 12f, dy = ny + 0.5f - 12f;
                    if (dx * dx + dy * dy < 9.5f * 9.5f && !IsBg(q) && q.a > 0) touch = true;
                }
                if (touch) c.Set(x, y, Ink);
            }
        }

        // ------------------------------------------------------------------ Perks

        private static void DrawPerk(PixelCanvas c, PerkId id)
        {
            switch (id)
            {
                case PerkId.Vitality: Heart(c, Red, C("#ff8a8a")); Plus(c, 15, 15, White); break;
                case PerkId.Swiftness: Boot(c); Streaks(c, Blue); break;
                case PerkId.Fury: Aura(c, Red); Sword(c, 5, 5, 18, 18, White, Steel); break;
                case PerkId.Focus: Hourglass(c); break;
                case PerkId.Lifesteal: Drop(c, Red, C("#ff8a8a")); Fang(c, White, small: true); break;
                case PerkId.GuardianBond: c.Ring(9, 12, 3.2f, 4.6f, Gold); c.Ring(15, 12, 3.2f, 4.6f, C("#ffe9b0")); break;
                case PerkId.Resonance: c.Ring(12, 12, 6f, 7f, Blue); c.Ring(12, 12, 3f, 4f, Amber); c.Disc(12, 12, 1.4f, White); break;
                case PerkId.Rapid: Sword(c, 5, 6, 15, 16, Steel, SteelDark); Sword(c, 9, 5, 19, 15, Steel, SteelDark); break;
                case PerkId.SecondWind: Heart(c, RedDark, Red); c.Line(12, 17, 10, 13, Ink); c.Line(10, 13, 13, 10, Ink); Sparks(c, Gold); break;
                case PerkId.Whirlwind: Spiral(c, Steel); break;
                case PerkId.SwordWave: c.Ring(9, 12, 7f, 9f, Blue); ClearLeft(c, 9); Sword(c, 4, 7, 11, 14, Steel, SteelDark); break;
                case PerkId.Bulwark: Shield(c, Steel, SteelDark, Gold); break;
                case PerkId.Crusher: Hammer(c); Shards(c, Blue); break;
                case PerkId.Rush: Sword(c, 8, 8, 19, 15, Steel, SteelDark); Streaks(c, Amber); break;
                case PerkId.Bloodlust: Claw(c, Red); break;
                case PerkId.SplitBolt: c.Disc(9, 12, 3.4f, Blue); c.Disc(9, 12, 1.8f, White); c.Disc(16, 16, 2.5f, Blue); c.Disc(16, 8, 2.5f, Blue); c.Line(12, 13, 14, 15, BlueDark); c.Line(12, 11, 14, 9, BlueDark); break;
                case PerkId.Multicast: c.Disc(7, 9, 2.6f, Blue); c.Disc(12, 15, 2.6f, Blue); c.Disc(17, 9, 2.6f, Blue); c.Set(7, 10, White); c.Set(12, 16, White); c.Set(17, 10, White); break;
                case PerkId.Permafrost: Snowflake(c); break;
                case PerkId.ChainBolt: Lightning(c); break;
                case PerkId.Mercy: Heart(c, GreenDark, Green); Sparks(c, White); break;
                case PerkId.Ignite: Flames(c, 12, 6, Red, big: true); break;
                case PerkId.Spread: Arrow(c, 5, 6, 18, 6); Arrow(c, 5, 12, 19, 12); Arrow(c, 5, 18, 18, 18); break;
                case PerkId.Piercing: c.Ring(14, 12, 3.5f, 5f, Red); Arrow(c, 3, 12, 20, 12); break;
                case PerkId.HunterEye: Eye(c); break;
                case PerkId.Venom: Drop(c, GreenDark, Toxic); c.Set(11, 10, Ink); c.Set(13, 10, Ink); break;
                case PerkId.DeepMark: c.Ring(12, 12, 6f, 7.2f, Amber); c.Disc(12, 12, 2f, Amber); c.Rect(11, 3, 2, 4, Amber); c.Rect(11, 17, 2, 4, Amber); c.Rect(3, 11, 4, 2, Amber); c.Rect(17, 11, 4, 2, Amber); break;
                case PerkId.Evasive: Feather(c, C("#dfe8f0"), C("#8fa8c2")); Streaks(c, Green); break;
            }
        }

        // ------------------------------------------------------------------ Symbols

        private static void Heart(PixelCanvas c, Color32 col, Color32 light)
        {
            c.Disc(9, 14, 3.6f, col);
            c.Disc(15, 14, 3.6f, col);
            for (int y = 5; y < 14; y++)
            {
                int half = (y - 5) * 7 / 9;
                for (int x = 12 - half; x <= 12 + half; x++) c.Set(x, y, col);
            }
            c.Set(8, 15, light); c.Set(9, 16, light); c.Set(8, 16, light);
        }

        private static void Plus(PixelCanvas c, int x, int y, Color32 col)
        {
            c.Rect(x - 1, y - 3, 3, 7, col);
            c.Rect(x - 3, y - 1, 7, 3, col);
        }

        private static void Sword(PixelCanvas c, int x0, int y0, int x1, int y1, Color32 blade, Color32 edge)
        {
            c.Line(x0 + 2, y0 + 2, x1, y1, blade);
            c.Line(x0 + 3, y0 + 2, x1, y1 - 1, edge);
            // Guard and grip at the start
            c.Line(x0 + 3, y0, x0, y0 + 3, Gold);
            c.Line(x0 + 1, y0 + 1, x0 - 1, y0 - 1, Wood);
            c.Set(x1, y1, White);
        }

        private static void Flames(PixelCanvas c, int x, int y, Color32 col, bool big = false)
        {
            float s = big ? 1.6f : 1f;
            c.Disc(x, y + 2 * s, 3.2f * s, col);
            for (int i = 0; i < (int)(6 * s); i++) c.Set(x - 1 + i % 3, y + (int)(4 * s) + i, col);
            c.Disc(x, y + 2 * s, 1.8f * s, Amber);
            c.Set(x, (int)(y + 2 * s), C("#fff3c0"));
            if (big)
            {
                c.Disc(x - 4, y + 3, 2.2f, col);
                c.Disc(x + 4, y + 2, 2f, col);
                c.Rect(x - 1, y + 9, 2, 4, col);
            }
        }

        private static void Hourglass(PixelCanvas c)
        {
            c.Rect(6, 4, 12, 2, Wood);
            c.Rect(6, 18, 12, 2, Wood);
            for (int y = 6; y < 18; y++)
            {
                int half = Mathf.Abs(y - 12) * 5 / 6 + 1;
                for (int x = 12 - half; x < 12 + half; x++) c.Set(x, y, y < 12 ? C("#d8c890") : C("#5a6a80"));
            }
            for (int y = 7; y < 11; y++) c.Set(12, y, Amber);
            c.Rect(10, 13, 4, 2, Amber);
        }

        private static void Drop(PixelCanvas c, Color32 col, Color32 light)
        {
            c.Disc(12, 10, 5.2f, col);
            for (int y = 14; y < 20; y++)
            {
                int half = (20 - y) * 4 / 6;
                for (int x = 12 - half; x <= 12 + half; x++) c.Set(x, y, col);
            }
            c.Set(10, 11, light); c.Set(10, 12, light); c.Set(11, 13, light);
        }

        private static void Fang(PixelCanvas c, Color32 col, bool small = false)
        {
            int x0 = small ? 13 : 8, y0 = small ? 4 : 5, h = small ? 6 : 13;
            for (int y = 0; y < h; y++)
            {
                int half = (h - y) * (small ? 3 : 4) / h;
                for (int x = x0 - half; x <= x0 + half; x++) c.Set(x + y / 4, y0 + y, col);
            }
            if (!small) c.Rect(5, 17, 9, 2, C("#6b4a32"));
        }

        private static void Boot(PixelCanvas c)
        {
            var leather = C("#8a5f36");
            var dark = C("#4a3020");
            c.Rect(10, 9, 5, 9, leather);              // shaft
            c.Rect(10, 17, 6, 2, C("#c8a070"));        // cuff
            c.Rect(10, 5, 8, 4, leather);              // foot
            c.Disc(17.5f, 7f, 2.2f, leather);          // rounded toe
            c.Rect(9, 4, 11, 1, dark);                 // sole
            c.Rect(10, 4, 3, 2, dark);                 // heel
            // A small wing on the heel
            c.Line(9, 14, 4, 18, White); c.Line(9, 12, 4, 15, White); c.Line(9, 10, 5, 12, Blue);
        }

        private static void Aura(PixelCanvas c, Color32 col)
        {
            for (int k = -2; k <= 2; k++)
                c.Line(6 + k, 6 - k, 18 + k, 18 - k, new Color32(col.r, col.g, col.b, 255));
            c.Line(4, 9, 7, 6, Amber);
            c.Line(15, 20, 19, 16, Amber);
        }

        private static void Streaks(PixelCanvas c, Color32 col)
        {
            c.Line(3, 8, 7, 8, col);
            c.Line(2, 11, 6, 11, col);
            c.Line(3, 14, 6, 14, col);
        }

        private static void Spiral(PixelCanvas c, Color32 col)
        {
            for (float a = 0f; a < 12f; a += 0.08f)
            {
                float r = 1f + a * 0.65f;
                c.Set(Mathf.RoundToInt(12 + Mathf.Cos(a) * r), Mathf.RoundToInt(12 + Mathf.Sin(a) * r), a > 9f ? White : col);
            }
        }

        private static void ClearLeft(PixelCanvas c, int maxX)
        {
            var bg = C("#221b22");
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < maxX; x++)
            {
                float dx = x + 0.5f - 12f, dy = y + 0.5f - 12f;
                if (dx * dx + dy * dy < 7.5f * 7.5f && !c.Get(x, y).Equals(C("#1a1419"))) c.Set(x, y, bg);
            }
        }

        private static void Shield(PixelCanvas c, Color32 face, Color32 dark, Color32 trim)
        {
            for (int y = 4; y < 20; y++)
            {
                int half = y < 11 ? 7 : Mathf.Max(1, 7 - (y - 11) * 7 / 9);
                for (int x = 12 - half; x <= 12 + half; x++) c.Set(x, 23 - y, x < 12 ? face : dark);
            }
            for (int y = 6; y < 17; y++) c.Set(12, y, trim);
            c.Rect(8, 13, 9, 2, trim);
        }

        private static void Hammer(PixelCanvas c)
        {
            c.Line(6, 5, 15, 14, Wood);
            c.Line(7, 5, 16, 14, C("#5a3e24"));
            c.Rect(12, 13, 8, 6, SteelDark);
            c.Rect(12, 16, 8, 3, Steel);
        }

        private static void Shards(PixelCanvas c, Color32 col)
        {
            c.Line(4, 15, 6, 19, col); c.Line(5, 15, 7, 18, White);
            c.Line(17, 6, 20, 4, col);
            c.Line(4, 9, 3, 6, col);
        }

        private static void Claw(PixelCanvas c, Color32 col)
        {
            for (int k = 0; k < 3; k++)
            {
                int x = 7 + k * 4;
                c.Line(x, 19, x + 4, 5, col);
                c.Line(x + 1, 19, x + 5, 5, RedDark);
            }
            c.Set(9, 6, C("#ff8a8a"));
        }

        private static void Snowflake(PixelCanvas c)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI / 3f;
                float dx = Mathf.Cos(a) * 8f, dy = Mathf.Sin(a) * 8f;
                c.Line(12 - dx, 12 - dy, 12 + dx, 12 + dy, Blue);
                for (int s = -1; s <= 1; s += 2)
                {
                    float bx = 12 + dx * 0.6f * s, by = 12 + dy * 0.6f * s;
                    c.Line(bx, by, bx + Mathf.Cos(a + 0.8f * s) * 3f * s, by + Mathf.Sin(a + 0.8f * s) * 3f * s, White);
                }
            }
            c.Disc(12, 12, 1.5f, White);
        }

        private static void Lightning(PixelCanvas c)
        {
            Vector2[] pts = { new(5, 17), new(10, 11), new(13, 14), new(19, 6) };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                c.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, Blue);
                c.Line(pts[i].x + 1, pts[i].y, pts[i + 1].x + 1, pts[i + 1].y, White);
            }
            foreach (var p in pts) c.Disc(p.x + 0.5f, p.y + 0.5f, 1.6f, BlueDark);
        }

        private static void Arrow(PixelCanvas c, int x0, int y0, int x1, int y1)
        {
            c.Line(x0, y0, x1, y1, Wood);
            c.Set(x1 + 1, y1, Steel); c.Set(x1, y1 + 1, Steel); c.Set(x1, y1 - 1, Steel); c.Set(x1 + 2, y1, White);
            c.Set(x0, y0 + 1, Red); c.Set(x0, y0 - 1, Red); c.Set(x0 + 1, y0 + 1, Red); c.Set(x0 + 1, y0 - 1, Red);
        }

        private static void Eye(PixelCanvas c)
        {
            for (int x = 4; x < 21; x++)
            {
                float t = (x - 12f) / 8.5f;
                int half = Mathf.RoundToInt((1f - t * t) * 5f);
                for (int y = 12 - half; y <= 12 + half; y++) c.Set(x, y, White);
            }
            c.Disc(12, 12, 3.6f, Amber);
            c.Disc(12, 12, 1.8f, Ink);
            c.Set(13, 13, White);
        }

        private static void Feather(PixelCanvas c, Color32 col, Color32 dark)
        {
            for (int i = 0; i < 13; i++)
            {
                int cx = 7 + i * 10 / 13, cy = 5 + i;
                int half = Mathf.RoundToInt(Mathf.Sin(i / 12f * Mathf.PI) * 3.5f);
                for (int k = -half; k <= half; k++) c.Set(cx + k, cy - k / 2, k < 0 ? col : dark);
            }
            c.Line(6, 4, 17, 18, Wood);
        }

        private static void Sparks(PixelCanvas c, Color32 col)
        {
            foreach (var (x, y) in new[] { (5, 18), (18, 18), (19, 7) })
            {
                c.Set(x, y, col); c.Set(x + 1, y, col); c.Set(x - 1, y, col); c.Set(x, y + 1, col); c.Set(x, y - 1, col);
            }
        }

        private static void Campfire(PixelCanvas c)
        {
            c.Line(5, 6, 18, 9, Wood);
            c.Line(5, 9, 18, 6, C("#6b4a32"));
            Flames(c, 12, 7, Red, big: false);
            c.Disc(12, 13, 3f, Amber);
            c.Set(12, 15, C("#fff3c0"));
        }

        private static void Amulet(PixelCanvas c)
        {
            c.Line(7, 19, 12, 13, Gold);
            c.Line(17, 19, 12, 13, Gold);
            c.Disc(12, 10, 4.2f, GoldDark);
            c.Disc(12, 10, 3f, Red);
            c.Set(11, 11, C("#ff9a9a"));
        }

        private static void Urn(PixelCanvas c)
        {
            c.Disc(12, 10, 5f, C("#9c958a"));
            c.Rect(9, 15, 7, 2, C("#6b645a"));
            c.Rect(10, 17, 5, 2, C("#cbc5b8"));
            c.Rect(8, 4, 9, 2, C("#6b645a"));
            for (int i = 0; i < 4; i++) c.Set(10 + i, 19 + i % 2, C("#d8d0c0"));
        }

        private static void Ring(PixelCanvas c)
        {
            c.Ring(12, 10, 4f, 6f, Gold);
            c.Disc(12, 16, 2.8f, C("#5fa8ff"));
            c.Set(11, 17, White);
        }
    }
}
