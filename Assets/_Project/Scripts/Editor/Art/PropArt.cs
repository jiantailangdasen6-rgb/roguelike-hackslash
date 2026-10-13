using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Room props and gimmicks (pillars, statues, tombs, altars, pews, crates, crystals, healing fonts,
    /// spike traps, flame vents) and the per-area floor/wall tiles and wall decorations.
    /// Area 0 = crypt (grey stone), 1 = chapel (marble, crimson and gold), 2 = abyss (obsidian, violet glow).
    /// </summary>
    public static class PropArt
    {
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static Color32 A(Color32 c, byte a) => new(c.r, c.g, c.b, a);
        private static readonly Color32 Outline = PixelRenderer.Outline;
        private static readonly Color32 Clear = default;

        // Stone ramps (dark → light) per area.
        private static readonly Color32[][] StoneRamp =
        {
            new[] { C("#18151b"), C("#2a2631"), C("#3a3542"), C("#4c4656"), C("#5f586a"), C("#77708a") },
            new[] { C("#2a2023"), C("#4a3b3f"), C("#6e5d60"), C("#93807f"), C("#b9a6a1"), C("#ddd0c8") },
            new[] { C("#06050a"), C("#100d18"), C("#1b1728"), C("#292339"), C("#3a3250"), C("#4e4569") },
        };
        private static readonly Color32[] Gold = { C("#2e1f0a"), C("#573d12"), C("#87641c"), C("#bb912f"), C("#e6c766") };
        private static readonly Color32[] Crimson = { C("#25060a"), C("#4a0c13"), C("#76121d"), C("#a31d28"), C("#cf3a3a") };
        private static readonly Color32[] Wood = { C("#1d140c"), C("#372616"), C("#563b21"), C("#78552e"), C("#9a7340") };
        private static readonly Color32[] Iron = { C("#141414"), C("#262626"), C("#3b3b3d"), C("#57585c"), C("#7c7e84") };
        private static readonly Color32[] Glow =
        {
            C("#ff9a3c"), // crypt: candle orange
            C("#ff4a3a"), // chapel: blood red
            C("#c58bff"), // abyss: violet soulfire
        };

        public struct Sheet
        {
            public List<PixelCanvas> Frames;
            public Vector2 Pivot;
        }

        private static Sheet One(PixelCanvas c, Vector2 pivot) => new() { Frames = new List<PixelCanvas> { c }, Pivot = pivot };

        /// <summary>All prop and gimmick sheets by name (Resources/Sheets/&lt;name&gt;).</summary>
        public static Dictionary<string, Sheet> Sheets()
        {
            var bottom = new Vector2(0.5f, 0.05f);
            var sheets = new Dictionary<string, Sheet>();
            for (int area = 0; area < 3; area++)
            {
                sheets[$"prop_pillar_{area}"] = One(Pillar(area), new Vector2(0.5f, 0.06f));
                sheets[$"prop_statue_{area}"] = One(Statue(area), new Vector2(0.5f, 0.06f));
                sheets[$"prop_altar_{area}"] = One(Altar(area), new Vector2(0.5f, 0.12f));
            }
            sheets["prop_sarcophagus"] = One(Sarcophagus(), new Vector2(0.5f, 0.14f));
            sheets["prop_pew"] = One(Pew(), new Vector2(0.5f, 0.12f));
            sheets["prop_crates"] = One(Crates(), new Vector2(0.5f, 0.1f));
            sheets["prop_crystal"] = One(Crystal(), new Vector2(0.5f, 0.06f));
            sheets["prop_font"] = new Sheet { Frames = Font(), Pivot = new Vector2(0.5f, 0.2f) };
            sheets["prop_lantern"] = new Sheet { Frames = Lantern(), Pivot = new Vector2(0.5f, 0.08f) };
            sheets["trap_spikes"] = new Sheet { Frames = Spikes(), Pivot = new Vector2(0.5f, 0.5f) };
            sheets["trap_vent"] = new Sheet { Frames = Vent(), Pivot = new Vector2(0.5f, 0.1f) };
            return sheets;
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>Cylinder shading: index into a ramp by horizontal position (light from the left).</summary>
        private static int CylShade(int x, int x0, int x1, int baseShade)
        {
            float t = (x - x0 + 0.5f) / (x1 - x0);
            if (t < 0.18f) return baseShade + 1;
            if (t < 0.32f) return baseShade + 2;
            if (t < 0.62f) return baseShade + 1;
            if (t < 0.85f) return baseShade;
            return baseShade - 1;
        }

        private static void HLine(PixelCanvas c, int x0, int x1, int y, Color32 col)
        {
            for (int x = x0; x <= x1; x++) c.Set(x, y, col);
        }

        private static void Box(PixelCanvas c, int x0, int y0, int w, int h, Color32[] ramp, int baseShade)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                int s = baseShade;
                if (y == y0 + h - 1) s = baseShade + 1;
                if (x == x0) s = Mathf.Max(s, baseShade + 1);
                if (x == x0 + w - 1 || y == y0) s = baseShade - 1;
                c.Set(x, y, ramp[Mathf.Clamp(s, 0, ramp.Length - 1)]);
            }
        }

        // ------------------------------------------------------------------ Pillars

        private static PixelCanvas Pillar(int area)
        {
            var s = StoneRamp[area];
            var c = new PixelCanvas(32, 64);
            var rng = new GameRandom((ulong)(100 + area));
            const int x0 = 9, x1 = 23;
            int top = area == 0 ? 50 : 53;
            for (int y = 5; y < top; y++)
            for (int x = x0; x < x1; x++)
            {
                int shade = CylShade(x, x0, x1, 2);
                if (area != 2 && (x - x0) % 4 == 3 && x < x1 - 1) shade -= 1; // flutes
                if (area == 0 && (y - 5) % 12 == 0) shade = 0;               // block joints
                c.Set(x, y, s[Mathf.Clamp(shade, 0, s.Length - 1)]);
            }
            // Base and plinth
            for (int y = 0; y < 5; y++)
                HLine(c, 6 - (y < 2 ? 1 : 0), 25 + (y < 2 ? 1 : 0), y, s[y == 4 ? 4 : y == 0 ? 1 : 2]);
            switch (area)
            {
                case 0:
                    // Broken, jagged top with a fallen chunk; moss and cracks.
                    for (int x = x0; x < x1; x++) c.Rect(x, top, 1, 2 + (x * 7) % 5, s[CylShade(x, x0, x1, 2)]);
                    for (int i = 0; i < 10; i++) c.Set(rng.Range(x0, x1), rng.Range(6, 20), rng.Chance(0.5f) ? C("#1f2a1d") : C("#2a3a26"));
                    { int x = 14, y = 34; for (int i = 0; i < 8; i++) { c.Set(x, y, s[0]); x += rng.Range(0, 2); y -= 1; } }
                    break;
                case 1:
                    // Marble with gold rings and a carved capital.
                    for (int y = top; y < top + 5; y++) HLine(c, 6 + (y == top ? 1 : 0), 25 - (y == top ? 1 : 0), y, y == top + 4 ? s[5] : s[3]);
                    HLine(c, 6, 25, top + 2, Gold[3]);
                    foreach (int ry in new[] { 18, 36 })
                    {
                        HLine(c, x0, x1 - 1, ry, Gold[2]);
                        HLine(c, x0, x1 - 1, ry + 1, Gold[4]);
                    }
                    HLine(c, 6, 25, 4, Crimson[2]);
                    break;
                default:
                    // Obsidian with glowing violet runes and a spiked crown.
                    for (int x = x0 - 1; x <= x1; x += 3) c.Rect(x, top, 2, 3 + (x % 2) * 3, s[3]);
                    for (int y = 9; y < top - 4; y += 6)
                    {
                        c.Set(15, y, Glow[2]); c.Set(16, y + 1, Glow[2]); c.Set(15, y + 2, Glow[2]);
                        c.Set(17, y + 2, A(Glow[2], 160));
                    }
                    for (int i = 0; i < 6; i++) c.Set(rng.Range(x0, x1), rng.Range(6, top), A(Glow[2], 120));
                    break;
            }
            c.Outline(Outline);
            return c;
        }

        // ------------------------------------------------------------------ Statues (ASCII, auto-shaded)

        private static Dictionary<char, PixelMaterial> EnvMaterials(int area)
        {
            var s = StoneRamp[area];
            var m = Palette.Standard();
            m['a'] = new PixelMaterial(2, Hex(s[1]), Hex(s[2]), Hex(s[3]), Hex(s[4]), Hex(s[5]));
            m['*'] = new PixelMaterial(Hex(Glow[area]));
            return m;
        }

        private static string Hex(Color32 c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Renders one ASCII part onto a canvas with a 1-pixel outline.</summary>
        private static PixelCanvas FromPart(PixelPart part, int width, int height, int offsetX = -1, int offsetY = 1)
        {
            var c = new PixelCanvas(width, height);
            int ox = offsetX >= 0 ? offsetX : (width - part.Width) / 2;
            for (int y = 0; y < part.Height; y++)
            for (int x = 0; x < part.Width; x++)
            {
                var col = part.Sample(x, y);
                if (col.a != 0) c.Set(ox + x, offsetY + y, col);
            }
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Statue(int area)
        {
            var m = EnvMaterials(area);
            PixelPart part = area switch
            {
                0 => new PixelPart("mourner", Vector2.zero, m, 0,
                    "......aaaa......",
                    ".....aaaaaa.....",
                    "....aaaaaaaa....",
                    "....aaakkkaa....",
                    "....aakkkkka....",
                    "....aaakkkaa....",
                    "...aaaaaaaaaa...",
                    "..aaaaaaaaaaaa..",
                    "..aaaaaaaaaaaa..",
                    ".aaaaaAaaaaaaaa.",
                    ".aaaaaAAaaaaaaa.",
                    ".aaaaaaAAaaaaaa.",
                    ".aaaaaaaaaaaaaa.",
                    ".aaaaaaaaaaaaaa.",
                    ".aaaaaaaAaaaaaa.",
                    ".aaaaaaaaaaaaaa.",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaAaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaAaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    ".aaaaaaaaaaaaaa.",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "kkkkkkkkkkkkkkkk",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaa"),
                1 => new PixelPart("angel", Vector2.zero, m, 0,
                    "aaa.......gggg.......aaa",
                    ".aaa.....gaaaag.....aaa.",
                    ".aaaa.....aaaa.....aaaa.",
                    "..aaaa....aaaa....aaaa..",
                    "..aaaaa...aaaa...aaaaa..",
                    "...aaaaa.aaaaaa.aaaaa...",
                    "...aaaaaaaaaaaaaaaaaa...",
                    "....aaaaaaaaaaaaaaaa....",
                    "....aaaaaaaaaaaaaaaa....",
                    ".....aaaaaaaaaaaaaa.....",
                    ".....aaaaaaAaaaaaaa.....",
                    "......aaaaaAaaaaaa......",
                    "......aaaaaaAaaaaa......",
                    "......aaaaaaaaaaaa......",
                    "......aaaaaaaaaaaa......",
                    ".....aaaaaaaAaaaaaa.....",
                    ".....aaaaaaaaaaaaaa.....",
                    ".....aaaaaaaaAaaaaa.....",
                    "....aaaaaaaaaaaaaaaa....",
                    "....aaaaaaaaaaaaaaaa....",
                    "....aaaaaaaaaaaaaaaa....",
                    "...aaaaaaaaaaaaaaaaaa...",
                    "...ggggggggggggggggggg..",
                    "...aaaaaaaaaaaaaaaaaa...",
                    "...aaaaaaaaaaaaaaaaaa...",
                    "...aaaaaaaaddaaaaaaaa...",
                    "...aaaaaaaddddaaaaaaa...",
                    "...aaaaaaaaddaaaaaaaa...",
                    "...aaaaaaaaaaaaaaaaaa...",
                    "..gggggggggggggggggggg..",
                    "..aaaaaaaaaaaaaaaaaaaa..",
                    "..aaaaaaaaaaaaaaaaaaaa.."),
                _ => new PixelPart("idol", Vector2.zero, m, 0,
                    ".a..............a.",
                    ".aa............aa.",
                    "..aa..........aa..",
                    "..aaa..aaaa..aaa..",
                    "...aaaaaaaaaaaa...",
                    "....aaaaaaaaaa....",
                    "....aa*aaaa*aa....",
                    "....aaaaaaaaaa....",
                    "....aaakkkkaaa....",
                    ".....akk**kka.....",
                    ".....aakkkkaa.....",
                    "...aaaaaaaaaaaa...",
                    "..aaaaaaaaaaaaaa..",
                    ".aaaaaaa*aaaaaaaa.",
                    ".aaaaaaa*aaaaaaaa.",
                    "aaaaaaaa*aaaaaaaaa",
                    "aaaaaaa***aaaaaaaa",
                    "aaaaaaaa*aaaaaaaaa",
                    "aaaaaaaaaaaaaaaaaa",
                    ".aaaaaaaaaaaaaaaa.",
                    ".aaaaaaaaaaaaaaaa.",
                    "..aaaaaaaaaaaaaa..",
                    "..aaaaaaaaaaaaaa..",
                    ".aaaaaaaaaaaaaaaa.",
                    "aaaaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaaaa",
                    "aaaa*aaaaaaaa*aaaa",
                    "aaaaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaaaa"),
            };
            return FromPart(part, 32, part.Height + 4);
        }

        // ------------------------------------------------------------------ Sarcophagus, altar, pew, crates, crystal

        private static PixelCanvas Sarcophagus()
        {
            var s = StoneRamp[0];
            var c = new PixelCanvas(80, 40);
            // Front face (14 px) then the lid seen from above (14 px).
            for (int y = 3; y < 17; y++)
            for (int x = 5; x < 75; x++)
            {
                int shade = y == 16 ? 3 : y < 5 ? 1 : 2;
                if (x == 5 || x == 74) shade = 1;
                if ((x - 5) % 14 == 0 && y > 5 && y < 15) shade = 1; // carved panels
                c.Set(x, y, s[shade]);
            }
            for (int y = 17; y < 32; y++)
            for (int x = 4 + (y > 29 ? 1 : 0); x < 76 - (y > 29 ? 1 : 0); x++)
            {
                int shade = y == 31 ? 5 : y == 17 ? 2 : 4;
                if (x <= 5) shade = 5;
                if (x >= 74) shade = 3;
                c.Set(x, y, s[shade]);
            }
            // Carved cross and skull on the lid.
            HLine(c, 30, 50, 24, s[2]);
            for (int y = 20; y < 29; y++) c.Set(40, y, s[2]);
            c.Disc(20, 24.5f, 3.2f, s[3]);
            c.Set(19, 25, s[1]); c.Set(21, 25, s[1]); c.Set(20, 23, s[1]);
            // Base shadow line and a few cracks/moss.
            HLine(c, 4, 75, 2, s[0]);
            var rng = new GameRandom(7);
            for (int i = 0; i < 12; i++) c.Set(rng.Range(6, 74), rng.Range(4, 16), rng.Chance(0.5f) ? C("#1f2a1d") : s[1]);
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Altar(int area)
        {
            var s = StoneRamp[area];
            var c = new PixelCanvas(84, 48);
            Box(c, 6, 4, 72, 16, s, 2);               // front
            for (int y = 20; y < 30; y++) HLine(c, 5, 78, y, s[y == 29 ? 5 : 4]); // top
            if (area == 2)
            {
                // Obsidian slab with a soulfire bowl and violet cracks.
                for (int x = 10; x < 74; x += 7) { c.Set(x, 8 + x % 5, Glow[2]); c.Set(x + 1, 9 + x % 5, A(Glow[2], 170)); }
                c.Disc(42, 33, 7, Iron[2]);
                c.Disc(42, 34, 5.5f, Iron[3]);
                for (int i = 0; i < 9; i++) c.Disc(42 + (i % 3 - 1) * 2.5f, 38 + i / 3 * 2.5f, 2.3f - i / 3 * 0.6f, i < 3 ? C("#7c4ad8") : i < 6 ? Glow[2] : C("#f0dcff"));
            }
            else
            {
                // Crimson altar cloth with gold trim hanging over the front.
                for (int y = 8; y < 30; y++)
                for (int x = 26; x < 58; x++)
                {
                    int shade = x < 29 ? 4 : x > 54 ? 1 : y > 27 ? 3 : 2;
                    c.Set(x, y, Crimson[shade]);
                }
                HLine(c, 26, 57, 8, Gold[3]);
                HLine(c, 26, 57, 9, Gold[2]);
                for (int y = 12; y < 26; y++) c.Set(41, y, Gold[3]);
                HLine(c, 37, 45, 21, Gold[3]);
                // Candles and a chalice on top.
                foreach (int x in new[] { 12, 16, 68, 72 })
                {
                    c.Rect(x, 28, 2, 6 + x % 3, C("#c8bfa5"));
                    c.Set(x, 34 + x % 3, C("#ffe08a"));
                    c.Set(x, 35 + x % 3, Glow[area]);
                }
                c.Rect(40, 29, 4, 2, Gold[2]);
                c.Rect(41, 31, 2, 3, Gold[3]);
                c.Rect(38, 34, 8, 4, Gold[3]);
                HLine(c, 39, 44, 37, Crimson[3]);
            }
            HLine(c, 6, 77, 3, s[0]);
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Pew()
        {
            var c = new PixelCanvas(108, 32);
            // Seat
            for (int y = 8; y < 12; y++) HLine(c, 4, 103, y, Wood[y == 11 ? 4 : 3]);
            // Backrest (seen above the seat)
            for (int y = 12; y < 26; y++)
            for (int x = 6; x < 102; x++)
            {
                int shade = y == 25 ? 4 : (x - 6) % 16 == 0 ? 1 : 2;
                c.Set(x, y, Wood[shade]);
            }
            HLine(c, 5, 102, 26, Wood[4]);
            HLine(c, 5, 102, 27, Wood[3]);
            // Legs and carved end panels
            foreach (int x in new[] { 5, 100 })
                c.Rect(x, 0, 3, 28, Wood[x < 50 ? 3 : 1]);
            foreach (int x in new[] { 30, 54, 78 })
                c.Rect(x, 1, 2, 7, Wood[1]);
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Crates()
        {
            var c = new PixelCanvas(48, 44);
            void Crate(int x0, int y0, int size)
            {
                Box(c, x0, y0, size, size, Wood, 2);
                for (int i = 1; i < size - 1; i++)
                {
                    c.Set(x0 + i, y0 + i, Wood[1]);
                    c.Set(x0 + i, y0 + size - 1 - i, Wood[1]);
                }
                HLine(c, x0, x0 + size - 1, y0 + size - 1, Wood[4]);
                c.Rect(x0, y0, 2, size, Wood[3]);
            }
            Crate(4, 2, 20);
            Crate(22, 2, 18);
            Crate(12, 21, 16);
            // A sack leaning on the side
            c.Disc(42, 9, 5.5f, C("#5a4a35"));
            c.Disc(41, 10, 4f, C("#76624a"));
            c.Set(42, 15, C("#3b2f22"));
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Crystal()
        {
            var c = new PixelCanvas(40, 60);
            var ramp = new[] { C("#1a0d2e"), C("#33195c"), C("#56309a"), C("#8456d6"), C("#b892ff"), C("#efe0ff") };
            void Shard(float bx, float by, float height, float width, float lean)
            {
                for (int y = 0; y < height; y++)
                {
                    float k = y / height;
                    float half = width * (k < 0.75f ? 1f : (1f - k) * 4f) * 0.5f;
                    float cx = bx + lean * y;
                    for (int x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
                    {
                        float u = (x - (cx - half)) / Mathf.Max(1f, 2f * half);
                        int shade = u < 0.3f ? 4 : u < 0.55f ? 3 : u < 0.8f ? 2 : 1;
                        if (y > height - 3) shade = 5;
                        c.Set(x, (int)by + y, ramp[shade]);
                    }
                }
            }
            Shard(13, 3, 30, 7, -0.12f);
            Shard(27, 3, 24, 6, 0.15f);
            Shard(20, 2, 48, 10, 0f);
            Shard(9, 2, 16, 5, -0.25f);
            Shard(31, 2, 14, 5, 0.3f);
            // Rock base
            for (int y = 0; y < 5; y++) HLine(c, 5 + y, 34 - y, y, StoneRamp[2][y == 4 ? 4 : 2]);
            c.Outline(Outline);
            return c;
        }

        // ------------------------------------------------------------------ Gimmicks

        /// <summary>The dark hall's hand lantern: an iron cage around a flickering flame, a ring to carry it by.</summary>
        private static List<PixelCanvas> Lantern()
        {
            var frames = new List<PixelCanvas>();
            var glass = new[] { C("#5a2a10"), C("#b8561c"), C("#f09038"), C("#ffd27a"), C("#fff6d8") };
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(16, 22);
                // Handle ring
                for (int x = 5; x <= 10; x++) c.Set(x, 20, Iron[3]);
                c.Set(4, 19, Iron[3]); c.Set(11, 19, Iron[2]); c.Set(4, 18, Iron[2]); c.Set(11, 18, Iron[1]);
                c.Set(6, 21, Iron[4]);
                // Cap: a little roof
                HLine(c, 5, 10, 17, Iron[2]);
                HLine(c, 4, 11, 16, Iron[3]);
                HLine(c, 3, 12, 15, Iron[2]);
                c.Set(4, 16, Iron[4]); c.Set(3, 15, Iron[3]);
                // Glass with the flame inside
                for (int y = 5; y <= 14; y++)
                for (int x = 4; x <= 11; x++)
                {
                    float dx = (x + 0.5f - 8f) / 3.6f, dy = (y + 0.5f - 8.5f) / 5.5f;
                    float heat = 1f - Mathf.Clamp01(dx * dx + dy * dy);
                    int shade = Mathf.Clamp(Mathf.FloorToInt(heat * 3.2f), 0, 3);
                    c.Set(x, y, glass[shade]);
                }
                // Flame: a teardrop that sways a pixel each frame
                int sway = f == 1 ? 1 : f == 3 ? -1 : 0;
                for (int y = 6; y <= 8; y++) { c.Set(7, y, glass[4]); c.Set(8, y, glass[4]); }
                c.Set(7 + Mathf.Max(0, sway), 9, glass[4]);
                c.Set(7 + Mathf.Max(0, sway) + (sway < 0 ? -1 : 0), 10, glass[4]);
                c.Set(7 + sway, 11, glass[3]);
                // Iron bars of the cage
                for (int y = 5; y <= 14; y++)
                {
                    c.Set(3, y, Iron[2]); c.Set(12, y, Iron[1]);
                    if (y % 3 == 0) { c.Set(7, y, A(Iron[1], 200)); }
                }
                c.Set(4, 13, A(glass[4], 255)); c.Set(4, 12, glass[3]); // glint on the glass
                // Base
                HLine(c, 3, 12, 4, Iron[3]);
                HLine(c, 4, 11, 3, Iron[2]);
                HLine(c, 5, 10, 2, Iron[1]);
                c.Set(3, 4, Iron[4]);
                c.Outline(Outline);
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> Font()
        {
            var frames = new List<PixelCanvas>();
            var s = StoneRamp[0];
            var water = new[] { C("#0d3b2a"), C("#1b6b4a"), C("#2fae73"), C("#7dffb4"), C("#e6fff2") };
            for (int f = 0; f < 4; f++)
            {
                bool empty = f == 3;
                var c = new PixelCanvas(48, 44);
                // Pedestal
                for (int y = 2; y < 14; y++) HLine(c, 18, 29, y, s[CylShade(y, 2, 14, 2) > 3 ? 3 : 2]);
                for (int y = 2; y < 14; y++) { c.Set(18, y, s[4]); c.Set(29, y, s[1]); }
                HLine(c, 14, 33, 1, s[1]); HLine(c, 14, 33, 2, s[3]);
                // Basin (ellipse rim seen from above)
                for (int y = 14; y < 32; y++)
                for (int x = 4; x < 44; x++)
                {
                    float dx = (x + 0.5f - 24f) / 20f, dy = (y + 0.5f - 24f) / 9f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    if (y < 20) { c.Set(x, y, s[2]); continue; } // bowl body
                    c.Set(x, y, d > 0.62f ? (dy > 0 ? s[5] : s[3]) : s[1]);
                }
                for (int y = 21; y < 30; y++)
                for (int x = 9; x < 39; x++)
                {
                    float dx = (x + 0.5f - 24f) / 14.5f, dy = (y + 0.5f - 25.5f) / 4.5f;
                    if (dx * dx + dy * dy > 1f) continue;
                    if (empty) { c.Set(x, y, C("#141218")); continue; }
                    int shade = ((x + y * 2 + f * 3) % 7 == 0) ? 4 : ((x * 3 + y + f * 5) % 11 == 0) ? 3 : 2;
                    c.Set(x, y, water[shade]);
                }
                if (!empty)
                    for (int i = 0; i < 4; i++)
                    {
                        int sx = 14 + i * 6 + (f * 3) % 5, sy = 31 + (i + f) % 3 * 2;
                        c.Set(sx, sy, A(water[4], 200));
                        c.Set(sx, sy + 2 + (f + i) % 2, A(water[3], 140));
                    }
                c.Outline(Outline);
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> Spikes()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(52, 52);
                // Iron plate with a rim.
                for (int y = 2; y < 50; y++)
                for (int x = 2; x < 50; x++)
                {
                    int shade = x == 2 || y == 49 ? 3 : x == 49 || y == 2 ? 0 : 2;
                    if ((x + y) % 13 == 0) shade = Mathf.Max(0, shade - 1);
                    c.Set(x, y, Iron[shade]);
                }
                for (int iy = 0; iy < 3; iy++)
                for (int ix = 0; ix < 3; ix++)
                {
                    int hx = 10 + ix * 16, hy = 9 + iy * 16;
                    c.Disc(hx, hy, 3.2f, C("#060506"));
                    if (f == 0) continue;
                    // Spikes: a hint of the tips (warning), then fully raised.
                    int len = f == 1 ? 3 : 11;
                    for (int k = 0; k < len; k++)
                    {
                        float w = (1f - k / (float)len) * 2.2f;
                        for (int x = Mathf.FloorToInt(hx - w); x <= Mathf.CeilToInt(hx + w); x++)
                            c.Set(x, hy + k, Iron[x < hx ? 4 : 3]);
                    }
                    c.Set(hx, hy + len, C("#d8dce4"));
                    if (f == 3 && (ix + iy) % 2 == 0) { c.Set(hx, hy + len - 1, C("#8a1f27")); c.Set(hx - 1, hy + len - 3, C("#5f151d")); }
                }
                c.Outline(Outline);
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> Vent()
        {
            var frames = new List<PixelCanvas>();
            var s = StoneRamp[2];
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(32, 40);
                // Pedestal
                for (int y = 1; y < 12; y++) HLine(c, 9, 22, y, s[y == 11 ? 4 : 2]);
                // Demon head facing right, mouth = the nozzle.
                c.Disc(15, 22, 8.5f, Iron[2]);
                c.Disc(14, 23, 7f, Iron[3]);
                for (int y = 29; y < 35; y++) { c.Set(9 + (y - 29) / 2, y, Iron[3]); c.Set(19 + (y - 29) / 3, y, Iron[3]); } // horns
                c.Set(18, 25, f == 0 ? C("#3a1010") : C("#ffcc66"));
                c.Set(19, 25, f == 0 ? C("#3a1010") : C("#ff7a2a"));
                Color32 mouth = f == 0 ? C("#0b0607") : f == 1 ? C("#8a2a10") : C("#ffd27a");
                c.Rect(20, 17, 4, 4, mouth);
                c.Rect(23, 18, 2, 2, f >= 2 ? C("#ffffff") : mouth);
                if (f >= 2)
                    for (int i = 0; i < 6; i++)
                        c.Set(25 + i, 18 + ((i + f) % 3) - 1, i < 3 ? C("#ffe08a") : C("#ff7a2a"));
                c.Outline(Outline);
                frames.Add(c);
            }
            return frames;
        }

        // ------------------------------------------------------------------ Area tiles (32x32)

        /// <summary>Floor/wall tiles for areas 1 and 2 (area 0 uses ProceduralArt's crypt tiles), plus wall decorations.</summary>
        public static Dictionary<string, PixelCanvas> AreaTiles()
        {
            var tiles = new Dictionary<string, PixelCanvas>();
            for (int v = 0; v < 4; v++)
            {
                tiles[$"floor{v}_1"] = ChapelFloor((ulong)(31 + v), v);
                tiles[$"floor{v}_2"] = AbyssFloor((ulong)(57 + v), v);
            }
            tiles["carpet_1"] = Carpet(false);
            tiles["carpet_edge_1"] = Carpet(true);
            tiles["wall_1"] = Wall(1, 5);
            tiles["wall_alt_1"] = Wall(1, 6);
            tiles["wall_2"] = Wall(2, 5);
            tiles["wall_alt_2"] = Wall(2, 6);
            return tiles;
        }

        /// <summary>Wall decorations by area, anchored at the bottom centre.</summary>
        public static Dictionary<string, PixelCanvas> Decor()
        {
            return new Dictionary<string, PixelCanvas>
            {
                ["window_1"] = StainedGlass(),
                ["banner_1"] = Banner(Crimson, Gold, cross: true),
                ["banner_2"] = Banner(new[] { C("#120a1c"), C("#24133a"), C("#3a1f5c"), C("#55307f"), C("#7547a8") }, new[] { C("#2a1440"), C("#5a2c8a"), C("#8f5ad0"), C("#c58bff"), C("#efe0ff") }, cross: false),
                ["chains_2"] = Chains(),
                ["bones_2"] = BonePile(),
                ["pillar_1"] = Pillar(1),
                ["pillar_2"] = Pillar(2),
            };
        }

        private static PixelCanvas ChapelFloor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 4517);
            var c = new PixelCanvas(32, 32);
            // Polished checker of dark red-brown and bone-grey marble, 16 px squares.
            var dark = new[] { C("#130a0c"), C("#1b0f12"), C("#221418"), C("#2a191d") };
            var light = new[] { C("#1c1618"), C("#241d20"), C("#2c2427"), C("#352b2e") };
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool odd = (x / 16 + y / 16) % 2 == 1;
                var ramp = odd ? dark : light;
                int shade = 2;
                if (x % 16 == 0 || y % 16 == 0) shade = 0;
                else if (y % 16 == 15 || x % 16 == 1) shade = 3;
                else if (rng.Value() < 0.08f) shade = 1;
                // Marble veins
                if (!odd && (x * 3 + y * 5 + (int)seed) % 23 == 0) shade = 3;
                c.Set(x, y, ramp[shade]);
            }
            if (variant == 3)
                for (int i = 0; i < 22; i++)
                {
                    float a = rng.Range(0f, 6.28f), r = rng.Range(0f, 5f);
                    c.Set((int)(16 + Mathf.Cos(a) * r), (int)(14 + Mathf.Sin(a) * r * 0.7f), rng.Chance(0.6f) ? C("#4a0c13") : C("#2a0a0e"));
                }
            if (variant == 1) { int x = 5, y = 20; for (int i = 0; i < 8; i++) { c.Set(x, y, dark[0]); x++; y -= rng.Range(0, 2); } }
            return c;
        }

        private static PixelCanvas Carpet(bool edge)
        {
            // Worn, dark velvet: low contrast so it frames the floor instead of shouting.
            var velvet = new[] { C("#1f060a"), C("#2e0910"), C("#3d0d15"), C("#4c121b") };
            var rng = new GameRandom(edge ? 91UL : 92UL);
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int shade = 2;
                float n = rng.Value();
                if (n < 0.12f) shade = 1;
                else if (n > 0.94f) shade = 3;
                if (edge && (x < 3 || x > 28)) { c.Set(x, y, x == 1 || x == 30 ? Gold[2] : Gold[0]); continue; }
                if (!edge && (y % 16 == 7 || y % 16 == 8) && x % 16 > 6 && x % 16 < 9) { c.Set(x, y, Gold[1]); continue; }
                c.Set(x, y, velvet[shade]);
            }
            return c;
        }

        private static PixelCanvas AbyssFloor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 7121);
            var s = StoneRamp[2];
            var c = new PixelCanvas(32, 32);
            int sx = 12 + rng.Range(0, 8), sy = 10 + rng.Range(0, 10);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool seam = x == 0 || y == 0 || (x == sx && y > sy) || (y == sy) || (x == sx / 2 && y < sy);
                int shade = seam ? 0 : 2;
                if (!seam && rng.Value() < 0.12f) shade = 1;
                if (!seam && (y == 31 || y == sy + 1 || x == 1)) shade = 3;
                c.Set(x, y, s[shade]);
            }
            // Glowing cracks
            if (variant >= 2)
            {
                int x = rng.Range(4, 26), y = rng.Range(4, 26);
                for (int i = 0; i < 10; i++)
                {
                    c.Set(x, y, i % 3 == 0 ? A(Glow[2], 200) : A(Glow[2], 120));
                    x += rng.Range(-1, 2);
                    y += 1;
                    if (y > 30) break;
                }
            }
            if (variant == 3) for (int i = 0; i < 5; i++) c.Set(rng.Range(2, 30), rng.Range(2, 30), A(Glow[2], 150));
            return c;
        }

        private static PixelCanvas Wall(int area, ulong seed)
        {
            var rng = new GameRandom(seed * 104729 + (ulong)area);
            var c = new PixelCanvas(32, 32);
            Color32[] brick = area == 1
                ? new[] { C("#160c0e"), C("#2a161a"), C("#3a2024"), C("#4a2a2e"), C("#5a3438") }
                : new[] { C("#050408"), C("#0f0c16"), C("#1a1524"), C("#251e33"), C("#322a44") };
            for (int y = 0; y < 32; y++)
            {
                int row = y / 8;
                int offset = row % 2 == 0 ? 0 : 8;
                for (int x = 0; x < 32; x++)
                {
                    bool mortar = y % 8 == 0 || (x + offset) % 16 == 0;
                    if (mortar) { c.Set(x, y, brick[0]); continue; }
                    int shade = y % 8 == 7 ? 3 : y % 8 == 1 ? 1 : 2;
                    if (rng.Value() < 0.1f) shade = Mathf.Max(1, shade - 1);
                    c.Set(x, y, brick[shade]);
                }
            }
            if (area == 1 && seed == 6) HLine(c, 0, 31, 4, Gold[2]); // gilded trim
            if (area == 2)
                for (int i = 0; i < 3; i++)
                {
                    int x = rng.Range(2, 30), y = rng.Range(2, 30);
                    c.Set(x, y, Glow[2]);
                    c.Set(x + 1, y - 1, A(Glow[2], 140));
                }
            return c;
        }

        private static PixelCanvas StainedGlass()
        {
            var c = new PixelCanvas(28, 52);
            var lead = C("#0b080c");
            Color32[] glass = { C("#a31d28"), C("#2c56a0"), C("#bb912f"), C("#46804e"), C("#7547a8") };
            for (int y = 2; y < 50; y++)
            for (int x = 2; x < 26; x++)
            {
                // Gothic arch top
                float dx = Mathf.Abs(x + 0.5f - 14f);
                if (y > 38 && dx > Mathf.Sqrt(Mathf.Max(0f, 144f - (y - 38) * (y - 38) * 1.0f))) continue;
                bool leadLine = x % 6 == 2 || y % 8 == 2;
                c.Set(x, y, leadLine ? lead : glass[((x / 6) + (y / 8) * 2) % glass.Length]);
            }
            // A pale figure in the centre panes
            for (int y = 18; y < 34; y++) c.Set(14, y, C("#e6dcc8"));
            for (int x = 11; x < 18; x++) c.Set(x, 30, C("#e6dcc8"));
            c.Disc(14, 36, 2.2f, C("#ffe9b0"));
            c.Outline(StoneRamp[1][1]);
            return c;
        }

        private static PixelCanvas Banner(Color32[] cloth, Color32[] trim, bool cross)
        {
            var c = new PixelCanvas(16, 32);
            c.Rect(1, 30, 14, 2, trim[2]);
            for (int y = 4; y < 30; y++)
            for (int x = 3; x < 13; x++)
                c.Set(x, y, cloth[x < 5 ? 3 : x > 10 ? 1 : 2]);
            for (int x = 3; x < 13; x++) c.Rect(x, 4 - (x * 5) % 4, 1, (x * 5) % 4, cloth[1]);
            if (cross)
            {
                for (int y = 12; y < 26; y++) c.Set(8, y, trim[3]);
                for (int x = 5; x < 12; x++) c.Set(x, 21, trim[3]);
            }
            else
            {
                // An eye sigil
                c.Disc(8, 19, 3.5f, trim[2]);
                c.Disc(8, 19, 2f, trim[4]);
                c.Set(8, 19, C("#0b080c"));
                c.Set(8, 24, trim[3]); c.Set(8, 14, trim[3]);
            }
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Chains()
        {
            var c = new PixelCanvas(20, 48);
            foreach (int x in new[] { 5, 14 })
            {
                int len = x == 5 ? 44 : 32;
                for (int y = 47; y > 47 - len; y -= 4)
                {
                    c.Set(x, y, Iron[3]); c.Set(x + 1, y, Iron[4]);
                    c.Set(x - 1, y - 1, Iron[2]); c.Set(x + 2, y - 1, Iron[2]);
                    c.Set(x, y - 2, Iron[3]); c.Set(x + 1, y - 2, Iron[1]);
                }
                // Shackle at the end
                c.Rect(x - 1, 47 - len - 2, 4, 3, Iron[3]);
            }
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas BonePile()
        {
            var c = new PixelCanvas(40, 24);
            var rng = new GameRandom(77);
            var bone = C("#b8ae95");
            for (int i = 0; i < 9; i++)
            {
                float x = rng.Range(6f, 34f), y = rng.Range(3f, 12f), a = rng.Range(0f, 3.14f);
                float dx = Mathf.Cos(a) * 5, dy = Mathf.Sin(a) * 3;
                c.Line(x - dx, y - dy, x + dx, y + dy, bone);
            }
            c.Disc(16, 10, 3.5f, bone);
            c.Disc(25, 13, 3.2f, C("#a39a82"));
            c.Set(15, 10, C("#524b3c")); c.Set(17, 10, C("#524b3c"));
            c.Set(24, 13, C("#524b3c")); c.Set(26, 13, C("#524b3c"));
            c.Outline(Outline);
            return c;
        }
    }
}
