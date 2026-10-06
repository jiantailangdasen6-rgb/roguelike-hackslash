using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public struct Enemy
    {
        public int Id;
        public EnemyType Type;
        public bool Elite;
        public Vector2 Position;
        public float Health;
        public float MaxHealth;
        public float Radius;
        public float Speed;
        public float ContactDamage;
        public float AttackTimer;
        public bool Dead;

        public EnemyDef Def => EnemyCatalog.Get(Type);
    }

    /// <summary>
    /// All enemies as a flat array plus a uniform grid for neighbour queries. No Unity physics,
    /// so hundreds of enemies stay cheap and the simulation can run headless on the host.
    /// Indices are stable until <see cref="RemoveDead"/> is called.
    /// </summary>
    public sealed class EnemySimulation
    {
        private const float CellSize = 2f;
        private const int MaxNeighboursForSeparation = 8;
        private const float SeparationStrength = 10f;

        private readonly Rect _bounds;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly int[] _cellHead;
        private int[] _next;

        private Enemy[] _enemies = new Enemy[256];
        private int _nextId = 1;

        public int Count { get; private set; }
        public float MaxRadius { get; private set; } = 0.5f;

        public EnemySimulation(Rect bounds)
        {
            _bounds = bounds;
            _gridWidth = Mathf.CeilToInt(bounds.width / CellSize) + 1;
            _gridHeight = Mathf.CeilToInt(bounds.height / CellSize) + 1;
            _cellHead = new int[_gridWidth * _gridHeight];
            _next = new int[_enemies.Length];
            RebuildGrid();
        }

        public ref Enemy this[int index] => ref _enemies[index];

        public int Spawn(EnemyType type, Vector2 position, float healthMultiplier, bool elite)
        {
            if (Count == _enemies.Length)
            {
                Array.Resize(ref _enemies, Count * 2);
                Array.Resize(ref _next, Count * 2);
            }

            var def = EnemyCatalog.Get(type);
            float health = def.Health * healthMultiplier * (elite ? EnemyCatalog.EliteHealthMultiplier : 1f);
            var enemy = new Enemy
            {
                Id = _nextId++,
                Type = type,
                Elite = elite,
                Position = ClampToBounds(position, def.Radius),
                Health = health,
                MaxHealth = health,
                Radius = def.Radius * (elite ? EnemyCatalog.EliteRadiusMultiplier : 1f),
                Speed = def.Speed,
                ContactDamage = def.ContactDamage * (elite ? EnemyCatalog.EliteDamageMultiplier : 1f),
                AttackTimer = def.FireCooldown * 0.5f,
            };
            _enemies[Count++] = enemy;
            MaxRadius = Mathf.Max(MaxRadius, enemy.Radius);
            return enemy.Id;
        }

        /// <summary>Moves every enemy toward (or, for archers, keeps distance from) the nearest living player.</summary>
        public void Step(float dt, IReadOnlyList<SimPlayer> players, List<HostileShot> shotsOut)
        {
            RebuildGrid();

            for (int i = 0; i < Count; i++)
            {
                ref var e = ref _enemies[i];
                if (e.Dead) continue;

                var target = NearestLivingPlayer(players, e.Position);
                Vector2 desired = Vector2.zero;
                if (target != null)
                {
                    Vector2 toPlayer = target.Position - e.Position;
                    float distance = toPlayer.magnitude;
                    Vector2 dir = distance > 0.001f ? toPlayer / distance : Vector2.zero;
                    var def = e.Def;

                    if (def.IsRanged && def.PreferredDistance > 0f)
                    {
                        if (distance > def.PreferredDistance + 1f) desired = dir;
                        else if (distance < def.PreferredDistance - 1f) desired = -dir;
                        else desired = new Vector2(-dir.y, dir.x) * 0.4f; // strafe
                    }
                    else
                    {
                        desired = dir;
                    }

                    if (def.IsRanged)
                    {
                        e.AttackTimer -= dt;
                        if (e.AttackTimer <= 0f && distance < 14f)
                        {
                            e.AttackTimer = def.FireCooldown;
                            FireVolley(e, dir, def, shotsOut);
                        }
                    }
                }

                Vector2 separation = Separation(i);
                e.Position = ClampToBounds(e.Position + (desired * e.Speed + separation) * dt, e.Radius);
            }
        }

        private static void FireVolley(in Enemy e, Vector2 aim, EnemyDef def, List<HostileShot> shotsOut)
        {
            if (def.ShotCount <= 1)
            {
                shotsOut.Add(new HostileShot(e.Position, aim * def.ShotSpeed, def.ShotDamage));
                return;
            }

            float baseAngle = Mathf.Atan2(aim.y, aim.x);
            for (int s = 0; s < def.ShotCount; s++)
            {
                float a = baseAngle + s * Mathf.PI * 2f / def.ShotCount;
                shotsOut.Add(new HostileShot(e.Position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * def.ShotSpeed, def.ShotDamage));
            }
        }

        private Vector2 Separation(int index)
        {
            Vector2 position = _enemies[index].Position;
            float radius = _enemies[index].Radius;
            Vector2 push = Vector2.zero;
            int overlapping = 0;

            GatherCandidates(position, radius + MaxRadius, _candidates);
            foreach (int other in _candidates)
            {
                if (other == index) continue;
                ref var o = ref _enemies[other];
                if (o.Dead) continue;
                Vector2 delta = position - o.Position;
                float minDist = radius + o.Radius;
                float sq = delta.sqrMagnitude;
                if (sq >= minDist * minDist) continue;

                float dist = Mathf.Sqrt(sq);
                Vector2 dir = dist > 0.0001f ? delta / dist : new Vector2((index & 1) == 0 ? 1f : -1f, 0f);
                push += dir * (minDist - dist) * SeparationStrength;
                if (++overlapping >= MaxNeighboursForSeparation) break;
            }
            return push;
        }

        public static SimPlayer NearestLivingPlayer(IReadOnlyList<SimPlayer> players, Vector2 position)
        {
            SimPlayer best = null;
            float bestSq = float.MaxValue;
            foreach (var p in players)
            {
                if (!p.Alive) continue;
                float sq = (p.Position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Adds indices of living enemies whose body overlaps the circle.</summary>
        public void QueryCircle(Vector2 center, float radius, List<int> results)
        {
            GatherCandidates(center, radius + MaxRadius, _candidates);
            results.Clear();
            foreach (int i in _candidates)
            {
                ref var e = ref _enemies[i];
                if (e.Dead) continue;
                float r = radius + e.Radius;
                if ((e.Position - center).sqrMagnitude <= r * r) results.Add(i);
            }
        }

        /// <summary>Index of the nearest living enemy within range, or -1.</summary>
        public int FindNearest(Vector2 position, float maxRange)
        {
            int best = -1;
            float bestSq = maxRange * maxRange;
            for (int i = 0; i < Count; i++)
            {
                if (_enemies[i].Dead) continue;
                float sq = (_enemies[i].Position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }
            return best;
        }

        public void RemoveDead()
        {
            int write = 0;
            for (int read = 0; read < Count; read++)
            {
                if (_enemies[read].Dead) continue;
                if (write != read) _enemies[write] = _enemies[read];
                write++;
            }
            Count = write;
            RebuildGrid(); // indices changed
        }

        public void Clear()
        {
            Count = 0;
            RebuildGrid();
        }

        // The grid is rebuilt at the start of each Step (and after compaction). Enemies move a little
        // during the step, so lookups scan one extra ring of cells; enemies spawned after the rebuild
        // (index >= _gridCount) are not in the grid and are checked directly.
        private readonly List<int> _candidates = new();
        private int _gridCount;

        private void RebuildGrid()
        {
            Array.Fill(_cellHead, -1);
            for (int i = 0; i < Count; i++)
            {
                int cell = CellIndex(_enemies[i].Position);
                _next[i] = _cellHead[cell];
                _cellHead[cell] = i;
            }
            _gridCount = Count;
        }

        private void GatherCandidates(Vector2 center, float radius, List<int> buffer)
        {
            buffer.Clear();
            int minX = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - _bounds.xMin) / CellSize) - 1, 0, _gridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((center.x + radius - _bounds.xMin) / CellSize) + 1, 0, _gridWidth - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((center.y - radius - _bounds.yMin) / CellSize) - 1, 0, _gridHeight - 1);
            int maxY = Mathf.Clamp(Mathf.FloorToInt((center.y + radius - _bounds.yMin) / CellSize) + 1, 0, _gridHeight - 1);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                for (int i = _cellHead[y * _gridWidth + x]; i != -1; i = _next[i])
                    buffer.Add(i);

            for (int i = _gridCount; i < Count; i++) buffer.Add(i);
        }

        private int CellIndex(Vector2 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - _bounds.xMin) / CellSize), 0, _gridWidth - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((p.y - _bounds.yMin) / CellSize), 0, _gridHeight - 1);
            return y * _gridWidth + x;
        }

        private Vector2 ClampToBounds(Vector2 p, float radius) => new(
            Mathf.Clamp(p.x, _bounds.xMin + radius, _bounds.xMax - radius),
            Mathf.Clamp(p.y, _bounds.yMin + radius, _bounds.yMax - radius));
    }
}
