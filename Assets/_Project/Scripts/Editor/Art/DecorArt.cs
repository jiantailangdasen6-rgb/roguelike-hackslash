using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Dressing for the special rooms of the area maps, placed at run time by ArenaView: heaps of gold for the
    /// treasure room, cobwebs and bone piles for the secret room, hanging chains for the champion's hall and a rune
    /// circle under the puzzles. 32 px = 1 tile.
    /// </summary>
    public static class DecorArt
    {
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static Color32 A(Color32 c, byte a) => new(c.r, c.g, c.b, a);
        private static readonly Color32 Outline = PixelRenderer.Outline;

        private static readonly Color32[] Gold = { C("#2e1f0a"), C("#573d12"), C("#87641c"), C("#bb912f"), C("#e6c766"), C("#fff1b0") };
        private static readonly Color32[] Bone = { C("#28231d"), C("#524b3c"), C("#857c6a"), C("#b8ae95"), C("#e2dac1") };
        private static readonly Color32[] Iron = { C("#141414"), C("#262626"), C("#3b3b3d"), C("#57585c"), C("#7c7e84") };

        public static Dictionary<string, PropArt.Sheet> Sheets() => new()
        {
            ["decor_gold"] = One(GoldHeap(), new Vector2(0.5f, 0.1f)),
            ["decor_cobweb"] = One(Cobweb(), new Vector2(0f, 1f)),
            ["decor_bones"] = One(BonePile(), new Vector2(0.5f, 0.15f)),
            ["decor_chains"] = One(Chains(), new Vector2(0.5f, 1f)),
            ["decor_rune"] = One(RuneCircle(), new Vector2(0.5f, 0.5f)),
            ["decor_bell"] = One(Bell(), new Vector2(0.5f, 0.04f)),
        };

        private static PropArt.Sheet One(PixelCanvas c, Vector2 pivot) => new() { Frames = new List<PixelCanvas> { c }, Pivot = pivot };

        private static float Hash(int x, int y) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);

        private static void AddOutline(PixelCanvas c)
        {
            var copy = (Color32[])c.Pixels.Clone();
            for (int y = 0; y < c.Height; y++)
            for (int x = 0; x < c.Width; x++)
            {
                if (copy[y * c.Width + x].a != 0) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= c.Width || ny >= c.Height || copy[ny * c.Width + nx].a < 200) continue;
                    c.Set(x, y, Outline);
                    break;
                }
            }
        }

        /// <summary>A heap of coins with a few gems and a goblet on top.</summary>
        private static PixelCanvas GoldHeap()
        {
            var c = new PixelCanvas(56, 30);
            for (int y = 0; y < 22; y++)
            for (int x = 2; x < 54; x++)
            {
                float u = (x - 28f) / 26f, v = y / 22f;
                if (v > 1f - u * u) continue; // a mound
                // Coins: round blobs, lit from the top-left.
                int cx = x / 4, cy = (y + (cx % 2) * 2) / 3;
                float h = Hash(cx, cy);
                int shade = 2 + (int)(h * 3f) + (y > 14 ? 1 : 0) - (x > 40 ? 1 : 0);
                if ((x % 4 == 0 || (y + (cx % 2) * 2) % 3 == 0) && h < 0.6f) shade -= 2;
                c.Set(x, y, Gold[Mathf.Clamp(shade, 0, Gold.Length - 1)]);
            }
            // Gems.
            foreach (var (x, y, col) in new[] { (18, 12, C("#ff5a4a")), (33, 16, C("#5ad1ff")), (25, 6, C("#7dff9a")), (40, 8, C("#ff5a4a")) })
            {
                c.Set(x, y, col); c.Set(x + 1, y, col); c.Set(x, y + 1, Color.white);
            }
            // A goblet on top.
            for (int y = 20; y < 27; y++) c.Set(28, y, Gold[3]);
            for (int x = 25; x <= 31; x++) { c.Set(x, 26, Gold[4]); c.Set(x, 27, Gold[3]); }
            for (int x = 26; x <= 30; x++) c.Set(x, 28, Gold[2]);
            AddOutline(c);
            return c;
        }

        /// <summary>A web hanging in a top-left corner (flip it for the other corners).</summary>
        private static PixelCanvas Cobweb()
        {
            var c = new PixelCanvas(48, 48);
            var silk = A(C("#d8d4e0"), 150);
            var faint = A(C("#d8d4e0"), 80);
            Vector2 corner = new(0f, 47f);
            // Spokes.
            foreach (float a in new[] { -10f, -30f, -50f, -70f, -88f })
            {
                Vector2 d = new(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                for (int t = 0; t < 46; t++) c.Set(Mathf.RoundToInt(corner.x + d.x * t), Mathf.RoundToInt(corner.y + d.y * t), silk);
            }
            // Sagging rings.
            foreach (float r in new[] { 10f, 19f, 28f, 37f })
                for (float a = -88f; a <= -10f; a += 0.5f)
                {
                    float sag = Mathf.Sin(Mathf.Repeat(a, 20f) / 20f * Mathf.PI) * r * 0.08f;
                    Vector2 p = corner + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * (r - sag);
                    c.Set(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), r > 30f ? faint : silk);
                }
            return c;
        }

        /// <summary>Old bones and a skull.</summary>
        private static PixelCanvas BonePile()
        {
            var c = new PixelCanvas(40, 22);
            void Stick(int x0, int y0, int x1, int y1)
            {
                int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
                for (int i = 0; i <= n; i++)
                {
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, i / (float)n)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, i / (float)n));
                    c.Set(x, y, Bone[3]); c.Set(x, y - 1, Bone[2]);
                }
                foreach (var (ex, ey) in new[] { (x0, y0), (x1, y1) })
                {
                    c.Set(ex - 1, ey, Bone[4]); c.Set(ex + 1, ey, Bone[3]); c.Set(ex, ey + 1, Bone[4]); c.Set(ex, ey - 2, Bone[2]);
                }
            }
            Stick(4, 4, 22, 9);
            Stick(10, 12, 30, 3);
            Stick(20, 6, 36, 10);
            // Skull.
            for (int y = 8; y < 18; y++)
            for (int x = 14; x < 25; x++)
            {
                float u = (x - 19.5f) / 5.5f, v = (y - 13f) / 5f;
                if (u * u + v * v > 1f) continue;
                c.Set(x, y, y > 14 ? Bone[4] : Bone[3]);
            }
            foreach (int x in new[] { 16, 17, 21, 22 }) { c.Set(x, 12, C("#100c11")); c.Set(x, 13, C("#100c11")); }
            c.Set(19, 10, C("#100c11"));
            AddOutline(c);
            return c;
        }

        /// <summary>Chains hanging from the top of the back wall, ending in a shackle.</summary>
        private static PixelCanvas Chains()
        {
            var c = new PixelCanvas(18, 70);
            for (int link = 0; link < 10; link++)
            {
                int y = 69 - link * 6;
                bool side = link % 2 == 1;
                for (int k = 0; k < 6; k++)
                {
                    if (side) { c.Set(8, y - k, Iron[3]); c.Set(9, y - k, Iron[2]); }
                    else if (k == 0 || k == 5) { for (int x = 6; x <= 11; x++) c.Set(x, y - k, Iron[k == 0 ? 4 : 2]); }
                    else { c.Set(6, y - k, Iron[3]); c.Set(11, y - k, Iron[1]); }
                }
            }
            // Shackle.
            for (int x = 3; x <= 14; x++) { c.Set(x, 8, Iron[3]); c.Set(x, 2, Iron[1]); }
            for (int y = 2; y <= 8; y++) { c.Set(3, y, Iron[3]); c.Set(14, y, Iron[1]); }
            AddOutline(c);
            return c;
        }

        private static readonly Color32[] Wood = { C("#21150d"), C("#3d2716"), C("#5e3d21"), C("#80562f"), C("#a87542") };
        private static readonly Color32[] Bronze = { C("#2a1a0c"), C("#5a3a17"), C("#8a5d24"), C("#b8862f"), C("#dcb04e"), C("#fff0b0") };

        /// <summary>The gathering bell by the campfire: a bronze bell under a wooden beam on two posts.</summary>
        private static PixelCanvas Bell()
        {
            var c = new PixelCanvas(44, 56);
            // Posts and beam.
            for (int y = 1; y < 50; y++)
            {
                c.Set(4, y, Wood[3]); c.Set(5, y, Wood[2]); c.Set(6, y, Wood[1]);
                c.Set(37, y, Wood[3]); c.Set(38, y, Wood[2]); c.Set(39, y, Wood[1]);
            }
            for (int x = 2; x <= 41; x++) { c.Set(x, 52, Wood[4]); c.Set(x, 51, Wood[3]); c.Set(x, 50, Wood[2]); c.Set(x, 49, Wood[1]); }
            for (int x = 1; x <= 9; x++) { c.Set(x, 1, Wood[2]); c.Set(x, 2, Wood[3]); }
            for (int x = 34; x <= 42; x++) { c.Set(x, 1, Wood[2]); c.Set(x, 2, Wood[3]); }
            // Rope.
            for (int y = 43; y < 49; y++) { c.Set(21, y, (y % 2 == 0) ? Wood[4] : Wood[3]); c.Set(22, y, Wood[2]); }
            // The bell: a flared shape, lit from the top-left.
            const float cx = 21.5f;
            for (int y = 16; y <= 42; y++)
            {
                float v = (42f - y) / 26f; // 0 crown .. 1 lip
                float half = 4.5f + 6.5f * Mathf.Pow(v, 1.6f) + (y <= 19 ? 1.8f : 0f);
                for (int x = 0; x < 44; x++)
                {
                    float dx = x - cx;
                    if (Mathf.Abs(dx) > half) continue;
                    float lit = -dx / half * 0.9f + (1f - v) * 0.6f;
                    int shade = Mathf.Clamp(Mathf.RoundToInt(2.4f + lit * 2f), 1, 5);
                    if (y == 18 || y == 19) shade = Mathf.Max(1, shade - 1); // the lip band
                    if (y == 27) shade = Mathf.Max(1, shade - 1); // a band round the waist
                    c.Set(x, y, Bronze[shade]);
                }
            }
            // The clapper under the lip.
            for (int y = 12; y <= 16; y++) { c.Set(21, y, Bronze[1]); c.Set(22, y, Bronze[2]); }
            c.Set(21, 11, Bronze[2]); c.Set(22, 11, Bronze[3]);
            AddOutline(c);
            return c;
        }

        /// <summary>A faint rune circle on the floor (seen at an angle).</summary>
        private static PixelCanvas RuneCircle()
        {
            const int w = 160, h = 90;
            var c = new PixelCanvas(w, h);
            var line = A(C("#8fe8ff"), 120);
            var dim = A(C("#8fe8ff"), 60);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f - w / 2f) / (w / 2f), v = (y + 0.5f - h / 2f) / (h / 2f);
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                bool outer = r > 0.95f && r <= 1f;
                bool inner = r > 0.78f && r <= 0.81f;
                bool glyph = r > 0.83f && r < 0.93f && Mathf.Repeat(a + 4f, 24f) < 5f;
                bool hex = r < 0.76f && Mathf.Abs(Mathf.Repeat(a + 30f, 60f) - 30f) < 30f * (1f - r / 0.76f) * 0.06f + 0.5f && r > 0.7f;
                if (outer || inner) c.Set(x, y, line);
                else if (glyph || hex) c.Set(x, y, dim);
            }
            return c;
        }
    }
}
