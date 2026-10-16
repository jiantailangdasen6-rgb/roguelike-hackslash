using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>How a wave of a fight room comes (RunSimulation.Waves).</summary>
    public enum WaveKind : byte
    {
        Normal,   // the usual: anywhere in the room
        Ambush,   // 奇襲: out of the doorways, behind the team
        Swarm,    // 大群: many of the land's quickest, each frail
        Encircle, // 包囲: a ring closing round one of the team
        Totem,    // 呼び声の柱: pillars that keep calling monsters until broken
    }

    public static class WaveCatalog
    {
        public static string Name(WaveKind k) => k switch
        {
            WaveKind.Ambush => "奇襲",
            WaveKind.Swarm => "大群",
            WaveKind.Encircle => "包囲",
            WaveKind.Totem => "呼び声の柱",
            _ => "",
        };

        public static string Hint(WaveKind k) => k switch
        {
            WaveKind.Ambush => "入口から背後を突いてくる",
            WaveKind.Swarm => "弱いが数が多い。範囲攻撃でまとめて",
            WaveKind.Encircle => "仲間の一人を囲む。輪の外へ抜ける",
            WaveKind.Totem => "柱が魔物を呼び続ける。先に柱を壊せ",
            _ => "",
        };
    }

    /// <summary>
    /// Wave kinds: after the first wave of a fight room, each wave comes in one of several ways (an ambush through
    /// the doorways, a swarm of frail quick ones, a ring round one of the team, or calling pillars that must be
    /// broken), so rooms don't play the same. The kind travels in WaveStarted (Size.x) for the HUD's call.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float SwarmCount = 1.8f;
        public const float SwarmHealth = 0.45f;
        public const float EncircleRadius = 4.5f;
        public const float TotemEvery = 4.5f;

        /// <summary>Tests and screenshots (-wave): every wave after the first comes this way.</summary>
        public WaveKind? ForcedWave;

        private WaveKind PickWave(RoomState room)
        {
            if (room.Wave <= 1 || room.Kind == RoomKind.MiniBoss) return WaveKind.Normal;
            if (ForcedWave.HasValue) return ForcedWave.Value;
            int roll = _spawnRng.Range(0, room.Index >= 1 ? 11 : 9);
            return roll switch
            {
                < 3 => WaveKind.Normal,
                < 5 => WaveKind.Ambush,
                < 7 => WaveKind.Swarm,
                < 9 => WaveKind.Encircle,
                _ => WaveKind.Totem,
            };
        }

        /// <summary>Queues a wave of <paramref name="size"/> in <see cref="Room"/> the given way.</summary>
        private void SpawnWave(WaveKind kind, int size, bool eliteRoom)
        {
            switch (kind)
            {
                case WaveKind.Ambush:
                {
                    var doors = new List<Vector2>();
                    var map = Map[Room.Id];
                    for (int d = 0; d < 4; d++)
                        if (map.HasDoor(d)) doors.Add(DungeonMap.EntryPoint(map, d));
                    if (doors.Count == 0) goto default;
                    for (int i = 0; i < size; i++)
                    {
                        Vector2 at = ClampToRoom(doors[i % doors.Count] + Rotate(Vector2.right, _spawnRng.Range(0f, 360f)) * _spawnRng.Range(0.3f, 1.6f), 0.6f);
                        QueueSpawn(RandomEnemyType(), _spawnRng.Chance(EliteChance), Arena.Resolve(Layout, at, 0.5f));
                    }
                    break;
                }
                case WaveKind.Swarm:
                {
                    var type = SwarmType();
                    int count = Mathf.RoundToInt(size * SwarmCount);
                    for (int i = 0; i < count; i++)
                    {
                        QueueSpawn(type, false);
                        var s = PendingSpawns[PendingSpawns.Count - 1];
                        s.HealthMul = SwarmHealth;
                        PendingSpawns[PendingSpawns.Count - 1] = s;
                    }
                    break;
                }
                case WaveKind.Encircle:
                {
                    SimPlayer target = null;
                    foreach (var p in Players)
                        if (p.Alive && p.RoomId == Room.Id && (target == null || _spawnRng.Chance(0.5f))) target = p;
                    if (target == null) goto default;
                    for (int i = 0; i < size; i++)
                    {
                        Vector2 at = ClampToRoom(target.Position + Rotate(Vector2.right, i * 360f / size) * EncircleRadius, 0.6f);
                        QueueSpawn(RandomEnemyType(), _spawnRng.Chance(EliteChance), Arena.Resolve(Layout, at, 0.5f));
                    }
                    break;
                }
                case WaveKind.Totem:
                {
                    int totems = PlayersIn(Room) >= 3 ? 2 : 1 + (_spawnRng.Chance(0.5f) ? 1 : 0);
                    for (int t = 0; t < totems; t++) QueueSpawn(EnemyType.SummonTotem, false);
                    for (int i = 0; i < size / 2; i++) QueueSpawn(RandomEnemyType(), _spawnRng.Chance(EliteChance));
                    break;
                }
                default:
                    for (int i = 0; i < size; i++)
                        QueueSpawn(RandomEnemyType(), (eliteRoom && i == 0) || _spawnRng.Chance(EliteChance));
                    break;
            }
        }

        /// <summary>The land's quickest walker makes a swarm.</summary>
        private EnemyType SwarmType()
        {
            EnemyType best = EnemyType.Skeleton;
            float speed = 0f;
            foreach (var (t, _, min) in BiomeCatalog.Get(Biome).Pool)
            {
                if (Room.Index < min) continue;
                var m = EnemyCatalog.Get(t);
                if (m.Boss || m.Prop || m.Flying || IsChampionKind(t) || m.Speed <= speed) continue;
                speed = m.Speed;
                best = t;
            }
            return best;
        }

        /// <summary>Each tick: calling pillars bring a monster now and then (up to a few at a time).</summary>
        private void StepTotems(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Type != EnemyType.SummonTotem) continue;
                if (e.RoomId < 0 || e.RoomId >= RoomStates.Count || !RoomStates[e.RoomId].Fighting) continue;
                e.AffixTimer -= dt;
                if (e.AffixTimer > 0f) continue;
                e.AffixTimer = TotemEvery;
                var room = RoomStates[e.RoomId];
                Room = room;
                if (HostilesIn(room) + PendingIn(room) >= Mathf.RoundToInt(10 * PlayerScale)) continue;
                Vector2 at = ClampToRoom(e.Position + Rotate(Vector2.right, _spawnRng.Range(0f, 360f)) * 1.4f, 0.6f);
                QueueSpawn(RandomEnemyType(), false, Arena.Resolve(Layout, at, 0.5f));
                Events.Add(new SimEvent(SimEventType.EliteShield) { Id = e.Id, Int = 2, Pos = e.Position }); // the call (shown like a summoner's)
                e = ref Enemies[i];
            }
        }
    }
}
