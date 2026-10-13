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
        Rest, // no fighting: a campfire that heals, treasure chests, doors open from the start
        Quiet, // no fighting: the start room and the pedestal rooms
        Plates,  // puzzle: light every pressure plate at once (monsters keep coming until then)
        Crystal, // puzzle: keep the ward crystal whole until the time is up
        Thief,    // puzzle: catch the treasure imp before it gets away
        Gauntlet, // puzzle: through the traps to the plate in the middle
    }

    /// <summary>What a door promises: the team receives it on walking through (Hades-style room choice).</summary>
    public enum RoomReward : byte
    {
        None,
        Boon,     // an extra perk choice for everyone
        Upgrade,  // raise one perk you already have by a level
        Vigor,    // max HP up and a full heal
        Shards,   // bonus soul shards at the end of the run
        Treasure, // a piece of gear (rare or better) for everyone
        Cursed,   // two perk picks for everyone, and a curse on the team for the rest of the run
        Shop,     // a merchant: each player may buy one thing with the run's soul shards
    }

    /// <summary>Lasting penalties taken through curse doors (team-wide, they stack).</summary>
    public enum CurseKind : byte
    {
        Frailty,  // max HP down
        Weariness, // dodge recovers slower
        Thirst,   // healing weaker
        Hunted,   // damage taken up
    }

    /// <summary>One of the exits that open when a room is cleared. A trial door doubles the reward but toughens the room behind it.</summary>
    public struct Door
    {
        public RoomReward Reward;
        public bool Trial;
        /// <summary>For a curse door: what it lays on the team.</summary>
        public CurseKind Curse;

        public Door(RoomReward reward, bool trial, CurseKind curse = default)
        {
            Reward = reward;
            Trial = trial;
            Curse = curse;
        }

        public const int MaxCount = 3;

        /// <summary>Count in the low 2 bits, then 4 bits per door (3 reward + 1 trial), then the curse (+1) of the curse door.</summary>
        public static int Pack(IReadOnlyList<Door> doors)
        {
            int count = Mathf.Min(doors.Count, MaxCount);
            int packed = count;
            for (int i = 0; i < count; i++)
            {
                packed |= ((int)doors[i].Reward & 0x7 | (doors[i].Trial ? 0x8 : 0)) << (2 + i * 4);
                if (doors[i].Reward == RoomReward.Cursed) packed |= ((int)doors[i].Curse + 1) << 14;
            }
            return packed;
        }

        public static Door[] Unpack(int packed)
        {
            int count = Mathf.Min(packed & 0x3, MaxCount);
            var curse = (CurseKind)Mathf.Max(0, ((packed >> 14) & 0x7) - 1);
            var doors = new Door[count];
            for (int i = 0; i < count; i++)
            {
                int v = (packed >> (2 + i * 4)) & 0xF;
                var reward = (RoomReward)(v & 0x7);
                doors[i] = new Door(reward, (v & 0x8) != 0, reward == RoomReward.Cursed ? curse : default);
            }
            return doors;
        }
    }

    /// <summary>Why a perk offer appeared (sent with the offer so the card screen can say so).</summary>
    public enum ChoiceKind : byte
    {
        LevelUp,
        Start,
        Boon,
        Upgrade,
        Shop,     // the merchant: priced cards plus "buy nothing"
        Blessing, // a god's blessing after a champion or a lord (GodCatalog)
    }

    /// <summary>One room of the area map at run time. Several can be fought at once while the team explores apart.</summary>
    public sealed class RoomState
    {
        public int Id;
        public RoomKind Kind;
        /// <summary>Strength tier (area * 4 + 0..3): monster health and damage, item level.</summary>
        public int Index;
        public RoomObjective Objective;
        public RoomLayout Layout;
        public Rect Bounds;
        public int Wave;
        public int WaveCount;
        public float Target;     // survive: seconds; waves: number of waves
        public float Progress;
        public bool Cleared;
        /// <summary>A fight is on (someone walked in; monsters keep coming until it is cleared).</summary>
        public bool Active;
        /// <summary>Someone has been inside (the map shows what it is).</summary>
        public bool Visited;
        public float WaveTimer;
        public float SpawnTimer;
        public int LastWaveSize;
        /// <summary>A sealed room (treasure, secret) has been opened.</summary>
        public bool Unlocked;
        /// <summary>Players who used this room's pedestal (each may once).</summary>
        public readonly HashSet<ulong> UsedBy = new();

        public int Area => Index / RunSimulation.RoomsPerArea;
        public Vector2 Center => Bounds.center;
        public bool IsFight => Objective is RoomObjective.Waves or RoomObjective.Survive or RoomObjective.Boss or RoomObjective.Plates or RoomObjective.Crystal or RoomObjective.Thief or RoomObjective.Gauntlet;
        /// <summary>Picks and gear swaps wait for this: a fight going on here.</summary>
        public bool Fighting => Active && !Cleared;
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
        Teamwork = 32,    // landed on an enemy a teammate froze, stunned or marked
        ArmorBreak = 64,  // an armoured enemy hit while its guard was broken
    }

    /// <summary>EnemyHit's Int: the hit flags in the low byte, the attacker's client id + 1 above them (0 = traps, enemies).</summary>
    public static class HitInfo
    {
        public static int Pack(HitFlags flags, SimPlayer attacker) =>
            (int)flags | (attacker != null ? (int)((attacker.ClientId + 1) & 0xFFFFFF) << 8 : 0);

        public static HitFlags Flags(int packed) => (HitFlags)(packed & 0xFF);

        /// <summary>False for hits nobody dealt (traps, exploding monsters).</summary>
        public static bool TryAttacker(int packed, out ulong clientId)
        {
            int raw = (packed >> 8) & 0xFFFFFF;
            clientId = raw > 0 ? (ulong)(raw - 1) : 0;
            return raw > 0;
        }
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
        Flask,       // alchemist basic: bursts where it lands or hits
        FireFlask,   // thrown to a spot (no collision), becomes a fire pool
        HealFlask,   // thrown to a spot (no collision), heals there
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
        // New classes. Size.x carries how long a lasting zone stays (seconds).
        Sanctuary,   // guardian: holy ground
        Smoke,       // assassin: smoke bomb
        ShadowStep,  // assassin: arrival slash
        FirePool,    // alchemist: burning ground
        Potion,      // alchemist: healing splash
        FlaskBurst,  // alchemist: basic flask bursting
        // Hostile
        SoulBurst,    // a vengeful dead monster / the Lich's soul
        BloodTorrent, // the Blood Bishop's ritual completed
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
        public bool Ricochet;
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
        /// <summary>Map room it appears in.</summary>
        public int Room;
        public EnemyType Type;
        public bool Champion;
        public Vector2 Position;
        public float Timer;
        public bool Elite;
    }

    public enum SimEventType : byte
    {
        PlayerAttack,    // ClientId, Int = AttackAnim, Pos, Vec = aim, Value = reach
        ProjectileSpawn, // Id, Int = ProjectileKind, Pos, Vec = velocity, Value = lifetime
        ProjectileEnd,   // Id, Pos
        EnemyHit,        // Id, Pos, Value = damage, Int = HitInfo (HitFlags + attacker)
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
        RoomStarted,     // a new area: Int = area * RoomsPerArea, Id = the room the team arrives in, Seed = map seed
        RoomCleared,     // Int = room index, Id = map room id
        RunEnded,        // Int = 1 victory / 0 defeat
        BossSpawned,     // Id, Pos
        PerkOffer,       // ClientId, Seed = packed PerkOptions, Int = ChoiceKind, Value = picks still waiting after this one
        WaveStarted,     // Int = wave number (1-based), Value = wave count, Id = map room id
        HazardWarn,      // Id, Int = GimmickKind, Pos, Vec = direction, Size, Value = seconds until it fires
        HazardFire,      // Id, Int = GimmickKind, Pos, Vec = direction, Size, Value = seconds it stays active
        RewardGranted,   // Int = RoomReward, Value = amount (shards / max HP %), Id = 1 when doubled by a trial
        PlayerJoined,    // ClientId, Pos (joined a run in progress)
        PlayerRejoined,  // ClientId, Pos (came back after dropping out)
        PlayerLeft,      // ClientId, Pos (dropped out; their character is kept for a while)
        TeamLevelUp,     // Int = new team level (the pick is banked until the room is cleared)
        LootDropped,     // Id, ClientId = owner, Pos, Seed = packed item, Int = Rarity (only the owner sees it)
        LootPicked,      // Id (0 = straight into the bag), ClientId, Int = Rarity (-1 = lost: bag full on leaving)
        GearSlot,        // ClientId, Int = slot (0..3 equipped, 4.. bag), Seed = packed item or 0 when empty
        ItemGiven,       // ClientId = giver, Id = receiver client, Int = Rarity
        ItemSalvaged,    // ClientId, Value = soul shards
        LegendaryProc,   // ClientId, Int = LegendaryId (None = a plain chain bolt), Pos, Vec = chain target (zero = none)
        Combo,           // ClientId = who set it off, Int = ComboKind, Pos, Value = radius
        TrialEvent,      // Int = TrialMoment, Pos, Value (seconds), ClientId (cages), Id (collapse stage)
        RoomEntered,     // ClientId = first one in, Id = map room id, Int = room index (the room is now on everyone's map)
        PedestalUsed,    // ClientId, Id = map room id, Int = RoomKind, Value = amount (curse kind for the altar)
        DoorOpened,      // Int = 0 the boss door, 1 the way down to the next area, 2 a treasure door unlocked, 3 a secret room found; Pos, Id = room
        KeyGained,       // Pos, Int = keys the team holds now, Id = room it came from
        PuzzleEnded,     // Id = room, Int = 1 solved / 0 failed, Pos
        BellRung,        // ClientId = who rang it, Pos = the bell, Int = players called to the campfire
        GodSign,         // ClientId, Int = GodSignKind, Id = GodId, Pos, Vec (blessings at work: lightning, a favour awakening...)
    }

    /// <summary>Two classes' abilities meeting (see RunSimulation.Combos).</summary>
    public enum ComboKind : byte
    {
        SteamBurst,      // frost nova on burning ground
        ShadowExecution, // shadow step onto a marked enemy
        HolyWarCry,      // war cry inside holy ground
        FlameCharge,     // shield charge through burning ground
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
