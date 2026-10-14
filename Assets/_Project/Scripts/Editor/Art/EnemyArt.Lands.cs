using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Monsters of the new lands. The snow wolf and the carrion crab have bodies of their own; the others are drawn by
    /// an existing generator in the land's colours (PixelMaterial.Swaps): the drowned are bloated teal ghouls, the tide
    /// witch a necromancer of the deep, the saint a veiled bishop of the water, the frozen dead a knight in ice, the
    /// frost witch and queen in pale blue and silver, the forge's iron guards, imps of fire, slag bloaters and the
    /// colossus in charred iron and embers.
    /// </summary>
    public static partial class EnemyArt
    {
        private static PixelMaterial R(params string[] hex) => new(2, hex);

        // Land materials.
        private static readonly PixelMaterial DrownedFlesh = R("#0f1f1b", "#1c352f", "#2c4f46", "#446c60", "#678f80");
        private static readonly PixelMaterial Kelp = R("#0a160f", "#132a1a", "#1f4228", "#2e5c36", "#467a48");
        private static readonly PixelMaterial TideRobe = R("#08161a", "#0f2830", "#173e48", "#22586a", "#347a8e");
        private static readonly PixelMaterial PaleSilver = R("#2a2e34", "#4e565e", "#7a848c", "#a8b2b8", "#dde4e8");
        private static readonly PixelMaterial SaintVeil = R("#14282a", "#24444a", "#3a6466", "#5a8a88", "#8cbcb6");
        private static readonly PixelMaterial IceArmor = R("#1a2a40", "#2c4462", "#46688c", "#6e94bc", "#a8ccec");
        private static readonly PixelMaterial FrostRobe = R("#141c2c", "#24324c", "#3a4e72", "#5a729c", "#8aa4c8");
        private static readonly PixelMaterial QueenRobe = R("#1c2638", "#34445e", "#56688a", "#8094b4", "#b8c8dc");
        private static readonly PixelMaterial IcyBone = R("#2a3440", "#4e5e6e", "#7a8ea0", "#a8c0d4", "#dcecf8");
        private static readonly PixelMaterial IronDark = R("#121214", "#222226", "#36363c", "#52525a", "#74747e");
        private static readonly PixelMaterial Soot = R("#0c0908", "#171210", "#221a16", "#30251e", "#403228");
        private static readonly PixelMaterial Brass = R("#3a1a08", "#6a3210", "#a35018", "#d8782a", "#ffb347");
        private static readonly PixelMaterial FireSkin = R("#2a0c06", "#5a1a0a", "#8a2e10", "#c24a14", "#ff7a1f");
        private static readonly PixelMaterial Molten = R("#3a1206", "#6a2208", "#a33a0c", "#e05a14", "#ffb347");
        private static readonly PixelMaterial Charcoal = R("#120c0a", "#221812", "#342418", "#4a3422", "#64482e");
        private static readonly PixelMaterial AquaGlow = new("#7ff0d8");
        private static readonly PixelMaterial ColdGlow = new("#a8e4ff");
        private static readonly PixelMaterial EmberGlow = new("#ffb347");

        /// <summary>Runs a generator with some materials replaced (by their darkest colour).</summary>
        private static List<Clip> Swapped(Func<List<Clip>> make, params (PixelMaterial From, PixelMaterial To)[] swaps)
        {
            var map = new Dictionary<int, PixelMaterial>();
            foreach (var (from, to) in swaps) map[PixelMaterial.SwapKey(from)] = to;
            PixelMaterial.Swaps = map;
            try { return make(); }
            finally { PixelMaterial.Swaps = null; }
        }

        private static readonly PixelMaterial KnightArmor = new(2, "#16181e", "#2c3038", "#4a505c", "#6e7684", "#9aa2b0");
        private static readonly PixelMaterial ImpSkin = new(2, "#1f2a14", "#36491f", "#506b2b", "#6f8f3b", "#94b555");

        // ------------------------------------------------------------------ Sunken catacombs

        public static List<Clip> Drowned() => Swapped(Ghoul,
            (Palette.Flesh, DrownedFlesh), (Palette.Rag, Kelp), (Palette.CultRobe, Kelp), (Palette.EyeRed, AquaGlow), (Palette.Skin, DrownedFlesh));

        public static List<Clip> TideWitch() => Swapped(Necromancer,
            (Palette.Robe, TideRobe), (Palette.Grave, TideRobe), (Palette.Hood, Kelp), (Palette.Soul, AquaGlow), (Palette.Violet, TideRobe), (Palette.Gold, PaleSilver));

        public static List<Clip> DrownedSaint() => Swapped(BloodBishop,
            (Palette.Crimson, SaintVeil), (Palette.Red, TideRobe), (Palette.CultRobe, TideRobe), (Palette.Gold, PaleSilver), (Palette.Gem, AquaGlow),
            (Palette.EyeRed, AquaGlow), (Palette.Skin, DrownedFlesh), (Palette.Pallid, DrownedFlesh));

        /// <summary>
        /// A crab the size of a dog, seen from the side facing right: a domed shell with barnacles, eyes on stalks,
        /// a great claw raised in front and a smaller one behind it, three pairs of jointed legs beneath.
        /// </summary>
        public static List<Clip> CarrionCrab()
        {
            var m = Palette.With(
                ('@', R("#140e0c", "#2c1c16", "#4a3022", "#6e4a32", "#94684a")),
                ('&', R("#1c1410", "#3a2a1e", "#5e442e", "#866248", "#b08a66")));
            var shell = new PixelPart("shell", P(10f, 1f), m, 0,
                "......@@@@@@@.......",
                "....@@@@@@@@@@@.....",
                "..@@@@&@@@@@&@@@@...",
                ".@@@@@@@@@@@@@@@@@@.",
                "@@@&@@@@@@@@@@@@&@@@",
                "@@@@@@@@@@@@@@@@@@@@",
                ".@@@@@@@@@@@@@@@@@@.",
                "..&&&&&&&&&&&&&&&&..");
            var stalk = new PixelPart("stalk", P(0.5f, 0.5f), m, 0, "ke", ".&", ".&");
            // The big claw: an arm then pincers opening forward (to the right).
            var claw = new PixelPart("claw", P(1f, 1f), m, 0,
                "......&&&&.",
                "....&&&&&&&",
                "...&&&&....",
                "..&&&&&&&&.",
                ".&&&&&&&&&&",
                "&&&&.......",
                "&&&........");
            var small = new PixelPart("small", P(1f, 1f), m, 0, "...&&&", "..&&&.", ".&&&&&", "&&....");
            var leg = new PixelPart("leg", P(1f, 7f), m, 0, "&&.", ".&.", ".&&", "..&", "..&", "..&", ".&&", ".&.");

            Frame Pose(float bob, float clawUp, float legs, float tilt = 0f, float fall = 0f, float lunge = 0f)
            {
                var f = new Frame { BodyAngle = tilt, BodyPivot = P(32f, 16f), BodyOffset = P(lunge, -fall) };
                Vector2 c = P(31f, 17f + bob); // the shell's underside, left of its middle
                for (int k = 0; k < 3; k++)
                {
                    float swing = (k % 2 == 0 ? legs : -legs);
                    f.Add(leg, c + P(-6f + k * 5f, 1f), -25f + swing, 2, -1);      // far legs
                    f.Add(leg, c + P(-4f + k * 5f, 0f), 20f - swing, 7);           // near legs
                }
                f.Add(small, c + P(7f, 4f), 20f + clawUp * 0.5f, 3, -1);
                f.Add(shell, c, 0f, 5);
                f.Add(stalk, c + P(7f, 8f), 0f, 4);
                f.Add(stalk, c + P(9f, 8f), 0f, 6);
                f.Add(claw, c + P(9f, 2f), clawUp, 8);
                return f;
            }

            Clip Loop(string name, float fps, float legAmp, float clawAmp)
            {
                var clip = new Clip(name, fps, true);
                for (int i = 0; i < 6; i++)
                {
                    float s = Mathf.Sin(i / 6f * Mathf.PI * 2f);
                    clip.Frames.Add(Pose(Mathf.Round(s * 0.6f), 10f + clawAmp * s, legAmp * s));
                }
                return clip;
            }

            var windup = new Clip("windup", 9f, false);
            windup.Frames.Add(Pose(-1f, 40f, 0f));
            windup.Frames.Add(Pose(-2f, 60f, 0f));
            windup.Frames.Add(Pose(-2f, 65f, 0f));
            var strike = new Clip("strike", 14f, false);
            strike.Frames.Add(Pose(1f, -20f, 20f, -8f, 0f, 3f));
            strike.Frames.Add(Pose(1f, -30f, -20f, -10f, 0f, 4f));
            strike.Frames.Add(Pose(0f, 0f, 0f, -3f, 0f, 2f));
            var hurt = new Clip("hurt", 10f, false);
            hurt.Frames.Add(Pose(1f, 35f, 25f, 10f));
            hurt.Frames.Add(Pose(0f, 20f, 0f, 4f));
            var death = new Clip("death", 10f, false);
            for (int i = 0; i < 6; i++) death.Frames.Add(Pose(0f, 40f + i * 12f, 30f, i * 36f, Mathf.Min(5f, i * 1.2f)));
            return new List<Clip> { Loop("idle", 6f, 4f, 8f), Loop("walk", 14f, 25f, 6f), windup, strike, hurt, death };
        }

        // ------------------------------------------------------------------ Frozen mausoleum

        public static List<Clip> Icebound() => Swapped(DreadKnight,
            (KnightArmor, IceArmor), (Palette.DarkSteel, IceArmor), (Palette.Steel, IceArmor), (Palette.Grave, FrostRobe), (Palette.Rust, IceArmor),
            (Palette.Red, FrostRobe), (Palette.EyeRed, ColdGlow), (Palette.Leather, FrostRobe));

        public static List<Clip> FrostWitch() => Swapped(Cultist,
            (Palette.CultRobe, FrostRobe), (Palette.Robe, FrostRobe), (Palette.Red, FrostRobe), (Palette.EyeRed, ColdGlow), (Palette.Skin, IcyBone), (Palette.Gold, PaleSilver));

        public static List<Clip> FrostQueen() => Swapped(Lich,
            (Palette.Robe, QueenRobe), (Palette.Violet, QueenRobe), (Palette.Gold, PaleSilver), (Palette.Bone, IcyBone), (Palette.Soul, ColdGlow),
            (Palette.Rag, FrostRobe), (Palette.Red, FrostRobe), (Palette.Crimson, FrostRobe), (Palette.EyeRed, ColdGlow));
        /// <summary>
        /// A rangy grey wolf with a frosted ruff and pale blue eyes: long legs, a long muzzle and pricked ears,
        /// a bushy tail; runs at a gallop and lunges.
        /// </summary>
        public static List<Clip> FrostWolf()
        {
            var m = Palette.With(
                ('@', R("#1a1e26", "#323a48", "#4e5a6c", "#76849a", "#a8b6c8")),
                ('&', R("#3a4456", "#5a6880", "#7e8ea6", "#a8b8cc", "#dce6f2")));
            var body = new PixelPart("body", P(5f, 2f), m, 0,
                "......&&&&&&&&&&&...",
                "...&&&&&&&&&&&&&&&&.",
                ".@@@&&&&&&&&&&&&&&&&",
                "@@@@@@@@@@@@@@@@@&&&",
                "@@@@@@@@@@@@@@@@@@&&",
                ".@@@@@@@@@@@@@@@@@&.",
                "..@@@@@.....@@@@@@..",
                "...@@.........@@....");
            var head = new PixelPart("head", P(2f, 3f), m, 0,
                "..&...&.......",
                "..&&.&&.......",
                ".&&&&&&&......",
                "&&&&&&&&&&....",
                "@@@@f@@@@@@@@.",
                "@@@@@@@@@@@@@k",
                ".@@@@@@@@@@@..",
                "..@@@@b@b.....",
                "...@@.........");
            var leg = new PixelPart("leg", P(1f, 9.5f), m, 0, "@@@", "@@@", ".@@", ".@@", ".@.", ".@.", ".@.", ".@.", ".@.", "k@@");
            var tail = new PixelPart("tail", P(10.5f, 2f), m, 0,
                "&&&&........",
                ".&&&&&&.....",
                "..@@@@@@@@@.",
                "....@@@@@@@@",
                ".......@@@..");

            Frame Pose(float bob, float front, float back, float tilt = 0f, float nod = 0f, float wag = 0f, float fall = 0f, float shift = 0f)
            {
                var f = new Frame { BodyAngle = tilt, BodyPivot = P(32f, 18f), BodyOffset = P(shift, -fall) };
                Vector2 hip = P(24f, 20f + bob);
                f.Add(leg, hip + P(1f, 0f), back, 2, -1);
                f.Add(leg, hip + P(-1f, 0f), -back, 3);
                f.Add(tail, hip + P(-4f, 3f), 15f + wag, 1);
                f.Add(body, hip, 0f, 5);
                f.Add(leg, hip + P(13f, 0f), -front, 2, -1);
                f.Add(leg, hip + P(11f, 0f), front, 4);
                f.Add(head, hip + P(15f, 5f), nod, 6);
                return f;
            }

            Clip Run(string name, float fps, float amp)
            {
                var clip = new Clip(name, fps, true);
                for (int i = 0; i < 6; i++)
                {
                    float s = Mathf.Sin(i / 6f * Mathf.PI * 2f);
                    clip.Frames.Add(Pose(Mathf.Round(Mathf.Abs(s)), amp * s, amp * s, 0f, -3f * s, 10f * s));
                }
                return clip;
            }

            var idle = new Clip("idle", 5f, true);
            for (int i = 0; i < 4; i++) idle.Frames.Add(Pose(i % 2 == 0 ? 0f : -1f, 0f, 0f, 0f, i % 2 == 0 ? 0f : -4f, i * 5f));
            var windup = new Clip("windup", 10f, false);
            windup.Frames.Add(Pose(-1f, 15f, -20f, 6f, -10f));
            windup.Frames.Add(Pose(-2f, 20f, -30f, 9f, -14f));
            windup.Frames.Add(Pose(-2f, 22f, -32f, 10f, -15f));
            var strike = new Clip("strike", 14f, false);
            strike.Frames.Add(Pose(2f, -55f, 45f, -12f, 10f, -20f, 0f, 2f));
            strike.Frames.Add(Pose(2f, -60f, 50f, -14f, 12f, -25f, 0f, 3f));
            strike.Frames.Add(Pose(0f, -20f, 20f, -4f, 4f, 0f, 0f, 1f));
            var hurt = new Clip("hurt", 10f, false);
            hurt.Frames.Add(Pose(1f, 20f, -10f, 14f, 20f));
            hurt.Frames.Add(Pose(0f, 8f, -4f, 6f, 8f));
            // Falls over onto its side.
            var death = new Clip("death", 10f, false);
            for (int i = 0; i < 6; i++) death.Frames.Add(Pose(0f, 30f + i * 8f, -30f - i * 8f, -i * 16f, 20f, -10f, Mathf.Min(6f, i * 1.2f)));
            return new List<Clip> { idle, Run("walk", 14f, 34f), windup, strike, hurt, death };
        }

        // ------------------------------------------------------------------ Burnt forge

        public static List<Clip> IronSentinel() => Swapped(DreadKnight,
            (KnightArmor, IronDark), (Palette.DarkSteel, IronDark), (Palette.Steel, IronDark), (Palette.Grave, Soot), (Palette.Rust, Brass),
            (Palette.Red, Soot), (Palette.EyeRed, EmberGlow), (Palette.Leather, Soot));

        public static List<Clip> FireImp() => Swapped(GimmickArt.TreasureImp,
            (ImpSkin, FireSkin), (Palette.Flesh, FireSkin), (Palette.Gold, Molten), (Palette.Soul, EmberGlow), (Palette.EyeRed, EmberGlow), (Palette.Rag, Soot), (Palette.Leather, Soot));

        public static List<Clip> SlagBloater() => Swapped(Bloater,
            (Palette.Pustule, Molten), (Palette.Flesh, Charcoal), (KnightArmor, IronDark), (Palette.DarkSteel, IronDark), (Palette.Toxic, EmberGlow), (Palette.Rag, Soot));

        // ------------------------------------------------------------------ Second lords (palette changes of other frames, drawn larger)

        private static readonly PixelMaterial ReaperRobe = R("#08070a", "#121016", "#1c1922", "#292431", "#3a3445");
        private static readonly PixelMaterial PaleGreen = new("#b8ffb0");
        private static readonly PixelMaterial ChoirRobe = R("#3a3530", "#6b645a", "#a49c8e", "#d4ccbc", "#f4efe2");
        private static readonly PixelMaterial VoidLid = R("#07040b", "#110a1a", "#1d1230", "#2c1c48", "#3e2a63");
        private static readonly PixelMaterial CrabShell = R("#1c0806", "#3a120c", "#5e1e12", "#86301c", "#b04a2a");
        private static readonly PixelMaterial CrabClaw = R("#24100a", "#4a2214", "#723a22", "#9a5634", "#c87a4c");
        private static readonly PixelMaterial WolfFur = R("#3a4250", "#6a7486", "#9ea8ba", "#cad2de", "#eef2f8");
        private static readonly PixelMaterial WolfRuff = R("#5a6478", "#8a96aa", "#bcc6d6", "#e2e8f0", "#ffffff");
        private static readonly PixelMaterial BlackIron = R("#09090b", "#141418", "#202026", "#2e2e36", "#42424c");

        public static List<Clip> GraveReaper() => Swapped(Necromancer,
            (Palette.Robe, ReaperRobe), (Palette.Grave, ReaperRobe), (Palette.Hood, ReaperRobe), (Palette.Soul, PaleGreen), (Palette.Violet, ReaperRobe), (Palette.Gold, Palette.Bone));

        public static List<Clip> ChoirMistress() => Swapped(BloodBishop,
            (Palette.Crimson, ChoirRobe), (Palette.Red, Palette.Gold), (Palette.CultRobe, ChoirRobe), (Palette.Gem, PaleSilver), (Palette.EyeRed, EmberGlow));

        public static List<Clip> VoidEye() => Swapped(AbyssEye, (Palette.Violet, VoidLid), (Palette.Ivory, R("#2a2418", "#4e4430", "#7a6c4a", "#a8966a", "#d4c08e")));

        public static List<Clip> GreatCrab() => Swapped(CarrionCrab,
            (R("#140e0c", "#2c1c16", "#4a3022", "#6e4a32", "#94684a"), CrabShell), (R("#1c1410", "#3a2a1e", "#5e442e", "#866248", "#b08a66"), CrabClaw));

        public static List<Clip> GreatWolf() => Swapped(FrostWolf,
            (R("#1a1e26", "#323a48", "#4e5a6c", "#76849a", "#a8b6c8"), WolfFur), (R("#3a4456", "#5a6880", "#7e8ea6", "#a8b8cc", "#dce6f2"), WolfRuff));

        public static List<Clip> Executioner() => Swapped(DreadKnight,
            (KnightArmor, BlackIron), (Palette.DarkSteel, BlackIron), (Palette.Steel, BlackIron), (Palette.Grave, Palette.Crimson), (Palette.Leather, Palette.Crimson));

        /// <summary>The Ashen King: the Lich's frame in ash and embers, a crown of gold gone to cinders.</summary>
        public static List<Clip> AshenKing() => Swapped(Lich,
            (Palette.Robe, Soot), (Palette.Violet, Charcoal), (Palette.Gold, Brass), (Palette.Bone, Charcoal), (Palette.Soul, EmberGlow),
            (Palette.SoulCore, EmberGlow), (Palette.Rag, Soot), (Palette.Red, Molten), (Palette.Crimson, Molten), (Palette.EyeRed, EmberGlow));

        public static List<Clip> ForgeColossus() => Swapped(BoneKing,
            (Palette.Bone, Charcoal), (Palette.Steel, IronDark), (Palette.DarkSteel, IronDark), (Palette.Gold, Brass), (Palette.Red, Soot),
            (Palette.Rag, Soot), (Palette.Blade, Molten), (Palette.EyeRed, EmberGlow), (Palette.Leather, Soot), (Palette.Rust, Brass));
    }
}
