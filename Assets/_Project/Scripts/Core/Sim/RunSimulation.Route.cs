using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The run's route through the lands (Biome.cs). A run starts in a land picked from its seed; once an area's
    /// lord falls two ways open in its hall (left and right), each to another land, and the whole team picks one
    /// by standing in it. Past the third lord the left way leads home and the right one deeper.
    /// Without <see cref="UseVariedRoute"/> (unit tests) the route is the classic crypt, chapel, abyss.
    /// </summary>
    public sealed partial class RunSimulation
    {
        private readonly List<Biome> _route = new();
        private Biome[] _plannedRoute;
        private List<(EnemyType Type, int Weight)> _pool;

        /// <summary>Lands are picked from the seed and by the team (otherwise the classic order).</summary>
        public bool VariedRoute { get; private set; }

        /// <summary>The land of the current area.</summary>
        public Biome Biome => BiomeOf(Area);

        /// <summary>The land of an area this run has been through (or is in).</summary>
        public Biome BiomeOf(int area) => area >= 0 && area < _route.Count ? _route[area] : BiomeCatalog.Classic(area);

        /// <summary>The lands so far, one per area.</summary>
        public IReadOnlyList<Biome> Route => _route;

        /// <summary>The two ways open after this area's lord: [0] the left one (home past the third lord), [1] the right one.</summary>
        public readonly Biome[] NextWays = new Biome[2];

        /// <summary>
        /// A real run: the first land comes from the seed and the ways down offer new lands. <paramref name="planned"/>
        /// fixes the lands of the first areas (screenshots, tests: -route).
        /// </summary>
        public void UseVariedRoute(params Biome[] planned)
        {
            VariedRoute = true;
            _plannedRoute = planned != null && planned.Length > 0 ? planned : null;
            _route.Clear();
            PickOmen(); // RunSimulation.Omens.cs
            BuildArea(0);
        }

        /// <summary>The land an area gets when nobody chose it (the first area, debug jumps).</summary>
        private Biome DefaultBiome(int area)
        {
            if (_plannedRoute != null && area < _plannedRoute.Length) return _plannedRoute[area];
            if (!VariedRoute) return BiomeCatalog.Classic(area);
            var options = Unvisited(exclude: -1);
            var rng = new GameRandom(Seed ^ (0xB10E5EEDul * (ulong)(area + 1)));
            return options[rng.Range(0, options.Count)];
        }

        /// <summary>Lands not on the route yet (all but <paramref name="exclude"/> once every land has been seen).</summary>
        private List<Biome> Unvisited(int exclude)
        {
            var list = new List<Biome>();
            foreach (var b in BiomeCatalog.Routable)
                if (!_route.Contains(b.Id)) list.Add(b.Id);
            if (list.Count == 0)
                foreach (var b in BiomeCatalog.Routable)
                    if ((int)b.Id != exclude) list.Add(b.Id);
            return list;
        }

        /// <summary>Puts <paramref name="biome"/> on the route for <paramref name="area"/> (forgetting anything past it).</summary>
        private Biome SetRoute(int area, Biome? biome)
        {
            if (_route.Count > area) _route.RemoveRange(area, _route.Count - area);
            while (_route.Count < area) _route.Add(DefaultBiome(_route.Count));
            var b = biome ?? DefaultBiome(area);
            _route.Add(b);
            _pool = BiomeCatalog.PoolAt(b, area);
            PickNextWays(area);
            return b;
        }

        /// <summary>The two lands offered after this area's lord: new ones if there are, never the land just left.</summary>
        private void PickNextWays(int area)
        {
            if (!VariedRoute || (_plannedRoute != null && area + 1 < _plannedRoute.Length))
            {
                NextWays[0] = NextWays[1] = DefaultBiome(area + 1);
                return;
            }
            var current = _route[area];
            var rng = new GameRandom(Seed ^ (0x3A7E5ul * (ulong)(area + 7)));
            var fresh = Unvisited(exclude: (int)current);
            fresh.Remove(current);
            Shuffle(fresh, rng);
            // Fewer new lands than ways: the other way leads somewhere seen before (but never back into this one).
            var seen = new List<Biome>();
            foreach (var b in BiomeCatalog.Routable)
                if (b.Id != current && !fresh.Contains(b.Id)) seen.Add(b.Id);
            Shuffle(seen, rng);
            fresh.AddRange(seen);
            if (fresh.Count == 0) fresh.Add(current);
            int side = rng.Range(0, 2);
            NextWays[side] = fresh[0];
            NextWays[1 - side] = fresh.Count > 1 ? fresh[1] : fresh[0];
        }

        private static void Shuffle(List<Biome> list, GameRandom rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>The way an area's lord hall offers on the left (home past the third lord) and on the right.</summary>
        public Vector2 LeftWayPosition => Map.Boss.Origin + HomeOffset;

        /// <summary>The land the way at this side leads to (0 left, 1 right); the right one past the third lord is the deep.</summary>
        public Biome WayLand(int side) => NextWays[Mathf.Clamp(side, 0, 1)];

        /// <summary>A monster of the current land for <see cref="Room"/>'s waves.</summary>
        private EnemyType RandomEnemyType()
        {
            var pool = _pool ?? BiomeCatalog.PoolAt(Biome, Room.Area);
            int total = 0;
            foreach (var (_, w) in pool) total += w;
            int pick = _spawnRng.Range(0, total);
            foreach (var (type, w) in pool)
            {
                if (pick < w) return type;
                pick -= w;
            }
            return EnemyType.Skeleton;
        }
    }
}
