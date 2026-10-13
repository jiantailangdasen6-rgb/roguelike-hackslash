using System.Collections.Generic;
using Game.Core;

namespace Game.EditorTools.Art
{
    /// <summary>Palette swaps for class skins (see <see cref="SkinCatalog"/>). Index 0 is the standard palette.</summary>
    public static class SkinArt
    {
        private static PixelMaterial R(params string[] hex) => new(2, hex);

        private static readonly PixelMaterial Silver = R("#2a2c30", "#4a4e55", "#767c86", "#a4abb6", "#d6dce4");
        private static readonly PixelMaterial Bronze = R("#22140a", "#4a2c14", "#7a4a20", "#a86a32", "#d0945a");

        /// <summary>A class's own base palette with some letters repainted.</summary>
        private static Dictionary<char, PixelMaterial> Over(Dictionary<char, PixelMaterial> map, params (char Letter, PixelMaterial Material)[] swaps)
        {
            foreach (var (letter, material) in swaps) map[letter] = material;
            return map;
        }

        public static Dictionary<char, PixelMaterial> Palette(ClassId cls, int index)
        {
            switch (cls)
            {
                case ClassId.Warrior:
                    return index switch
                    {
                        1 => Art.Palette.With( // 黒騎士: blackened steel, violet cape, silver trim
                            ('m', R("#0c0d12", "#1a1c24", "#2b2e3a", "#434858", "#636a7e")),
                            ('c', R("#160a22", "#2a1340", "#42205f", "#5e3282", "#7f4aa8")),
                            ('g', Silver)),
                        2 => Art.Palette.With( // 聖騎士: white steel, royal blue, gold
                            ('m', R("#3a3e48", "#6b7280", "#a3abb8", "#d0d6de", "#f4f6f8")),
                            ('c', R("#0a1428", "#12244a", "#1d3a74", "#2c56a0", "#4a7cc8"))),
                        3 => Art.Palette.With( // 血濡れの狂戦士: rusted plate, black rags, crimson
                            ('m', Art.Palette.Rust),
                            ('c', R("#0b090c", "#171318", "#241e26", "#332b36", "#463b4a")),
                            ('g', Art.Palette.Crimson),
                            ('w', R("#2a1214", "#5a2228", "#8a3a3a", "#b86a5a", "#e0b0a0"))),
                        _ => null,
                    };
                case ClassId.Mage:
                    return index switch
                    {
                        1 => Art.Palette.With( // 氷の賢者
                            ('r', R("#1c2633", "#34465c", "#56708e", "#86a2bf", "#c0d4e6")),
                            ('g', Silver)),
                        2 => Art.Palette.With( // 紅蓮の魔女
                            ('r', R("#260808", "#4a1010", "#7a1c12", "#a83218", "#d4582a")),
                            ('f', new PixelMaterial("#ffb04a")),
                            ('F', new PixelMaterial("#fff0c8"))),
                        3 => Art.Palette.With( // 深緑の隠者
                            ('r', R("#0a140c", "#132616", "#1f3d24", "#2f5a36", "#46804e")),
                            ('f', new PixelMaterial("#b6ff4a")),
                            ('F', new PixelMaterial("#f0ffd0")),
                            ('g', Bronze)),
                        _ => null,
                    };
                case ClassId.Guardian:
                    return index switch
                    {
                        1 => Over(HeroArt.GuardianBase(), // 黒鉄の門番: blackened iron, dark red tabard, silver
                            ('m', R("#0e0f13", "#1c1e25", "#2d3039", "#454a57", "#656c7c")),
                            ('c', R("#1a0606", "#330c0e", "#521418", "#721f22", "#93302f")),
                            ('g', Silver)),
                        2 => Over(HeroArt.GuardianBase(), // 黄金の聖堂騎士: gilded plate, silver trim
                            ('m', R("#3a2a0e", "#6a4e1a", "#9c7a2c", "#c9a648", "#efd98a")),
                            ('g', R("#2a2c30", "#4a4e55", "#767c86", "#a4abb6", "#f0f4f8"))),
                        3 => Over(HeroArt.GuardianBase(), // 苔むした古兵: verdigris plate, moss tabard, bronze
                            ('m', R("#13201c", "#223a33", "#36574c", "#517a6a", "#76a08c")),
                            ('c', Art.Palette.Grave),
                            ('g', Bronze)),
                        _ => null,
                    };
                case ClassId.Assassin:
                    return index switch
                    {
                        1 => Over(HeroArt.AssassinBase(), // 深紅の影
                            ('h', R("#1a0507", "#33090d", "#520f15", "#72161d", "#952229")),
                            ('n', R("#0b090c", "#171318", "#241e26", "#332b36", "#463b4a"))),
                        2 => Over(HeroArt.AssassinBase(), // 灰の亡霊
                            ('h', R("#2a2a2e", "#47474d", "#6a6a72", "#9393a0", "#c0c0cc")),
                            ('l', R("#1c1a1a", "#302d2c", "#47423f", "#615a55", "#7f7670"))),
                        3 => Over(HeroArt.AssassinBase(), // 毒蛇
                            ('h', R("#0a1a0c", "#123018", "#1c4a24", "#2a6834", "#3d8a48")),
                            ('e', new PixelMaterial("#b6ff4a")),
                            ('w', R("#1a2a12", "#2f4a20", "#4f7a34", "#7aaa4e", "#b6ff4a"))),
                        _ => null,
                    };
                case ClassId.Deathcaller:
                    return index switch
                    {
                        1 => Over(HeroArt.DeathcallerBase(), // 血の降霊師
                            ('r', R("#140608", "#260b10", "#3d1219", "#561a23", "#74242e")),
                            ('f', new PixelMaterial("#ff5a4a")),
                            ('F', new PixelMaterial("#ffd8c8"))),
                        2 => Over(HeroArt.DeathcallerBase(), // 灰の巫女
                            ('r', R("#26262a", "#424248", "#64646c", "#8c8c96", "#b8b8c4")),
                            ('h', R("#1a1a1e", "#2e2e34", "#46464e", "#62626c", "#82828e")),
                            ('f', new PixelMaterial("#bfe8ff")),
                            ('F', new PixelMaterial("#f0faff"))),
                        3 => Over(HeroArt.DeathcallerBase(), // 奈落の導師
                            ('r', Art.Palette.Violet),
                            ('h', R("#0a0612", "#140c22", "#201436", "#2e1e4a", "#3e2a62")),
                            ('f', new PixelMaterial("#c58bff")),
                            ('F', new PixelMaterial("#f2e4ff"))),
                        _ => null,
                    };
                case ClassId.Paladin:
                    return index switch
                    {
                        1 => Over(HeroArt.PaladinBase(), // 緋の誓約者
                            ('c', R("#1c080c", "#3a0e14", "#5f151d", "#8a1f27", "#b3333a"))),
                        2 => Over(HeroArt.PaladinBase(), // 黒曜の審問官
                            ('m', R("#0c0d12", "#1a1c24", "#2b2e3a", "#434858", "#636a7e")),
                            ('c', R("#0a0a0c", "#16161a", "#24242a", "#34343c", "#484852")),
                            ('g', Silver)),
                        3 => Over(HeroArt.PaladinBase(), // 白百合の騎士
                            ('c', R("#3a3530", "#6b645a", "#9c958a", "#cbc5b8", "#efeadf")),
                            ('g', R("#2e2a1a", "#56502e", "#8a8250", "#c4ba7a", "#f0e8b0"))),
                        _ => null,
                    };
                case ClassId.Alchemist:
                    return index switch
                    {
                        1 => Over(HeroArt.AlchemistBase(), // 翡翠の薬師
                            ('u', R("#0a1c16", "#123228", "#1c4e3e", "#2a6e58", "#3f9478")),
                            ('f', new PixelMaterial("#6affd8")),
                            ('F', new PixelMaterial("#e0fff6"))),
                        2 => Over(HeroArt.AlchemistBase(), // 紫煙の調合師
                            ('u', Art.Palette.Violet),
                            ('l', R("#120d16", "#211826", "#33263a", "#47364f", "#5e4868")),
                            ('f', new PixelMaterial("#d88aff")),
                            ('F', new PixelMaterial("#f6e6ff"))),
                        3 => Over(HeroArt.AlchemistBase(), // 黄金の賢者
                            ('u', R("#2e1f0a", "#573d12", "#87641c", "#bb912f", "#e6c766")),
                            ('l', R("#2a2c30", "#4a4e55", "#767c86", "#a4abb6", "#d6dce4")),
                            ('f', new PixelMaterial("#ffd060")),
                            ('F', new PixelMaterial("#fff6d0"))),
                        _ => null,
                    };
                default:
                    return index switch
                    {
                        1 => Art.Palette.With( // 影の追跡者
                            ('h', R("#0d0d10", "#1a1a20", "#2a2a33", "#3d3d4a", "#565666")),
                            ('l', R("#120d0a", "#221812", "#33241a", "#463224", "#5c4230"))),
                        2 => Art.Palette.With( // 秋の旅人
                            ('h', R("#1f0f06", "#3d1f0c", "#633314", "#8c4b1e", "#b8692d")),
                            ('l', R("#2a1c10", "#4a321c", "#6e4c2c", "#94683e", "#b88a56"))),
                        3 => Art.Palette.With( // 雪原の射手
                            ('h', R("#3a4250", "#66707f", "#98a2b0", "#c8d0da", "#eef2f6")),
                            ('l', R("#1c1e22", "#33363c", "#4c5058", "#686d76", "#8a909a")),
                            ('g', Silver)),
                        _ => null,
                    };
            }
        }
    }
}
