using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// The room around the players: switches the area's look (tiles, candles, light colour) and places the
    /// current layout's obstacles and gimmicks (spike traps, flame vents, healing fonts). Kegs are drawn by
    /// <see cref="EnemyView"/> since they are breakable "enemies" in the simulation.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        /// <summary>Ambient light per area: crypt (warm grey), chapel (blood red), abyss (cold violet).</summary>
        private static readonly (Color Color, float Intensity)[] AreaLight =
        {
            (new Color(0.9f, 0.82f, 0.8f), 0.8f),
            (new Color(0.95f, 0.72f, 0.7f), 0.72f),
            (new Color(0.72f, 0.7f, 0.95f), 0.7f),
        };

        private sealed class GimmickVisual
        {
            public GimmickDef Def;
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public Light2D Light;
            public float ActiveUntil;
            public float WarnUntil;
            public bool Used;
        }

        [SerializeField] private Transform[] areaRoots;
        [SerializeField] private Light2D globalLight;

        private readonly List<GameObject> _spawned = new();
        private readonly Dictionary<int, GimmickVisual> _gimmicks = new();
        private int _area = -1;
        private int _roomId = -1;
        private (ulong Seed, int Area, int Unlocked) _mapKey;
        private MapRoom _room;

        private sealed class DoorVisual
        {
            public MapRoom To;
            public SpriteRenderer Bars;
        }

        private readonly List<DoorVisual> _doors = new();

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        private void Start() => ShowArea(0);

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.HazardWarn when _gimmicks.TryGetValue(e.Id, out var g):
                    g.WarnUntil = Time.time + e.Value;
                    break;
                case SimEventType.HazardFire when _gimmicks.TryGetValue(e.Id, out var g):
                    if (g.Def.Kind == GimmickKind.HealingFont) g.Used = true;
                    else g.ActiveUntil = Time.time + e.Value;
                    break;
            }
        }

        private void Update()
        {
            var director = RunDirector.Instance;
            if (director != null && director.IsSpawned)
            {
                // Draw the room the local player stands in (another one after a door or a new area).
                ShowArea(director.Area.Value);
                var room = director.LocalRoom;
                var key = (director.MapSeed.Value, director.Area.Value, director.UnlockedRooms.Value);
                if (room.Id != _roomId || key != _mapKey)
                {
                    _mapKey = key;
                    BuildRoom(room);
                }
                foreach (var d in _doors)
                    if (d.Bars != null) d.Bars.enabled = !director.DoorOpen(_room, d.To);
            }

            foreach (var g in _gimmicks.Values) Animate(g);
            UpdateAbyss(director);
        }

        // ------------------------------------------------------------------ The Lich's collapsing floor

        private const int AbyssPixelsPerUnit = 16;
        private SpriteRenderer _abyss;
        private SpriteRenderer _abyssWarn;
        private int _abyssStage;
        private int _warnStage;

        private void UpdateAbyss(RunDirector director)
        {
            // Only the boss room falls; it is drawn when you stand in it.
            bool inBossRoom = _room != null && _room.Kind == RoomKind.Boss;
            int stage = director != null && director.IsSpawned && inBossRoom ? director.CollapseStage.Value : 0;
            if (stage != _abyssStage)
            {
                _abyssStage = stage;
                SetAbyss(ref _abyss, stage > 0 ? AbyssTexture(stage, warn: false) : null, -940);
            }

            // The next band to fall pulses red while the warning lasts.
            int warn = director != null && director.IsSpawned && inBossRoom && director.Trial.Value == BossTrial.Collapse ? stage + 1 : 0;
            if (warn != _warnStage)
            {
                _warnStage = warn;
                SetAbyss(ref _abyssWarn, warn > 0 ? AbyssTexture(warn, warn: true) : null, -939);
            }
            if (_abyssWarn != null)
                _abyssWarn.color = new Color(1f, 1f, 1f, 0.45f + 0.4f * Mathf.Sin(Time.time * 12f));
        }

        private void SetAbyss(ref SpriteRenderer renderer, Texture2D texture, int order)
        {
            if (renderer != null)
            {
                Destroy(renderer.sprite.texture);
                Destroy(renderer.gameObject);
                renderer = null;
            }
            if (texture == null) return;
            var go = new GameObject("Abyss");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = _room != null ? _room.Bounds.min : RunSimulation.DefaultBounds.min;
            renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, AbyssPixelsPerUnit);
            renderer.sortingOrder = order;
        }

        /// <summary>
        /// Pixel-art void over the fallen floor: a crumbled stone rim, then darkness with faint drifting motes.
        /// The warning version is a red band (only the strip about to fall).
        /// </summary>
        private static Texture2D AbyssTexture(int stage, bool warn)
        {
            var bounds = RunSimulation.DefaultBounds;
            var safe = RunSimulation.SafeArea(bounds, stage);
            var before = RunSimulation.SafeArea(bounds, stage - 1);
            int w = Mathf.RoundToInt(bounds.width * AbyssPixelsPerUnit), h = Mathf.RoundToInt(bounds.height * AbyssPixelsPerUnit);
            var pixels = new Color32[w * h];
            var rim = new Color32(70, 60, 84, 255);
            var rimLight = new Color32(112, 100, 128, 255);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bounds.min + new Vector2((x + 0.5f) / AbyssPixelsPerUnit, (y + 0.5f) / AbyssPixelsPerUnit);
                // Chunky jitter (4-pixel cells) so the edge looks broken, not ruled.
                float jitter = (Hash(x / 4, y / 4, stage) - 0.5f) * 0.5f;
                float outside = Outside(safe, p) + jitter;
                if (outside <= 0f) continue;
                if (warn)
                {
                    if (Outside(before, p) + jitter > 0f) continue; // already fallen
                    pixels[y * w + x] = outside < 0.15f ? new Color32(255, 120, 90, 200) : new Color32(200, 30, 30, 110);
                    continue;
                }
                Color32 c;
                if (outside < 0.12f) c = Hash(x, y, 7) < 0.5f ? rimLight : rim;
                else if (outside < 0.24f) c = new Color32(18, 12, 26, 255); // the cliff's shadow
                else
                {
                    // A violet glow rising from below the broken edge, fading into black.
                    float deep = Mathf.Clamp01((outside - 0.24f) / 1.4f);
                    float glow = (1f - deep) * (1f - deep);
                    // Banded (pixel-art) rather than smooth.
                    glow = Mathf.Floor(glow * 5f) / 5f;
                    c = new Color32((byte)(4 + 70 * glow), (byte)(2 + 22 * glow), (byte)(8 + 120 * glow), 255);
                    float mote = Hash(x, y, 13);
                    if (mote > 0.994f) c = new Color32(120, 90, 200, 255);
                    else if (mote > 0.985f) c = new Color32(48, 36, 84, 255);
                }
                pixels[y * w + x] = c;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static float Outside(Rect r, Vector2 p) =>
            Mathf.Max(Mathf.Max(r.xMin - p.x, p.x - r.xMax), Mathf.Max(r.yMin - p.y, p.y - r.yMax));

        private static float Hash(int x, int y, int salt)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * 2246822519);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        private void ShowArea(int area)
        {
            area = Mathf.Clamp(area, 0, 2);
            if (area == _area) return;
            _area = area;
            if (areaRoots != null)
                for (int i = 0; i < areaRoots.Length; i++)
                    if (areaRoots[i] != null) areaRoots[i].gameObject.SetActive(i == area);
            if (globalLight != null)
            {
                globalLight.color = AreaLight[area].Color;
                globalLight.intensity = AreaLight[area].Intensity;
            }
            if (_room != null) BuildRoom(_room); // props use the area's palette
        }

        /// <summary>The room's floor and walls (the area look moved to its spot), obstacles, traps and doorways.</summary>
        private void BuildRoom(MapRoom room)
        {
            _room = room;
            _roomId = room.Id;
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();
            _gimmicks.Clear();
            _doors.Clear();
            if (areaRoots != null)
                foreach (var root in areaRoots)
                    if (root != null) root.localPosition = room.Origin;
            BuildDoors(room);
            AddRoomDecor(room);

            var layout = room.Layout;
            foreach (var o in layout.Obstacles)
            {
                string kind = o.Kind.ToString().ToLowerInvariant();
                var sheet = SpriteSheet.Load($"prop_{kind}_{Mathf.Max(0, _area)}", quiet: true) ?? SpriteSheet.Load($"prop_{kind}");
                var r = Sprite("Obstacle", sheet?.First, o.Position, EnemyView.SortOrder(o.Position.y));
                if (o.Kind == ObstacleKind.Campfire)
                {
                    // The rest room's fire: flickering flames over a ring of stones, and a big warm light.
                    r.gameObject.AddComponent<SpriteLoop>().Configure("prop_campfire", 10f);
                    Sprite("Stones", SpriteSheet.Load("camp_stones")?.First, o.Position + new Vector2(0f, 0.1f), -900);
                    var light = AddLight(r.gameObject, new Color(1f, 0.6f, 0.3f), 1.5f, 9f);
                    light.transform.localPosition = new Vector3(0f, 0.8f);
                    light.gameObject.AddComponent<LightFlicker>().Configure(1.5f, 0.2f);
                    continue;
                }
                r.gameObject.AddComponent<PropFader>();
            }

            int idBase = room.Id * RunSimulation.HazardIdsPerRoom;
            for (int i = 0; i < layout.Gimmicks.Length; i++)
            {
                var def = layout.Gimmicks[i];
                switch (def.Kind)
                {
                    case GimmickKind.SpikeTrap:
                    {
                        var sheet = SpriteSheet.Load("trap_spikes");
                        var r = Sprite("Spikes", sheet?.First, def.Position, -960);
                        _gimmicks[idBase + i] = new GimmickVisual { Def = def, Renderer = r, Frames = sheet?.Sprites("default") };
                        break;
                    }
                    case GimmickKind.FlameJet:
                    {
                        var sheet = SpriteSheet.Load("trap_vent");
                        var r = Sprite("Vent", sheet?.First, def.Position, EnemyView.SortOrder(def.Position.y) + 5);
                        r.flipX = def.Direction.x < 0f;
                        var light = AddLight(r.gameObject, new Color(1f, 0.45f, 0.15f), 0.3f, 2.2f);
                        _gimmicks[idBase + i] = new GimmickVisual { Def = def, Renderer = r, Frames = sheet?.Sprites("default"), Light = light };
                        break;
                    }
                    case GimmickKind.HealingFont:
                    {
                        var sheet = SpriteSheet.Load("prop_font");
                        var r = Sprite("Font", sheet?.First, def.Position, EnemyView.SortOrder(def.Position.y));
                        var light = AddLight(r.gameObject, new Color(0.45f, 1f, 0.7f), 1.1f, 3.2f);
                        _gimmicks[idBase + i] = new GimmickVisual { Def = def, Renderer = r, Frames = sheet?.Sprites("default"), Light = light };
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Dressing that tells the special rooms apart at a glance: candles around a pedestal, gold in the treasure room,
        /// webs and bones in the secret room, chains and a red glow in the champion's hall, a rune circle under a puzzle.
        /// </summary>
        private void AddRoomDecor(MapRoom room)
        {
            var b = room.Bounds;
            Vector2 o = room.Origin;
            void Decor(string sheet, Vector2 at, int? order = null, bool flipX = false, bool flipY = false, float scale = 1f)
            {
                var r = Sprite(sheet, SpriteSheet.Load(sheet, quiet: true)?.First, at, order ?? EnemyView.SortOrder(at.y));
                r.flipX = flipX;
                r.flipY = flipY;
                r.transform.localScale = Vector3.one * scale;
            }
            void Candle(Vector2 at, string sheet, Color color)
            {
                var r = Sprite("Candle", SpriteSheet.Load(sheet, quiet: true)?.First, at, EnemyView.SortOrder(at.y));
                r.gameObject.AddComponent<SpriteLoop>().Configure(sheet, 9f);
                var l = AddLight(r.gameObject, color, 0.55f, 1.8f);
                l.gameObject.AddComponent<LightFlicker>().Configure(0.55f, 0.15f);
            }
            void Glow(Vector2 at, Color color, float intensity, float radius)
            {
                var go = new GameObject("RoomGlow");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = at;
                _spawned.Add(go);
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.pointLightOuterRadius = radius;
                l.pointLightInnerRadius = 0.5f;
            }

            if (RoomCatalog.HasPedestal(room.Kind))
            {
                Vector2 pedestal = RunSimulation.PedestalPosition(b);
                string candles = room.Kind is RoomKind.CurseAltar or RoomKind.Secret or RoomKind.Spirit ? "fx_candle_soul" : "fx_candle";
                var color = room.Kind == RoomKind.Spirit ? new Color(0.5f, 0.85f, 1f) : room.Kind is RoomKind.CurseAltar or RoomKind.Secret ? new Color(0.72f, 0.48f, 1f) : new Color(1f, 0.62f, 0.32f);
                for (int i = 0; i < 6; i++)
                {
                    float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                    Candle(pedestal + new Vector2(Mathf.Cos(a) * 3.2f, Mathf.Sin(a) * 2f - 0.3f), candles, color);
                }
            }
            switch (room.Kind)
            {
                case RoomKind.Treasure:
                    foreach (var p in new[] { new Vector2(-4.6f, -2.6f), new Vector2(4.8f, -2.4f), new Vector2(-5f, 2.9f), new Vector2(5.2f, 3.1f), new Vector2(-10.5f, -5.6f), new Vector2(10.5f, -5.6f), new Vector2(-11f, 4.4f), new Vector2(11f, 4.2f) })
                        Decor("decor_gold", o + p);
                    Glow(o + new Vector2(0f, 1f), new Color(1f, 0.82f, 0.45f), 1.1f, 11f);
                    break;
                case RoomKind.Secret:
                    Decor("decor_cobweb", new Vector2(b.xMin - 1f, b.yMax + 1f), 905);
                    Decor("decor_cobweb", new Vector2(b.xMax + 1f, b.yMax + 1f), 905, flipX: true);
                    Decor("decor_cobweb", new Vector2(b.xMin - 1f, b.yMin + 0.5f), 905, flipY: true);
                    Decor("decor_cobweb", new Vector2(b.xMax + 1f, b.yMin + 0.5f), 905, flipX: true, flipY: true);
                    foreach (var p in new[] { new Vector2(-9f, -5f), new Vector2(8.5f, 5f), new Vector2(10f, -6f), new Vector2(-10.5f, 4.5f) })
                        Decor("decor_bones", o + p, -950);
                    Glow(o, new Color(0.6f, 0.45f, 1f), 0.7f, 10f);
                    break;
                case RoomKind.MiniBoss:
                    foreach (float x in new[] { -9.5f, -6f, 6f, 9.5f }) Decor("decor_chains", new Vector2(o.x + x, b.yMax + 2.2f), -985);
                    foreach (var p in new[] { new Vector2(-10f, -5.5f), new Vector2(9.5f, -6f), new Vector2(-11f, 3.5f), new Vector2(11f, 3f) })
                        Decor("decor_bones", o + p, -950);
                    Glow(o + new Vector2(0f, 2.5f), new Color(1f, 0.25f, 0.2f), 0.9f, 9f);
                    break;
                case RoomKind.Campfire:
                {
                    // The gathering bell (RunSimulation.Bell), with a lantern-warm glow on it.
                    Vector2 bell = b.center + RunSimulation.BellSpot;
                    Decor("decor_bell", bell);
                    Glow(bell + new Vector2(0f, 1f), new Color(1f, 0.75f, 0.4f), 0.55f, 3.2f);
                    break;
                }
                case RoomKind.Puzzle:
                    Decor("decor_rune", o + new Vector2(0f, 0.3f), -955, scale: 1.6f);
                    Glow(o, new Color(0.5f, 0.9f, 1f), 0.6f, 8f);
                    break;
                case RoomKind.Spirit:
                {
                    // The wandering spirit floats behind its pedestal, pale and see-through, in a cold light.
                    Vector2 at = RunSimulation.PedestalPosition(b) + new Vector2(0f, 2.2f);
                    var spirit = Sprite("Spirit", SpriteSheet.Load("wraith", quiet: true)?.First, at, EnemyView.SortOrder(at.y));
                    spirit.color = new Color(0.65f, 0.95f, 1f, 0.7f);
                    spirit.gameObject.AddComponent<SpriteLoop>().Configure("wraith", 6f);
                    spirit.gameObject.AddComponent<Bobber>();
                    Glow(at + new Vector2(0f, 0.6f), new Color(0.5f, 0.85f, 1f), 1.2f, 6f);
                    Decor("decor_rune", o + new Vector2(0f, 0.3f), -955, scale: 1.4f);
                    break;
                }
            }
        }

        /// <summary>Torch colour of the doorways per area.</summary>
        private static readonly Color[] DoorLight = { new(1f, 0.62f, 0.32f), new(1f, 0.35f, 0.28f), new(0.72f, 0.48f, 1f) };

        /// <summary>An arch or a gap in the wall for every door of the room, and the bone bars of a shut one.</summary>
        private void BuildDoors(MapRoom room)
        {
            var director = RunDirector.Instance;
            int area = Mathf.Max(0, _area);
            for (int d = 0; d < 4; d++)
            {
                if (!room.HasDoor(d)) continue;
                // A secret room's door is just wall until the cracked wall in front of it is broken.
                var behind = director != null ? director.Map[room.Neighbor[d]] : null;
                if (behind != null && behind.Kind == RoomKind.Secret && !director.IsUnlocked(behind.Id)) continue;
                Vector2 at = DungeonMap.DoorPosition(room, d);
                string kind = d == Dir.N ? "n" : d == Dir.S ? "s" : "side";
                // The arch stands in the back wall above the floor; the others sit in the black wall tops
                // (the side sheet is drawn for the east and mirrored for the west, around its right-edge pivot).
                Vector2 spot = d switch
                {
                    Dir.E => at + new Vector2(1.25f, 0f),
                    Dir.W => at - new Vector2(1.25f, 0f),
                    _ => at,
                };
                var r = Sprite("Door", SpriteSheet.Load($"door_{kind}_{area}")?.First, spot, d == Dir.N ? -985 : 905);
                r.flipX = d == Dir.W;
                var light = AddLight(r.gameObject, DoorLight[area], 0.9f, 3.2f);
                light.transform.position = at;
                var bars = Sprite("Bars", SpriteSheet.Load($"door_bars_{kind}")?.First, r.transform.localPosition, r.sortingOrder + 1);
                bars.flipX = d == Dir.W;
                var to = director != null ? director.Map[room.Neighbor[d]] : null;
                bars.enabled = director != null && !director.DoorOpen(room, to);
                _doors.Add(new DoorVisual { To = to, Bars = bars });
            }
        }

        private void Animate(GimmickVisual g)
        {
            if (g.Renderer == null || g.Frames == null || g.Frames.Length == 0) return;
            float now = Time.time;
            switch (g.Def.Kind)
            {
                case GimmickKind.SpikeTrap:
                {
                    // 0 flush, 1 rattling (warning), 2-3 spikes up
                    int frame = now < g.ActiveUntil ? (now < g.ActiveUntil - 0.35f ? 3 : 2) : now < g.WarnUntil ? 1 : 0;
                    g.Renderer.sprite = g.Frames[Mathf.Min(frame, g.Frames.Length - 1)];
                    g.Renderer.transform.localPosition = (Vector3)g.Def.Position + (now < g.WarnUntil && now >= g.ActiveUntil ? new Vector3(Mathf.Sin(now * 70f) * 0.03f, 0f) : Vector3.zero);
                    break;
                }
                case GimmickKind.FlameJet:
                {
                    bool active = now < g.ActiveUntil, warn = now < g.WarnUntil;
                    int frame = active ? 2 + Mathf.FloorToInt(now * 14f) % 2 : warn ? 1 : 0;
                    g.Renderer.sprite = g.Frames[Mathf.Min(frame, g.Frames.Length - 1)];
                    g.Light.intensity = active ? 1.6f + Mathf.Sin(now * 30f) * 0.3f : warn ? 0.6f + 0.4f * Mathf.Sin(now * 25f) : 0.25f;
                    break;
                }
                case GimmickKind.HealingFont:
                {
                    int frame = g.Used ? g.Frames.Length - 1 : Mathf.FloorToInt(now * 5f) % Mathf.Max(1, g.Frames.Length - 1);
                    g.Renderer.sprite = g.Frames[frame];
                    g.Light.intensity = g.Used ? 0f : 0.9f + 0.25f * Mathf.Sin(now * 2.5f);
                    break;
                }
            }
        }

        private SpriteRenderer Sprite(string name, Sprite sprite, Vector2 at, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            _spawned.Add(go);
            return r;
        }

        private static Light2D AddLight(GameObject parent, Color color, float intensity, float radius)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.5f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = 0.2f;
            return light;
        }
    }
}
