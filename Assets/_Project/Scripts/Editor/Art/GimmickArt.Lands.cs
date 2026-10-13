using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Objects of the new lords' trials (the saint's flood seals, the colossus' water cisterns) and the new lands'
    /// floor traps (icicles overhead, lava cracks), as sheets for EnemyView and ArenaView.
    /// </summary>
    public static partial class GimmickArt
    {
        private static Color32 Hx(string hex) => PixelMaterial.Hex(hex);

        /// <summary>A stone plug sealed with chains and a glowing rune; water seeps round it.</summary>
        public static List<Clip> FloodSeal()
        {
            var m = Palette.With(
                ('a', new PixelMaterial(2, "#0e1816", "#1c2e2a", "#2e4642", "#46625c", "#64847c")),
                ('*', new PixelMaterial("#7ff0d8")),
                ('+', new PixelMaterial("#e0fff8")));
            var plug = new PixelPart("plug", P(8f, 0f), m, 0,
                ".....aaaaaa.....",
                "...aaaaaaaaaa...",
                "..aaaaa##aaaaa..",
                ".aaaa######aaaa.",
                ".aaa##*++*##aaa.",
                "aaaa#*+**+*#aaaa",
                "aaaa#*+**+*#aaaa",
                ".aaa##*++*##aaa.",
                ".aaaa######aaaa.",
                "..aaaaa##aaaaa..",
                "...aaaaaaaaaa...",
                "....#..aa..#....",
                "...##......##...");
            var drop = new PixelPart("drop", P(0.5f, 0.5f), m, 0, "*", "+");

            Frame Seal(float pulse, float shake = 0f)
            {
                var f = new Frame();
                f.Add(plug, P(32f + shake, 14f), 0f, 4);
                for (int i = 0; i < 3; i++)
                    f.Add(drop, P(25f + i * 7f, 12f - (pulse + i) % 3f), 0f, 5);
                return f;
            }

            var idle = new Clip("idle", 6f, true);
            for (int i = 0; i < 6; i++) idle.Frames.Add(Seal(i * 0.5f));
            var hurt = new Clip("hurt", 16f, false);
            hurt.Frames.Add(Seal(0f, 1.5f));
            hurt.Frames.Add(Seal(1f, -1.5f));
            var bit = new PixelPart("bit", P(1f, 1f), m, 0, "aa", "a#");
            var death = Shatter(new[] { bit, drop, bit, drop }, P(32f, 20f), 5);
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }

        /// <summary>A great stone cistern brimming with water, banded in iron.</summary>
        public static List<Clip> QuenchCistern()
        {
            var m = Palette.With(
                ('a', new PixelMaterial(2, "#1a1612", "#2e2820", "#463c30", "#605242", "#7c6a56")),
                ('w', new PixelMaterial(2, "#0c2a36", "#14425a", "#1e5e7e", "#2e7ea2", "#5aa8c8")));
            var tub = new PixelPart("tub", P(9f, 0f), m, 0,
                "..wwwwwwwwwwwwww..",
                ".aWwwwwwwwwwwwwwa.",
                "aaaaaaaaaaaaaaaaaa",
                "a#################",
                "aaaaaaaaaaaaaaaaaa",
                "aaaaaaaaaaaaaaaaaa",
                "aaaaaaaaaaaaaaaaaa",
                "a#################",
                "aaaaaaaaaaaaaaaaaa",
                ".aaaaaaaaaaaaaaaa.",
                "..aa..........aa..");
            var bit = new PixelPart("bit", P(1f, 1f), m, 0, "aa", "aw");
            var splash = new PixelPart("splash", P(1f, 1f), m, 0, "ww", "wW");

            Frame Tub(float shake)
            {
                var f = new Frame();
                f.Add(tub, P(32f + shake, 10f), 0f, 4);
                return f;
            }

            var idle = new Clip("idle", 4f, true);
            idle.Frames.Add(Tub(0f));
            idle.Frames.Add(Tub(0f));
            var hurt = new Clip("hurt", 16f, false);
            hurt.Frames.Add(Tub(1.5f));
            hurt.Frames.Add(Tub(-1.5f));
            var death = Shatter(new[] { bit, splash, bit, splash, splash }, P(32f, 16f), 5);
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }

        // ------------------------------------------------------------------ Floor traps (ArenaView frames)

        /// <summary>Where icicles hang overhead: 0 a ring of frost, 1 cracks spreading (warning), 2 shattered ice.</summary>
        public static List<PixelCanvas> Icicle()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 3; f++)
            {
                var c = new PixelCanvas(80, 80);
                float cx = 40f, cy = 40f, r = 36f;
                for (int y = 0; y < 80; y++)
                for (int x = 0; x < 80; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d > r) continue;
                    // Frost thickens toward the rim (a faint ring), plus the cracks of the warning.
                    float rim = Mathf.Clamp01((d - r * 0.7f) / (r * 0.3f));
                    float h = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);
                    if (h < 0.75f - rim * 0.5f) continue;
                    c.Set(x, y, new Color32(170, 205, 240, (byte)(40 + 90 * rim)));
                }
                if (f >= 1)
                {
                    // Cracks: lines from the middle.
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * 51.4f * Mathf.Deg2Rad;
                        float len = f == 1 ? 18f : 30f;
                        for (float t = 3f; t < len; t += 0.7f)
                            c.Set((int)(cx + Mathf.Cos(a + t * 0.02f) * t), (int)(cy + Mathf.Sin(a + t * 0.02f) * t), Hx("#d8ecff"));
                    }
                }
                if (f == 2)
                    for (int k = 0; k < 14; k++)
                    {
                        float a = k * 25.7f * Mathf.Deg2Rad, t = 8f + (k * 7) % 20;
                        int x = (int)(cx + Mathf.Cos(a) * t), y = (int)(cy + Mathf.Sin(a) * t);
                        c.Rect(x, y, 2, 3, Hx("#a8ccec"));
                        c.Set(x, y + 2, Hx("#f0faff"));
                    }
                frames.Add(c);
            }
            return frames;
        }

        /// <summary>A crack in the floor (6.5 units long, drawn from its left end): 0 dull, 1 glowing (warning), 2-3 erupting.</summary>
        public static List<PixelCanvas> LavaCrack()
        {
            var frames = new List<PixelCanvas>();
            const int w = 208, h = 40;
            var rng = new Game.Core.GameRandom(77);
            var path = new float[w];
            float y0 = h * 0.5f;
            for (int x = 0; x < w; x++)
            {
                y0 += rng.Range(-0.6f, 0.6f);
                y0 = Mathf.Clamp(y0, h * 0.5f - 6f, h * 0.5f + 6f);
                path[x] = y0;
            }
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(w, h);
                for (int x = 2; x < w - 2; x++)
                {
                    float taper = Mathf.Min(1f, Mathf.Min(x, w - x) / 20f);
                    float half = (f >= 2 ? 3.2f : 2f) * taper + 0.5f;
                    for (int y = (int)(path[x] - half - 1); y <= path[x] + half + 1; y++)
                    {
                        float k = Mathf.Abs(y - path[x]);
                        Color32 col;
                        if (k > half) col = Hx("#0a0605");                         // the crack's lip
                        else if (f == 0) col = k < half * 0.4f ? Hx("#5a1e08") : Hx("#24100a");
                        else if (f == 1) col = k < half * 0.5f ? Hx("#ff8a2a") : Hx("#a33a0c");
                        else col = k < half * 0.5f ? ((x + f * 3) % 7 < 3 ? Hx("#fff0b0") : Hx("#ffb347")) : Hx("#ff6a1a");
                        c.Set(x, y, col);
                    }
                }
                frames.Add(c);
            }
            return frames;
        }
    }
}
