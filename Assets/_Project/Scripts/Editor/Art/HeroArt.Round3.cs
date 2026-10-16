using System.Collections.Generic;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// The classes of round 5: the deathcaller (deep hood, bone mask, a skull-topped staff burning with green soul
    /// fire) and the paladin (bright plate, a blue tabard with a gold cross, a crested helm, a holy sword and a cape).
    /// Same clip order as the others.
    /// </summary>
    public static partial class HeroArt
    {
        // ------------------------------------------------------------------ Deathcaller

        /// <summary>Base look: black-green robe and hood, bone mask, green soul fire. Skins repaint r / h / f.</summary>
        public static Dictionary<char, PixelMaterial> DeathcallerBase() => Palette.With(
            ('r', Palette.Ramp(2, "#0d1411", "#18241e", "#24382e", "#33503f", "#466a54")),
            ('h', Palette.Ramp(2, "#060807", "#0e1411", "#18221d", "#22302a", "#2e4038")),
            ('f', new PixelMaterial("#7dff9a")), ('F', new PixelMaterial("#e0ffe6")));

        public static List<Clip> Deathcaller(Dictionary<char, PixelMaterial> palette = null)
        {
            var m = palette ?? DeathcallerBase();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("hood", P(5.5f, 0.5f), m, 0,
                    "...hhhhh...",
                    "..hhhhhhh..",
                    ".hhhhhhhhh.",
                    ".hhhhbbbbh.",
                    "hhhhbbbbbbh",
                    "hhhhbkbbkbh",
                    "hhhhbfbbfbh",
                    "hhhhbbbbbbh",
                    ".hhhhbkkkbh",
                    ".hhhhhbbbh.",
                    "..hhhhhhh..",
                    "...hhhhh..."),
                Torso = new PixelPart("robe", P(6f, 7.5f), m, 0,
                    "...rrrrrr...",
                    "..rrrrrrrrr.",
                    ".rrrrbrrrrrr",
                    ".rrrbbbrrrr.",
                    ".rrrrbrrrrr.",
                    "..rrbbbrrr..",
                    "..llllflll..",
                    "..rrrrrrrrr.",
                    "..rrrrrrrrr.",
                    ".rrrrrrrrrr.",
                    ".rrhrrrrhrrr",
                    ".rrhrrrrhrrr",
                    "rrhrrrrrrhrr",
                    "rrr.rrr.rrr."),
                LegFront = new PixelPart("boot", P(2f, 9.5f), m, 0, ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".kk.", ".kk.", "kkkk", "kkkk"),
                LegBack = new PixelPart("boot", P(2f, 9.5f), m, 0, ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".kk.", ".kk.", "kkkk", "kkkk"),
                ArmFront = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".rrr.", "rrrrr", "rrrrr", "rrrr.", "rrrr.", ".rrr.", ".rrr.", "rrrrr", ".bbb.", ".b.b."),
                ArmBack = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".rrr.", "rrrrr", "rrrrr", "rrrr.", "rrrr.", ".rrr.", ".rrr.", "rrrrr", ".bbb.", ".b.b."),
                Weapon = new PixelPart("skullstaff", P(3.5f, 11.5f), m, 0,
                    "...f...",
                    "..fFf..",
                    ".fFFFf.",
                    "..bbb..",
                    ".bbbbb.",
                    ".bkbkb.",
                    ".bbbbb.",
                    "..bkb..",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...b..."),
                WeaponZ = 9,
                WeaponReach = 14f,
                SmearColor = PixelMaterial.Hex("#7dff9a"),
            };
            const float arm = 48f, staff = 4f;
            return new List<Clip>
            {
                rig.Clip("idle", 9f, true, Idle(arm, -5f, staff)),
                rig.Clip("run", 16f, true, Run(35f, 15f, armSwing: 8f)),
                rig.Clip("attack", 16f, false, Thrust()),
                rig.Clip("attack2", 16f, false, Thrust()),
                rig.Clip("skill1", 11f, false, Raise(staffSpin: false)),
                rig.Clip("skill2", 11f, false, Raise(staffSpin: true)),
                rig.Clip("dodge", 16f, false, Dodge(0f)),
                rig.Clip("hurt", 10f, false, Hurt(arm, staff)),
                rig.Clip("death", 9f, false, Death(arm, 180f)),
            };
        }

        // ------------------------------------------------------------------ Paladin

        /// <summary>Base look: bright plate, blue tabard and cape, gold cross and trim. Skins repaint m / c / g.</summary>
        public static Dictionary<char, PixelMaterial> PaladinBase() => Palette.With(
            ('m', Palette.Ramp(2, "#262a34", "#474e5e", "#77819a", "#aeb8cc", "#e4ebf6")),
            ('c', Palette.Ramp(2, "#0a1226", "#13224a", "#1f3a78", "#3058a8", "#4a7ad0")));

        public static List<Clip> Paladin(Dictionary<char, PixelMaterial> palette = null)
        {
            var m = palette ?? PaladinBase();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("greathelm", P(5.5f, 0.5f), m, 0,
                    "....ggg....",
                    "...mmgmm...",
                    "..mmmgmmm..",
                    ".mmmmmmmmm.",
                    ".mmmmmmmmMm",
                    ".mmmmgkkkkm",
                    ".mmmmgggggm",
                    ".mmmmgmkkkm",
                    ".mmmmmgmmmm",
                    "..mmmmmmmm.",
                    "...mmmmmm..",
                    "...ggggg..."),
                Torso = new PixelPart("tabard", P(6f, 5.5f), m, 0,
                    "...mmmmmm...",
                    "..mmmmmmmmm.",
                    ".mmmmmMMmmmm",
                    ".mmmcgccmmmm",
                    ".mmccgcccmm.",
                    "..mgggggggm.",
                    "..mccgcccm..",
                    "..ggggggggg.",
                    "..ccccgcccc.",
                    "..cccccccccc",
                    "..ccc..cccc.",
                    "..cc....ccc.",
                    "..c......cc."),
                LegFront = Leg(m),
                LegBack = Leg(m),
                ArmFront = Arm(m),
                ArmBack = Arm(m),
                Weapon = new PixelPart("holysword", P(2.5f, 3.5f), m, 0,
                    "..W..",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wWw.",
                    ".wgw.",
                    "ggggg",
                    "gg.gg",
                    "..l..",
                    "..l..",
                    "..l..",
                    ".ggg.",
                    "..y.."),
                Cape = new PixelPart("cape", P(8f, 13.5f), m, -1,
                    "....cccccc",
                    "...ccccccc",
                    "..cccccccc",
                    "..ccccccc.",
                    ".cccccccc.",
                    ".ccccccc..",
                    ".ccccccc..",
                    "cccccccc..",
                    "ccccccc...",
                    "ccccccc...",
                    "cccccc....",
                    "gg.ggg....",
                    "g..gg.....",
                    "...g......"),
                WeaponReach = 19f,
                SmearColor = PixelMaterial.Hex("#fff0b0"),
            };
            const float idleArm = 18f, idleSword = -40f;
            return new List<Clip>
            {
                rig.Clip("idle", 9f, true, Idle(idleArm, -8f, idleSword)),
                rig.Clip("run", 16f, true, Run(25f, -55f)),
                rig.Clip("attack", 17f, false, Swing(rig, fromArm: 210f, toArm: 40f)),
                rig.Clip("attack2", 17f, false, Swing(rig, fromArm: 60f, toArm: 205f, backhand: true)),
                rig.Clip("skill1", 11f, false, Raise(staffSpin: false)),
                rig.Clip("skill2", 10f, false, WarCry()),
                rig.Clip("dodge", 16f, false, Dodge(-120f)),
                rig.Clip("hurt", 10f, false, Hurt(idleArm, idleSword)),
                rig.Clip("death", 9f, false, Death(idleArm, 0f)),
            };
        }
    }
}
