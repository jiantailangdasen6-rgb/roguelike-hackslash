using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Each class's other weapon (ClassCatalog.AltWeapons), drawn into the class's frames in place of its usual one:
    /// the warrior's great axe, the mage's tome, the ranger's crossbow, the guardian's lance, the assassin's throwing
    /// knife, the alchemist's bomb, the deathcaller's scythe and the paladin's greatsword. Held upright, grip at the pivot.
    /// </summary>
    public static partial class HeroArt
    {
        /// <summary>Runs a class's generator with its other weapon in hand.</summary>
        public static List<Clip> WithAltWeapon(ClassId cls, Func<List<Clip>> make)
        {
            HumanoidRig.Override = AltWeapon(cls);
            try { return make(); }
            finally { HumanoidRig.Override = null; }
        }

        private static HumanoidRig.AltWeapon AltWeapon(ClassId cls)
        {
            var m = Palette.Standard();
            HumanoidRig.AltWeapon W(PixelPart part, float reach, int z, string smear) =>
                new() { Part = part, Reach = reach, Z = z, Smear = PixelMaterial.Hex(smear) };
            switch (cls)
            {
                case ClassId.Warrior:
                    return W(new PixelPart("axe", P(4.5f, 3.5f), m, 0,
                        "....o....",
                        ".mmmommm.",
                        "wmmmommmw",
                        "wmMmomMmw",
                        "wmMmomMmw",
                        "wmmmommmw",
                        ".mmmommm.",
                        "....o....",
                        "....o....",
                        "....o....",
                        "....o....",
                        "....o....",
                        "....o....",
                        "....o....",
                        "....o....",
                        "....o....",
                        "....g....",
                        "....l....",
                        "....l....",
                        "....g...."), 22f, 7, "#e0e6ee");
                case ClassId.Mage:
                    return W(new PixelPart("tome", P(3.5f, 0.5f), m, 0,
                        "cccccccc",
                        "ciiigiic",
                        "cifiiiic",
                        "ciiiific",
                        "cifiiiic",
                        "ciiiiiic",
                        "cccccccc",
                        "...gg..."), 9f, 9, "#c8a8ff");
                case ClassId.Ranger:
                    return W(new PixelPart("crossbow", P(5f, 2.5f), m, 0,
                        ".ooo...ooo.",
                        "o...omo...o",
                        "qq..omo..qq",
                        "..qqomoqq..",
                        "....omo....",
                        "....omo....",
                        "....ooo....",
                        "....ooo....",
                        "....ooo....",
                        ".....l.....",
                        ".....l....."), 11f, 9, "#e8d8a8");
                case ClassId.Guardian:
                    return W(new PixelPart("lance", P(2f, 8f), m, 0,
                        "..w..",
                        ".wWw.",
                        ".wWw.",
                        ".wWw.",
                        "..w..",
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
                        "..n.."), 24f, 9, "#e8eef6");
                case ClassId.Assassin:
                    return W(new PixelPart("knife", P(1f, 1.5f), m, 0,
                        ".w.",
                        "wWw",
                        "wWw",
                        "wWw",
                        "wWw",
                        "kkk",
                        ".l.",
                        ".l."), 9f, 9, "#d8deea");
                case ClassId.Alchemist:
                    return W(new PixelPart("bomb", P(2.5f, 0.5f), m, 0,
                        "...z.",
                        "..q..",
                        ".kkk.",
                        "kknkk",
                        "kknkk",
                        "kkkkk",
                        ".kkk."), 7f, 9, "#ffb04a");
                case ClassId.Deathcaller:
                    return W(new PixelPart("scythe", P(9f, 6f), m, 0,
                        "wwwwwwww...",
                        ".wWWWWWwwo.",
                        "..wwwwwwwo.",
                        "......wwwo.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........o.",
                        ".........n."), 22f, 7, "#b6ff9a");
                default:
                    return W(new PixelPart("greatsword", P(3.5f, 3.5f), m, 0,
                        "...w...",
                        "..wWw..",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwWww.",
                        ".wwkww.",
                        "ggggggg",
                        "g..l..g",
                        "...l...",
                        "...l...",
                        "..ggg.."), 26f, 7, "#fff3c0");
            }
        }
    }
}
