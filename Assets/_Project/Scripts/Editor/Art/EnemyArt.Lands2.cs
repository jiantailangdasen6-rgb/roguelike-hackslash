using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Monsters of the fungal grotto and the cursed library. The sporeling, the great cap, the living tome and the
    /// bookworm have bodies of their own (seen from the side, facing right); the rest are older frames in the lands'
    /// colours: fungus-threaded ghouls, moss shamans, the Mycelium Mother (the bishop's frame), the Rot Giant (the
    /// king's), ink wraiths, scribes, the Archivist (the lich's) and the Ink Beast (the wolf's).
    /// </summary>
    public static partial class EnemyArt
    {
        private static readonly PixelMaterial SporeCap = R("#2a0f14", "#4a1a20", "#6e2a2a", "#94402f", "#b85e3c");
        private static readonly PixelMaterial PurpleCap = R("#1e1028", "#341a42", "#4e2a5e", "#6e3e7a", "#925a98");
        private static readonly PixelMaterial PaleStalk = R("#3a3428", "#5a503e", "#7e7258", "#a49574", "#c8b994");
        private static readonly PixelMaterial FungusFlesh = R("#22261a", "#3a4028", "#565e3c", "#767e56", "#9aa274");
        private static readonly PixelMaterial MossRobe = R("#0c140a", "#162210", "#203418", "#2c4821", "#3c602c");
        private static readonly PixelMaterial Bark = R("#120c08", "#22170f", "#342418", "#4a3422", "#624630");
        private static readonly PixelMaterial RotFlesh = R("#1c2016", "#323a26", "#4c5638", "#68744c", "#8a9866");
        private static readonly PixelMaterial SporeGlow = new("#a8ff70");
        private static readonly PixelMaterial InkCloth = R("#06050c", "#0e0b1a", "#181430", "#241e46", "#342c62");
        private static readonly PixelMaterial NavyRobe = R("#0a0c1c", "#141a32", "#1e284c", "#2c3a68", "#405288");
        private static readonly PixelMaterial Parchment = R("#3a3428", "#6a604a", "#9a8e70", "#c4b896", "#e8dcbc");
        private static readonly PixelMaterial VioletGlow = new("#c4a8ff");
        private static readonly PixelMaterial BookLeather = R("#1a0a0c", "#341418", "#521e22", "#74302c", "#984838");
        private static readonly PixelMaterial Silverfish = R("#2a2e38", "#4a5260", "#727c8c", "#a0aab8", "#d0d8e4");

        // ------------------------------------------------------------------ Fungal grotto

        public static List<Clip> Sporeling() => Toadstool(SporeCap, big: false);

        public static List<Clip> GreatCap() => Toadstool(PurpleCap, big: true);

        /// <summary>A walking toadstool: a spotted cap over a squat stalk with a face, two stubby feet; it hops.</summary>
        private static List<Clip> Toadstool(PixelMaterial capColour, bool big)
        {
            var m = Palette.With(('c', capColour), ('s', PaleStalk), ('i', new PixelMaterial("#ecdcc0")), ('e', SporeGlow), ('g', SporeGlow));
            var cap = big
                ? new PixelPart("cap", P(11f, 0.5f), m, 0,
                    "......cccccccccc......",
                    "....ccccicccccciccc...",
                    "..cccccccccicccccccc..",
                    ".cciccccccccccccicccc.",
                    "cccccccicccccccccccccc",
                    "ccccccccccccciccccccic",
                    "cccciccccccccccccccccc",
                    "gggggggggggggggggggggg")
                : new PixelPart("cap", P(7f, 0.5f), m, 0,
                    "....cccccc....",
                    "..cccicccccc..",
                    ".cccccccccicc.",
                    "ccicccccccccc.",
                    "cccccccicccccc",
                    "gggggggggggggg");
            var stalk = big
                ? new PixelPart("stalk", P(5f, 0.5f), m, 0,
                    "..ssssss..",
                    ".ssssssss.",
                    ".sssseses.",
                    ".sssssssss",
                    "ssssskkss.",
                    "sssssssss.",
                    "ssssssssss",
                    ".ssssssss.",
                    ".ssssssss.")
                : new PixelPart("stalk", P(3.5f, 0.5f), m, 0,
                    ".ssss.",
                    "sssese",
                    "ssssss",
                    "sssks.",
                    "ssssss",
                    ".ssss.");
            var foot = new PixelPart("foot", P(1f, 0.5f), m, 0, big ? "sss" : "ss", big ? "sss" : "ss");
            float u = big ? 1.5f : 1f;

            Frame Pose(float hop, float squash, float tilt, float lean = 0f, float fall = 0f)
            {
                var f = new Frame { BodyAngle = tilt, BodyPivot = P(32f, 10f), BodyOffset = P(lean, -fall) };
                Vector2 b = P(32f, 10f + hop);
                f.Add(foot, b + P(-3f * u, -1f - squash * 0.3f), 0f, 2);
                f.Add(foot, b + P(2f * u, -1f - squash * 0.3f), 0f, 3);
                f.Add(stalk, b, 0f, 4);
                f.Add(cap, b + P(0f, (big ? 8.5f : 5.5f) - squash), tilt * 0.3f, 6);
                return f;
            }

            var idle = new Clip("idle", 6f, true);
            for (int i = 0; i < 4; i++) idle.Frames.Add(Pose(0f, i % 2 == 0 ? 0f : 1f, 0f));
            var walk = new Clip("walk", 10f, true);
            foreach (var (hop, squash) in new[] { (0f, 1f), (2f, 0f), (4f, -0.5f), (3f, 0f), (1f, 0.5f), (0f, 1.5f) })
                walk.Frames.Add(Pose(hop * u, squash, 0f, 0f));
            var windup = new Clip("windup", 9f, false);
            windup.Frames.Add(Pose(0f, 1.5f, 6f));
            windup.Frames.Add(Pose(0f, 2.5f, 10f));
            var strike = new Clip("strike", 14f, false);
            strike.Frames.Add(Pose(3f * u, -1f, -18f, 3f));
            strike.Frames.Add(Pose(2f * u, 0f, -12f, 4f));
            strike.Frames.Add(Pose(0f, 1f, 0f, 2f));
            var hurt = new Clip("hurt", 10f, false);
            hurt.Frames.Add(Pose(0f, 2f, 14f));
            hurt.Frames.Add(Pose(0f, 1f, 6f));
            var death = new Clip("death", 10f, false);
            for (int i = 0; i < 6; i++) death.Frames.Add(Pose(0f, Mathf.Min(4f, i * 0.8f), 20f + i * 12f, 0f, Mathf.Min(3f, i * 0.6f)));
            return new List<Clip> { idle, walk, windup, strike, hurt, death };
        }

        public static List<Clip> Mycelid() => Swapped(Ghoul,
            (Palette.Flesh, FungusFlesh), (Palette.Rag, MossRobe), (Palette.CultRobe, MossRobe), (Palette.EyeRed, SporeGlow), (Palette.Skin, FungusFlesh));

        public static List<Clip> MossShaman() => Swapped(Necromancer,
            (Palette.Robe, MossRobe), (Palette.Grave, MossRobe), (Palette.Hood, Bark), (Palette.Soul, SporeGlow), (Palette.Violet, MossRobe), (Palette.Gold, PaleStalk));

        public static List<Clip> MyceliumMother() => Swapped(BloodBishop,
            (Palette.Crimson, R("#1c2416", "#2e3c24", "#465836", "#62784c", "#869c6a")), (Palette.Red, SporeCap), (Palette.CultRobe, MossRobe), (Palette.Gold, PaleStalk), (Palette.Gem, SporeGlow),
            (Palette.EyeRed, SporeGlow), (Palette.Skin, FungusFlesh), (Palette.Pallid, FungusFlesh));

        public static List<Clip> RotGiant() => Swapped(BoneKing,
            (Palette.Bone, RotFlesh), (Palette.Steel, Bark), (Palette.DarkSteel, Bark), (Palette.Gold, MossRobe), (Palette.Red, MossRobe),
            (Palette.Rag, MossRobe), (Palette.Blade, PaleStalk), (Palette.EyeRed, SporeGlow), (Palette.Leather, Bark), (Palette.Rust, SporeCap));

        // ------------------------------------------------------------------ Cursed library

        public static List<Clip> InkWraith() => Swapped(Wraith, (Palette.Ghost, InkCloth), (Palette.Rag, NavyRobe), (Palette.Soul, VioletGlow), (Palette.Bone, Parchment));

        public static List<Clip> Scribe() => Swapped(Cultist,
            (Palette.CultRobe, NavyRobe), (Palette.Robe, NavyRobe), (Palette.Red, Palette.Gold), (Palette.EyeRed, VioletGlow), (Palette.Skin, Palette.Pallid));

        public static List<Clip> Archivist() => Swapped(Lich,
            (Palette.Robe, NavyRobe), (Palette.Violet, InkCloth), (Palette.Bone, Parchment), (Palette.Soul, VioletGlow), (Palette.SoulCore, VioletGlow),
            (Palette.Rag, InkCloth), (Palette.Red, BookLeather), (Palette.Crimson, BookLeather), (Palette.EyeRed, VioletGlow));

        public static List<Clip> InkBeast() => Swapped(FrostWolf,
            (R("#1a1e26", "#323a48", "#4e5a6c", "#76849a", "#a8b6c8"), R("#0e0c1e", "#1c1838", "#2e2856", "#443c78", "#5e549c")), (R("#3a4456", "#5a6880", "#7e8ea6", "#a8b8cc", "#dce6f2"), R("#241e44", "#3a3266", "#564c8c", "#7a6eb4", "#a498dc")));

        /// <summary>A book flying on its own covers: the covers beat like wings, the pages flutter between them.</summary>
        public static List<Clip> LivingTome()
        {
            var m = Palette.With(('l', BookLeather), ('p', Parchment), ('g', Palette.Gold), ('e', VioletGlow));
            var cover = new PixelPart("cover", P(0.5f, 0.5f), m, 0,
                "llllllllll",
                "lgllllllgl",
                "llllelllll",
                "llllllllll",
                "lgllllllgl",
                "llllllllll");
            var pages = new PixelPart("pages", P(1f, 3f), m, 0,
                ".pppppppp.",
                "pppppppppp",
                "pp.pp.pppp",
                "pppppppppp",
                "ppp.pp.ppp",
                "pppppppppp",
                ".pppppppp.");
            var spine = new PixelPart("spine", P(1f, 3f), m, 0, "ll", "lg", "ll", "ll", "lg", "ll", "ll");

            Frame Pose(float flap, float bob, float tilt = 0f, float fall = 0f, float lunge = 0f)
            {
                var f = new Frame { BodyAngle = tilt, BodyPivot = P(32f, 20f), BodyOffset = P(lunge, -fall) };
                Vector2 s = P(28f, 20f + bob);
                f.Add(cover, s, 180f - flap, 2);   // the far cover, opened back
                f.Add(pages, s + P(1f, 0f), 0f, 4);
                f.Add(spine, s, 0f, 5);
                f.Add(cover, s + P(0f, 1f), flap, 6); // the near cover
                return f;
            }
            Clip Fly(string name, float fps, float amp)
            {
                var clip = new Clip(name, fps, true);
                for (int i = 0; i < 6; i++)
                {
                    float k = Mathf.Sin(i / 6f * Mathf.PI * 2f);
                    clip.Frames.Add(Pose(30f + amp * k, Mathf.Round(k * 1.2f)));
                }
                return clip;
            }
            var windup = new Clip("windup", 9f, false);
            windup.Frames.Add(Pose(80f, 1f, 6f));
            windup.Frames.Add(Pose(95f, 1f, 8f));
            var strike = new Clip("strike", 14f, false);
            strike.Frames.Add(Pose(10f, 0f, -10f, 0f, 2f));
            strike.Frames.Add(Pose(25f, 0f, -4f, 0f, 1f));
            var hurt = new Clip("hurt", 10f, false);
            hurt.Frames.Add(Pose(70f, 0f, 16f));
            hurt.Frames.Add(Pose(45f, 0f, 6f));
            var death = new Clip("death", 10f, false);
            for (int i = 0; i < 6; i++) death.Frames.Add(Pose(5f + i * 4f, 0f, i * 30f, i * 2.5f));
            return new List<Clip> { Fly("idle", 8f, 18f), Fly("walk", 12f, 28f), windup, strike, hurt, death };
        }

        /// <summary>A silverfish grown huge on old paper: plated segments in a wave, feelers in front, three bristles behind.</summary>
        public static List<Clip> Bookworm()
        {
            var m = Palette.With(('s', Silverfish), ('e', VioletGlow), ('k', Palette.Black));
            var head = new PixelPart("head", P(1.5f, 1.5f), m, 0,
                "..ssss..",
                ".ssssss.",
                "sssssse.",
                "ssssssss",
                ".ssssss.",
                "..s..s..");
            var seg = new PixelPart("segment", P(4.5f, 1.5f), m, 0,
                "..SSSSS..",
                ".SsssssS.",
                "sssssssss",
                "sssssssss",
                ".sssssss.",
                "..s...s..");
            var feeler = new PixelPart("feeler", P(0.5f, 0.5f), m, 0, "s.......", ".ss.....", "...sss..", "......ss");
            var bristle = new PixelPart("bristle", P(7.5f, 0.5f), m, 0, "ss......", "..sss...", ".....sss");

            Frame Pose(float phase, float wave, float lift = 0f, float tilt = 0f, float fall = 0f, float lunge = 0f)
            {
                var f = new Frame { BodyAngle = tilt, BodyPivot = P(30f, 12f), BodyOffset = P(lunge, -fall) };
                const int n = 5;
                for (int k = n - 1; k >= 0; k--)
                {
                    float x = 14f + k * 6f, y = 12f + Mathf.Sin(phase + k * 1.1f) * wave + (k == n - 1 ? lift : 0f);
                    if (k == n - 1)
                    {
                        f.Add(head, P(x - 1f, y), -lift * 3f, 6);
                        f.Add(feeler, P(x + 7f, y + 3f), 20f - lift * 4f, 7);
                        f.Add(feeler, P(x + 6f, y + 2f), 5f - lift * 3f, 3);
                    }
                    else f.Add(seg, P(x, y), 0f, 4 + (k % 2));
                }
                f.Add(bristle, P(11f, 13f + Mathf.Sin(phase) * wave), 15f, 3);
                f.Add(bristle, P(11f, 12f + Mathf.Sin(phase) * wave), -10f, 3);
                return f;
            }
            Clip Crawl(string name, float fps, float wave)
            {
                var clip = new Clip(name, fps, true);
                for (int i = 0; i < 6; i++) clip.Frames.Add(Pose(i / 6f * Mathf.PI * 2f, wave));
                return clip;
            }
            var windup = new Clip("windup", 9f, false);
            windup.Frames.Add(Pose(0f, 1f, 3f));
            windup.Frames.Add(Pose(0.5f, 1f, 5f));
            var strike = new Clip("strike", 14f, false);
            strike.Frames.Add(Pose(1f, 0.5f, -2f, 0f, 0f, 4f));
            strike.Frames.Add(Pose(1.5f, 0.5f, -1f, 0f, 0f, 3f));
            strike.Frames.Add(Pose(2f, 1f, 0f, 0f, 0f, 1f));
            var hurt = new Clip("hurt", 10f, false);
            hurt.Frames.Add(Pose(0f, 2f, 2f, 8f));
            hurt.Frames.Add(Pose(0f, 1f, 1f, 3f));
            var death = new Clip("death", 10f, false);
            for (int i = 0; i < 6; i++) death.Frames.Add(Pose(i * 0.6f, Mathf.Max(0f, 2f - i * 0.4f), 0f, i * 6f, Mathf.Min(2f, i * 0.5f)));
            return new List<Clip> { Crawl("idle", 6f, 0.6f), Crawl("walk", 12f, 1.6f), windup, strike, hurt, death };
        }
    }
}
