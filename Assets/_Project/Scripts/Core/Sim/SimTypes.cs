using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public enum RunPhase : byte
    {
        Playing,
        Victory,
        Defeat,
    }

    public enum RoomObjective : byte
    {
        Survive,
        Kill,
        Boss,
    }

    public sealed class RoomState
    {
        public int Index;
        public RoomObjective Objective;
        public float Target;
        public float Progress;
        public bool Cleared;
        public float ExitHold;
        public Vector2 ExitPosition;

        public int Area => Index / RunSimulation.RoomsPerArea;
    }

    /// <summary>Enemy projectile.</summary>
    public struct HostileShot
    {
        public int Id;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Damage;
        public float Life;

        public HostileShot(Vector2 position, Vector2 velocity, float damage)
        {
            Id = 0;
            Position = position;
            Velocity = velocity;
            Damage = damage;
            Life = 4f;
        }
    }

    /// <summary>Player projectile (throwing axe). Remembers who it hit so piercing hits each enemy once.</summary>
    public sealed class PlayerProjectile
    {
        public int Id;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Damage;
        public float Radius;
        public int PierceLeft;
        public float Life;
        public readonly List<int> HitEnemyIds = new(8);
    }

    public struct XpGem
    {
        public int Id;
        public Vector2 Position;
        public int Value;
    }

    public enum SimEventType : byte
    {
        Slash,          // ClientId, Pos, Vec = direction, Value = radius
        AxeSpawn,       // Id, Pos, Vec = velocity, Value = lifetime
        AxeEnd,         // Id
        ShotSpawn,      // Id, Pos, Vec = velocity, Value = lifetime
        ShotEnd,        // Id
        EnemyHit,       // Id = enemy, Pos, Value = damage
        EnemyDied,      // Id = enemy, Pos, Int = EnemyType
        GemSpawn,       // Id, Pos, Int = xp value
        GemCollected,   // Id, ClientId = collector
        PlayerHurt,     // ClientId, Value = damage
        PlayerDowned,   // ClientId
        PlayerRevived,  // ClientId
        LootDrop,       // ClientId = receiver, Pos, Seed, Int = item level, Value = rarity boost
        LevelUpOffer,   // ClientId, Seed = packed UpgradeOptions
        Teleport,       // ClientId, Pos
        RoomStarted,    // Int = room index
        RoomCleared,    // Int = room index, Pos = exit
        RunEnded,       // Int = 1 victory / 0 defeat
        BossSpawned,    // Id, Pos
    }

    /// <summary>Something that happened during a tick; the network layer sends these to clients for presentation.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int Id;
        public ulong ClientId;
        public Vector2 Pos;
        public Vector2 Vec;
        public float Value;
        public int Int;
        public ulong Seed;

        public SimEvent(SimEventType type) : this() => Type = type;
    }
}
