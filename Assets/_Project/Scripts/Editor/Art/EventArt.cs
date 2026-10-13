using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Room events (round 5): the mimic (a campfire chest that bites back) and the captive adventurer in a cage
    /// (break the bars to set them free).
    /// </summary>
    public static class EventArt
    {
        private static Vector2 P(float x, float y) => new(x, y);

        // ------------------------------------------------------------------ Mimic

        public static List<Clip> Mimic()
        {
            var m = Palette.Standard();
            // The chest's own body, with a pair of glowing eyes in the dark keyhole slot.
            var body = new PixelPart("body", P(10f, 0.5f), m, 0,
                "o#oooooooooooooooo#o",
                "o#oooooEkkkkEooooo#o",
                "o#ooooooggggoooooo#o",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "o#oooooooooooooooo#o",
                "####################",
                "##................##");
            var lid = new PixelPart("lid", P(0.5f, 0.5f), m, 0,
                "...oooooooooooooo...",
                ".oooooooooooooooooo.",
                "o#oooooooooooooooo#o",
                "o#ooooooggggoooooo#o",
                "o#oooooooooooooooo#o",
                "####################");
            // Teeth hang under the lid (same hinge) and stand on the rim of the body.
            var upper = new PixelPart("upper", P(0.5f, 2.5f), m, 0,
                ".i.i.ii.i.i.ii.i.i..",
                ".i...i..i...i...i...");
            var lower = new PixelPart("lower", P(9.5f, 0.5f), m, 0,
                "..i...i..i...i..i...",
                ".iii.ii.iii.ii.iii..");
            var maw = new PixelPart("maw", P(9f, 0.5f), m, 0,
                ".kkkkkkkkkkkkkkkk.",
                "kkcckkkkkkkkkkcckk",
                "kccccckkkkkccccckk");
            var tongue = new PixelPart("tongue", P(1.5f, 7.5f), m, 0,
                ".cc.",
                "cCcc",
                "cccc",
                ".ccc",
                ".cc.",
                "ccc.",
                "cc..",
                ".c..");
            var claw = new PixelPart("claw", P(1f, 4.5f), m, 0, ".x", "xx", "xx", "x.", "xx");
            var coin = new PixelPart("coin", P(1f, 1f), m, 0, "gG", "Gg");

            // lid: degrees open; hop: lifted off the floor; lean: forward (+) / back (-); legs: which claws reach.
            Frame Pose(float lidAngle, float hop = 0f, float lean = 0f, bool tongueOut = false, int legs = 0, float shake = 0f)
            {
                var f = new Frame();
                float x = 32f + lean + shake, y = 10f + hop;
                // Four stubby claws under the box (scuttling).
                float[] cx = { 24f, 29f, 35f, 40f };
                for (int i = 0; i < cx.Length; i++)
                {
                    float reach = legs == 0 ? 0f : ((i + legs) % 2 == 0 ? 2f : -1f);
                    f.Add(claw, P(cx[i] + lean + shake + reach, y - 1f), (i < 2 ? -15f : 15f) + reach * 8f, 2);
                }
                f.Add(body, P(x, y), 0f, 4);
                f.Add(maw, P(x, y + 9f), 0f, 5);
                f.Add(lower, P(x, y + 11f), 0f, 6);
                if (tongueOut) f.Add(tongue, P(x + 3f, y + 10f), -20f - lean * 3f, 7);
                f.Add(lid, P(x - 10f, y + 9f), lidAngle, 8);
                f.Add(upper, P(x - 10f, y + 9f), lidAngle, 8);
                return f;
            }

            var idle = new Clip("idle", 4f, true);
            idle.Frames.Add(Pose(4f));
            idle.Frames.Add(Pose(8f, 0.5f));
            idle.Frames.Add(Pose(12f, 1f, 0f, tongueOut: true));
            idle.Frames.Add(Pose(6f, 0.5f));
            var walk = new Clip("walk", 10f, true);
            walk.Frames.Add(Pose(10f, 0f, 0f, legs: 1));
            walk.Frames.Add(Pose(22f, 3f, 1f, legs: 2));
            walk.Frames.Add(Pose(14f, 4f, 1f, legs: 1));
            walk.Frames.Add(Pose(6f, 1f, 0f, legs: 2));
            var windup = new Clip("windup", 8f, false);
            windup.Frames.Add(Pose(30f, 1f, -2f, true));
            windup.Frames.Add(Pose(48f, 2f, -3f, true));
            windup.Frames.Add(Pose(56f, 2f, -4f, true, shake: 0.5f));
            var strike = new Clip("strike", 14f, false);
            strike.Frames.Add(Pose(40f, 3f, 4f, true));
            strike.Frames.Add(Pose(4f, 1f, 6f));
            strike.Frames.Add(Pose(0f, 0f, 3f));
            var hurt = new Clip("hurt", 14f, false);
            hurt.Frames.Add(Pose(18f, 0f, -2f, shake: 1f));
            hurt.Frames.Add(Pose(6f, 0f, 1f, shake: -1f));
            var death = new Clip("death", 9f, false);
            for (int i = 0; i < 6; i++)
            {
                float k = (i + 1) / 6f;
                var f = Pose(Mathf.Lerp(30f, 115f, Mathf.Sqrt(k)), -1f * k, 0f, true);
                for (int s = 0; s < 8; s++)
                {
                    float a = (20f + s * 20f) * Mathf.Deg2Rad;
                    var at = P(32f, 22f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (4f + 15f * k) - P(0f, 11f * k * k);
                    if (at.y < 10f) at.y = 10f;
                    f.Add(coin, at, 0f, 9);
                }
                death.Frames.Add(f);
            }
            return new List<Clip> { idle, walk, windup, strike, hurt, death };
        }

        // ------------------------------------------------------------------ Captive adventurer

        public static List<Clip> Captive()
        {
            var m = Palette.Standard();
            var bar = new PixelPart("bar", P(1f, 0f), m, 0,
                "mm", "Mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm", "mm");
            var top = new PixelPart("top", P(12.5f, 0.5f), m, 0,
                "..######################..",
                ".#nnnnnnnnnnnnnnnnnnnnnn#.",
                "##########################");
            var floor = new PixelPart("floor", P(12.5f, 0.5f), m, 0,
                "##########################",
                ".#nnnnnnnnnnnnnnnnnnnnnn#.");
            var chain = new PixelPart("chain", P(1f, 0f), m, 0, ".#", "#.", ".#", "#.", ".#");
            // A ragged adventurer sitting on the cage floor, knees up, one hand on the bars.
            // Red hair, a red tunic over a pale shirt: readable behind the bars.
            var person = new PixelPart("person", P(7f, 0.5f), m, 0,
                "....uuuuu.....",
                "...uuuuuuu....",
                "...uusssuu....",
                "...usksksu....",
                "....sssss.....",
                ".....sss......",
                "..ccccccccc...",
                ".cccbbbbbccc..",
                ".ccbbbbbbbcc..",
                ".scccccccccs..",
                ".s.ccccccc.s..",
                "...lllllll....",
                "..lll...lll...",
                "..ll.....ll...",
                ".kkk.....kkk..");
            var hand = new PixelPart("hand", P(1f, 1f), m, 0, ".ss", "sss", "ss.", "s..");
            var bit = new PixelPart("bit", P(1f, 2f), m, 0, "##", "#n", "#n", "##");

            Frame Cage(float shake = 0f, float bob = 0f, bool wave = false)
            {
                var f = new Frame();
                f.Add(floor, P(32f + shake, 9f), 0f, 2);
                f.Add(person, P(32f + shake, 11f + bob), 0f, 3);
                if (wave) f.Add(hand, P(35f + shake, 26f + bob), 0f, 6);
                float[] xs = { 21f, 27.5f, 37f, 43f }; // a gap in the middle shows the face
                foreach (float bx in xs) f.Add(bar, P(bx + shake, 10f), 0f, 5);
                f.Add(top, P(32f + shake, 33f), 0f, 6);
                f.Add(chain, P(32f + shake, 36f), 0f, 6);
                return f;
            }

            var idle = new Clip("idle", 3f, true);
            idle.Frames.Add(Cage());
            idle.Frames.Add(Cage(0f, 0.5f));
            idle.Frames.Add(Cage(0f, 0.5f, true));
            idle.Frames.Add(Cage(0f, 0f, true));
            var hurt = new Clip("hurt", 16f, false);
            hurt.Frames.Add(Cage(1.5f));
            hurt.Frames.Add(Cage(-1.5f));
            // Freed: the bars burst outwards (the adventurer runs off: drawn by the presenter as a callout).
            var death = new Clip("death", 12f, false);
            for (int i = 0; i < 5; i++)
            {
                float k = (i + 1) / 5f;
                var f = new Frame();
                f.Add(floor, P(32f, 9f), 0f, 2);
                for (int b = 0; b < 8; b++)
                {
                    float a = (b * 45f + 20f) * Mathf.Deg2Rad;
                    var at = P(32f, 22f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (6f + 16f * k) - P(0f, 8f * k * k);
                    if (at.y < 10f) at.y = 10f;
                    f.Add(bit, at, b * 40f + 90f * k, 5);
                }
                death.Frames.Add(f);
            }
            static Clip Same(string name, Clip from)
            {
                var c = new Clip(name, from.Fps, from.Loop);
                c.Frames.AddRange(from.Frames);
                return c;
            }
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }
    }
}
