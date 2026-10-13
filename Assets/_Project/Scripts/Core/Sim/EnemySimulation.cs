using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public enum EnemyState : byte
    {
        Chase,
        Windup,
        Strike,
        Recover,
    }

    /// <summary>Animation state sent to clients.</summary>
    public enum EnemyAnim : byte
    {
        Idle,
        Walk,
        Windup,
        Strike,
        Hurt,
        Frozen,
        Stunned,
    }

    public struct Enemy
    {
        public int Id;
        /// <summary>Map room it belongs to (it never leaves it).</summary>
        public int RoomId;
        /// <summary>A mini-boss room's champion: bigger, tougher, drops the key.</summary>
        public bool Champion;
        /// <summary>A chest that is really a mimic (it wakes when hit).</summary>
        public bool Disguised;
        public EnemyType Type;
        public bool Elite;
        public Vector2 Position;
        public Vector2 Knockback;
        public float Health;
        public float MaxHealth;
        public float Radius;
        public float DamageMultiplier;
        public bool Dead;

        // AI
        public EnemyState State;
        public float StateTimer;
        public float Cooldown;
        public int AttackIndex;
        public Vector2 AimDirection;
        public Vector2 AimTarget;
        public float Facing;          // +1 right, −1 left
        public float LungeLeft;       // distance left in a lunge
        public bool StrikeHit;        // a lunge only hits each player once
        public int HitPlayersMask;
        public float HitStun;
        public bool Moving;
        public int AttackCounter;     // boss pattern

        // Statuses (seconds left)
        public float Frozen;
        public float Marked;
        public float Stunned;
        /// <summary>A ghost (downed player) hovers close: slowed for this long.</summary>
        public float Haunted;
        /// <summary>Cursed by a deathcaller (RunSimulation.Deathcaller): takes more damage, rises as a servant when slain.</summary>
        public float Cursed;
        public ulong CursedBy;
        /// <summary>A bone servant: whose (client + 1) and how long it has left.</summary>
        public ulong Master;
        public float Life;
        public float Burning;
        public float BurnDps;
        public float Poisoned;
        public float PoisonDps;
        public float MarkBonus;
        public float DotAccumulator;
        /// <summary>Client id + 1 of whoever last froze, stunned or marked it (0 = nobody): hits by a teammate count as teamwork.</summary>
        public ulong StatusBy;

        public EnemyDef Def => EnemyCatalog.Get(Type);
        public bool Disabled => Frozen > 0f || Stunned > 0f;

        public EnemyAnim Anim =>
            Frozen > 0f ? EnemyAnim.Frozen :
            Stunned > 0f ? EnemyAnim.Stunned :
            HitStun > 0f && State == EnemyState.Chase ? EnemyAnim.Hurt :
            State == EnemyState.Windup ? EnemyAnim.Windup :
            State == EnemyState.Strike || (State == EnemyState.Recover && StateTimer > 0.2f) ? EnemyAnim.Strike :
            Moving ? EnemyAnim.Walk : EnemyAnim.Idle;
    }

    /// <summary>
    /// All enemies as a flat array plus a uniform grid for neighbour queries. No Unity physics,
    /// so the simulation runs headless on the host. Indices are stable until <see cref="RemoveDead"/>.
    /// </summary>
    public sealed class EnemySimulation
    {
        private const float CellSize = 2f;
        private const int MaxNeighboursForSeparation = 6;
        private const float SeparationStrength = 8f;

        private readonly Rect _bounds;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly int[] _cellHead;
        private int[] _next;
        private readonly List<int> _candidates = new();
        private int _gridCount;

        private Enemy[] _enemies = new Enemy[64];
        private int _nextId = 1;

        public int Count { get; private set; }
        public float MaxRadius { get; private set; } = 0.5f;

        /// <summary>Living monsters (breakable props such as powder kegs don't count).</summary>
        public int HostileCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++)
                    if (!_enemies[i].Dead && !EnemyCatalog.Get(_enemies[i].Type).Prop) n++;
                return n;
            }
        }
        public Rect Bounds => _bounds;

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

        public int Spawn(EnemyType type, Vector2 position, float healthMultiplier, float damageMultiplier, bool elite)
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
                DamageMultiplier = damageMultiplier * (elite ? EnemyCatalog.EliteDamageMultiplier : 1f),
                Cooldown = 0.6f + (_nextId % 5) * 0.15f, // stagger first attacks
                Facing = 1f,
                AimDirection = Vector2.right,
            };
            _enemies[Count++] = enemy;
            MaxRadius = Mathf.Max(MaxRadius, enemy.Radius);
            return enemy.Id;
        }

        public int IndexOf(int id)
        {
            for (int i = 0; i < Count; i++) if (_enemies[i].Id == id) return i;
            return -1;
        }

        /// <summary>Call once per tick before moving enemies.</summary>
        public void BeginStep() => RebuildGrid();

        public Vector2 Separation(int index)
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

        /// <summary>Adds indices of living enemies whose body overlaps the circle.</summary>
        public void QueryCircle(Vector2 center, float radius, List<int> results)
        {
            GatherCandidates(center, radius + MaxRadius, _candidates);
            results.Clear();
            foreach (int i in _candidates)
            {
                ref var e = ref _enemies[i];
                if (e.Dead || e.Def.Untargetable || e.Def.Friendly) continue; // bone servants are on the players' side
                float r = radius + e.Radius;
                if ((e.Position - center).sqrMagnitude <= r * r) results.Add(i);
            }
        }

        /// <summary>Enemies overlapping a cone (apex, direction, range, full angle in degrees).</summary>
        public void QueryCone(Vector2 apex, Vector2 direction, float range, float angle, List<int> results)
        {
            QueryCircle(apex, range, results);
            float minDot = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            results.RemoveAll(i =>
            {
                Vector2 to = _enemies[i].Position - apex;
                float d = to.magnitude;
                if (d <= _enemies[i].Radius + 0.3f) return false; // hugging the attacker
                return Vector2.Dot(to / d, direction) < minDot - _enemies[i].Radius / Mathf.Max(d, 0.01f);
            });
        }

        /// <summary>Index of the nearest living enemy within range, or -1.</summary>
        public int FindNearest(Vector2 position, float maxRange, int exceptId = -1)
        {
            int best = -1;
            float bestSq = maxRange * maxRange;
            for (int i = 0; i < Count; i++)
            {
                if (_enemies[i].Dead || _enemies[i].Id == exceptId || _enemies[i].Def.Friendly) continue;
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
            RebuildGrid();
        }

        public void Clear()
        {
            Count = 0;
            RebuildGrid();
        }

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
                    if (i < Count) buffer.Add(i);

            for (int i = _gridCount; i < Count; i++) buffer.Add(i);
        }

        private int CellIndex(Vector2 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - _bounds.xMin) / CellSize), 0, _gridWidth - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((p.y - _bounds.yMin) / CellSize), 0, _gridHeight - 1);
            return y * _gridWidth + x;
        }

        public Vector2 ClampToBounds(Vector2 p, float radius) => new(
            Mathf.Clamp(p.x, _bounds.xMin + radius, _bounds.xMax - radius),
            Mathf.Clamp(p.y, _bounds.yMin + radius, _bounds.yMax - radius));
    }
}
