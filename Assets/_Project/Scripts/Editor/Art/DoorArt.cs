using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Doorways between the rooms of an area map: an arch in the back wall (north), gaps in the side and bottom wall
    /// tops (east/west/south), each per area look, and the bone portcullis that shuts the boss door. 32 px = 1 tile.
    /// </summary>
    public static class DoorArt
    {
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static readonly Color32 Outline = PixelRenderer.Outline;

        // Stone ramps (dark -> light) per land: crypt grey, chapel marble, abyss obsidian, sunken green, frost blue, forge char.
        private static readonly Color32[][] Stone =
        {
            new[] { C("#16131a"), C("#2a2631"), C("#3d3846"), C("#544d5f"), C("#6c6479"), C("#8a8199") },
            new[] { C("#22191c"), C("#45363a"), C("#6b5a5d"), C("#92807e"), C("#b6a39d"), C("#d8cbc2") },
            new[] { C("#07060b"), C("#120f1b"), C("#1f1a2d"), C("#2e2742"), C("#41385a"), C("#5a4f7a") },
            new[] { C("#081010"), C("#111d1c"), C("#1b2c2a"), C("#273e3a"), C("#35524c"), C("#4a6c64") }, // sunken
            new[] { C("#0c1220"), C("#182235"), C("#27354f"), C("#3a4b6c"), C("#566b90"), C("#86a0c4") }, // frost
            new[] { C("#0e0907"), C("#1b130e"), C("#291d16"), C("#392a1f"), C("#4b392b"), C("#634c3a") }, // forge
            new[] { C("#0c0506"), C("#1a090b"), C("#2a0f12"), C("#3c161a"), C("#521f24"), C("#6e2c30") }, // heart
            new[] { C("#090c07"), C("#141a10"), C("#202918"), C("#2e3a23"), C("#405033"), C("#586b46") }, // grotto
            new[] { C("#0e0907"), C("#1b120d"), C("#2a1d15"), C("#3b2a1e"), C("#503a29"), C("#6b4e37") }, // library
        };
        private static readonly Color32[] Torch = { C("#ff9a3c"), C("#ff4a3a"), C("#c58bff"), C("#5fe8c8"), C("#a8e4ff"), C("#ff8a2a"), C("#ffae42"), C("#9cff6a"), C("#b49cff") };

        public static Dictionary<string, PropArt.Sheet> Sheets()
        {
            var sheets = new Dictionary<string, PropArt.Sheet>();
            for (int area = 0; area < LandArt.Count; area++)
            {
                sheets[$"door_n_{area}"] = One(North(area), new Vector2(0.5f, 0f));
                sheets[$"door_s_{area}"] = One(South(area), new Vector2(0.5f, 1f));
                sheets[$"door_side_{area}"] = One(Side(area), new Vector2(1f, 0.5f));
            }
            sheets["door_bars_n"] = One(BarsNorth(), new Vector2(0.5f, 0f));
            sheets["door_bars_s"] = One(BarsFlat(horizontal: true), new Vector2(0.5f, 1f));
            sheets["door_bars_side"] = One(BarsFlat(horizontal: false), new Vector2(1f, 0.5f));
            return sheets;
        }

        private static PropArt.Sheet One(PixelCanvas c, Vector2 pivot) => new() { Frames = new List<PixelCanvas> { c }, Pivot = pivot };

        private static float Hash(int x, int y) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);

        /// <summary>Rough cut blocks: a brick pattern with lit top-left edges.</summary>
        private static Color32 Block(Color32[] ramp, int x, int y, int bw = 8, int bh = 6)
        {
            int row = y / bh;
            int ox = (row & 1) * (bw / 2);
            int bx = (x + ox) % bw, by = y % bh;
            if (bx == 0 || by == 0) return ramp[1];
            int shade = by == bh - 1 || bx == 1 ? 4 : bx == bw - 1 ? 2 : 3;
            if (Hash((x + ox) / bw, row) > 0.7f) shade--;
            return ramp[Mathf.Clamp(shade, 1, ramp.Length - 1)];
        }

        /// <summary>The way out through the back wall: a stone arch over a dark passage, with a step and two sconces.</summary>
        private static PixelCanvas North(int area)
        {
            var s = Stone[area];
            const int w = 112, h = 96;
            var c = new PixelCanvas(w, h);
            const int cx = 56, open = 22, frame = 9, archY = 58; // half-width of the opening, frame thickness, where the arch starts
            for (int y = 0; y < h - 8; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx;
                float outerHalf = open + frame;
                // Arch: a semicircle on top of straight jambs.
                float yy = y - archY;
                bool insideOuter = y < archY ? Mathf.Abs(dx) < outerHalf : dx * dx + yy * yy < outerHalf * outerHalf;
                bool insideOpen = y < archY ? Mathf.Abs(dx) < open : dx * dx + yy * yy < open * open;
                if (!insideOuter) continue;
                if (insideOpen)
                {
                    // The passage: darker the deeper (higher) you look, a faint floor going in.
                    float t = Mathf.Clamp01(y / (float)(archY + open));
                    byte v = (byte)Mathf.Lerp(26f, 2f, Mathf.Sqrt(t));
                    var col = new Color32(v, (byte)(v * 0.85f), (byte)(v * 1.1f), 255);
                    if (y < 10 && Hash(x / 3, y / 2) > 0.6f) col = s[1];
                    c.Set(x, y, col);
                    continue;
                }
                // Voussoirs on the arch, blocks on the jambs.
                var stone = y >= archY ? Voussoir(s, dx, yy, open, frame) : Block(s, x, y, 9, 7);
                c.Set(x, y, stone);
            }
            // Keystone.
            for (int y = archY + open; y < archY + open + frame + 2; y++)
            for (int x = cx - 4; x <= cx + 4; x++)
                c.Set(x, y, y == archY + open + frame + 1 || x == cx - 4 ? s[5] : s[4]);
            // Threshold step.
            for (int x = cx - open - 3; x <= cx + open + 3; x++)
            {
                c.Set(x, 0, s[2]);
                c.Set(x, 1, s[4]);
                c.Set(x, 2, s[3]);
            }
            // Sconces with a flame on each jamb.
            foreach (int sx in new[] { cx - open - frame / 2 - 1, cx + open + frame / 2 })
            {
                for (int y = 34; y < 38; y++) c.Set(sx, y, s[0]);
                c.Set(sx - 1, 38, s[0]); c.Set(sx, 38, s[0]); c.Set(sx + 1, 38, s[0]);
                var fire = Torch[area];
                c.Set(sx, 39, fire); c.Set(sx, 40, fire); c.Set(sx - 1, 39, fire); c.Set(sx + 1, 39, fire); c.Set(sx, 41, new Color32(255, 240, 200, 255));
            }
            OutlineShape(c);
            return c;
        }

        private static Color32 Voussoir(Color32[] s, float dx, float yy, float open, float frame)
        {
            float r = Mathf.Sqrt(dx * dx + yy * yy);
            float a = Mathf.Atan2(yy, dx) * Mathf.Rad2Deg;
            bool seam = Mathf.Repeat(a, 18f) < 2.2f || r - open < 1f;
            if (seam) return s[1];
            float lit = Mathf.Clamp01((r - open) / frame);
            return s[lit > 0.7f ? 4 : lit > 0.3f ? 3 : 2];
        }

        /// <summary>A gap in the bottom wall top: two stone posts and steps going down into the dark.</summary>
        private static PixelCanvas South(int area)
        {
            var s = Stone[area];
            const int w = 112, h = 40;
            var c = new PixelCanvas(w, h);
            const int cx = 56, open = 22;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - cx);
                if (dx < open)
                {
                    // Steps down (top of the sprite is the room side).
                    int step = (h - 1 - y) / 6;
                    byte v = (byte)Mathf.Max(3, 40 - step * 8);
                    var col = (h - 1 - y) % 6 == 0 ? new Color32((byte)(v + 14), (byte)(v + 12), (byte)(v + 16), 255) : new Color32(v, (byte)(v * 0.9f), (byte)(v * 1.1f), 255);
                    c.Set(x, y, col);
                }
                else if (dx < open + 8) c.Set(x, y, Block(s, x, y, 8, 6));
            }
            OutlineShape(c);
            return c;
        }

        /// <summary>A gap in a side wall top (drawn for the east side; flipped for the west).</summary>
        private static PixelCanvas Side(int area)
        {
            var s = Stone[area];
            const int w = 40, h = 112;
            var c = new PixelCanvas(w, h);
            const int cy = 56, open = 22;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dy = Mathf.Abs(y + 0.5f - cy);
                if (dy < open)
                {
                    // A passage leading off to the side, fading out.
                    byte v = (byte)Mathf.Lerp(34f, 3f, x / (float)(w - 1));
                    var col = new Color32(v, (byte)(v * 0.88f), (byte)(v * 1.1f), 255);
                    if (x < 12 && Hash(x / 3, y / 3) > 0.65f) col = s[1];
                    c.Set(x, y, col);
                }
                else if (dy < open + 8) c.Set(x, y, Block(s, y, x, 8, 6));
            }
            OutlineShape(c);
            return c;
        }

        /// <summary>Bone portcullis over the arch: shut until the whole team stands before it.</summary>
        private static PixelCanvas BarsNorth()
        {
            var bone = new[] { C("#3b352b"), C("#6b6250"), C("#a59a80"), C("#d6ccb0"), C("#f0e8d2") };
            const int w = 112, h = 96, cx = 56, open = 22, archY = 58;
            var c = new PixelCanvas(w, h);
            for (int bar = -2; bar <= 2; bar++)
            {
                int x = cx + bar * 9;
                for (int y = 0; y < archY + open - 2; y++)
                {
                    float dx = x + 0.5f - cx, yy = y - archY;
                    if (y >= archY && dx * dx + yy * yy >= open * open) break;
                    c.Set(x - 1, y, bone[1]); c.Set(x, y, bone[3]); c.Set(x + 1, y, bone[2]);
                    if (y % 14 == 6) { c.Set(x - 2, y, bone[2]); c.Set(x + 2, y, bone[2]); }
                }
            }
            // Cross bars and a skull lock with red eyes.
            foreach (int y in new[] { 18, 44 })
                for (int x = cx - open + 2; x <= cx + open - 2; x++) { c.Set(x, y, bone[2]); c.Set(x, y + 1, bone[1]); }
            for (int y = 26; y < 36; y++)
            for (int x = cx - 5; x <= cx + 5; x++)
                if ((x - cx) * (x - cx) + (y - 31) * (y - 31) * 1.4f < 30f) c.Set(x, y, bone[3]);
            c.Set(cx - 2, 31, C("#ff3b2f")); c.Set(cx + 2, 31, C("#ff3b2f"));
            c.Set(cx - 1, 28, bone[0]); c.Set(cx + 1, 28, bone[0]);
            OutlineShape(c);
            return c;
        }

        private static PixelCanvas BarsFlat(bool horizontal)
        {
            var bone = new[] { C("#3b352b"), C("#6b6250"), C("#a59a80"), C("#d6ccb0"), C("#f0e8d2") };
            int w = horizontal ? 112 : 40, h = horizontal ? 40 : 112;
            var c = new PixelCanvas(w, h);
            const int center = 56, open = 22;
            for (int bar = -2; bar <= 2; bar++)
            {
                int at = center + bar * 9;
                for (int t = 2; t < 38; t++)
                {
                    if (horizontal) { c.Set(at - 1, t, bone[1]); c.Set(at, t, bone[3]); c.Set(at + 1, t, bone[2]); }
                    else { c.Set(t, at - 1, bone[1]); c.Set(t, at, bone[3]); c.Set(t, at + 1, bone[2]); }
                }
            }
            for (int t = center - open + 2; t <= center + open - 2; t++)
            {
                if (horizontal) { c.Set(t, 20, bone[2]); c.Set(t, 21, bone[1]); }
                else { c.Set(20, t, bone[2]); c.Set(21, t, bone[1]); }
            }
            OutlineShape(c);
            return c;
        }

        /// <summary>A dark pixel outline around everything drawn.</summary>
        private static void OutlineShape(PixelCanvas c)
        {
            var copy = (Color32[])c.Pixels.Clone();
            for (int y = 0; y < c.Height; y++)
            for (int x = 0; x < c.Width; x++)
            {
                if (copy[y * c.Width + x].a != 0) continue;
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx >= 0 && ny >= 0 && nx < c.Width && ny < c.Height && copy[ny * c.Width + nx].a != 0) edge = true;
                }
                if (edge) c.Set(x, y, Outline);
            }
        }
    }
}
