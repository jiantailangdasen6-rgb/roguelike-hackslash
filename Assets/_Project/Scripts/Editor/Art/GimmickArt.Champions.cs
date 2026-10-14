using System.Collections.Generic;

namespace Game.EditorTools.Art
{
    /// <summary>The bone digger's heap (RunSimulation.Champions): bones and a skull, stirring as they gather themselves.</summary>
    public static partial class GimmickArt
    {
        public static List<Clip> BonePile()
        {
            var m = Palette.With(('b', new PixelMaterial(2, "#2a2418", "#4e4430", "#7a6c4e", "#a89a76", "#d2c6a2")));
            var heap = new PixelPart("heap", P(8f, 2f), m, 0,
                "....bb.b........",
                "..bbbbbbb..b....",
                ".bbbBbbbbbbbbb..",
                "bbbbbbbbbBbbbbbb",
                ".bbbbbbbbbbbbbb.");
            var skull = new PixelPart("skull", P(3f, 2.5f), m, 0,
                ".bbbb.",
                "bbbbbb",
                "bkbbkb",
                "bbbbbb",
                ".b.b..");
            var bone = new PixelPart("bone", P(3f, 1f), m, 0, "b....b", "bbbbbb", "b....b");
            Frame Pose(float stir, float skullUp)
            {
                var f = new Frame();
                f.Add(heap, P(32f, 12f), 0f, 3);
                f.Add(bone, P(27f + stir, 15f + stir * 0.5f), 30f + stir * 20f, 4);
                f.Add(bone, P(37f - stir, 14f + stir * 0.3f), -25f - stir * 15f, 4);
                f.Add(skull, P(32f + stir * 0.5f, 16f + skullUp), stir * 8f, 5);
                return f;
            }
            var idle = new Clip("idle", 6f, true);
            for (int i = 0; i < 6; i++) idle.Frames.Add(Pose(i % 2 == 0 ? 0f : 1f, i >= 3 ? 1f : 0f));
            var hurt = new Clip("hurt", 16f, false);
            hurt.Frames.Add(Pose(-1.5f, 0f));
            hurt.Frames.Add(Pose(1.5f, 0f));
            var death = Shatter(new[] { bone, skull, bone, bone }, P(32f, 14f), 5);
            return new List<Clip> { idle, Same("walk", idle), Same("windup", idle), Same("strike", idle), hurt, death };
        }
    }
}
