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
        Bookshelf,  // library: a tall case of books (cover)
    }

    /// <summary>Room gimmicks. Traps hurt players and monsters alike, so luring enemies into them pays off.</summary>
    public enum GimmickKind : byte
    {
        SpikeTrap,   // floor spikes on a cycle
        FlameJet,    // a vent in the wall breathes fire along a line
        HealingFont, // stand on it to heal everyone nearby (once per room)
        PowderKeg,   // a breakable barrel (spawned as a static "enemy"), explodes when hit
        TreasureChest, // rest rooms: a breakable chest (static "enemy") full of souls
        // The new lands (Direction carries a zone's size)
        Water,       // sunken: shallow water slows everyone wading through it
        Ice,         // frost: a frozen sheet, players slide on it
        Icicle,      // frost: icicles fall from the ceiling on a cycle (a shadow grows first)
        LavaCrack,   // forge: a crack in the floor that erupts along a line
        SporeVent,   // grotto: a vent in the floor that puffs a lingering cloud of spores
        Glyph,       // library: a written page on the floor that bursts a moment after someone steps on it
    }

    public struct Obstacle
    {
        public ObstacleKind Kind;
        public Vector2 Position; // base centre (feet line) — sprites are anchored here
        public Vector2 Size;     // collision footprint

        public Rect Box => new(Position.x - Size.x * 0.5f, Position.y - Size.y * 0.5f, Size.x, Size.y);
        public bool BlocksProjectiles => Kind is ObstacleKind.Pillar or ObstacleKind.Statue or ObstacleKind.Crystal or ObstacleKind.Bookshelf;
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
        /// <summary>Where the room's centre sits in the world (layouts are authored around zero).</summary>
        public Vector2 Origin;

        /// <summary>A copy moved to a room's place on the area map, so collision works in world coordinates.</summary>
        public RoomLayout Placed(Vector2 origin)
        {
            var copy = new RoomLayout { Id = Id, Name = Name, Boss = Boss, Origin = origin };
            copy.Obstacles = (Obstacle[])Obstacles.Clone();
            for (int i = 0; i < copy.Obstacles.Length; i++) copy.Obstacles[i].Position += origin;
            copy.Gimmicks = (GimmickDef[])Gimmicks.Clone();
            for (int i = 0; i < copy.Gimmicks.Length; i++) copy.Gimmicks[i].Position += origin;
            return copy;
        }
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

        public const float WaterSlow = 0.7f;          // players wading
        public const float WaterSlowMonsters = 0.65f; // walkers wading

        public const float IcicleRadius = 1.2f;
        public const float IciclePeriod = 5.2f;
        public const float IcicleWarn = 1.1f;
        public const float IcicleActive = 0.25f;
        public const float IcicleDamage = 18f;

        public const float LavaLength = 6.5f;
        public const float LavaWidth = 1.2f;
        public const float LavaPeriod = 6f;
        public const float LavaWarn = 1.2f;
        public const float LavaActive = 1.2f;
        public const float LavaTick = 0.35f;
        public const float LavaDamage = 9f;

        public const float SporeRadius = 1.9f;
        public const float SporePeriod = 6f;
        public const float SporeWarn = 1.1f;
        public const float SporeActive = 2.4f;
        public const float SporeTick = 0.45f;
        public const float SporeDamage = 6f;

        public const float GlyphTrigger = 0.9f;
        public const float GlyphRadius = 1.7f;
        public const float GlyphWarn = 0.7f;
        public const float GlyphRearm = 4f;
        public const float GlyphDamage = 20f;

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
                ObstacleKind.Bookshelf => new Vector2(2.6f, 0.8f),
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
                    O(ObstacleKind.Statue, -11.5f, 4.2f), O(ObstacleKind.Statue, 11.5f, 4.2f),
                },
                Gimmicks = new[] { G(Keg, 0f, 0.8f), G(Keg, -3.2f, 5f), G(Keg, 3.2f, 5f) },
            },
            new()
            {
                Id = 4, Name = "spike_hall",
                Obstacles = new[] { O(P, -11f, 3.2f), O(P, 11f, 3.2f) },
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
                    G(Flame, -13.7f, -3f, 0.5f, 1f, 0f), G(Flame, 13.7f, -3f, 0f, -1f, 0f),
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
                    G(Keg, 11.5f, -3f), G(Keg, 4f, -6f),
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
                Gimmicks = new[] { G(Flame, -13.7f, 2.8f, 0f, 1f, 0f), G(Flame, 13.7f, 2.8f, 0.5f, -1f, 0f), G(Keg, -12f, -5.5f), G(Keg, 12f, -5.5f) },
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
                Gimmicks = new[] { G(Keg, -12.5f, 2.6f), G(Keg, 12.5f, 2.6f), G(Font, 12f, -6.2f), G(Font, -12f, -6.2f) },
            },
            new()
            {
                // A breather: a campfire in the middle, chests along the walls, no traps.
                Id = 11, Name = "rest",
                Obstacles = new[] { O(ObstacleKind.Campfire, 0f, 0f), O(ObstacleKind.Crates, -12f, 5.8f), O(ObstacleKind.Crates, 12f, -6f) },
                Gimmicks = new[] { G(Chest, -11.5f, 1.2f), G(Chest, 11.5f, -1.8f), G(Chest, -6.5f, -6.2f) },
            },
            new()
            {
                // Pedestal rooms (shrine, spring, merchant...): the pedestal in the middle, statues keeping watch.
                Id = 12, Name = "sanctum",
                Obstacles = new[] { O(ObstacleKind.Statue, -7f, 3.6f), O(ObstacleKind.Statue, 7f, 3.6f), O(P, -7f, -3.6f), O(P, 7f, -3.6f) },
            },
            new()
            {
                // Pressure plate puzzles: the plates (RunSimulation.PlateSpots) lie between four pillars.
                Id = 13, Name = "plate_hall",
                Obstacles = new[] { O(P, -4f, 0.5f), O(P, 4f, 0.5f), O(P, -11f, 4.5f), O(P, 11f, 4.5f) },
            },
            new()
            {
                // Gauntlet puzzles: a ring of spikes around the plate in the middle, fire breathing across the room.
                Id = 14, Name = "gauntlet",
                Obstacles = new[] { O(P, -6.5f, 4.6f), O(P, 6.5f, 4.6f), O(P, -6.5f, -3.4f), O(P, 6.5f, -3.4f) },
                Gimmicks = new[]
                {
                    G(Spike, -2.2f, 0.5f, 0f), G(Spike, 2.2f, 0.5f, 0.5f), G(Spike, 0f, 2.6f, 0.25f), G(Spike, 0f, -1.6f, 0.75f),
                    G(Spike, -2.2f, 2.6f, 0.6f), G(Spike, 2.2f, 2.6f, 0.1f), G(Spike, -2.2f, -1.6f, 0.35f), G(Spike, 2.2f, -1.6f, 0.85f),
                    G(Spike, -6f, 0.5f, 0.4f), G(Spike, 6f, 0.5f, 0.9f), G(Spike, -9.5f, 0.5f, 0.15f), G(Spike, 9.5f, 0.5f, 0.65f),
                    G(Flame, -13.7f, 3.4f, 0f, 1f, 0f), G(Flame, 13.7f, 3.4f, 0.5f, -1f, 0f),
                    G(Flame, -13.7f, -2.6f, 0.5f, 1f, 0f), G(Flame, 13.7f, -2.6f, 0f, -1f, 0f),
                },
            },

            // ---------------------------------------------------------------- Sunken catacombs (water slows the wader)
            new()
            {
                // A channel of water across the hall: cross it, or fight around its ends.
                Id = 15, Name = "flooded_hall",
                Obstacles = new[] { O(P, -7f, 4.2f), O(P, 7f, 4.2f), O(P, -7f, -4.2f), O(P, 7f, -4.2f) },
                Gimmicks = new[] { Z(Pool, 0f, 0.2f, 22f, 3.2f), G(Font, 12f, 5f) },
            },
            new()
            {
                Id = 16, Name = "sunken_tombs",
                Obstacles = new[]
                {
                    O(ObstacleKind.Sarcophagus, -8f, 2.8f), O(ObstacleKind.Sarcophagus, 8f, -0.8f),
                    O(ObstacleKind.Statue, -11.5f, 4.6f), O(ObstacleKind.Statue, 11.5f, 4.6f),
                },
                Gimmicks = new[] { Z(Pool, -8f, -0.6f, 9f, 6f), Z(Pool, 8f, 2.2f, 9f, 5.5f), G(Spike, 0f, 3.2f, 0f), G(Spike, 0f, -1.2f, 0.5f) },
            },
            new()
            {
                // A sunken pool in the middle ringed by pillars; spikes keep the dry edges busy.
                Id = 17, Name = "drowned_crossing",
                Obstacles = new[] { O(P, -6.5f, 4f), O(P, 6.5f, 4f), O(P, -6.5f, -3.4f), O(P, 6.5f, -3.4f) },
                Gimmicks = new[] { Z(Pool, 0f, 0.4f, 10f, 6.4f), G(Spike, -11f, -5f, 0f), G(Spike, 11f, -5f, 0.5f), G(Spike, -11f, 4f, 0.25f), G(Spike, 11f, 4f, 0.75f) },
            },

            // ---------------------------------------------------------------- Frozen mausoleum (ice sheets, icicles)
            new()
            {
                Id = 18, Name = "ice_hall",
                Obstacles = new[] { O(P, -11f, 4.2f), O(P, 11f, 4.2f), O(P, 0f, 4.6f) },
                Gimmicks = new[]
                {
                    Z(Frozen, -7f, 0f, 8f, 10f), Z(Frozen, 7f, 0f, 8f, 10f),
                    G(Drip, 0f, 1.5f, 0f), G(Drip, -4f, -3.5f, 0.33f), G(Drip, 4f, -3.5f, 0.66f),
                },
            },
            new()
            {
                Id = 19, Name = "frozen_court",
                Obstacles = new[] { O(ObstacleKind.Statue, -9f, 4.2f), O(ObstacleKind.Statue, 9f, 4.2f), O(ObstacleKind.Statue, -9f, -3.6f), O(ObstacleKind.Statue, 9f, -3.6f) },
                Gimmicks = new[]
                {
                    G(Drip, -4f, 2.4f, 0f), G(Drip, 4f, 2.4f, 0.5f), G(Drip, 0f, -0.4f, 0.25f), G(Drip, -4f, -3.2f, 0.75f), G(Drip, 4f, -3.2f, 0.1f),
                    Z(Frozen, 0f, 5f, 8f, 2.6f),
                },
            },
            new()
            {
                // A frozen lake with blocks of ice standing in it.
                Id = 20, Name = "glacier",
                Obstacles = new[] { O(ObstacleKind.Crystal, -3.5f, 2.2f), O(ObstacleKind.Crystal, 3.5f, -1.6f) },
                Gimmicks = new[]
                {
                    Z(Frozen, 0f, 0.4f, 17f, 9f),
                    G(Drip, -10.5f, 4.2f, 0f), G(Drip, 10.5f, 4.2f, 0.5f), G(Drip, -10.5f, -4f, 0.25f), G(Drip, 10.5f, -4f, 0.75f),
                },
            },

            // ---------------------------------------------------------------- Burnt forge (lava cracks erupt along lines)
            new()
            {
                Id = 21, Name = "foundry",
                Obstacles = new[] { O(ObstacleKind.Crates, -4f, 5.2f), O(ObstacleKind.Crates, 4.5f, 5.2f), O(P, -10f, 0.5f), O(P, 10f, 0.5f) },
                Gimmicks = new[]
                {
                    G(Lava, -12.5f, 3.4f, 0f, 1f, 0f), G(Lava, 12.5f, 3.4f, 0.5f, -1f, 0f),
                    G(Lava, -12.5f, -3.6f, 0.5f, 1f, 0f), G(Lava, 12.5f, -3.6f, 0f, -1f, 0f),
                    G(Keg, -6f, 0.4f), G(Keg, 6f, 0.4f),
                },
            },
            new()
            {
                // The great furnace in the middle, cracks running up from the floor on both sides.
                Id = 22, Name = "furnace_hall",
                Obstacles = new[] { O(ObstacleKind.Altar, 0f, 2.4f), O(ObstacleKind.Crates, -11.5f, -5.5f), O(ObstacleKind.Crates, 11.5f, -5.5f) },
                Gimmicks = new[]
                {
                    G(Lava, -7.5f, -6.8f, 0f, 0f, 1f), G(Lava, 7.5f, -6.8f, 0.5f, 0f, 1f),
                    G(Keg, -11.5f, 5f), G(Keg, 11.5f, 5f), G(Flame, -13.7f, 3f, 0.25f, 1f, 0f), G(Flame, 13.7f, 3f, 0.75f, -1f, 0f),
                },
            },
            new()
            {
                Id = 23, Name = "smelter",
                Obstacles = new[] { O(P, -10f, 4.2f), O(P, 10f, 4.2f), O(P, -10f, -4f), O(P, 10f, -4f) },
                Gimmicks = new[]
                {
                    G(Lava, -6.5f, 0.6f, 0f, 1f, 0f), G(Lava, 6.5f, 0.6f, 0.5f, -1f, 0f),
                    G(Lava, -3f, 6.8f, 0.25f, 0f, -1f),
                    G(Keg, -12f, 0.6f), G(Keg, 12f, 0.6f), G(Font, 0f, -5.6f),
                },
            },

            // ---------------------------------------------------------------- Fungal grotto (spore vents puff lingering clouds)
            new()
            {
                Id = 24, Name = "spore_hollow",
                Obstacles = new[] { O(P, -8f, 3.6f), O(P, 8f, 3.6f), O(P, -8f, -3.2f), O(P, 8f, -3.2f) },
                Gimmicks = new[] { G(Vent, 0f, 0.6f, 0f), G(Vent, -11.5f, 0.4f, 0.5f), G(Vent, 11.5f, 0.4f, 0.5f) },
            },
            new()
            {
                // Toadstools as tall as a man, and between them the vents and a damp hollow.
                Id = 25, Name = "root_tangle",
                Obstacles = new[] { O(ObstacleKind.Statue, -5f, 4.4f), O(ObstacleKind.Statue, 5f, 4.4f), O(P, -10.5f, -3.6f), O(P, 10.5f, -3.6f) },
                Gimmicks = new[] { G(Vent, -5f, -1.5f, 0f), G(Vent, 5f, -1.5f, 0.5f), Z(Pool, 0f, 1.6f, 6f, 2.6f) },
            },
            new()
            {
                Id = 26, Name = "fungal_pit",
                Obstacles = new[] { O(P, -11f, 4.2f), O(P, 11f, 4.2f), O(ObstacleKind.Statue, -3.5f, -0.4f), O(ObstacleKind.Statue, 3.5f, -0.4f) },
                Gimmicks = new[] { G(Vent, -7f, 2.6f, 0f), G(Vent, 7f, 2.6f, 0.33f), G(Vent, 0f, 0.4f, 0.66f), G(Font, -12f, -5f) },
            },

            // ---------------------------------------------------------------- Cursed library (bookshelves, pages that burst, ink)
            new()
            {
                Id = 27, Name = "reading_hall",
                Obstacles = new[] { O(Shelf, -8f, 3.8f), O(Shelf, 8f, 3.8f), O(Shelf, -8f, -3f), O(Shelf, 8f, -3f) },
                Gimmicks = new[] { G(Page, -3.5f, 0.6f), G(Page, 3.5f, 0.6f), G(Page, 0f, -1.8f) },
            },
            new()
            {
                // Stacks down both sides; the pages lie where readers would stand.
                Id = 28, Name = "archive_stacks",
                Obstacles = new[] { O(Shelf, -10.5f, 4.4f), O(Shelf, -10.5f, -2.4f), O(Shelf, 10.5f, 4.4f), O(Shelf, 10.5f, -2.4f), O(Shelf, -5f, -3.4f), O(Shelf, 5f, -3.4f) },
                Gimmicks = new[] { G(Page, -7f, -1.4f), G(Page, 7f, -1.4f), G(Page, 0f, 2.4f) },
            },
            new()
            {
                // A spilt lake of ink in the middle (it slows like water), pages around it.
                Id = 29, Name = "ink_gallery",
                Obstacles = new[] { O(Shelf, -11f, 4.4f), O(Shelf, 11f, 4.4f), O(P, -7f, -4f), O(P, 7f, -4f) },
                Gimmicks = new[] { Z(Pool, 0f, 0.6f, 12f, 4f), G(Page, -11f, -1f), G(Page, 11f, -1f), G(Font, 12f, -5.4f) },
            },
        };

        private const GimmickKind Vent = GimmickKind.SporeVent;
        private const GimmickKind Page = GimmickKind.Glyph;
        private const ObstacleKind Shelf = ObstacleKind.Bookshelf;

        private const GimmickKind Pool = GimmickKind.Water;
        private const GimmickKind Frozen = GimmickKind.Ice;
        private const GimmickKind Drip = GimmickKind.Icicle;
        private const GimmickKind Lava = GimmickKind.LavaCrack;

        /// <summary>A zone gimmick (water, ice): its centre and size.</summary>
        private static GimmickDef Z(GimmickKind kind, float x, float y, float w, float h) =>
            new() { Kind = kind, Position = new Vector2(x, y), Direction = new Vector2(w, h) };

        public static RoomLayout Get(int id) => id >= 0 && id < Layouts.Length ? Layouts[id] : Layouts[0];

        public const int SanctumLayout = 12;
        public const int PlatesLayout = 13;
        public const int GauntletLayout = 14;

        /// <summary>Picks the layout for a fight room (never the same twice in a row).</summary>
        public static int PickFight(GameRandom rng, Biome biome, int previous)
        {
            // Some rooms suit some lands better (pews in the chapel, crystals in the abyss).
            var pool = BiomeCatalog.Get(biome).FightLayouts;
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
                case GimmickKind.LavaCrack:
                {
                    Vector2 to = p - g.Position;
                    float along = Vector2.Dot(to, g.Direction);
                    float across = Mathf.Abs(g.Direction.x * to.y - g.Direction.y * to.x);
                    return along >= -margin && along <= LavaLength + margin && across <= LavaWidth * 0.5f + margin;
                }
                case GimmickKind.Icicle:
                    return (p - g.Position).sqrMagnitude <= (IcicleRadius + margin) * (IcicleRadius + margin);
                case GimmickKind.SporeVent:
                    return (p - g.Position).sqrMagnitude <= (SporeRadius + margin) * (SporeRadius + margin);
                case GimmickKind.Glyph:
                    return (p - g.Position).sqrMagnitude <= (GlyphRadius + margin) * (GlyphRadius + margin);
                case GimmickKind.Water:
                case GimmickKind.Ice:
                    return false; // not a danger: see InZone
                default:
                    return (p - g.Position).sqrMagnitude <= (1f + margin) * (1f + margin);
            }
        }

        /// <summary>Inside a zone gimmick (water, ice).</summary>
        public static bool InZone(in GimmickDef g, Vector2 p) =>
            g.Kind is GimmickKind.Water or GimmickKind.Ice
            && Mathf.Abs(p.x - g.Position.x) <= g.Direction.x * 0.5f && Mathf.Abs(p.y - g.Position.y) <= g.Direction.y * 0.5f;

        /// <summary>Standing in a zone of this kind somewhere in the room.</summary>
        public static bool InZone(RoomLayout layout, GimmickKind kind, Vector2 p)
        {
            if (layout == null) return false;
            foreach (var g in layout.Gimmicks)
                if (g.Kind == kind && InZone(g, p)) return true;
            return false;
        }
    }
}
