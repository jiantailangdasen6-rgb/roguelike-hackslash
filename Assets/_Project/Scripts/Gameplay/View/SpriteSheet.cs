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
        /// <summary>Same layout, every opaque pixel white: used for hit flashes.</summary>
        public Texture2D FlashTexture;
        public int FrameWidth;
        public int FrameHeight;
        /// <summary>Normalized pivot inside a frame (feet for characters, centre for effects).</summary>
        public Vector2 Pivot = new(0.5f, 0.5f);
        public float PixelsPerUnit = 32f;
        public List<ClipInfo> Clips = new();

        [NonSerialized] private Dictionary<string, Sprite[]> _sprites;
        [NonSerialized] private Dictionary<string, Sprite[]> _flashSprites;
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

        public Sprite[] Sprites(string clipName, bool flash = false)
        {
            var texture = flash && FlashTexture != null ? FlashTexture : Texture;
            var cache = flash && FlashTexture != null ? (_flashSprites ??= new Dictionary<string, Sprite[]>()) : (_sprites ??= new Dictionary<string, Sprite[]>());
            if (cache.TryGetValue(clipName, out var sprites)) return sprites;
            if (!TryGetClip(clipName, out var clip)) return null;

            sprites = new Sprite[clip.Frames];
            int rows = texture.height / FrameHeight;
            for (int i = 0; i < clip.Frames; i++)
            {
                var rect = new Rect(i * FrameWidth, (rows - 1 - clip.Row) * FrameHeight, FrameWidth, FrameHeight);
                sprites[i] = Sprite.Create(texture, rect, Pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = $"{name}_{clipName}_{i}";
            }
            cache[clipName] = sprites;
            return sprites;
        }

        /// <summary>First frame of the first clip (static props/effects).</summary>
        public Sprite First => Clips.Count > 0 ? Sprites(Clips[0].Name)[0] : null;
    }
}
