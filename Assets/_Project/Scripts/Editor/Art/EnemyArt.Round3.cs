using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>Each area's own monsters: the crypt's shield skeleton, the chapel's flagellant, the abyss eye and the shade stalker.</summary>
    public static partial class EnemyArt
    {
        // ------------------------------------------------------------------ Shield skeleton (crypt)

        public static List<Clip> ShieldSkeleton()
        {
            var m = Palette.Standard();
            // A tall tower shield of rusted iron with a gold boss, held edge-on in front.
            var shield = new PixelPart("shield", P(4.5f, 4.5f), m, 0,
                ".nnnnnnn.",
                "nmmmmmmmn",
                "nmuuuuumn",
                "nmummmumn",
                "nmumggumn",
                "nmumgGumn",
                "nmumggumn",
                "nmummmumn",
                "nmuuuuumn",
                "nmmmmmmmn",
                "nmmmmmmmn",
                ".nmmmmmn.",
                "..nmmmn..",
                "...nnn...");
            var rig = SkeletonRig(m, hooded: true, shield, 9);
            rig.SmearColor = PixelMaterial.Hex("#a9b1bb");
            const float lean = -6f, arm = 70f, hold = 0f;
            return new List<Clip>
            {
                rig.Clip("idle", 7.5f, true, Idle(arm, hold, lean)),
                rig.Clip("walk", 10.7f, true, Shamble(arm, hold, lean)),
                rig.Clip("windup", 9f, false, new[]
                {
                    new RigPose { Torso = lean + 10f, ArmFront = 50f, ArmBack = -30f, Head = 6f, Shift = -1f, WeaponAbsolute = 10f },
                    new RigPose { Torso = lean + 16f, ArmFront = 40f, ArmBack = -40f, Head = 10f, Shift = -2f, Bob = -1f, WeaponAbsolute = 14f },
                    new RigPose { Torso = lean + 18f, ArmFront = 36f, ArmBack = -44f, Head = 12f, Shift = -2f, Bob = -1f, WeaponAbsolute = 16f },
                }),
                rig.Clip("strike", 14f, false, new[]
                {
                    new RigPose { Torso = lean - 14f, ArmFront = 90f, ArmBack = 10f, LegFront = 28f, LegBack = -24f, Shift = 4f, WeaponAbsolute = -8f },
                    new RigPose { Torso = lean - 18f, ArmFront = 96f, ArmBack = 16f, LegFront = 30f, LegBack = -26f, Shift = 5f, Bob = -1f, WeaponAbsolute = -12f },
                    new RigPose { Torso = lean - 8f, ArmFront = 80f, ArmBack = 4f, LegFront = 14f, LegBack = -12f, Shift = 2f, WeaponAbsolute = -4f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(arm, hold, lean)),
                FromFrames("death", 11f, false, Collapse(rig, arm, hold, lean)),
            };
        }

        // ------------------------------------------------------------------ Flagellant (chapel)

        public static List<Clip> Flagellant()
        {
            var rig = HeroArt.MageRig(Palette.WithRobe(Palette.Crimson), glowingEyes: true);
            // A long scourge: a short grip and knotted leather thongs.
            rig.Weapon = new PixelPart("whip", P(1.5f, 1.5f), Palette.Standard(), 0,
                ".l.", "lll", ".l.", ".q.", ".q.", "q.q", ".q.", "q..", ".q.", "..q", ".q.", "q..", ".q.", "..q", ".d.", "d.d");
            rig.WeaponZ = 9;
            rig.WeaponReach = 16f;
            rig.SmearColor = PixelMaterial.Hex("#cf3a3a");
            return new List<Clip>
            {
                rig.Clip("idle", 9f, true, Idle(20f, -80f, 4f)),
                rig.Clip("walk", 13.3f, true, Shamble(20f, -80f, 0f)),
                rig.Clip("windup", 9f, false, new[]
                {
                    new RigPose { ArmFront = 140f, ArmBack = -60f, Torso = 10f, Head = 8f, WeaponAbsolute = 160f },
                    new RigPose { ArmFront = 190f, ArmBack = -80f, Torso = 16f, Head = 14f, Bob = -1f, WeaponAbsolute = 220f },
                    new RigPose { ArmFront = 205f, ArmBack = -90f, Torso = 18f, Head = 16f, Bob = -1f, WeaponAbsolute = 240f },
                }),
                rig.Clip("strike", 18f, false, new[]
                {
                    new RigPose { ArmFront = 120f, ArmBack = -20f, Torso = -14f, LegFront = 20f, LegBack = -18f, Shift = 2f, Smear = true, SmearFrom = 205f, SmearTo = 120f, WeaponAbsolute = 110f },
                    new RigPose { ArmFront = 50f, ArmBack = 10f, Torso = -22f, LegFront = 24f, LegBack = -20f, Shift = 3f, Smear = true, SmearFrom = 120f, SmearTo = 50f, WeaponAbsolute = 0f },
                    new RigPose { ArmFront = 40f, ArmBack = 10f, Torso = -18f, LegFront = 20f, LegBack = -16f, Shift = 3f, WeaponAbsolute = -10f },
                    new RigPose { ArmFront = 30f, ArmBack = 0f, Torso = -6f, Shift = 1f, WeaponAbsolute = -40f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(20f, -80f, 4f)),
                rig.Clip("death", 9f, false, CultistDeath()),
            };
        }

        // ------------------------------------------------------------------ Shade stalker (abyss)

        /// <summary>The wraith's body in shadow and violet, with burning eyes.</summary>
        public static List<Clip> ShadeStalker()
        {
            var m = Palette.Standard();
            m['j'] = Palette.Violet;
            m['t'] = Palette.Black;
            return WraithClips(m);
        }

        // ------------------------------------------------------------------ Abyss eye (abyss)

        public static List<Clip> AbyssEye()
        {
            var m = Palette.Standard();
            var empty = new PixelPart("none", P(0f, 0f), m, 0, ".");
            var tentacle = new PixelPart("tentacle", P(1.5f, 0.5f), m, 0, ".vv", "vvv", ".vv", ".vv", "vv.", "vv.", ".vv", ".vv", "..v", ".v.", "v..");
            var rig = new HumanoidRig
            {
                // A great eye in a fleshy violet lid.
                Head = new PixelPart("eye", P(8f, 7.5f), m, 0,
                    "....vvvvvvv.....",
                    "..vvvvvvvvvvv...",
                    ".vvviiiiiiiivv..",
                    "vvviiiiiiiiiivv.",
                    "vviiiiieeeiiiivv",
                    "vviiiieeeeeiiivv",
                    "vviiieeekkeeiivv",
                    "vviiieekkkeeiivv",
                    "vviiieeekkeeiivv",
                    "vviiiieeeeeiiivv",
                    "vviiiiieeeiiiivv",
                    "vvviiiiiiiiiivv.",
                    ".vvviiiiiiiivv..",
                    "..vvvvvvvvvvv...",
                    "....vvvvvvv....."),
                Torso = new PixelPart("lid", P(5f, 2.5f), m, 0,
                    "..vvvvvv..",
                    ".vvvvvvvv.",
                    "vvvvvvvvvv",
                    ".vv.vv.vv.",
                    ".v..v..v.."),
                LegFront = empty,
                LegBack = empty,
                ArmFront = tentacle,
                ArmBack = tentacle,
                Hip = P(32f, 22f),
                NeckOnTorso = P(0f, 9f),
                ShoulderFront = P(3f, 0f),
                ShoulderBack = P(-3f, 0f),
                ArmLength = 10f,
                WeaponReach = 4f,
                SmearColor = PixelMaterial.Hex("#7547a8"),
            };

            IEnumerable<RigPose> Hover(int frames, float sway)
            {
                for (int i = 0; i < frames; i++)
                {
                    float s = Mathf.Sin(i / (float)frames * Mathf.PI * 2f);
                    yield return new RigPose { Bob = Mathf.Round(2f * s), Head = 4f * s, ArmFront = 170f + sway * s, ArmBack = 190f - sway * s };
                }
            }

            var death = new List<RigPose>();
            for (int i = 0; i < 6; i++)
            {
                float k = i / 5f;
                death.Add(new RigPose { Bob = -12f * k, Head = 60f * k, ArmFront = 170f + 60f * k, ArmBack = 190f - 60f * k, BodyAngle = 40f * k, BodyPivot = P(32f, 18f) });
            }

            return new List<Clip>
            {
                rig.Clip("idle", 6f, true, Hover(6, 14f)),
                rig.Clip("walk", 8f, true, Hover(6, 22f)),
                rig.Clip("windup", 8f, false, new[]
                {
                    new RigPose { Bob = 1f, Head = -8f, ArmFront = 120f, ArmBack = 240f },
                    new RigPose { Bob = 2f, Head = -12f, ArmFront = 100f, ArmBack = 260f },
                    new RigPose { Bob = 2f, Head = -14f, ArmFront = 95f, ArmBack = 265f },
                }),
                rig.Clip("strike", 12f, false, new[]
                {
                    new RigPose { Bob = -1f, Head = 10f, ArmFront = 200f, ArmBack = 160f },
                    new RigPose { Bob = -2f, Head = 14f, ArmFront = 210f, ArmBack = 150f },
                    new RigPose { Bob = 0f, Head = 4f, ArmFront = 180f, ArmBack = 180f },
                }),
                rig.Clip("hurt", 10f, false, new[]
                {
                    new RigPose { Head = 25f, Shift = -2f, ArmFront = 140f, ArmBack = 220f },
                    new RigPose { Head = 10f, Shift = -1f, ArmFront = 160f, ArmBack = 200f },
                }),
                rig.Clip("death", 9f, false, death),
            };
        }
    }
}
