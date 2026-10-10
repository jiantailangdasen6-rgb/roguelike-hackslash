using System.Collections.Generic;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Dark-fantasy palette: desaturated, low-key ramps with red and gold accents and a few glowing colours.
    /// Each ramp runs dark → light; base shade 2 is the mid tone.
    /// </summary>
    public static class Palette
    {
        public static readonly PixelMaterial Steel = new(2, "#16181f", "#2b2f3a", "#474d5a", "#6f7785", "#a9b1bb");
        public static readonly PixelMaterial Blade = new(2, "#262a32", "#4f5967", "#86919e", "#bfc8d1", "#eef3f7");
        public static readonly PixelMaterial Red = new(2, "#1c080c", "#3a0e14", "#5f151d", "#8a1f27", "#b3333a");
        public static readonly PixelMaterial Leather = new(2, "#1b120c", "#302018", "#4a3324", "#664831", "#876142");
        public static readonly PixelMaterial Gold = new(2, "#2e1f0a", "#573d12", "#87641c", "#bb912f", "#e6c766");
        public static readonly PixelMaterial Bone = new(2, "#28231d", "#524b3c", "#857c6a", "#b8ae95", "#e2dac1");
        public static readonly PixelMaterial Rust = new(2, "#22130b", "#462815", "#693c1c", "#8d5428", "#ab703e");
        public static readonly PixelMaterial Rag = new(2, "#141218", "#24202b", "#373141", "#4d465b", "#665d76");
        public static readonly PixelMaterial Skin = new(2, "#2b1915", "#55342a", "#845642", "#ae7d65", "#d6a388");
        public static readonly PixelMaterial Robe = new(2, "#110e1d", "#201a37", "#322a56", "#483d7a", "#64579f");
        public static readonly PixelMaterial Hood = new(2, "#0e150f", "#1a281c", "#2a3d2e", "#3c5640", "#557558");
        public static readonly PixelMaterial Wood = new(2, "#1d140c", "#372616", "#563b21", "#78552e", "#9a7340");
        public static readonly PixelMaterial Flesh = new(2, "#191c16", "#33392c", "#505843", "#747c5c", "#9ca37a");
        public static readonly PixelMaterial CultRobe = new(2, "#130507", "#280b0f", "#431219", "#631b23", "#86282e");
        public static readonly PixelMaterial DarkSteel = new(1, "#0f1015", "#1d1f27", "#30333f", "#4a4f5d", "#6c7383");

        public static readonly PixelMaterial Black = new("#100c11");
        public static readonly PixelMaterial EyeRed = new("#ff3b2f");
        public static readonly PixelMaterial EyeCore = new("#ffd2a6");
        public static readonly PixelMaterial Arcane = new("#8fd3ff");
        public static readonly PixelMaterial ArcaneCore = new("#effaff");
        public static readonly PixelMaterial Gem = new("#ff5a4a");
        public static readonly PixelMaterial String = new("#c8c0aa");
        public static readonly PixelMaterial Ember = new("#ff8a2a");

        /// <summary>Material letters shared by all character art.</summary>
        public static Dictionary<char, PixelMaterial> Standard() => new()
        {
            ['m'] = Steel,
            ['w'] = Blade,
            ['c'] = Red,
            ['l'] = Leather,
            ['g'] = Gold,
            ['b'] = Bone,
            ['u'] = Rust,
            ['t'] = Rag,
            ['s'] = Skin,
            ['r'] = Robe,
            ['h'] = Hood,
            ['o'] = Wood,
            ['x'] = Flesh,
            ['n'] = DarkSteel,
            ['k'] = Black,
            ['e'] = EyeRed,
            ['E'] = EyeCore,
            ['f'] = Arcane,
            ['F'] = ArcaneCore,
            ['y'] = Gem,
            ['q'] = String,
            ['z'] = Ember,
        };

        /// <summary>Same letters, with the robe letter repainted (palette swap, e.g. mage → cultist).</summary>
        public static Dictionary<char, PixelMaterial> WithRobe(PixelMaterial robe)
        {
            var map = Standard();
            map['r'] = robe;
            return map;
        }
    }
}
