using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>Bosses of the chapel (Blood Bishop) and the abyss (Lich). 112-pixel frames like the Bone King.</summary>
    public static partial class EnemyArt
    {
        private static IEnumerable<RigPose> BossIdle(float arm, float armBack, float? weapon, int bobAmp = 1)
        {
            for (int i = 0; i < 8; i++)
            {
                float s = Mathf.Sin(i / 8f * Mathf.PI * 2f);
                yield return new RigPose
                {
                    Bob = Mathf.Round(bobAmp * s), Torso = -2f + 1.5f * s, Head = 2f * s,
                    ArmFront = arm + 3f * s, ArmBack = armBack - 3f * s, WeaponAbsolute = weapon + 3f * s, Cape = -6f - 3f * s,
                };
            }
        }

        private static IEnumerable<RigPose> BossGlide(float arm, float armBack, float? weapon, int bobAmp = 1)
        {
            for (int i = 0; i < 8; i++)
            {
                float s = Mathf.Sin(i / 8f * Mathf.PI * 2f);
                yield return new RigPose
                {
                    Bob = Mathf.Round(bobAmp * s), Torso = -8f + 2f * s, Head = 4f,
                    ArmFront = arm - 6f * s, ArmBack = armBack + 6f * s, WeaponAbsolute = weapon - 6f * s, Cape = -22f - 6f * Mathf.Abs(s),
                    LegFront = 10f * s, LegBack = -10f * s,
                };
            }
        }

        private static IEnumerable<RigPose> BossFall(float arm, float? weapon)
        {
            for (int i = 0; i < 7; i++)
            {
                float k = i / 6f;
                yield return new RigPose
                {
                    Bob = -6f * k, Torso = 25f * k, Head = 35f * k, ArmFront = arm + 50f * k, ArmBack = -40f - 30f * k,
                    WeaponAbsolute = weapon - 60f * k, Cape = 20f * k,
                    BodyAngle = i >= 5 ? 75f : 0f, BodyPivot = P(50f, 14f), BodyOffset = P(0f, i >= 5 ? -4f : 0f),
                };
            }
        }

        // ------------------------------------------------------------------ Blood Bishop

        public static List<Clip> BloodBishop()
        {
            var m = Palette.Standard();
            var none = new PixelPart("none", P(0f, 0f), m, 0, ".");
            var sleeve = new PixelPart("sleeve", P(3f, 13.5f), m, 0,
                ".dddd...",
                "dddddd..",
                "ddddddd.",
                "ddddddd.",
                ".dddddd.",
                ".dddddd.",
                ".ddddddd",
                ".ddddddd",
                "..dddddd",
                "..dddddd",
                "..gggggg",
                "...pppp.",
                "...pppp.",
                "....pp..");
            var rig = new HumanoidRig
            {
                Head = new PixelPart("mitre", P(6.5f, 0.5f), m, 0,
                    "......d......",
                    ".....ddd.....",
                    ".....dgd.....",
                    "....ddgdd....",
                    "....dgggd....",
                    "...dddgddd...",
                    "...dddgddd...",
                    "..ddddgdddd..",
                    "..ddgggggdd..",
                    "..ddddgdddd..",
                    "..ddddgdddd..",
                    "..ggggggggg..",
                    "..gggyggggg..",
                    "..ppppppppp..",
                    ".ppppppppppp.",
                    ".ppppppkkppp.",
                    ".pppppppkepp.",
                    ".ppppppppppp.",
                    ".ppppppppppk.",
                    "..pppppkkkp..",
                    "...ppppppp...",
                    "....iiiii...."),
                Torso = new PixelPart("robe", P(10f, 11.5f), m, 0,
                    "......iiiiiiii......",
                    ".....iiiiiiiiiid....",
                    "....ddiiiiiiiiddd...",
                    "...dddiiigiiiiddddd.",
                    "..ddddiiigiiidddddd.",
                    "..dddddiigiidddddddd",
                    ".ddddddiigiiddddddd.",
                    ".dddddddigidddddddd.",
                    ".ddddddddgddddddddd.",
                    ".dddddgggggggdddddd.",
                    ".ddddddddgddddddddd.",
                    ".ddddddddgddddddddd.",
                    ".ddddddddgddddddddd.",
                    "..dddddddgdddddddd..",
                    "..gggggggggggggggg..",
                    "..dddddddddddddddd..",
                    "..ddddddddgdddddddd.",
                    "..ddddddddgddddddddd",
                    ".dddddddddgdddddddd.",
                    ".dddddddddgddddddddd",
                    ".ddddddddddddddddddd",
                    "dddddddddddddddddddd",
                    "dddddddddddddddddddd",
                    "ddddddddddddddddddd.",
                    "dgddgddgddgddgddgdd.",
                    ".dd.ddd.ddd.ddd.dd.."),
                LegFront = none,
                LegBack = none,
                ArmFront = sleeve,
                ArmBack = sleeve,
                Weapon = new PixelPart("chalicestaff", P(4.5f, 12.5f), m, 0,
                    "...ddd...",
                    "..ddydd..",
                    "..ddddd..",
                    "..ggggg..",
                    "...ggg...",
                    "....g....",
                    "...ggg...",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "...ggg...",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "....g....",
                    "...ggg...",
                    "....g...."),
                Cape = new PixelPart("cape", P(12f, 23.5f), m, -1,
                    "......dddddddd",
                    "....dddddddddd",
                    "...ddddddddddd",
                    "..dddddddddddd",
                    "..ddddddddddd.",
                    ".dddddddddddd.",
                    ".ddddddddddd..",
                    ".ddddddddddd..",
                    "dddddddddddd..",
                    "ddddddddddd...",
                    "ddddddddddd...",
                    "dddddddddd....",
                    "dddddddddd....",
                    "ddddddddd.....",
                    "dddddddd......",
                    "ddddddd.......",
                    "dddddd........",
                    "dd.ddd........",
                    "d..dd.........",
                    "...d..........",
                    "..............",
                    "..............",
                    "..............",
                    ".............."),
                Hip = P(56f, 21f),
                NeckOnTorso = P(1f, 14f),
                ShoulderFront = P(4f, 12f),
                ShoulderBack = P(-4f, 12.5f),
                CapeOnTorso = P(-3f, 13f),
                ArmLength = 12f,
                WeaponReach = 30f,
                WeaponZ = 9,
                SmearColor = PixelMaterial.Hex("#cf3a3a"),
            };
            const float arm = 35f, armBack = -20f, staff = -5f;
            return new List<Clip>
            {
                rig.Clip("idle", 6.7f, true, BossIdle(arm, armBack, staff)),
                rig.Clip("walk", 9.3f, true, BossGlide(arm, armBack, staff)),
                rig.Clip("windup", 6f, false, new[]
                {
                    new RigPose { Torso = 4f, ArmFront = 120f, ArmBack = -90f, Head = 6f, WeaponAbsolute = 10f, Cape = 4f },
                    new RigPose { Torso = 10f, ArmFront = 165f, ArmBack = -140f, Head = 12f, Bob = 1f, WeaponAbsolute = 0f, Cape = 10f },
                    new RigPose { Torso = 12f, ArmFront = 175f, ArmBack = -155f, Head = 14f, Bob = 2f, WeaponAbsolute = -5f, Cape = 12f },
                }),
                rig.Clip("strike", 12f, false, new[]
                {
                    new RigPose { Torso = -10f, ArmFront = 110f, ArmBack = -60f, Shift = 3f, WeaponAbsolute = -50f, Cape = -12f },
                    new RigPose { Torso = -16f, ArmFront = 80f, ArmBack = -30f, Shift = 4f, Bob = -2f, WeaponAbsolute = -75f, Cape = -18f },
                    new RigPose { Torso = -12f, ArmFront = 70f, ArmBack = -25f, Shift = 4f, Bob = -2f, WeaponAbsolute = -70f, Cape = -16f },
                    new RigPose { Torso = -4f, ArmFront = 45f, ArmBack = -20f, Shift = 1f, WeaponAbsolute = -20f, Cape = -8f },
                }),
                rig.Clip("hurt", 10f, false, new[]
                {
                    new RigPose { Torso = 14f, Head = 14f, ArmFront = arm + 30f, ArmBack = -50f, Shift = -2f, WeaponAbsolute = staff + 25f, Cape = 8f },
                    new RigPose { Torso = 7f, Head = 7f, ArmFront = arm + 15f, ArmBack = -35f, Shift = -1f, WeaponAbsolute = staff + 12f, Cape = 4f },
                }),
                rig.Clip("death", 6f, false, BossFall(arm, staff)),
            };
        }

        // ------------------------------------------------------------------ Lich

        public static List<Clip> Lich()
        {
            var m = Palette.Standard();
            var none = new PixelPart("none", P(0f, 0f), m, 0, ".");
            var sleeve = new PixelPart("sleeve", P(3f, 13.5f), m, 0,
                ".vvvv...",
                "vvvvvv..",
                "vvvvvvv.",
                "vvvvvvv.",
                ".vvvvvv.",
                ".vvvvvv.",
                ".vvvvvvv",
                ".vvvvvvv",
                "..vvvvvv",
                "..vvvvvv",
                "..vgvgvv",
                "...bbbb.",
                "...bkbb.",
                "...b.b..");
            var rig = new HumanoidRig
            {
                Head = new PixelPart("crownskull", P(7.5f, 0.5f), m, 0,
                    "..g...g...g....",
                    "..gg.ggg.gg....",
                    "..g*gg*gg*g....",
                    "..gggggggggg...",
                    "..bbbbbbbbbbb..",
                    ".bbbbbbbbbbbbb.",
                    "bbbbbbbkkkbkkkb",
                    "bbbbbbkk*kbk*kb",
                    "bbbbbbbkkkbbkkb",
                    "bbbbbbbbbbkbbbb",
                    ".bbbbbbbbkkbbb.",
                    "..bbbbbbbbbbbb.",
                    "...bkbkbkbkbk..",
                    "...bbbbbbbbbb..",
                    "....bkbkbkbk...",
                    ".....bbbbbb....",
                    "....vvvvvvv...."),
                Torso = new PixelPart("robe", P(10f, 9.5f), m, 0,
                    ".....vvvvvvvvv......",
                    "....vvvvvvvvvvvv....",
                    "...vvvvbbbbbbvvvv...",
                    "..vvvvbkbbbkbbvvvv..",
                    "..vvvvbbbbbbbbvvvvv.",
                    ".vvvvvbkbbbkbbvvvvv.",
                    ".vvvvvbbbbbbbbvvvvv.",
                    ".vvvvvvbkbkbbvvvvvv.",
                    ".vvvvvvvbbbbvvvvvvv.",
                    ".vvvvvvvvgvvvvvvvvv.",
                    "..vvvvvvgygvvvvvvv..",
                    "..vvvvvvvgvvvvvvvv..",
                    "..ggggggggggggggggg.",
                    "..vvvvvvvvvvvvvvvv..",
                    "..vvvvvvvvvvvvvvvvv.",
                    ".vvvvvvvvvvvvvvvvvv.",
                    ".vvvvvvvvvvvvvvvvvvv",
                    ".vvvvvvvv.vvvvvvvvv.",
                    "vvvvvvv.vv.vvvvvvvv.",
                    "vvvvvv..vv..vvvvvvv.",
                    ".vvvv...v....vvvvv..",
                    ".vvv....v.....vvv...",
                    "..vv..........vv....",
                    "..v............v...."),
                LegFront = none,
                LegBack = none,
                ArmFront = sleeve,
                ArmBack = sleeve,
                Weapon = new PixelPart("soulstaff", P(4.5f, 12.5f), m, 0,
                    "...***...",
                    "..*+++*..",
                    ".*+++++*.",
                    ".*+++++*.",
                    "..*+++*..",
                    "...***...",
                    "..vgggv..",
                    "...ggg...",
                    "....n....",
                    "....n....",
                    "...nnn...",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "....n....",
                    "...nnn...",
                    "....n...."),
                Cape = new PixelPart("tatters", P(12f, 21.5f), m, -1,
                    "......vvvvvvvv",
                    "....vvvvvvvvvv",
                    "...vvvvvvvvvvv",
                    "..vvvvvvvvvvvv",
                    "..vvvvvvvvvvv.",
                    ".vvvvvvvvvvvv.",
                    ".vvvvvvvvvvv..",
                    ".vvvvvvvvvvv..",
                    "vvvvvvvvvvvv..",
                    "vvvvvvvvvvv...",
                    "vvvvvvvvvv....",
                    "vvvvvvv.vv....",
                    "vvvvvv..v.....",
                    "vvv.vv........",
                    "vv..v.........",
                    "v...v.........",
                    "..............",
                    "..............",
                    "..............",
                    "..............",
                    "..............",
                    ".............."),
                Hip = P(56f, 26f),
                NeckOnTorso = P(1f, 12f),
                ShoulderFront = P(4f, 10.5f),
                ShoulderBack = P(-4f, 11f),
                CapeOnTorso = P(-3f, 11f),
                ArmLength = 12f,
                WeaponReach = 28f,
                WeaponZ = 9,
                SmearColor = PixelMaterial.Hex("#8ff5ff"),
            };
            const float arm = 40f, armBack = -25f, staff = 0f;
            return new List<Clip>
            {
                rig.Clip("idle", 6.7f, true, BossIdle(arm, armBack, staff, 2)),
                rig.Clip("walk", 8f, true, BossGlide(arm, armBack, staff, 2)),
                rig.Clip("windup", 6f, false, new[]
                {
                    new RigPose { Torso = 4f, ArmFront = 120f, ArmBack = -100f, Head = 6f, Bob = 1f, WeaponAbsolute = 5f, Cape = 4f },
                    new RigPose { Torso = 10f, ArmFront = 165f, ArmBack = -150f, Head = 12f, Bob = 3f, WeaponAbsolute = 0f, Cape = 10f },
                    new RigPose { Torso = 12f, ArmFront = 172f, ArmBack = -160f, Head = 14f, Bob = 4f, WeaponAbsolute = -3f, Cape = 12f },
                }),
                rig.Clip("strike", 12f, false, new[]
                {
                    new RigPose { Torso = -10f, ArmFront = 100f, ArmBack = -40f, Shift = 2f, WeaponAbsolute = -60f, Cape = -12f },
                    new RigPose { Torso = -14f, ArmFront = 90f, ArmBack = -20f, Shift = 3f, Bob = -1f, WeaponAbsolute = -80f, Cape = -16f },
                    new RigPose { Torso = -12f, ArmFront = 85f, ArmBack = -20f, Shift = 3f, Bob = -1f, WeaponAbsolute = -75f, Cape = -14f },
                    new RigPose { Torso = -4f, ArmFront = 50f, ArmBack = -25f, Shift = 1f, WeaponAbsolute = -15f, Cape = -6f },
                }),
                rig.Clip("hurt", 10f, false, new[]
                {
                    new RigPose { Torso = 14f, Head = 16f, ArmFront = arm + 30f, ArmBack = -55f, Shift = -2f, WeaponAbsolute = staff + 25f, Cape = 8f },
                    new RigPose { Torso = 7f, Head = 8f, ArmFront = arm + 15f, ArmBack = -40f, Shift = -1f, WeaponAbsolute = staff + 12f, Cape = 4f },
                }),
                rig.Clip("death", 6f, false, BossFall(arm, staff)),
            };
        }
    }
}
