using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// The new lands' room features: shallow water and ice sheets (pixel textures made to the zone's size, shimmering),
    /// icicles that crack and fall, lava cracks that glow and erupt, the Drowned Saint's flood over her hall and the
    /// water of the Forge Colossus' broken cisterns.
    /// </summary>
    public sealed partial class ArenaView
    {
        private const int ZonePpu = 16;
        private static readonly Dictionary<(GimmickKind, int, int), Sprite[]> ZoneCache = new();

        private void AddLandGimmick(GimmickDef def, int id)
        {
            switch (def.Kind)
            {
                case GimmickKind.Water:
                case GimmickKind.Ice:
                {
                    var frames = ZoneFrames(def.Kind, def.Direction);
                    var r = Sprite(def.Kind == GimmickKind.Water ? "Water" : "Ice", frames[0], def.Position - def.Direction * 0.5f, -996);
                    var light = AddLight(r.gameObject, def.Kind == GimmickKind.Water ? new Color(0.35f, 0.9f, 0.8f) : new Color(0.6f, 0.82f, 1f), 0.35f,
                        Mathf.Max(def.Direction.x, def.Direction.y) * 0.55f);
                    light.transform.localPosition = def.Direction * 0.5f;
                    _gimmicks[id] = new GimmickVisual { Def = def, Renderer = r, Frames = frames, Light = light };
                    break;
                }
                case GimmickKind.Icicle:
                {
                    var sheet = SpriteSheet.Load("trap_icicle", quiet: true);
                    var r = Sprite("Icicle", sheet?.First, def.Position, -958);
                    _gimmicks[id] = new GimmickVisual { Def = def, Renderer = r, Frames = sheet?.Sprites("default") };
                    break;
                }
                case GimmickKind.LavaCrack:
                {
                    var sheet = SpriteSheet.Load("trap_lava", quiet: true);
                    var r = Sprite("Lava", sheet?.First, def.Position, -958);
                    r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(def.Direction.y, def.Direction.x) * Mathf.Rad2Deg);
                    var light = AddLight(r.gameObject, new Color(1f, 0.45f, 0.12f), 0.3f, 3f);
                    light.transform.localPosition = new Vector3(Arena.LavaLength * 0.5f, 0f);
                    _gimmicks[id] = new GimmickVisual { Def = def, Renderer = r, Frames = sheet?.Sprites("default"), Light = light };
                    break;
                }
            }
        }

        private static void AnimateLand(GimmickVisual g, float now)
        {
            switch (g.Def.Kind)
            {
                case GimmickKind.Water:
                case GimmickKind.Ice:
                    g.Renderer.sprite = g.Frames[Mathf.FloorToInt(now * (g.Def.Kind == GimmickKind.Water ? 2.2f : 0.8f)) % g.Frames.Length];
                    if (g.Light != null) g.Light.intensity = 0.32f + 0.06f * Mathf.Sin(now * 1.7f + g.Def.Position.x);
                    break;
                case GimmickKind.Icicle:
                {
                    // 0 frost ring, 1 cracking (warning), 2 shattered ice (just fell)
                    int frame = now < g.ActiveUntil + 0.5f && g.ActiveUntil > 0f ? 2 : now < g.WarnUntil ? 1 : 0;
                    g.Renderer.sprite = g.Frames[Mathf.Min(frame, g.Frames.Length - 1)];
                    g.Renderer.transform.localPosition = (Vector3)g.Def.Position + (frame == 1 ? new Vector3(Mathf.Sin(now * 60f) * 0.02f, 0f) : Vector3.zero);
                    break;
                }
                case GimmickKind.LavaCrack:
                {
                    bool active = now < g.ActiveUntil, warn = now < g.WarnUntil;
                    int frame = active ? 2 + Mathf.FloorToInt(now * 12f) % 2 : warn ? 1 : 0;
                    g.Renderer.sprite = g.Frames[Mathf.Min(frame, g.Frames.Length - 1)];
                    g.Light.intensity = active ? 1.8f + Mathf.Sin(now * 30f) * 0.3f : warn ? 0.7f + 0.5f * Mathf.Sin(now * 22f) : 0.3f + 0.08f * Mathf.Sin(now * 3f);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Zone textures

        /// <summary>Two frames of a water or ice zone, drawn pixel by pixel to its size (cached per size).</summary>
        private static Sprite[] ZoneFrames(GimmickKind kind, Vector2 size)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(size.x * ZonePpu)), h = Mathf.Max(4, Mathf.RoundToInt(size.y * ZonePpu));
            var key = (kind, w, h);
            if (ZoneCache.TryGetValue(key, out var cached) && cached[0] != null) return cached;
            var frames = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Distance to the edge, roughened so the shore is not ruled.
                    float d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) + (Hash(x / 2, y / 2, 3) - 0.5f) * 3f;
                    if (d < 0f) continue;
                    pixels[y * w + x] = kind == GimmickKind.Water ? WaterPixel(x, y, d, f) : IcePixel(x, y, d, f);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(pixels);
                tex.Apply();
                frames[f] = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, ZonePpu);
            }
            ZoneCache[key] = frames;
            return frames;
        }

        private static Color32 WaterPixel(int x, int y, float d, int frame)
        {
            if (d < 1.5f) return new Color32(70, 132, 116, 200);                  // wet shore
            if (d < 4f) return new Color32(26, 66, 60, 190);                      // shallows
            var c = new Color32(12, 36, 36, 180);                                 // still water
            if ((x + y * 3 + frame * 5) % 31 == 0 && Hash(x, y, frame) > 0.4f) c = new Color32(60, 140, 126, 205); // ripples
            if (Hash(x, y, 9 + frame) > 0.997f) c = new Color32(150, 240, 220, 230); // glints of the drowned light
            return c;
        }

        private static Color32 IcePixel(int x, int y, float d, int frame)
        {
            if (d < 1.5f) return new Color32(200, 226, 248, 225);                 // frosted rim
            var c = new Color32(48, 80, 122, 200);
            int band = (x - y * 2 + 4000 + frame * 6) % 37;
            if (band < 2) c = new Color32(116, 156, 204, 210);                   // streaks of shine
            if (Hash(x / 3, y / 2, 21) > 0.93f) c = new Color32(170, 204, 240, 220); // cracks and frost
            return c;
        }

        // ------------------------------------------------------------------ Trials over the room

        private SpriteRenderer _flood;
        private float _floodAlpha;
        private readonly List<(SpriteRenderer Renderer, float Until)> _quench = new();

        private void OnLandTrial(in SimEvent e)
        {
            switch ((TrialMoment)e.Int)
            {
                case TrialMoment.CisternBroken:
                {
                    var frames = PoolFrames();
                    var go = new GameObject("Quench");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = e.Pos;
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = frames[0];
                    r.sortingOrder = -995;
                    var l = AddLight(go, new Color(0.4f, 0.8f, 1f), 0.8f, 3.5f);
                    l.transform.localPosition = Vector3.zero;
                    _quench.Add((r, Time.time + e.Value));
                    break;
                }
                case TrialMoment.Quenched:
                case TrialMoment.CooledDown:
                    foreach (var (r, _) in _quench) if (r != null) Destroy(r.gameObject);
                    _quench.Clear();
                    break;
            }
        }

        private void UpdateLands(RunDirector director)
        {
            // The flood rises over the saint's hall while her seals hold (seen by whoever stands in it).
            bool flood = director != null && director.IsSpawned && director.Trial.Value == BossTrial.Flood && _room != null && _room.Kind == RoomKind.Boss;
            _floodAlpha = Mathf.MoveTowards(_floodAlpha, flood ? 1f : 0f, Time.deltaTime * 0.8f);
            if (_floodAlpha > 0f && _flood == null && _room != null)
            {
                var b = _room.Bounds;
                var go = new GameObject("Flood");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = b.min;
                _flood = go.AddComponent<SpriteRenderer>();
                _flood.sprite = ZoneFrames(GimmickKind.Water, b.size)[0];
                _flood.sortingOrder = -940;
            }
            if (_flood != null)
            {
                if (_floodAlpha <= 0f)
                {
                    Destroy(_flood.gameObject);
                    _flood = null;
                }
                else
                {
                    var frames = ZoneFrames(GimmickKind.Water, _room != null ? _room.Bounds.size : RunSimulation.DefaultBounds.size);
                    _flood.sprite = frames[Mathf.FloorToInt(Time.time * 2.5f) % frames.Length];
                    _flood.color = new Color(1f, 1f, 1f, 0.75f * _floodAlpha);
                }
            }

            for (int i = _quench.Count - 1; i >= 0; i--)
            {
                var (r, until) = _quench[i];
                if (r == null || Time.time >= until)
                {
                    if (r != null) Destroy(r.gameObject);
                    _quench.RemoveAt(i);
                    continue;
                }
                var frames = PoolFrames();
                r.sprite = frames[Mathf.FloorToInt(Time.time * 3f) % frames.Length];
                r.color = new Color(1f, 1f, 1f, Mathf.Clamp01((until - Time.time) / 1.5f));
            }
        }

        private static Sprite[] _poolFrames;

        /// <summary>A round splash of water left by a broken cistern, centred on its sprite.</summary>
        private static Sprite[] PoolFrames()
        {
            if (_poolFrames != null && _poolFrames[0] != null) return _poolFrames;
            int size = Mathf.RoundToInt(RunSimulation.QuenchRadius * 2f * ZonePpu);
            _poolFrames = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var pixels = new Color32[size * size];
                float c = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = c - Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) + (Hash(x / 2, y / 2, 5 + f) - 0.5f) * 2.5f;
                    if (d < 0f) continue;
                    pixels[y * size + x] = d < 1.5f ? new Color32(210, 236, 255, 220) : WaterPixel(x, y, d, f);
                }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(pixels);
                tex.Apply();
                _poolFrames[f] = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ZonePpu);
            }
            return _poolFrames;
        }
    }
}
