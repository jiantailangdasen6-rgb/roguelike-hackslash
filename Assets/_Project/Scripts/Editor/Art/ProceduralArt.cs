using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>Small immediate-mode pixel canvas for procedural effects and tiles (origin bottom-left).</summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            Pixels[y * Width + x] = c;
        }

        public Color32 Get(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? default : Pixels[y * Width + x];

        public void Fill(Color32 c) => Array.Fill(Pixels, c);

        public void Disc(float cx, float cy, float r, Color32 c)
        {
            for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
            for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) Set(x, y, c);
        }

        public void Ring(float cx, float cy, float r0, float r1, Color32 c)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                if (d >= r0 && d <= r1) Set(x, y, c);
            }
        }

        public void Line(float x0, float y0, float x1, float y1, Color32 c)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Set(Mathf.FloorToInt(Mathf.Lerp(x0, x1, t)), Mathf.FloorToInt(Mathf.Lerp(y0, y1, t)), c);
            }
        }

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++) Set(xx, yy, c);
        }

        public void Outline(Color32 c)
        {
            var copy = (Color32[])Pixels.Clone();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (copy[y * Width + x].a != 0) continue;
                bool touch = (x > 0 && copy[y * Width + x - 1].a != 0) || (x < Width - 1 && copy[y * Width + x + 1].a != 0)
                             || (y > 0 && copy[(y - 1) * Width + x].a != 0) || (y < Height - 1 && copy[(y + 1) * Width + x].a != 0);
                if (touch) Pixels[y * Width + x] = c;
            }
        }

        public static void WriteStrip(string path, IReadOnlyList<PixelCanvas> frames)
        {
            int w = frames[0].Width, h = frames[0].Height;
            var tex = new Texture2D(w * frames.Count, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[w * frames.Count * h]);
            for (int i = 0; i < frames.Count; i++) tex.SetPixels32(i * w, 0, w, h, frames[i].Pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ArtIO.WriteBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    /// <summary>Effects, projectiles, telegraph shapes and environment tiles.</summary>
    public static class ProceduralArt
    {
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static Color32 A(Color32 c, byte a) => new(c.r, c.g, c.b, a);
        private static readonly Color32 Outline = PixelRenderer.Outline;

        public delegate List<PixelCanvas> Generator();

        public static readonly Dictionary<string, Generator> Effects = new()
        {
            ["hit"] = Hit,
            ["puff"] = Puff,
            ["bolt"] = Bolt,
            ["arrow"] = () => new List<PixelCanvas> { Arrow(C("#6d4b33"), C("#c9d2dc"), C("#8a1f27")) },
            ["bonearrow"] = () => new List<PixelCanvas> { Arrow(C("#857c6a"), C("#e2dac1"), C("#ff3b2f")) },
            ["fireball"] = Fireball,
            ["soul"] = Soul,
            ["shard"] = () => new List<PixelCanvas> { Shard() },
            ["shadow"] = () => new List<PixelCanvas> { Shadow() },
            ["circle"] = () => new List<PixelCanvas> { TelegraphCircle() },
            ["cone"] = () => new List<PixelCanvas> { TelegraphCone() },
            ["line"] = () => new List<PixelCanvas> { TelegraphLine() },
            ["nova"] = () => new List<PixelCanvas> { Nova(C("#8fd3ff"), C("#effaff")) },
            ["heal"] = () => new List<PixelCanvas> { Nova(C("#7dff9a"), C("#e6ffd0")) },
            ["warcry"] = () => new List<PixelCanvas> { Nova(C("#ecce6a"), C("#fff3c0")) },
            ["portal"] = Portal,
            ["mark"] = () => new List<PixelCanvas> { Mark() },
            ["ice"] = () => new List<PixelCanvas> { IceBlock() },
            ["candle"] = Candle,
            ["candle_red"] = () => CandleTinted(C("#ff5a3a"), C("#ffb08a"), C("#c8b0a5")),
            ["candle_soul"] = () => CandleTinted(C("#a66bff"), C("#e6d0ff"), C("#8f8aa8")),
            ["candle_teal"] = () => CandleTinted(C("#4fd8b8"), C("#dffff6"), C("#7d958f")),
            ["candle_ice"] = () => CandleTinted(C("#7cc4ff"), C("#f0faff"), C("#b8c8dc")),
            ["candle_ember"] = () => CandleTinted(C("#ff6a1a"), C("#ffd08a"), C("#5a4636")),
            ["square"] = () => new List<PixelCanvas> { TelegraphSquare() },
            ["flame"] = FlameJet,
            ["explosion"] = Explosion,
            ["holy"] = HolyColumn,
            ["orb"] = Orb,
            ["downed"] = () => new List<PixelCanvas> { DownedIcon() },
            ["revive_ring"] = () => new List<PixelCanvas> { ReviveRing() },
        };

        private static List<PixelCanvas> CandleTinted(Color32 flame, Color32 core, Color32 wax)
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 3; f++)
            {
                var c = new PixelCanvas(16, 16);
                void Wax(int x, int h) { c.Rect(x, 1, 2, h, wax); c.Set(x, h, Color32.Lerp(wax, new Color32(255, 255, 255, 255), 0.3f)); }
                Wax(4, 6); Wax(8, 9); Wax(11, 5);
                c.Rect(2, 0, 12, 1, C("#3a3540"));
                void Flame(int x, int y) { c.Set(x, y + 1, flame); c.Set(x, y + 2, core); if (f != 1) c.Set(x + (f == 2 ? 1 : 0), y + 3, flame); }
                Flame(4, 6); Flame(8, 9); Flame(11, 5);
                frames.Add(c);
            }
            return frames;
        }

        private static PixelCanvas TelegraphSquare()
        {
            var c = new PixelCanvas(32, 32);
            c.Rect(0, 0, 32, 32, new Color32(255, 255, 255, 70));
            c.Rect(0, 0, 32, 2, new Color32(255, 255, 255, 230));
            c.Rect(0, 30, 32, 2, new Color32(255, 255, 255, 230));
            c.Rect(0, 0, 2, 32, new Color32(255, 255, 255, 230));
            c.Rect(30, 0, 2, 32, new Color32(255, 255, 255, 230));
            return c;
        }

        /// <summary>A horizontal jet of fire (pivot at the left/nozzle end), 2 units long and half a unit tall.</summary>
        private static List<PixelCanvas> FlameJet()
        {
            var frames = new List<PixelCanvas>();
            var rng = new GameRandom(23);
            Color32[] ramp = { C("#5f151d"), C("#a31d28"), C("#ff5a2a"), C("#ff9a3c"), C("#ffe08a"), C("#fff8e0") };
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(64, 16);
                for (int x = 0; x < 64; x++)
                {
                    float k = x / 63f;
                    float half = Mathf.Lerp(2.5f, 7.5f, Mathf.Sqrt(k)) + Mathf.Sin(x * 0.45f + f * 1.7f) * 1.2f;
                    for (int y = 0; y < 16; y++)
                    {
                        float d = Mathf.Abs(y + 0.5f - 8f) / half;
                        if (d > 1f) continue;
                        float heat = (1f - d) * (1f - k * 0.55f) + rng.Range(-0.12f, 0.12f);
                        int shade = Mathf.Clamp(Mathf.FloorToInt(heat * ramp.Length), 0, ramp.Length - 1);
                        if (k > 0.85f && rng.Chance(0.35f)) continue; // ragged tip
                        c.Set(x, y, ramp[shade]);
                    }
                }
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> Explosion()
        {
            var frames = new List<PixelCanvas>();
            var rng = new GameRandom(99);
            Color32[] fire = { C("#fff8e0"), C("#ffe08a"), C("#ff9a3c"), C("#ff5a2a"), C("#a31d28") };
            Color32[] smoke = { C("#3a3441"), C("#2a2430"), C("#1a161d") };
            var blobs = new List<(float x, float y, float r)>();
            for (int i = 0; i < 9; i++) blobs.Add((rng.Range(-9f, 9f), rng.Range(-6f, 9f), rng.Range(5f, 9f)));
            for (int f = 0; f < 8; f++)
            {
                var c = new PixelCanvas(64, 64);
                float k = f / 7f;
                foreach (var b in blobs)
                {
                    float r = b.r * (0.5f + k * 1.1f);
                    var col = f < 2 ? fire[0] : f < 3 ? fire[1] : f < 5 ? (b.r > 7f ? fire[2] : fire[3]) : smoke[Mathf.Min(2, f - 5)];
                    c.Disc(32 + b.x * (0.6f + k), 30 + b.y * (0.6f + k) + k * 6f, r * (f >= 6 ? 0.8f : 1f), col);
                }
                if (f < 5) c.Disc(32, 31 + k * 4f, 10f * (1f - k * 0.6f), fire[Mathf.Min(f, 2)]);
                if (f >= 5)
                    for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        if ((x + y + f) % (f == 5 ? 4 : 2) == 0) c.Set(x, y, default);
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> HolyColumn()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 6; f++)
            {
                var c = new PixelCanvas(32, 48);
                float k = f / 5f;
                for (int y = 0; y < 48; y++)
                {
                    float half = (6f + 3f * Mathf.Sin(y * 0.3f + f)) * (1f - k * 0.5f);
                    for (int x = 0; x < 32; x++)
                    {
                        float d = Mathf.Abs(x + 0.5f - 16f);
                        if (d > half) continue;
                        byte a = (byte)(Mathf.Clamp01(1f - d / half) * (1f - y / 60f) * 230f);
                        c.Set(x, y, d < half * 0.35f ? new Color32(240, 255, 245, a) : new Color32(140, 255, 190, a));
                    }
                }
                for (int i = 0; i < 6; i++)
                {
                    int sx = 6 + (i * 7 + f * 3) % 20, sy = (i * 9 + f * 8) % 46;
                    c.Set(sx, sy, new Color32(255, 255, 255, 255));
                    c.Set(sx, sy + 1, new Color32(200, 255, 220, 200));
                }
                frames.Add(c);
            }
            return frames;
        }

        /// <summary>Glowing orb drawn in near-white so it can be tinted (blood red, soul cyan).</summary>
        private static List<PixelCanvas> Orb()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(16, 16);
                c.Disc(8, 8, 6.5f + (f % 2) * 0.4f, new Color32(150, 150, 150, 120));
                c.Disc(8, 8, 5f, new Color32(200, 200, 200, 255));
                c.Disc(8, 8, 3.5f, new Color32(235, 235, 235, 255));
                c.Disc(7.5f, 8.5f, 1.8f, new Color32(255, 255, 255, 255));
                c.Set(2 + f, 8, new Color32(200, 200, 200, 200));
                c.Set(8, 13 - f, new Color32(200, 200, 200, 200));
                frames.Add(c);
            }
            return frames;
        }

        /// <summary>Skull marker floating over a downed player (white, tinted at runtime).</summary>
        private static PixelCanvas DownedIcon()
        {
            var c = new PixelCanvas(16, 18);
            var w = new Color32(245, 240, 235, 255);
            var dark = new Color32(40, 20, 24, 255);
            c.Disc(8, 11, 5.5f, w);
            c.Rect(5, 4, 7, 4, w);
            c.Rect(5, 9, 2, 3, dark); c.Rect(10, 9, 2, 3, dark);
            c.Set(8, 7, dark);
            c.Set(6, 4, dark); c.Set(8, 4, dark); c.Set(10, 4, dark);
            // Small cross-bones under it
            c.Line(2, 0, 14, 3, w);
            c.Line(2, 3, 14, 0, w);
            c.Outline(Outline);
            return c;
        }

        /// <summary>Dashed ring two units across: where to stand to help a downed teammate.</summary>
        private static PixelCanvas ReviveRing()
        {
            var c = new PixelCanvas(64, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = x + 0.5f - 32f, dy = y + 0.5f - 32f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 28.5f || d > 31.5f) continue;
                float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                if ((int)(a / 15f) % 2 == 0) c.Set(x, y, new Color32(255, 255, 255, 235));
            }
            return c;
        }

        private static List<PixelCanvas> Hit()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 5; f++)
            {
                var c = new PixelCanvas(24, 24);
                float r = 3f + f * 2.4f;
                var col = f < 2 ? C("#ffffff") : f < 4 ? C("#ffe08a") : C("#ff8a2a");
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * Mathf.PI / 4f + (f % 2) * 0.2f;
                    float inner = f * 1.5f;
                    float len = a % 2 == 0 ? r : r * 0.6f;
                    c.Line(12 + Mathf.Cos(ang) * inner, 12 + Mathf.Sin(ang) * inner, 12 + Mathf.Cos(ang) * len, 12 + Mathf.Sin(ang) * len, col);
                }
                if (f < 2) c.Disc(12, 12, 3 - f, C("#ffffff"));
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> Puff()
        {
            var frames = new List<PixelCanvas>();
            var rng = new GameRandom(11);
            var blobs = new List<(float x, float y, float r)>();
            for (int i = 0; i < 7; i++) blobs.Add((rng.Range(-5f, 5f), rng.Range(-3f, 5f), rng.Range(3f, 5.5f)));
            var embers = new List<(float x, float y, float vx, float vy)>();
            for (int i = 0; i < 6; i++) embers.Add((0, 0, rng.Range(-1.2f, 1.2f), rng.Range(0.5f, 2f)));

            for (int f = 0; f < 7; f++)
            {
                var c = new PixelCanvas(32, 32);
                float k = f / 6f;
                var smoke = Color32.Lerp(C("#3a3441"), C("#1a161d"), k);
                foreach (var b in blobs)
                {
                    float r = b.r * (0.6f + k * 0.9f) * (1f - k * 0.35f);
                    if (f >= 5 && b.r < 4f) continue;
                    c.Disc(16 + b.x * (1 + k), 14 + b.y * (1 + k) + k * 4f, r, smoke);
                }
                // Dither the outer smoke away as it fades.
                if (f >= 4)
                    for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                        if ((x + y + f) % (f == 4 ? 3 : 2) == 0) c.Set(x, y, default);
                foreach (var e in embers)
                    c.Set((int)(16 + e.vx * f * 2.2f), (int)(14 + e.vy * f * 2f), f < 5 ? C("#ff5a2a") : C("#8a1f27"));
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> Bolt()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(16, 16);
                c.Disc(8, 8, 6 + (f % 2) * 0.5f, C("#2a5c9a"));
                c.Disc(8, 8, 4.5f, C("#5fa8ff"));
                c.Disc(8, 8, 3f, C("#8fd3ff"));
                c.Disc(8.5f, 8.5f, 1.6f, C("#effaff"));
                c.Set(2 + f, 8 + (f % 2), C("#8fd3ff"));
                c.Set(13 - f, 7 - (f % 2), C("#8fd3ff"));
                frames.Add(c);
            }
            return frames;
        }

        private static PixelCanvas Arrow(Color32 shaft, Color32 tip, Color32 fletch)
        {
            var c = new PixelCanvas(20, 5);
            for (int x = 3; x < 16; x++) c.Set(x, 2, shaft);
            c.Set(16, 1, tip); c.Set(16, 2, tip); c.Set(16, 3, tip); c.Set(17, 2, tip); c.Set(18, 2, tip); c.Set(17, 1, tip); c.Set(17, 3, tip);
            c.Set(1, 1, fletch); c.Set(2, 1, fletch); c.Set(3, 1, fletch); c.Set(1, 3, fletch); c.Set(2, 3, fletch); c.Set(3, 3, fletch); c.Set(0, 2, fletch);
            return c;
        }

        private static List<PixelCanvas> Fireball()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(16, 16);
                c.Disc(7 - (f % 2), 8, 5.5f, C("#8a1f27"));
                c.Disc(8, 8, 4.5f, C("#ff5a2a"));
                c.Disc(8.5f, 8, 3f, C("#ff9a3c"));
                c.Disc(9, 8.5f, 1.5f, C("#ffe08a"));
                c.Set(1 + f % 2, 9, C("#ff5a2a"));
                c.Set(2, 6 + f % 2, C("#8a1f27"));
                frames.Add(c);
            }
            return frames;
        }

        private static List<PixelCanvas> Soul()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(12, 14);
                c.Disc(6, 5, 4f, C("#2f6f9f"));
                c.Disc(6, 5, 3f, C("#7fd0ff"));
                c.Disc(6, 5.5f, 1.5f, C("#e8fbff"));
                // flickering flame tip
                int sway = f % 2 == 0 ? 0 : 1;
                c.Set(5 + sway, 9, C("#7fd0ff")); c.Set(6 + sway, 10, C("#7fd0ff")); c.Set(6, 11 + (f == 2 ? 1 : 0), C("#2f6f9f"));
                c.Set(6, 9, C("#7fd0ff"));
                frames.Add(c);
            }
            return frames;
        }

        private static PixelCanvas Shard()
        {
            var c = new PixelCanvas(8, 8);
            c.Line(1, 1, 6, 6, C("#8fd3ff"));
            c.Line(2, 1, 6, 5, C("#effaff"));
            c.Line(1, 2, 5, 6, C("#5fa8ff"));
            return c;
        }

        private static PixelCanvas Shadow()
        {
            var c = new PixelCanvas(24, 8);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 24; x++)
            {
                float dx = (x + 0.5f - 12f) / 12f, dy = (y + 0.5f - 4f) / 4f;
                float d = dx * dx + dy * dy;
                if (d <= 1f) c.Set(x, y, new Color32(0, 0, 0, (byte)(d < 0.5f ? 150 : 90)));
            }
            return c;
        }

        // Telegraphs are white with alpha; tinted red at runtime. Fill is faint, edge is solid.
        private static PixelCanvas TelegraphCircle()
        {
            var c = new PixelCanvas(64, 64);
            c.Disc(32, 32, 31.5f, new Color32(255, 255, 255, 70));
            c.Ring(32, 32, 29.5f, 31.5f, new Color32(255, 255, 255, 230));
            return c;
        }

        private static PixelCanvas TelegraphCone()
        {
            // 90° sector pointing right (+X), apex at the left-centre.
            var c = new PixelCanvas(64, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = x + 0.5f, dy = y + 0.5f - 32f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                if (d > 63.5f || ang > 45f) continue;
                bool edge = d > 61f || ang > 43f;
                c.Set(x, y, new Color32(255, 255, 255, (byte)(edge ? 230 : 70)));
            }
            return c;
        }

        private static PixelCanvas TelegraphLine()
        {
            var c = new PixelCanvas(64, 8);
            c.Rect(0, 0, 64, 8, new Color32(255, 255, 255, 70));
            c.Rect(0, 0, 64, 1, new Color32(255, 255, 255, 230));
            c.Rect(0, 7, 64, 1, new Color32(255, 255, 255, 230));
            return c;
        }

        private static PixelCanvas Nova(Color32 main, Color32 light)
        {
            var c = new PixelCanvas(64, 64);
            c.Ring(32, 32, 26f, 31.5f, A(main, 200));
            c.Ring(32, 32, 28.5f, 30f, A(light, 255));
            c.Disc(32, 32, 26f, A(main, 40));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                c.Line(32 + Mathf.Cos(a) * 22, 32 + Mathf.Sin(a) * 22, 32 + Mathf.Cos(a) * 31, 32 + Mathf.Sin(a) * 31, A(light, 255));
            }
            return c;
        }

        private static List<PixelCanvas> Portal()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(48, 48);
                c.Disc(24, 24, 22, C("#140608"));
                c.Disc(24, 24, 18, C("#2a0c10"));
                c.Ring(24, 24, 20, 23, C("#8a1f27"));
                c.Ring(24, 24, 21, 22, C("#ff5a4a"));
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f + f * Mathf.PI / 16f;
                    c.Disc(24 + Mathf.Cos(a) * 21.5f, 24 + Mathf.Sin(a) * 21.5f, 1.4f, C("#ffd2a6"));
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f - f * 0.5f;
                    for (float r = 3; r < 16; r += 1f)
                        c.Set((int)(24 + Mathf.Cos(a + r * 0.25f) * r), (int)(24 + Mathf.Sin(a + r * 0.25f) * r), C("#631b23"));
                }
                frames.Add(c);
            }
            return frames;
        }

        private static PixelCanvas Mark()
        {
            var c = new PixelCanvas(16, 16);
            var red = C("#ff3b2f");
            c.Ring(8, 8, 5, 6.5f, red);
            c.Line(8, 0, 8, 4, red); c.Line(8, 12, 8, 15, red); c.Line(0, 8, 4, 8, red); c.Line(12, 8, 15, 8, red);
            c.Set(8, 8, red);
            return c;
        }

        private static PixelCanvas IceBlock()
        {
            var c = new PixelCanvas(32, 32);
            c.Disc(16, 14, 13, new Color32(143, 211, 255, 110));
            c.Ring(16, 14, 11.5f, 13f, new Color32(220, 245, 255, 200));
            c.Line(9, 20, 13, 24, new Color32(255, 255, 255, 220));
            c.Line(20, 7, 24, 11, new Color32(255, 255, 255, 160));
            return c;
        }

        private static List<PixelCanvas> Candle()
        {
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 3; f++)
            {
                var c = new PixelCanvas(16, 16);
                void Wax(int x, int h) { c.Rect(x, 1, 2, h, C("#c8bfa5")); c.Set(x, h, C("#e2dac1")); }
                Wax(4, 6); Wax(8, 9); Wax(11, 5);
                c.Rect(2, 0, 12, 1, C("#57503f"));
                void Flame(int x, int y) { c.Set(x, y + 1, C("#ff9a3c")); c.Set(x, y + 2, C("#ffe08a")); if (f != 1) c.Set(x + (f == 2 ? 1 : 0), y + 3, C("#ff5a2a")); }
                Flame(4, 6); Flame(8, 9); Flame(11, 5);
                frames.Add(c);
            }
            return frames;
        }

        // ------------------------------------------------------------------ Tiles (32x32)

        public static Dictionary<string, PixelCanvas> Tiles()
        {
            return new Dictionary<string, PixelCanvas>
            {
                ["floor0"] = Floor(1, 0),
                ["floor1"] = Floor(2, 1),
                ["floor2"] = Floor(3, 2),
                ["floor3"] = Floor(4, 3),
                ["wall"] = WallFace(5),
                ["wall_alt"] = WallFace(6),
                ["walltop"] = WallTop(7),
                ["bones"] = Bones(8),
                ["pillar"] = Pillar(),
                ["banner"] = Banner(),
                ["shade_top"] = Shade(0, 1),
                ["shade_left"] = Shade(1, 0),
                ["shade_right"] = Shade(-1, 0),
                ["mote"] = Mote(),
            };
        }

        /// <summary>Soft black gradient laid on the floor along a wall (ambient occlusion). dx/dy = direction toward the wall.</summary>
        private static PixelCanvas Shade(int towardLeft, int towardTop)
        {
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float t = towardTop != 0 ? y / 31f : towardLeft > 0 ? 1f - x / 31f : x / 31f;
                // Quantize into a few bands so it stays pixel-art-like.
                float band = Mathf.Floor(t * 5f) / 5f;
                c.Set(x, y, new Color32(4, 2, 6, (byte)(band * band * 190f)));
            }
            return c;
        }

        private static PixelCanvas Mote()
        {
            var c = new PixelCanvas(3, 3);
            c.Set(1, 1, new Color32(255, 220, 160, 255));
            c.Set(0, 1, new Color32(255, 140, 60, 140));
            c.Set(2, 1, new Color32(255, 140, 60, 140));
            c.Set(1, 0, new Color32(255, 140, 60, 140));
            c.Set(1, 2, new Color32(255, 140, 60, 140));
            return c;
        }

        private static readonly Color32[] Stone = { C("#13100f"), C("#1d1815"), C("#28221e"), C("#332b26"), C("#3e352e") };

        /// <summary>Flagstones: 2x2 irregular slabs with mortar, noise and a variant detail (crack, moss, blood).</summary>
        private static PixelCanvas Floor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 7919);
            var c = new PixelCanvas(32, 32);
            int splitX = 14 + rng.Range(0, 5), splitY = 14 + rng.Range(0, 5);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool mortar = x == 0 || y == 0 || x == splitX || (y == splitY && x < splitX) || (y == 31 - splitY / 2 && x > splitX);
                if (mortar) { c.Set(x, y, Stone[0]); continue; }
                int shade = 2;
                float n = rng.Value();
                if (n < 0.12f) shade = 1;
                else if (n > 0.9f) shade = 3;
                // bevel: light top-left edges of each slab, dark bottom-right
                bool edgeTop = y == 31 || y == splitY - 1 || y == 31 - splitY / 2 - 1;
                bool edgeLeft = x == 1 || x == splitX + 1;
                if (edgeTop || edgeLeft) shade = Mathf.Min(4, shade + 1);
                c.Set(x, y, Stone[shade]);
            }

            switch (variant)
            {
                case 1: // crack
                {
                    int x = 6 + rng.Range(0, 6), y = 6 + rng.Range(0, 6);
                    for (int i = 0; i < 9; i++)
                    {
                        c.Set(x, y, Stone[0]);
                        x += rng.Range(0, 2);
                        y += rng.Range(-1, 2) == 0 ? 1 : 0;
                    }
                    break;
                }
                case 2: // moss
                    for (int i = 0; i < 18; i++) c.Set(rng.Range(18, 31), rng.Range(1, 12), rng.Chance(0.5f) ? C("#1f2a1d") : C("#2a3a26"));
                    break;
                case 3: // old blood
                    for (int i = 0; i < 26; i++)
                    {
                        float a = rng.Range(0f, 6.28f), r = rng.Range(0f, 6f);
                        c.Set((int)(20 + Mathf.Cos(a) * r), (int)(20 + Mathf.Sin(a) * r * 0.7f), rng.Chance(0.6f) ? C("#3a0e14") : C("#2a0a0e"));
                    }
                    break;
            }
            return c;
        }

        private static PixelCanvas WallFace(ulong seed)
        {
            var rng = new GameRandom(seed * 104729);
            var c = new PixelCanvas(32, 32);
            Color32[] brick = { C("#171312"), C("#241e1b"), C("#2f2723"), C("#3a312b"), C("#463b34") };
            for (int y = 0; y < 32; y++)
            {
                int row = y / 8;
                int offset = row % 2 == 0 ? 0 : 8;
                for (int x = 0; x < 32; x++)
                {
                    bool mortar = y % 8 == 0 || (x + offset) % 16 == 0;
                    if (mortar) { c.Set(x, y, brick[0]); continue; }
                    int shade = 2;
                    if (y % 8 == 7) shade = 3; // top edge of brick lit
                    if (y % 8 == 1) shade = 1;
                    if (rng.Value() < 0.1f) shade = Mathf.Max(1, shade - 1);
                    c.Set(x, y, brick[shade]);
                }
            }
            for (int i = 0; i < 6; i++) c.Set(rng.Range(0, 32), rng.Range(0, 32), brick[0]);
            return c;
        }

        private static PixelCanvas WallTop(ulong seed)
        {
            var rng = new GameRandom(seed);
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                c.Set(x, y, rng.Value() < 0.08f ? C("#16141a") : C("#0d0b10"));
            return c;
        }

        private static PixelCanvas Bones(ulong seed)
        {
            var rng = new GameRandom(seed);
            var c = new PixelCanvas(32, 32);
            var bone = C("#b8ae95");
            var dark = C("#524b3c");
            for (int i = 0; i < 5; i++)
            {
                float x = rng.Range(6f, 26f), y = rng.Range(6f, 20f), a = rng.Range(0f, 3.14f);
                float dx = Mathf.Cos(a) * 5, dy = Mathf.Sin(a) * 5;
                c.Line(x - dx, y - dy, x + dx, y + dy, bone);
                c.Set((int)(x - dx), (int)(y - dy) + 1, bone);
                c.Set((int)(x + dx), (int)(y + dy) + 1, bone);
            }
            // a skull
            c.Disc(14, 12, 3.5f, bone);
            c.Set(13, 12, dark); c.Set(15, 12, dark); c.Set(14, 10, dark);
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Pillar()
        {
            var c = new PixelCanvas(32, 64);
            Color32[] s = { C("#18151b"), C("#2a2631"), C("#3a3542"), C("#4c4656"), C("#5f586a") };
            for (int y = 4; y < 52; y++)
            for (int x = 8; x < 24; x++)
            {
                int shade = x < 11 ? 3 : x > 20 ? 1 : 2;
                if ((y - 4) % 12 == 0) shade = 0;
                c.Set(x, y, s[shade]);
            }
            c.Rect(5, 0, 22, 5, s[2]); c.Rect(5, 4, 22, 1, s[3]);
            // broken top
            for (int x = 8; x < 24; x++) c.Rect(x, 52, 1, 2 + (x * 7) % 5, s[x < 11 ? 3 : 2]);
            c.Outline(Outline);
            return c;
        }

        private static PixelCanvas Banner()
        {
            var c = new PixelCanvas(16, 32);
            var red = Palette.Red.Ramp;
            c.Rect(1, 30, 14, 2, C("#87641c"));
            for (int y = 4; y < 30; y++)
            for (int x = 3; x < 13; x++)
            {
                int shade = x < 5 ? 3 : x > 10 ? 1 : 2;
                c.Set(x, y, red[shade]);
            }
            // ragged bottom
            for (int x = 3; x < 13; x++) c.Rect(x, 4 - (x * 5) % 4, 1, (x * 5) % 4, red[1]);
            // sigil
            c.Line(6, 20, 8, 24, C("#bb912f")); c.Line(8, 24, 10, 20, C("#bb912f")); c.Line(8, 24, 8, 14, C("#bb912f"));
            c.Outline(Outline);
            return c;
        }
    }
}
