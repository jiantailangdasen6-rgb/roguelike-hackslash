using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// A generated pixel-art sheet: one row per clip (row 0 at the top), frames left to right.
    /// Sprites are sliced lazily at runtime. Sheets live in Resources/Sheets/.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Sprite Sheet")]
    public sealed class SpriteSheet : ScriptableObject
    {
        [Serializable]
        public struct ClipInfo
        {
            public string Name;
            public int Row;
            public int Frames;
            public float Fps;
            public bool Loop;
        }

        public Texture2D Texture;
        /// <summary>Lighting relief made from the art (NormalMaps): candles and torches shade the sprite with it.</summary>
        public Texture2D NormalTexture;
        public int FrameWidth;
        public int FrameHeight;
        /// <summary>Normalized pivot inside a frame (feet for characters, centre for effects).</summary>
        public Vector2 Pivot = new(0.5f, 0.5f);
        public float PixelsPerUnit = 32f;
        public List<ClipInfo> Clips = new();

        /// <summary>Skins: made at runtime from another sheet by repainting its colours (the build carries only the base sheet).</summary>
        public SpriteSheet RecolorOf;
        public Color32[] RecolorFrom, RecolorTo;
        /// <summary>The few pixels the colour map gets wrong (a colour the skin repaints in one place but not another).</summary>
        public int[] FixPixels;
        public Color32[] FixColors;

        [NonSerialized] private Texture2D _recolored;
        private Texture2D Tex => Texture != null ? Texture : RecolorOf != null ? (_recolored ??= Recolor()) : null;
        private Texture2D Normal => NormalTexture != null ? NormalTexture : RecolorOf != null ? RecolorOf.NormalTexture : null;

        private Texture2D Recolor()
        {
            var source = RecolorOf.Texture;
            var px = source.GetPixels32();
            var map = new Dictionary<int, Color32>(RecolorFrom.Length);
            for (int i = 0; i < RecolorFrom.Length; i++) map[Key(RecolorFrom[i])] = RecolorTo[i];
            for (int i = 0; i < px.Length; i++)
                if (px[i].a != 0 && map.TryGetValue(Key(px[i]), out var c)) px[i] = c;
            if (FixPixels != null)
                for (int i = 0; i < FixPixels.Length; i++) px[FixPixels[i]] = FixColors[i];
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(px);
            texture.Apply(false, true);
            return texture;
        }

        public static int Key(Color32 c) => c.r << 24 | c.g << 16 | c.b << 8 | c.a;

        [NonSerialized] private Dictionary<string, Sprite[]> _sprites;
        private static readonly Dictionary<string, SpriteSheet> Loaded = new();

        /// <param name="quiet">Don't warn when it is missing (optional variants).</param>
        public static SpriteSheet Load(string name, bool quiet = false)
        {
            if (Loaded.TryGetValue(name, out var sheet) && sheet != null) return sheet;
            sheet = Resources.Load<SpriteSheet>("Sheets/" + name);
            if (sheet == null && !quiet) Debug.LogWarning($"[SpriteSheet] Missing Resources/Sheets/{name}");
            Loaded[name] = sheet;
            return sheet;
        }

        /// <summary>Loads every sheet at once (a slow device would otherwise stall the first time each one is used).</summary>
        public static void PreloadAll()
        {
            if (_preloaded) return;
            _preloaded = true;
            foreach (var sheet in Resources.LoadAll<SpriteSheet>("Sheets")) Loaded[sheet.name] = sheet;
        }
        private static bool _preloaded;

        public bool TryGetClip(string name, out ClipInfo clip)
        {
            foreach (var c in Clips)
                if (c.Name == name)
                {
                    clip = c;
                    return true;
                }
            clip = default;
            return false;
        }

        public Sprite[] Sprites(string clipName)
        {
            var cache = _sprites ??= new Dictionary<string, Sprite[]>();
            if (cache.TryGetValue(clipName, out var sprites)) return sprites;
            if (!TryGetClip(clipName, out var clip)) return null;

            var texture = Tex;
            var normal = Normal;
            sprites = new Sprite[clip.Frames];
            int rows = texture.height / FrameHeight;
            for (int i = 0; i < clip.Frames; i++)
            {
                var rect = new Rect(i * FrameWidth, (rows - 1 - clip.Row) * FrameHeight, FrameWidth, FrameHeight);
                sprites[i] = normal == null
                    ? Sprite.Create(texture, rect, Pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect)
                    : Sprite.Create(texture, rect, Pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false,
                        new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = normal } });
                sprites[i].name = $"{name}_{clipName}_{i}";
            }
            cache[clipName] = sprites;
            return sprites;
        }

        /// <summary>First frame of the first clip (static props/effects).</summary>
        public Sprite First => Clips.Count > 0 ? Sprites(Clips[0].Name)[0] : null;
    }
}
