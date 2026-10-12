using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Monsters added in round 2: vampire bats, bloaters, dread knights, necromancers, wraiths, the powder keg prop,
    /// and the bosses of areas 2 and 3 (Blood Bishop, Lich). Same clip names as the other enemies.
    /// </summary>
    public static partial class EnemyArt
    {
        // ------------------------------------------------------------------ Vampire bat (custom frames)

        public static List<Clip> Bat()
        {
            // Seen from the front with wings spread: reads instantly as a bat in a top-down view.
            var m = Palette.With(
                ('@', new PixelMaterial(2, "#0c080b", "#1a1117", "#2a1b24", "#3d2834", "#553846")),
                ('&', new PixelMaterial(2, "#2a1018", "#4a1a26", "#6e2a38", "#94404e", "#b85a66")));
            var body = new PixelPart("body", P(3.5f, 5.5f), m, 0,
                "@.....@",
                "@@...@@",
                "@@@@@@@",
                "@e@@@e@",
                "@@@@@@@",
                ".@b@b@.",
                ".@@@@@.",
                "@@@@@@@",
                "@@@@@@@",
                ".@@@@@.",
                "..@.@..");
            string[] right =
            {
                "..............@.",
                "........@@@@@@&@",
                "...@@@@@&&&&&&&@",
                "@@@&&&&&&&&&&&&@",
                "@&&&&&&&&&&&&&@.",
                ".@&&&&@&&&&@&&@.",
                "..@&&@..@&&@.@..",
                "...@@....@@.....",
            };
            var left = new string[right.Length];
            for (int i = 0; i < right.Length; i++)
            {
                var chars = right[i].ToCharArray();
                System.Array.Reverse(chars);
                left[i] = new string(chars);
            }
            var wingR = new PixelPart("wingR", P(0.5f, 4.5f), m, 0, right);
            var wingL = new PixelPart("wingL", P(15.5f, 4.5f), m, 0, left);

            Frame Pose(float y, float wing, float tilt = 0f, float fall = 0f)
            {
                var f = new Frame { BodyAngle = tilt, BodyPivot = P(32f, y), BodyOffset = P(0f, -fall) };
                f.Add(wingL, P(30f, y + 2f), -wing, 3, -1);
                f.Add(wingR, P(34f, y + 2f), wing, 3);
                f.Add(body, P(32f, y), 0f, 5);
                return f;
            }

            Clip Flap(string name, float fps)
            {
                var clip = new Clip(name, fps, true);
                float[] wings = { 40f, 20f, -5f, -30f, -10f, 20f };
                float[] bob = { 0f, 0f, 1f, 2f, 1f, 0f };
                for (int i = 0; i < wings.Length; i++) clip.Frames.Add(Pose(30f + bob[i], wings[i]));
                return clip;
            }

            var windup = new Clip("windup", 10f, false);
            windup.Frames.Add(Pose(31f, 50f));
            windup.Frames.Add(Pose(33f, 65f));
            windup.Frames.Add(Pose(34f, 70f));
            var strike = new Clip("strike", 14f, false);
            strike.Frames.Add(Pose(27f, -55f, -15f));
            strike.Frames.Add(Pose(26f, -60f, -20f));
            strike.Frames.Add(Pose(28f, -20f, -5f));
            var hurt = new Clip("hurt", 10f, false);
            hurt.Frames.Add(Pose(32f, 60f, 20f));
            hurt.Frames.Add(Pose(31f, 30f, 8f));
            var death = new Clip("death", 10f, false);
            float[] fall = { 0f, 4f, 9f, 14f, 17f, 18f };
            for (int i = 0; i < fall.Length; i++) death.Frames.Add(Pose(30f, -40f - i * 6f, 30f + i * 25f, fall[i]));

            return new List<Clip> { Flap("idle", 12f), Flap("walk", 16f), windup, strike, hurt, death };
        }

        // ------------------------------------------------------------------ Bloater (swells up, then bursts)

        public static List<Clip> Bloater()
        {
            var m = Palette.Standard();
            PixelPart Belly(string name, int grow) => grow switch
            {
                0 => new PixelPart(name, P(8f, 3.5f), m, 0,
                    "....xxxxxxx.....",
                    "..xxxxxxxxxxx...",
                    ".xxxxx%xxxxxxx..",
                    ".xxxxxxxxxx%xxx.",
                    "xxxx%xxxxxxxxxxx",
                    "xxxxxxxkxxxxx%xx",
                    "xxxxxxkxkxxxxxxx",
                    "xxxxxkxxxkxxxxxx",
                    "xx%xxxxxxxxxxxxx",
                    "xxxxxxxxxxx%xxxx",
                    "xxxxxxxxxxxxxxx.",
                    ".xxxx%xxxxxxxxx.",
                    ".xxxxxxxxxxxxx..",
                    "..xxxxxxxxxxx...",
                    "....xxxxxxx....."),
                1 => new PixelPart(name, P(9f, 3.5f), m, 0,
                    ".....xxxxxxxx.....",
                    "...xxxxxxxxxxxx...",
                    "..xxxxxx%xxxxxxx..",
                    ".xxxxxxxxxxxx%xxx.",
                    ".xxxx%xxxxxxxxxxx.",
                    "xxxxxxxxkxxxxxx%xx",
                    "xxxxxxxkxkxxxxxxxx",
                    "xxxxxxk!xxkxxxxxxx",
                    "xxx%xxxxxxxxxxxxxx",
                    "xxxxxxxxxxxx%xxxxx",
                    "xxxxxxxxxxxxxxxxxx",
                    "xxxxx%xxxxxxxxxxx.",
                    ".xxxxxxxxxxx!xxxx.",
                    ".xxxxxxxxxxxxxxxx.",
                    "..xxxxxxxxxxxxxx..",
                    "...xxxxxxxxxxxx...",
                    ".....xxxxxxxx....."),
                _ => new PixelPart(name, P(10f, 3.5f), m, 1,
                    "......xxxxxxxx......",
                    "....xxxxxxxxxxxx....",
                    "..xxxxxxx%xxxxxxxx..",
                    ".xxxx!xxxxxxxxx%xxx.",
                    ".xxxxxxxxxxxxxxxxxx.",
                    "xxxxx%xxxkxxxxxxxxxx",
                    "xxxxxxxxk!kxxxxx%xxx",
                    "xxxxxxxk!x!kxxxxxxxx",
                    "xxxx%xxxxxxxxxxxxxxx",
                    "xxxxxxxxxxxxx!xxxxxx",
                    "xxxxxxxxxxxxxxxxxxxx",
                    "xx!xxx%xxxxxxxxxxxx.",
                    ".xxxxxxxxxxxx%xxxxx.",
                    ".xxxxxxxxxxxxxxxxxx.",
                    "..xxxxxx!xxxxxxxxx..",
                    "...xxxxxxxxxxxxxx...",
                    ".....xxxxxxxxxx.....",
                    ".......xxxxxx......."),
            };

            HumanoidRig Rig(int grow) => new()
            {
                Head = new PixelPart("head", P(4f, 0.5f), m, 0,
                    "..xxxx..",
                    ".xxxxxxx",
                    "xxxxxk!x",
                    "xxxxxxxx",
                    "xxxxkkkx",
                    ".xxk%kx.",
                    "..xxxx.."),
                Torso = Belly("belly" + grow, grow),
                LegFront = new PixelPart("leg", P(2f, 6.5f), m, 0, "xxxx.", "xxxx.", ".xxx.", ".xxx.", ".xxx.", "xxxx.", "kkkkk"),
                LegBack = new PixelPart("leg", P(2f, 6.5f), m, 0, "xxxx.", "xxxx.", ".xxx.", ".xxx.", ".xxx.", "xxxx.", "kkkkk"),
                ArmFront = new PixelPart("arm", P(1.5f, 7.5f), m, 0, "xxx.", "xxxx", ".xxx", ".xxx", ".xx.", ".xx.", "xxx.", "x.x."),
                ArmBack = new PixelPart("arm", P(1.5f, 7.5f), m, 0, "xxx.", "xxxx", ".xxx", ".xxx", ".xx.", ".xx.", "xxx.", "x.x."),
                Hip = P(32f, 17f),
                LegSpread = 3f,
                NeckOnTorso = P(3f, 10f + grow),
                ShoulderFront = P(5f + grow, 7f),
                ShoulderBack = P(-5f - grow, 7f),
                ArmLength = 7f,
                WeaponReach = 4f,
                SmearColor = PixelMaterial.Hex("#b6ff4a"),
            };

            var r0 = Rig(0);
            var r1 = Rig(1);
            var r2 = Rig(2);
            var idle = r0.Clip("idle", 5f, true, new[]
            {
                new RigPose { ArmFront = 25f, ArmBack = -20f, Head = 8f },
                new RigPose { ArmFront = 28f, ArmBack = -18f, Head = 10f, Bob = -1f },
                new RigPose { ArmFront = 30f, ArmBack = -15f, Head = 12f, Bob = -1f, Torso = 2f },
                new RigPose { ArmFront = 27f, ArmBack = -18f, Head = 9f },
            });
            var walk = r0.Clip("walk", 8f, true, Waddle());
            var windup = new Clip("windup", 6f, false);
            windup.Frames.Add(r1.Build(new RigPose { ArmFront = 60f, ArmBack = -60f, Head = 20f, Bob = 1f }));
            windup.Frames.Add(r1.Build(new RigPose { ArmFront = 80f, ArmBack = -80f, Head = 25f, Bob = 1f, Shift = 1f }));
            windup.Frames.Add(r2.Build(new RigPose { ArmFront = 100f, ArmBack = -100f, Head = 30f, Bob = 2f, Shift = -1f }));
            windup.Frames.Add(r2.Build(new RigPose { ArmFront = 110f, ArmBack = -110f, Head = 30f, Bob = 2f, Shift = 1f }));
            var strike = new Clip("strike", 12f, false);
            strike.Frames.Add(r2.Build(new RigPose { ArmFront = 130f, ArmBack = -130f, Head = 35f, Bob = 3f }));
            var hurt = r0.Clip("hurt", 10f, false, new[]
            {
                new RigPose { Torso = 14f, Head = 20f, ArmFront = 60f, ArmBack = -50f, Shift = -2f },
                new RigPose { Torso = 6f, Head = 12f, ArmFront = 40f, ArmBack = -30f, Shift = -1f },
            });

            // Death: it bursts. Legs stay; chunks of flesh fly out.
            var death = new Clip("death", 12f, false);
            var chunk = new PixelPart("chunk", P(1.5f, 1.5f), m, 0, ".x%", "xxx", "%x.");
            var chunk2 = new PixelPart("chunk2", P(1f, 1f), m, 0, "xx", "x%");
            death.Frames.Add(r2.Build(new RigPose { ArmFront = 130f, ArmBack = -130f, Head = 35f, Bob = 3f }));
            Vector2[] dirs = { P(-1f, 0.6f), P(1f, 0.8f), P(-0.6f, 1f), P(0.7f, -0.2f), P(-0.9f, -0.1f), P(0.2f, 1f), P(1f, 0.2f) };
            for (int i = 1; i <= 5; i++)
            {
                var f = new Frame();
                float k = i / 5f;
                f.Add(r0.LegBack, P(29f, 17f - k * 3f), -30f * k, 2, -1);
                f.Add(r0.LegFront, P(35f, 17f - k * 3f), 40f * k, 3);
                for (int c = 0; c < dirs.Length; c++)
                {
                    float d = 6f + 16f * Mathf.Sqrt(k);
                    float drop = 30f * k * k;
                    var at = P(32f, 24f) + dirs[c] * d - P(0f, drop);
                    if (at.y < 11f) at.y = 11f + (c % 2);
                    f.Add(c % 2 == 0 ? chunk : chunk2, at, c * 70f + i * 40f, 6);
                }
                death.Frames.Add(f);
            }
            return new List<Clip> { idle, walk, windup, strike, hurt, death };
        }

        private static IEnumerable<RigPose> Waddle()
        {
            for (int i = 0; i < 6; i++)
            {
                float s = Mathf.Sin(i / 6f * Mathf.PI * 2f);
                yield return new RigPose
                {
                    Bob = Mathf.Abs(s) > 0.6f ? 1f : 0f,
                    Torso = 6f * s,
                    Head = 8f - 6f * s,
                    LegFront = 22f * s,
                    LegBack = -22f * s,
                    ArmFront = 25f + 15f * s,
                    ArmBack = -20f - 15f * s,
                };
            }
        }

        // ------------------------------------------------------------------ Dread knight (armoured, shield + mace)

        public static List<Clip> DreadKnight()
        {
            // Blued plate (n) with blackened joints (#) so it reads on the dark floor.
            var m = Palette.With(('n', new PixelMaterial(2, "#16181e", "#2c3038", "#4a505c", "#6e7684", "#9aa2b0")), ('#', Palette.DarkSteel));
            var rig = new HumanoidRig
            {
                Head = new PixelPart("helm", P(5.5f, 0.5f), m, 0,
                    "b.........b",
                    "bb.......bb",
                    ".bb.nnn.bb.",
                    "..bnnnnnb..",
                    "..nnnnnnn..",
                    ".nnnnnnnnn.",
                    ".nnnnnnnNn.",
                    "nnnnnnnnnnn",
                    "nnnnnkkkkkk",
                    "nnnnnkeekek",
                    "nnnnnnnnnnn",
                    "nnnnnn#n#nn",
                    ".nnnnnnnnn.",
                    "..#######.."),
                Torso = new PixelPart("plate", P(6.5f, 5.5f), m, 0,
                    "...nnnnnnn...",
                    "..nnnnnnnnnn.",
                    ".nnnnNNnnnnnn",
                    "nnnnnnnnnnnnn",
                    "nnnnnnnnnnnnn",
                    "nnn#nnnnnn#nn",
                    "nnnnnnnnnnnnn",
                    ".nnnnnnnnnnn.",
                    ".nnnuuuuuunn.",
                    "..nnnnnnnnn..",
                    "..ttnnnnnntt.",
                    "..tttnnnnttt.",
                    "..tt.tttt.tt.",
                    "..t...t...t.."),
                LegFront = new PixelPart("leg", P(2f, 10.5f), m, 0, "nnnn.", "nnnn.", "#nn#.", ".nn..", "nnnn.", "nNnn.", "nnnn.", "nnnn.", "nnnnn", "nnnnn", "kkkkk"),
                LegBack = new PixelPart("leg", P(2f, 10.5f), m, 0, "nnnn.", "nnnn.", "#nn#.", ".nn..", "nnnn.", "nNnn.", "nnnn.", "nnnn.", "nnnnn", "nnnnn", "kkkkk"),
                ArmFront = new PixelPart("arm", P(2.5f, 9.5f), m, 0, ".nnn.", "nnnnn", "nNnnn", "nnnnn", ".nnn.", ".#n#.", ".nnn.", ".nnn.", ".###.", ".###."),
                ArmBack = new PixelPart("arm", P(2.5f, 9.5f), m, 0, ".nnn.", "nnnnn", "nNnnn", "nnnnn", ".nnn.", ".#n#.", ".nnn.", ".nnn.", ".###.", ".###."),
                Weapon = new PixelPart("mace", P(3.5f, 2.5f), m, 0,
                    "..#.#..",
                    ".#####.",
                    "#######",
                    ".##k##.",
                    "#######",
                    ".#####.",
                    "..#.#..",
                    "...l...",
                    "...l...",
                    "...l...",
                    "...l...",
                    "...l...",
                    "...l...",
                    "..ggg..",
                    "...l...",
                    "...k..."),
                Offhand = new PixelPart("shield", P(5f, 8f), m, 0,
                    ".uuuuuuuu.",
                    "uuuuuuuuuu",
                    "uunnnnnnuu",
                    "unnnnnnnnu",
                    "unnngggnnu",
                    "unnnngnnnu",
                    "unnggggnnu",
                    "unnnngnnnu",
                    "unnnnnnnnu",
                    ".unnnnnnu.",
                    ".unnnnnnu.",
                    "..unnnnu..",
                    "..unnnnu..",
                    "...unnu...",
                    "....uu...."),
                Cape = new PixelPart("cloak", P(7f, 12.5f), m, -1,
                    "...tttttt",
                    "..ttttttt",
                    ".tttttttt",
                    ".ttttttt.",
                    "tttttttt.",
                    "ttttttt..",
                    "ttttttt..",
                    "tttttt...",
                    "tt.ttt...",
                    "t..tt....",
                    "...t....."),
                Hip = P(32f, 21f),
                LegSpread = 2f,
                NeckOnTorso = P(0.5f, 8f),
                ShoulderFront = P(2.5f, 6.5f),
                ShoulderBack = P(-2f, 7f),
                CapeOnTorso = P(-2f, 7f),
                ArmLength = 9f,
                WeaponReach = 14f,
                WeaponZ = 9,
                SmearColor = PixelMaterial.Hex("#5a6070"),
            };
            const float lean = -4f, arm = 30f, mace = -10f, shield = 35f;
            var idle = new List<RigPose>();
            for (int i = 0; i < 4; i++)
            {
                float k = i >= 2 ? 1f : 0f;
                idle.Add(new RigPose { Bob = -k, Torso = lean, Head = i == 3 ? 3f : 0f, ArmFront = arm + 3f * k, ArmBack = shield, WeaponAbsolute = mace + 3f * k, Cape = -4f - i });
            }
            var walk = new List<RigPose>();
            for (int i = 0; i < 6; i++)
            {
                float s = Mathf.Sin(i / 6f * Mathf.PI * 2f);
                walk.Add(new RigPose
                {
                    Bob = Mathf.Abs(s) > 0.7f ? -1f : 0f, Torso = lean - 4f, LegFront = 24f * s, LegBack = -24f * s,
                    ArmFront = arm - 8f * s, ArmBack = shield + 4f * s, WeaponAbsolute = mace - 8f * s, Cape = -10f - 6f * Mathf.Abs(s),
                });
            }
            return new List<Clip>
            {
                rig.Clip("idle", 5f, true, idle),
                rig.Clip("walk", 8f, true, walk),
                rig.Clip("windup", 8f, false, new[]
                {
                    new RigPose { Torso = lean + 8f, ArmFront = 130f, ArmBack = shield + 10f, Head = 6f, Cape = -6f },
                    new RigPose { Torso = lean + 14f, ArmFront = 185f, ArmBack = shield + 20f, Head = 10f, Bob = 1f, Cape = -8f },
                    new RigPose { Torso = lean + 16f, ArmFront = 200f, ArmBack = shield + 22f, Head = 12f, Bob = 1f, Cape = -8f },
                }),
                rig.Clip("strike", 14f, false, new[]
                {
                    new RigPose { Torso = lean - 12f, ArmFront = 120f, ArmBack = shield, LegFront = 25f, LegBack = -20f, Shift = 2f, Smear = true, SmearFrom = 200f, SmearTo = 120f, Cape = -14f },
                    new RigPose { Torso = lean - 22f, ArmFront = 40f, ArmBack = shield - 10f, LegFront = 30f, LegBack = -25f, Shift = 3f, Bob = -2f, Smear = true, SmearFrom = 120f, SmearTo = 40f, Cape = -22f },
                    new RigPose { Torso = lean - 18f, ArmFront = 30f, ArmBack = shield - 10f, LegFront = 28f, LegBack = -22f, Shift = 3f, Bob = -2f, Cape = -18f },
                    new RigPose { Torso = lean - 6f, ArmFront = 30f, ArmBack = shield, LegFront = 10f, LegBack = -10f, Shift = 1f, Cape = -8f },
                }),
                rig.Clip("hurt", 10f, false, new[]
                {
                    new RigPose { Torso = lean + 10f, Head = 10f, ArmFront = arm + 20f, ArmBack = shield + 25f, Shift = -1f, WeaponAbsolute = mace + 20f },
                    new RigPose { Torso = lean + 5f, Head = 5f, ArmFront = arm + 10f, ArmBack = shield + 10f, WeaponAbsolute = mace + 10f },
                }),
                rig.Clip("death", 8f, false, KnightFall(arm, shield)),
            };
        }

        private static IEnumerable<RigPose> KnightFall(float arm, float shield)
        {
            float[] fall = { 0f, -8f, -25f, -55f, -82f, -90f, -90f };
            for (int i = 0; i < fall.Length; i++)
            {
                float k = i / (fall.Length - 1f);
                yield return new RigPose
                {
                    Torso = -10f * k, Head = -20f * k, ArmFront = arm + 100f * k, ArmBack = shield - 60f * k,
                    LegFront = 20f * k, LegBack = -10f * k, WeaponAbsolute = -10f - 80f * k, Cape = 25f * k,
                    BodyAngle = fall[i], BodyPivot = P(36f, 10f), BodyOffset = P(0f, i >= 5 ? -2f : 0f),
                };
            }
        }

        // ------------------------------------------------------------------ Necromancer (mage rig, grave robes, skull face)

        public static List<Clip> Necromancer()
        {
            var m = Palette.With(('r', Palette.Grave));
            var rig = HeroArt.MageRig(m, glowingEyes: false);
            rig.Head = new PixelPart("skullhood", P(5.5f, 0.5f), m, 0,
                "....rrrr...",
                "...rrrrrrr.",
                "..rrrrrrrrr",
                ".rrrrrrRrrr",
                ".rrrrrbbbbr",
                "rrrrrbbbbbb",
                "rrrrrbbk!bk",
                "rrrrrbbbbbb",
                ".rrrrbbkbkb",
                "..rrrrrbbb.",
                "...ggggg...");
            rig.Weapon = new PixelPart("skullstaff", P(2.5f, 9.5f), m, 0,
                "..!..",
                ".!+!.",
                "..!..",
                ".bbb.",
                "bk!kb",
                "bbbbb",
                ".bkb.",
                "..o..",
                "..o..",
                ".ooo.",
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
                "..g..");
            rig.SmearColor = PixelMaterial.Hex("#b6ff4a");
            const float arm = 48f, staff = 4f;
            return new List<Clip>
            {
                rig.Clip("idle", 6f, true, Idle(arm, staff, -2f)),
                rig.Clip("walk", 8f, true, Shamble(arm, staff, -4f)),
                rig.Clip("windup", 8f, false, new[]
                {
                    new RigPose { ArmFront = 100f, ArmBack = -100f, Torso = 6f, Head = 8f, WeaponAbsolute = 10f },
                    new RigPose { ArmFront = 150f, ArmBack = -145f, Torso = 10f, Head = 14f, Bob = 1f, WeaponAbsolute = 0f },
                    new RigPose { ArmFront = 160f, ArmBack = -155f, Torso = 12f, Head = 16f, Bob = 2f, WeaponAbsolute = -5f },
                }),
                rig.Clip("strike", 12f, false, new[]
                {
                    new RigPose { ArmFront = 85f, ArmBack = -40f, Torso = -8f, LegFront = 15f, LegBack = -12f, Shift = 1f, WeaponAbsolute = -60f },
                    new RigPose { ArmFront = 85f, ArmBack = -35f, Torso = -10f, LegFront = 15f, LegBack = -12f, Shift = 1f, WeaponAbsolute = -70f },
                    new RigPose { ArmFront = 50f, ArmBack = -15f, Torso = -2f, WeaponAbsolute = -20f },
                }),
                rig.Clip("hurt", 10f, false, Hurt(arm, staff, -2f)),
                rig.Clip("death", 9f, false, CultistDeath()),
            };
        }

        // ------------------------------------------------------------------ Wraith (floating cloak, claws)

        public static List<Clip> Wraith() => WraithClips(Palette.Standard());

        /// <summary>The wraith's body and moves; recoloured for the shade stalker (EnemyArt.Round3).</summary>
        private static List<Clip> WraithClips(Dictionary<char, PixelMaterial> m)
        {
            var empty = new PixelPart("none", P(0f, 0f), m, 0, ".");
            var claw = new PixelPart("arm", P(1.5f, 11.5f), m, 0, ".jj", "jjj", "jjj", ".jj", ".jj", ".jj", ".jj", ".jj", ".jb", ".bb", "b.b", "b.b");
            var rig = new HumanoidRig
            {
                Head = new PixelPart("hood", P(4.5f, 0.5f), m, 0,
                    "...jjjj...",
                    "..jjjjjjj.",
                    ".jjjjjjjjj",
                    "jjjjjjJjjj",
                    "jjjjjkkkkj",
                    "jjjjkkkkkk",
                    "jjjjkk*kk*",
                    "jjjjkkkkkk",
                    ".jjjjkkkk.",
                    "..jjjjjj.."),
                Torso = new PixelPart("shroud", P(7f, 12.5f), m, 0,
                    "....jjjjjj....",
                    "...jjjjjjjjj..",
                    "..jjjjjjjjjjj.",
                    "..jjjjJjjjjjj.",
                    ".jjjjjjjjjjjj.",
                    ".jjjjjjjjjjjjj",
                    ".jjjjjjtjjjjjj",
                    "jjjjjjjtjjjjjj",
                    "jjjjjjjjtjjjjj",
                    "jjjjjjjjjjjjj.",
                    "jjjjjjjjjjjjj.",
                    ".jjjjjjjjjjjj.",
                    ".jjjjjjjjjjjj.",
                    ".jjjjjj.jjjjj.",
                    "..jjjjj.jjjj..",
                    "..jjjj..jjjj..",
                    "..jjj....jjj..",
                    "...jj....jj...",
                    "...j......j...",
                    "..j........j.."),
                LegFront = empty,
                LegBack = empty,
                ArmFront = claw,
                ArmBack = claw,
                Hip = P(32f, 25f),
                NeckOnTorso = P(1f, 7f),
                ShoulderFront = P(3f, 5f),
                ShoulderBack = P(-2f, 5.5f),
                ArmLength = 11f,
                WeaponReach = 5f,
                SmearColor = PixelMaterial.Hex("#8ff5ff"),
            };

            IEnumerable<RigPose> Float(float lean, int frames, float armSway)
            {
                for (int i = 0; i < frames; i++)
                {
                    float s = Mathf.Sin(i / (float)frames * Mathf.PI * 2f);
                    yield return new RigPose { Bob = Mathf.Round(2f * s), Torso = lean + 3f * s, Head = -3f * s, ArmFront = 30f + armSway * s, ArmBack = -15f - armSway * s };
                }
            }

            var death = new List<RigPose>();
            for (int i = 0; i < 6; i++)
            {
                float k = i / 5f;
                death.Add(new RigPose { Bob = -10f * k, Torso = 20f * k, Head = 40f * k, ArmFront = 30f + 120f * k, ArmBack = -15f - 90f * k, BodyAngle = 30f * k, BodyPivot = P(32f, 14f) });
            }

            return new List<Clip>
            {
                rig.Clip("idle", 6f, true, Float(-4f, 6, 8f)),
                rig.Clip("walk", 8f, true, Float(-14f, 6, 12f)),
                rig.Clip("windup", 8f, false, new[]
                {
                    new RigPose { Torso = 10f, Head = 10f, ArmFront = 140f, ArmBack = -130f, Bob = 2f },
                    new RigPose { Torso = 16f, Head = 14f, ArmFront = 170f, ArmBack = -160f, Bob = 3f },
                    new RigPose { Torso = 18f, Head = 16f, ArmFront = 175f, ArmBack = -165f, Bob = 3f },
                }),
                rig.Clip("strike", 14f, false, new[]
                {
                    new RigPose { Torso = -15f, ArmFront = 100f, ArmBack = -40f, Bob = 0f, Smear = true, SmearFrom = 175f, SmearTo = 100f },
                    new RigPose { Torso = -25f, ArmFront = 30f, ArmBack = 10f, Bob = -1f, Smear = true, SmearFrom = 100f, SmearTo = 30f },
                    new RigPose { Torso = -18f, ArmFront = 20f, ArmBack = 0f, Bob = -1f },
                }),
                rig.Clip("hurt", 10f, false, new[]
                {
                    new RigPose { Torso = 18f, Head = 20f, ArmFront = 70f, ArmBack = -60f, Shift = -2f },
                    new RigPose { Torso = 8f, Head = 10f, ArmFront = 45f, ArmBack = -35f, Shift = -1f },
                }),
                rig.Clip("death", 9f, false, death),
            };
        }

        // ------------------------------------------------------------------ Powder keg (breakable prop)

        public static List<Clip> PowderKeg()
        {
            var m = Palette.Standard();
            var keg = new PixelPart("keg", P(7f, 0.5f), m, 0,
                "...oooooooo...",
                "..oooooooooo..",
                ".############.",
                ".oooooooooooo.",
                "oooooooooooooo",
                "oooooddooddooo",
                "ooooooddddoooo",
                "oooooooddooooo",
                "ooooooddddoooo",
                "oooooddooddooo",
                "##############",
                "oooooooooooooo",
                ".oooooooooooo.",
                ".############.",
                "..oooooooooo..",
                "...oooooooo...");
            var fuse = new PixelPart("fuse", P(0.5f, 0.5f), m, 0, ".z", "q.", "q.");
            var spark = new PixelPart("spark", P(1f, 1f), m, 0, ".z.", "zFz", ".z.");
            var stave = new PixelPart("stave", P(1.5f, 3.5f), m, 0, "oo#", "ooo", "ooo", "o#o", "ooo", "ooo", ".oo");

            Frame Keg(float tilt, bool sparkOn, float dx = 0f)
            {
                var f = new Frame();
                f.Add(keg, P(32f + dx, 10f), tilt, 4);
                f.Add(fuse, P(33f + dx, 26f), tilt, 5);
                if (sparkOn) f.Add(spark, P(34f + dx, 29f), 0f, 6);
                return f;
            }

            var idle = new Clip("idle", 4f, true);
            idle.Frames.Add(Keg(0f, true));
            idle.Frames.Add(Keg(0f, false));
            var walk = new Clip("walk", 4f, true);
            walk.Frames.Add(Keg(0f, true));
            var hurt = new Clip("hurt", 12f, false);
            hurt.Frames.Add(Keg(8f, true, 1f));
            hurt.Frames.Add(Keg(-6f, true, -1f));
            var death = new Clip("death", 12f, false);
            for (int i = 0; i < 4; i++)
            {
                var f = new Frame();
                float k = (i + 1) / 4f;
                for (int s = 0; s < 6; s++)
                {
                    float a = s * 60f + 15f;
                    var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                    var at = P(32f, 17f) + dir * (4f + 10f * k) - P(0f, 6f * k * k);
                    if (at.y < 11f) at.y = 11f;
                    f.Add(stave, at, a + 90f * k * (s % 2 == 0 ? 1f : -1f), 4);
                }
                death.Frames.Add(f);
            }
            return new List<Clip> { idle, walk, Same("windup", idle), Same("strike", idle), hurt, death };
        }

        private static Clip Same(string name, Clip from)
        {
            var c = new Clip(name, from.Fps, from.Loop);
            c.Frames.AddRange(from.Frames);
            return c;
        }
    }
}
