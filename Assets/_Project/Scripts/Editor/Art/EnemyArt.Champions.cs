using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// The lands' own champions (RunSimulation.Champions): palette changes of other frames, drawn larger, and two heroes'
    /// frames turned monster (the ice knight from the warrior, the bombsmith from the alchemist).
    /// </summary>
    public static partial class EnemyArt
    {
        private static readonly PixelMaterial GraveDirt = R("#1a140c", "#33281a", "#52422c", "#766244", "#a08a64");
        private static readonly PixelMaterial DirtBone = R("#2a2418", "#4e4430", "#7a6c4e", "#a89a76", "#d2c6a2");
        private static readonly PixelMaterial TwinRobe = R("#3a2e30", "#6a5458", "#9c8488", "#c8b4b4", "#ece0dc");
        private static readonly PixelMaterial ShadeRed = R("#16040a", "#2c0a12", "#48121a", "#6c1c24", "#962a2e");
        private static readonly PixelMaterial LeechSkin = R("#0c1a18", "#1a302c", "#2a4c46", "#3e6c62", "#5a9284");
        private static readonly PixelMaterial LeechBlister = R("#0c1a26", "#183450", "#2a5476", "#4a82a0", "#86c4d4");
        private static readonly PixelMaterial SootSkin = R("#2a1810", "#4a2c1e", "#70442e", "#966044", "#be8462");

        public static List<Clip> BoneDigger() => Swapped(Skeleton,
            (Palette.Bone, DirtBone), (Palette.Steel, IronDark), (Palette.Rust, GraveDirt), (Palette.Leather, GraveDirt), (Palette.EyeRed, PaleGreen));

        public static List<Clip> TwinAcolyte() => Swapped(Cultist,
            (Palette.CultRobe, TwinRobe), (Palette.Red, Palette.Crimson), (Palette.Skin, Palette.Pallid));

        public static List<Clip> ShadeLord() => Swapped(ShadeStalker,
            (Palette.Violet, ShadeRed), (Palette.Ghost, ShadeRed));

        public static List<Clip> LeechMother() => Swapped(Bloater,
            (Palette.Flesh, LeechSkin), (Palette.Pustule, LeechBlister), (Palette.Toxic, AquaGlow), (Palette.Rag, Kelp), (KnightArmor, LeechSkin), (Palette.DarkSteel, LeechSkin));

        public static List<Clip> IceKnight() => AsMonster(HeroArt.Warrior(Palette.With(
            ('m', IceArmor), ('c', FrostRobe), ('g', PaleSilver), ('n', IceArmor), ('w', IcyBone), ('l', FrostRobe), ('k', ColdGlow))));

        public static List<Clip> Bombsmith() => AsMonster(HeroArt.Alchemist(Palette.With(
            ('a', Soot), ('s', SootSkin), ('g', EmberGlow), ('l', R("#24160e", "#42281a", "#64402a", "#8a5c3e", "#b27e58")), ('u', R("#1a1a1e", "#303036", "#4c4c54", "#6c6c76", "#9494a0")), ('t', Molten), ('n', Brass),
            ('f', BlackIron), ('F', EmberGlow), ('o', Molten))));

        /// <summary>
        /// A hero's clips under the names monsters use: run walks, the first half of the attack winds up and the rest strikes.
        /// </summary>
        private static List<Clip> AsMonster(List<Clip> hero)
        {
            Clip Get(string name) => hero.Find(c => c.Name == name);
            Clip Part(string name, Clip from, float fps, bool loop, int start, int count)
            {
                var c = new Clip(name, fps, loop);
                c.Frames.AddRange(from.Frames.GetRange(start, count));
                return c;
            }
            var attack = Get("attack");
            int half = Mathf.Max(1, attack.Frames.Count / 2);
            var idle = Get("idle");
            var run = Get("run");
            var hurt = Get("hurt");
            var death = Get("death");
            return new List<Clip>
            {
                Part("idle", idle, idle.Fps, true, 0, idle.Frames.Count),
                Part("walk", run, run.Fps * 0.75f, true, 0, run.Frames.Count),
                Part("windup", attack, 8f, false, 0, half),
                Part("strike", attack, attack.Fps, false, half, attack.Frames.Count - half),
                hurt,
                death,
            };
        }
    }
}
