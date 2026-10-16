using System.Collections.Generic;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// The classes unlocked by requests: guardian (plate, kite shield, warhammer), assassin (hood, mask, twin
    /// daggers) and alchemist (brimmed hat, goggles, apron, flasks). Same clip order as the first three.
    /// </summary>
    public static partial class HeroArt
    {
        // ------------------------------------------------------------------ Guardian

        /// <summary>Base look: silver plate with an ivory tabard and gold trim. Skins repaint m / c / g.</summary>
        public static Dictionary<char, PixelMaterial> GuardianBase() => Palette.With(('c', Palette.Ivory));

        public static List<Clip> Guardian(Dictionary<char, PixelMaterial> palette = null)
        {
            var m = palette ?? GuardianBase();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("helm", P(5.5f, 0.5f), m, 0,
                    "...mmmmm...",
                    "..mmmmmmm..",
                    ".mmmmmmmmm.",
                    ".mmmmmmmmMm",
                    ".mmmmmgkkkm",
                    ".mmmmmgggkm",
                    ".mmmmmgkkkm",
                    ".mmmmmmgmmm",
                    "..mmmmmmmm.",
                    "..mmmmmmmm.",
                    "...mmmmmm..",
                    "...ggggg..."),
                Torso = new PixelPart("plate", P(6f, 5.5f), m, 0,
                    "...mmmmmm...",
                    "..mmmmmmmmm.",
                    ".mmmmmmMMmmm",
                    ".mmmcccccmmm",
                    ".mmmccgccmm.",
                    "..mmcgggcmm.",
                    "..mmccgccm..",
                    "..ggggggggg.",
                    "..cccccccc..",
                    "..ccccccccc.",
                    "..ccc..cccc.",
                    "..cc....ccc.",
                    "..c......cc."),
                LegFront = Leg(m),
                LegBack = Leg(m),
                ArmFront = Arm(m),
                ArmBack = Arm(m),
                Weapon = new PixelPart("warhammer", P(3.5f, 3.5f), m, 0,
                    ".mmmmm.",
                    "mmmMmmm",
                    "mmmmmmm",
                    "mmmmmmm",
                    ".mmmmm.",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...o...",
                    "...l...",
                    "...l...",
                    "...l...",
                    "..ggg..",
                    "...g..."),
                Offhand = new PixelPart("kite", P(5f, 8f), m, 0,
                    "..gggggg..",
                    ".gjjjjjjg.",
                    "gjjjjgjjjg",
                    "gjjjjgjjjg",
                    "gjjgggggjg",
                    "gjjjjgjjjg",
                    "gjjjjgjjjg",
                    "gjjjjgjjjg",
                    ".gjjjgjjg.",
                    ".gjjjjjjg.",
                    "..gjjjjg..",
                    "..gjjjjg..",
                    "...gjjg...",
                    "....gg...."),
                OffhandTilt = 0f,
                WeaponReach = 17f,
                SmearColor = PixelMaterial.Hex("#ffe9b0"),
            };

            const float idleArm = 22f, idleHammer = -35f;
            return new List<Clip>
            {
                // The shield is held up in front (back arm raised), the hammer rests on the shoulder.
                rig.Clip("idle", 7.5f, true, Idle(idleArm, 60f, idleHammer)),
                rig.Clip("run", 14.7f, true, GuardRun()),
                rig.Clip("attack", 15f, false, Swing(rig, fromArm: 200f, toArm: 40f)),
                rig.Clip("attack2", 15f, false, Swing(rig, fromArm: 190f, toArm: 30f)),
                rig.Clip("skill1", 14f, false, ShieldBash()),
                rig.Clip("skill2", 9f, false, WarCry()),
                rig.Clip("dodge", 16f, false, Dodge(-120f)),
                rig.Clip("hurt", 10f, false, Hurt(idleArm, idleHammer)),
                rig.Clip("death", 9f, false, Death(idleArm, 0f)),
            };
        }

        /// <summary>A steady trot behind the shield (no wild arm swing).</summary>
        private static IEnumerable<RigPose> GuardRun()
        {
            foreach (var pose in Run(25f, -45f, armSwing: 6f))
            {
                var p = pose;
                p.ArmBack = 40f + p.ArmBack * 0.15f;
                p.Torso = -6f;
                yield return p;
            }
        }

        /// <summary>Shield thrust forward with the body behind it.</summary>
        private static IEnumerable<RigPose> ShieldBash()
        {
            yield return new RigPose { ArmFront = 30f, ArmBack = 60f, Torso = 8f, Bob = -1f, LegFront = 20f, LegBack = -20f, WeaponAbsolute = -40f };
            yield return new RigPose { ArmFront = 20f, ArmBack = 95f, Torso = -22f, Bob = -1f, LegFront = 42f, LegBack = -42f, Shift = 3f, WeaponAbsolute = -50f };
            yield return new RigPose { ArmFront = 20f, ArmBack = 100f, Torso = -26f, Bob = -1f, LegFront = 48f, LegBack = -46f, Shift = 4f, WeaponAbsolute = -50f };
            yield return new RigPose { ArmFront = 22f, ArmBack = 80f, Torso = -14f, LegFront = 30f, LegBack = -26f, Shift = 3f, WeaponAbsolute = -45f };
            yield return new RigPose { ArmFront = 22f, ArmBack = 45f, Torso = -4f, LegFront = 10f, LegBack = -10f, Shift = 1f, WeaponAbsolute = -38f };
        }

        // ------------------------------------------------------------------ Assassin

        /// <summary>Base look: charcoal hood and mask over dark leather. Skins repaint h / l / n.</summary>
        public static Dictionary<char, PixelMaterial> AssassinBase() => Palette.With(
            ('h', Palette.Ramp(2, "#12121a", "#22222e", "#363648", "#4e4e66", "#6e6e8c")),
            ('l', Palette.Ramp(2, "#1a1310", "#2e221c", "#45332a", "#5e473b", "#7a5f50")));

        public static List<Clip> Assassin(Dictionary<char, PixelMaterial> palette = null)
        {
            var m = palette ?? AssassinBase();
            var dagger = new PixelPart("dagger", P(2.5f, 2.5f), m, 0,
                "..w..",
                ".wWw.",
                ".wWw.",
                ".wWw.",
                ".wWw.",
                ".wWw.",
                ".wWw.",
                "ggggg",
                "..n..",
                "..n..",
                "..g..");
            var rig = new HumanoidRig
            {
                Head = new PixelPart("hood", P(5.5f, 0.5f), m, 0,
                    "....hhhh...",
                    "...hhhhhhh.",
                    "..hhhhhhhhh",
                    ".hhhhhhhHhh",
                    ".hhhhhkkkkh",
                    "hhhhhkskske",
                    "hhhhhksssss",
                    "hhhhhnnnnnn",
                    ".hhhhhnnnn.",
                    "..hhhhhhh..",
                    "...hhhhh..."),
                Torso = new PixelPart("leathers", P(6f, 5.5f), m, 0,
                    "...hhhhhh...",
                    "..lllllllll.",
                    ".llllnlllll.",
                    ".lllllnllll.",
                    ".llllllnlll.",
                    "..llllllnll.",
                    "..nnnnnnnn..",
                    "..lllllllll.",
                    "..llllllll..",
                    "..lll..lll..",
                    "..ll....ll.."),
                LegFront = new PixelPart("leg", P(2f, 9.5f), m, 0, "llll.", "llll.", "llll.", ".ll..", "llll.", "nnnn.", "nnnn.", "llll.", "lllll", "lllll"),
                LegBack = new PixelPart("leg", P(2f, 9.5f), m, 0, "llll.", "llll.", "llll.", ".ll..", "llll.", "nnnn.", "nnnn.", "llll.", "lllll", "lllll"),
                ArmFront = new PixelPart("arm", P(2f, 8.5f), m, 0, ".lll.", "lllll", "lllll", ".lll.", ".lll.", ".nnn.", ".lll.", ".lll.", ".sss.", ".ss.."),
                ArmBack = new PixelPart("arm", P(2f, 8.5f), m, 0, ".lll.", "lllll", "lllll", ".lll.", ".lll.", ".nnn.", ".lll.", ".lll.", ".sss.", ".ss.."),
                Weapon = dagger,
                Offhand = dagger,
                OffhandTilt = -150f, // reverse grip
                Cape = new PixelPart("scarf", P(6f, 10.5f), m, -1,
                    "..dddddd",
                    ".ddddddd",
                    "ddddddd.",
                    "dddddd..",
                    "ddddd...",
                    "ddd.d...",
                    "dd..d...",
                    "d...d...",
                    "........",
                    "...d...."),
                WeaponReach = 10f,
                SmearColor = PixelMaterial.Hex("#b7a6e8"),
            };

            const float idleArm = 45f;
            return new List<Clip>
            {
                rig.Clip("idle", 7f, true, Crouch()),
                rig.Clip("run", 18.7f, true, Run(40f, 20f, armSwing: 12f)),
                rig.Clip("attack", 22f, false, Swing(rig, fromArm: 150f, toArm: 45f)),
                rig.Clip("attack2", 22f, false, Swing(rig, fromArm: 40f, toArm: 150f, backhand: true)),
                rig.Clip("skill1", 16f, false, Dodge(30f)),
                rig.Clip("skill2", 12f, false, ThrowPoses(underhand: false)),
                rig.Clip("dodge", 18f, false, Dodge(30f)),
                rig.Clip("hurt", 10f, false, Hurt(idleArm, 30f)),
                rig.Clip("death", 9f, false, Death(idleArm, 60f)),
            };
        }

        /// <summary>Low, coiled stance; blades ready.</summary>
        private static IEnumerable<RigPose> Crouch()
        {
            for (int i = 0; i < 4; i++)
            {
                float breath = i >= 2 ? -1f : 0f;
                yield return new RigPose
                {
                    Bob = -2f + breath,
                    Torso = -12f,
                    Head = 6f,
                    LegFront = 22f,
                    LegBack = -18f,
                    ArmFront = 45f - (i >= 2 ? 3f : 0f),
                    ArmBack = 30f + (i >= 2 ? 3f : 0f),
                    WeaponAbsolute = 30f - (i >= 2 ? 3f : 0f),
                    Cape = -14f - i * 3f,
                };
            }
        }

        // ------------------------------------------------------------------ Alchemist

        /// <summary>Base look: dark green hat, canvas apron over a leather coat, gold goggles. Skins repaint u / l / f.</summary>
        public static Dictionary<char, PixelMaterial> AlchemistBase() => Palette.With(
            ('f', new PixelMaterial("#b6ff4a")), ('F', new PixelMaterial("#f0ffd0")),
            ('u', Palette.Ramp(2, "#2a2418", "#4a3f2c", "#6e5f44", "#94825e", "#b8a67e"))); // canvas apron

        public static List<Clip> Alchemist(Dictionary<char, PixelMaterial> palette = null)
        {
            var m = palette ?? AlchemistBase();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("hat", P(6.5f, 0.5f), m, 0,
                    "....aaaaa....",
                    "...aaaaaaa...",
                    "...aaaaaaA...",
                    "aaaaaaaaaaaaa",
                    "..sssssssss..",
                    "..ssssgfgfg..",
                    "..sssssssss..",
                    "..ssssssss...",
                    "...ssssss....",
                    "...ttttttt...",
                    "....ttttt...."),
                Torso = new PixelPart("coat", P(6f, 6.5f), m, 0,
                    "...tttttt...",
                    "..lllllllll.",
                    ".llluuuuulll",
                    ".llluuuuull.",
                    ".lllqyqfqll.",
                    "..lluuuuul..",
                    "..nnnnnnnn..",
                    "..luuuuuul..",
                    ".lluuuuuull.",
                    ".lluuuuuuul.",
                    ".llll..llll.",
                    "llll....llll"),
                LegFront = new PixelPart("boot", P(2f, 9.5f), m, 0, ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", "llll", "llll"),
                LegBack = new PixelPart("boot", P(2f, 9.5f), m, 0, ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", ".ll.", "llll", "llll"),
                ArmFront = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".lll.", "lllll", "lllll", "llll.", "llll.", ".lll.", ".lll.", "lllll", ".sss.", ".ss.."),
                ArmBack = new PixelPart("sleeve", P(2.5f, 8.5f), m, 0, ".lll.", "lllll", "lllll", "llll.", "llll.", ".lll.", ".lll.", "lllll", ".sss.", ".ss.."),
                Weapon = new PixelPart("flask", P(2f, 0.5f), m, 0,
                    ".oo.",
                    ".qq.",
                    ".ff.",
                    "ffff",
                    "fFff",
                    "ffff",
                    ".ff."),
                WeaponZ = 9,
                WeaponReach = 6f,
                SmearColor = PixelMaterial.Hex("#b6ff4a"),
            };

            const float idleArm = 40f;
            return new List<Clip>
            {
                rig.Clip("idle", 9f, true, Idle(idleArm, -12f, 10f)),
                rig.Clip("run", 16f, true, Run(35f, 20f, armSwing: 10f)),
                rig.Clip("attack", 16f, false, ThrowPoses(underhand: false)),
                rig.Clip("attack2", 16f, false, ThrowPoses(underhand: false)),
                rig.Clip("skill1", 13f, false, ThrowPoses(underhand: false, big: true)),
                rig.Clip("skill2", 13f, false, ThrowPoses(underhand: true, big: true)),
                rig.Clip("dodge", 16f, false, Dodge(0f)),
                rig.Clip("hurt", 10f, false, Hurt(idleArm, 10f)),
                rig.Clip("death", 9f, false, Death(idleArm, 90f)),
            };
        }

        /// <summary>Wind back and hurl (overhand), or a gentle lob (underhand).</summary>
        private static IEnumerable<RigPose> ThrowPoses(bool underhand, bool big = false)
        {
            float lean = big ? 1.4f : 1f;
            if (underhand)
            {
                yield return new RigPose { ArmFront = -40f, ArmBack = 20f, Torso = 8f, Bob = -1f, LegFront = 10f, LegBack = -10f, WeaponAbsolute = 0f };
                yield return new RigPose { ArmFront = -55f, ArmBack = 30f, Torso = 12f, Bob = -2f, LegFront = 16f, LegBack = -14f, WeaponAbsolute = 0f };
                yield return new RigPose { ArmFront = 60f, ArmBack = -10f, Torso = -6f, LegFront = 22f, LegBack = -18f, Shift = 1f, WeaponAbsolute = 40f };
                yield return new RigPose { ArmFront = 110f, ArmBack = -20f, Torso = -8f, Head = 4f, LegFront = 18f, LegBack = -16f, Shift = 1f, WeaponAbsolute = 80f };
                yield return new RigPose { ArmFront = 60f, ArmBack = -5f, Torso = -2f, LegFront = 8f, LegBack = -6f, WeaponAbsolute = 20f };
                yield break;
            }
            yield return new RigPose { ArmFront = 150f, ArmBack = 30f, Torso = 10f * lean, LegFront = 8f, LegBack = -10f, WeaponAbsolute = 0f };
            yield return new RigPose { ArmFront = 195f, ArmBack = 40f, Torso = 14f * lean, Bob = -1f, LegFront = 14f, LegBack = -14f, Shift = -1f, WeaponAbsolute = 20f };
            yield return new RigPose { ArmFront = 90f, ArmBack = -30f, Torso = -14f * lean, LegFront = 28f, LegBack = -24f, Shift = 2f, WeaponAbsolute = -60f };
            yield return new RigPose { ArmFront = 55f, ArmBack = -35f, Torso = -16f * lean, LegFront = 30f, LegBack = -26f, Shift = 2f, WeaponAbsolute = -80f };
            yield return new RigPose { ArmFront = 40f, ArmBack = -10f, Torso = -4f, LegFront = 10f, LegBack = -8f, Shift = 1f, WeaponAbsolute = -20f };
        }
    }
}
