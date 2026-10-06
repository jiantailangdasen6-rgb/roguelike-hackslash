using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Draws enemies. The host reads the simulation directly; clients draw the latest snapshot,
    /// smoothing toward it each frame.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const float StaleAfterSeconds = 1f;
        private const float Smoothing = 14f;

        private sealed class Visual
        {
            public SpriteRenderer Renderer;
            public Color BaseColor;
            public float Flash;
            public int SeenFrame;
        }

        private readonly Dictionary<ushort, Visual> _visuals = new();
        private readonly List<ushort> _remove = new();
        private SpritePool _pool;
        private int _frame;

        public static EnemyView Instance { get; private set; }

        public int VisibleCount => _visuals.Count;

        private void Awake()
        {
            Instance = this;
            _pool = new SpritePool(transform);
        }

        /// <summary>Sum of directions away from nearby enemies, weighted by closeness (used by test bots).</summary>
        public Vector2 ThreatAvoidance(Vector2 position, float radius)
        {
            Vector2 away = Vector2.zero;
            foreach (var v in _visuals.Values)
            {
                Vector2 delta = position - (Vector2)v.Renderer.transform.position;
                float d = delta.magnitude;
                if (d < radius && d > 0.001f) away += delta / d * (1f - d / radius);
            }
            return away;
        }

        /// <summary>Position of the nearest drawn enemy, or null (used by test bots).</summary>
        public Vector2? Nearest(Vector2 position)
        {
            Vector2? best = null;
            float bestSq = float.MaxValue;
            foreach (var v in _visuals.Values)
            {
                Vector2 p = v.Renderer.transform.position;
                float sq = (p - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = p;
                }
            }
            return best;
        }
        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.EnemyHit:
                    if (_visuals.TryGetValue((ushort)e.Id, out var v)) v.Flash = 0.08f;
                    break;
                case SimEventType.EnemyDied:
                    Release((ushort)e.Id);
                    break;
            }
        }

        private void LateUpdate()
        {
            var director = RunDirector.Instance;
            if (director == null) return;
            _frame++;

            if (director.IsServer && director.Sim != null)
            {
                var enemies = director.Sim.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    ref var e = ref enemies[i];
                    if (e.Dead) continue;
                    Show((ushort)e.Id, e.Type, e.Elite, e.Position, e.Radius, snap: true);
                }
            }
            else
            {
                float now = Time.time;
                foreach (var (id, entry) in director.ClientEnemies)
                {
                    if (now - entry.Time > StaleAfterSeconds) continue;
                    var s = entry.State;
                    Show(id, s.Type, s.Elite, s.Position, s.Radius, snap: false);
                }
            }

            _remove.Clear();
            foreach (var (id, v) in _visuals)
            {
                if (v.SeenFrame != _frame) _remove.Add(id);
                else
                {
                    v.Flash -= Time.deltaTime;
                    v.Renderer.color = v.Flash > 0f ? Color.white : v.BaseColor;
                }
            }
            foreach (var id in _remove) Release(id);
        }

        private void Show(ushort id, EnemyType type, bool elite, Vector2 position, float radius, bool snap)
        {
            if (!_visuals.TryGetValue(id, out var v))
            {
                var color = GameArt.EnemyColor(type, elite);
                int order = EnemyCatalog.Get(type).IsBoss ? 6 : 5;
                v = new Visual
                {
                    Renderer = _pool.Get(GameArt.Instance.Circle, color, order, position, radius * 2f),
                    BaseColor = color,
                };
                _visuals.Add(id, v);
                snap = true;
            }

            v.SeenFrame = _frame;
            var t = v.Renderer.transform;
            t.position = snap
                ? (Vector3)position
                : Vector3.Lerp(t.position, position, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
        }

        private void Release(ushort id)
        {
            if (!_visuals.TryGetValue(id, out var v)) return;
            _pool.Release(v.Renderer);
            _visuals.Remove(id);
        }
    }
}
