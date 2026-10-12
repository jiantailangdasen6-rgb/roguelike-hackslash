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
        private const float CellW = 40f, CellH = 28f, Gap = 12f;
        private const float Pad = 10f;

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
        private (ulong Seed, int Area) _key = (0, -1);

        public RectTransform Root => _root;

        public MinimapView(Transform parent, Vector2 offset)
        {
            float w = DungeonMap.Size * (CellW + Gap) - Gap + Pad * 2f, h = DungeonMap.Size * (CellH + Gap) - Gap + Pad * 2f;
            var panel = UIKit.Framed(parent, "Minimap", new Color(0.03f, 0.025f, 0.04f, 0.78f));
            _root = panel.Outer().Place(new Vector2(1f, 1f), new Vector2(w, h), offset);
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
                    line.rectTransform.sizeDelta = dir == Dir.E ? new Vector2(Gap + 6f, 6f) : new Vector2(6f, Gap + 6f);
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
                var mark = UIKit.Label(box.transform, "", 18, TextAnchor.MiddleCenter);
                mark.rectTransform.Stretch();
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
                if (d.IsVisited(r.Id)) return true;
                foreach (int n in r.Neighbor) if (n >= 0 && d.IsVisited(n)) return true;
                return false;
            }

            foreach (var c in _cells)
            {
                bool visited = d.IsVisited(c.Room.Id), known = visited || Known(c.Room);
                bool current = c.Room == here;
                bool fighting = d.IsFighting(c.Room.Id);
                var kindColor = RoomCatalog.Color(c.Room.Kind);
                c.Frame.color = !known ? Color.clear
                    : current ? new Color(1f, 0.85f, 0.45f, 1f)
                    : fighting ? new Color(1f, 0.3f, 0.25f, 0.6f + 0.4f * Mathf.Sin(Time.time * 6f))
                    : visited ? new Color(0.55f, 0.48f, 0.4f, 0.9f) : new Color(0.32f, 0.29f, 0.27f, 0.7f);
                c.Box.color = !known ? Color.clear : visited ? new Color(0.13f, 0.11f, 0.12f, 0.95f) : new Color(0.06f, 0.05f, 0.06f, 0.85f);
                bool used = NetPlayer.Local != null && (NetPlayer.Local.UsedPedestals.Value & (1 << c.Room.Id)) != 0;
                c.Mark.text = visited ? RoomCatalog.Mark(c.Room.Kind) : known ? "?" : "";
                c.Mark.color = visited ? (used || (d.IsCleared(c.Room.Id) && c.Room.IsFight) ? kindColor * 0.55f : kindColor) : new Color(0.5f, 0.47f, 0.45f);
            }
            foreach (var (line, a, b) in _links)
                line.color = Known(a) && Known(b) && (d.IsVisited(a.Id) || d.IsVisited(b.Id)) ? new Color(0.45f, 0.4f, 0.35f, 0.9f) : Color.clear;

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
                    dot.rectTransform.anchoredPosition = CellCenter(room.Cell) + inCell;
                    bool me = p == NetPlayer.Local;
                    dot.color = p.Downed.Value
                        ? new Color(1f, 0.15f, 0.1f, Mathf.PingPong(Time.time * 3f, 1f) > 0.5f ? 1f : 0.25f)
                        : me ? Color.white : ClassColor(p.Class.Value);
                    dot.rectTransform.sizeDelta = me ? new Vector2(12f, 12f) : new Vector2(10f, 10f);
                    dot.transform.SetAsLastSibling();
                }
                slot++;
            }
            for (int i = slot; i < _dots.Count; i++) _dots[i].enabled = false;
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
