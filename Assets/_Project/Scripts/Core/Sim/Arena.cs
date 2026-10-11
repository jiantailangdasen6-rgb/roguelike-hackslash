using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Solid things standing in a room. Tall ones also stop projectiles (cover).</summary>
    public enum ObstacleKind : byte
    {
        Pillar,
        Statue,
        Sarcophagus,
        Altar,
        Pew,
        Crates,
        Crystal,
        Campfire,   // the rest room's fire (heals, see RunSimulation.WarmByTheFire)
    }

    /// <summary>Room gimmicks. Traps hurt players and monsters alike, so luring enemies into them pays off.</summary>
    public enum GimmickKind : byte
    {
        SpikeTrap,   // floor spikes on a cycle
        FlameJet,    // a vent in the wall breathes fire along a line
        HealingFont, // stand on it to heal everyone nearby (once per room)
        PowderKeg,   // a breakable barrel (spawned as a static "enemy"), explodes when hit
        TreasureChest, // rest rooms: a breakable chest (static "enemy") full of souls
    }

    public struct Obstacle
    {
        public ObstacleKind Kind;
        public Vector2 Position; // base centre (feet line) — sprites are anchored here
        public Vector2 Size;     // collision footprint

        public Rect Box => new(Position.x - Size.x * 0.5f, Position.y - Size.y * 0.5f, Size.x, Size.y);
        public bool BlocksProjectiles => Kind is ObstacleKind.Pillar or ObstacleKind.Statue or ObstacleKind.Crystal;
    }

    public struct GimmickDef
    {
        public GimmickKind Kind;
        public Vector2 Position;
        public Vector2 Direction; // flame jets
        public float Phase;       // 0..1 offset in the trap cycle
    }

    public sealed class RoomLayout
    {
        public int Id;
        public string Name;
        public Obstacle[] Obstacles = System.Array.Empty<Obstacle>();
        public GimmickDef[] Gimmicks = System.Array.Empty<GimmickDef>();
        public bool Boss;
    }

    /// <summary>
    /// Hand-made room layouts (obstacles + gimmicks) inside the fixed arena bounds. Shared by the host
    /// simulation, owner-side player movement and the room visuals, so everyone sees the same room.
    /// Layouts keep the start area (bottom centre), the exit (top centre) and the boss spawn clear.
    /// </summary>
    public static class Arena
    {
        public const float SpikeSize = 1.6f;
        public const float SpikePeriod = 3.6f;
        public const float SpikeWarn = 0.75f;
        public const float SpikeActive = 0.5f;
        public const float SpikeDamage = 16f;

        public const float FlameLength = 8.5f;
        public const float FlameWidth = 1.3f;
        public const float FlamePeriod = 5.2f;
        public const float FlameWarn = 1.0f;
        public const float FlameActive = 1.4f;
        public const float FlameTick = 0.35f;
        public const float FlameDamage = 8f;

        public const float FontRadius = 1.2f;
        public const float FontHoldTime = 0.8f;
        public const float FontHealRadius = 4.5f;
        public const float FontHeal = 0.35f;

        public const float KegFuse = 0.55f;
        public const float KegRadius = 2.6f;
        public const float KegDamageToPlayers = 20f;

        private static Obstacle O(ObstacleKind kind, float x, float y) => new()
        {
            Kind = kind,
            Position = new Vector2(x, y),
            Size = kind switch
            {
                ObstacleKind.Pillar => new Vector2(1.0f, 0.7f),
                ObstacleKind.Statue => new Vector2(1.1f, 0.8f),
                ObstacleKind.Sarcophagus => new Vector2(2.3f, 0.9f),
                ObstacleKind.Altar => new Vector2(2.4f, 1.0f),
                ObstacleKind.Pew => new Vector2(3.2f, 0.6f),
                ObstacleKind.Crates => new Vector2(1.3f, 0.9f),
                ObstacleKind.Campfire => new Vector2(1.5f, 0.8f),
                _ => new Vector2(1.0f, 0.8f),
            },
        };

        private static GimmickDef G(GimmickKind kind, float x, float y, float phase = 0f, float dx = 0f, float dy = 0f) =>
            new() { Kind = kind, Position = new Vector2(x, y), Phase = phase, Direction = new Vector2(dx, dy) };

        private const ObstacleKind P = ObstacleKind.Pillar;
        private const GimmickKind Spike = GimmickKind.SpikeTrap;
        private const GimmickKind Flame = GimmickKind.FlameJet;
        private const GimmickKind Keg = GimmickKind.PowderKeg;
        private const GimmickKind Font = GimmickKind.HealingFont;
        private const GimmickKind Chest = GimmickKind.TreasureChest;

        public const int RestLayout = 11;
        public static readonly Vector2 RestFire = Vector2.zero;

        public static readonly RoomLayout[] Layouts =
        {
            new() { Id = 0, Name = "open" },
            new()
            {
                Id = 1, Name = "four_pillars",
                Obstacles = new[] { O(P, -7f, 3f), O(P, 7f, 3f), O(P, -7f, -3f), O(P, 7f, -3f) },
                Gimmicks = new[] { G(Keg, -11.5f, 5.5f), G(Keg, 11.5f, -5.5f) },
            },
            new()
            {
                Id = 2, Name = "colonnade",
                Obstacles = new[] { O(P, -9.5f, 2f), O(P, 9.5f, 2f), O(P, -4.5f, -0.5f), O(P, 4.5f, -0.5f), O(P, -9.5f, -4f), O(P, 9.5f, -4f) },
                Gimmicks = new[] { G(Spike, 0f, 2.2f, 0f), G(Spike, -7f, 4.8f, 0.5f), G(Spike, 7f, 4.8f, 0.5f) },
            },
            new()
            {
                Id = 3, Name = "tombs",
                Obstacles = new[]
                {
                    O(ObstacleKind.Sarcophagus, -8f, 2.5f), O(ObstacleKind.Sarcophagus, 8f, 2.5f),
                    O(ObstacleKind.Sarcophagus, -6f, -2.5f), O(ObstacleKind.Sarcophagus, 6f, -2.5f),
                    O(ObstacleKind.Statue, -11.5f, -0.2f), O(ObstacleKind.Statue, 11.5f, -0.2f),
                },
                Gimmicks = new[] { G(Keg, 0f, 0.8f), G(Keg, -3.2f, 5f), G(Keg, 3.2f, 5f) },
            },
            new()
            {
                Id = 4, Name = "spike_hall",
                Obstacles = new[] { O(P, -11f, 1f), O(P, 11f, 1f) },
                Gimmicks = new[]
                {
                    G(Spike, -6f, 1f, 0f), G(Spike, -3f, 1f, 0.25f), G(Spike, 0f, 1f, 0.5f), G(Spike, 3f, 1f, 0.75f), G(Spike, 6f, 1f, 0f),
                    G(Spike, -8f, -3.5f, 0.5f), G(Spike, 8f, -3.5f, 0.5f), G(Spike, -8f, 5f, 0.25f), G(Spike, 8f, 5f, 0.75f),
                },
            },
            new()
            {
                Id = 5, Name = "flame_gallery",
                Obstacles = new[] { O(ObstacleKind.Statue, 0f, 0.6f), O(P, -6f, -5f), O(P, 6f, -5f) },
                Gimmicks = new[]
                {
                    G(Flame, -13.7f, 3.6f, 0f, 1f, 0f), G(Flame, 13.7f, 3.6f, 0.5f, -1f, 0f),
                    G(Flame, -13.7f, -1.6f, 0.5f, 1f, 0f), G(Flame, 13.7f, -1.6f, 0f, -1f, 0f),
                },
            },
            new()
            {
                Id = 6, Name = "powder_store",
                Obstacles = new[] { O(ObstacleKind.Crates, -9f, 4.5f), O(ObstacleKind.Crates, 9.5f, -3f), O(ObstacleKind.Crates, 4f, 3.2f), O(ObstacleKind.Crates, -5f, -1.5f) },
                Gimmicks = new[]
                {
                    G(Keg, -7.6f, 4.3f), G(Keg, -10.4f, 3.6f), G(Keg, 8.2f, -2.6f), G(Keg, 5.3f, 3.4f),
                    G(Keg, -3.7f, -1.2f), G(Keg, 0f, 2.4f), G(Keg, 11f, 4.5f), G(Keg, -11f, -5f),
                },
            },
            new()
            {
                Id = 7, Name = "shrine",
                Obstacles = new[] { O(P, 8f, 3f), O(P, 8f, -3f), O(ObstacleKind.Statue, -11.5f, 5.2f) },
                Gimmicks = new[]
                {
                    G(Font, -8.5f, 3.5f), G(Spike, -8.5f, 0.8f, 0f), G(Spike, -5.8f, 3.5f, 0.5f),
                    G(Keg, 11.5f, 0f), G(Keg, 4f, -6f),
                },
            },
            new()
            {
                Id = 8, Name = "chapel_nave",
                Obstacles = new[]
                {
                    O(ObstacleKind.Altar, 0f, 1.6f),
                    O(ObstacleKind.Pew, -8.5f, 3.5f), O(ObstacleKind.Pew, 8.5f, 3.5f),
                    O(ObstacleKind.Pew, -8.5f, -1f), O(ObstacleKind.Pew, 8.5f, -1f),
                },
                Gimmicks = new[] { G(Flame, -13.7f, 1.3f, 0f, 1f, 0f), G(Flame, 13.7f, 1.3f, 0.5f, -1f, 0f), G(Keg, -12f, -5.5f), G(Keg, 12f, -5.5f) },
            },
            new()
            {
                Id = 9, Name = "crystal_rift",
                Obstacles = new[] { O(ObstacleKind.Crystal, -5f, 3f), O(ObstacleKind.Crystal, 5.5f, 2.5f), O(ObstacleKind.Crystal, -9.5f, -2f), O(ObstacleKind.Crystal, 10f, -3.5f), O(ObstacleKind.Crystal, 0f, -0.5f) },
                Gimmicks = new[] { G(Spike, -2.5f, 3f, 0f), G(Spike, 2.5f, 3f, 0.5f), G(Spike, -6f, -4.5f, 0.25f), G(Spike, 6f, -1f, 0.75f), G(Font, 12f, 5f) },
            },
            new()
            {
                Id = 10, Name = "throne_room", Boss = true,
                Obstacles = new[] { O(P, -9f, 3.5f), O(P, 9f, 3.5f), O(P, -9f, -3.5f), O(P, 9f, -3.5f) },
                Gimmicks = new[] { G(Keg, -12.5f, 0f), G(Keg, 12.5f, 0f), G(Font, 12f, -6.2f), G(Font, -12f, -6.2f) },
            },
            new()
            {
                // A breather: a campfire in the middle, chests along the walls, no traps.
                Id = 11, Name = "rest",
                Obstacles = new[] { O(ObstacleKind.Campfire, 0f, 0f), O(ObstacleKind.Crates, -12f, 5.8f), O(ObstacleKind.Crates, 12f, -6f) },
                Gimmicks = new[] { G(Chest, -11.5f, 1.2f), G(Chest, 11.5f, -1.8f), G(Chest, -6.5f, -6.2f) },
            },
        };

        public static RoomLayout Get(int id) => id >= 0 && id < Layouts.Length ? Layouts[id] : Layouts[0];

        /// <summary>Picks the layout for a room: the first room is open, boss rooms use the throne room, others vary.</summary>
        public static int Pick(GameRandom rng, int roomIndex, int previous)
        {
            if (roomIndex == 0) return 0;
            if (roomIndex % RunSimulation.RoomsPerArea == RunSimulation.RoomsPerArea - 1) return 10;
            int area = roomIndex / RunSimulation.RoomsPerArea;
            // Some rooms suit some areas better (pews in the chapel, crystals in the abyss).
            var pool = area switch
            {
                0 => new[] { 1, 2, 3, 4, 5, 6, 7 },
                1 => new[] { 1, 2, 4, 5, 6, 7, 8, 8 },
                _ => new[] { 2, 4, 5, 6, 7, 9, 9, 3 },
            };
            int pick;
            int guard = 0;
            do pick = pool[rng.Range(0, pool.Length)];
            while (pick == previous && ++guard < 10);
            return pick;
        }

        // ------------------------------------------------------------------ Collision

        /// <summary>Moves a circle out of any obstacle it overlaps.</summary>
        public static Vector2 Resolve(RoomLayout layout, Vector2 p, float radius)
        {
            if (layout == null) return p;
            foreach (var o in layout.Obstacles) p = PushOut(p, radius, o.Box);
            return p;
        }

        public static bool Overlaps(RoomLayout layout, Vector2 p, float radius)
        {
            if (layout == null) return false;
            foreach (var o in layout.Obstacles)
            {
                var b = o.Box;
                float cx = Mathf.Clamp(p.x, b.xMin, b.xMax), cy = Mathf.Clamp(p.y, b.yMin, b.yMax);
                if ((p - new Vector2(cx, cy)).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        /// <summary>True if a projectile at this point hits a tall obstacle.</summary>
        public static bool BlocksShot(RoomLayout layout, Vector2 p)
        {
            if (layout == null) return false;
            foreach (var o in layout.Obstacles)
            {
                if (!o.BlocksProjectiles) continue;
                var b = o.Box;
                // Tall things block a bit above their footprint too (shots fly at chest height).
                if (p.x >= b.xMin && p.x <= b.xMax && p.y >= b.yMin && p.y <= b.yMax + 0.6f) return true;
            }
            return false;
        }

        /// <summary>Bends a movement direction around an obstacle ahead so walkers slide past instead of sticking.</summary>
        public static Vector2 Steer(RoomLayout layout, Vector2 pos, Vector2 dir, float radius, int salt)
        {
            if (layout == null || layout.Obstacles.Length == 0 || dir.sqrMagnitude < 0.0001f) return dir;
            float speed = dir.magnitude;
            Vector2 n = dir / speed;
            Vector2 probe = pos + n * (radius + 0.8f);
            foreach (var o in layout.Obstacles)
            {
                var b = o.Box;
                var grown = new Rect(b.xMin - radius, b.yMin - radius, b.width + radius * 2f, b.height + radius * 2f);
                if (!grown.Contains(probe) && !grown.Contains(pos + n * 0.35f)) continue;
                Vector2 perp = new(-n.y, n.x);
                float side = Vector2.Dot(b.center - pos, perp);
                Vector2 slide = Mathf.Abs(side) < 0.05f ? ((salt & 1) == 0 ? perp : -perp) : side > 0f ? -perp : perp;
                return (n * 0.2f + slide).normalized * speed;
            }
            return dir;
        }

        public static Vector2 PushOut(Vector2 p, float r, Rect box)
        {
            float cx = Mathf.Clamp(p.x, box.xMin, box.xMax), cy = Mathf.Clamp(p.y, box.yMin, box.yMax);
            Vector2 closest = new(cx, cy);
            Vector2 d = p - closest;
            float sq = d.sqrMagnitude;
            if (sq >= r * r) return p;
            if (sq > 1e-8f) return closest + d / Mathf.Sqrt(sq) * r;

            // Centre inside the box: leave by the nearest side.
            float left = p.x - box.xMin, right = box.xMax - p.x, down = p.y - box.yMin, up = box.yMax - p.y;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
            if (m == left) return new Vector2(box.xMin - r, p.y);
            if (m == right) return new Vector2(box.xMax + r, p.y);
            if (m == down) return new Vector2(p.x, box.yMin - r);
            return new Vector2(p.x, box.yMax + r);
        }

        /// <summary>Danger zone of a trap (for spawn checks and bots).</summary>
        public static bool InGimmick(in GimmickDef g, Vector2 p, float margin)
        {
            switch (g.Kind)
            {
                case GimmickKind.SpikeTrap:
                    return Mathf.Abs(p.x - g.Position.x) <= SpikeSize * 0.5f + margin && Mathf.Abs(p.y - g.Position.y) <= SpikeSize * 0.5f + margin;
                case GimmickKind.FlameJet:
                {
                    Vector2 to = p - g.Position;
                    float along = Vector2.Dot(to, g.Direction);
                    float across = Mathf.Abs(g.Direction.x * to.y - g.Direction.y * to.x);
                    return along >= -margin && along <= FlameLength + margin && across <= FlameWidth * 0.5f + margin;
                }
                default:
                    return (p - g.Position).sqrMagnitude <= (1f + margin) * (1f + margin);
            }
        }
    }
}
