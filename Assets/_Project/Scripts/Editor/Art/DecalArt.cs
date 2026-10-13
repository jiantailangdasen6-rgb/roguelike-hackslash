using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Floor features that make the rooms of a land differ from each other (ArenaView picks a few per room from the
    /// land's set): pools of water, frozen sheets, spilled slag, glowing rifts, rugs, rubble, cracks and blood.
    /// Flat, drawn on the floor under everything. 32 px = 1 tile.
    /// </summary>
    public static class DecalArt
    {
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static Color32 A(Color32 c, byte a) => new(c.r, c.g, c.b, a);
        private static readonly Color32 Outline = PixelRenderer.Outline;

        public static Dictionary<string, PropArt.Sheet> Sheets() => new()
        {
            ["decal_pool"] = One(Pool()),
            ["decal_algae"] = One(Algae()),
            ["decal_ice"] = One(IceSheet()),
            ["decal_frozen"] = One(Frozen()),
            ["decal_slag"] = One(SlagSpill()),
            ["decal_tools"] = One(Tools()),
            ["decal_rift"] = One(Rift()),
            ["decal_chainpile"] = One(ChainPile()),
            ["decal_rug_red"] = One(Rug(new[] { C("#1f060a"), C("#2e0910"), C("#3d0d15"), C("#4c121b") }, C("#87641c"))),
            ["decal_rug_brown"] = One(Rug(new[] { C("#160f0a"), C("#21170f"), C("#2c1f14"), C("#38281a") }, C("#4a3a26"))),
            ["decal_glass"] = One(Glass()),
            ["decal_blood"] = One(Blood()),
            ["decal_rubble"] = One(Rubble()),
            ["decal_crack"] = One(Crack()),
        };

        private static PropArt.Sheet One(PixelCanvas c) => new() { Frames = new List<PixelCanvas> { c }, Pivot = new Vector2(0.5f, 0.5f) };

        private static float Hash(int x, int y) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);

        /// <summary>Distance-like value of an irregular blob (below 1 = inside), wobbling with the angle.</summary>
        private static float Blob(float x, float y, float cx, float cy, float rx, float ry, int seed, float wobble = 0.22f)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            float a = Mathf.Atan2(dy, dx);
            float r = 1f + wobble * (Mathf.Sin(a * 3f + seed) * 0.6f + Mathf.Sin(a * 5f + seed * 2.3f) * 0.4f);
            return Mathf.Sqrt(dx * dx + dy * dy) / r;
        }

        // ------------------------------------------------------------------ Sunken

        /// <summary>A shallow pool: still dark water, a lighter shallow rim with ripples, glints of the drowned light.</summary>
        private static PixelCanvas Pool()
        {
            var c = new PixelCanvas(160, 96);
            for (int y = 0; y < 96; y++)
            for (int x = 0; x < 160; x++)
            {
                float d = Blob(x + 0.5f, y + 0.5f, 80f, 48f, 74f, 40f, 3);
                if (d > 1f) continue;
                Color32 col = d > 0.9f ? C("#16342d") : d > 0.72f ? C("#0d221e") : C("#071513");
                if (d > 0.9f && Hash(x / 2, y / 2) > 0.6f) col = C("#1f4a3f");
                // Rings of ripples and a band of reflected light on the far side.
                if (Mathf.Abs(d - 0.5f) < 0.012f && x % 3 != 0) col = C("#123029");
                if (Mathf.Abs(d - 0.3f) < 0.012f && x % 4 == 0) col = C("#123029");
                if (y > 58 && d < 0.8f && d > 0.55f && (x + y * 3) % 17 == 0) col = C("#3fa894");
                c.Set(x, y, col);
            }
            foreach (var (x, y) in new[] { (40, 60), (104, 36), (122, 58), (66, 30) })
            {
                c.Set(x, y, C("#7ff0d8")); c.Set(x + 1, y, C("#3fa894"));
            }
            return c;
        }

        /// <summary>A green bloom of algae and weed on the wet stone (no edge: it fades out).</summary>
        private static PixelCanvas Algae()
        {
            // Clumps of weed: a dark mat, lighter tips, a few strands trailing off.
            var c = new PixelCanvas(72, 44);
            var g = new[] { C("#14240e"), C("#1c3313"), C("#264419"), C("#335a20") };
            var rng = new Game.Core.GameRandom(57);
            for (int k = 0; k < 7; k++)
            {
                float cx = rng.Range(12f, 60f), cy = rng.Range(10f, 34f), r = rng.Range(3f, 6.5f);
                for (int y = (int)(cy - r); y <= cy + r; y++)
                for (int x = (int)(cx - r * 1.4f); x <= cx + r * 1.4f; x++)
                {
                    float d = Blob(x + 0.5f, y + 0.5f, cx, cy, r * 1.4f, r, k, 0.4f);
                    if (d > 1f) continue;
                    c.Set(x, y, d > 0.7f ? g[0] : d > 0.35f ? g[1] : g[2]);
                }
                for (int s = 0; s < 3; s++)
                {
                    float x = cx, y = cy, a = rng.Range(0f, 6.28f);
                    for (int i = 0; i < 7; i++) { c.Set((int)x, (int)y, i < 4 ? g[2] : g[3]); x += Mathf.Cos(a); y += Mathf.Sin(a) * 0.6f; a += rng.Range(-0.5f, 0.5f); }
                }
            }
            return c;
        }

        // ------------------------------------------------------------------ Frost

        /// <summary>A frozen pool: smooth blue ice with white cracks and streaks of shine, a ridge of snow round it.</summary>
        private static PixelCanvas IceSheet()
        {
            var c = new PixelCanvas(144, 84);
            for (int y = 0; y < 84; y++)
            for (int x = 0; x < 144; x++)
            {
                float d = Blob(x + 0.5f, y + 0.5f, 72f, 42f, 68f, 37f, 11);
                if (d > 1f) continue;
                Color32 col = d > 0.92f ? C("#7e96ba") : d > 0.84f ? C("#3e5478") : C("#1d3150");
                if (d < 0.84f && (x - y * 2 + 300) % 23 < 2) col = C("#5d7ea8");    // shine
                if (d < 0.84f && (x - y * 2 + 300) % 23 == 2) col = C("#a8c8ec");
                c.Set(x, y, col);
            }
            // Cracks branching from a point.
            var rng = new Game.Core.GameRandom(21);
            for (int b = 0; b < 4; b++)
            {
                float x = 70, y = 40, a = b * 1.57f + rng.Range(-0.4f, 0.4f);
                for (int i = 0; i < 26; i++)
                {
                    if (Blob(x, y, 72f, 42f, 68f, 37f, 11) > 0.84f) break;
                    c.Set((int)x, (int)y, C("#c6e0ff"));
                    a += rng.Range(-0.35f, 0.35f);
                    x += Mathf.Cos(a) * 1.3f;
                    y += Mathf.Sin(a) * 0.8f;
                }
            }
            return c;
        }

        /// <summary>Someone who never got out: a dark shape inside a block of ice.</summary>
        private static PixelCanvas Frozen()
        {
            var c = new PixelCanvas(52, 30);
            for (int y = 2; y < 26; y++)
            for (int x = 2; x < 50; x++)
            {
                bool rim = x == 2 || x == 49 || y == 2 || y == 25;
                c.Set(x, y, rim ? C("#8fb0d8") : y > 20 ? C("#4e6c94") : C("#2c4466"));
            }
            // The body: head, torso, an arm reaching out.
            c.Disc(12, 14, 3.5f, C("#1a1e28"));
            c.Rect(15, 11, 18, 6, C("#151922"));
            c.Rect(33, 13, 9, 2, C("#1a1e28"));
            c.Rect(41, 12, 2, 4, C("#1a1e28"));
            for (int i = 0; i < 7; i++) c.Set(6 + i * 6, 22 - i % 3, C("#c6e0ff"));
            c.Outline(Outline);
            return c;
        }

        // ------------------------------------------------------------------ Forge

        /// <summary>Molten iron spilled and crusted over: black crust, veins and a heart still glowing.</summary>
        private static PixelCanvas SlagSpill()
        {
            var c = new PixelCanvas(120, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 120; x++)
            {
                float d = Blob(x + 0.5f, y + 0.5f, 60f, 32f, 56f, 28f, 5, 0.3f);
                if (d > 1f) continue;
                Color32 col = d > 0.88f ? C("#2a1c15") : C("#160e0b");
                float vein = Mathf.Abs(Mathf.Sin(x * 0.21f + y * 0.37f) + Mathf.Sin(x * 0.11f - y * 0.29f));
                if (d < 0.75f && vein < 0.08f) col = d < 0.35f ? C("#ffb347") : d < 0.55f ? C("#ff7a1f") : C("#8a2a0a");
                if (d < 0.22f) col = Hash(x, y) > 0.5f ? C("#ff9a3c") : C("#ffcf7a");
                c.Set(x, y, col);
            }
            return c;
        }

        /// <summary>A smith's tools dropped where they were: tongs, a hammer, horseshoes.</summary>
        private static PixelCanvas Tools()
        {
            var c = new PixelCanvas(60, 30);
            var iron = new[] { C("#262628"), C("#3b3b3e"), C("#57585c") };
            var wood = new[] { C("#372616"), C("#563b21") };
            c.Line(6, 6, 24, 20, iron[1]); c.Line(8, 5, 26, 18, iron[2]);          // tongs
            c.Rect(32, 8, 2, 14, wood[1]); c.Rect(28, 20, 10, 5, iron[1]); c.Rect(28, 24, 10, 1, iron[2]); // hammer
            c.Ring(48, 10, 3f, 4.5f, iron[1]); c.Rect(46, 5, 5, 3, default);       // horseshoe (open at the top)
            c.Ring(52, 22, 2.5f, 4f, iron[2]);
            c.Outline(Outline);
            return c;
        }

        // ------------------------------------------------------------------ Abyss

        /// <summary>A rift in the floor: jagged black with violet light welling up from below.</summary>
        private static PixelCanvas Rift()
        {
            var c = new PixelCanvas(132, 44);
            var rng = new Game.Core.GameRandom(9);
            float w = 1f;
            for (int x = 4; x < 128; x++)
            {
                float t = (x - 4) / 124f;
                float half = Mathf.Sin(t * Mathf.PI) * 9f * w + 1f;
                w = Mathf.Clamp(w + rng.Range(-0.12f, 0.12f), 0.6f, 1.3f);
                float cy = 22f + Mathf.Sin(t * 9f) * 4f;
                for (int y = (int)(cy - half - 3); y <= cy + half + 3; y++)
                {
                    float k = Mathf.Abs(y - cy);
                    Color32 col = k < half * 0.45f ? C("#05020a") : k < half ? C("#2a1450") : k < half + 1.5f ? C("#6a3ab0") : A(C("#c58bff"), 90);
                    if (k < half * 0.45f && Hash(x, y) > 0.95f) col = C("#c58bff");
                    c.Set(x, y, col);
                }
            }
            return c;
        }

        private static PixelCanvas ChainPile()
        {
            var c = new PixelCanvas(52, 26);
            var iron = new[] { C("#1d1d1f"), C("#3b3b3d"), C("#57585c"), C("#7c7e84") };
            for (int i = 0; i < 26; i++)
            {
                float a = i * 0.7f, r = 3f + i * 0.6f;
                int x = 26 + (int)(Mathf.Cos(a) * r), y = 13 + (int)(Mathf.Sin(a) * r * 0.45f);
                c.Ring(x, y, 1f, 2.2f, iron[1 + i % 3]);
            }
            c.Outline(Outline);
            return c;
        }

        // ------------------------------------------------------------------ Chapel and crypt

        /// <summary>A worn runner rug with a border, frayed at the ends.</summary>
        private static PixelCanvas Rug(Color32[] cloth, Color32 trim)
        {
            var c = new PixelCanvas(104, 60);
            for (int y = 2; y < 58; y++)
            for (int x = 2; x < 102; x++)
            {
                bool fray = (x < 6 || x > 97) && Hash(x, y) > 0.5f;
                if (fray) continue;
                bool border = y < 6 || y > 53 || x < 9 || x > 94;
                var col = border ? (y == 4 || y == 55 || x == 7 || x == 96 ? trim : cloth[1]) : cloth[Hash(x / 3, y / 3) > 0.8f ? 3 : 2];
                // A worn patch in the middle.
                if (!border && Blob(x, y, 60f, 30f, 14f, 9f, 2) < 1f && Hash(x, y) > 0.4f) col = cloth[0];
                c.Set(x, y, col);
            }
            return c;
        }

        /// <summary>Shards of stained glass fallen from a window, catching the light.</summary>
        private static PixelCanvas Glass()
        {
            var c = new PixelCanvas(56, 34);
            Color32[] glass = { C("#a31d28"), C("#2c56a0"), C("#bb912f"), C("#46804e"), C("#7547a8") };
            var rng = new Game.Core.GameRandom(41);
            for (int i = 0; i < 16; i++)
            {
                int x = rng.Range(4, 52), y = rng.Range(4, 30), w = rng.Range(2, 5), h = rng.Range(1, 3);
                var col = glass[rng.Range(0, glass.Length)];
                c.Rect(x, y, w, h, col);
                c.Set(x, y + h - 1, Color32.Lerp(col, new Color32(255, 255, 255, 255), 0.6f));
            }
            c.Outline(Outline);
            return c;
        }

        // ------------------------------------------------------------------ Shared

        private static PixelCanvas Blood()
        {
            var c = new PixelCanvas(72, 44);
            for (int y = 0; y < 44; y++)
            for (int x = 0; x < 72; x++)
            {
                float d = Blob(x + 0.5f, y + 0.5f, 30f, 22f, 20f, 13f, 4, 0.3f);
                bool drop = Blob(x + 0.5f, y + 0.5f, 58f, 30f, 4f, 3f, 1) < 1f || Blob(x + 0.5f, y + 0.5f, 52f, 12f, 3f, 2.4f, 2) < 1f;
                if (d > 1f && !drop) continue;
                c.Set(x, y, d < 0.6f ? C("#2a0508") : C("#3d0a10"));
            }
            return c;
        }

        /// <summary>Fallen stones and grit from a broken ceiling.</summary>
        private static PixelCanvas Rubble()
        {
            var c = new PixelCanvas(60, 34);
            var s = new[] { C("#1a1820"), C("#2a2631"), C("#3a3542"), C("#4c4656") };
            var rng = new Game.Core.GameRandom(17);
            for (int i = 0; i < 9; i++)
            {
                float x = rng.Range(8f, 52f), y = rng.Range(7f, 24f), r = rng.Range(2f, 5f);
                c.Disc(x, y, r, s[1]);
                c.Disc(x - r * 0.3f, y + r * 0.3f, r * 0.6f, s[2]);
                c.Set((int)(x - r * 0.4f), (int)(y + r * 0.5f), s[3]);
            }
            for (int i = 0; i < 30; i++) c.Set(rng.Range(2, 58), rng.Range(2, 32), s[rng.Range(0, 3)]);
            c.Outline(Outline);
            return c;
        }

        /// <summary>A long crack through the flagstones.</summary>
        private static PixelCanvas Crack()
        {
            var c = new PixelCanvas(110, 46);
            var rng = new Game.Core.GameRandom(33);
            void Branch(float x, float y, float a, int len, int width)
            {
                for (int i = 0; i < len; i++)
                {
                    c.Set((int)x, (int)y, C("#07050a"));
                    if (width > 1) c.Set((int)x, (int)y + 1, C("#100c12"));
                    if (i % 7 == 3 && len > 12) Branch(x, y, a + rng.Range(-1f, 1f), len / 3, 1);
                    a += rng.Range(-0.3f, 0.3f);
                    x += Mathf.Cos(a);
                    y += Mathf.Sin(a) * 0.6f;
                    if (x < 1 || x > 108 || y < 1 || y > 44) return;
                }
            }
            Branch(4, 22, 0f, 104, 2);
            return c;
        }
    }
}
