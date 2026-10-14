using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Objects of the grotto's and the library's lords' trials: the glowing caps (clean air in the spore fog), the lilies
    /// that cure the parasite, the forbidden tomes on their lecterns and the ink beast's candelabra. As with the sigils,
    /// "walk" is the lit look (a lit tome or candle; caps and lilies glow either way).
    /// </summary>
    public static partial class GimmickArt
    {
        private static PixelMaterial M(params string[] hex) => new(2, hex);

        /// <summary>A clump of mushrooms glowing green, breathing light.</summary>
        public static List<Clip> Glowcap()
        {
            var m = Palette.With(('c', M("#1c3a12", "#2c5a1c", "#46862a", "#6ab83c", "#9cf06a")), ('s', M("#3a4a30", "#5a6a48", "#7e8e66", "#a4b488", "#c8d8aa")),
                ('g', new PixelMaterial("#d8ffb0")));
            var big = new PixelPart("big", P(5f, 0.5f), m, 0,
                "...cccc...",
                ".cccgcccc.",
                "ccgcccccgc",
                "cccccccccc",
                "gggggggggg",
                "....ss....",
                "....ss....",
                "....ss....",
                "...ssss...");
            var small = new PixelPart("small", P(3f, 0.5f), m, 0, ".ccc..", "ccgcc.", "gggggg", "..ss..", "..ss..", ".ssss.");
            Frame Pose(int glow)
            {
                var f = new Frame();
                f.Add(small, P(26f, 10f), 8f, 3, glow);
                f.Add(big, P(32f, 10f), 0f, 4, glow);
                f.Add(small, P(38f, 10f), -10f, 5, glow);
                return f;
            }
            var idle = new Clip("idle", 4f, true);
            foreach (int g in new[] { 0, 1, 1, 0 }) idle.Frames.Add(Pose(g));
            var hurt = new Clip("hurt", 12f, false);
            hurt.Frames.Add(Pose(1));
            var death = Shatter(new[] { small, small, small, small }, P(32f, 14f), 4);
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }

        /// <summary>A white lily on a tall stem, swaying; it sheds its petals when used.</summary>
        public static List<Clip> Lily()
        {
            var m = Palette.With(('w', M("#6a6a5a", "#a8a490", "#d8d4bc", "#f0ecd8", "#ffffff")), ('l', M("#0e2a10", "#1a4a1c", "#2a6a2a", "#3e8e3a", "#5ab04e")),
                ('y', new PixelMaterial("#ffe08a")));
            var bloom = new PixelPart("bloom", P(4f, 0.5f), m, 0,
                "w..ww..w",
                "ww.ww.ww",
                ".wwwwww.",
                "..wyyw..",
                "..wwww..",
                "...ww...");
            var stem = new PixelPart("stem", P(1.5f, 0.5f), m, 0, ".l", ".l", "ll", "l.", "l.", ".l", ".l", "ll", "l.", ".l");
            var leaf = new PixelPart("leaf", P(0.5f, 0.5f), m, 0, "...ll", ".llll", "lll..");
            Frame Pose(float sway, int glow)
            {
                var f = new Frame();
                f.Add(leaf, P(31f, 11f), 10f, 3);
                f.Add(leaf, P(33f, 13f), 160f, 3);
                f.Add(stem, P(32f, 10f), sway, 4);
                f.Add(bloom, P(32f + sway * 0.12f, 19f), sway * 0.6f, 5, glow);
                return f;
            }
            var idle = new Clip("idle", 4f, true);
            foreach (var s in new[] { 0f, 4f, 0f, -4f }) idle.Frames.Add(Pose(s, s == 0f ? 1 : 0));
            var hurt = new Clip("hurt", 12f, false);
            hurt.Frames.Add(Pose(6f, 1));
            var petal = new PixelPart("petal", P(1f, 0.5f), m, 0, "ww", "w.");
            var death = Shatter(new[] { petal, petal, leaf, petal }, P(32f, 20f), 5);
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }

        /// <summary>A forbidden tome chained open on a lectern: dark and quiet, or lit with violet script ("walk").</summary>
        public static List<Clip> ForbiddenTome()
        {
            var m = Palette.With(('o', M("#120b07", "#1c120b", "#2b1e16", "#3d2b1f", "#523b2a")), ('p', M("#3a3428", "#6a604a", "#9a8e70", "#c4b896", "#e8dcbc")),
                ('l', M("#1a0a0c", "#341418", "#521e22", "#74302c", "#984838")), ('v', new PixelMaterial("#c4a8ff")), ('n', M("#2a2a30", "#3e3e46", "#56565e", "#72727c", "#9090a0")));
            var lectern = new PixelPart("lectern", P(6f, 0.5f), m, 0,
                "oooooooooooo",
                ".oooooooooo.",
                "...oooooo...",
                "....oooo....",
                "....oooo....",
                "....oooo....",
                "....oooo....",
                "....oooo....",
                "...oooooo...",
                "..oooooooo..");
            string[] book(bool lit) => new[]
            {
                "ll..........ll",
                "lpppppnnpppppl",
                lit ? "lpvpvpnnpvvpvl" : "lpkpkpnnpkkpkl",
                "lppppppppppppl",
                lit ? "lpvvpvnnvpvppl" : "lpkkpknnkpkppl",
                "lppppppppppppl",
                "llllllllllllll",
            };
            var dark = new PixelPart("book", P(7f, 0.5f), m, 0, book(false));
            var lit = new PixelPart("book_lit", P(7f, 0.5f), m, 0, book(true));
            Frame Pose(PixelPart b, int glow, float shake = 0f)
            {
                var f = new Frame();
                f.Add(lectern, P(32f + shake, 10f), 0f, 3);
                f.Add(b, P(32f + shake, 19f), -8f, 5, glow);
                return f;
            }
            var idle = new Clip("idle", 4f, true);
            idle.Frames.Add(Pose(dark, 0));
            var on = new Clip("walk", 6f, true);
            foreach (int g in new[] { 1, 2, 1, 0 }) on.Frames.Add(Pose(lit, g));
            var hurt = new Clip("hurt", 16f, false);
            hurt.Frames.Add(Pose(dark, 1, 1.5f));
            hurt.Frames.Add(Pose(dark, 0, -1.5f));
            var page = new PixelPart("page", P(1f, 0.5f), m, 0, "ppp", "pp.");
            var death = Shatter(new[] { page, page, page, page }, P(32f, 20f), 4);
            return new List<Clip> { idle, on, Same("windup", idle), Same("strike", idle), hurt, death };
        }

        /// <summary>A tall iron candelabrum with three candles: cold and smoking, or burning ("walk").</summary>
        public static List<Clip> Candelabra()
        {
            var m = Palette.With(('n', M("#141418", "#24242a", "#3a3a42", "#56565e", "#7a7a86")), ('w', M("#6a6252", "#9a9078", "#c8bea0", "#e4dcc0", "#f8f0dc")),
                ('f', new PixelMaterial("#ffb84a")), ('F', new PixelMaterial("#fff0c0")), ('s', new PixelMaterial("#6a6a74")));
            string[] stand(bool burning) => new[]
            {
                burning ? ".F...F...F." : ".s...s...s.",
                burning ? ".f...f...f." : "..s...s..s.",
                ".w...w...w.",
                ".w...w...w.",
                ".w...w...w.",
                "nnn.nnn.nnn",
                ".n...n...n.",
                ".nnnnnnnnn.",
                ".....n.....",
                ".....n.....",
                ".....n.....",
                ".....n.....",
                ".....n.....",
                ".....n.....",
                "....nnn....",
                "...nnnnn...",
                "..nnnnnnn..",
            };
            var cold = new PixelPart("cold", P(5.5f, 0.5f), m, 0, stand(false));
            var lit = new PixelPart("lit", P(5.5f, 0.5f), m, 0, stand(true));
            var idle = new Clip("idle", 3f, true);
            idle.Frames.Add(new Frame().Add(cold, P(32f, 10f), 0f, 4));
            var on = new Clip("walk", 8f, true);
            foreach (int g in new[] { 0, 1, 2, 1 }) on.Frames.Add(new Frame().Add(lit, P(32f, 10f), 0f, 4, g));
            var hurt = new Clip("hurt", 12f, false);
            hurt.Frames.Add(new Frame().Add(cold, P(32.5f, 10f), 0f, 4, 1));
            var bit = new PixelPart("bit", P(1f, 0.5f), m, 0, "nn", "n.");
            var death = Shatter(new[] { bit, bit, bit, bit }, P(32f, 16f), 4);
            return new List<Clip> { idle, on, Same("windup", idle), Same("strike", idle), hurt, death };
        }
    }
}
