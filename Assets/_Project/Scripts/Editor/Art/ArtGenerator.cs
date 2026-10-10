using System;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Generates all pixel art: character/enemy sheets and effect strips (+ SpriteSheet assets in Resources/Sheets),
    /// environment tiles, and enlarged previews in Logs/art/ for review.
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.Art.ArtGenerator.Generate
    /// </summary>
    public static class ArtGenerator
    {
        public const string CharacterDir = "Assets/_Project/Art/Characters";
        public const string EffectDir = "Assets/_Project/Art/Effects";
        public const string TileDir = "Assets/_Project/Art/Environment";
        public const string SheetDir = "Assets/_Project/Resources/Sheets";
        private const string PreviewDir = "Logs/art";

        /// <summary>Feet sit this many pixels above the bottom of a character frame.</summary>
        public const int GroundY = 10;

        [MenuItem("Tools/Game/Generate Pixel Art")]
        public static void Generate()
        {
            foreach (var dir in new[] { CharacterDir, EffectDir, TileDir, SheetDir, PreviewDir }) Directory.CreateDirectory(dir);

            Character("warrior", HeroArt.Warrior(), HeroArt.FrameSize);
            Character("mage", HeroArt.Mage(), HeroArt.FrameSize);
            Character("ranger", HeroArt.Ranger(), HeroArt.FrameSize);
            Character("skeleton", EnemyArt.Skeleton(), EnemyArt.FrameSize);
            Character("skeleton_archer", EnemyArt.SkeletonArcher(), EnemyArt.FrameSize);
            Character("ghoul", EnemyArt.Ghoul(), EnemyArt.FrameSize);
            Character("cultist", EnemyArt.Cultist(), EnemyArt.FrameSize);
            Character("bone_king", EnemyArt.BoneKing(), EnemyArt.BossFrameSize);

            foreach (var (name, gen) in ProceduralArt.Effects) Effect(name, gen());
            foreach (var (name, canvas) in ProceduralArt.Tiles())
            {
                string path = $"{TileDir}/{name}.png";
                PixelCanvas.WriteStrip(path, new[] { canvas });
                Import(path, name == "pillar" ? new Vector2(0.5f, 0.05f) : new Vector2(0.5f, 0.5f));
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ArtGenerator] Done");
        }

        private static void Character(string name, List<Clip> clips, int frameSize)
        {
            string path = $"{CharacterDir}/{name}.png";
            PixelRenderer.WriteSheet(path, clips, frameSize);
            PixelRenderer.WritePreview($"{PreviewDir}/{name}.png", clips, frameSize, frameSize > 64 ? 3 : 4);
            var texture = Import(path, null);
            string flashPath = $"{CharacterDir}/{name}_flash.png";
            PixelRenderer.WriteSheet(flashPath, clips, frameSize, flash: true);
            var flashTexture = Import(flashPath, null);

            var sheet = LoadOrCreateSheet(name);
            sheet.Texture = texture;
            sheet.FlashTexture = flashTexture;
            sheet.FrameWidth = frameSize;
            sheet.FrameHeight = frameSize;
            sheet.Pivot = new Vector2(0.5f, (float)GroundY / frameSize);
            sheet.PixelsPerUnit = 32f;
            sheet.Clips.Clear();
            for (int i = 0; i < clips.Count; i++)
                sheet.Clips.Add(new SpriteSheet.ClipInfo { Name = clips[i].Name, Row = i, Frames = clips[i].Frames.Count, Fps = clips[i].Fps, Loop = clips[i].Loop });
            EditorUtility.SetDirty(sheet);
        }

        private static void Effect(string name, List<PixelCanvas> frames)
        {
            string path = $"{EffectDir}/{name}.png";
            PixelCanvas.WriteStrip(path, frames);
            var texture = Import(path, null);

            var sheet = LoadOrCreateSheet("fx_" + name);
            sheet.Texture = texture;
            sheet.FrameWidth = frames[0].Width;
            sheet.FrameHeight = frames[0].Height;
            sheet.Pivot = name == "cone" ? new Vector2(0f, 0.5f) : name == "line" ? new Vector2(0f, 0.5f) : name == "candle" ? new Vector2(0.5f, 0.05f) : new Vector2(0.5f, 0.5f);
            sheet.PixelsPerUnit = 32f;
            sheet.Clips.Clear();
            sheet.Clips.Add(new SpriteSheet.ClipInfo { Name = "default", Row = 0, Frames = frames.Count, Fps = 12f, Loop = true });
            EditorUtility.SetDirty(sheet);
        }

        private static SpriteSheet LoadOrCreateSheet(string name)
        {
            string path = $"{SheetDir}/{name}.asset";
            var sheet = AssetDatabase.LoadAssetAtPath<SpriteSheet>(path);
            if (sheet != null) return sheet;
            sheet = ScriptableObject.CreateInstance<SpriteSheet>();
            AssetDatabase.CreateAsset(sheet, path);
            return sheet;
        }

        /// <summary>Imports as a crisp, uncompressed texture; with a pivot it is also a single sprite (tiles/props).</summary>
        private static Texture2D Import(string path, Vector2? spritePivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (spritePivot.HasValue)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = spritePivot.Value;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
