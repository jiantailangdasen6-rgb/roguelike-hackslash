using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>What a room on the area's map holds.</summary>
    public enum RoomKind : byte
    {
        Start,      // where the team arrives (quiet)
        Combat,     // a short fight (2-3 waves)
        Elite,      // elites lead every wave; better loot
        Shrine,     // pick a new power (each player once)
        Whetstone,  // raise a power you have by a level
        Spring,     // max HP up and a full heal
        CurseAltar, // two powers, and a curse for the rest of the run
        Merchant,   // buy with the run's soul shards
        Campfire,   // the breather before the boss: heals, chests
        Boss,       // the area's lord (everyone enters together)
        MiniBoss,   // a champion and its escort: drops a key
        Puzzle,     // a light trial (pressure plates, guard the crystal): solved, it gives a key
        Treasure,   // behind a locked door (a key): gear for everyone
        Secret,     // behind a cracked wall: the old map of the area, and treasure
        Spirit,     // a wandering spirit: offer a tenth of your max HP for a god's blessing (each player once)
        Encounter,  // something met on the way: the team chooses between two (RunSimulation.Encounters)
    }

    /// <summary>Which trial a puzzle room holds.</summary>
    public enum PuzzleKind : byte
    {
        Plates,  // light every pressure plate within a few seconds of each other
        Crystal, // keep the crystal alive while monsters go for it
        Thief,    // catch the imp running off with treasure before it escapes
        Gauntlet, // get through the traps to the plate in the middle
        Twin,     // two plates far apart, held at the same time (a co-op trial; alone they stay lit a while)
    }

    /// <summary>Directions of a room's doors (bit = 1 << dir).</summary>
    public static class Dir
    {
        public const int N = 0, E = 1, S = 2, W = 3;

        public static Vector2Int Step(int d) => d switch
        {
            N => new Vector2Int(0, 1),
            E => new Vector2Int(1, 0),
            S => new Vector2Int(0, -1),
            _ => new Vector2Int(-1, 0),
        };

        public static int Opposite(int d) => (d + 2) & 3;
    }

    /// <summary>One room of an area's map, placed in the world at its own spot (rooms never overlap).</summary>
    public sealed class MapRoom
    {
        public int Id;
        public Vector2Int Cell;
        public RoomKind Kind;
        /// <summary>Steps from the start room.</summary>
        public int Depth;
        public PuzzleKind Puzzle;
        /// <summary>What an encounter room holds.</summary>
        public EncounterKind Encounter;
        /// <summary>A dark hall: fought by the light of a lantern someone carries.</summary>
        public bool Dark;
        /// <summary>A fight room with a caged adventurer to set free (one per area).</summary>
        public bool Captive;
        /// <summary>Monster strength tier for this room (area * 4 + 0..3), the old "room index".</summary>
        public int Index;
        /// <summary>Obstacles and gimmicks, already moved to the room's place in the world.</summary>
        public RoomLayout Layout;
        public Vector2 Origin;
        public readonly int[] Neighbor = { -1, -1, -1, -1 };

        public Rect Bounds => new(Origin + RunSimulation.DefaultBounds.min, RunSimulation.DefaultBounds.size);
        public bool HasDoor(int dir) => Neighbor[dir] >= 0;
        public bool IsFight => Kind is RoomKind.Combat or RoomKind.Elite or RoomKind.Boss or RoomKind.MiniBoss or RoomKind.Puzzle;
        /// <summary>Rooms with a pedestal to use (each player once).</summary>
        public bool HasPedestal => Kind is RoomKind.Shrine or RoomKind.Whetstone or RoomKind.Spring or RoomKind.CurseAltar or RoomKind.Merchant or RoomKind.Treasure or RoomKind.Secret or RoomKind.Spirit;
        /// <summary>The room behind this door is shut by a lock (treasure) or hidden by a cracked wall (secret).</summary>
        public bool Sealed => Kind is RoomKind.Treasure or RoomKind.Secret;
    }

    /// <summary>
    /// The passage between two neighbouring rooms (A to B, B lies <see cref="Dir"/> of A): a straight strip of floor from
    /// A's wall to B's, as wide as a doorway. Walked through on foot (phase 5: the labyrinth in one piece).
    /// </summary>
    public sealed class Corridor
    {
        public int A, B, Dir;
        public Rect Rect;
        public bool Vertical => Dir == Game.Core.Dir.N;
    }

    /// <summary>
    /// An area of the labyrinth: 8-10 rooms on a 5 x 5 grid, generated from the run's seed so every peer builds the same
    /// map. Rooms sit apart in the world and short corridors join the neighbours, so the team walks from room to room;
    /// the simulation runs several rooms at once while players explore apart.
    /// </summary>
    public sealed class DungeonMap
    {
        /// <summary>Half the width of a corridor (four tiles across).</summary>
        public const float CorridorHalf = 2f;

        public readonly List<Corridor> Corridors = new();

        /// <summary>The corridor between two rooms (either way round), or null.</summary>
        public Corridor CorridorBetween(int a, int b) =>
            Corridors.Find(c => (c.A == a && c.B == b) || (c.A == b && c.B == a));

        /// <summary>Joins every pair of neighbours with a corridor (north and east of each room).</summary>
        private void BuildCorridors()
        {
            Corridors.Clear();
            foreach (var a in Rooms)
                foreach (int d in new[] { Game.Core.Dir.N, Game.Core.Dir.E })
                {
                    var b = this[a.Neighbor[d]];
                    if (b == null) continue;
                    Rect ra = a.Bounds, rb = b.Bounds;
                    var rect = d == Game.Core.Dir.N
                        ? new Rect(ra.center.x - CorridorHalf, ra.yMax, 2f * CorridorHalf, rb.yMin - ra.yMax)
                        : new Rect(ra.xMax, ra.center.y - CorridorHalf, rb.xMin - ra.xMax, 2f * CorridorHalf);
                    Corridors.Add(new Corridor { A = a.Id, B = b.Id, Dir = d, Rect = rect });
                }
        }

        /// <summary>
        /// Can a body of this radius stand here: on a room's floor (<paramref name="floor"/> gives it, so the Lich's hall
        /// can shrink), or in an open corridor (its ends reach a little into both rooms so the step across is smooth).
        /// </summary>
        public bool Walkable(Vector2 p, float r, System.Func<Corridor, bool> open, System.Func<MapRoom, Rect> floor)
        {
            foreach (var room in Rooms)
            {
                var f = floor(room);
                if (p.x >= f.xMin + r && p.x <= f.xMax - r && p.y >= f.yMin + r && p.y <= f.yMax - r) return true;
            }
            foreach (var c in Corridors)
            {
                var k = c.Rect;
                bool inside = c.Vertical
                    ? p.x >= k.xMin + r && p.x <= k.xMax - r && p.y >= k.yMin - 1f && p.y <= k.yMax + 1f
                    : p.y >= k.yMin + r && p.y <= k.yMax - r && p.x >= k.xMin - 1f && p.x <= k.xMax + 1f;
                if (inside && open(c)) return true;
            }
            return false;
        }

        /// <summary>Inside any corridor (shots fly down them).</summary>
        public bool InCorridor(Vector2 p, float margin = 0.3f)
        {
            foreach (var c in Corridors)
            {
                var k = c.Rect;
                if (p.x >= k.xMin - margin && p.x <= k.xMax + margin && p.y >= k.yMin - margin && p.y <= k.yMax + margin) return true;
            }
            return false;
        }

        /// <summary>
        /// How far a monster of <paramref name="room"/> may follow into one of its open corridors (the first
        /// <paramref name="depth"/> units past the doorway): the point kept within that stretch, or null when the point is
        /// not in one of them.
        /// </summary>
        public Vector2? Leash(MapRoom room, Vector2 p, float r, float depth, System.Func<Corridor, bool> open)
        {
            var b = room.Bounds;
            foreach (var c in Corridors)
            {
                if ((c.A != room.Id && c.B != room.Id) || !open(c)) continue;
                var k = c.Rect;
                Rect band;
                if (c.Vertical)
                    band = c.A == room.Id ? new Rect(k.xMin, b.yMax - 1f, k.width, depth + 1f) : new Rect(k.xMin, b.yMin - depth, k.width, depth + 1f);
                else
                    band = c.A == room.Id ? new Rect(b.xMax - 1f, k.yMin, depth + 1f, k.height) : new Rect(b.xMin - depth, k.yMin, depth + 1f, k.height);
                if (!band.Contains(p)) continue;
                return c.Vertical
                    ? new Vector2(Mathf.Clamp(p.x, band.xMin + r, band.xMax - r), p.y)
                    : new Vector2(p.x, Mathf.Clamp(p.y, band.yMin + r, band.yMax - r));
            }
            return null;
        }

        /// <summary>A step from <paramref name="from"/> toward <paramref name="to"/> that stays where one can stand (sliding along walls).</summary>
        public Vector2 Step(Vector2 from, Vector2 to, float r, System.Func<Corridor, bool> open, System.Func<MapRoom, Rect> floor)
        {
            if (!Walkable(from, r, open, floor) || Walkable(to, r, open, floor)) return to; // stuck in a closed place: let them out
            var slideX = new Vector2(to.x, from.y);
            if (Walkable(slideX, r, open, floor)) return slideX;
            var slideY = new Vector2(from.x, to.y);
            return Walkable(slideY, r, open, floor) ? slideY : from;
        }

        /// <summary>Everything the map's floors and corridors cover (for the camera).</summary>
        public Rect Extent
        {
            get
            {
                var e = Rooms[0].Bounds;
                foreach (var r in Rooms)
                {
                    var b = r.Bounds;
                    e = Rect.MinMaxRect(Mathf.Min(e.xMin, b.xMin), Mathf.Min(e.yMin, b.yMin), Mathf.Max(e.xMax, b.xMax), Mathf.Max(e.yMax, b.yMax));
                }
                return e;
            }
        }

        public const int Size = 5;
        public const float PitchX = 44f, PitchY = 30f;
        /// <summary>Everything any room of any map can reach (for the monster grid).</summary>
        public static readonly Rect WorldBounds = new(-(Size - 0.4f) * PitchX, -(Size - 0.4f) * PitchY, 2f * (Size - 0.4f) * PitchX, 2f * (Size - 0.4f) * PitchY);

        public readonly int Area;
        /// <summary>The land this area is in (its look and its fight-room layouts).</summary>
        public readonly Biome Biome;
        public readonly List<MapRoom> Rooms = new();
        public MapRoom Start { get; private set; }
        public MapRoom Boss { get; private set; }
        public MapRoom Campfire { get; private set; }
        private readonly int[,] _grid = new int[Size, Size];

        private DungeonMap(int area, Biome biome)
        {
            Area = area;
            Biome = biome;
            for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                _grid[x, y] = -1;
        }

        public MapRoom this[int id] => id >= 0 && id < Rooms.Count ? Rooms[id] : null;

        /// <summary>The start room sits at the world origin (where the single arena used to be); the others around it.</summary>
        public Vector2 OriginOf(Vector2Int cell) => new((cell.x - _startCell.x) * PitchX, (cell.y - _startCell.y) * PitchY);
        private Vector2Int _startCell;

        /// <summary>The room whose floor holds this point (null between rooms).</summary>
        public MapRoom RoomAt(Vector2 world, float margin = 1.5f)
        {
            int cx = Mathf.RoundToInt(world.x / PitchX) + _startCell.x, cy = Mathf.RoundToInt(world.y / PitchY) + _startCell.y;
            if (cx < 0 || cy < 0 || cx >= Size || cy >= Size) return null;
            var room = this[_grid[cx, cy]];
            if (room == null) return null;
            var b = room.Bounds;
            return world.x >= b.xMin - margin && world.x <= b.xMax + margin && world.y >= b.yMin - margin && world.y <= b.yMax + margin + 2f ? room : null;
        }

        /// <summary>Middle of the doorway on a room's wall.</summary>
        public static Vector2 DoorPosition(MapRoom room, int dir)
        {
            var b = room.Bounds;
            return dir switch
            {
                Dir.N => new Vector2(b.center.x, b.yMax),
                Dir.S => new Vector2(b.center.x, b.yMin),
                Dir.E => new Vector2(b.xMax, b.center.y),
                _ => new Vector2(b.xMin, b.center.y),
            };
        }

        /// <summary>Where someone arriving through the door on this side stands (a step inside).</summary>
        public static Vector2 EntryPoint(MapRoom room, int dir)
        {
            var at = DoorPosition(room, dir);
            return at - (Vector2)Dir.Step(dir) * 1.8f;
        }

        // ------------------------------------------------------------------ Generation

        public static DungeonMap Generate(ulong seed, int area) => Generate(seed, area, BiomeCatalog.Classic(area));

        public static DungeonMap Generate(ulong seed, int area, Biome biome)
        {
            var rng = new GameRandom(seed ^ (0x9E3779B97F4A7C15ul * (ulong)(area + 1)));
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var map = new DungeonMap(area, biome);
                if (map.TryBuild(rng, biome == Biome.Heart ? 8 : 11 + (area > 0 ? 1 : 0))) // the heart is a short land
                {
                    map.AssignLayouts(rng);
                    map.PickDarkHall(new GameRandom(seed ^ (0xDA4C1E55u * (ulong)(area + 1))));
                    map.PickCaptive(new GameRandom(seed ^ (0xCA9715EEu * (ulong)(area + 1))));
                    map.BuildCorridors();
                    return map;
                }
            }
            var fallback = new DungeonMap(area, biome);
            fallback.BuildLine();
            fallback.AssignLayouts(rng);
            fallback.BuildCorridors();
            return fallback;
        }

        private MapRoom Place(Vector2Int cell)
        {
            if (Rooms.Count == 0) _startCell = cell;
            var room = new MapRoom { Id = Rooms.Count, Cell = cell, Origin = OriginOf(cell) };
            Rooms.Add(room);
            _grid[cell.x, cell.y] = room.Id;
            return room;
        }

        private static void Link(MapRoom a, MapRoom b, int dir)
        {
            a.Neighbor[dir] = b.Id;
            b.Neighbor[Dir.Opposite(dir)] = a.Id;
        }

        private bool Free(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Size && c.y < Size && _grid[c.x, c.y] < 0;

        private int Occupied(Vector2Int c)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
            {
                var o = c + Dir.Step(d);
                if (o.x >= 0 && o.y >= 0 && o.x < Size && o.y < Size && _grid[o.x, o.y] >= 0) n++;
            }
            return n;
        }

        /// <summary>A branching walk: each new room touches only the room it grew from (so the map stays a tree of corridors).</summary>
        private bool TryBuild(GameRandom rng, int count)
        {
            Start = Place(new Vector2Int(rng.Range(1, 4), 0));
            int guard = 0;
            while (Rooms.Count < count && guard++ < 400)
            {
                // Grow mostly from the newest rooms (long corridors), sometimes from older ones (side branches).
                var from = Rooms[rng.Chance(0.6f) ? Rooms.Count - 1 - rng.Range(0, Mathf.Min(2, Rooms.Count)) : rng.Range(0, Rooms.Count)];
                int dir = rng.Range(0, 4);
                var cell = from.Cell + Dir.Step(dir);
                if (!Free(cell) || Occupied(cell) > 1) continue;
                Link(from, Place(cell), dir);
            }
            if (Rooms.Count < count) return false;

            // Depth from the start; the boss waits at the farthest dead end, the campfire just before it.
            var queue = new Queue<MapRoom>();
            foreach (var r in Rooms) r.Depth = -1;
            Start.Depth = 0;
            queue.Enqueue(Start);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (int n in r.Neighbor)
                    if (n >= 0 && Rooms[n].Depth < 0)
                    {
                        Rooms[n].Depth = r.Depth + 1;
                        queue.Enqueue(Rooms[n]);
                    }
            }
            MapRoom boss = null;
            foreach (var r in Rooms)
                if (r != Start && DoorCount(r) == 1 && (boss == null || r.Depth > boss.Depth)) boss = r;
            if (boss == null || boss.Depth < 4) return false;
            Boss = boss;
            Campfire = this[ParentOf(boss)];
            if (Campfire == null || Campfire == Start || DoorCount(Campfire) > 3) return false;

            Start.Kind = RoomKind.Start;
            Boss.Kind = RoomKind.Boss;
            Campfire.Kind = RoomKind.Campfire;
            AssignKinds(rng);
            AddSecret(rng);
            return true;
        }

        /// <summary>A hidden room off a quiet corner: a free cell next to exactly one ordinary room.</summary>
        private void AddSecret(GameRandom rng)
        {
            var options = new List<(MapRoom From, int Dir)>();
            foreach (var r in Rooms)
            {
                if (r.Kind is RoomKind.Boss or RoomKind.Campfire or RoomKind.Treasure) continue;
                for (int d = 0; d < 4; d++)
                {
                    var c = r.Cell + Dir.Step(d);
                    if (Free(c) && Occupied(c) == 1) options.Add((r, d));
                }
            }
            if (options.Count == 0) return;
            var (from, dir) = options[rng.Range(0, options.Count)];
            var secret = Place(from.Cell + Dir.Step(dir));
            Link(from, secret, dir);
            secret.Kind = RoomKind.Secret;
            secret.Depth = from.Depth + 1;
            secret.Index = from.Index;
        }

        private static int DoorCount(MapRoom r)
        {
            int n = 0;
            foreach (int x in r.Neighbor) if (x >= 0) n++;
            return n;
        }

        private int ParentOf(MapRoom r)
        {
            foreach (int n in r.Neighbor)
                if (n >= 0 && Rooms[n].Depth == r.Depth - 1) return n;
            return -1;
        }

        /// <summary>
        /// Rooms on the way to the boss are fights; dead ends off the way are the treats worth a detour
        /// (shrine, spring or whetstone, and from the second area a curse altar or the merchant).
        /// </summary>
        private void AssignKinds(GameRandom rng)
        {
            var onPath = new HashSet<int>();
            for (var r = Campfire; r != null && r != Start; r = this[ParentOf(r)]) onPath.Add(r.Id);

            var free = new List<MapRoom>();
            foreach (var r in Rooms)
                if (r != Start && r != Boss && r != Campfire) free.Add(r);
            // Side rooms (off the path, dead ends first) get the treats.
            free.Sort((a, b) =>
            {
                int sa = (onPath.Contains(a.Id) ? 2 : 0) + (DoorCount(a) == 1 ? 0 : 1);
                int sb = (onPath.Contains(b.Id) ? 2 : 0) + (DoorCount(b) == 1 ? 0 : 1);
                return sa != sb ? sa.CompareTo(sb) : rng.Range(-1, 2);
            });

            // The treasure room needs a dead end off the path (its door is locked); the keys come from the champion and
            // the puzzle. The way to the boss keeps at least two fights.
            var treats = new List<RoomKind> { RoomKind.MiniBoss, RoomKind.Puzzle, RoomKind.Shrine, rng.Chance(0.5f) ? RoomKind.Spring : RoomKind.Whetstone };
            // Deeper in, the shrine is sometimes a wandering spirit instead (a blessing for a tenth of max HP).
            if (Area >= 1 && rng.Chance(0.4f)) treats[2] = RoomKind.Spirit;
            if (Area >= 1) treats.Add(rng.Chance(0.5f) ? RoomKind.CurseAltar : RoomKind.Merchant);
            // An encounter (decided apart from the map's own draws, so the rest of the map stays as it was).
            var encounterRng = new GameRandom((ulong)(Boss.Cell.x * 73856093 ^ Boss.Cell.y * 19349663 ^ Campfire.Cell.x * 83492791 ^ Campfire.Cell.y * 2654435 ^ Area * 7919 ^ Rooms.Count * 104729) ^ 0xE7C0ul);
            // It takes the place of the spring or whetstone now and then (the map keeps its fights).
            if (encounterRng.Chance(0.6f)) treats[3] = RoomKind.Encounter;
            var treasure = free.Find(r => !onPath.Contains(r.Id) && DoorCount(r) == 1);
            if (treasure != null)
            {
                treasure.Kind = RoomKind.Treasure;
                free.Remove(treasure);
            }
            int pathRooms = 0;
            foreach (var r in free) if (onPath.Contains(r.Id)) pathRooms++;
            int t = 0;
            bool elite = false;
            foreach (var r in free)
            {
                bool keepForFight = onPath.Contains(r.Id) && pathRooms <= 2;
                if (t < treats.Count && !keepForFight)
                {
                    r.Kind = treats[t++];
                    if (onPath.Contains(r.Id)) pathRooms--;
                    continue;
                }
                r.Kind = !elite && r.Depth >= 2 ? RoomKind.Elite : RoomKind.Combat;
                elite |= r.Kind == RoomKind.Elite;
            }
            // No champion and no puzzle (a cramped map): nothing would ever open the treasure room.
            if (treasure != null && !Rooms.Exists(r => r.Kind is RoomKind.MiniBoss or RoomKind.Puzzle)) treasure.Kind = RoomKind.Shrine;
            foreach (var r in Rooms)
                if (r.Kind == RoomKind.Puzzle) r.Puzzle = (PuzzleKind)rng.Range(0, 5);
            foreach (var r in Rooms)
                if (r.Kind == RoomKind.Encounter) r.Encounter = (EncounterKind)new GameRandom((ulong)(r.Id * 31 + Area * 977 + r.Depth * 7) ^ 0x5EEDul).Range(0, RunSimulation.EncounterCount);

            int maxDepth = Boss.Depth;
            foreach (var r in Rooms)
            {
                int tier = r.Kind == RoomKind.Boss ? 3 : Mathf.Clamp(Mathf.FloorToInt(3f * r.Depth / Mathf.Max(1, maxDepth)), 0, 2);
                r.Index = Area * RunSimulation.RoomsPerArea + tier;
            }
        }

        private static int CountFights(List<MapRoom> rooms)
        {
            int n = 0;
            foreach (var r in rooms) if (r.Kind is RoomKind.Combat or RoomKind.Elite) n++;
            return n;
        }

        /// <summary>Room layouts: quiet halls for the start and the pedestals, the throne room for the boss, the rest vary by area.</summary>
        private void AssignLayouts(GameRandom rng)
        {
            int previous = -1;
            foreach (var r in Rooms)
            {
                int id = r.Kind switch
                {
                    RoomKind.Start => 0,
                    RoomKind.Boss => 10,
                    RoomKind.Campfire => Arena.RestLayout,
                    RoomKind.Combat or RoomKind.Elite or RoomKind.MiniBoss => Arena.PickFight(rng, Biome, previous),
                    RoomKind.Puzzle => r.Puzzle switch
                    {
                        PuzzleKind.Plates or PuzzleKind.Twin => Arena.PlatesLayout,
                        PuzzleKind.Thief => 1,
                        PuzzleKind.Gauntlet => Arena.GauntletLayout,
                        _ => 0,
                    },
                    _ => Arena.SanctumLayout,
                };
                if (r.Kind is RoomKind.Combat or RoomKind.Elite or RoomKind.MiniBoss) previous = id;
                r.Layout = Arena.Get(id).Placed(r.Origin);
            }
        }

        /// <summary>One ordinary fight per area is a dark hall (its own random stream: the rest of the map stays as it was).</summary>
        private void PickDarkHall(GameRandom rng)
        {
            var fights = Rooms.FindAll(r => r.Kind == RoomKind.Combat);
            if (fights.Count == 0) return;
            fights[rng.Range(0, fights.Count)].Dark = true;
        }

        /// <summary>One other ordinary fight holds a caged adventurer (its own random stream, like the dark hall).</summary>
        private void PickCaptive(GameRandom rng)
        {
            var fights = Rooms.FindAll(r => r.Kind == RoomKind.Combat && !r.Dark);
            if (fights.Count == 0) return;
            fights[rng.Range(0, fights.Count)].Captive = true;
        }

        /// <summary>The dark hall of this area, or null.</summary>
        public MapRoom DarkHall => Rooms.Find(r => r.Dark);

        /// <summary>Should never be needed: a straight line start - fights - campfire - boss.</summary>
        private void BuildLine()
        {
            Start = Place(new Vector2Int(2, 0));
            var prev = Start;
            for (int y = 1; y < Size; y++)
            {
                var r = Place(new Vector2Int(2, y));
                Link(prev, r, Dir.N);
                r.Depth = y;
                r.Kind = y == Size - 1 ? RoomKind.Boss : y == Size - 2 ? RoomKind.Campfire : RoomKind.Combat;
                r.Index = Area * RunSimulation.RoomsPerArea + Mathf.Min(3, y - 1);
                prev = r;
            }
            Start.Kind = RoomKind.Start;
            Start.Index = Area * RunSimulation.RoomsPerArea;
            Boss = Rooms[Rooms.Count - 1];
            Campfire = Rooms[Rooms.Count - 2];
        }
    }
}
