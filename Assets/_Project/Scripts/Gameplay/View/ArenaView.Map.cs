using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Game.Gameplay
{
    /// <summary>
    /// The labyrinth in one piece (phase 5): every room of the area is drawn at once - each a copy of the land's look
    /// moved to its spot, its walls opened where a corridor leaves - and the corridors between them are laid in tiles
    /// with candles on their walls. Shut ways (the boss door, a locked treasure room, the lord's hall while he fights)
    /// show bone gates at both ends; a secret room is not drawn until its cracked wall is broken. Lights far from the
    /// camera are switched off, so a map of a dozen rooms costs about as much as one.
    /// </summary>
    public sealed partial class ArenaView
    {
        private readonly List<GameObject> _roomCopies = new();
        private readonly List<Light2D> _lights = new();
        private float _cullAt;

        // Room cells (the land's look is drawn around a room at the origin): floor x -14..13, y -8..7; the back wall
        // y 8..9 and its black top y 10; the bottom top y -9; the side tops x -15 and 14.
        private const int Gap = 2; // a doorway is four tiles: -2..1

        private static readonly string[] CandleOf =
        {
            "fx_candle", "fx_candle_red", "fx_candle_soul", "fx_candle_teal", "fx_candle_ice", "fx_candle_ember", "fx_candle_red", "fx_candle_spore", "fx_candle_violet",
        };

        /// <summary>Each land's floor litter (the sheets PrototypeBuilder scatters along its walls).</summary>
        private static readonly string[] ScatterOf = { "bones", "bones", "bones_2", "urns_3", "snow_4", "slag_5", "bones_2", "shrooms_7", "papers_8" };

        private void BuildMap(DungeonMap map, RunDirector director)
        {
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();
            _gimmicks.Clear();
            _doors.Clear();
            foreach (var go in _roomCopies) Destroy(go);
            _roomCopies.Clear();
            if (areaRoots == null || _area < 0 || _area >= areaRoots.Length || areaRoots[_area] == null) return;
            var template = areaRoots[_area];
            template.gameObject.SetActive(false);

            bool Hidden(MapRoom r) => r.Kind == RoomKind.Secret && !director.IsUnlocked(r.Id);
            foreach (var room in map.Rooms)
            {
                if (Hidden(room)) continue;
                var copy = Instantiate(template, transform);
                copy.name = $"Room{room.Id}";
                copy.localPosition = room.Origin;
                copy.gameObject.SetActive(true);
                _roomCopies.Add(copy.gameObject);
                for (int d = 0; d < 4; d++)
                {
                    var next = map[room.Neighbor[d]];
                    if (next != null && !Hidden(next)) OpenWall(copy, d);
                }
            }

            BuildCorridors(map, template, Hidden);
            foreach (var room in map.Rooms)
                if (!Hidden(room)) BuildRoomContents(room);

            // Rooms nobody has walked into yet lie in darkness (it lifts as the team comes in).
            _shrouds.Clear();
            foreach (var room in map.Rooms)
            {
                if (Hidden(room)) continue;
                var b = room.Bounds;
                var veil = Sprite("Unexplored", UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f),
                    b.center + new Vector2(0f, 1f), 960);
                veil.transform.localScale = new Vector3(b.width + 2f, b.height + 4f, 1f);
                veil.color = new Color(0.01f, 0.005f, 0.01f, director.IsVisited(room.Id) ? 0f : ShroudAlpha);
                _shrouds[room.Id] = veil;
            }

            _lights.Clear();
            GetComponentsInChildren(true, _lights);
            _cullAt = 0f;
        }

        private const float ShroudAlpha = 0.82f;
        private readonly Dictionary<int, SpriteRenderer> _shrouds = new();

        /// <summary>The darkness over a room fades once someone has been in.</summary>
        private void UpdateShrouds(RunDirector director)
        {
            foreach (var (id, veil) in _shrouds)
            {
                if (veil == null) continue;
                var c = veil.color;
                float target = director.IsVisited(id) ? 0f : ShroudAlpha;
                if (Mathf.Approximately(c.a, target)) continue;
                c.a = Mathf.MoveTowards(c.a, target, Time.deltaTime * 1.5f);
                veil.color = c;
                veil.enabled = c.a > 0.001f;
            }
        }

        /// <summary>Takes the wall out where a corridor leaves the room (and the decorations standing in the way).</summary>
        private static void OpenWall(Transform copy, int dir)
        {
            Tilemap Map(string name) => copy.Find(name)?.GetComponent<Tilemap>();
            var floor = Map("Floor");
            var walls = Map("Walls");
            var tops = Map("WallTops");
            if (floor == null) return;
            var ground = floor.GetTile(new Vector3Int(0, 0, 0));
            void Open(int x, int y)
            {
                var cell = new Vector3Int(x, y, 0);
                walls?.SetTile(cell, null);
                tops?.SetTile(cell, null);
                floor.SetTile(cell, ground);
            }
            for (int k = -Gap; k < Gap; k++)
                switch (dir)
                {
                    case Dir.N: for (int y = 8; y <= 10; y++) Open(k, y); break;
                    case Dir.S: Open(k, -9); break;
                    case Dir.E: Open(14, k); break;
                    default: Open(-15, k); break;
                }

            var decor = copy.Find("Decor");
            if (decor == null) return;
            for (int i = decor.childCount - 1; i >= 0; i--)
            {
                var child = decor.GetChild(i);
                Vector2 at = child.localPosition;
                bool inTheWay = dir switch
                {
                    Dir.N => Mathf.Abs(at.x) < Gap + 1f && at.y > 6f,
                    Dir.S => Mathf.Abs(at.x) < Gap + 1f && at.y < -6.5f,
                    Dir.E => at.x > 12f && Mathf.Abs(at.y) < Gap + 0.8f,
                    _ => at.x < -12f && Mathf.Abs(at.y) < Gap + 0.8f,
                };
                if (inTheWay) Destroy(child.gameObject);
            }
        }

        /// <summary>The corridors in tiles: floor between the rooms, wall faces and black tops along their sides.</summary>
        private void BuildCorridors(DungeonMap map, Transform template, System.Func<MapRoom, bool> hidden)
        {
            Tilemap Source(string name) => template.Find(name)?.GetComponent<Tilemap>();
            var sourceFloor = Source("Floor");
            if (sourceFloor == null) return;
            var grounds = new List<TileBase>();
            for (int x = -6; x < 6; x++)
            {
                var t = sourceFloor.GetTile(new Vector3Int(x, 0, 0));
                if (t != null && !grounds.Contains(t)) grounds.Add(t);
            }
            var wall = Source("Walls")?.GetTile(new Vector3Int(0, 8, 0));
            var top = Source("WallTops")?.GetTile(new Vector3Int(0, 10, 0));

            var root = new GameObject("Corridors");
            root.transform.SetParent(transform, false);
            _spawned.Add(root);
            Tilemap Layer(string name, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var tm = go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>().sortingOrder = order;
                return tm;
            }
            var floor = Layer("Floor", -1000);
            var walls = Layer("Walls", -990);
            var tops = Layer("Tops", 900);
            var rng = new GameRandom(17ul + (ulong)map.Area * 31ul);
            TileBase Ground() => grounds.Count > 0 ? grounds[rng.Range(0, grounds.Count)] : null;
            string candle = CandleOf[Mathf.Clamp(_area, 0, CandleOf.Length - 1)];
            var torch = LandLook.Torch((Biome)Mathf.Max(0, _area));

            // The land's litter is one of the look's own decorations (environment sprites, not sheets): borrow its picture.
            string scatter = ScatterOf[Mathf.Clamp(_area, 0, ScatterOf.Length - 1)];
            Sprite litterSprite = null;
            var decorRoot = template.Find("Decor");
            if (decorRoot != null)
                foreach (Transform child in decorRoot)
                    if (child.name == scatter && child.TryGetComponent<SpriteRenderer>(out var sr)) { litterSprite = sr.sprite; break; }
            foreach (var c in map.Corridors)
            {
                var a = map[c.A];
                var b = map[c.B];
                if (hidden(a) || hidden(b)) continue;
                int ox = Mathf.RoundToInt(a.Origin.x), oy = Mathf.RoundToInt(a.Origin.y);
                // A little of the land's litter along the way (bones, urns, snow, slag, toadstools, pages).
                for (int k = 0, n = litterSprite != null ? rng.Range(1, 3) : 0; k < n; k++)
                {
                    float along = rng.Range(0.2f, 0.8f), side = rng.Chance(0.5f) ? -1f : 1f;
                    Vector2 at = c.Vertical
                        ? new Vector2(ox + side * (Gap - 0.7f), Mathf.Lerp(c.Rect.yMin, c.Rect.yMax, along))
                        : new Vector2(Mathf.Lerp(c.Rect.xMin, c.Rect.xMax, along), oy + side * (Gap - 0.8f));
                    var litter = Sprite("Litter", litterSprite, at, -950);
                    litter.flipX = rng.Chance(0.5f);
                }
                if (c.Vertical)
                {
                    int y0 = oy + 11, y1 = Mathf.RoundToInt(b.Origin.y) - 10;
                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = ox - Gap; x < ox + Gap; x++) floor.SetTile(new Vector3Int(x, y, 0), Ground());
                        tops.SetTile(new Vector3Int(ox - Gap - 1, y, 0), top);
                        tops.SetTile(new Vector3Int(ox + Gap, y, 0), top);
                    }
                    float mid = (y0 + y1 + 1) * 0.5f;
                    CorridorCandle(new Vector2(ox - Gap - 0.5f, mid), candle, torch, 905);
                    CorridorCandle(new Vector2(ox + Gap + 0.5f, mid), candle, torch, 905);
                    AddGate(c, new Vector2(ox, a.Bounds.yMax), "gate_n", -985);
                    AddGate(c, new Vector2(ox, b.Bounds.yMin - 0.5f), "gate_h", 906);
                }
                else
                {
                    int x0 = ox + 15, x1 = Mathf.RoundToInt(b.Origin.x) - 16;
                    for (int x = x0; x <= x1; x++)
                    {
                        for (int y = oy - Gap; y < oy + Gap; y++) floor.SetTile(new Vector3Int(x, y, 0), Ground());
                        walls.SetTile(new Vector3Int(x, oy + Gap, 0), wall);
                        walls.SetTile(new Vector3Int(x, oy + Gap + 1, 0), wall);
                        tops.SetTile(new Vector3Int(x, oy + Gap + 2, 0), top);
                        tops.SetTile(new Vector3Int(x, oy - Gap - 1, 0), top);
                    }
                    float mid = (x0 + x1 + 1) * 0.5f;
                    CorridorCandle(new Vector2(mid - 3f, oy + Gap + 0.4f), candle, torch, -980);
                    CorridorCandle(new Vector2(mid + 3f, oy + Gap + 0.4f), candle, torch, -980);
                    AddGate(c, new Vector2(a.Bounds.xMax + 0.5f, oy), "gate_v", 906);
                    AddGate(c, new Vector2(b.Bounds.xMin - 0.5f, oy), "gate_v", 906);
                }
            }
        }

        private void CorridorCandle(Vector2 at, string sheet, Color color, int order)
        {
            var r = Sprite("CorridorCandle", SpriteSheet.Load(sheet, quiet: true)?.First, at, order);
            r.gameObject.AddComponent<SpriteLoop>().Configure(sheet, 9f);
            var l = AddLight(r.gameObject, color, 0.65f, 3.2f);
            l.gameObject.AddComponent<LightFlicker>().Configure(0.65f, 0.15f);
        }

        /// <summary>Bone bars across a corridor's end, shown while the way is shut.</summary>
        private void AddGate(Corridor c, Vector2 at, string sheet, int order)
        {
            var bars = Sprite("Gate", SpriteSheet.Load(sheet, quiet: true)?.First, at, order);
            bars.enabled = false;
            _doors.Add(new DoorVisual { Corridor = c, Bars = bars });
        }

        /// <summary>Lights far from the camera are off (a dozen rooms of candles would otherwise all be lit).</summary>
        private void CullLights()
        {
            if (Time.time < _cullAt) return;
            _cullAt = Time.time + 0.25f;
            var cam = Camera.main;
            if (cam == null) return;
            Vector2 c = cam.transform.position;
            float reach = cam.orthographicSize * Mathf.Max(1f, cam.aspect) + 6f;
            for (int i = _lights.Count - 1; i >= 0; i--)
            {
                var l = _lights[i];
                if (l == null)
                {
                    _lights.RemoveAt(i);
                    continue;
                }
                float r = reach + l.pointLightOuterRadius;
                bool near = ((Vector2)l.transform.position - c).sqrMagnitude < r * r;
                if (l.enabled != near) l.enabled = near;
            }
        }
    }
}
