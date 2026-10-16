using System.Collections.Generic;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay
{
    public static class NetMessageNames
    {
        public const string Events = "run.events";
        public const string Snapshot = "run.snapshot";
    }

    /// <summary>Compact binary form of <see cref="SimEvent"/>: a type byte, then only the fields that type uses.</summary>
    public static class EventCodec
    {
        [System.Flags]
        private enum F : byte
        {
            None = 0,
            Id = 1,
            Client = 2,
            Pos = 4,
            Vec = 8,
            Value = 16,
            Int = 32,
            Seed = 64,
            Size = 128,
        }

        private static readonly F[] Layout = BuildLayout();

        private static F[] BuildLayout()
        {
            var layout = new F[System.Enum.GetValues(typeof(SimEventType)).Length];
            void Set(SimEventType t, F f) => layout[(int)t] = f;
            Set(SimEventType.PlayerAttack, F.Client | F.Int | F.Pos | F.Vec | F.Value);
            Set(SimEventType.ProjectileSpawn, F.Id | F.Int | F.Pos | F.Vec | F.Value);
            Set(SimEventType.ProjectileEnd, F.Id | F.Pos);
            Set(SimEventType.EnemyHit, F.Id | F.Pos | F.Value | F.Int);
            Set(SimEventType.EnemyDied, F.Id | F.Pos | F.Int | F.Value); // Value 1 = crumbled with the room (no reward)
            Set(SimEventType.EnemyTelegraph, F.Id | F.Int | F.Pos | F.Vec | F.Size | F.Value);
            Set(SimEventType.EnemyStrike, F.Id | F.Int | F.Pos | F.Vec);
            Set(SimEventType.SpawnWarning, F.Pos | F.Int | F.Value);
            Set(SimEventType.StatusApplied, F.Id | F.Int | F.Value);
            Set(SimEventType.AreaEffect, F.Client | F.Int | F.Pos | F.Value | F.Size); // Size.x = how long a lasting zone stays
            Set(SimEventType.PlayerHealed, F.Client | F.Pos | F.Value);
            Set(SimEventType.SoulSpawn, F.Id | F.Pos | F.Int);
            Set(SimEventType.SoulCollected, F.Id | F.Client);
            Set(SimEventType.PlayerHurt, F.Client | F.Pos | F.Value);
            Set(SimEventType.PlayerDowned, F.Client | F.Pos);
            Set(SimEventType.PlayerRevived, F.Client | F.Pos);
            Set(SimEventType.Teleport, F.Client | F.Pos);
            Set(SimEventType.RoomStarted, F.Int | F.Id | F.Seed | F.Value); // a new area: Int = its first room index, Id = arrival room, Seed = map seed, Value = its land
            Set(SimEventType.RoomCleared, F.Int | F.Id | F.Value); // Value = RoomKind
            Set(SimEventType.RunEnded, F.Int);
            Set(SimEventType.BossSpawned, F.Id | F.Pos | F.Int);
            Set(SimEventType.PerkOffer, F.Client | F.Seed | F.Int | F.Value);
            Set(SimEventType.WaveStarted, F.Int | F.Value | F.Id);
            Set(SimEventType.HazardWarn, F.Id | F.Int | F.Pos | F.Vec | F.Size | F.Value);
            Set(SimEventType.HazardFire, F.Id | F.Int | F.Pos | F.Vec | F.Size | F.Value);
            Set(SimEventType.RewardGranted, F.Id | F.Int | F.Value);
            Set(SimEventType.PlayerJoined, F.Client | F.Pos);
            Set(SimEventType.PlayerRejoined, F.Client | F.Pos);
            Set(SimEventType.PlayerLeft, F.Client | F.Pos);
            Set(SimEventType.TeamLevelUp, F.Int);
            Set(SimEventType.LootDropped, F.Id | F.Client | F.Pos | F.Seed | F.Int);
            Set(SimEventType.LootPicked, F.Id | F.Client | F.Int);
            Set(SimEventType.GearSlot, F.Client | F.Int | F.Seed);
            Set(SimEventType.ItemGiven, F.Client | F.Id | F.Int);
            Set(SimEventType.ItemSalvaged, F.Client | F.Value);
            Set(SimEventType.LegendaryProc, F.Client | F.Int | F.Pos | F.Vec);
            Set(SimEventType.Combo, F.Client | F.Int | F.Pos | F.Value | F.Id | F.Vec | F.Size); // a union: Id = the pair, Vec/Size = the two players
            Set(SimEventType.TrialEvent, F.Int | F.Pos | F.Value | F.Client | F.Id);
            Set(SimEventType.RoomEntered, F.Client | F.Id | F.Int | F.Value); // Value = RoomKind
            Set(SimEventType.PedestalUsed, F.Client | F.Id | F.Int | F.Value | F.Pos);
            Set(SimEventType.DoorOpened, F.Int | F.Pos | F.Id);
            Set(SimEventType.KeyGained, F.Int | F.Pos | F.Id);
            Set(SimEventType.PuzzleEnded, F.Int | F.Pos | F.Id);
            Set(SimEventType.BellRung, F.Client | F.Pos | F.Int);
            Set(SimEventType.GodSign, F.Client | F.Int | F.Id | F.Pos | F.Vec | F.Value);
            Set(SimEventType.Lantern, F.Id | F.Int | F.Pos | F.Client);
            Set(SimEventType.RoomEvent, F.Int | F.Pos | F.Id);
            return layout;
        }

        public static void Write(FastBufferWriter w, in SimEvent e)
        {
            var f = Layout[(int)e.Type];
            w.WriteValueSafe((byte)e.Type);
            if ((f & F.Id) != 0) w.WriteValueSafe(e.Id);
            if ((f & F.Client) != 0) w.WriteValueSafe(e.ClientId);
            if ((f & F.Pos) != 0) w.WriteValueSafe(e.Pos);
            if ((f & F.Vec) != 0) w.WriteValueSafe(e.Vec);
            if ((f & F.Size) != 0) w.WriteValueSafe(e.Size);
            if ((f & F.Value) != 0) w.WriteValueSafe(e.Value);
            if ((f & F.Int) != 0) w.WriteValueSafe(e.Int);
            if ((f & F.Seed) != 0) w.WriteValueSafe(e.Seed);
        }

        public static SimEvent Read(FastBufferReader r)
        {
            r.ReadValueSafe(out byte type);
            var e = new SimEvent((SimEventType)type);
            var f = Layout[type];
            if ((f & F.Id) != 0) r.ReadValueSafe(out e.Id);
            if ((f & F.Client) != 0) r.ReadValueSafe(out e.ClientId);
            if ((f & F.Pos) != 0) r.ReadValueSafe(out e.Pos);
            if ((f & F.Vec) != 0) r.ReadValueSafe(out e.Vec);
            if ((f & F.Size) != 0) r.ReadValueSafe(out e.Size);
            if ((f & F.Value) != 0) r.ReadValueSafe(out e.Value);
            if ((f & F.Int) != 0) r.ReadValueSafe(out e.Int);
            if ((f & F.Seed) != 0) r.ReadValueSafe(out e.Seed);
            return e;
        }
    }

    /// <summary>What any peer needs to draw one enemy.</summary>
    public struct EnemyRenderState
    {
        public ushort Id;
        public EnemyType Type;
        public bool Elite;
        public Vector2 Position;
        public EnemyAnim Anim;
        public bool FacingLeft;
        public bool Marked;
        public bool Burning;
        public bool Poisoned;
        public bool Cursed;
        public float Health; // 0..1

        public static EnemyRenderState From(in Enemy e) => new()
        {
            Id = (ushort)e.Id,
            Type = e.Type,
            Elite = e.Elite,
            Position = e.Position,
            Anim = e.Anim,
            FacingLeft = e.Facing < 0f,
            Marked = e.Marked > 0f,
            Burning = e.Burning > 0f,
            Poisoned = e.Poisoned > 0f,
            Cursed = e.Cursed > 0f,
            Health = Mathf.Clamp01(e.Health / e.MaxHealth),
        };
    }

    /// <summary>
    /// Enemy states for clients, quantized to 9 bytes each and split into chunks that fit one
    /// unreliable packet. Deaths arrive separately as reliable events.
    /// </summary>
    public static class SnapshotCodec
    {
        public const int EnemiesPerChunk = 100;
        public const int BytesPerEnemy = 9;
        private const float PositionScale = 100f;

        public static void WriteChunk(FastBufferWriter w, EnemySimulation enemies, int start, int count)
        {
            w.WriteValueSafe((ushort)count);
            for (int i = start; i < start + count; i++)
            {
                var s = EnemyRenderState.From(enemies[i]);
                w.WriteValueSafe(s.Id);
                w.WriteValueSafe((byte)((byte)s.Type | (s.Elite ? 0x80 : 0)));
                w.WriteValueSafe((short)Mathf.RoundToInt(s.Position.x * PositionScale));
                w.WriteValueSafe((short)Mathf.RoundToInt(s.Position.y * PositionScale));
                byte flags = (byte)((byte)s.Anim & 0x07); // the anim needs 3 bits; 0x08 is the curse
                if (s.Cursed) flags |= 0x08;
                if (s.FacingLeft) flags |= 0x10;
                if (s.Marked) flags |= 0x20;
                if (s.Burning) flags |= 0x40;
                if (s.Poisoned) flags |= 0x80;
                w.WriteValueSafe(flags);
                w.WriteValueSafe((byte)Mathf.RoundToInt(s.Health * 255f));
            }
        }

        public static void ReadChunk(FastBufferReader r, List<EnemyRenderState> results)
        {
            results.Clear();
            r.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out ushort id);
                r.ReadValueSafe(out byte typeAndElite);
                r.ReadValueSafe(out short x);
                r.ReadValueSafe(out short y);
                r.ReadValueSafe(out byte flags);
                r.ReadValueSafe(out byte hp);
                results.Add(new EnemyRenderState
                {
                    Id = id,
                    Type = (EnemyType)(typeAndElite & 0x7F),
                    Elite = (typeAndElite & 0x80) != 0,
                    Position = new Vector2(x / PositionScale, y / PositionScale),
                    Anim = (EnemyAnim)(flags & 0x07),
                    Cursed = (flags & 0x08) != 0,
                    FacingLeft = (flags & 0x10) != 0,
                    Marked = (flags & 0x20) != 0,
                    Burning = (flags & 0x40) != 0,
                    Poisoned = (flags & 0x80) != 0,
                    Health = hp / 255f,
                });
            }
        }
    }
}
