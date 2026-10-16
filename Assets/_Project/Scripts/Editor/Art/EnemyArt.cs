using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Enemy sheets. Clip order/names read by the runtime: idle, walk, windup, strike, hurt, death.
    /// "windup" ends on the telegraph pose (held while the warning shows), "strike" is the release.
    /// </summary>
    public static partial class EnemyArt
    {
        public const int FrameSize = 64;
        public const int BossFrameSize = 112;

        private static Vector2 P(float x, float y) => new(x, y);

        // ------------------------------------------------------------------ Skeleton parts

        private static PixelPart Skull(Dictionary<char, PixelMaterial> m, bool hooded) => hooded
            ? new PixelPart("skull", P(4.5f, 0.5f), m, 0,
                "..ttttt..",
                ".ttttttt.",
                "tttttttbt",
                "ttttbbbbb",
                "tttbbkebb",
                "tttbbbbbk",
                ".ttbbbbb.",
                "..tbkbkb.",
                "...bbbb..")
            : new PixelPart("skull", P(4.5f, 0.5f), m, 0,
                "..bbbbb..",
                ".bbbbbbb.",
                "bbbbbbbBb",
                "bbbbbkkbb",
                "bbbbbkebb",
                ".bbbbbbbk",
                "..bbbbbb.",
                "..bkbkbk.",
                "...bbbb..");

        private static PixelPart Ribs(Dictionary<char, PixelMaterial> m) => new("ribs", P(4.5f, 3.5f), m, 0,
            "...bbbb..",
            "..bbbbbb.",
            ".bbkbbkbb",
            ".bbbbbbbb",
            ".bkbbbkb.",
            "..bbbbbb.",
            "...kbbk..",
            "....bb...",
            "....bb...",
            "..uuuuu..",
            ".ttttttt.",
            ".tt.t.tt.");

        private static PixelPart BoneLeg(Dictionary<char, PixelMaterial> m) => new("leg", P(1.5f, 9.5f), m, 0,
            ".bb.", ".bb.", ".bb.", ".bb.", "bbbb", ".bb.", ".bb.", ".bb.", ".bbb", "bbbb");

        private static PixelPart BoneArm(Dictionary<char, PixelMaterial> m) => new("arm", P(1.5f, 8.5f), m, 0,
            "bbb", ".b.", ".b.", ".b.", "bbb", ".b.", ".b.", ".b.", "bb.");

        private static HumanoidRig SkeletonRig(Dictionary<char, PixelMaterial> m, bool hooded, PixelPart weapon, int weaponZ)
        {
            return new HumanoidRig
            {
                Head = Skull(m, hooded),
                Torso = Ribs(m),
                LegFront = BoneLeg(m),
                LegBack = BoneLeg(m),
                ArmFront = BoneArm(m),
                ArmBack = BoneArm(m),
                Weapon = weapon,
                WeaponZ = weaponZ,
                NeckOnTorso = P(1f, 8f),
                ShoulderFront = P(1.5f, 6.5f),
                ShoulderBack = P(-1.5f, 6.5f),
                Hip = P(32f, 20f),
                LegSpread = 1f,
                WeaponReach = 14f,
                SmearColor = PixelMaterial.Hex("#b58a5a"),
            };
        }

        private static IEnumerable<RigPose> Shamble(float arm, float? weapon, float lean)
        {
            // Eight frames of a dragging, lurching walk.
            for (int i = 0; i < 8; i++)
            {
                float s = Mathf.Sin(i / 8f * Mathf.PI * 2f);
                yield return new RigPose
                {
                    Bob = Mathf.Abs(s) < 0.4f ? 1f : 0f,
                    Torso = lean - 3f * s,
                    Head = 6f * s,
                    LegFront = 26f * s,
                    LegBack = -26f * s,
                    ArmBack = 22f * s,
                    ArmFront = arm - 10f * s,
                    WeaponAbsolute = weapon.HasValue ? weapon.Value - 10f * s : null,
                };
            }
        }

        private static IEnumerable<RigPose> Idle(float arm, float? weapon, float lean)
        {
            for (int i = 0; i < 6; i++)
            {
                float b = 0.5f - 0.5f * Mathf.Cos(i / 6f * Mathf.PI * 2f);
                float k = b > 0.5f ? 1f : b;
                yield return new RigPose
                {
                    Bob = b > 0.5f ? -1f : 0f,
                    Torso = lean - 2f * k,
                    Head = i == 1 ? 8f : i == 4 ? -6f : 0f, // twitchy head
                    ArmFront = arm + 4f * k,
                    ArmBack = -6f,
                    WeaponAbsolute = weapon.HasValue ? weapon.Value + 4f * k : null,
                };
            }
        }

        private static IEnumerable<RigPose> Hurt(float arm, float? weapon, float lean)
        {
            yield return new RigPose { Torso = lean + 24f, Head = 28f, ArmFront = arm + 45f, ArmBack = -55f, LegFront = 12f, Shift = -2f, Bob = 1f, WeaponAbsolute = weapon + 45f };
            yield return new RigPose { Torso = lean + 15f, Head = 16f, ArmFront = arm + 26f, ArmBack = -36f, LegFront = 6f, Shift = -2f, WeaponAbsolute = weapon + 26f };
            yield return new RigPose { Torso = lean + 6f, Head = 5f, ArmFront = arm + 9f, ArmBack = -15f, Shift = -1f, Bob = -1f, WeaponAbsolute = weapon + 9f };
        }

        /// <summary>The skeleton falls apart: bones drop to the floor, the skull rolls.</summary>
        private static List<Frame> Collapse(HumanoidRig rig, float arm, float? weapon, float lean)
        {
            var frames = new List<Frame>();
            float[] drop = { 0f, 3f, 7f, 10f, 11f, 11f, 11f };
            for (int i = 0; i < drop.Length; i++)
            {
                float k = i / (drop.Length - 1f);
                var f = new Frame();
                float d = drop[i];
                f.Add(rig.LegBack, P(30f - 4f * k, 20f - d * 0.9f), -20f - 70f * k, 2, -1);
                f.Add(rig.LegFront, P(34f + 3f * k, 20f - d * 0.9f), 25f + 65f * k, 3);
                f.Add(rig.Torso, P(32f - 2f * k, 20f - d), lean + 40f * k, 5);
                f.Add(rig.ArmBack, P(30f - 7f * k, 26f - d * 1.6f), -40f - 50f * k, 1, -1);
                f.Add(rig.ArmFront, P(34f + 6f * k, 26f - d * 1.6f), arm + 80f * k, 8);
                // Skull pops up, then rolls away.
                float hop = i < 3 ? i * 2f : Mathf.Max(0f, 6f - (i - 2) * 4f);
                f.Add(rig.Head, P(33f + 9f * k, 29f - d * 1.9f + hop), -120f * k, 6);
                if (rig.Weapon != null) f.Add(rig.Weapon, P(38f + 6f * k, 18f - d * 0.6f), (weapon ?? 0f) - 95f * k, 4);
                frames.Add(f);
            }
            return frames;
        }

        private static Clip FromFrames(string name, float fps, bool loop, List<Frame> frames)
        {
            var clip = new Clip(name, fps, loop);
            clip.Frames.AddRange(frames);
            return clip;
        }

        // ------------------------------------------------------------------ Skeleton warrior

        public static List<Clip> Skeleton()
        {
            var m = Palette.Standard();
            var cleaver = new PixelPart("cleaver", P(2.5f, 2.5f), m, 0,
                ".mmm.",
                "mmmmu",
                "mmMmm",
                "mumMm",
                ".mmmm",
                "mmmu.",
                ".mmm.",
                ".mMu.",
                ".mmm.",
                ".mmm.",
                "kkkkk",
                "..l..",
                "..l..",
                "..l..");
            var rig = SkeletonRig(m, hooded: false, cleaver, 7);
            const float lean = -12f, arm = 20f, weapon = -35f;
            return new List<Clip>
            {
                rig.Clip("idle", 9f, true, Idle(arm, weapon, lean)),
                rig.Clip("walk", 12f, true, Shamble(arm, weapon, lean)),
                rig.Clip("windup", 10f, false, new[]
                {
                    new RigPose { Torso = lean + 8f, ArmFront = 120f, ArmBack = -30f, Head = 6f },
                    new RigPose { Torso = lean + 16f, ArmFront = 190f, ArmBack = -45f, Head = 12f, Bob = -1f },
                    new RigPose { Torso = lean + 18f, ArmFront = 205f, ArmBack = -50f, Head = 14f, Bob = -1f },
                }),
                rig.Clip("strike", 16f, false, new[]
                {
                    new RigPose { Torso = lean - 10f, ArmFront = 120f, ArmBack = 20f, LegFront = 25f, LegBack = -20f, Shift = 2f, Smear = true, SmearFrom = 205f, SmearTo = 120f },
                    new RigPose { Torso = lean - 20f, ArmFront = 40f, ArmBack = 30f, LegFront = 30f, LegBack = -25f, Shift = 3f, Bob = -1f, Smear = true, SmearFrom = 120f, SmearTo = 40f },
                    new RigPose { Torso = lean - 16f, ArmFront = 30f, ArmBack = 20f, LegFront = 25f, LegBack = -20f, Shift = 3f, Bob = -1f },
                    new RigPose { Torso = lean - 6f, ArmFront = 25f, ArmBack = 5f, LegFront = 10f, LegBack = -10f, Shift = 1f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(arm, weapon, lean)),
                FromFrames("death", 11f, false, Collapse(rig, arm, weapon, lean)),
            };
        }

        // ------------------------------------------------------------------ Skeleton archer

        public static List<Clip> SkeletonArcher()
        {
            var m = Palette.Standard();
            var bow = new PixelPart("bonebow", P(3.5f, 7.5f), m, 0,
                ".b...",
                ".qb..",
                ".q.b.",
                ".q..b",
                ".q..b",
                ".q.lb",
                ".q.lb",
                ".q.lb",
                ".q..b",
                ".q..b",
                ".q.b.",
                ".qb..",
                ".b...");
            var rig = SkeletonRig(m, hooded: true, bow, 9);
            const float lean = -6f, arm = 25f, idleBow = -10f;
            return new List<Clip>
            {
                rig.Clip("idle", 9f, true, Idle(arm, idleBow, lean)),
                rig.Clip("walk", 12f, true, Shamble(arm, idleBow, lean)),
                rig.Clip("windup", 8f, false, new[]
                {
                    new RigPose { Torso = lean + 2f, ArmFront = 70f, ArmBack = 40f, WeaponAbsolute = -20f },
                    new RigPose { Torso = lean + 4f, ArmFront = 90f, ArmBack = 95f, WeaponAbsolute = 0f, Head = 3f },
                    new RigPose { Torso = lean + 6f, ArmFront = 90f, ArmBack = 125f, WeaponAbsolute = 0f, Head = 3f, Shift = -1f },
                }),
                rig.Clip("strike", 14f, false, new[]
                {
                    new RigPose { Torso = lean + 2f, ArmFront = 94f, ArmBack = 140f, WeaponAbsolute = 4f, Shift = -1f },
                    new RigPose { Torso = lean, ArmFront = 85f, ArmBack = 60f, WeaponAbsolute = -5f },
                    new RigPose { Torso = lean, ArmFront = 50f, ArmBack = 10f, WeaponAbsolute = -15f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(arm, idleBow, lean)),
                FromFrames("death", 11f, false, Collapse(rig, arm, idleBow, lean)),
            };
        }

        // ------------------------------------------------------------------ Ghoul

        public static List<Clip> Ghoul()
        {
            var m = Palette.Standard();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("head", P(3.5f, 1.5f), m, 0,
                    "..xxxx..",
                    ".xxxxxx.",
                    "xxxxxxxx",
                    "xxxxxkex",
                    "xxxxxxxk",
                    ".xxxxbkb",
                    "..xxbkb."),
                Torso = new PixelPart("body", P(5f, 3.5f), m, 0,
                    "...xxxxx..",
                    "..xxxxxxxx",
                    ".xxxkxxxxx",
                    ".xxxxxkxxx",
                    "..xxkxxxx.",
                    "..xxxxxxx.",
                    "...xxxxx..",
                    "..ttttttt.",
                    "..tt.tt.t."),
                LegFront = new PixelPart("leg", P(1.5f, 8.5f), m, 0,
                    "xxx.", "xxx.", ".xx.", "xxx.", "xx..", ".xx.", ".xx.", ".xxx", "xbxb"),
                LegBack = new PixelPart("leg", P(1.5f, 8.5f), m, 0,
                    "xxx.", "xxx.", ".xx.", "xxx.", "xx..", ".xx.", ".xx.", ".xxx", "xbxb"),
                ArmFront = new PixelPart("arm", P(1.5f, 11.5f), m, 0,
                    ".xx", "xxx", "xxx", ".xx", ".xx", ".xx", "xxx", ".xx", ".xx", ".xx", "bxb", "b.b"),
                ArmBack = new PixelPart("arm", P(1.5f, 11.5f), m, 0,
                    ".xx", "xxx", "xxx", ".xx", ".xx", ".xx", "xxx", ".xx", ".xx", ".xx", "bxb", "b.b"),
                NeckOnTorso = P(3f, 5f),
                ShoulderFront = P(2f, 4.5f),
                ShoulderBack = P(-1f, 5f),
                Hip = P(32f, 18f),
                ArmLength = 11f,
                WeaponReach = 4f,
                SmearColor = PixelMaterial.Hex("#9ca37a"),
            };
            const float lean = -30f;
            var clips = new List<Clip>
            {
                rig.Clip("idle", 10.5f, true, Idle(35f, null, lean)),
                rig.Clip("walk", 13f, true, Gallop(lean)),
                rig.Clip("windup", 10f, false, new[]
                {
                    new RigPose { Torso = lean + 10f, ArmFront = 120f, ArmBack = 100f, Bob = -2f, LegFront = 30f, LegBack = -20f },
                    new RigPose { Torso = lean + 18f, ArmFront = 160f, ArmBack = 140f, Bob = -3f, LegFront = 40f, LegBack = -30f, Head = 15f },
                    new RigPose { Torso = lean + 20f, ArmFront = 170f, ArmBack = 150f, Bob = -3f, LegFront = 42f, LegBack = -32f, Head = 18f },
                }),
                rig.Clip("strike", 16f, false, new[]
                {
                    new RigPose { Torso = lean - 25f, ArmFront = 100f, ArmBack = 80f, LegFront = 60f, LegBack = -60f, Bob = 2f, Shift = 3f, Smear = true, SmearFrom = 170f, SmearTo = 100f },
                    new RigPose { Torso = lean - 30f, ArmFront = 60f, ArmBack = 50f, LegFront = 55f, LegBack = -55f, Bob = 1f, Shift = 4f, Smear = true, SmearFrom = 100f, SmearTo = 50f },
                    new RigPose { Torso = lean - 15f, ArmFront = 40f, ArmBack = 30f, LegFront = 25f, LegBack = -25f, Shift = 3f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(35f, null, lean)),
                rig.Clip("death", 10f, false, GhoulDeath(lean)),
            };
            return clips;
        }

        private static IEnumerable<RigPose> Gallop(float lean)
        {
            for (int i = 0; i < 6; i++)
            {
                float s = Mathf.Sin(i / 6f * Mathf.PI * 2f);
                yield return new RigPose
                {
                    Bob = Mathf.Abs(s) > 0.7f ? 1f : 0f,
                    Torso = lean - 6f * s,
                    Head = 10f * s,
                    LegFront = 40f * s,
                    LegBack = -40f * s,
                    ArmFront = 60f - 40f * s,
                    ArmBack = 60f + 40f * s,
                };
            }
        }

        private static IEnumerable<RigPose> GhoulDeath(float lean)
        {
            float[] fall = { 0f, -20f, -50f, -80f, -95f, -95f };
            for (int i = 0; i < fall.Length; i++)
            {
                float k = i / (fall.Length - 1f);
                yield return new RigPose
                {
                    Torso = lean, Head = 20f * k, ArmFront = 40f + 100f * k, ArmBack = 30f + 60f * k,
                    LegFront = 20f * k, LegBack = -30f * k,
                    BodyAngle = fall[i], BodyPivot = P(36f, 10f), BodyOffset = P(0f, -2f * k),
                };
            }
        }

        // ------------------------------------------------------------------ Cultist (caster)

        public static List<Clip> Cultist()
        {
            var rig = HeroArt.MageRig(Palette.WithRobe(Palette.CultRobe), glowingEyes: true);
            rig.Weapon = new PixelPart("dagger", P(1.5f, 2.5f), Palette.Standard(), 0,
                ".w.", "wWw", "wWw", "wWw", "www", "kkk", ".l.", ".l.");
            rig.WeaponZ = 9;
            rig.SmearColor = PixelMaterial.Hex("#ff8a2a");
            return new List<Clip>
            {
                rig.Clip("idle", 9f, true, Idle(25f, -60f, -4f)),
                rig.Clip("walk", 12f, true, Shamble(25f, -60f, -6f)),
                rig.Clip("windup", 8f, false, new[]
                {
                    new RigPose { ArmFront = 100f, ArmBack = -90f, Torso = 6f, Head = 8f, WeaponTilt = 0f },
                    new RigPose { ArmFront = 150f, ArmBack = -140f, Torso = 10f, Head = 14f, Bob = 1f },
                    new RigPose { ArmFront = 165f, ArmBack = -160f, Torso = 12f, Head = 16f, Bob = 2f },
                }),
                rig.Clip("strike", 12f, false, new[]
                {
                    new RigPose { ArmFront = 90f, ArmBack = -80f, Torso = -10f, LegFront = 15f, LegBack = -15f },
                    new RigPose { ArmFront = 70f, ArmBack = -60f, Torso = -12f, LegFront = 15f, LegBack = -15f },
                    new RigPose { ArmFront = 40f, ArmBack = -20f, Torso = -4f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(25f, -60f, -4f)),
                rig.Clip("death", 9f, false, CultistDeath()),
            };
        }

        private static IEnumerable<RigPose> CultistDeath()
        {
            float[] fall = { 0f, 8f, 25f, 55f, 80f, 90f };
            for (int i = 0; i < fall.Length; i++)
            {
                float k = i / (fall.Length - 1f);
                yield return new RigPose
                {
                    Torso = 10f * k, Head = 25f * k, ArmFront = 25f + 140f * k, ArmBack = -30f - 100f * k,
                    BodyAngle = fall[i], BodyPivot = P(29f, 10f), BodyOffset = P(0f, i >= 4 ? -2f : 0f),
                };
            }
        }

        // ------------------------------------------------------------------ Boss: the Bone King

        public static List<Clip> BoneKing()
        {
            var m = Palette.Standard();
            var rig = new HumanoidRig
            {
                Head = new PixelPart("crownskull", P(7.5f, 0.5f), m, 0,
                    "..g..g..g..g...",
                    "..gg.gg.gg.gg..",
                    "..ggggggggggg..",
                    "..gygggyggggy..",
                    ".bbbbbbbbbbbbb.",
                    "bbbbbbbbbbbbBbb",
                    "bbbbbbbbkkkbbbb",
                    "bbbbbbbkkeekbbb",
                    "bbbbbbbkkEekbbb",
                    "bbbbbbbbkkkbbbb",
                    ".bbbbbbbbbbbbkb",
                    "..bbbbbbbbbbbb.",
                    "..bkbkbkbkbkb..",
                    "...bbbbbbbbb...",
                    "....bbbbbbb...."),
                Torso = new PixelPart("armor", P(9f, 8.5f), m, 0,
                    "....nnnnnnnnn.....",
                    "..nnnnnnnnnnnnnn..",
                    ".nnnnnnnNNnnnnnnnn",
                    "nnnnnnnnnnnnnnnnnn",
                    "nnnngnnnnnnnngnnnn",
                    "nnnnnnbbkbbbnnnnn.",
                    ".nnnnbkbbbkbbnnnn.",
                    ".nnnnbbbbbbbbnnnn.",
                    "..nnnnbkbbkbnnnn..",
                    "..nnnnnbbbbnnnnn..",
                    "...nnggggggggnn...",
                    "...nnnyggggynnn...",
                    "..cccccccccccccc..",
                    "..ccccccccccccccc.",
                    ".cccccc.cccc.cccc.",
                    ".ccc.cc..cc...ccc.",
                    ".cc...c...c....cc."),
                LegFront = new PixelPart("leg", P(3f, 15.5f), m, 0,
                    "nnnnn.", "nnnnn.", "nnnnn.", ".bbb..", ".bbb..", "nnnnn.", "nNnnn.", "nnnnn.",
                    ".bbb..", ".bbb..", ".bbb..", "nnnnn.", "nnnnnn", "nnnnnn", "nnnnnn", "kkkkkk"),
                LegBack = new PixelPart("leg", P(3f, 15.5f), m, 0,
                    "nnnnn.", "nnnnn.", "nnnnn.", ".bbb..", ".bbb..", "nnnnn.", "nNnnn.", "nnnnn.",
                    ".bbb..", ".bbb..", ".bbb..", "nnnnn.", "nnnnnn", "nnnnnn", "nnnnnn", "kkkkkk"),
                ArmFront = new PixelPart("arm", P(3.5f, 13.5f), m, 0,
                    ".nnnnn.", "nnnnnnn", "nNnnnnn", "nnnnnnn", ".nnnnn.", "..bbb..", "..bbb..", "..bbb..",
                    ".nnnnn.", ".nnnnn.", "..bbb..", "..bbb..", ".bbbbb.", ".bkbkb."),
                ArmBack = new PixelPart("arm", P(3.5f, 13.5f), m, 0,
                    ".nnnnn.", "nnnnnnn", "nNnnnnn", "nnnnnnn", ".nnnnn.", "..bbb..", "..bbb..", "..bbb..",
                    ".nnnnn.", ".nnnnn.", "..bbb..", "..bbb..", ".bbbbb.", ".bkbkb."),
                Weapon = new PixelPart("greatsword", P(3.5f, 5.5f), m, 0,
                    "...n...",
                    "..nnn..",
                    "..nMn..",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMn..",
                    ".nnMnu.",
                    ".nnMnu.",
                    "..nMnn.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnMnu.",
                    ".nnknn.",
                    ".nnknn.",
                    "ggggggg",
                    "gnnynng",
                    "...l...",
                    "...l...",
                    "...l...",
                    "...l...",
                    "..ggg..",
                    "...g..."),
                Cape = new PixelPart("cape", P(12f, 21.5f), m, -1,
                    "......tttttttt",
                    "....tttttttttt",
                    "...ttttttttttt",
                    "..tttttttttttt",
                    "..ttttttttttt.",
                    ".tttttttttttt.",
                    ".ttttttttttt..",
                    ".ttttttttttt..",
                    "tttttttttttt..",
                    "ttttttttttt...",
                    "ttttttttttt...",
                    "tttttttttt....",
                    "tttttttttt....",
                    "ttttttttt.....",
                    "tttt.tttt.....",
                    "ttt..ttt......",
                    "tt...tt.......",
                    "t....t........",
                    "t....t........",
                    ".....t........",
                    "..............",
                    ".............."),
                Hip = P(56f, 26f),
                LegSpread = 3f,
                NeckOnTorso = P(1f, 9f),
                ShoulderFront = P(4f, 6.5f),
                ShoulderBack = P(-4f, 7f),
                CapeOnTorso = P(-2f, 8f),
                ArmLength = 12f,
                WeaponReach = 26f,
                SmearColor = PixelMaterial.Hex("#c9a46a"),
            };

            const float lean = -4f, arm = 15f, sword = -30f;
            var deathFrames = new List<RigPose>();
            float[] kneel = { 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            for (int i = 0; i < kneel.Length; i++)
            {
                float k = i / (kneel.Length - 1f);
                deathFrames.Add(new RigPose
                {
                    Bob = -6f * k, Torso = lean + 25f * k, Head = 35f * k, LegFront = 70f * k, LegBack = -15f * k,
                    ArmFront = arm + 40f * k, ArmBack = -30f - 30f * k, WeaponAbsolute = sword - 50f * k,
                    BodyAngle = i >= 5 ? 70f : 0f, BodyPivot = P(50f, 14f), BodyOffset = P(0f, i >= 5 ? -4f : 0f), Cape = 20f * k,
                });
            }

            return new List<Clip>
            {
                rig.Clip("idle", 7.5f, true, Idle(arm, sword, lean)),
                rig.Clip("walk", 10.7f, true, Shamble(arm, sword, lean)),
                rig.Clip("windup", 7f, false, new[]
                {
                    new RigPose { Torso = lean + 6f, ArmFront = 120f, ArmBack = 60f, Head = 6f, Cape = 4f },
                    new RigPose { Torso = lean + 14f, ArmFront = 185f, ArmBack = 120f, Head = 14f, Bob = 1f, Cape = 10f },
                    new RigPose { Torso = lean + 16f, ArmFront = 200f, ArmBack = 140f, Head = 16f, Bob = 2f, Cape = 12f },
                }),
                rig.Clip("strike", 14f, false, new[]
                {
                    new RigPose { Torso = lean - 10f, ArmFront = 120f, ArmBack = 40f, LegFront = 20f, LegBack = -20f, Shift = 3f, Smear = true, SmearFrom = 200f, SmearTo = 120f, Cape = -10f },
                    new RigPose { Torso = lean - 22f, ArmFront = 45f, ArmBack = 10f, LegFront = 30f, LegBack = -25f, Shift = 5f, Bob = -3f, Smear = true, SmearFrom = 120f, SmearTo = 45f, Cape = -20f },
                    new RigPose { Torso = lean - 20f, ArmFront = 40f, ArmBack = 10f, LegFront = 30f, LegBack = -25f, Shift = 5f, Bob = -3f, Cape = -20f },
                    new RigPose { Torso = lean - 8f, ArmFront = 30f, ArmBack = 0f, LegFront = 15f, LegBack = -12f, Shift = 2f, Cape = -8f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(arm, sword, lean)),
                rig.Clip("death", 6f, false, deathFrames),
            };
        }
    }
}
