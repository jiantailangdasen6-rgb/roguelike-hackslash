using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// The fungal grotto (land 7: damp rock, moss, threads of mycelium and toadstools that glow) and the cursed library
    /// (land 8: oak parquet, walls of books, spilt ink and loose pages): floors, walls, decorations, their pillars and
    /// statues, the library's bookshelf and the two lands' floor traps (spore vents, written pages).
    /// </summary>
    public static partial class LandArt
    {
        public static readonly Color32 GrottoGlow = C("#9cff6a");
        public static readonly Color32 LibraryGlow = C("#b49cff");

        private static readonly Color32[] Rock = { C("#080a07"), C("#0f140d"), C("#151c12"), C("#1c2518"), C("#25301f") };
        private static readonly Color32[] Moss = { C("#16260f"), C("#1f3615"), C("#2b4a1c") };
        private static readonly Color32[] Oak = { C("#140c06"), C("#20140a"), C("#2c1c0f"), C("#382414"), C("#462e1a") };
        private static readonly Color32[] Cap = { C("#2a0f14"), C("#4a1a20"), C("#6e2a2a"), C("#94402f"), C("#b85e3c") };
        private static readonly Color32[] Stalk = { C("#3a3428"), C("#5a503e"), C("#7e7258"), C("#a49574"), C("#c8b994") };
        private static readonly Color32[] BookColors =
        {
            C("#4a1418"), C("#5e2a16"), C("#1e3a2a"), C("#1c2a4a"), C("#3a2a4e"), C("#5a4a22"), C("#2e2a26"), C("#6a3a2a"),
        };

        // ------------------------------------------------------------------ Floors

        /// <summary>Damp rock in uneven slabs, moss in the cracks; mycelium threads (2) and a ring of tiny glowing caps (3).</summary>
        private static PixelCanvas GrottoFloor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 7919);
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float n = Hash(x / 5 + (int)seed, y / 4) * 0.6f + Hash(x, y) * 0.4f;
                int shade = n > 0.85f ? 3 : n > 0.45f ? 2 : 1;
                bool crack = Hash(x / 2, y / 2 + 40) > 0.95f;
                c.Set(x, y, crack ? Rock[0] : Rock[shade]);
                if (crack && Hash(x, y + 7) > 0.5f) c.Set(x, y, Moss[rng.Range(0, 2)]);
            }
            if (variant == 1)
                for (int i = 0; i < 30; i++)
                {
                    int x = rng.Range(1, 31), y = rng.Range(1, 31);
                    c.Set(x, y, Moss[rng.Range(0, 3)]);
                    if (rng.Chance(0.6f)) c.Set(x + 1, y, Moss[1]);
                }
            if (variant == 2)
            {
                // Pale threads of mycelium running over the rock.
                for (int t = 0; t < 3; t++)
                {
                    float x = rng.Range(0f, 32f), y = rng.Range(0f, 32f);
                    float a = rng.Range(0f, Mathf.PI * 2f);
                    for (int k = 0; k < 26; k++)
                    {
                        c.Set((int)x & 31, (int)y & 31, k % 5 == 0 ? C("#5e6650") : C("#363c2c"));
                        a += rng.Range(-0.6f, 0.6f);
                        x += Mathf.Cos(a);
                        y += Mathf.Sin(a);
                    }
                }
            }
            if (variant == 3)
            {
                // A fairy ring of tiny glowing caps.
                float cx = rng.Range(10f, 22f), cy = rng.Range(10f, 22f);
                for (int i = 0; i < 3; i++)
                {
                    float a = rng.Range(0f, Mathf.PI * 2f);
                    int x = Mathf.RoundToInt(cx + Mathf.Cos(a) * 2.5f), y = Mathf.RoundToInt(cy + Mathf.Sin(a) * 2f);
                    c.Set(x, y, C("#4a6a2a"));
                    c.Set(x, y + 1, GrottoGlow);
                    if (i % 3 == 0) c.Set(x + 1, y + 1, C("#d8ffb0"));
                }
            }
            return c;
        }

        /// <summary>Dark oak parquet in staggered planks; a loose page (1), a blot of ink (2), a worn rug's fringe (3).</summary>
        private static PixelCanvas LibraryFloor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 4421);
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            {
                int row = y / 8, offset = row % 2 == 0 ? 0 : 12;
                for (int x = 0; x < 32; x++)
                {
                    bool seam = y % 8 == 0 || (x + offset) % 24 == 0;
                    int shade = seam ? 0 : y % 8 == 7 ? 3 : y % 8 == 1 ? 1 : 2;
                    if (!seam && Hash(x / 6 + row * 5, (int)seed) > 0.6f) shade = Mathf.Max(1, shade - 1); // plank to plank
                    if (!seam && (x * 3 + y * 7 + row) % 17 == 0) shade = 1;                                // grain
                    c.Set(x, y, Oak[shade]);
                }
            }
            if (variant == 1)
                for (int i = 0; i < 6; i++) c.Set(rng.Range(1, 31), rng.Range(1, 31), Oak[0]); // worn knots
            if (variant == 3)
            {
                int x0 = rng.Range(6, 18), y0 = rng.Range(6, 18);
                for (int y = 0; y < 9; y++)
                for (int x = 0; x < 7; x++)
                    c.Set(x0 + x + y / 4, y0 + y, (y % 2 == 1 && x > 0 && x < 6) ? C("#8a8270") : C("#c8bea6"));
            }
            if (variant == 2)
            {
                float cx = rng.Range(9f, 23f), cy = rng.Range(9f, 23f);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x + 0.5f - cx) / 3.6f, dy = (y + 0.5f - cy) / 2.4f, d = dx * dx + dy * dy;
                    if (d < 1f + (Hash(x, y) - 0.5f) * 0.6f) c.Set(x, y, d < 0.5f ? C("#0a0816") : C("#151026"));
                }
                c.Set((int)cx + 1, (int)cy + 1, C("#4a3a8a"));
            }
            return c;
        }

        // ------------------------------------------------------------------ Walls

        private static PixelCanvas Wall2(int land, ulong seed)
        {
            var rng = new GameRandom(seed * 7121 + (ulong)land);
            var c = new PixelCanvas(32, 32);
            bool alt = seed == 6;
            if (land == 7)
            {
                // Rough rock in heaped lumps; moss hanging from the ledges, glowing fungus on the alternate block.
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float n = Hash(x / 6, y / 5 + 11) * 0.7f + Hash(x, y + 3) * 0.3f;
                    c.Set(x, y, Rock[n > 0.75f ? 4 : n > 0.45f ? 3 : n > 0.2f ? 2 : 1]);
                    if (Hash(x / 6, y / 5 + 11) > 0.82f && y % 5 == 4) c.Set(x, y, Rock[0]);
                }
                for (int x = 0; x < 32; x++)
                    if (Hash(x, 2) > 0.4f)
                        for (int k = 0; k < 1 + (int)(Hash(x, 5) * 4); k++) c.Set(x, 31 - k, Moss[k == 0 ? 2 : 1]);
                if (alt)
                    for (int i = 0; i < 4; i++)
                    {
                        int x = rng.Range(3, 28), y = rng.Range(6, 22);
                        c.Rect(x - 1, y, 4, 1, C("#4a6a2a"));
                        c.Set(x, y + 1, GrottoGlow); c.Set(x + 1, y + 1, C("#d8ffb0")); c.Set(x + 2, y + 1, GrottoGlow);
                        c.Set(x + 1, y - 1, C("#2a3a1e"));
                    }
                return c;
            }
            // The library: a wall of books, two shelves to a block; the alternate block has a gap and a sconce.
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                c.Set(x, y, Oak[(x == 0 || x == 31) ? 3 : 1]);
            foreach (int shelf in new[] { 1, 17 })
            {
                for (int x = 0; x < 32; x++) { c.Set(x, shelf - 1 < 0 ? 0 : shelf - 1, Oak[4]); c.Set(x, shelf, Oak[3]); }
                int bx = 1;
                while (bx < 31)
                {
                    int w = 2 + rng.Range(0, 2), h = 9 + rng.Range(0, 5);
                    if (alt && shelf == 17 && bx > 10 && bx < 20) { bx += w; continue; } // a gap where books were taken
                    var col = BookColors[rng.Range(0, BookColors.Length)];
                    bool lean = rng.Chance(0.08f);
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w && bx + x < 31; x++)
                    {
                        int px = bx + x + (lean ? y / 5 : 0);
                        if (px > 30) continue;
                        var shade = x == 0 ? Color32.Lerp(col, Color.white, 0.18f) : x == w - 1 ? Color32.Lerp(col, Color.black, 0.35f) : col;
                        if (y == h - 3 || y == 3) shade = Color32.Lerp(shade, C("#c8a24a"), 0.45f); // gilt bands
                        c.Set(px, shelf + 1 + y, shade);
                    }
                    bx += w;
                }
            }
            if (alt)
            {
                // A brass sconce with a violet flame in the gap.
                c.Rect(14, 18, 4, 2, C("#8a6a2a"));
                c.Rect(15, 20, 2, 3, C("#d8d0b8"));
                c.Set(15, 23, LibraryGlow); c.Set(16, 23, C("#e8dcff")); c.Set(15, 24, LibraryGlow);
            }
            return c;
        }

        // ------------------------------------------------------------------ Decorations

        private static Dictionary<string, PixelCanvas> WithDecor2(Dictionary<string, PixelCanvas> decor)
        {
            decor["vines_7"] = GlowVines();
            decor["banner_7"] = Tatters(new[] { C("#0c1408"), C("#16220f"), C("#203218"), C("#2c4421"), C("#3a5a2c") }, GrottoGlow);
            decor["shrooms_7"] = Toadstools(26, 20, 3, 911);
            decor["candles_8"] = CandleStand();
            decor["banner_8"] = Tatters(new[] { C("#0c0a18"), C("#151126"), C("#1f1a38"), C("#2b244c"), C("#3a3264") }, C("#c8a24a"));
            decor["papers_8"] = Papers();
            return decor;
        }

        /// <summary>Roots hanging down the wall, beaded with glowing fungus.</summary>
        private static PixelCanvas GlowVines()
        {
            var c = new PixelCanvas(24, 44);
            var rng = new GameRandom(733);
            for (int s = 0; s < 5; s++)
            {
                float x = 3 + s * 4.2f;
                int len = 16 + rng.Range(0, 26);
                for (int y = 43; y > 43 - len; y--)
                {
                    x += rng.Range(-0.5f, 0.5f);
                    c.Set((int)x, y, C("#2a2216"));
                    c.Set((int)x + 1, y, C("#3e3222"));
                    if (y % 6 == s % 6) { c.Set((int)x - 1, y, GrottoGlow); c.Set((int)x - 1, y - 1, C("#4a7a2a")); }
                }
                c.Set((int)x, 43 - len, GrottoGlow);
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>A cluster of toadstools with red caps and pale spots (floor scatter and statues).</summary>
        private static PixelCanvas Toadstools(int w, int h, int count, ulong seed)
        {
            var c = new PixelCanvas(w, h);
            var rng = new GameRandom(seed);
            for (int i = 0; i < count; i++)
            {
                float k = 1f - i * 0.22f;
                int cx = Mathf.RoundToInt(w * (0.3f + 0.4f * Hash(i, (int)seed))) , stalkH = Mathf.RoundToInt(h * 0.45f * k);
                float r = w * 0.2f * k + 1.5f;
                for (int y = 0; y < stalkH; y++)
                    for (int x = -1; x <= 1; x++) c.Set(cx + x, y, Stalk[x < 0 ? 3 : x > 0 ? 1 : 2]);
                for (int y = 0; y <= r * 0.8f; y++)
                for (int x = -(int)r; x <= (int)r; x++)
                {
                    float d = x * x / (r * r) + y * y / (r * r * 0.55f);
                    if (d > 1f) continue;
                    int shade = y > r * 0.5f ? 4 : x < -r * 0.3f ? 3 : x > r * 0.4f ? 1 : 2;
                    c.Set(cx + x, stalkH + y, Cap[shade]);
                }
                for (int s = 0; s < 3; s++) c.Set(cx + rng.Range(-(int)r + 1, (int)r), stalkH + rng.Range(1, Mathf.Max(2, (int)(r * 0.6f))), C("#e8dcc0"));
                for (int x = -(int)r + 1; x < (int)r; x++) c.Set(cx + x, stalkH, x % 2 == 0 ? GrottoGlow : C("#4a6a2a")); // glowing gills
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>A tall iron candle stand dripping wax, its flames violet.</summary>
        private static PixelCanvas CandleStand()
        {
            var c = new PixelCanvas(20, 44);
            c.Rect(9, 2, 2, 30, C("#2a2a30"));
            c.Rect(5, 0, 10, 2, C("#3a3a42"));
            c.Rect(3, 30, 14, 2, C("#4a4a54"));
            foreach (int x in new[] { 4, 9, 14 })
            {
                c.Rect(x, 32, 2, 5 + (x % 3), C("#d8d0b8"));
                c.Set(x, 37 + x % 3, LibraryGlow);
                c.Set(x, 38 + x % 3, C("#e8dcff"));
                c.Set(x + 1, 32, C("#b0a890"));
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>Pages scattered over the floor.</summary>
        private static PixelCanvas Papers()
        {
            var c = new PixelCanvas(30, 14);
            var rng = new GameRandom(577);
            for (int p = 0; p < 4; p++)
            {
                int x0 = rng.Range(1, 21), y0 = rng.Range(1, 6);
                for (int y = 0; y < 6; y++)
                for (int x = 0; x < 7; x++)
                    c.Set(x0 + x + (p % 2 == 0 ? y / 3 : 0), y0 + y, y % 2 == 1 && x > 0 && x < 6 && rng.Chance(0.7f) ? C("#7a7262") : C("#c8bea6"));
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        // ------------------------------------------------------------------ Pillars, statues, the bookshelf

        /// <summary>The grotto's pillar is a toadstool as tall as a man; the library's an oak column with a sconce.</summary>
        public static PixelCanvas Pillar2(int area)
        {
            var c = new PixelCanvas(32, 64);
            if (area == 7)
            {
                for (int y = 0; y < 40; y++)
                {
                    int half = 4 + (y < 4 ? 2 - y / 2 : 0);
                    for (int x = -half; x < half; x++)
                    {
                        int shade = x < -half + 2 ? 3 : x > half - 3 ? 1 : 2;
                        if ((y * 3 + x) % 11 == 0) shade = Mathf.Max(0, shade - 1);
                        c.Set(16 + x, y, Stalk[shade]);
                    }
                }
                // A ring on the stalk, then the broad cap.
                for (int x = -6; x < 6; x++) { c.Set(16 + x, 28, Stalk[4]); c.Set(16 + x, 27, Stalk[1]); }
                for (int y = 0; y < 18; y++)
                for (int x = -15; x <= 15; x++)
                {
                    float d = x * x / 225f + (y - 2) * (y - 2) / 260f;
                    if (d > 1f || y < 0) continue;
                    int shade = y > 12 ? 4 : x < -7 ? 3 : x > 8 ? 1 : 2;
                    c.Set(16 + x, 40 + y, Cap[shade]);
                }
                for (int x = -14; x <= 14; x++) c.Set(16 + x, 40, x % 2 == 0 ? GrottoGlow : C("#3a5a22")); // glowing gills under the cap
                foreach (var (sx, sy) in new[] { (-9, 46), (-3, 52), (5, 49), (10, 44), (0, 45) })
                {
                    c.Set(16 + sx, sy, C("#ecdcc0"));
                    c.Set(17 + sx, sy, C("#ecdcc0"));
                    c.Set(16 + sx, sy + 1, C("#fff4dc"));
                }
            }
            else
            {
                // Dark oak, fluted, with brass bands and a candle in a sconce.
                const int x0 = 10, x1 = 22;
                for (int y = 4; y < 54; y++)
                for (int x = x0; x < x1; x++)
                {
                    int shade = x < x0 + 2 ? 4 : x > x1 - 3 ? 1 : (x - x0) % 3 == 2 ? 2 : 3;
                    c.Set(x, y, Oak[Mathf.Clamp(shade, 0, 4)]);
                }
                for (int y = 0; y < 4; y++) for (int x = 7; x < 25; x++) c.Set(x, y, Oak[y == 3 ? 4 : 2]);
                for (int y = 54; y < 60; y++) for (int x = 7 + (y - 54) / 2; x < 25 - (y - 54) / 2; x++) c.Set(x, y, Oak[y == 59 ? 4 : 3]);
                foreach (int by in new[] { 10, 44 })
                    for (int x = x0 - 1; x <= x1; x++) { c.Set(x, by, C("#6a4a1a")); c.Set(x, by + 1, C("#c8a24a")); }
                c.Rect(13, 28, 6, 2, C("#8a6a2a"));
                c.Rect(15, 30, 2, 4, C("#d8d0b8"));
                c.Set(15, 34, LibraryGlow); c.Set(16, 34, C("#e8dcff")); c.Set(15, 35, LibraryGlow);
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>The grotto's statue is a tall cluster of toadstools; the library's a lectern with an open book.</summary>
        public static PixelCanvas Statue2(int area)
        {
            if (area == 7) return Toadstools(40, 52, 3, 4242);
            var c = new PixelCanvas(36, 48);
            // The lectern: a carved post on a foot, a slanted desk, the book open on it.
            c.Rect(8, 0, 20, 3, Oak[2]);
            c.Rect(15, 3, 6, 26, Oak[3]);
            c.Rect(16, 3, 2, 26, Oak[4]);
            for (int y = 0; y < 8; y++) for (int x = 4 + y / 2; x < 32 - y / 3; x++) c.Set(x, 29 + y, Oak[y == 7 ? 4 : 2]);
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 22; x++)
            {
                int px = 7 + x, py = 35 + y + (x < 11 ? x / 4 : (21 - x) / 4);
                c.Set(px, py, x == 10 || x == 11 ? C("#8a8270") : (y % 2 == 1 && x % 11 > 1 && x % 11 < 10) ? C("#6a6252") : C("#d8ceb4"));
            }
            c.Set(12, 43, LibraryGlow); c.Set(25, 43, LibraryGlow);
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>A tall case of books (2.6 tiles wide), its top carved, three shelves full.</summary>
        public static PixelCanvas Bookshelf()
        {
            var rng = new GameRandom(1999);
            var c = new PixelCanvas(86, 76);
            const int x0 = 2, x1 = 84;
            for (int y = 0; y < 70; y++)
            for (int x = x0; x < x1; x++)
            {
                bool frame = x < x0 + 3 || x >= x1 - 3 || y < 3 || y >= 66;
                c.Set(x, y, frame ? Oak[x < x0 + 2 ? 4 : x >= x1 - 2 ? 1 : 3] : Oak[0]);
            }
            for (int y = 70; y < 76; y++) for (int x = x0 + (y - 70); x < x1 - (y - 70); x++) c.Set(x, y, Oak[y == 75 ? 4 : 3]);
            foreach (int shelf in new[] { 3, 24, 45 })
            {
                for (int x = x0 + 3; x < x1 - 3; x++) { c.Set(x, shelf, Oak[4]); c.Set(x, shelf + 1, Oak[2]); }
                int bx = x0 + 4;
                while (bx < x1 - 5)
                {
                    int w = 3 + rng.Range(0, 3), h = 13 + rng.Range(0, 6);
                    if (rng.Chance(0.06f)) { bx += w + 2; continue; }
                    var col = BookColors[rng.Range(0, BookColors.Length)];
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w && bx + x < x1 - 4; x++)
                    {
                        var shade = x == 0 ? Color32.Lerp(col, Color.white, 0.2f) : x == w - 1 ? Color32.Lerp(col, Color.black, 0.4f) : col;
                        if (y == 3 || y == h - 4) shade = Color32.Lerp(shade, C("#c8a24a"), 0.5f);
                        c.Set(bx + x, shelf + 2 + y, shade);
                    }
                    bx += w;
                }
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        // ------------------------------------------------------------------ Traps

        /// <summary>A crusted vent in the floor ringed with fungus: idle, swelling (warning), puffing (two frames).</summary>
        public static List<PixelCanvas> SporeVent()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(40, 30);
                float swell = f == 1 ? 1.2f : f >= 2 ? 1.1f : 1f;
                for (int y = 0; y < 30; y++)
                for (int x = 0; x < 40; x++)
                {
                    float dx = (x + 0.5f - 20f) / (13f * swell), dy = (y + 0.5f - 12f) / (8f * swell), d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    if (d < 0.22f) { c.Set(x, y, f >= 2 ? C("#4a7a2a") : C("#0a0d07")); continue; }  // the mouth
                    c.Set(x, y, d < 0.4f ? C("#3a4a2a") : dy > 0.2f ? Rock[4] : Rock[3]);
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    int x = 20 + Mathf.RoundToInt(Mathf.Cos(a) * 11f * swell), y = 12 + Mathf.RoundToInt(Mathf.Sin(a) * 6.5f * swell);
                    c.Set(x, y, Cap[3]); c.Set(x, y + 1, Cap[4]);
                    c.Set(x + 1, y, f == 1 ? C("#d8ffb0") : GrottoGlow);
                }
                if (f >= 2)
                    for (int i = 0; i < 10; i++)
                        c.Set(20 + (int)(Hash(i, f) * 14f) - 7, 14 + (int)(Hash(i + 5, f) * 14f), i % 2 == 0 ? C("#c8ff9a") : GrottoGlow);
                c.Outline(PixelRenderer.Outline);
                frames.Add(c);
            }
            return frames;
        }

        /// <summary>A page on the floor, written in a dead hand: quiet, burning violet (warning), burnt to ash.</summary>
        public static List<PixelCanvas> Glyph()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 3; f++)
            {
                var c = new PixelCanvas(44, 36);
                if (f == 1)
                    c.Ring(22f, 18f, 14f, 15.5f, LibraryGlow);
                for (int y = 0; y < 20; y++)
                for (int x = 0; x < 16; x++)
                {
                    int px = 14 + x + y / 6, py = 8 + y;
                    if (f == 2)
                    {
                        if (Hash(x, y + 3) > 0.45f) c.Set(px, py, Hash(x + 4, y) > 0.7f ? C("#3a3430") : C("#1a1614"));
                        continue;
                    }
                    bool line = y % 3 == 1 && x > 1 && x < 14 && Hash(x, y) > 0.25f;
                    c.Set(px, py, line ? (f == 1 ? LibraryGlow : C("#4a3a5a")) : C("#cfc4a8"));
                }
                if (f == 1) for (int i = 0; i < 6; i++) c.Set(10 + (int)(Hash(i, 2) * 24f), 6 + (int)(Hash(i, 9) * 24f), C("#e8dcff"));
                c.Outline(PixelRenderer.Outline);
                frames.Add(c);
            }
            return frames;
        }
    }
}
