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

            // Each class with its usual and its other weapon ("_w1", HeroArt.Weapons). Skins are palette swaps, made at
            // runtime from these sheets (SpriteSheet.RecolorOf): the build carries one sheet per class and weapon.
            foreach (Game.Core.ClassId cls in System.Enum.GetValues(typeof(Game.Core.ClassId)))
            {
                List<Clip> Make(Dictionary<char, PixelMaterial> palette) => cls switch
                {
                    Game.Core.ClassId.Warrior => HeroArt.Warrior(palette),
                    Game.Core.ClassId.Mage => HeroArt.Mage(palette),
                    Game.Core.ClassId.Ranger => HeroArt.Ranger(palette),
                    Game.Core.ClassId.Guardian => HeroArt.Guardian(palette),
                    Game.Core.ClassId.Assassin => HeroArt.Assassin(palette),
                    Game.Core.ClassId.Alchemist => HeroArt.Alchemist(palette),
                    Game.Core.ClassId.Deathcaller => HeroArt.Deathcaller(palette),
                    _ => HeroArt.Paladin(palette),
                };
                string sheet = Game.Core.ClassCatalog.Get(cls).Sheet;
                Character(sheet, Make(null), HeroArt.FrameSize, readable: true);
                Character($"{sheet}_w1", HeroArt.WithAltWeapon(cls, () => Make(null)), HeroArt.FrameSize, readable: true);
                for (int skin = 1; skin < Game.Core.SkinCatalog.PerClass; skin++)
                {
                    var palette = SkinArt.Palette(cls, skin);
                    Skin($"{sheet}_s{skin}", sheet, Make(palette), HeroArt.FrameSize);
                    Skin($"{sheet}_s{skin}_w1", $"{sheet}_w1", HeroArt.WithAltWeapon(cls, () => Make(palette)), HeroArt.FrameSize);
                }
            }

            Character("skeleton", EnemyArt.Skeleton(), EnemyArt.FrameSize);
            Character("bone_servant", EnemyArt.Skeleton(), EnemyArt.FrameSize); // a deathcaller's summon (tinted green at runtime)
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
            // Room events (round 5).
            Character("mimic", EventArt.Mimic(), EnemyArt.FrameSize);
            Character("captive", EventArt.Captive(), EnemyArt.FrameSize);
            // The new lands (round 6).
            Character("drowned", EnemyArt.Drowned(), EnemyArt.FrameSize);
            Character("carrion_crab", EnemyArt.CarrionCrab(), EnemyArt.FrameSize);
            Character("tide_witch", EnemyArt.TideWitch(), EnemyArt.FrameSize);
            Character("drowned_saint", EnemyArt.DrownedSaint(), EnemyArt.BossFrameSize);
            Character("flood_seal", GimmickArt.FloodSeal(), EnemyArt.FrameSize);
            Character("frost_wolf", EnemyArt.FrostWolf(), EnemyArt.FrameSize);
            Character("icebound", EnemyArt.Icebound(), EnemyArt.FrameSize);
            Character("frost_witch", EnemyArt.FrostWitch(), EnemyArt.FrameSize);
            Character("frost_queen", EnemyArt.FrostQueen(), EnemyArt.BossFrameSize);
            Character("iron_sentinel", EnemyArt.IronSentinel(), EnemyArt.FrameSize);
            Character("fire_imp", EnemyArt.FireImp(), EnemyArt.FrameSize);
            Character("slag_bloater", EnemyArt.SlagBloater(), EnemyArt.FrameSize);
            Character("forge_colossus", EnemyArt.ForgeColossus(), EnemyArt.BossFrameSize);
            Character("quench_cistern", GimmickArt.QuenchCistern(), EnemyArt.FrameSize);
            Character("urn", GimmickArt.Urn(), EnemyArt.FrameSize);
            Character("ashen_king", EnemyArt.AshenKing(), EnemyArt.BossFrameSize);
            Character("grave_reaper", EnemyArt.GraveReaper(), EnemyArt.FrameSize);
            Character("choir_mistress", EnemyArt.ChoirMistress(), EnemyArt.BossFrameSize);
            Character("void_eye", EnemyArt.VoidEye(), EnemyArt.FrameSize);
            Character("great_crab", EnemyArt.GreatCrab(), EnemyArt.FrameSize);
            Character("great_wolf", EnemyArt.GreatWolf(), EnemyArt.FrameSize);
            Character("executioner", EnemyArt.Executioner(), EnemyArt.FrameSize);
            Character("coffin", GimmickArt.Coffin(), EnemyArt.FrameSize);
            Character("chain_post", GimmickArt.ChainPost(), EnemyArt.FrameSize);
            Character("bone_digger", EnemyArt.BoneDigger(), EnemyArt.FrameSize);
            Character("bone_pile", GimmickArt.BonePile(), EnemyArt.FrameSize);
            Character("twin_acolyte", EnemyArt.TwinAcolyte(), EnemyArt.FrameSize);
            Character("shade_lord", EnemyArt.ShadeLord(), EnemyArt.FrameSize);
            Character("leech_mother", EnemyArt.LeechMother(), EnemyArt.FrameSize);
            Character("ice_knight", EnemyArt.IceKnight(), EnemyArt.FrameSize);
            Character("bombsmith", EnemyArt.Bombsmith(), EnemyArt.FrameSize);
            Character("sporeling", EnemyArt.Sporeling(), EnemyArt.FrameSize);
            Character("mycelid", EnemyArt.Mycelid(), EnemyArt.FrameSize);
            Character("moss_shaman", EnemyArt.MossShaman(), EnemyArt.FrameSize);
            Character("mycelium_mother", EnemyArt.MyceliumMother(), EnemyArt.BossFrameSize);
            Character("rot_giant", EnemyArt.RotGiant(), EnemyArt.BossFrameSize);
            Character("great_cap", EnemyArt.GreatCap(), EnemyArt.FrameSize);
            Character("glowcap", GimmickArt.Glowcap(), EnemyArt.FrameSize);
            Character("lily", GimmickArt.Lily(), EnemyArt.FrameSize);
            Character("living_tome", EnemyArt.LivingTome(), EnemyArt.FrameSize);
            Character("ink_wraith", EnemyArt.InkWraith(), EnemyArt.FrameSize);
            Character("scribe", EnemyArt.Scribe(), EnemyArt.FrameSize);
            Character("archivist", EnemyArt.Archivist(), EnemyArt.BossFrameSize);
            Character("ink_beast", EnemyArt.InkBeast(), EnemyArt.FrameSize);
            Character("bookworm", EnemyArt.Bookworm(), EnemyArt.FrameSize);
            Character("forbidden_tome", GimmickArt.ForbiddenTome(), EnemyArt.FrameSize);
            Character("candelabra", GimmickArt.Candelabra(), EnemyArt.FrameSize);
            Effect("trap_icicle", GimmickArt.Icicle(), "trap_icicle", new Vector2(0.5f, 0.5f));
            Effect("trap_lava", GimmickArt.LavaCrack(), "trap_lava", new Vector2(0f, 0.5f));

            foreach (var (name, gen) in ProceduralArt.Effects) Effect(name, gen());
            foreach (var (name, sheet) in PropArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            foreach (var (name, sheet) in DoorArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            foreach (var (name, sheet) in DecorArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
            foreach (var (name, sheet) in DecalArt.Sheets()) Effect(name, sheet.Frames, name, sheet.Pivot);
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
            WritePreview("decals", DecalArt.Sheets());
            WritePreview("pings", PingArt.Sheets());
            WritePreview("skills", new Dictionary<string, PropArt.Sheet> { ["icons_skill"] = new() { Frames = skills, Pivot = new Vector2(0.5f, 0.5f) } });
            WritePreview("camp", CampArt.Sheets());

            var tiles = ProceduralArt.Tiles();
            foreach (var (name, canvas) in PropArt.AreaTiles()) tiles[name] = canvas;
            foreach (var (name, canvas) in LandArt.Tiles()) tiles[name] = canvas;
            foreach (var (name, canvas) in CampArt.Tiles()) tiles[name] = canvas;
            foreach (var (name, canvas) in tiles)
            {
                string path = $"{TileDir}/{name}.png";
                PixelCanvas.WriteStrip(path, new[] { canvas });
                // Floors are flat (relief only); the rest also round off at their outline.
                bool flat = name.StartsWith("floor") || name.StartsWith("carpet") || name.StartsWith("camp_ground");
                bool lit = !(name.StartsWith("shade") || name == "mote" || name == "walltop");
                var normal = lit ? NormalMaps.Make(path, tiles: name != "pillar", dome: flat ? 0f : 0.6f, relief: 1f) : null;
                Import(path, name == "pillar" ? new Vector2(0.5f, 0.05f) : new Vector2(0.5f, 0.5f), normal);
            }
            var decor = PropArt.Decor();
            foreach (var (name, canvas) in LandArt.Decor()) decor[name] = canvas;
            foreach (var (name, canvas) in decor)
            {
                string path = $"{TileDir}/{name}.png";
                PixelCanvas.WriteStrip(path, new[] { canvas });
                Import(path, new Vector2(0.5f, 0.02f), NormalMaps.Make(path, tiles: false));
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

        /// <param name="readable">Skins are repainted from it at runtime (SpriteSheet.RecolorOf).</param>
        private static void Character(string name, List<Clip> clips, int frameSize, bool readable = false)
        {
            string path = $"{CharacterDir}/{name}.png";
            PixelRenderer.WriteSheet(path, clips, frameSize);
            PixelRenderer.WritePreview($"{PreviewDir}/{name}.png", clips, frameSize, frameSize > 64 ? 3 : 4);
            var texture = Import(path, null, readable: readable);
            AssetDatabase.DeleteAsset($"{CharacterDir}/{name}_flash.png"); // the white flash is a material now (SpriteFlash)

            var sheet = LoadOrCreateSheet(name);
            sheet.Texture = texture;
            sheet.NormalTexture = NormalMaps.Make(path, tiles: false, dome: 1f, relief: 0.7f);
            sheet.RecolorOf = null;
            sheet.RecolorFrom = sheet.RecolorTo = sheet.FixColors = null;
            sheet.FixPixels = null;
            Layout(sheet, clips, frameSize);
        }

        private static void Layout(SpriteSheet sheet, List<Clip> clips, int frameSize)
        {
            sheet.FrameWidth = frameSize;
            sheet.FrameHeight = frameSize;
            sheet.Pivot = new Vector2(0.5f, (float)GroundY / frameSize);
            sheet.PixelsPerUnit = 32f;
            sheet.Clips.Clear();
            for (int i = 0; i < clips.Count; i++)
                sheet.Clips.Add(new SpriteSheet.ClipInfo { Name = clips[i].Name, Row = i, Frames = clips[i].Frames.Count, Fps = clips[i].Fps, Loop = clips[i].Loop });
            EditorUtility.SetDirty(sheet);
        }

        /// <summary>
        /// A palette swap of <paramref name="baseName"/>: rendered here only to learn which colour becomes which
        /// (plus the few pixels that map gets wrong); the game repaints the base sheet with it at runtime.
        /// </summary>
        private static void Skin(string name, string baseName, List<Clip> clips, int frameSize)
        {
            string temp = $"Temp/skins/{name}.png";
            PixelRenderer.WriteSheet(temp, clips, frameSize);
            PixelRenderer.WritePreview($"{PreviewDir}/{name}.png", clips, frameSize, frameSize > 64 ? 3 : 4);
            var skin = ReadPixels(temp);
            var source = ReadPixels($"{CharacterDir}/{baseName}.png");
            if (skin.Length != source.Length) throw new InvalidOperationException($"skin {name} does not match {baseName}");

            // The most common repaint of each colour; the pixels it gets wrong are listed one by one.
            var votes = new Dictionary<int, Dictionary<int, int>>();
            var colours = new Dictionary<int, Color32>();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].a == 0) continue;
                int from = SpriteSheet.Key(source[i]), to = SpriteSheet.Key(skin[i]);
                colours[from] = source[i];
                colours[to] = skin[i];
                if (!votes.TryGetValue(from, out var v)) votes[from] = v = new Dictionary<int, int>();
                v[to] = v.TryGetValue(to, out int n) ? n + 1 : 1;
            }
            var map = new Dictionary<int, int>();
            var fromList = new List<Color32>();
            var toList = new List<Color32>();
            foreach (var (from, v) in votes)
            {
                int best = from, count = -1;
                foreach (var (to, n) in v)
                    if (n > count)
                    {
                        best = to;
                        count = n;
                    }
                map[from] = best;
                if (best == from) continue;
                fromList.Add(colours[from]);
                toList.Add(colours[best]);
            }
            var fixPixels = new List<int>();
            var fixColors = new List<Color32>();
            for (int i = 0; i < source.Length; i++)
            {
                bool wrong = source[i].a == 0 ? skin[i].a != 0 : map[SpriteSheet.Key(source[i])] != SpriteSheet.Key(skin[i]);
                if (!wrong) continue;
                fixPixels.Add(i);
                fixColors.Add(skin[i]);
            }
            // The same repaint as SpriteSheet.Recolor must give back exactly the rendered skin.
            var check = (Color32[])source.Clone();
            for (int i = 0; i < check.Length; i++)
                if (check[i].a != 0) check[i] = colours[map[SpriteSheet.Key(check[i])]];
            for (int i = 0; i < fixPixels.Count; i++) check[fixPixels[i]] = fixColors[i];
            for (int i = 0; i < check.Length; i++)
                if (SpriteSheet.Key(check[i]) != SpriteSheet.Key(skin[i]) && (check[i].a != 0 || skin[i].a != 0))
                    throw new InvalidOperationException($"skin {name}: the repaint differs at pixel {i}");
            Debug.Log($"[ArtGenerator] skin {name}: {fromList.Count} colours repainted, {fixPixels.Count} pixels fixed");

            AssetDatabase.DeleteAsset($"{CharacterDir}/{name}.png");
            AssetDatabase.DeleteAsset($"{CharacterDir}/{name}{NormalMaps.Suffix}.png");
            var sheet = LoadOrCreateSheet(name);
            sheet.Texture = null;
            sheet.NormalTexture = null;
            sheet.RecolorOf = LoadOrCreateSheet(baseName);
            sheet.RecolorFrom = fromList.ToArray();
            sheet.RecolorTo = toList.ToArray();
            sheet.FixPixels = fixPixels.ToArray();
            sheet.FixColors = fixColors.ToArray();
            Layout(sheet, clips, frameSize);
        }

        private static Color32[] ReadPixels(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            var px = t.GetPixels32();
            UnityEngine.Object.DestroyImmediate(t);
            return px;
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
            // Solid things in the room are shaded by the lights; effects (glows, flames, telegraphs) are not.
            string sheetId = sheetName ?? "fx_" + name;
            bool solid = sheetId.StartsWith("prop_") && sheetId != "prop_campfire" || sheetId.StartsWith("door_") || sheetId.StartsWith("decor_") || sheetId.StartsWith("decal_") || sheetId.StartsWith("camp_");
            sheet.NormalTexture = solid ? NormalMaps.Make(path, tiles: false, dome: sheetId.StartsWith("decal_") ? 0.3f : 1f, relief: 0.8f) : null;
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
        private static Texture2D Import(string path, Vector2? spritePivot, Texture2D normal = null, bool readable = false)
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
            importer.isReadable = readable;
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
            importer.secondarySpriteTextures = normal != null ? new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = normal } } : System.Array.Empty<SecondarySpriteTexture>();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
