using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Objects of the bosses' trials: the Bone King's braziers and bone cage, the Blood Bishop's sigils and the
    /// Lich's phylactery. Same clip names as the monsters (idle, walk, windup, strike, hurt, death); for the sigil
    /// "walk" is its lit look.
    /// </summary>
    public static class GimmickArt
    {
        private static Vector2 P(float x, float y) => new(x, y);

        private static Clip Same(string name, Clip from)
        {
            var c = new Clip(name, from.Fps, from.Loop);
            c.Frames.AddRange(from.Frames);
            return c;
        }

        /// <summary>Bits flying apart for a death clip.</summary>
        private static Clip Shatter(IReadOnlyList<PixelPart> bits, Vector2 center, int frames = 4)
        {
            var death = new Clip("death", 12f, false);
            for (int i = 0; i < frames; i++)
            {
                var f = new Frame();
                float k = (i + 1f) / frames;
                for (int s = 0; s < 8; s++)
                {
                    float a = s * 45f + 20f;
                    var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                    var at = center + dir * (3f + 13f * k) - P(0f, 9f * k * k);
                    if (at.y < 11f) at.y = 11f;
                    f.Add(bits[s % bits.Count], at, a + 140f * k * (s % 2 == 0 ? 1f : -1f), 4);
                }
                death.Frames.Add(f);
            }
            return death;
        }

        // ------------------------------------------------------------------ Bone brazier (Bone King's ward)

        public static List<Clip> BoneBrazier()
        {
            var m = Palette.Standard();
            var pillar = new PixelPart("pillar", P(5.5f, 0f), m, 0,
                "..bbbbbbbb..",
                ".bBbbbbbbbb.",
                "..bkbbbbkb..",
                "...bbbbbb...",
                "...bkbbkb...",
                "...bbbbbb...",
                "....bbbb....",
                "....bkkb....",
                "....bbbb....",
                "...bbbbbb...",
                "....bbbb....",
                "....bkkb....",
                "....bbbb....",
                "...bbbbbb...",
                "..bbbbbbbb..",
                ".bbbbbbbbbb.",
                "bbbbbbbbbbbb");
            var bowl = new PixelPart("bowl", P(7.5f, 0f), m, 0,
                "bb..........bb",
                "bbbbbbbbbbbbbb",
                ".bbbbbbbbbbbb.",
                ".bkbbkbbkbbkb.",
                "..bbbbbbbbbb..");
            var skull = new PixelPart("skull", P(3.5f, 0f), m, 0,
                ".iiiiii.",
                "iiiiiiii",
                "ikkiikki",
                "ikeiieki",
                "iiiikiii",
                ".iiiiii.",
                "..i.i.i.");
            PixelPart Flame(int h)
            {
                var rows = new List<string>();
                for (int y = 0; y < h; y++)
                {
                    float t = y / (float)(h - 1); // 0 top .. 1 bottom
                    int half = Mathf.RoundToInt(Mathf.Lerp(0.5f, 5f, Mathf.Sqrt(t)));
                    int core = Mathf.Max(0, half - 2);
                    var row = new char[12];
                    for (int x = 0; x < 12; x++)
                    {
                        int d = Mathf.Abs(x * 2 - 11) / 2;
                        row[x] = d < core ? '+' : d < half ? '*' : '.';
                    }
                    rows.Add(new string(row));
                }
                return new PixelPart($"flame{h}", P(5.5f, 0f), m, 0, rows.ToArray());
            }
            var flames = new[] { Flame(13), Flame(11), Flame(14) };

            Frame Brazier(int flame, float tilt = 0f, float dx = 0f)
            {
                var f = new Frame();
                f.Add(pillar, P(32f + dx, 10f), tilt, 3);
                f.Add(bowl, P(32f + dx, 27f), tilt, 4);
                f.Add(skull, P(32f + dx, 15f), tilt, 5);
                f.Add(flames[flame], P(32f + dx, 31f), 0f, 6);
                return f;
            }

            var idle = new Clip("idle", 7f, true);
            for (int i = 0; i < 3; i++) idle.Frames.Add(Brazier(i));
            var hurt = new Clip("hurt", 14f, false);
            hurt.Frames.Add(Brazier(1, 6f, 1f));
            hurt.Frames.Add(Brazier(2, -4f, -1f));
            var bit = new PixelPart("bit", P(1.5f, 1f), m, 0, "bbbb", ".bb.");
            var shard = new PixelPart("soul", P(1f, 1f), m, 0, ".*.", "*+*", ".*.");
            var death = Shatter(new[] { bit, shard, bit, bit }, P(32f, 22f));
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }

        // ------------------------------------------------------------------ Bone cage (closes around a player)

        public static List<Clip> BoneCage()
        {
            var m = Palette.Standard();
            // Front bars only: the trapped player stays visible between them.
            var rib = new PixelPart("rib", P(1f, 0f), m, 0,
                ".b.", "bb.", "bb.", "bb.", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb",
                ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", ".bb", "bb.", "bb.", "bb.", "bbb");
            var crown = new PixelPart("crown", P(12.5f, 0f), m, 0,
                ".......iiiiiiiiiiii.......",
                ".....iiikiiiiiiiikiii.....",
                "...iiiibbbbbbbbbbbbiiii...",
                "..bbbb..............bbbb..",
                ".bb....................bb.");
            var ring = new PixelPart("ring", P(13.5f, 1f), m, 0,
                "..bbbbbbbbbbbbbbbbbbbbbbbb..",
                "bbbkbbbbkbbbbkbbbbkbbbbkbbbb",
                "..bbbbbbbbbbbbbbbbbbbbbbbb..");
            var skull = new PixelPart("skull", P(3.5f, 0f), m, 0,
                ".iiiiii.", "iiiiiiii", "ikkiikki", "ikeiieki", "iiiikiii", ".iiiiii.", "..i.i.i.");

            Frame Cage(float close, float shake = 0f)
            {
                var f = new Frame();
                f.Add(ring, P(32f + shake, 10f), 0f, 3);
                float[] xs = { 21f, 27f, 33f, 39f, 44f };
                for (int i = 0; i < xs.Length; i++)
                {
                    float lean = (xs[i] - 32f) * -0.9f * close;
                    f.Add(rib, P(xs[i] + shake, 11f), lean, 4);
                }
                f.Add(crown, P(32f + shake, 36f + 3f * (1f - close)), 0f, 5);
                f.Add(skull, P(32f + shake, 41f + 3f * (1f - close)), 0f, 6);
                return f;
            }

            var idle = new Clip("idle", 3f, true);
            idle.Frames.Add(Cage(1f));
            idle.Frames.Add(Cage(1f, 0.5f));
            var windup = new Clip("windup", 10f, false);
            windup.Frames.Add(Cage(0.3f));
            windup.Frames.Add(Cage(0.7f));
            windup.Frames.Add(Cage(1f));
            var hurt = new Clip("hurt", 16f, false);
            hurt.Frames.Add(Cage(1f, 1.5f));
            hurt.Frames.Add(Cage(1f, -1.5f));
            var bit = new PixelPart("bit", P(1f, 2f), m, 0, "bb", "bb", "bb", "bb", "b.");
            var death = Shatter(new[] { bit, bit, rib, bit }, P(32f, 24f));
            return new List<Clip> { idle, Same("walk", idle), windup, Same("strike", idle), hurt, death };
        }

        // ------------------------------------------------------------------ Blood sigil (a rune on the floor)

        public static List<Clip> BloodSigil()
        {
            // Unlit: pale chalk lines (readable on the red carpet and on stone); lit: glowing blood red.
            var dim = Palette.With(('d', new PixelMaterial(2, "#3d3434", "#5e5252", "#857777", "#a89a98", "#c9bcb8")));
            var lit = Palette.With(('d', new PixelMaterial(2, "#5a0c12", "#9a1620", "#d02a2e", "#ff5a48", "#ffc08a")));

            string[] Rune()
            {
                // An ellipse (seen at an angle) with a ring of glyph ticks and a star in the middle.
                const int w = 40, h = 22;
                var rows = new string[h];
                for (int y = 0; y < h; y++)
                {
                    var row = new char[w];
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x - (w - 1) * 0.5f) / (w * 0.5f), v = (y - (h - 1) * 0.5f) / (h * 0.5f);
                        float r = Mathf.Sqrt(u * u + v * v);
                        float a = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                        bool outer = r > 0.86f && r <= 1f;
                        bool inner = r > 0.6f && r <= 0.68f;
                        bool tick = r > 0.68f && r <= 0.86f && Mathf.Repeat(a + 8f, 36f) < 7f;
                        // Five-pointed star: points at r 0.6, every 72 degrees.
                        float sa = Mathf.Repeat(a + 90f, 72f) - 36f;
                        bool star = r <= 0.58f && Mathf.Abs(sa) * Mathf.Deg2Rad * r < 0.07f + 0.12f * (0.58f - r);
                        bool hub = r < 0.12f;
                        row[x] = outer || inner || tick || star || hub ? 'd' : '.';
                    }
                    rows[y] = new string(row);
                }
                return rows;
            }

            var rune = Rune();
            var dimPart = new PixelPart("rune", P(19.5f, 10.5f), dim, 0, rune);
            var litPart = new PixelPart("rune_lit", P(19.5f, 10.5f), lit, 0, rune);
            var litBright = new PixelPart("rune_lit2", P(19.5f, 10.5f), lit, 1, rune);

            Frame One(PixelPart part) => new Frame().Add(part, P(32f, 21f), 0f, 3);
            var idle = new Clip("idle", 2f, true);
            idle.Frames.Add(One(dimPart));
            var on = new Clip("walk", 6f, true);
            on.Frames.Add(One(litPart));
            on.Frames.Add(One(litBright));
            var hurt = new Clip("hurt", 10f, false);
            hurt.Frames.Add(One(dimPart));
            var death = new Clip("death", 10f, false);
            death.Frames.Add(One(litBright));
            death.Frames.Add(One(litPart));
            return new List<Clip> { idle, on, Same("windup", idle), Same("strike", on), hurt, death };
        }

        // ------------------------------------------------------------------ Soul vessel (the Lich's phylactery)

        public static List<Clip> SoulVessel()
        {
            var m = Palette.Standard();
            var box = new PixelPart("box", P(7.5f, 0f), m, 0,
                "....gggggggg....",
                "...gGggggggGg...",
                "..gggvvvvvvggg..",
                "..gvvvv**vvvvg..",
                "..gvvv*++*vvvg..",
                "..gvv*++++*vvg..",
                "..gvv*++++*vvg..",
                "..gvvv*++*vvvg..",
                "..gvvvv**vvvvg..",
                "..gggvvvvvvggg..",
                "...gggggggggg...",
                "....gkgggggkg...",
                "...gg......gg...");
            var spike = new PixelPart("spike", P(1f, 0f), m, 0, ".g.", ".g.", "ggg", ".g.");
            var mote = new PixelPart("mote", P(0.5f, 0.5f), m, 0, "*+", "+*");

            Frame Vessel(float bob, float spin, float shake = 0f)
            {
                var f = new Frame();
                float y = 18f + bob;
                f.Add(box, P(32f + shake, y), 0f, 4);
                f.Add(spike, P(32f + shake, y + 13f), 0f, 5);
                for (int i = 0; i < 3; i++)
                {
                    float a = (spin + i * 120f) * Mathf.Deg2Rad;
                    f.Add(mote, P(32f + Mathf.Cos(a) * 12f, y + 6f + Mathf.Sin(a) * 4f), 0f, Mathf.Sin(a) > 0f ? 3 : 6);
                }
                return f;
            }

            var idle = new Clip("idle", 8f, true);
            for (int i = 0; i < 6; i++) idle.Frames.Add(Vessel(Mathf.Sin(i / 6f * Mathf.PI * 2f) * 1.5f, i * 60f));
            var hurt = new Clip("hurt", 16f, false);
            hurt.Frames.Add(Vessel(0f, 0f, 1.5f));
            hurt.Frames.Add(Vessel(0f, 30f, -1.5f));
            var bit = new PixelPart("bit", P(1f, 1f), m, 0, "gg", "gv");
            var death = Shatter(new[] { bit, mote, bit, mote }, P(32f, 24f), 5);
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }
    }
}
