using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// The three playable classes. Each returns its clips in a fixed order that the runtime animator reads by name:
    /// idle, run, attack, attack2, skill1, skill2, dodge, hurt, death.
    /// </summary>
    public static class HeroArt
    {
        public const int FrameSize = 64;

        private static Vector2 P(float x, float y) => new(x, y);

        // ------------------------------------------------------------------ Shared motion

        private static IEnumerable<RigPose> Idle(float arm, float armBack, float? weapon, float tilt = 0f)
        {
            for (int i = 0; i < 4; i++)
            {
                float breath = i >= 2 ? -1f : 0f;
                yield return new RigPose
                {
                    Bob = breath,
                    Head = i == 3 ? -2f : 0f,
                    ArmFront = arm - (i >= 2 ? 3f : 0f),
                    ArmBack = armBack + (i >= 2 ? 3f : 0f),
                    WeaponAbsolute = weapon.HasValue ? weapon.Value - (i >= 2 ? 3f : 0f) : null,
                    WeaponTilt = tilt,
                    Cape = -4f - i * 1.5f,
                };
            }
        }

        private static IEnumerable<RigPose> Run(float arm, float? weapon, float tilt = 0f, float armSwing = 15f)
        {
            for (int i = 0; i < 6; i++)
            {
                float s = Mathf.Sin(i / 6f * Mathf.PI * 2f);
                float c = Mathf.Abs(Mathf.Cos(i / 6f * Mathf.PI * 2f));
                yield return new RigPose
                {
                    Bob = c > 0.7f ? 1f : 0f,
                    Torso = -9f,
                    Head = 4f,
                    LegFront = 38f * s,
                    LegBack = -38f * s,
                    ArmBack = 35f * s,
                    ArmFront = arm - armSwing * s,
                    WeaponAbsolute = weapon.HasValue ? weapon.Value - armSwing * s : null,
                    WeaponTilt = tilt,
                    Cape = -30f - 14f * c,
                };
            }
        }

        private static IEnumerable<RigPose> Dodge(float? weapon)
        {
            float[] spin = { 0f, -95f, -190f, -285f };
            for (int i = 0; i < 4; i++)
            {
                yield return new RigPose
                {
                    Bob = -3f,
                    Torso = -25f,
                    LegFront = 70f,
                    LegBack = 50f,
                    ArmFront = 70f,
                    ArmBack = 50f,
                    WeaponAbsolute = weapon,
                    WeaponTilt = 160f,
                    Cape = -20f,
                    BodyAngle = spin[i],
                    BodyPivot = P(32f, 19f),
                    BodyOffset = P(0f, -2f),
                };
            }
        }

        private static IEnumerable<RigPose> Hurt(float arm, float? weapon, float tilt = 0f)
        {
            yield return new RigPose { Torso = 16f, Head = 14f, ArmFront = arm + 35f, ArmBack = -45f, LegFront = 12f, LegBack = -8f, WeaponAbsolute = weapon + 35f, WeaponTilt = tilt, Cape = 10f, Shift = -1f };
            yield return new RigPose { Torso = 8f, Head = 6f, ArmFront = arm + 15f, ArmBack = -20f, LegFront = 6f, LegBack = -4f, WeaponAbsolute = weapon + 15f, WeaponTilt = tilt, Cape = 4f };
        }

        private static IEnumerable<RigPose> Death(float arm, float tilt)
        {
            float[] fall = { 0f, 12f, 35f, 62f, 84f, 90f };
            for (int i = 0; i < fall.Length; i++)
            {
                float k = i / (fall.Length - 1f);
                yield return new RigPose
                {
                    Torso = 10f + 10f * k,
                    Head = 10f + 20f * k,
                    ArmFront = arm + 120f * k,
                    ArmBack = -30f - 90f * k,
                    LegFront = 15f * k,
                    LegBack = -10f * k,
                    WeaponTilt = tilt + 60f * k,
                    Cape = 30f * k,
                    BodyAngle = fall[i],
                    BodyPivot = P(29f, 10f),
                    BodyOffset = P(0f, i >= 4 ? -2f : 0f),
                };
            }
        }

        // ------------------------------------------------------------------ Warrior

        public static List<Clip> Warrior()
        {
            var m = Palette.Standard();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("head", P(6.5f, 0.5f), m, 0,
                    "...ccc.....",
                    "..cccmmm...",
                    ".ccmmmmmmm.",
                    ".cmmmmmmmmm",
                    "ccmmmmmmmMm",
                    "c.mmmmmmmMm",
                    "..mmmmmkkkk",
                    "..mmmmmmkmm",
                    "..mmmmmmkmm",
                    "..mmmmmmmmm",
                    "...mmmmmmm.",
                    "....ggggg.."),
                Torso = new PixelPart("torso", P(6f, 5.5f), m, 0,
                    "...mmmmmm...",
                    "..mmmmmmmmm.",
                    ".mmmmmMMmmmm",
                    ".mmmmcccmmmm",
                    ".mmmmcccmmm.",
                    "..mmmcccmmm.",
                    "..mmmcccmm..",
                    "..llggggll..",
                    "..cccccccc..",
                    "..ccccccccc.",
                    "..ccc..cccc.",
                    "..cc....ccc.",
                    "..c......cc."),
                LegFront = Leg(m),
                LegBack = Leg(m),
                ArmFront = Arm(m),
                ArmBack = Arm(m),
                Weapon = new PixelPart("sword", P(2.5f, 3.5f), m, 0,
                    "..w..",
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
                    ".wWw.",
                    ".wkw.",
                    "ggggg",
                    "g.l.g",
                    "..l..",
                    "..l..",
                    "..l..",
                    ".ggg.",
                    "..g.."),
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
                    "cc.ccc....",
                    "c..cc.....",
                    "...c......"),
                WeaponReach = 20f,
            };

            const float idleArm = 18f, idleSword = -40f;
            var clips = new List<Clip>
            {
                rig.Clip("idle", 6f, true, Idle(idleArm, -8f, idleSword)),
                rig.Clip("run", 12f, true, Run(25f, -55f)),
                rig.Clip("attack", 18f, false, Swing(rig, fromArm: 215f, toArm: 35f)),
                rig.Clip("attack2", 18f, false, Swing(rig, fromArm: 60f, toArm: 200f, backhand: true)),
                rig.Clip("skill1", 16f, false, Lunge()),
                rig.Clip("skill2", 10f, false, WarCry()),
                rig.Clip("dodge", 16f, false, Dodge(-120f)),
                rig.Clip("hurt", 10f, false, Hurt(idleArm, idleSword)),
                rig.Clip("death", 9f, false, Death(idleArm, 0f)),
            };
            return clips;
        }

        private static PixelPart Leg(Dictionary<char, PixelMaterial> m) => new("leg", P(2f, 9.5f), m, 0,
            "mmmm.",
            "mmmm.",
            "nmmm.",
            ".mm..",
            "mMmm.",
            "mmmm.",
            "mmmm.",
            "llll.",
            "lllll",
            "lllll");

        private static PixelPart Arm(Dictionary<char, PixelMaterial> m) => new("arm", P(2.5f, 8.5f), m, 0,
            ".mmm.",
            "mmmmm",
            "mMmmm",
            "mmmmm",
            ".mmm.",
            ".nmn.",
            ".mmm.",
            ".mmm.",
            ".lll.",
            ".lll.");

        /// <summary>Wind-up, swing with a smear, follow-through.</summary>
        private static IEnumerable<RigPose> Swing(HumanoidRig rig, float fromArm, float toArm, bool backhand = false)
        {
            float wind = backhand ? -15f : 15f;
            float mid = Mathf.Lerp(fromArm, toArm, 0.55f);
            yield return new RigPose { ArmFront = fromArm, ArmBack = -30f, Torso = backhand ? -4f : 8f, Head = 2f, LegFront = 8f, LegBack = -8f, Cape = -6f };
            yield return new RigPose { ArmFront = fromArm + wind, ArmBack = -40f, Torso = backhand ? -6f : 12f, Bob = -1f, LegFront = 14f, LegBack = -12f, Cape = -8f };
            yield return new RigPose { ArmFront = mid, ArmBack = 15f, Torso = -6f, LegFront = 26f, LegBack = -22f, Shift = 1f, Cape = -18f, Smear = true, SmearFrom = fromArm + wind, SmearTo = mid };
            yield return new RigPose { ArmFront = toArm, ArmBack = 35f, Torso = backhand ? 2f : -14f, Bob = -1f, LegFront = 32f, LegBack = -28f, Shift = 2f, Cape = -26f, Smear = true, SmearFrom = mid, SmearTo = toArm };
            yield return new RigPose { ArmFront = toArm - (backhand ? -8f : 8f), ArmBack = 25f, Torso = backhand ? 0f : -10f, LegFront = 26f, LegBack = -22f, Shift = 2f, Cape = -18f };
            yield return new RigPose { ArmFront = Mathf.Lerp(toArm, 18f, 0.5f), ArmBack = 5f, Torso = -3f, LegFront = 10f, LegBack = -10f, Shift = 1f, Cape = -8f };
        }

        private static IEnumerable<RigPose> Lunge()
        {
            yield return new RigPose { ArmFront = 160f, ArmBack = -50f, Torso = 10f, Bob = -2f, LegFront = 30f, LegBack = -30f, Cape = -10f };
            yield return new RigPose { ArmFront = 95f, ArmBack = -60f, Torso = -25f, Bob = -1f, LegFront = 45f, LegBack = -45f, Shift = 3f, Cape = -50f, Smear = true, SmearFrom = 160f, SmearTo = 95f };
            yield return new RigPose { ArmFront = 85f, ArmBack = -60f, Torso = -28f, Bob = -1f, LegFront = 50f, LegBack = -50f, Shift = 4f, Cape = -60f };
            yield return new RigPose { ArmFront = 60f, ArmBack = -30f, Torso = -18f, LegFront = 35f, LegBack = -30f, Shift = 3f, Cape = -40f, Smear = true, SmearFrom = 85f, SmearTo = 30f };
            yield return new RigPose { ArmFront = 30f, ArmBack = 0f, Torso = -8f, LegFront = 15f, LegBack = -15f, Shift = 1f, Cape = -15f };
        }

        private static IEnumerable<RigPose> WarCry()
        {
            yield return new RigPose { ArmFront = 120f, ArmBack = -20f, Torso = 4f, Bob = -1f, WeaponTilt = 0f, Cape = -6f };
            yield return new RigPose { ArmFront = 170f, ArmBack = -60f, Torso = 10f, Head = 12f, Bob = 1f, Cape = 8f };
            yield return new RigPose { ArmFront = 178f, ArmBack = -80f, Torso = 12f, Head = 16f, Bob = 2f, Cape = 14f };
            yield return new RigPose { ArmFront = 178f, ArmBack = -80f, Torso = 12f, Head = 16f, Bob = 2f, Cape = 10f };
            yield return new RigPose { ArmFront = 120f, ArmBack = -30f, Torso = 4f, Cape = 0f };
        }

        // ------------------------------------------------------------------ Mage (also the base of the cultist enemy)

        public static HumanoidRig MageRig(Dictionary<char, PixelMaterial> m, bool glowingEyes)
        {
            return new HumanoidRig
            {
                Head = new PixelPart("hood", P(5.5f, 0.5f), m, 0,
                    "....rrrr...",
                    "...rrrrrrr.",
                    "..rrrrrrrrr",
                    ".rrrrrrRrrr",
                    ".rrrrrkkkkr",
                    "rrrrrkkkkkk",
                    glowingEyes ? "rrrrrkkekek" : "rrrrrkkfkfk",
                    "rrrrrkkkkkk",
                    ".rrrrrkkkk.",
                    "..rrrrrrr..",
                    "...ggggg..."),
                Torso = new PixelPart("robe", P(6f, 7.5f), m, 0,
                    "...rrrrrr...",
                    "..rrrrrrrrr.",
                    ".rrrrgrrrrrr",
                    ".rrrrgrrrrr.",
                    ".rrrrgrrrrr.",
                    "..rrrgrrrr..",
                    "..llllylll..",
                    "..rrrrrrrrr.",
                    "..rrrrrrrrr.",
                    ".rrrrrrrrrr.",
                    ".rrrrrrrrrrr",
                    ".rrrrrrrrrrr",
                    "rrrrrrrrrrrr",
                    "rrrr.rrrrrr."),
                LegFront = new PixelPart("boot", P(2f, 9.5f), m, 0,
                    ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".ll.", ".ll.", "llll", "llll"),
                LegBack = new PixelPart("boot", P(2f, 9.5f), m, 0,
                    ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".rr.", ".ll.", ".ll.", "llll", "llll"),
                ArmFront = Sleeve(m),
                ArmBack = Sleeve(m),
                Weapon = new PixelPart("staff", P(2.5f, 9.5f), m, 0,
                    ".fff.",
                    "fFFFf",
                    "fFFFf",
                    ".fff.",
                    "g.g.g",
                    ".ggg.",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..o..",
                    "..g.."),
                WeaponZ = 9,
                WeaponReach = 14f,
                SmearColor = PixelMaterial.Hex("#8fd3ff"),
            };
        }

        private static PixelPart Sleeve(Dictionary<char, PixelMaterial> m) => new("sleeve", P(2.5f, 8.5f), m, 0,
            ".rrr.",
            "rrrrr",
            "rrrrr",
            "rrrr.",
            "rrrr.",
            ".rrr.",
            ".rrr.",
            "rrrrr",
            ".sss.",
            ".ss..");

        public static List<Clip> Mage()
        {
            var rig = MageRig(Palette.Standard(), glowingEyes: false);
            const float arm = 48f, staff = 4f; // staff held upright, in front of the body
            return new List<Clip>
            {
                rig.Clip("idle", 6f, true, Idle(arm, -5f, staff)),
                rig.Clip("run", 12f, true, Run(35f, 15f, armSwing: 8f)),
                rig.Clip("attack", 16f, false, Thrust()),
                rig.Clip("attack2", 16f, false, Thrust()),
                rig.Clip("skill1", 12f, false, Raise(staffSpin: true)),
                rig.Clip("skill2", 10f, false, Raise(staffSpin: false)),
                rig.Clip("dodge", 16f, false, Dodge(0f)),
                rig.Clip("hurt", 10f, false, Hurt(arm, staff)),
                rig.Clip("death", 9f, false, Death(arm, 180f)),
            };
        }

        /// <summary>Staff thrust forward to fire a bolt.</summary>
        private static IEnumerable<RigPose> Thrust()
        {
            yield return new RigPose { ArmFront = 20f, ArmBack = -20f, Torso = 6f, WeaponAbsolute = 20f };
            yield return new RigPose { ArmFront = 75f, ArmBack = -35f, Torso = -6f, LegFront = 15f, LegBack = -12f, Shift = 1f, WeaponAbsolute = -55f };
            yield return new RigPose { ArmFront = 85f, ArmBack = -35f, Torso = -8f, LegFront = 18f, LegBack = -14f, Shift = 1f, WeaponAbsolute = -70f };
            yield return new RigPose { ArmFront = 50f, ArmBack = -15f, Torso = -2f, LegFront = 8f, LegBack = -6f, WeaponAbsolute = -25f };
        }

        /// <summary>Both arms raised with the staff overhead (spells).</summary>
        private static IEnumerable<RigPose> Raise(bool staffSpin)
        {
            yield return new RigPose { ArmFront = 70f, ArmBack = -40f, Torso = 4f, Bob = -1f, WeaponAbsolute = 0f, Cape = 0f };
            yield return new RigPose { ArmFront = 150f, ArmBack = -130f, Torso = 8f, Head = 10f, Bob = 1f, WeaponAbsolute = staffSpin ? 40f : 0f };
            yield return new RigPose { ArmFront = 165f, ArmBack = -150f, Torso = 10f, Head = 14f, Bob = 2f, WeaponAbsolute = staffSpin ? 90f : 0f };
            yield return new RigPose { ArmFront = 165f, ArmBack = -150f, Torso = 10f, Head = 14f, Bob = 2f, WeaponAbsolute = staffSpin ? 150f : 0f };
            yield return new RigPose { ArmFront = 60f, ArmBack = -20f, Torso = 2f, WeaponAbsolute = 10f };
        }

        // ------------------------------------------------------------------ Ranger

        public static List<Clip> Ranger()
        {
            var m = Palette.Standard();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("hood", P(5.5f, 0.5f), m, 0,
                    "...hhhhh...",
                    "..hhhhhhh..",
                    ".hhhhhhhhh.",
                    "hhhhhhhHhhh",
                    "hhhhhhkssss",
                    "hhhhhkssksk",
                    "hhhhhksssss",
                    ".hhhhhkssss",
                    ".hhhhhhkkk.",
                    "..hhhhhhh..",
                    "...lllll..."),
                Torso = new PixelPart("tunic", P(6f, 5.5f), m, 0,
                    "...hhhhhh...",
                    "..hhllllhhh.",
                    ".hhlllLlllh.",
                    ".hhllllllhh.",
                    ".hhllgllll..",
                    "..hlllglll..",
                    "..hllllgll..",
                    "..llggggll..",
                    "..lllllllll.",
                    "..hhhlllhh..",
                    "..hhh..hhh..",
                    "..hh....hh.."),
                LegFront = new PixelPart("leg", P(2f, 9.5f), m, 0,
                    "llll.", "llll.", "llll.", ".ll..", "llll.", "hhhh.", "hhhh.", "llll.", "lllll", "lllll"),
                LegBack = new PixelPart("leg", P(2f, 9.5f), m, 0,
                    "llll.", "llll.", "llll.", ".ll..", "llll.", "hhhh.", "hhhh.", "llll.", "lllll", "lllll"),
                ArmFront = new PixelPart("arm", P(2f, 8.5f), m, 0,
                    ".hhh.", "hhhhh", "hhhhh", ".lll.", ".lll.", ".lll.", ".lll.", ".lll.", ".sss.", ".ss.."),
                ArmBack = new PixelPart("arm", P(2f, 8.5f), m, 0,
                    ".hhh.", "hhhhh", "hhhhh", ".lll.", ".lll.", ".lll.", ".lll.", ".lll.", ".sss.", ".ss.."),
                Weapon = new PixelPart("bow", P(3.5f, 9.5f), m, 0,
                    ".o...",
                    ".qo..",
                    ".q.o.",
                    ".q.o.",
                    ".q..o",
                    ".q..o",
                    ".q..o",
                    ".q..o",
                    ".q.lo",
                    ".q.lo",
                    ".q.lo",
                    ".q..o",
                    ".q..o",
                    ".q..o",
                    ".q..o",
                    ".q.o.",
                    ".q.o.",
                    ".qo..",
                    ".o..."),
                WeaponZ = 9,
                Cape = new PixelPart("cloak", P(7f, 12.5f), m, -1,
                    "...hhhhhh",
                    "..hhhhhhh",
                    ".hhhhhhhh",
                    ".hhhhhhh.",
                    "hhhhhhhh.",
                    "hhhhhhh..",
                    "hhhhhhh..",
                    "hhhhhh...",
                    "hhhhh....",
                    "hh.hh....",
                    "h..h....."),
            };

            const float arm = 25f, bow = -15f;
            return new List<Clip>
            {
                rig.Clip("idle", 6f, true, Idle(arm, -10f, bow)),
                rig.Clip("run", 12f, true, Run(30f, -25f, armSwing: 10f)),
                rig.Clip("attack", 18f, false, Shoot()),
                rig.Clip("attack2", 18f, false, Shoot()),
                rig.Clip("skill1", 14f, false, Shoot(skyward: true)),
                rig.Clip("skill2", 14f, false, Dodge(-90f)),
                rig.Clip("dodge", 16f, false, Dodge(-90f)),
                rig.Clip("hurt", 10f, false, Hurt(arm, bow)),
                rig.Clip("death", 9f, false, Death(arm, 90f)),
            };
        }

        /// <summary>Raise the bow, draw, release.</summary>
        private static IEnumerable<RigPose> Shoot(bool skyward = false)
        {
            float aim = skyward ? 125f : 90f;
            float bow = aim - 90f;
            yield return new RigPose { ArmFront = aim - 30f, ArmBack = 20f, Torso = 2f, WeaponAbsolute = bow - 30f };
            yield return new RigPose { ArmFront = aim, ArmBack = aim - 10f, Torso = 4f, Head = 2f, LegFront = 12f, LegBack = -12f, WeaponAbsolute = bow };
            yield return new RigPose { ArmFront = aim, ArmBack = aim + 25f, Torso = 6f, Head = 2f, LegFront = 12f, LegBack = -12f, WeaponAbsolute = bow };
            yield return new RigPose { ArmFront = aim + 4f, ArmBack = aim + 40f, Torso = 2f, LegFront = 10f, LegBack = -10f, Shift = -1f, WeaponAbsolute = bow + 4f };
            yield return new RigPose { ArmFront = aim - 20f, ArmBack = 10f, Torso = 0f, WeaponAbsolute = bow - 20f };
        }
    }
}
