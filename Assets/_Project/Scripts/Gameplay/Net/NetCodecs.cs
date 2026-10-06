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
        }

        private static readonly F[] Layout = BuildLayout();

        private static F[] BuildLayout()
        {
            var layout = new F[System.Enum.GetValues(typeof(SimEventType)).Length];
            void Set(SimEventType t, F f) => layout[(int)t] = f;
            Set(SimEventType.Slash, F.Client | F.Pos | F.Vec | F.Value);
            Set(SimEventType.AxeSpawn, F.Id | F.Pos | F.Vec | F.Value);
            Set(SimEventType.AxeEnd, F.Id);
            Set(SimEventType.ShotSpawn, F.Id | F.Pos | F.Vec | F.Value);
            Set(SimEventType.ShotEnd, F.Id);
            Set(SimEventType.EnemyHit, F.Id | F.Pos | F.Value);
            Set(SimEventType.EnemyDied, F.Id | F.Pos | F.Int);
            Set(SimEventType.GemSpawn, F.Id | F.Pos | F.Int);
            Set(SimEventType.GemCollected, F.Id | F.Client);
            Set(SimEventType.PlayerHurt, F.Client | F.Pos | F.Value);
            Set(SimEventType.PlayerDowned, F.Client | F.Pos);
            Set(SimEventType.PlayerRevived, F.Client | F.Pos);
            Set(SimEventType.LootDrop, F.Client | F.Pos | F.Seed | F.Int | F.Value);
            Set(SimEventType.LevelUpOffer, F.Client | F.Seed);
            Set(SimEventType.Teleport, F.Client | F.Pos);
            Set(SimEventType.RoomStarted, F.Int);
            Set(SimEventType.RoomCleared, F.Int | F.Pos);
            Set(SimEventType.RunEnded, F.Int);
            Set(SimEventType.BossSpawned, F.Id | F.Pos);
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
            if ((f & F.Value) != 0) r.ReadValueSafe(out e.Value);
            if ((f & F.Int) != 0) r.ReadValueSafe(out e.Int);
            if ((f & F.Seed) != 0) r.ReadValueSafe(out e.Seed);
            return e;
        }
    }

    /// <summary>What a client needs to draw one enemy.</summary>
    public struct EnemyRenderState
    {
        public ushort Id;
        public EnemyType Type;
        public bool Elite;
        public Vector2 Position;
        public float Radius;
    }

    /// <summary>
    /// Enemy positions for clients, quantized to 7 bytes each and split into chunks that fit
    /// one unreliable packet. Deaths arrive separately as reliable events.
    /// </summary>
    public static class SnapshotCodec
    {
        public const int EnemiesPerChunk = 100;
        private const float PositionScale = 100f;

        public static void WriteChunk(FastBufferWriter w, EnemySimulation enemies, int start, int count)
        {
            w.WriteValueSafe((ushort)count);
            for (int i = start; i < start + count; i++)
            {
                ref var e = ref enemies[i];
                w.WriteValueSafe((ushort)e.Id);
                w.WriteValueSafe((byte)((byte)e.Type | (e.Elite ? 0x80 : 0)));
                w.WriteValueSafe((short)Mathf.RoundToInt(e.Position.x * PositionScale));
                w.WriteValueSafe((short)Mathf.RoundToInt(e.Position.y * PositionScale));
            }
        }

        public static void ReadChunk(FastBufferReader r, List<EnemyRenderState> results)
        {
            results.Clear();
            r.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out ushort id);
                r.ReadValueSafe(out byte typeAndFlags);
                r.ReadValueSafe(out short x);
                r.ReadValueSafe(out short y);
                var type = (EnemyType)(typeAndFlags & 0x7F);
                bool elite = (typeAndFlags & 0x80) != 0;
                results.Add(new EnemyRenderState
                {
                    Id = id,
                    Type = type,
                    Elite = elite,
                    Position = new Vector2(x / PositionScale, y / PositionScale),
                    Radius = EnemyCatalog.Get(type).Radius * (elite ? EnemyCatalog.EliteRadiusMultiplier : 1f),
                });
            }
        }
    }
}
