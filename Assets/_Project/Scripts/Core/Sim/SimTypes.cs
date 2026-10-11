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
        Waves,
        Survive,
        Boss,
    }

    public sealed class RoomState
    {
        public int Index;
        public RoomObjective Objective;
        public int Wave;
        public int WaveCount;
        public float Target;     // survive: seconds; waves: number of waves
        public float Progress;
        public bool Cleared;
        public float ExitHold;
        public Vector2 ExitPosition;

        public int Area => Index / RunSimulation.RoomsPerArea;
    }

    public enum StatusKind : byte
    {
        Frozen,
        Marked,
        Stunned,
        Burning,
        Poisoned,
    }

    [System.Flags]
    public enum HitFlags : byte
    {
        None = 0,
        Crit = 1,
        Shatter = 2,
        Heavy = 4,
        DamageOverTime = 8,
        Blocked = 16,     // armour soaked most of it
    }

    public enum ProjectileKind : byte
    {
        Bolt,
        Arrow,
        PierceArrow,
        SwordWave,
        EnemyArrow,
        BloodOrb,
        SoulOrb,
    }

    public enum AttackAnim : byte
    {
        BasicA,
        BasicB,
        Spin,
        Skill1,
        Skill2,
        Dodge,
    }

    public enum AreaKind : byte
    {
        FrostNova,
        Heal,
        WarCry,
        Mark,
        Shatter,
        Slam,
        Explosion,
        HolyLight,
    }

    /// <summary>Owner input sent to the host each network tick. Button presses are counters so a lost packet never loses a press.</summary>
    public struct PlayerInput
    {
        public Vector2 Aim;
        public bool Attack;
        public byte Skill1Count;
        public byte Skill2Count;
        public byte DodgeCount;
    }

    public sealed class Projectile
    {
        public int Id;
        public ProjectileKind Kind;
        public bool Hostile;
        public ulong Owner;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Damage;
        public int PierceLeft;
        public float Life;
        public HitFlags Flags;
        public bool Splits;
        public readonly List<int> HitIds = new(4);
    }

    public struct Soul
    {
        public int Id;
        public Vector2 Position;
        public int Value;
    }

    /// <summary>A delayed spawn: the warning rune shows first, then the enemy appears.</summary>
    public struct PendingSpawn
    {
        public EnemyType Type;
        public Vector2 Position;
        public float Timer;
        public bool Elite;
    }

    public enum SimEventType : byte
    {
        PlayerAttack,    // ClientId, Int = AttackAnim, Pos, Vec = aim, Value = reach
        ProjectileSpawn, // Id, Int = ProjectileKind, Pos, Vec = velocity, Value = lifetime
        ProjectileEnd,   // Id, Pos
        EnemyHit,        // Id, Pos, Value = damage, Int = HitFlags
        EnemyDied,       // Id, Pos, Int = EnemyType
        EnemyTelegraph,  // Id, Int = AttackShape, Pos, Vec = direction, Size, Value = duration
        EnemyStrike,     // Id, Int = AttackKind, Pos, Vec = direction
        SpawnWarning,    // Pos, Int = EnemyType, Value = delay
        StatusApplied,   // Id, Int = StatusKind, Value = duration
        AreaEffect,      // ClientId, Int = AreaKind, Pos, Value = radius
        PlayerHealed,    // ClientId, Pos, Value = amount
        SoulSpawn,       // Id, Pos, Int = value
        SoulCollected,   // Id, ClientId
        PlayerHurt,      // ClientId, Pos, Value = damage
        PlayerDowned,    // ClientId, Pos
        PlayerRevived,   // ClientId, Pos
        Teleport,        // ClientId, Pos
        RoomStarted,     // Int = room index, Id = layout id
        RoomCleared,     // Int = room index, Pos = exit
        RunEnded,        // Int = 1 victory / 0 defeat
        BossSpawned,     // Id, Pos
        PerkOffer,       // ClientId, Seed = packed PerkOptions
        WaveStarted,     // Int = wave number (1-based), Value = wave count
        HazardWarn,      // Id, Int = GimmickKind, Pos, Vec = direction, Size, Value = seconds until it fires
        HazardFire,      // Id, Int = GimmickKind, Pos, Vec = direction, Size, Value = seconds it stays active
    }

    /// <summary>Something that happened during a tick; the network layer sends these to clients for presentation.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int Id;
        public ulong ClientId;
        public Vector2 Pos;
        public Vector2 Vec;
        public Vector2 Size;
        public float Value;
        public int Int;
        public ulong Seed;

        public SimEvent(SimEventType type) : this() => Type = type;
    }
}
