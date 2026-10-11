using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// 24x24 pixel icons: one per perk (PerkId order, then "rest") and one per trinket (catalog order).
    /// Each sits on a dark medallion whose rim shows who can take it (gold common, red warrior, blue mage, green ranger).
    /// </summary>
    public static class IconArt
    {
        public const int Size = 24;
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);

        private static readonly Color32 Ink = PixelRenderer.Outline;
        private static readonly Color32 White = C("#f4efe6");
        private static readonly Color32 Steel = C("#a9b1bb");
        private static readonly Color32 SteelDark = C("#5a616d");
        private static readonly Color32 Gold = C("#e6c766");
        private static readonly Color32 GoldDark = C("#87641c");
        private static readonly Color32 Red = C("#d23a3a");
        private static readonly Color32 RedDark = C("#6a1018");
        private static readonly Color32 Blue = C("#8fd3ff");
        private static readonly Color32 BlueDark = C("#2a5c9a");
        private static readonly Color32 Green = C("#7dff9a");
        private static readonly Color32 GreenDark = C("#2a7a3e");
        private static readonly Color32 Amber = C("#ffb04a");
        private static readonly Color32 Wood = C("#8a5f36");
        private static readonly Color32 Toxic = C("#b6ff4a");

        public static List<PixelCanvas> Perks()
        {
            var icons = new List<PixelCanvas>();
            foreach (PerkId id in Enum.GetValues(typeof(PerkId)))
            {
                var def = PerkCatalog.Get(id);
                var c = Medallion(def.Class);
                DrawPerk(c, id);
                Finish(c);
                icons.Add(c);
            }
            var rest = Medallion(null);
            Campfire(rest);
            Finish(rest);
            icons.Add(rest);
            return icons;
        }

        public static List<PixelCanvas> Trinkets()
        {
            var icons = new List<PixelCanvas>();
            foreach (var t in MetaCatalog.Trinkets)
            {
                var c = Medallion(null, trinket: true);
                switch (t.Id)
                {
                    case "charm_hp": Amulet(c); break;
                    case "wolf_fang": Fang(c, White); break;
                    case "feather": Feather(c, C("#dfe8f0"), C("#8fa8c2")); break;
                    case "iron_oath": Shield(c, Steel, SteelDark, Gold); break;
                    case "saint_ash": Urn(c); break;
                    case "soul_ring": Ring(c); break;
                    case "hourglass": Hourglass(c); break;
                    case "phoenix": Feather(c, Amber, Red); Sparks(c, Gold); break;
                    case "soul_lantern": Lantern(c); break;
                    case "bone_dice": Dice(c); break;
                    case "elder_tome": Tome(c); break;
                    case "blood_chalice": Chalice(c); break;
                }
                Finish(c);
                icons.Add(c);
            }
            return icons;
        }

        /// <summary>Door reward medallions (RoomReward order from Boon), then the trial skull.</summary>
        public static List<PixelCanvas> Rewards()
        {
            var icons = new List<PixelCanvas>();
            void Add(Color32 rim, Action<PixelCanvas> draw)
            {
                var c = Medallion(null, rim: rim);
                draw(c);
                Finish(c);
                icons.Add(c);
            }
            Add(Gold, Sunburst);
            Add(C("#5aa8e0"), c =>
            {
                Sword(c, 4, 4, 14, 14, White, Steel);
                // A bold upward arrow: "one level higher"
                c.Rect(16, 6, 3, 8, Blue);
                for (int k = 0; k < 4; k++) c.Rect(14 + k, 14 + k, 7 - k * 2, 1, k == 3 ? White : Blue);
                Sparks(c, C("#bfe8ff"));
            });
            Add(C("#3e9a52"), c => { Heart(c, C("#e04848"), C("#ff9a9a")); Plus(c, 17, 16, Green); });
            Add(C("#8a5ac8"), Crystals);
            Add(C("#c8902c"), Chest);
            Add(C("#b0302c"), Skull);
            return icons;
        }

        // ------------------------------------------------------------------ Frame

        private static PixelCanvas Medallion(ClassId? cls, bool trinket = false, Color32? rim = null)
        {
            var c = new PixelCanvas(Size, Size);
            c.Disc(12, 12, 11.5f, Ink);
            c.Disc(12, 12, 10.5f, rim ?? RimFor(cls, trinket));
            c.Disc(12, 12, 9.5f, C("#1a1419"));
            c.Disc(11.5f, 12.5f, 7.5f, C("#221b22"));
            return c;
        }

        private static Color32 RimFor(ClassId? cls, bool trinket) =>
            trinket ? C("#9a8a6a") : cls switch
            {
                ClassId.Warrior => C("#b0302c"),
                ClassId.Mage => C("#4a7cc8"),
                ClassId.Ranger => C("#4f9a4a"),
                _ => GoldDark,
            };

        /// <summary>Outline the symbol inside the medallion so it pops.</summary>
        private static void Finish(PixelCanvas c)
        {
            var copy = (Color32[])c.Pixels.Clone();
            var bg1 = C("#1a1419");
            var bg2 = C("#221b22");
            bool IsBg(Color32 p) => p.Equals(bg1) || p.Equals(bg2);
            for (int y = 1; y < Size - 1; y++)
            for (int x = 1; x < Size - 1; x++)
            {
                var p = copy[y * Size + x];
                if (!IsBg(p)) continue;
                bool touch = false;
                for (int k = 0; k < 4 && !touch; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    var q = copy[ny * Size + nx];
                    float dx = nx + 0.5f - 12f, dy = ny + 0.5f - 12f;
                    if (dx * dx + dy * dy < 9.5f * 9.5f && !IsBg(q) && q.a > 0) touch = true;
                }
                if (touch) c.Set(x, y, Ink);
            }
        }

        // ------------------------------------------------------------------ Perks

        private static void DrawPerk(PixelCanvas c, PerkId id)
        {
            switch (id)
            {
                case PerkId.Vitality: Heart(c, Red, C("#ff8a8a")); Plus(c, 15, 15, White); break;
                case PerkId.Swiftness: Boot(c); Streaks(c, Blue); break;
                case PerkId.Fury: Aura(c, Red); Sword(c, 5, 5, 18, 18, White, Steel); break;
                case PerkId.Focus: Hourglass(c); break;
                case PerkId.Lifesteal: Drop(c, Red, C("#ff8a8a")); Fang(c, White, small: true); break;
                case PerkId.GuardianBond: c.Ring(9, 12, 3.2f, 4.6f, Gold); c.Ring(15, 12, 3.2f, 4.6f, C("#ffe9b0")); break;
                case PerkId.Resonance: c.Ring(12, 12, 6f, 7f, Blue); c.Ring(12, 12, 3f, 4f, Amber); c.Disc(12, 12, 1.4f, White); break;
                case PerkId.Rapid: Sword(c, 5, 6, 15, 16, Steel, SteelDark); Sword(c, 9, 5, 19, 15, Steel, SteelDark); break;
                case PerkId.SecondWind: Heart(c, RedDark, Red); c.Line(12, 17, 10, 13, Ink); c.Line(10, 13, 13, 10, Ink); Sparks(c, Gold); break;
                case PerkId.Whirlwind: Spiral(c, Steel); break;
                case PerkId.SwordWave: c.Ring(9, 12, 7f, 9f, Blue); ClearLeft(c, 9); Sword(c, 4, 7, 11, 14, Steel, SteelDark); break;
                case PerkId.Bulwark: Shield(c, Steel, SteelDark, Gold); break;
                case PerkId.Crusher: Hammer(c); Shards(c, Blue); break;
                case PerkId.Rush: Sword(c, 8, 8, 19, 15, Steel, SteelDark); Streaks(c, Amber); break;
                case PerkId.Bloodlust: Claw(c, Red); break;
                case PerkId.SplitBolt: c.Disc(9, 12, 3.4f, Blue); c.Disc(9, 12, 1.8f, White); c.Disc(16, 16, 2.5f, Blue); c.Disc(16, 8, 2.5f, Blue); c.Line(12, 13, 14, 15, BlueDark); c.Line(12, 11, 14, 9, BlueDark); break;
                case PerkId.Multicast: c.Disc(7, 9, 2.6f, Blue); c.Disc(12, 15, 2.6f, Blue); c.Disc(17, 9, 2.6f, Blue); c.Set(7, 10, White); c.Set(12, 16, White); c.Set(17, 10, White); break;
                case PerkId.Permafrost: Snowflake(c); break;
                case PerkId.ChainBolt: Lightning(c); break;
                case PerkId.Mercy: Heart(c, GreenDark, Green); Sparks(c, White); break;
                case PerkId.Ignite: Flames(c, 12, 6, Red, big: true); break;
                case PerkId.Spread: Arrow(c, 5, 6, 18, 6); Arrow(c, 5, 12, 19, 12); Arrow(c, 5, 18, 18, 18); break;
                case PerkId.Piercing: c.Ring(14, 12, 3.5f, 5f, Red); Arrow(c, 3, 12, 20, 12); break;
                case PerkId.HunterEye: Eye(c); break;
                case PerkId.Venom: Drop(c, GreenDark, Toxic); c.Set(11, 10, Ink); c.Set(13, 10, Ink); break;
                case PerkId.DeepMark: c.Ring(12, 12, 6f, 7.2f, Amber); c.Disc(12, 12, 2f, Amber); c.Rect(11, 3, 2, 4, Amber); c.Rect(11, 17, 2, 4, Amber); c.Rect(3, 11, 4, 2, Amber); c.Rect(17, 11, 4, 2, Amber); break;
                case PerkId.Evasive: Feather(c, C("#dfe8f0"), C("#8fa8c2")); Streaks(c, Green); break;
                case PerkId.Executioner: Axe(c); break;
                case PerkId.SoulHarvest: Wisp(c); break;
                case PerkId.Comradery: Comrades(c); break;
                case PerkId.Blessing: Shield(c, C("#fff1c8"), Gold, White); Sparks(c, Gold); break;
                case PerkId.Earthshaker: Stomp(c); break;
                case PerkId.FrostArmor: Shield(c, Blue, BlueDark, White); c.Line(10, 9, 14, 13, White); c.Line(10, 13, 14, 9, White); break;
                case PerkId.Ricochet:
                    c.Line(3, 6, 11, 14, Wood);
                    c.Set(3, 7, Red); c.Set(4, 6, Red); c.Set(3, 5, Red);
                    c.Disc(11.5f, 14.5f, 1.8f, Amber); c.Set(11, 16, White); c.Set(13, 15, White);
                    Arrow(c, 12, 13, 18, 7);
                    break;
            }
        }

        // ------------------------------------------------------------------ Symbols (round 3)

        private static void Axe(PixelCanvas c)
        {
            c.Line(5, 3, 14, 17, Wood);
            c.Line(6, 3, 15, 17, C("#5a3e24"));
            // Crescent blade on the upper end
            for (int y = 9; y <= 21; y++)
            {
                int w = 7 - Mathf.Abs(y - 15) * 5 / 6;
                if (w <= 0) continue;
                c.Rect(13, y, w, 1, y > 15 ? Steel : SteelDark);
                c.Set(13 + w, y, White);
            }
            c.Set(20, 13, Red); c.Set(20, 12, Red); c.Set(19, 11, RedDark);
        }

        private static void Wisp(PixelCanvas c)
        {
            var soul = C("#5fd0ff");
            for (int i = 0; i < 7; i++) c.Disc(12 + Mathf.Sin(i * 0.9f) * 1.2f, 14 + i, 3.6f - i * 0.5f, soul);
            c.Disc(12, 10, 5f, soul);
            c.Disc(12, 10, 3.2f, C("#bff0ff"));
            c.Set(10, 11, Ink); c.Set(14, 11, Ink);
            c.Disc(5, 5, 1.3f, soul); c.Disc(19.5f, 6, 1.3f, soul); c.Set(4, 9, C("#bff0ff"));
        }

        private static void Comrades(PixelCanvas c)
        {
            var skin = C("#f0d2b0");
            void Person(int x, int y, Color32 cloth, Color32 dark)
            {
                c.Disc(x, y + 1, 3.6f, Ink);
                c.Disc(x, y + 1, 2.8f, cloth);
                c.Rect(x - 3, y - 3, 7, 3, cloth);
                c.Rect(x + 1, y - 3, 2, 4, dark);
                c.Disc(x, y + 5.5f, 2.6f, Ink);
                c.Disc(x, y + 5.5f, 1.9f, skin);
            }
            Person(12, 9, C("#6a8ad8"), C("#3a5ca0"));
            Person(7, 6, C("#d04848"), C("#8a2020"));
            Person(17, 6, C("#4aa85a"), C("#2a6a36"));
            c.Set(12, 20, Gold); c.Set(11, 19, Gold); c.Set(13, 19, Gold);
        }

        private static void Stomp(PixelCanvas c)
        {
            // A blade driven into the floor, cracks running out from it
            c.Rect(11, 8, 3, 9, Steel);
            c.Rect(13, 8, 1, 9, SteelDark);
            c.Set(12, 7, Steel);
            c.Rect(8, 16, 9, 2, Gold);
            c.Rect(11, 18, 3, 3, Wood);
            c.Line(12, 6, 4, 8, Amber); c.Line(12, 6, 20, 9, Amber);
            c.Line(12, 6, 7, 3, Amber); c.Line(12, 6, 17, 3, Amber);
            c.Set(3, 10, Amber); c.Set(21, 11, Amber); c.Set(5, 12, C("#fff3c0"));
        }

        private static void Sunburst(PixelCanvas c)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.2f;
                float len = k % 2 == 0 ? 7f : 5.2f;
                c.Line(12 + Mathf.Cos(a) * 3f, 12 + Mathf.Sin(a) * 3f, 12 + Mathf.Cos(a) * len, 12 + Mathf.Sin(a) * len, k % 2 == 0 ? Gold : Amber);
            }
            c.Disc(12, 12, 4f, Gold);
            c.Disc(12, 12, 2.6f, C("#fff3c0"));
            c.Set(11, 13, White);
        }

        private static void Crystals(PixelCanvas c)
        {
            void Crystal(float cx, float bottom, float height, float half, Color32 light, Color32 dark)
            {
                int top = Mathf.RoundToInt(bottom + height);
                for (int y = (int)bottom; y <= top; y++)
                {
                    float t = (y - bottom) / height;
                    float w = t < 0.7f ? half * (0.55f + t * 0.65f) : half * (1f - (t - 0.7f) / 0.3f);
                    for (int x = Mathf.RoundToInt(cx - w); x <= Mathf.RoundToInt(cx + w); x++) c.Set(x, y, x < cx ? light : dark);
                }
            }
            Crystal(7, 5, 9, 2.5f, C("#c9a8ff"), C("#6a3ab0"));
            Crystal(17, 5, 10, 2.5f, C("#c9a8ff"), C("#6a3ab0"));
            Crystal(12, 4, 15, 3.6f, C("#e4d4ff"), C("#8a5ad8"));
            c.Set(11, 14, White); c.Set(11, 15, White);
        }

        /// <summary>
        /// Gear icons on a clear background (the bag cell supplies the rarity frame). Order matches GameIcons.Item:
        /// worn weapon per class, keen weapon per class, then the armour, amulet and ring bases in catalog order.
        /// </summary>
        public static List<PixelCanvas> Items()
        {
            var icons = new List<PixelCanvas>();
            void Add(Action<PixelCanvas> draw)
            {
                var c = new PixelCanvas(Size, Size);
                draw(c);
                c.Outline(Ink);
                icons.Add(c);
            }
            var rust = C("#9a8f84");
            Add(c => Sword(c, 4, 4, 19, 19, rust, C("#c9c0b4")));                           // worn longsword
            Add(c => Staff(c, Wood, C("#7f9a7a"), C("#c4dcc0")));                           // worn staff
            Add(c => Bow(c, Wood, C("#5a3c22"), false));                                    // worn bow
            Add(c => { Sword(c, 4, 4, 17, 17, Steel, White); c.Disc(17, 17, 3.2f, SteelDark); c.Disc(16.5f, 17.5f, 2.2f, Steel); }); // keen axe
            Add(c => { Staff(c, C("#5a3c6e"), C("#5ab8ff"), C("#dff4ff")); Sparks(c, C("#bfe8ff")); }); // crystal staff
            Add(c => Bow(c, C("#4a2c18"), GoldDark, true));                                 // hunter's longbow
            Add(c => Armour(c, C("#8a5a32"), C("#5a3a1e"), C("#b07a46"), false));          // leather armour
            Add(c => Armour(c, Steel, SteelDark, White, true));                             // chainmail
            Add(c => Necklace(c, C("#e8dcc4"), C("#9a8a6a")));                              // bone necklace
            Add(c => { Necklace(c, Gold, GoldDark); c.Disc(12, 8, 3.4f, C("#dfe8ff")); c.Disc(13.6f, 9f, 2.8f, new Color32(0, 0, 0, 0)); }); // moon charm
            Add(c => GearRing(c, C("#c8783a"), C("#7a4018"), Red));                        // copper ring
            Add(c => GearRing(c, C("#dfe4ea"), C("#7a828c"), Blue));                       // silver ring
            return icons;
        }

        private static void Staff(PixelCanvas c, Color32 shaft, Color32 gem, Color32 shine)
        {
            c.Line(5, 3, 16, 16, shaft);
            c.Line(6, 3, 17, 16, shaft);
            c.Disc(17.5f, 17.5f, 3f, gem);
            c.Set(16, 19, shine);
            c.Set(17, 19, shine);
        }

        private static void Bow(PixelCanvas c, Color32 wood, Color32 tips, bool longbow)
        {
            float r = longbow ? 9.5f : 8f;
            for (int a = -75; a <= 75; a += 3)
            {
                float rad = a * Mathf.Deg2Rad;
                int x = Mathf.RoundToInt(8 + Mathf.Cos(rad) * r * 0.55f);
                int y = Mathf.RoundToInt(12 + Mathf.Sin(rad) * r);
                c.Set(x, y, wood);
                c.Set(x + 1, y, wood);
            }
            c.Line(8, Mathf.RoundToInt(12 - r * 0.97f), 8, Mathf.RoundToInt(12 + r * 0.97f), White);
            c.Set(9, Mathf.RoundToInt(12 + r * 0.97f), tips);
            c.Set(9, Mathf.RoundToInt(12 - r * 0.97f), tips);
            Arrow(c, 6, 12, 19, 12);
        }

        private static void Armour(PixelCanvas c, Color32 body, Color32 dark, Color32 light, bool mail)
        {
            c.Rect(7, 4, 10, 12, body);      // torso
            c.Rect(4, 13, 4, 5, body);       // shoulders
            c.Rect(16, 13, 4, 5, body);
            c.Rect(10, 15, 4, 4, new Color32(0, 0, 0, 0)); // neck opening
            c.Rect(7, 4, 10, 1, dark);
            c.Rect(11, 5, 2, 9, dark);
            if (mail)
                for (int y = 6; y < 16; y += 2)
                for (int x = 7 + (y / 2) % 2; x < 17; x += 2) c.Set(x, y, light);
            else
            {
                c.Rect(8, 9, 8, 1, dark);    // belt
                c.Set(12, 9, Gold);
                c.Set(5, 16, light); c.Set(17, 16, light);
            }
        }

        private static void Necklace(PixelCanvas c, Color32 pendant, Color32 chain)
        {
            for (int a = 200; a <= 340; a += 6)
            {
                float rad = a * Mathf.Deg2Rad;
                c.Set(Mathf.RoundToInt(12 + Mathf.Cos(rad) * 8f), Mathf.RoundToInt(19 + Mathf.Sin(rad) * 8f), chain);
            }
            c.Rect(11, 7, 3, 5, pendant);
            c.Rect(10, 10, 5, 1, pendant);
            c.Set(12, 6, pendant);
        }

        private static void GearRing(PixelCanvas c, Color32 band, Color32 dark, Color32 gem)
        {
            c.Ring(12, 9, 4.2f, 6.2f, band);
            c.Ring(12, 9, 4.2f, 4.9f, dark);
            c.Disc(12, 16, 2.6f, gem);
            c.Set(11, 17, White);
        }

        /// <summary>Treasure door: a banded chest with a gold lock and a glint.</summary>
        private static void Chest(PixelCanvas c)
        {
            var wood = C("#8a4e24");
            var woodDark = C("#5a3014");
            var woodLight = C("#b06a34");
            c.Rect(5, 5, 15, 7, wood);                      // body
            c.Rect(5, 5, 15, 1, woodDark);
            c.Rect(6, 12, 13, 4, woodLight);                // lid
            c.Rect(5, 13, 15, 2, woodLight);
            c.Rect(7, 16, 11, 1, woodLight);
            c.Rect(5, 12, 15, 1, GoldDark);                 // seam
            c.Rect(8, 5, 2, 12, Gold);                      // bands
            c.Rect(15, 5, 2, 12, Gold);
            c.Rect(11, 9, 3, 4, Gold);                      // lock
            c.Set(12, 10, Ink);
            c.Set(19, 18, White); c.Set(18, 19, White); c.Set(20, 19, White); c.Set(19, 20, White); // glint
        }

        private static void Skull(PixelCanvas c)
        {
            var bone = C("#ece2cc");
            var shade = C("#b8aa8c");
            c.Disc(12, 13, 6.2f, bone);
            c.Rect(8, 6, 9, 4, bone);
            c.Rect(14, 6, 3, 4, shade);
            c.Disc(9.5f, 12.5f, 2f, Ink);
            c.Disc(14.5f, 12.5f, 2f, Ink);
            c.Set(9, 12, Red); c.Set(14, 12, Red);
            c.Set(12, 10, Ink); c.Set(12, 9, Ink);
            for (int x = 9; x <= 15; x += 2) c.Set(x, 7, Ink);
        }

        private static void Lantern(PixelCanvas c)
        {
            var iron = C("#4a4650");
            c.Ring(12, 19, 1.5f, 2.6f, iron);
            c.Rect(7, 16, 11, 2, iron);
            c.Rect(7, 5, 11, 2, iron);
            c.Rect(8, 7, 9, 9, C("#1e3a4a"));
            c.Rect(8, 7, 1, 9, iron); c.Rect(16, 7, 1, 9, iron); c.Rect(12, 7, 1, 9, C("#2c2a30"));
            c.Disc(12, 10.5f, 2.6f, C("#5fd0ff"));
            c.Disc(12, 10.5f, 1.4f, C("#dff8ff"));
            c.Set(12, 13, C("#5fd0ff"));
        }

        private static void Dice(PixelCanvas c)
        {
            var bone = C("#ece2cc");
            var shade = C("#b8aa8c");
            c.Rect(4, 5, 8, 8, bone); c.Rect(4, 5, 8, 1, shade); c.Rect(11, 5, 1, 8, shade);
            c.Set(6, 10, Ink); c.Set(9, 7, Ink); c.Set(7, 8, RedDark);
            c.Rect(12, 11, 8, 8, bone); c.Rect(12, 11, 8, 1, shade); c.Rect(19, 11, 1, 8, shade);
            c.Set(14, 17, Ink); c.Set(17, 17, Ink); c.Set(14, 13, Ink); c.Set(17, 13, Ink); c.Set(15, 15, Red);
        }

        private static void Tome(PixelCanvas c)
        {
            var cover = C("#5a2a4a");
            c.Rect(5, 5, 13, 14, cover);
            c.Rect(17, 6, 2, 12, C("#e8e0d0"));
            c.Rect(5, 5, 2, 14, C("#3a1830"));
            c.Ring(11.5f, 12, 2f, 3.2f, Gold);
            c.Set(11, 12, C("#ff9ad8")); c.Set(12, 12, C("#ff9ad8"));
            c.Rect(16, 11, 3, 2, Gold);
        }

        private static void Chalice(PixelCanvas c)
        {
            for (int y = 12; y <= 18; y++)
            {
                int half = 2 + (y - 12) * 4 / 6;
                c.Rect(12 - half, y, half * 2 + 1, 1, y > 16 ? Gold : GoldDark);
            }
            c.Rect(8, 18, 9, 1, Red);
            c.Rect(9, 19, 7, 1, C("#ff5a5a"));
            c.Rect(11, 8, 3, 4, GoldDark);
            c.Rect(8, 5, 9, 3, Gold);
            c.Set(17, 17, Red); c.Set(17, 16, Red); c.Set(17, 14, RedDark);
        }

        // ------------------------------------------------------------------ Symbols

        private static void Heart(PixelCanvas c, Color32 col, Color32 light)
        {
            c.Disc(9, 14, 3.6f, col);
            c.Disc(15, 14, 3.6f, col);
            for (int y = 5; y < 14; y++)
            {
                int half = (y - 5) * 7 / 9;
                for (int x = 12 - half; x <= 12 + half; x++) c.Set(x, y, col);
            }
            c.Set(8, 15, light); c.Set(9, 16, light); c.Set(8, 16, light);
        }

        private static void Plus(PixelCanvas c, int x, int y, Color32 col)
        {
            c.Rect(x - 1, y - 3, 3, 7, col);
            c.Rect(x - 3, y - 1, 7, 3, col);
        }

        private static void Sword(PixelCanvas c, int x0, int y0, int x1, int y1, Color32 blade, Color32 edge)
        {
            c.Line(x0 + 2, y0 + 2, x1, y1, blade);
            c.Line(x0 + 3, y0 + 2, x1, y1 - 1, edge);
            // Guard and grip at the start
            c.Line(x0 + 3, y0, x0, y0 + 3, Gold);
            c.Line(x0 + 1, y0 + 1, x0 - 1, y0 - 1, Wood);
            c.Set(x1, y1, White);
        }

        private static void Flames(PixelCanvas c, int x, int y, Color32 col, bool big = false)
        {
            float s = big ? 1.6f : 1f;
            c.Disc(x, y + 2 * s, 3.2f * s, col);
            for (int i = 0; i < (int)(6 * s); i++) c.Set(x - 1 + i % 3, y + (int)(4 * s) + i, col);
            c.Disc(x, y + 2 * s, 1.8f * s, Amber);
            c.Set(x, (int)(y + 2 * s), C("#fff3c0"));
            if (big)
            {
                c.Disc(x - 4, y + 3, 2.2f, col);
                c.Disc(x + 4, y + 2, 2f, col);
                c.Rect(x - 1, y + 9, 2, 4, col);
            }
        }

        private static void Hourglass(PixelCanvas c)
        {
            c.Rect(6, 4, 12, 2, Wood);
            c.Rect(6, 18, 12, 2, Wood);
            for (int y = 6; y < 18; y++)
            {
                int half = Mathf.Abs(y - 12) * 5 / 6 + 1;
                for (int x = 12 - half; x < 12 + half; x++) c.Set(x, y, y < 12 ? C("#d8c890") : C("#5a6a80"));
            }
            for (int y = 7; y < 11; y++) c.Set(12, y, Amber);
            c.Rect(10, 13, 4, 2, Amber);
        }

        private static void Drop(PixelCanvas c, Color32 col, Color32 light)
        {
            c.Disc(12, 10, 5.2f, col);
            for (int y = 14; y < 20; y++)
            {
                int half = (20 - y) * 4 / 6;
                for (int x = 12 - half; x <= 12 + half; x++) c.Set(x, y, col);
            }
            c.Set(10, 11, light); c.Set(10, 12, light); c.Set(11, 13, light);
        }

        private static void Fang(PixelCanvas c, Color32 col, bool small = false)
        {
            int x0 = small ? 13 : 8, y0 = small ? 4 : 5, h = small ? 6 : 13;
            for (int y = 0; y < h; y++)
            {
                int half = (h - y) * (small ? 3 : 4) / h;
                for (int x = x0 - half; x <= x0 + half; x++) c.Set(x + y / 4, y0 + y, col);
            }
            if (!small) c.Rect(5, 17, 9, 2, C("#6b4a32"));
        }

        private static void Boot(PixelCanvas c)
        {
            var leather = C("#8a5f36");
            var dark = C("#4a3020");
            c.Rect(10, 9, 5, 9, leather);              // shaft
            c.Rect(10, 17, 6, 2, C("#c8a070"));        // cuff
            c.Rect(10, 5, 8, 4, leather);              // foot
            c.Disc(17.5f, 7f, 2.2f, leather);          // rounded toe
            c.Rect(9, 4, 11, 1, dark);                 // sole
            c.Rect(10, 4, 3, 2, dark);                 // heel
            // A small wing on the heel
            c.Line(9, 14, 4, 18, White); c.Line(9, 12, 4, 15, White); c.Line(9, 10, 5, 12, Blue);
        }

        private static void Aura(PixelCanvas c, Color32 col)
        {
            for (int k = -2; k <= 2; k++)
                c.Line(6 + k, 6 - k, 18 + k, 18 - k, new Color32(col.r, col.g, col.b, 255));
            c.Line(4, 9, 7, 6, Amber);
            c.Line(15, 20, 19, 16, Amber);
        }

        private static void Streaks(PixelCanvas c, Color32 col)
        {
            c.Line(3, 8, 7, 8, col);
            c.Line(2, 11, 6, 11, col);
            c.Line(3, 14, 6, 14, col);
        }

        private static void Spiral(PixelCanvas c, Color32 col)
        {
            for (float a = 0f; a < 12f; a += 0.08f)
            {
                float r = 1f + a * 0.65f;
                c.Set(Mathf.RoundToInt(12 + Mathf.Cos(a) * r), Mathf.RoundToInt(12 + Mathf.Sin(a) * r), a > 9f ? White : col);
            }
        }

        private static void ClearLeft(PixelCanvas c, int maxX)
        {
            var bg = C("#221b22");
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < maxX; x++)
            {
                float dx = x + 0.5f - 12f, dy = y + 0.5f - 12f;
                if (dx * dx + dy * dy < 7.5f * 7.5f && !c.Get(x, y).Equals(C("#1a1419"))) c.Set(x, y, bg);
            }
        }

        private static void Shield(PixelCanvas c, Color32 face, Color32 dark, Color32 trim)
        {
            for (int y = 4; y < 20; y++)
            {
                int half = y < 11 ? 7 : Mathf.Max(1, 7 - (y - 11) * 7 / 9);
                for (int x = 12 - half; x <= 12 + half; x++) c.Set(x, 23 - y, x < 12 ? face : dark);
            }
            for (int y = 6; y < 17; y++) c.Set(12, y, trim);
            c.Rect(8, 13, 9, 2, trim);
        }

        private static void Hammer(PixelCanvas c)
        {
            c.Line(6, 5, 15, 14, Wood);
            c.Line(7, 5, 16, 14, C("#5a3e24"));
            c.Rect(12, 13, 8, 6, SteelDark);
            c.Rect(12, 16, 8, 3, Steel);
        }

        private static void Shards(PixelCanvas c, Color32 col)
        {
            c.Line(4, 15, 6, 19, col); c.Line(5, 15, 7, 18, White);
            c.Line(17, 6, 20, 4, col);
            c.Line(4, 9, 3, 6, col);
        }

        private static void Claw(PixelCanvas c, Color32 col)
        {
            for (int k = 0; k < 3; k++)
            {
                int x = 7 + k * 4;
                c.Line(x, 19, x + 4, 5, col);
                c.Line(x + 1, 19, x + 5, 5, RedDark);
            }
            c.Set(9, 6, C("#ff8a8a"));
        }

        private static void Snowflake(PixelCanvas c)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI / 3f;
                float dx = Mathf.Cos(a) * 8f, dy = Mathf.Sin(a) * 8f;
                c.Line(12 - dx, 12 - dy, 12 + dx, 12 + dy, Blue);
                for (int s = -1; s <= 1; s += 2)
                {
                    float bx = 12 + dx * 0.6f * s, by = 12 + dy * 0.6f * s;
                    c.Line(bx, by, bx + Mathf.Cos(a + 0.8f * s) * 3f * s, by + Mathf.Sin(a + 0.8f * s) * 3f * s, White);
                }
            }
            c.Disc(12, 12, 1.5f, White);
        }

        private static void Lightning(PixelCanvas c)
        {
            Vector2[] pts = { new(5, 17), new(10, 11), new(13, 14), new(19, 6) };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                c.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, Blue);
                c.Line(pts[i].x + 1, pts[i].y, pts[i + 1].x + 1, pts[i + 1].y, White);
            }
            foreach (var p in pts) c.Disc(p.x + 0.5f, p.y + 0.5f, 1.6f, BlueDark);
        }

        private static void Arrow(PixelCanvas c, int x0, int y0, int x1, int y1)
        {
            c.Line(x0, y0, x1, y1, Wood);
            c.Set(x1 + 1, y1, Steel); c.Set(x1, y1 + 1, Steel); c.Set(x1, y1 - 1, Steel); c.Set(x1 + 2, y1, White);
            c.Set(x0, y0 + 1, Red); c.Set(x0, y0 - 1, Red); c.Set(x0 + 1, y0 + 1, Red); c.Set(x0 + 1, y0 - 1, Red);
        }

        private static void Eye(PixelCanvas c)
        {
            for (int x = 4; x < 21; x++)
            {
                float t = (x - 12f) / 8.5f;
                int half = Mathf.RoundToInt((1f - t * t) * 5f);
                for (int y = 12 - half; y <= 12 + half; y++) c.Set(x, y, White);
            }
            c.Disc(12, 12, 3.6f, Amber);
            c.Disc(12, 12, 1.8f, Ink);
            c.Set(13, 13, White);
        }

        private static void Feather(PixelCanvas c, Color32 col, Color32 dark)
        {
            for (int i = 0; i < 13; i++)
            {
                int cx = 7 + i * 10 / 13, cy = 5 + i;
                int half = Mathf.RoundToInt(Mathf.Sin(i / 12f * Mathf.PI) * 3.5f);
                for (int k = -half; k <= half; k++) c.Set(cx + k, cy - k / 2, k < 0 ? col : dark);
            }
            c.Line(6, 4, 17, 18, Wood);
        }

        private static void Sparks(PixelCanvas c, Color32 col)
        {
            foreach (var (x, y) in new[] { (5, 18), (18, 18), (19, 7) })
            {
                c.Set(x, y, col); c.Set(x + 1, y, col); c.Set(x - 1, y, col); c.Set(x, y + 1, col); c.Set(x, y - 1, col);
            }
        }

        private static void Campfire(PixelCanvas c)
        {
            c.Line(5, 6, 18, 9, Wood);
            c.Line(5, 9, 18, 6, C("#6b4a32"));
            Flames(c, 12, 7, Red, big: false);
            c.Disc(12, 13, 3f, Amber);
            c.Set(12, 15, C("#fff3c0"));
        }

        private static void Amulet(PixelCanvas c)
        {
            c.Line(7, 19, 12, 13, Gold);
            c.Line(17, 19, 12, 13, Gold);
            c.Disc(12, 10, 4.2f, GoldDark);
            c.Disc(12, 10, 3f, Red);
            c.Set(11, 11, C("#ff9a9a"));
        }

        private static void Urn(PixelCanvas c)
        {
            c.Disc(12, 10, 5f, C("#9c958a"));
            c.Rect(9, 15, 7, 2, C("#6b645a"));
            c.Rect(10, 17, 5, 2, C("#cbc5b8"));
            c.Rect(8, 4, 9, 2, C("#6b645a"));
            for (int i = 0; i < 4; i++) c.Set(10 + i, 19 + i % 2, C("#d8d0c0"));
        }

        private static void Ring(PixelCanvas c)
        {
            c.Ring(12, 10, 4f, 6f, Gold);
            c.Disc(12, 16, 2.8f, C("#5fa8ff"));
            c.Set(11, 17, White);
        }
    }
}
