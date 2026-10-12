using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The area map in the HUD's top-right corner: rooms someone has entered (with a mark for their kind) and the
    /// outlines of the rooms next to them, the doors between them, the room you stand in, and a dot per player
    /// (blinking red when they are down, so you know where to run).
    /// </summary>
    public sealed class MinimapView
    {
        private readonly float CellW, CellH, Gap, Pad;
        /// <summary>The big map (M): room names in full and bigger dots.</summary>
        private readonly bool _large;

        private sealed class Cell
        {
            public MapRoom Room;
            public Image Box;
            public Image Frame;
            public Text Mark;
        }

        private readonly RectTransform _root;
        private readonly RectTransform _grid;
        private readonly List<Cell> _cells = new();
        private readonly List<(Image Line, MapRoom A, MapRoom B)> _links = new();
        private readonly List<Image> _dots = new();
        private readonly List<(Image Ring, Image Core)> _pingMarks = new();
        private (ulong Seed, int Area) _key = (0, -1);

        public RectTransform Root => _root;

        public MinimapView(Transform parent, Vector2 offset, bool large = false)
        {
            _large = large;
            CellW = large ? 200f : 40f;
            CellH = large ? 100f : 28f;
            Gap = large ? 34f : 12f;
            Pad = large ? 26f : 10f;
            float w = DungeonMap.Size * (CellW + Gap) - Gap + Pad * 2f, h = DungeonMap.Size * (CellH + Gap) - Gap + Pad * 2f;
            var panel = UIKit.Framed(parent, "Minimap", new Color(0.03f, 0.025f, 0.04f, 0.78f));
            _root = panel.Outer().Place(large ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f), new Vector2(w, h), offset);
            _grid = UIKit.Rect("Grid", panel.transform);
            _grid.Stretch(Pad - 2f);
        }

        private Vector2 CellCenter(Vector2Int cell) =>
            new(cell.x * (CellW + Gap) + CellW * 0.5f, cell.y * (CellH + Gap) + CellH * 0.5f);

        private void Rebuild(DungeonMap map)
        {
            for (int i = _grid.childCount - 1; i >= 0; i--) Object.Destroy(_grid.GetChild(i).gameObject);
            _cells.Clear();
            _links.Clear();
            _dots.Clear();
            _pingMarks.Clear();

            foreach (var room in map.Rooms)
                for (int d = 0; d < 2; d++) // each door once: east and north
                {
                    int dir = d == 0 ? Dir.E : Dir.N;
                    if (!room.HasDoor(dir)) continue;
                    var other = map[room.Neighbor[dir]];
                    var line = UIKit.Panel(_grid, "Door", Color.clear);
                    var a = CellCenter(room.Cell);
                    var b = CellCenter(other.Cell);
                    line.rectTransform.anchorMin = line.rectTransform.anchorMax = Vector2.zero;
                    line.rectTransform.anchoredPosition = (a + b) * 0.5f;
                    float t = _large ? 12f : 6f;
                line.rectTransform.sizeDelta = dir == Dir.E ? new Vector2(Gap + t, t) : new Vector2(t, Gap + t);
                    line.raycastTarget = false;
                    _links.Add((line, room, other));
                }

            foreach (var room in map.Rooms)
            {
                var frame = UIKit.Panel(_grid, "Room", Color.clear);
                frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = Vector2.zero;
                frame.rectTransform.anchoredPosition = CellCenter(room.Cell);
                frame.rectTransform.sizeDelta = new Vector2(CellW, CellH);
                frame.raycastTarget = false;
                var box = UIKit.Panel(frame.transform, "Fill", Color.clear);
                box.rectTransform.Stretch(2f);
                box.raycastTarget = false;
                var mark = UIKit.Label(box.transform, "", _large ? 26 : 18, TextAnchor.MiddleCenter);
                mark.rectTransform.Stretch();
                if (_large) mark.rectTransform.anchorMin = new Vector2(0f, 0.4f); // the name on top, the dots below
                _cells.Add(new Cell { Room = room, Box = box, Frame = frame, Mark = mark });
            }

            for (int i = 0; i < GameSession.MaxPlayers; i++)
            {
                var dot = UIKit.Panel(_grid, "Dot", Color.white);
                dot.sprite = UIKit.DiscSprite;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = Vector2.zero;
                dot.rectTransform.sizeDelta = new Vector2(11f, 11f);
                dot.raycastTarget = false;
                dot.enabled = false;
                _dots.Add(dot);
            }
            for (int i = 0; i < GameSession.MaxPlayers * 4; i++)
            {
                var ring = UIKit.Panel(_grid, "PingRing", Color.clear);
                ring.sprite = UIKit.RingSprite;
                var core = UIKit.Panel(_grid, "PingCore", Color.clear);
                core.sprite = UIKit.DiscSprite;
                foreach (var img in new[] { ring, core })
                {
                    img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                    img.raycastTarget = false;
                    img.enabled = false;
                }
                _pingMarks.Add((ring, core));
            }
        }

        public void Update(RunDirector d)
        {
            var map = d.Map;
            var key = (d.MapSeed.Value, d.Area.Value);
            if (key != _key)
            {
                _key = key;
                Rebuild(map);
            }
            var here = d.LocalRoom;

            bool Known(MapRoom r)
            {
                if (r.Kind == RoomKind.Secret && !d.IsUnlocked(r.Id)) return false; // hidden until the wall breaks
                if (d.IsVisited(r.Id) || d.MapRevealed.Value) return true;
                foreach (int n in r.Neighbor) if (n >= 0 && d.IsVisited(n)) return true;
                return false;
            }

            foreach (var c in _cells)
            {
                bool visited = d.IsVisited(c.Room.Id), known = visited || Known(c.Room);
                bool shown = visited || (known && d.MapRevealed.Value); // the old map names every room
                bool current = c.Room == here;
                bool fighting = d.IsFighting(c.Room.Id);
                var kindColor = RoomCatalog.Color(c.Room.Kind);
                c.Frame.color = !known ? Color.clear
                    : current ? new Color(1f, 0.85f, 0.45f, 1f)
                    : fighting ? new Color(1f, 0.3f, 0.25f, 0.6f + 0.4f * Mathf.Sin(Time.time * 6f))
                    : visited ? new Color(0.55f, 0.48f, 0.4f, 0.9f) : new Color(0.32f, 0.29f, 0.27f, 0.7f);
                c.Box.color = !known ? Color.clear : visited ? new Color(0.13f, 0.11f, 0.12f, 0.95f) : new Color(0.06f, 0.05f, 0.06f, 0.85f);
                bool used = NetPlayer.Local != null && (NetPlayer.Local.UsedPedestals.Value & (1 << c.Room.Id)) != 0;
                bool locked = c.Room.Kind == RoomKind.Treasure && !d.IsUnlocked(c.Room.Id);
                string name = _large ? RoomCatalog.Name(c.Room.Kind) : RoomCatalog.Mark(c.Room.Kind);
                c.Mark.text = shown ? name : known ? (locked && Known(c.Room) ? (_large ? "鍵のかかった部屋" : "錠") : "?") : "";
                c.Mark.color = shown ? (used || (d.IsCleared(c.Room.Id) && c.Room.IsFight) ? kindColor * 0.55f : kindColor) : new Color(0.5f, 0.47f, 0.45f);
            }
            foreach (var (line, a, b) in _links)
                line.color = Known(a) && Known(b) && (d.IsVisited(a.Id) || d.IsVisited(b.Id) || d.MapRevealed.Value) ? new Color(0.45f, 0.4f, 0.35f, 0.9f) : Color.clear;

            int slot = 0;
            foreach (var p in NetPlayer.All)
            {
                if (slot >= _dots.Count) break;
                var dot = _dots[slot];
                var room = map.RoomAt(p.transform.position);
                dot.enabled = room != null;
                if (room != null)
                {
                    // Where in the room they stand, squeezed into the map cell.
                    Vector2 local = (Vector2)p.transform.position - room.Bounds.center;
                    Vector2 inCell = new(local.x / room.Bounds.width * (CellW - 10f), local.y / room.Bounds.height * (CellH - 10f));
                    if (_large) inCell.y -= CellH * 0.22f; // under the room's name
                    dot.rectTransform.anchoredPosition = CellCenter(room.Cell) + inCell;
                    bool me = p == NetPlayer.Local;
                    dot.color = p.Downed.Value
                        ? new Color(1f, 0.15f, 0.1f, Mathf.PingPong(Time.time * 3f, 1f) > 0.5f ? 1f : 0.25f)
                        : me ? Color.white : ClassColor(p.Class.Value);
                    float size = (me ? 12f : 10f) * (_large ? 1.8f : 1f);
                    dot.rectTransform.sizeDelta = new Vector2(size, size);
                    dot.transform.SetAsLastSibling();
                }
                slot++;
            }
            for (int i = slot; i < _dots.Count; i++) _dots[i].enabled = false;
            UpdatePings(map);
        }

        /// <summary>Pings pulse on the map where they were made (so a call from another room can be found).</summary>
        private void UpdatePings(DungeonMap map)
        {
            Pings.Prune();
            int k = 0;
            foreach (var m in Pings.Active)
            {
                if (k >= _pingMarks.Count) break;
                var room = map.RoomAt(m.Position);
                if (room == null) continue;
                var (ring, core) = _pingMarks[k++];
                Vector2 local = m.Position - room.Bounds.center;
                Vector2 inCell = new(local.x / room.Bounds.width * (CellW - 10f), local.y / room.Bounds.height * (CellH - 10f));
                if (_large) inCell.y -= CellH * 0.22f;
                Vector2 at = CellCenter(room.Cell) + inCell;
                float scale = _large ? 1.8f : 1f;
                float fade = Mathf.Clamp01(Pings.Life - m.Age);
                float pulse = Mathf.Repeat(m.Age * 1.3f, 1f);
                var color = Pings.ColorOf(m);
                ring.enabled = core.enabled = true;
                ring.rectTransform.anchoredPosition = core.rectTransform.anchoredPosition = at;
                float size = Mathf.Lerp(10f, 34f, pulse) * scale;
                ring.rectTransform.sizeDelta = new Vector2(size, size);
                ring.color = new Color(color.r, color.g, color.b, (1f - pulse) * fade);
                core.rectTransform.sizeDelta = new Vector2(8f * scale, 8f * scale);
                core.color = new Color(color.r, color.g, color.b, fade * (Mathf.PingPong(m.Age * 4f, 1f) > 0.3f ? 1f : 0.4f));
                ring.transform.SetAsLastSibling();
                core.transform.SetAsLastSibling();
            }
            for (; k < _pingMarks.Count; k++) _pingMarks[k].Ring.enabled = _pingMarks[k].Core.enabled = false;
        }

        /// <summary>The known room drawn under a screen point (the big map pings a room with a click).</summary>
        public MapRoom RoomUnder(Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_grid, screenPoint, null, out var local)) return null;
            Vector2 p = local - _grid.rect.min; // from the grid's bottom-left, like the cell centres
            foreach (var c in _cells)
            {
                if (c.Frame.color.a <= 0.01f) continue; // nobody knows it yet
                Vector2 d = p - CellCenter(c.Room.Cell);
                if (Mathf.Abs(d.x) <= CellW * 0.5f && Mathf.Abs(d.y) <= CellH * 0.5f) return c.Room;
            }
            return null;
        }

        private static Color ClassColor(ClassId c) => c switch
        {
            ClassId.Warrior => new Color(1f, 0.4f, 0.35f),
            ClassId.Mage => new Color(0.45f, 0.65f, 1f),
            ClassId.Ranger => new Color(0.45f, 0.9f, 0.45f),
            ClassId.Guardian => new Color(1f, 0.85f, 0.45f),
            ClassId.Assassin => new Color(0.75f, 0.5f, 1f),
            _ => new Color(1f, 0.6f, 0.25f),
        };
    }
}
