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

            var warriorFlash = Character("warrior", HeroArt.Warrior(), HeroArt.FrameSize);
            var mageFlash = Character("mage", HeroArt.Mage(), HeroArt.FrameSize);
            var rangerFlash = Character("ranger", HeroArt.Ranger(), HeroArt.FrameSize);
            var guardianFlash = Character("guardian", HeroArt.Guardian(), HeroArt.FrameSize);
            var assassinFlash = Character("assassin", HeroArt.Assassin(), HeroArt.FrameSize);
            var alchemistFlash = Character("alchemist", HeroArt.Alchemist(), HeroArt.FrameSize);
            // Skins are palette swaps: same silhouettes, so they share the class's white flash sheet.
            for (int skin = 1; skin < Game.Core.SkinCatalog.PerClass; skin++)
            {
                Character($"warrior_s{skin}", HeroArt.Warrior(SkinArt.Palette(Game.Core.ClassId.Warrior, skin)), HeroArt.FrameSize, warriorFlash);
                Character($"mage_s{skin}", HeroArt.Mage(SkinArt.Palette(Game.Core.ClassId.Mage, skin)), HeroArt.FrameSize, mageFlash);
                Character($"ranger_s{skin}", HeroArt.Ranger(SkinArt.Palette(Game.Core.ClassId.Ranger, skin)), HeroArt.FrameSize, rangerFlash);
                Character($"guardian_s{skin}", HeroArt.Guardian(SkinArt.Palette(Game.Core.ClassId.Guardian, skin)), HeroArt.FrameSize, guardianFlash);
                Character($"assassin_s{skin}", HeroArt.Assassin(SkinArt.Palette(Game.Core.ClassId.Assassin, skin)), HeroArt.FrameSize, assassinFlash);
                Character($"alchemist_s{skin}", HeroArt.Alchemist(SkinArt.Palette(Game.Core.ClassId.Alchemist, skin)), HeroArt.FrameSize, alchemistFlash);
            }

            Character("skeleton", EnemyArt.Skeleton(), EnemyArt.FrameSize);
            Character("skeleton_archer", EnemyArt.SkeletonArcher(), EnemyArt.FrameSize);
            Character("ghoul", EnemyArt.Ghoul(), EnemyArt.FrameSize);
            Character("cultist", EnemyArt.Cultist(), EnemyArt.FrameSize);
            Character("bone_king", EnemyArt.BoneKing(), EnemyArt.BossFrameSize);
            Character("bat", EnemyArt.Bat(), EnemyArt.FrameSize);
            Character("bloater", EnemyArt.Bloater(), EnemyArt.FrameSize);
            Character("dread_knight", EnemyArt.DreadKnight(), EnemyArt.FrameSize);
            Character("necromancer", EnemyArt.Necromancer(), EnemyArt.FrameSize);
            Character("wraith", EnemyArt.Wraith(), EnemyArt.FrameSize);
            Character("powder_keg", EnemyArt.PowderKeg(), EnemyArt.FrameSize);
            Character("blood_bishop", EnemyArt.BloodBishop(), EnemyArt.BossFrameSize);
            Character("lich", EnemyArt.Lich(), EnemyArt.BossFrameSize);
            Character("shield_skeleton", EnemyArt.ShieldSkeleton(), EnemyArt.FrameSize);
            Character("flagellant", EnemyArt.Flagellant(), EnemyArt.FrameSize);
            Character("abyss_eye", EnemyArt.AbyssEye(), EnemyArt.FrameSize);
            Character("shade_stalker", EnemyArt.ShadeStalker(), EnemyArt.FrameSize);
            // The camp's residents (idle only).
            Character("npc_master", CampArt.Master(), HeroArt.FrameSize);
            Character("npc_crone", CampArt.Crone(), HeroArt.FrameSize);
            Character("npc_keeper", CampArt.Gravekeeper(), HeroArt.FrameSize);
            Character("treasure_chest", CampArt.TreasureChest(), EnemyArt.FrameSize);
            // The bosses' trials.
            Character("bone_brazier", GimmickArt.BoneBrazier(), EnemyArt.FrameSize);
            Character("bone_cage", GimmickArt.BoneCage(), EnemyArt.FrameSize);
            Character("blood_sigil", GimmickArt.BloodSigil(), EnemyArt.FrameSize);
            Character("soul_vessel", GimmickArt.SoulVessel(), EnemyArt.FrameSize);
            Character("pressure_plate", GimmickArt.PressurePlate(), EnemyArt.FrameSize);
            Character("ward_crystal", GimmickArt.WardCrystal(), EnemyArt.FrameSize);
            Character("cracked_wall", GimmickArt.CrackedWall(), EnemyArt.FrameSize);
            Character("treasure_imp", GimmickArt.TreasureImp(), EnemyArt.FrameSize);

            foreach (var (name, gen) in ProceduralArt.Effects) Effect(name, gen());
            foreach (var (name, sheet) in PropArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            foreach (var (name, sheet) in DoorArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            foreach (var (name, sheet) in DecorArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            foreach (var (name, sheet) in PingArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            foreach (var (name, sheet) in CampArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            Effect("icons_perk", IconArt.Perks(), "icons_perk");
            Effect("icons_trinket", IconArt.Trinkets(), "icons_trinket");
            Effect("icons_reward", IconArt.Rewards(), "icons_reward");
            Effect("icons_item", IconArt.Items(), "icons_item");
            var skills = SkillArt.Icons();
            Effect("icons_skill", skills, "icons_skill");
            WritePreview("props", PropArt.Sheets());
            WritePreview("doors", DoorArt.Sheets());
            WritePreview("decor", DecorArt.Sheets());
            WritePreview("pings", PingArt.Sheets());
            WritePreview("skills", new Dictionary<string, PropArt.Sheet> { ["icons_skill"] = new() { Frames = skills, Pivot = new Vector2(0.5f, 0.5f) } });
            WritePreview("camp", CampArt.Sheets());

            var tiles = ProceduralArt.Tiles();
            foreach (var (name, canvas) in PropArt.AreaTiles()) tiles[name] = canvas;
            foreach (var (name, canvas) in CampArt.Tiles()) tiles[name] = canvas;
            foreach (var (name, canvas) in tiles)
            {
                string path = $"{TileDir}/{name}.png";
                PixelCanvas.WriteStrip(path, new[] { canvas });
                Import(path, name == "pillar" ? new Vector2(0.5f, 0.05f) : new Vector2(0.5f, 0.5f));
            }
            foreach (var (name, canvas) in PropArt.Decor())
            {
                string path = $"{TileDir}/{name}.png";
                PixelCanvas.WriteStrip(path, new[] { canvas });
                Import(path, new Vector2(0.5f, 0.02f));
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ArtGenerator] Done");
        }

        /// <summary>Contact sheet of the props on a dark background (Logs/art/props.png) for review.</summary>
        private static void WritePreview(string name, Dictionary<string, PropArt.Sheet> sheets)
        {
            const int scale = 3;
            int width = 0, height = 0, rowHeight = 0, x = 0;
            var placed = new List<(PixelCanvas Canvas, int X, int Y)>();
            foreach (var sheet in sheets.Values)
            foreach (var frame in sheet.Frames)
            {
                if (x + frame.Width > 420)
                {
                    x = 0;
                    height += rowHeight + 4;
                    rowHeight = 0;
                }
                placed.Add((frame, x, height));
                x += frame.Width + 4;
                width = Mathf.Max(width, x);
                rowHeight = Mathf.Max(rowHeight, frame.Height);
            }
            height += rowHeight;
            var bg = PixelMaterial.Hex("#2b2833");
            int W = width * scale, H = height * scale;
            var pixels = new Color32[W * H];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
            foreach (var (canvas, px, py) in placed)
                for (int cy = 0; cy < canvas.Height; cy++)
                for (int cx = 0; cx < canvas.Width; cx++)
                {
                    var c = canvas.Get(cx, cy);
                    if (c.a == 0) continue;
                    int topRow = py + (canvas.Height - 1 - cy); // rows counted from the top of the sheet
                    for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                        pixels[(H - 1 - (topRow * scale + sy)) * W + (px + cx) * scale + sx] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            ArtIO.WriteBytes($"{PreviewDir}/{name}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <returns>The flash texture (white silhouettes), for sharing with skins.</returns>
        private static Texture2D Character(string name, List<Clip> clips, int frameSize, Texture2D sharedFlash = null)
        {
            string path = $"{CharacterDir}/{name}.png";
            PixelRenderer.WriteSheet(path, clips, frameSize);
            PixelRenderer.WritePreview($"{PreviewDir}/{name}.png", clips, frameSize, frameSize > 64 ? 3 : 4);
            var texture = Import(path, null);
            var flashTexture = sharedFlash;
            if (flashTexture == null)
            {
                string flashPath = $"{CharacterDir}/{name}_flash.png";
                PixelRenderer.WriteSheet(flashPath, clips, frameSize, flash: true);
                flashTexture = Import(flashPath, null);
            }

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
            return flashTexture;
        }

        private static Vector2 EffectPivot(string name) => name switch
        {
            "cone" or "line" or "flame" => new Vector2(0f, 0.5f),
            "candle" or "candle_red" or "candle_soul" => new Vector2(0.5f, 0.05f),
            _ => new Vector2(0.5f, 0.5f),
        };

        /// <param name="sheetName">Defaults to fx_&lt;name&gt;.</param>
        private static void Effect(string name, List<PixelCanvas> frames, string sheetName = null, Vector2? pivot = null)
        {
            string path = $"{EffectDir}/{name}.png";
            PixelCanvas.WriteStrip(path, frames);
            var texture = Import(path, null);

            var sheet = LoadOrCreateSheet(sheetName ?? "fx_" + name);
            sheet.Texture = texture;
            sheet.FrameWidth = frames[0].Width;
            sheet.FrameHeight = frames[0].Height;
            sheet.Pivot = pivot ?? EffectPivot(name);
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
