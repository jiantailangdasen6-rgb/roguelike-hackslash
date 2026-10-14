using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>Floor features of the fungal grotto (moss with glowing caps, threads of mycelium) and the cursed library (ink, fallen books).</summary>
    public static partial class DecalArt
    {
        private static PixelCanvas MossPatch()
        {
            var c = new PixelCanvas(76, 46);
            var g = new[] { C("#12200c"), C("#1a2e11"), C("#233f17"), C("#2e521e") };
            var rng = new GameRandom(711);
            for (int y = 0; y < 46; y++)
            for (int x = 0; x < 76; x++)
            {
                float d = Blob(x + 0.5f, y + 0.5f, 38f, 23f, 34f, 19f, 5, 0.35f);
                if (d > 1f || Hash(x, y) > 1.2f - d) continue;
                c.Set(x, y, d > 0.75f ? g[0] : d > 0.45f ? g[1] : Hash(x / 3, y / 2) > 0.6f ? g[3] : g[2]);
            }
            for (int i = 0; i < 9; i++)
            {
                int x = rng.Range(14, 62), y = rng.Range(10, 36);
                c.Set(x, y, C("#4a6a2a"));
                c.Set(x, y + 1, C("#9cff6a"));
                c.Set(x + 1, y + 1, C("#d8ffb0"));
                c.Set(x - 1, y + 1, C("#6ac83c"));
            }
            return c;
        }

        private static PixelCanvas Mycelium()
        {
            var c = new PixelCanvas(80, 50);
            var rng = new GameRandom(733);
            for (int t = 0; t < 9; t++)
            {
                float x = 40f + rng.Range(-6f, 6f), y = 25f + rng.Range(-4f, 4f), a = t * 0.7f + rng.Range(-0.3f, 0.3f);
                for (int k = 0; k < 34; k++)
                {
                    c.Set((int)x, (int)y, k % 6 == 0 ? C("#c8d0a8") : C("#7a8462"));
                    a += rng.Range(-0.45f, 0.45f);
                    x += Mathf.Cos(a);
                    y += Mathf.Sin(a) * 0.6f;
                    if (x < 1 || x > 78 || y < 1 || y > 48) break;
                }
            }
            c.Disc(40f, 25f, 3f, C("#4a503a"));
            c.Set(40, 26, C("#9cff6a"));
            return c;
        }

        private static PixelCanvas InkStain()
        {
            var c = new PixelCanvas(84, 52);
            for (int y = 0; y < 52; y++)
            for (int x = 0; x < 84; x++)
            {
                float d = Blob(x + 0.5f, y + 0.5f, 42f, 26f, 30f, 17f, 9, 0.3f);
                bool drop = (new Vector2(x - 12f, y - 10f)).sqrMagnitude < 9f || (new Vector2(x - 74f, y - 40f)).sqrMagnitude < 6f;
                if (d > 1f && !drop) continue;
                c.Set(x, y, d < 0.55f ? C("#07061a") : C("#110e26"));
                if (d < 0.5f && Hash(x / 2, y) > 0.985f) c.Set(x, y, C("#5a4aa0"));
            }
            return c;
        }

        private static PixelCanvas FallenBooks()
        {
            var c = new PixelCanvas(56, 34);
            var rng = new GameRandom(919);
            var colors = new[] { C("#4a1418"), C("#1e3a2a"), C("#1c2a4a"), C("#5a4a22"), C("#3a2a4e") };
            for (int b = 0; b < 5; b++)
            {
                int x0 = 6 + b * 9 + rng.Range(-2, 3), y0 = 6 + rng.Range(0, 14), w = 10, h = 7;
                var col = colors[b % colors.Length];
                bool open = b == 2;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int px = x0 + x + (b % 2 == 0 ? y / 3 : -y / 3), py = y0 + y;
                    var shade = open ? (x == w / 2 ? C("#8a8270") : y % 2 == 1 && x % 5 > 0 ? C("#7a7262") : C("#d0c6aa"))
                        : y == h - 1 ? Color32.Lerp(col, Color.white, 0.2f) : y == 0 ? Color32.Lerp(col, Color.black, 0.35f) : col;
                    if (!open && (x == 1 || x == w - 2)) shade = Color32.Lerp(shade, C("#c8a24a"), 0.4f);
                    c.Set(px, py, shade);
                }
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }
    }
}
