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
