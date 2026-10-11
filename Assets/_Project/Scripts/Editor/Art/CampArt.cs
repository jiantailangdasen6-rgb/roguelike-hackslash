using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// The camp between runs: night-time courtyard in front of a ruined chapel. Ground tiles, the campfire, the
    /// gate into the labyrinth, tents, the stations' props (mirror, weapon rack, charm table, rune tablet) and the
    /// two people who live there (the old master and the charm maker).
    /// </summary>
    public static class CampArt
    {
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static Color32 A(Color32 c, byte a) => new(c.r, c.g, c.b, a);
        private static readonly Color32 Outline = PixelRenderer.Outline;

        private static readonly Color32[] Earth = { C("#0e0b0a"), C("#15110e"), C("#1c1713"), C("#241d17"), C("#2d251d") };
        private static readonly Color32[] Moss = { C("#141b12"), C("#1c2618"), C("#26331f"), C("#34452a") };
        private static readonly Color32[] Stone = { C("#18151b"), C("#2a2631"), C("#3a3542"), C("#4c4656"), C("#5f586a"), C("#77708a") };
        private static readonly Color32[] Wood = { C("#1d140c"), C("#372616"), C("#563b21"), C("#78552e"), C("#9a7340") };
        private static readonly Color32[] DeadWood = { C("#0f0b09"), C("#1a1410"), C("#261d17"), C("#33281f"), C("#433528") };
        private static readonly Color32[] Gold = { C("#2e1f0a"), C("#573d12"), C("#87641c"), C("#bb912f"), C("#e6c766") };
        private static readonly Color32[] Cloth = { C("#1f0d0c"), C("#36160f"), C("#4f2215"), C("#6b331e"), C("#8a4a2a") };
        private static readonly Color32[] Violet = { C("#120a1c"), C("#24133a"), C("#3a1f5c"), C("#55307f"), C("#7547a8") };
        private static readonly Color32[] Iron = { C("#141414"), C("#262626"), C("#3b3b3d"), C("#57585c"), C("#7c7e84") };
        private static readonly Color32[] Straw = { C("#2b220f"), C("#4a3b19"), C("#6b5a2a"), C("#8f7b3c"), C("#b09a50") };
        private static readonly Color32[] Fire = { C("#5f151d"), C("#a31d28"), C("#ff5a2a"), C("#ff9a3c"), C("#ffe08a"), C("#fff8e0") };

        // ================================================================ Sheets (Resources/Sheets/camp_*)

        public static Dictionary<string, PropArt.Sheet> Sheets()
        {
            var bottom = new Vector2(0.5f, 0.04f);
            PropArt.Sheet One(PixelCanvas c, Vector2? pivot = null) => new() { Frames = new List<PixelCanvas> { c }, Pivot = pivot ?? bottom };
            return new Dictionary<string, PropArt.Sheet>
            {
                ["camp_fire"] = new() { Frames = Campfire(), Pivot = new Vector2(0.5f, 0.1f) },
                ["camp_gate"] = One(Gate()),
                ["camp_tent"] = One(Tent()),
                ["camp_mirror"] = new() { Frames = Mirror(), Pivot = bottom },
                ["camp_rack"] = One(Rack()),
                ["camp_dummy"] = One(Dummy()),
                ["camp_table"] = One(Table()),
                ["camp_tablet"] = new() { Frames = Tablet(), Pivot = bottom },
                ["camp_log"] = One(Log(), new Vector2(0.5f, 0.2f)),
                ["camp_tree"] = One(DeadTree()),
                ["camp_tree2"] = One(DeadTree(seed: 9, height: 92)),
                ["camp_stones"] = One(StoneRing(), new Vector2(0.5f, 0.5f)),
                ["camp_chest"] = One(Chest()),
                ["camp_board"] = One(Board()),
                ["fx_flask"] = new() { Frames = Flask(), Pivot = new Vector2(0.5f, 0.5f) },
                ["prop_campfire"] = new() { Frames = Campfire(), Pivot = new Vector2(0.5f, 0.1f) },
            };
        }

        /// <summary>32x32 ground tiles: packed earth with moss, pebbles and roots (seamless).</summary>
        public static Dictionary<string, PixelCanvas> Tiles() => new()
        {
            ["camp_ground0"] = Ground(1, 0),
            ["camp_ground1"] = Ground(2, 1),
            ["camp_ground2"] = Ground(3, 2),
            ["camp_ground3"] = Ground(4, 3),
        };

        // ================================================================ Ground

        private static PixelCanvas Ground(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 7351 + 11);
            var c = new PixelCanvas(32, 32);
            // Coarse periodic value noise (4x4 cells of 8 px), so neighbouring tiles join without seams.
            var cells = new float[4, 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++) cells[x, y] = rng.Value();
            float Noise(int px, int py)
            {
                float fx = px / 8f, fy = py / 8f;
                int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0, ty = fy - y0;
                tx = tx * tx * (3 - 2 * tx);
                ty = ty * ty * (3 - 2 * ty);
                float a = cells[x0 & 3, y0 & 3], b = cells[(x0 + 1) & 3, y0 & 3];
                float d = cells[x0 & 3, (y0 + 1) & 3], e = cells[(x0 + 1) & 3, (y0 + 1) & 3];
                return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(d, e, tx), ty);
            }
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float n = Noise(x, y) + (rng.Value() - 0.5f) * 0.22f;
                int shade = n < 0.22f ? 1 : n < 0.55f ? 2 : n < 0.82f ? 3 : 4;
                var col = Earth[shade];
                // Moss in the low, damp patches.
                if (n < 0.3f && rng.Chance(0.55f)) col = Moss[n < 0.18f ? 1 : 0];
                c.Set(x, y, col);
            }
            // Pebbles: light top, dark shadow underneath.
            int pebbles = 3 + rng.Range(0, 4);
            for (int i = 0; i < pebbles; i++)
            {
                int px = rng.Range(1, 30), py = rng.Range(2, 30);
                bool big = rng.Chance(0.3f);
                c.Set(px, py - 1, Earth[0]);
                c.Set(px + 1, py - 1, Earth[0]);
                c.Set(px, py, Stone[3]);
                c.Set(px + 1, py, Stone[2]);
                if (big)
                {
                    c.Set(px, py + 1, Stone[4]);
                    c.Set(px + 1, py + 1, Stone[3]);
                    c.Set(px + 2, py, Stone[1]);
                }
            }
            // Grass tufts (more on some variants), a root on one.
            int tufts = variant == 2 ? 6 : variant == 3 ? 1 : 3;
            for (int i = 0; i < tufts; i++)
            {
                int tx = rng.Range(2, 29), ty = rng.Range(2, 28);
                c.Set(tx, ty, Moss[1]);
                c.Set(tx - 1, ty + 1, Moss[2]);
                c.Set(tx + 1, ty + 1, Moss[2]);
                c.Set(tx, ty + 2, Moss[3]);
                if (rng.Chance(0.5f)) c.Set(tx + 2, ty + 2, Moss[2]);
            }
            if (variant == 3)
            {
                float y = rng.Range(8f, 24f);
                for (int x = 0; x < 32; x++)
                {
                    y += Mathf.Sin(x * 0.4f) * 0.5f;
                    c.Set(x, (int)y, DeadWood[3]);
                    c.Set(x, (int)y - 1, DeadWood[1]);
                }
            }
            return c;
        }

        // ================================================================ Campfire

        private static List<PixelCanvas> Campfire()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 6; f++)
            {
                var rng = new GameRandom((ulong)(f * 31 + 5));
                var c = new PixelCanvas(48, 64);
                // Logs (crossed) and glowing embers at the base.
                for (int t = -1; t <= 1; t++)
                {
                    c.Line(8, 4 + t, 40, 11 + t, t == 1 ? Wood[3] : Wood[1 + (t + 1)]);
                    c.Line(8, 11 + t, 40, 4 + t, t == 1 ? Wood[3] : Wood[2]);
                }
                for (int i = 0; i < 14; i++)
                {
                    int ex = rng.Range(14, 34), ey = rng.Range(5, 11);
                    c.Set(ex, ey, rng.Chance(0.5f) ? Fire[2] : Fire[3]);
                }
                // Flames: nested tongues that sway and flicker per frame.
                float height = 30f + (f % 3) * 3f + (f == 4 ? 4f : 0f);
                void Tongue(float cx, float baseY, float h, float w, float phase, int layer)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float k = y / h;
                        float half = w * 0.5f * (1f - Mathf.Pow(k, 1.6f)) * (y < 3 ? 0.6f + y * 0.13f : 1f);
                        float sway = Mathf.Sin(k * 5f + phase + f * 1.05f) * 2.2f * k;
                        float x0 = cx + sway - half, x1 = cx + sway + half;
                        for (int x = Mathf.RoundToInt(x0); x <= Mathf.RoundToInt(x1); x++) c.Set(x, (int)baseY + y, Fire[layer]);
                    }
                }
                Tongue(24, 8, height, 22f, 0f, 1);
                Tongue(19, 9, height * 0.62f, 11f, 1.7f, 1);
                Tongue(29, 9, height * 0.7f, 11f, 3.1f, 1);
                Tongue(24, 8, height * 0.8f, 16f, 0.4f, 2);
                Tongue(24, 9, height * 0.58f, 10f, 0.9f, 3);
                Tongue(24, 9, height * 0.36f, 6f, 1.3f, 4);
                Tongue(24, 10, height * 0.18f, 3f, 1.6f, 5);
                // Loose flame wisps and sparks above.
                for (int i = 0; i < 3; i++)
                {
                    int wx = 18 + rng.Range(0, 13), wy = (int)(8 + height + 1 + rng.Range(0, 6));
                    c.Set(wx, wy, Fire[2]);
                    c.Set(wx, wy + 1, Fire[1]);
                }
                for (int i = 0; i < 2; i++) c.Set(rng.Range(12, 36), (int)(height + 14 + rng.Range(0, 10)), Fire[4]);
                // no outline: fire stays soft
                frames.Add(c);
            }
            return frames;
        }

        /// <summary>The ring of stones around the fire (drawn flat on the ground under the fire sheet).</summary>
        private static PixelCanvas StoneRing()
        {
            var c = new PixelCanvas(64, 32);
            var rng = new GameRandom(77);
            // Scorched earth and ash.
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = (x + 0.5f - 32f) / 26f, dy = (y + 0.5f - 16f) / 11f;
                float d = dx * dx + dy * dy;
                if (d < 1f) c.Set(x, y, d < 0.45f ? C("#0b0807") : rng.Chance(0.5f) ? C("#141010") : C("#1a1513"));
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                float sx = 32 + Mathf.Cos(a) * 25f, sy = 16 + Mathf.Sin(a) * 10f;
                float r = 2.6f + rng.Value() * 1.2f;
                c.Disc(sx, sy, r, Stone[1]);
                c.Disc(sx - 0.6f, sy + 0.6f, r - 0.9f, Stone[3]);
                c.Set((int)(sx - 1), (int)(sy + 1), Stone[4]);
            }
            c.Outline(Outline);
            return c;
        }

        // ================================================================ The gate into the labyrinth

        private static PixelCanvas Gate()
        {
            const int W = 168, H = 124;
            const float cx = 84f, cy = 52f, inner = 54f, outer = 70f;
            var c = new PixelCanvas(W, H);
            var rng = new GameRandom(404);

            // Dark opening (the portal is drawn over it at runtime).
            for (int y = 0; y < H; y++)
            for (int x = 30; x < 138; x++)
            {
                bool inside = y < cy || (x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) < inner * inner;
                if (inside) c.Set(x, y, y < 6 ? C("#120d10") : C("#08060a"));
            }
            // Arch: voussoirs with mortar lines, lit from the upper left.
            for (int y = (int)cy; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < inner || d > outer) continue;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // 0..180
                bool mortar = Mathf.Abs(Mathf.Repeat(ang + 7.5f, 15f) - 7.5f) < 0.9f || d < inner + 1f;
                int shade = mortar ? 0 : d > outer - 2f ? 4 : dx < 0 ? 3 : 2;
                if (!mortar && rng.Chance(0.07f)) shade--;
                c.Set(x, y, Stone[Mathf.Clamp(shade, 0, 5)]);
            }
            // Pillars (cut blocks).
            foreach (int x0 in new[] { 14, 138 })
            {
                for (int y = 0; y < (int)cy + 2; y++)
                for (int x = x0; x < x0 + 16; x++)
                {
                    bool joint = (y % 14 == 0) || ((y / 14) % 2 == 0 ? x == x0 + 8 : false);
                    int shade = joint ? 0 : x == x0 ? 4 : x < x0 + 5 ? 3 : x > x0 + 13 ? 1 : 2;
                    if (!joint && rng.Chance(0.06f)) shade--;
                    c.Set(x, y, Stone[Mathf.Clamp(shade, 0, 5)]);
                }
                // Base plinth
                c.Rect(x0 - 3, 0, 22, 5, Stone[2]);
                for (int x = x0 - 3; x < x0 + 19; x++) c.Set(x, 4, Stone[4]);
            }
            // Keystone with a glowing red sigil.
            for (int y = (int)(cy + inner - 2); y < (int)(cy + outer + 2) && y < H; y++)
            for (int x = 76; x < 92; x++) c.Set(x, y, x == 76 ? Stone[5] : x > 89 ? Stone[1] : Stone[3]);
            var red = C("#ff3b2f");
            int ky = (int)(cy + inner + 6);
            c.Line(80, ky, 88, ky + 8, red);
            c.Line(88, ky, 80, ky + 8, red);
            c.Set(84, ky + 4, C("#ffd2a6"));
            // Cracks and hanging chains / vines for a ruined look.
            c.Line(30, 70, 38, 86, Stone[0]);
            c.Line(130, 92, 124, 104, Stone[0]);
            for (int y = 34; y < 62; y++) if (y % 3 != 0) c.Set(33, y, Iron[y % 3 == 1 ? 2 : 3]);
            for (int y = 28; y < 66; y++) if (y % 3 != 0) c.Set(134, y, Iron[y % 3 == 1 ? 2 : 3]);
            for (int i = 0; i < 26; i++)
            {
                float a = rng.Range(15f, 165f) * Mathf.Deg2Rad;
                float r = rng.Range(outer - 6f, outer + 1f);
                c.Set((int)(cx + Mathf.Cos(a) * r), (int)(cy + Mathf.Sin(a) * r), rng.Chance(0.5f) ? Moss[2] : Moss[3]);
            }
            // Two steps in front.
            for (int x = 26; x < 142; x++)
            {
                c.Set(x, 3, Stone[3]);
                c.Set(x, 2, Stone[2]);
                c.Set(x, 1, Stone[1]);
            }
            c.Outline(Outline);
            return c;
        }

        // ================================================================ Tent

        private static PixelCanvas Tent()
        {
            var c = new PixelCanvas(104, 76);
            const int apexX = 52, apexY = 62, baseY = 4, halfW = 48;
            for (int y = baseY; y <= apexY; y++)
            {
                float k = (y - baseY) / (float)(apexY - baseY);
                int half = Mathf.RoundToInt(halfW * (1f - k));
                for (int x = apexX - half; x <= apexX + half; x++)
                {
                    bool left = x < apexX;
                    int shade = left ? (x < apexX - half + 3 ? 2 : 3) : (x > apexX + half - 3 ? 0 : 1);
                    if ((y - baseY) % 9 == 0) shade = Mathf.Max(0, shade - 1); // seams
                    c.Set(x, y, Cloth[shade]);
                }
            }
            // Ridge line
            for (int y = baseY; y <= apexY; y++) c.Set(apexX, y, Cloth[4]);
            // Opening with a flap folded back.
            for (int y = baseY; y < 40; y++)
            {
                float k = (y - baseY) / 36f;
                int half = Mathf.RoundToInt(14 * (1f - k));
                for (int x = apexX - half; x <= apexX + half; x++) c.Set(x, y, C("#0a0605"));
                if (half > 0) c.Set(apexX - half - 1, y, Cloth[4]);
            }
            for (int y = baseY; y < 26; y++) for (int x = apexX + 15 - (y - baseY) / 2; x < apexX + 22 - (y - baseY) / 3; x++) c.Set(x, y, Cloth[3]);
            // Warm light inside.
            c.Set(apexX - 2, baseY + 2, Fire[3]);
            c.Set(apexX + 1, baseY + 3, Fire[2]);
            // Poles and ropes to stakes.
            c.Line(apexX, apexY, apexX - 3, apexY + 8, Wood[2]);
            c.Line(apexX, apexY, apexX + 3, apexY + 8, Wood[1]);
            c.Line(apexX - 20, 36, 1, 2, C("#6b5d48"));
            c.Line(apexX + 20, 36, 102, 2, C("#4d4334"));
            c.Rect(0, 0, 2, 4, Wood[3]);
            c.Rect(101, 0, 2, 4, Wood[2]);
            // Shadow at the base
            for (int x = 4; x < 100; x++) c.Set(x, baseY - 1, Cloth[0]);
            c.Outline(Outline);
            return c;
        }

        // ================================================================ Station props

        private static List<PixelCanvas> Mirror()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 6; f++)
            {
                var c = new PixelCanvas(44, 84);
                const float cx = 22f, cy = 48f, rx = 15f, ry = 28f;
                // Wooden stand
                c.Line(10, 0, 18, 18, Wood[2]);
                c.Line(34, 0, 26, 18, Wood[1]);
                c.Rect(20, 2, 4, 18, Wood[2]);
                for (int y = 0; y < 84; y++)
                for (int x = 0; x < 44; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1.2f) continue;
                    if (d > 0.82f)
                    {
                        // Gilded frame, lit from the left
                        int shade = dx < -0.3f ? 4 : dx < 0.2f ? 3 : 2;
                        if (d > 1.12f) shade = 1;
                        c.Set(x, y, Gold[shade]);
                        continue;
                    }
                    // Glass: deep blue-black with a slow diagonal shimmer.
                    float band = (x - y * 0.55f + f * 6f) % 40f;
                    var glass = d < 0.4f ? C("#141c2c") : C("#0d1220");
                    if (band > 0 && band < 3) glass = C("#3e4f66");
                    else if (band >= 3 && band < 5) glass = C("#5f7690");
                    c.Set(x, y, glass);
                }
                // A faint figure in the glass (your reflection waits there).
                c.Rect(20, 36, 4, 12, C("#1d2638"));
                c.Disc(22, 52, 3f, C("#1d2638"));
                // Crest with a red gem
                c.Disc(22, 79, 4f, Gold[3]);
                c.Disc(22, 79, 2f, C(f % 3 == 0 ? "#ff8a7a" : "#ff5a4a"));
                c.Set(16, 76, Gold[2]);
                c.Set(28, 76, Gold[2]);
                if (f == 2 || f == 3) c.Set(14 + f * 2, 60 - f * 3, C("#c8dcff"));
                c.Outline(Outline);
                frames.Add(c);
            }
            return frames;
        }

        private static PixelCanvas Rack()
        {
            var c = new PixelCanvas(64, 56);
            // Posts and bars
            c.Rect(4, 0, 4, 44, Wood[3]);
            c.Rect(56, 0, 4, 44, Wood[1]);
            for (int x = 3; x < 61; x++)
            {
                c.Set(x, 40, Wood[4]);
                c.Set(x, 39, Wood[2]);
                c.Set(x, 16, Wood[3]);
                c.Set(x, 15, Wood[1]);
            }
            // Swords leaning on the top bar
            foreach (var (x, tilt) in new[] { (16, 2), (26, -1), (36, 1) })
            {
                for (int y = 4; y < 50; y++)
                {
                    int xx = x + tilt * (y - 4) / 16;
                    c.Set(xx, y, y > 38 ? Gold[3] : C("#86919e"));
                    c.Set(xx + 1, y, y > 38 ? Gold[1] : C("#bfc8d1"));
                }
                c.Rect(x - 3 + tilt * 2, 38, 8, 2, Gold[2]);
            }
            // Spear
            for (int y = 0; y < 54; y++) c.Set(46, y, Wood[3]);
            c.Set(46, 54, C("#bfc8d1"));
            c.Set(45, 53, C("#86919e"));
            c.Set(47, 53, C("#86919e"));
            // Round shield hanging on the left post
            c.Disc(10, 26, 8f, Iron[2]);
            c.Disc(10, 26, 6.5f, Wood[2]);
            c.Disc(9, 27, 4f, Wood[3]);
            c.Disc(10, 26, 1.6f, Iron[4]);
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Dummy()
        {
            var c = new PixelCanvas(36, 62);
            c.Rect(16, 0, 4, 40, Wood[2]);
            c.Rect(16, 0, 1, 40, Wood[3]);
            for (int x = 4; x < 32; x++) { c.Set(x, 40, Wood[3]); c.Set(x, 39, Wood[1]); }
            // Straw body
            for (int y = 18; y < 44; y++)
            for (int x = 10; x < 26; x++)
            {
                float k = Mathf.Abs(x - 17.5f) / 8f;
                if (k > 1f - (y < 22 ? (22 - y) * 0.1f : 0f)) continue;
                int shade = x < 14 ? 4 : x > 22 ? 1 : (x + y) % 5 == 0 ? 2 : 3;
                c.Set(x, y, Straw[shade]);
            }
            for (int x = 10; x < 26; x++) c.Set(x, 30, C("#4a3b19"));
            // Sack head with stitched eyes
            c.Disc(18, 50, 7f, C("#5a4a35"));
            c.Disc(17, 51, 5.5f, C("#76624a"));
            c.Line(14, 50, 16, 52, C("#2a1f16"));
            c.Line(16, 50, 14, 52, C("#2a1f16"));
            c.Line(20, 50, 22, 52, C("#2a1f16"));
            c.Line(22, 50, 20, 52, C("#2a1f16"));
            c.Line(15, 46, 21, 46, C("#2a1f16"));
            // A broken arrow stuck in it
            c.Line(23, 34, 31, 38, Wood[3]);
            c.Set(31, 38, C("#c9d2dc"));
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Table()
        {
            var c = new PixelCanvas(96, 60);
            // Legs and top
            c.Rect(8, 0, 4, 20, Wood[2]);
            c.Rect(84, 0, 4, 20, Wood[1]);
            for (int y = 18; y < 24; y++) for (int x = 4; x < 92; x++) c.Set(x, y, Wood[y == 23 ? 4 : y == 18 ? 1 : 3]);
            // Violet cloth hanging over the front
            for (int y = 8; y < 23; y++)
            for (int x = 22; x < 74; x++)
            {
                int shade = x < 25 ? 4 : x > 70 ? 1 : (x / 6) % 2 == 0 ? 3 : 2;
                if (y < 10 && (x % 6) > 3) continue; // ragged hem
                c.Set(x, y, Violet[shade]);
            }
            for (int x = 22; x < 74; x++) c.Set(x, 21, Gold[3]);
            // Candles
            foreach (int x in new[] { 10, 86 })
            {
                c.Rect(x, 24, 3, 7, C("#c8bfa5"));
                c.Set(x + 1, 31, Fire[4]);
                c.Set(x + 1, 32, Fire[2]);
            }
            // Bottles: green poison, red blood, blue soul
            void Bottle(int x, Color32 glass, Color32 shine, int h)
            {
                c.Rect(x, 24, 6, h, glass);
                c.Rect(x + 2, 24 + h, 2, 3, glass);
                c.Set(x + 1, 25 + h / 2, shine);
                c.Set(x + 2, 27 + h, C("#8a6a40"));
            }
            Bottle(18, C("#3f7a2a"), C("#b6ff4a"), 8);
            Bottle(27, C("#7a1a22"), C("#ff5a4a"), 6);
            Bottle(64, C("#2a4a7a"), C("#8fd3ff"), 9);
            // Crystal ball on a stand
            c.Rect(42, 24, 10, 2, Gold[2]);
            c.Disc(47, 31, 6f, C("#2f6f9f"));
            c.Disc(46, 32, 4.5f, C("#5fa8ff"));
            c.Disc(45, 33, 1.6f, C("#effaff"));
            // Amulets on a string above the table
            for (int x = 6; x < 90; x++) c.Set(x, 54 - (int)(Mathf.Sin((x - 6) / 84f * Mathf.PI) * 6f), C("#4d4334"));
            foreach (int x in new[] { 20, 36, 58, 74 })
            {
                int y = 54 - (int)(Mathf.Sin((x - 6) / 84f * Mathf.PI) * 6f);
                c.Line(x, y, x, y - 5, Gold[2]);
                c.Disc(x, y - 7, 2f, x % 3 == 0 ? Gold[4] : C("#ff5a4a"));
            }
            // Skull
            c.Disc(78, 28, 3.5f, C("#b8ae95"));
            c.Set(77, 28, C("#100c11"));
            c.Set(79, 28, C("#100c11"));
            c.Outline(Outline);
            return c;
        }

        private static List<PixelCanvas> Tablet()
        {
            var frames = new List<PixelCanvas>();
            float[] glow = { 0.55f, 0.75f, 1f, 0.75f };
            for (int f = 0; f < glow.Length; f++)
            {
                var c = new PixelCanvas(44, 72);
                var rng = new GameRandom(55);
                for (int y = 4; y < 66; y++)
                for (int x = 8; x < 36; x++)
                {
                    float top = 56f + Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((x + 0.5f - 22f) / 14f, 2f))) * 10f;
                    if (y > top) continue;
                    int shade = x < 11 ? 4 : x > 32 ? 1 : 2 + (rng.Chance(0.08f) ? -1 : 0);
                    if (y > top - 1.5f) shade = 4;
                    c.Set(x, y, Stone[shade]);
                }
                // Glowing runes (pulsing)
                var rune = Color32.Lerp(C("#5f151d"), C("#ff5a4a"), glow[f]);
                var hot = Color32.Lerp(C("#a31d28"), C("#ffd2a6"), glow[f]);
                int[][] marks =
                {
                    new[] { 15, 50, 15, 58 }, new[] { 15, 54, 19, 58 }, new[] { 22, 50, 26, 58 }, new[] { 26, 50, 22, 58 },
                    new[] { 15, 38, 19, 44 }, new[] { 19, 44, 15, 44 }, new[] { 23, 38, 28, 38 }, new[] { 25, 38, 25, 45 },
                    new[] { 15, 26, 28, 26 }, new[] { 18, 22, 18, 30 }, new[] { 24, 22, 27, 30 },
                };
                foreach (var m in marks) c.Line(m[0], m[1], m[2], m[3], rune);
                c.Set(25, 41, hot);
                c.Set(18, 54, hot);
                // Rubble at the base
                for (int x = 4; x < 40; x++) for (int y = 0; y < 5; y++) if (Mathf.Abs(x - 22) < 18 - y * 2) c.Set(x, y, Stone[y == 4 ? 3 : 1]);
                c.Outline(Outline);
                frames.Add(c);
            }
            return frames;
        }

        /// <summary>The vault: a big iron-banded chest, lid ajar with a warm glint of gold inside.</summary>
        private static PixelCanvas Chest()
        {
            var c = new PixelCanvas(64, 48);
            // Body
            for (int y = 2; y < 24; y++)
            for (int x = 4; x < 60; x++)
            {
                int shade = x < 7 ? 4 : x > 56 ? 1 : (x - 4) % 13 == 0 ? 1 : 2;
                if (y == 23) shade = 3;
                c.Set(x, y, Wood[shade]);
            }
            // Lid, raised a little: dark gap with gold glinting inside
            for (int x = 6; x < 58; x++) { c.Set(x, 24, C("#0a0605")); c.Set(x, 25, x % 7 == 3 ? Gold[4] : Gold[2]); }
            for (int y = 26; y < 40; y++)
            for (int x = 3; x < 61; x++)
            {
                float arch = 34f + Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((x + 0.5f - 32f) / 29f, 2f))) * 6f;
                if (y > arch) continue;
                int shade = x < 6 ? 4 : x > 57 ? 1 : y > arch - 1.5f ? 4 : 3;
                c.Set(x, y, Wood[shade]);
            }
            // Iron bands and corner plates
            foreach (int x in new[] { 10, 52 })
                for (int y = 2; y < 41; y++) { c.Set(x, y, Iron[3]); c.Set(x + 1, y, Iron[2]); c.Set(x + 2, y, Iron[1]); }
            for (int x = 4; x < 60; x++) { c.Set(x, 2, Iron[2]); c.Set(x, 3, Iron[3]); }
            // Lock plate
            c.Rect(28, 15, 8, 10, Iron[2]);
            c.Rect(29, 16, 6, 8, Gold[3]);
            c.Rect(31, 18, 2, 3, C("#100c11"));
            c.Outline(Outline);
            return c;
        }

        /// <summary>Notice board for requests: two posts, a plank board with pinned notes, a lantern on top.</summary>
        private static PixelCanvas Board()
        {
            var c = new PixelCanvas(64, 64);
            c.Rect(6, 0, 4, 52, Wood[3]);
            c.Rect(54, 0, 4, 52, Wood[1]);
            for (int y = 18; y < 48; y++)
            for (int x = 4; x < 60; x++)
            {
                int shade = (y - 18) % 7 == 0 ? 1 : x < 7 ? 4 : x > 56 ? 1 : 2;
                c.Set(x, y, Wood[shade]);
            }
            for (int x = 2; x < 62; x++) { c.Set(x, 48, Wood[4]); c.Set(x, 49, Wood[3]); c.Set(x, 17, Wood[1]); }
            // Pinned notes (parchment), one with a red seal
            var paper = new[] { C("#8f8470"), C("#b8ab8e"), C("#d8ccad") };
            void Note(int x0, int y0, int w, int h, bool seal)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    c.Set(x, y, paper[x == x0 ? 2 : x == x0 + w - 1 || y == y0 ? 0 : 1]);
                for (int y = y0 + 3; y < y0 + h - 2; y += 2)
                for (int x = x0 + 2; x < x0 + w - 2; x++)
                    if ((x + y) % 5 != 0) c.Set(x, y, C("#5a4f40"));
                c.Set(x0 + w / 2, y0 + h - 1, Iron[4]);
                if (seal) c.Disc(x0 + w - 3, y0 + 3, 1.8f, C("#a31d28"));
            }
            Note(9, 24, 13, 18, false);
            Note(25, 21, 14, 22, true);
            Note(42, 26, 12, 16, false);
            // Lantern hanging from the top beam
            c.Line(32, 50, 32, 54, Iron[2]);
            c.Rect(29, 54, 7, 7, Iron[2]);
            c.Rect(30, 55, 5, 5, Fire[3]);
            c.Set(32, 57, Fire[5]);
            c.Outline(Outline);
            return c;
        }

        /// <summary>The alchemist's tumbling flask (6 turning frames). Pale glass: the game tints it per brew.</summary>
        private static List<PixelCanvas> Flask()
        {
            var frames = new List<PixelCanvas>();
            var liquid = C("#e8f0d8");
            var liquidDark = C("#a8b898");
            var glass = C("#f4f8fc");
            var cork = C("#9a7340");
            for (int f = 0; f < 6; f++)
            {
                var c = new PixelCanvas(16, 16);
                float a = f * 60f * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    // Rotate the pixel back into the upright bottle's space (centre 8,8).
                    float dx = x + 0.5f - 8f, dy = y + 0.5f - 8f;
                    float u = dx * cos + dy * sin, v = -dx * sin + dy * cos;
                    bool body = u * u + (v + 1.5f) * (v + 1.5f) <= 4.2f * 4.2f;
                    bool neck = Mathf.Abs(u) <= 1.2f && v > 1.5f && v <= 4.6f;
                    bool stopper = Mathf.Abs(u) <= 1.6f && v > 4.6f && v <= 6f;
                    if (stopper) c.Set(x, y, cork);
                    else if (neck) c.Set(x, y, glass);
                    else if (body) c.Set(x, y, v < 0.3f ? (u < -1.5f ? liquid : liquidDark) : glass);
                }
                c.Set(6, 9, C("#ffffff"));
                c.Outline(Outline);
                frames.Add(c);
            }
            return frames;
        }

        private static PixelCanvas Log()
        {
            var c = new PixelCanvas(60, 20);
            for (int y = 4; y < 16; y++)
            for (int x = 4; x < 56; x++)
            {
                int shade = y > 13 ? 4 : y > 10 ? 3 : y > 6 ? 2 : 1;
                if ((x * 7 + y * 3) % 11 == 0) shade--;
                c.Set(x, y, Wood[Mathf.Clamp(shade, 0, 4)]);
            }
            // Cut end with rings
            c.Disc(5, 10, 6f, Wood[3]);
            c.Disc(5, 10, 4f, C("#b08a55"));
            c.Disc(5, 10, 2f, Wood[3]);
            c.Set(5, 10, C("#b08a55"));
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas DeadTree(int seed = 3, int height = 108)
        {
            var c = new PixelCanvas(80, height + 4);
            var rng = new GameRandom((ulong)seed);
            void Branch(float x, float y, float angle, float length, float width, int depth)
            {
                for (int i = 0; i < length; i++)
                {
                    float k = i / length;
                    float w = Mathf.Max(1f, width * (1f - k * 0.7f));
                    angle += rng.Range(-6f, 6f);
                    x += Mathf.Sin(angle * Mathf.Deg2Rad);
                    y += Mathf.Cos(angle * Mathf.Deg2Rad);
                    for (int dx = 0; dx < Mathf.CeilToInt(w); dx++)
                        c.Set((int)(x - w * 0.5f) + dx, (int)y, DeadWood[dx == 0 ? 4 : dx == Mathf.CeilToInt(w) - 1 ? 1 : 2]);
                    if (depth > 0 && i > length * 0.35f && rng.Chance(0.08f))
                        Branch(x, y, angle + (rng.Chance(0.5f) ? 1 : -1) * rng.Range(25f, 50f), length * 0.55f, w * 0.6f, depth - 1);
                }
            }
            Branch(40, 0, 0f, height * 0.62f, 8f, 2);
            Branch(40, height * 0.4f, -38f, height * 0.4f, 4f, 1);
            Branch(41, height * 0.5f, 34f, height * 0.38f, 3.5f, 1);
            // Roots
            c.Line(30, 0, 36, 4, DeadWood[2]);
            c.Line(50, 0, 44, 4, DeadWood[1]);
            c.Outline(Outline);
            return c;
        }

        // ================================================================ Rest room chest (a breakable "enemy" sheet)

        /// <summary>Treasure chest in rest rooms: a glint while closed, a shake when hit, the lid flying open and coins spilling.</summary>
        public static List<Clip> TreasureChest()
        {
            var m = Palette.Standard();
            var body = new PixelPart("chest", P(10f, 0.5f), m, 0,
                "o#ooooooggggoooooo#o",
                "o#ooooooggkgoooooo#o",
                "o#ooooooggggoooooo#o",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "####################",
                "##................##");
            var lid = new PixelPart("lid", P(0.5f, 0.5f), m, 0,
                "...oooooooooooooo...",
                ".oooooooooooooooooo.",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "####################");
            var glint = new PixelPart("glint", P(1f, 1f), m, 0, ".G.", "GgG", ".G.");
            var coin = new PixelPart("coin", P(1f, 1f), m, 0, "gG", "Gg");

            Frame Chest(float lidAngle, float dx = 0f, bool shine = false)
            {
                var f = new Frame();
                f.Add(body, P(32f + dx, 10f), 0f, 4);
                f.Add(lid, P(22f + dx, 19f), lidAngle, 5);
                if (shine) f.Add(glint, P(38f + dx, 22f), 0f, 6);
                return f;
            }

            var idle = new Clip("idle", 3f, true);
            idle.Frames.Add(Chest(0f, shine: true));
            idle.Frames.Add(Chest(0f));
            idle.Frames.Add(Chest(0f));
            var hurt = new Clip("hurt", 14f, false);
            hurt.Frames.Add(Chest(6f, 1f));
            hurt.Frames.Add(Chest(-2f, -1f));
            var death = new Clip("death", 10f, false);
            for (int i = 0; i < 5; i++)
            {
                float k = (i + 1) / 5f;
                var f = Chest(Mathf.Lerp(20f, 110f, Mathf.Sqrt(k)));
                for (int s = 0; s < 7; s++)
                {
                    float a = (30f + s * 20f) * Mathf.Deg2Rad;
                    var at = P(32f, 22f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (4f + 14f * k) - P(0f, 10f * k * k);
                    if (at.y < 11f) at.y = 11f;
                    f.Add(coin, at, 0f, 7);
                }
                death.Frames.Add(f);
            }
            static Clip Same(string name, Clip from)
            {
                var c = new Clip(name, from.Fps, from.Loop);
                c.Frames.AddRange(from.Frames);
                return c;
            }
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }

        // ================================================================ People of the camp (idle only)

        private static Vector2 P(float x, float y) => new(x, y);

        private static IEnumerable<RigPose> Breathe(float arm, float armBack, float? weapon, float torso = 0f, float head = 0f)
        {
            for (int i = 0; i < 6; i++)
            {
                float b = i is 2 or 3 ? -1f : 0f;
                yield return new RigPose
                {
                    Bob = b,
                    Torso = torso,
                    Head = head + (i == 4 ? -3f : 0f),
                    ArmFront = arm - (b < 0 ? 2f : 0f),
                    ArmBack = armBack + (b < 0 ? 2f : 0f),
                    WeaponAbsolute = weapon,
                    Cape = -3f - (i % 3),
                };
            }
        }

        /// <summary>The master (師範): an old knight with a white beard, resting both hands on a greatsword.</summary>
        public static List<Clip> Master()
        {
            var m = Palette.Standard();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("head", P(5.5f, 0.5f), m, 0,
                    "...iiii....",
                    "..iiiiiii..",
                    ".iiissssss.",
                    ".iissssssss",
                    ".iisssssksk",
                    ".iissssssss",
                    "..isssssss.",
                    "..iiiiiiii.",
                    "..iiiiiiiii",
                    "...iiiiiii.",
                    "....iiiii..",
                    ".....iii..."),
                Torso = new PixelPart("coat", P(6f, 6.5f), m, 0,
                    "...llllll...",
                    "..llllllll..",
                    ".lllllcllll.",
                    ".lllllclllll",
                    ".lllllcllll.",
                    "..llllcllll.",
                    "..ggggggggg.",
                    "..lllllllll.",
                    "..lllllllll.",
                    ".llllllllll.",
                    ".lllll.lllll",
                    ".llll...llll"),
                LegFront = new PixelPart("leg", P(2f, 9.5f), m, 0, ".nn.", ".nn.", ".nn.", ".nn.", ".nn.", ".nn.", ".ll.", ".ll.", "llll", "llll"),
                LegBack = new PixelPart("leg", P(2f, 9.5f), m, 0, ".nn.", ".nn.", ".nn.", ".nn.", ".nn.", ".nn.", ".ll.", ".ll.", "llll", "llll"),
                ArmFront = new PixelPart("arm", P(2.5f, 8.5f), m, 0, ".lll.", "lllll", "lllll", "llll.", ".lll.", ".lll.", ".lll.", ".lll.", ".sss.", ".ss.."),
                ArmBack = new PixelPart("arm", P(2.5f, 8.5f), m, 0, ".lll.", "lllll", "lllll", "llll.", ".lll.", ".lll.", ".lll.", ".lll.", ".sss.", ".ss.."),
                Cape = new PixelPart("cape", P(7f, 13.5f), m, -1,
                    "...aaaaaa", "..aaaaaaa", "..aaaaaaa", ".aaaaaaa.", ".aaaaaaa.", ".aaaaaaa.", "aaaaaaaa.", "aaaaaaa..", "aaaaaaa..", "aaaaaa...", "aaaaaa...", "aa.aaa...", "a..aa...."),
                // Drawn blade-up with the grip at the bottom (like every weapon); held at 180° it points at the ground.
                Weapon = new PixelPart("greatsword", P(3.5f, 3.5f), m, 0,
                    "...w...",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    "..wWw..",
                    ".gwWwg.",
                    "ggggggg",
                    "...l...",
                    "...l...",
                    "...l...",
                    "..ggg..",
                    "...g..."),
                WeaponZ = 9,
            };
            return new List<Clip> { rig.Clip("idle", 5f, true, Breathe(arm: 104f, armBack: 98f, weapon: 180f)) };
        }

        /// <summary>The gravekeeper (墓守): a tall, ragged grey figure leaning on a spade. He knows the way down.</summary>
        public static List<Clip> Gravekeeper()
        {
            var m = Palette.Standard();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("hood", P(5.5f, 0.5f), m, 0,
                    "....tttt...",
                    "...ttttttt.",
                    "..ttttttttt",
                    ".tttttttTtt",
                    ".tttttkkkkt",
                    "tttttkkpkpk",
                    "tttttkkkkkk",
                    "tttttkkkkkt",
                    ".ttttttttt.",
                    "..tttttttt.",
                    "...ttttt..."),
                Torso = new PixelPart("coat", P(6f, 7.5f), m, 0,
                    "...tttttt...",
                    "..ttttttttt.",
                    ".tttttttttt.",
                    ".tttttttttt.",
                    ".ttttlttttt.",
                    "..tttltttt..",
                    "..llllllll..",
                    "..ttttttttt.",
                    "..ttttttttt.",
                    ".tttttttttt.",
                    ".ttttttttttt",
                    "tttttttttttt",
                    "ttt.tttt.ttt",
                    "tt...tt...tt"),
                LegFront = new PixelPart("leg", P(2f, 9.5f), m, 0, ".tt.", ".tt.", ".tt.", ".tt.", ".tt.", ".tt.", ".ll.", ".ll.", "llll", "llll"),
                LegBack = new PixelPart("leg", P(2f, 9.5f), m, 0, ".tt.", ".tt.", ".tt.", ".tt.", ".tt.", ".tt.", ".ll.", ".ll.", "llll", "llll"),
                ArmFront = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".ttt.", "ttttt", "ttttt", "tttt.", "tttt.", ".ttt.", ".ttt.", "ttttt", ".ppp.", ".pp.."),
                ArmBack = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".ttt.", "ttttt", "ttttt", "tttt.", "tttt.", ".ttt.", ".ttt.", "ttttt", ".ppp.", ".pp.."),
                // Spade: blade at the top, handle at the bottom; held at 180° the blade rests on the ground.
                Weapon = new PixelPart("spade", P(2.5f, 2.5f), m, 0,
                    ".###.",
                    "#####",
                    "#####",
                    "#####",
                    ".###.",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    ".ooo."),
                WeaponZ = 9,
            };
            return new List<Clip> { rig.Clip("idle", 4f, true, Breathe(arm: 100f, armBack: 92f, weapon: 180f, torso: -4f)) };
        }

        /// <summary>The charm maker (お守り職人): a hunched, hooded old woman holding up a little lantern.</summary>
        public static List<Clip> Crone()
        {
            var m = Palette.With(('r', Palette.Violet));
            var rig = new HumanoidRig
            {
                Head = new PixelPart("hood", P(5.5f, 0.5f), m, 0,
                    "....rrrr...",
                    "...rrrrrrr.",
                    "..rrrrrrrrr",
                    ".rrrrrrRrrr",
                    ".rrrrrkkkkr",
                    "rrrrrkspsps",
                    "rrrrrkssssk",
                    "rrrrrkksssk",
                    ".rrrrrkkkk.",
                    "..rrrrrrr..",
                    "...rrrrr..."),
                Torso = new PixelPart("robe", P(6f, 7.5f), m, 0,
                    "...rrrrrr...",
                    "..rrrrrrrrr.",
                    ".rrrrrrrrrrr",
                    ".rrrrrrrrrr.",
                    ".rrrrgrrrrr.",
                    "..rrrgrrrr..",
                    "..qqqqqqqq..",
                    "..rrrrrrrrr.",
                    "..rrrrrrrrr.",
                    ".rrrrrrrrrr.",
                    ".rrrrrrrrrrr",
                    "rrrrrrrrrrrr",
                    "rrrrrrrrrrrr",
                    "rrrr.rrrrrr."),
                LegFront = new PixelPart("boot", P(2f, 9.5f), m, 0, ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".ll.", ".ll.", "llll", "llll"),
                LegBack = new PixelPart("boot", P(2f, 9.5f), m, 0, ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".ll.", ".ll.", "llll", "llll"),
                ArmFront = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".rrr.", "rrrrr", "rrrrr", "rrrr.", "rrrr.", ".rrr.", ".rrr.", "rrrrr", ".ppp.", ".pp.."),
                ArmBack = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".rrr.", "rrrrr", "rrrrr", "rrrr.", "rrrr.", ".rrr.", ".rrr.", "rrrrr", ".ppp.", ".pp.."),
                // Chain at the bottom (in the hand); held at 180° the lantern hangs below it.
                Weapon = new PixelPart("lantern", P(2.5f, 0.5f), m, 0,
                    "..#..",
                    ".###.",
                    "#zzz#",
                    "#zZz#",
                    "#zzz#",
                    ".###.",
                    "..#..",
                    "..#..",
                    "..#.."),
                WeaponZ = 9,
            };
            return new List<Clip> { rig.Clip("idle", 5f, true, Breathe(arm: 70f, armBack: -10f, weapon: 180f, torso: -14f, head: 6f)) };
        }
    }
}
