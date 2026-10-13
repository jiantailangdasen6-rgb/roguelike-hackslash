using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Floors, walls and wall decorations of the lands added after the first three (Biome 3..5): the sunken
    /// catacombs (wet green stone, puddles, roots), the frozen mausoleum (frosted tiles, snow, icicles) and the
    /// burnt forge (scorched brick, iron grates, glowing cracks). 32 px = 1 tile, named "floor{v}_{land}" like the others.
    /// </summary>
    public static class LandArt
    {
        /// <summary>Lands with a look (BuildAreaLook makes one root per land).</summary>
        public const int Count = 6;

        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static Color32 A(Color32 c, byte a) => new(c.r, c.g, c.b, a);

        public static readonly Color32 SunkenGlow = C("#5fe8c8");
        public static readonly Color32 FrostGlow = C("#a8e4ff");
        public static readonly Color32 ForgeGlow = C("#ff7a1f");

        public static Dictionary<string, PixelCanvas> Tiles()
        {
            var tiles = new Dictionary<string, PixelCanvas>();
            for (int v = 0; v < 4; v++)
            {
                tiles[$"floor{v}_3"] = SunkenFloor((ulong)(71 + v), v);
                tiles[$"floor{v}_4"] = FrostFloor((ulong)(83 + v), v);
                tiles[$"floor{v}_5"] = ForgeFloor((ulong)(97 + v), v);
            }
            for (int land = 3; land < Count; land++)
            {
                tiles[$"wall_{land}"] = Wall(land, 5);
                tiles[$"wall_alt_{land}"] = Wall(land, 6);
            }
            return tiles;
        }

        /// <summary>Wall decorations (anchored at the bottom centre) and floor scatter of the new lands.</summary>
        public static Dictionary<string, PixelCanvas> Decor() => new()
        {
            ["roots_3"] = Roots(),
            ["banner_3"] = Tatters(new[] { C("#081211"), C("#0f201e"), C("#17302c"), C("#22443e"), C("#2f5c53") }, C("#6f5a2c")),
            ["urns_3"] = Urns(),
            ["icicles_4"] = Icicles(),
            ["banner_4"] = Tatters(new[] { C("#121826"), C("#1f2a40"), C("#33415f"), C("#4b5d82"), C("#6b80a8") }, C("#c6d8ee")),
            ["snow_4"] = Drift(),
            ["rack_5"] = WeaponRack(),
            ["banner_5"] = Tatters(new[] { C("#0b0807"), C("#151010"), C("#201817"), C("#2c2220"), C("#3a2e2a") }, ForgeGlow),
            ["slag_5"] = Slag(),
        };

        // ------------------------------------------------------------------ Floors

        /// <summary>Wet slate slabs, moss in the seams; puddles that catch the light (variants 2 and 3).</summary>
        private static PixelCanvas SunkenFloor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 6151);
            var stone = new[] { C("#0e1814"), C("#15231c"), C("#1c2d24"), C("#24382d"), C("#2e4537") };
            var moss = new[] { C("#1a2a10"), C("#243a16"), C("#30501e") };
            var c = new PixelCanvas(32, 32);
            int sx = 10 + rng.Range(0, 12), sy = 9 + rng.Range(0, 14);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool seam = x == 0 || y == 0 || (y == sy) || (x == sx && y < sy) || (x == (sx + 16) % 32 && y > sy);
                int shade = 2;
                if (!seam && rng.Value() < 0.14f) shade = 1;
                if (!seam && rng.Value() < 0.05f) shade = 3;
                if (!seam && (y == sy - 1 || y == 31 || x == 1)) shade = 3;
                var col = seam ? (rng.Chance(0.45f) ? moss[rng.Range(0, 2)] : stone[0]) : stone[shade];
                c.Set(x, y, col);
            }
            if (variant == 1)
                for (int i = 0; i < 26; i++)
                {
                    int x = rng.Range(2, 30), y = rng.Range(2, 30);
                    c.Set(x, y, moss[rng.Range(0, 3)]);
                    if (rng.Chance(0.5f)) c.Set(x + 1, y, moss[1]);
                }
            if (variant == 2)
            {
                // Wet stone: a darker sheen with a couple of glints, no edge to it.
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    if (Hash(x / 3 + (int)seed, y / 2) > 0.62f && c.Get(x, y).g > 30) c.Set(x, y, stone[1]);
                c.Set(rng.Range(4, 28), rng.Range(4, 28), C("#2f6e62"));
            }
            if (variant == 3)
            {
                // A small puddle with a soft edge and a single glint of the drowned light.
                float cx = rng.Range(10f, 22f), cy = rng.Range(10f, 22f);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x + 0.5f - cx) / 7f, dy = (y + 0.5f - cy) / 4f, d = dx * dx + dy * dy;
                    if (d > 1f + (Hash(x, y) - 0.5f) * 0.5f) continue;
                    c.Set(x, y, d > 0.6f ? stone[1] : C("#0c1614"));
                }
                c.Set((int)cx + 2, (int)cy + 1, C("#3f8a7c"));
            }
            return c;
        }

        /// <summary>Frosted marble squares; drifts of snow (variant 2) and a sheet of ice with streaks of shine (variant 3).</summary>
        private static PixelCanvas FrostFloor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 3571);
            var tile = new[] { C("#121826"), C("#182030"), C("#1e283b"), C("#253046"), C("#2e3b54") };
            var frost = C("#3e4e6a");
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int shade = 2;
                if (x % 16 == 0 || y % 16 == 0) shade = 0;
                else if (y % 16 == 15 || x % 16 == 1) shade = 3;
                else if (rng.Value() < 0.1f) shade = 1;
                var col = tile[shade];
                if (shade != 0 && rng.Value() < 0.07f) col = frost;
                c.Set(x, y, col);
            }
            if (variant == 1)
            {
                // A crack with frost along it.
                int x = rng.Range(4, 12), y = rng.Range(18, 28);
                for (int i = 0; i < 14; i++)
                {
                    c.Set(x, y, tile[0]);
                    c.Set(x, y + 1, frost);
                    x++;
                    y -= rng.Range(0, 2);
                }
            }
            if (variant == 2)
            {
                // Snow piled in a soft drift, brightest on its crest.
                float cx = rng.Range(8f, 24f), cy = rng.Range(8f, 24f);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x + 0.5f - cx) / 8f, dy = (y + 0.5f - cy) / 4.5f, d = dx * dx + dy * dy;
                    if (d > 1f + (Hash(x, y) - 0.5f) * 0.9f) continue;
                    c.Set(x, y, d < 0.25f ? C("#6d82a6") : d < 0.6f ? C("#536888") : C("#3c4c68"));
                }
            }
            if (variant == 3)
            {
                for (int y = 2; y < 30; y++)
                for (int x = 2; x < 30; x++)
                    if (x % 16 != 0 && y % 16 != 0) c.Set(x, y, (x - y + 64) % 11 == 0 ? C("#4a6890") : C("#1a2a44"));
            }
            return c;
        }

        /// <summary>Scorched brick; an iron grate glowing from below (variant 2) and a crack of embers (variant 3).</summary>
        private static PixelCanvas ForgeFloor(ulong seed, int variant)
        {
            var rng = new GameRandom(seed * 4339);
            var brick = new[] { C("#0e0907"), C("#160f0c"), C("#1e1510"), C("#271c15"), C("#31241b") };
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            {
                int row = y / 8, offset = row % 2 == 0 ? 0 : 8;
                for (int x = 0; x < 32; x++)
                {
                    bool mortar = y % 8 == 0 || (x + offset) % 16 == 0;
                    int shade = mortar ? 0 : y % 8 == 7 ? 3 : 2;
                    if (!mortar && rng.Value() < 0.12f) shade = 1;
                    c.Set(x, y, brick[shade]);
                }
            }
            // Soot.
            for (int i = 0; i < 14; i++) c.Set(rng.Range(1, 31), rng.Range(1, 31), C("#080504"));
            if (variant == 2)
            {
                // A small iron drain over a faint red glow.
                var iron = new[] { C("#151516"), C("#1f1f21"), C("#2b2b2e"), C("#3a3a3e") };
                for (int y = 9; y < 23; y++)
                for (int x = 9; x < 23; x++)
                {
                    bool edge = x == 9 || y == 9 || x == 22 || y == 22;
                    bool bar = (x - 9) % 3 == 0 || (y - 9) % 3 == 0;
                    var col = edge ? iron[y == 22 ? 3 : 2] : bar ? iron[1] : ((x * 3 + y) % 7 == 0 ? C("#5a1e08") : C("#24100a"));
                    c.Set(x, y, col);
                }
            }
            if (variant == 3)
            {
                // A short hairline crack, dull red with one or two hot points.
                int x = rng.Range(6, 14), y = rng.Range(8, 24);
                for (int i = 0; i < 12 && x < 30; i++)
                {
                    c.Set(x, y, i == 5 ? C("#ff9a3c") : i % 4 == 1 ? C("#c24a12") : C("#6a220a"));
                    x++;
                    y += rng.Range(-1, 2);
                    y = Mathf.Clamp(y, 2, 29);
                }
            }
            return c;
        }

        // ------------------------------------------------------------------ Walls

        private static PixelCanvas Wall(int land, ulong seed)
        {
            var rng = new GameRandom(seed * 104729 + (ulong)land);
            var c = new PixelCanvas(32, 32);
            Color32[] brick = land switch
            {
                3 => new[] { C("#060b0b"), C("#0e1716"), C("#152220"), C("#1d2d2a"), C("#273b37") },
                4 => new[] { C("#0a0e17"), C("#131a28"), C("#1c2639"), C("#27334b"), C("#35435e") },
                _ => new[] { C("#0a0605"), C("#150e0b"), C("#201510"), C("#2b1d15"), C("#37261c") },
            };
            for (int y = 0; y < 32; y++)
            {
                int row = y / 8, offset = row % 2 == 0 ? 0 : 8;
                for (int x = 0; x < 32; x++)
                {
                    bool mortar = y % 8 == 0 || (x + offset) % 16 == 0;
                    if (mortar) { c.Set(x, y, brick[0]); continue; }
                    int shade = y % 8 == 7 ? 3 : y % 8 == 1 ? 1 : 2;
                    if (rng.Value() < 0.1f) shade = Mathf.Max(1, shade - 1);
                    c.Set(x, y, brick[shade]);
                }
            }
            bool alt = seed == 6;
            switch (land)
            {
                case 3:
                    // Water stains running down from the joints, moss along them.
                    for (int i = 0; i < (alt ? 5 : 3); i++)
                    {
                        int x = rng.Range(1, 31), top = rng.Range(14, 31);
                        for (int y = top; y > top - rng.Range(6, 14) && y > 0; y--) c.Set(x, y, y % 3 == 0 ? C("#1c3a2c") : C("#0a1413"));
                        c.Set(x, top, C("#2a4a26"));
                        c.Set(x + 1, top, C("#2a4a26"));
                    }
                    if (alt) for (int x = 0; x < 32; x++) if (Hash(x, 7) > 0.35f) c.Set(x, 1 + (int)(Hash(x, 3) * 3), C("#1f3a1c"));
                    break;
                case 4:
                    // Rime along the top of every course; icicles under it on the alternate block.
                    for (int y = 7; y < 32; y += 8)
                        for (int x = 0; x < 32; x++)
                            if (Hash(x, y) > 0.3f) c.Set(x, y, C("#5a6f90"));
                    if (alt)
                        for (int x = 2; x < 32; x += 5)
                        {
                            int len = 2 + (int)(Hash(x, 1) * 5);
                            for (int k = 0; k < len; k++) c.Set(x, 22 - k, k == len - 1 ? C("#a8d0f0") : C("#7a9cc4"));
                        }
                    break;
                default:
                    // Soot climbing from below; on the alternate block a seam glowing hot.
                    for (int x = 0; x < 32; x++)
                        for (int y = 0; y < 4 + (int)(Hash(x, 2) * 6); y++)
                            if (Hash(x, y + 9) > 0.35f) c.Set(x, y, C("#070404"));
                    if (alt)
                        for (int x = 0; x < 32; x++)
                            if ((x + 8) % 16 != 0) { c.Set(x, 16, C("#a33a0c")); if (x % 3 == 0) c.Set(x, 16, ForgeGlow); }
                    break;
            }
            return c;
        }

        // ------------------------------------------------------------------ Decorations

        /// <summary>Roots and drowned weed hanging down the wall.</summary>
        private static PixelCanvas Roots()
        {
            var c = new PixelCanvas(24, 44);
            var rng = new GameRandom(311);
            var root = new[] { C("#1a140c"), C("#2c2214"), C("#3e311d") };
            var weed = new[] { C("#14261a"), C("#1e3a24"), C("#2c5232") };
            for (int s = 0; s < 6; s++)
            {
                float x = 3 + s * 3.4f;
                int len = 18 + rng.Range(0, 24);
                bool isWeed = s % 2 == 1;
                for (int y = 43; y > 43 - len; y--)
                {
                    x += rng.Range(-0.6f, 0.6f);
                    var ramp = isWeed ? weed : root;
                    c.Set((int)x, y, ramp[1]);
                    c.Set((int)x + 1, y, ramp[(y % 4 == 0) ? 2 : 0]);
                    if (isWeed && y % 5 == 0) c.Set((int)x - 1, y, ramp[2]);
                }
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>A long ragged banner (worn ends) in the land's cloth, with a trim stripe.</summary>
        private static PixelCanvas Tatters(Color32[] cloth, Color32 trim)
        {
            var c = new PixelCanvas(16, 36);
            c.Rect(1, 34, 14, 2, C("#2a2620"));
            for (int y = 4; y < 34; y++)
            for (int x = 3; x < 13; x++)
            {
                if (y < 10 && Hash(x, y) > 0.55f + (y - 4) * 0.08f) continue; // torn hem
                c.Set(x, y, cloth[x < 5 ? 3 : x > 10 ? 1 : 2]);
            }
            for (int x = 3; x < 13; x++) c.Set(x, 30, trim);
            for (int y = 16; y < 26; y++) c.Set(8, y, A(trim, 200));
            c.Disc(8, 21, 2.2f, trim);
            c.Set(8, 21, cloth[0]);
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>Broken burial urns and shells left by the water (floor scatter).</summary>
        private static PixelCanvas Urns()
        {
            var c = new PixelCanvas(28, 18);
            var clay = new[] { C("#2a1a12"), C("#4a2e1c"), C("#6a4428"), C("#8a5c36") };
            for (int y = 2; y < 12; y++)
            for (int x = 4; x < 12; x++)
            {
                float dx = (x + 0.5f - 8f) / 4f, dy = (y + 0.5f - 7f) / 5f;
                if (dx * dx + dy * dy > 1f || (y > 9 && x > 8)) continue; // the broken lip
                c.Set(x, y, clay[x < 6 ? 3 : x > 9 ? 1 : 2]);
            }
            c.Rect(15, 2, 5, 3, clay[1]); c.Rect(16, 5, 3, 1, clay[2]); // a shard
            c.Rect(22, 3, 3, 2, C("#b8ae95")); c.Set(23, 5, C("#d6ccb0")); // a shell
            c.Set(12, 2, C("#1f3a1c")); c.Set(13, 3, C("#1f3a1c"));
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>A row of icicles under the wall's lip.</summary>
        private static PixelCanvas Icicles()
        {
            var c = new PixelCanvas(40, 24);
            for (int x = 1; x < 39; x++)
            {
                int len = 3 + (int)(Hash(x, 5) * Hash(x / 3, 2) * 20f);
                for (int k = 0; k < len; k++)
                {
                    int y = 23 - k;
                    var col = k == len - 1 ? C("#e8f6ff") : x % 3 == 0 ? C("#9cc4e8") : C("#6f94bf");
                    c.Set(x, y, col);
                }
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>Snow heaped against the wall (floor scatter).</summary>
        private static PixelCanvas Drift()
        {
            var c = new PixelCanvas(40, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 40; x++)
            {
                float dx = (x + 0.5f - 20f) / 19f, dy = y / 12f;
                if (dy > 1f - dx * dx + (Hash(x, y) - 0.5f) * 0.12f) continue;
                c.Set(x, y, dy > 0.6f ? C("#9fb6d6") : dy > 0.3f ? C("#7a92b6") : C("#5a7096"));
            }
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>A rack of blades and axes left half-forged.</summary>
        private static PixelCanvas WeaponRack()
        {
            var c = new PixelCanvas(32, 40);
            var wood = new[] { C("#1d140c"), C("#372616"), C("#563b21"), C("#78552e") };
            var steel = new[] { C("#2b2f3a"), C("#474d5a"), C("#6f7785"), C("#a9b1bb") };
            c.Rect(3, 2, 2, 34, wood[2]); c.Rect(27, 2, 2, 34, wood[1]);
            c.Rect(3, 30, 26, 2, wood[3]); c.Rect(3, 12, 26, 2, wood[2]);
            foreach (int x in new[] { 8, 14, 20 })
            {
                for (int y = 6; y < 34; y++) c.Set(x, y, steel[y > 28 ? 3 : 2]);
                c.Set(x + 1, 30, steel[1]);
                c.Rect(x - 1, 8, 3, 1, wood[1]); // guard
                for (int y = 3; y < 8; y++) c.Set(x, y, wood[2]);
            }
            // An axe head, still glowing at the edge.
            c.Rect(24, 22, 3, 7, steel[1]);
            c.Set(26, 22, ForgeGlow); c.Set(26, 23, C("#ffb347")); c.Set(26, 24, ForgeGlow);
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        /// <summary>Lumps of cooling slag with a hot heart (floor scatter).</summary>
        private static PixelCanvas Slag()
        {
            var c = new PixelCanvas(26, 14);
            c.Disc(8, 6, 5f, C("#1a1210"));
            c.Disc(8, 7, 3.6f, C("#2a1c16"));
            c.Disc(18, 5, 3.6f, C("#1a1210"));
            c.Disc(18, 6, 2.4f, C("#2a1c16"));
            c.Set(8, 7, ForgeGlow); c.Set(9, 7, C("#ffb347")); c.Set(7, 6, C("#a33a0c"));
            c.Set(18, 6, C("#a33a0c")); c.Set(19, 6, ForgeGlow);
            c.Outline(PixelRenderer.Outline);
            return c;
        }

        private static float Hash(int x, int y) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);
    }
}
