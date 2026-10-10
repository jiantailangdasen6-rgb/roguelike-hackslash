using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// Draws enemies as animated pixel-art sprites. The host reads the simulation directly; clients draw the
    /// latest snapshot, smoothing toward it. Animation state, facing and statuses come with each enemy.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const float StaleAfterSeconds = 1f;
        private const float Smoothing = 16f;

        private sealed class Visual
        {
            public GameObject Root;
            public PixelAnimator Animator;
            public SpriteRenderer Shadow;
            public SpriteRenderer Icon;
            public SpriteRenderer Ice;
            public Light2D Glow;
            public EnemyType Type;
            public EnemyAnim Anim = (EnemyAnim)255;
            public int SeenFrame;
        }

        private sealed class Corpse
        {
            public Visual Visual;
            public float Age;
        }

        public static EnemyView Instance { get; private set; }

        private readonly Dictionary<ushort, Visual> _visuals = new();
        private readonly List<Corpse> _corpses = new();
        private readonly Stack<Visual> _pool = new();
        private readonly List<ushort> _remove = new();
        private int _frame;

        public int VisibleCount => _visuals.Count;

        private void Awake() => Instance = this;
        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        // ------------------------------------------------------------------ Queries for bots / presenter

        public Vector2 ThreatAvoidance(Vector2 position, float radius)
        {
            Vector2 away = Vector2.zero;
            foreach (var v in _visuals.Values)
            {
                Vector2 delta = position - (Vector2)v.Root.transform.position;
                float d = delta.magnitude;
                if (d < radius && d > 0.001f) away += delta / d * (1f - d / radius);
            }
            return away;
        }

        public Vector2? Nearest(Vector2 position)
        {
            Vector2? best = null;
            float bestSq = float.MaxValue;
            foreach (var v in _visuals.Values)
            {
                Vector2 p = v.Root.transform.position;
                float sq = (p - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = p;
                }
            }
            return best;
        }

        public bool TryGetPosition(int id, out Vector2 position)
        {
            if (_visuals.TryGetValue((ushort)id, out var v))
            {
                position = v.Root.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        // ------------------------------------------------------------------ Events

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.EnemyHit:
                    if (_visuals.TryGetValue((ushort)e.Id, out var v)) v.Animator.Flash(0.07f);
                    break;
                case SimEventType.EnemyDied:
                    if (_visuals.Remove((ushort)e.Id, out var dead))
                    {
                        dead.Animator.Frozen = false;
                        dead.Animator.Renderer.color = Color.white;
                        dead.Animator.Play("death", force: true, hold: true);
                        dead.Icon.enabled = false;
                        dead.Ice.enabled = false;
                        if (dead.Glow != null) dead.Glow.enabled = false;
                        _corpses.Add(new Corpse { Visual = dead });
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ Frame

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
                    if (enemies[i].Dead) continue;
                    Show(EnemyRenderState.From(enemies[i]), snap: true);
                }
            }
            else
            {
                float now = Time.time;
                foreach (var (_, entry) in director.ClientEnemies)
                    if (now - entry.Time <= StaleAfterSeconds) Show(entry.State, snap: false);
            }

            _remove.Clear();
            foreach (var (id, v) in _visuals)
                if (v.SeenFrame != _frame) _remove.Add(id);
            foreach (var id in _remove)
            {
                Release(_visuals[id]);
                _visuals.Remove(id);
            }

            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                var c = _corpses[i];
                c.Age += Time.deltaTime;
                if (c.Age > 1.6f)
                {
                    var color = c.Visual.Animator.Renderer.color;
                    color.a = Mathf.Clamp01(1f - (c.Age - 1.6f) / 0.6f);
                    c.Visual.Animator.Renderer.color = color;
                    c.Visual.Shadow.enabled = false;
                }
                if (c.Age > 2.2f)
                {
                    Release(c.Visual);
                    _corpses.RemoveAt(i);
                }
            }
        }

        private void Show(in EnemyRenderState s, bool snap)
        {
            if (!_visuals.TryGetValue(s.Id, out var v))
            {
                v = Acquire(s.Type, s.Elite);
                _visuals.Add(s.Id, v);
                snap = true;
            }

            v.SeenFrame = _frame;
            var t = v.Root.transform;
            Vector3 target = s.Position;
            t.position = snap ? target : Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));

            var r = v.Animator.Renderer;
            r.flipX = s.FacingLeft;
            r.sortingOrder = SortOrder(t.position.y);

            if (s.Anim != v.Anim)
            {
                v.Anim = s.Anim;
                switch (s.Anim)
                {
                    case EnemyAnim.Idle: v.Animator.SetBase("idle"); v.Animator.Play("idle"); break;
                    case EnemyAnim.Walk: v.Animator.SetBase("walk"); v.Animator.Play("walk"); break;
                    case EnemyAnim.Windup: v.Animator.Play("windup", force: true, hold: true); break;
                    case EnemyAnim.Strike: v.Animator.Play("strike", force: true, hold: true); break;
                    case EnemyAnim.Hurt: v.Animator.Play("hurt", force: true); break;
                    case EnemyAnim.Stunned: v.Animator.Play("hurt", force: true, hold: true); break;
                }
            }

            bool frozen = s.Anim == EnemyAnim.Frozen;
            v.Animator.Frozen = frozen;
            v.Ice.enabled = frozen;
            Color tint = Color.white;
            if (frozen) tint = new Color(0.65f, 0.85f, 1f);
            else if (s.Burning) tint = new Color(1f, 0.72f, 0.55f);
            else if (s.Poisoned) tint = new Color(0.72f, 1f, 0.65f);
            else if (s.Elite) tint = new Color(1f, 0.82f, 0.7f);
            r.color = tint;

            // Status icon over the head: red mark or spinning stun stars.
            bool stunned = s.Anim == EnemyAnim.Stunned;
            v.Icon.enabled = s.Marked || stunned;
            if (v.Icon.enabled)
            {
                v.Icon.color = stunned ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.25f, 0.2f);
                v.Icon.transform.localRotation = stunned ? Quaternion.Euler(0, 0, Time.time * 360f) : Quaternion.identity;
                v.Icon.sortingOrder = r.sortingOrder + 2;
            }
            v.Ice.sortingOrder = r.sortingOrder + 1;
        }

        public static int SortOrder(float y) => Mathf.RoundToInt(-y * 32f);

        private Visual Acquire(EnemyType type, bool elite)
        {
            var def = EnemyCatalog.Get(type);
            Visual v = null;
            while (_pool.Count > 0 && v == null)
            {
                var candidate = _pool.Pop();
                if (candidate.Type == type) v = candidate;
                else Destroy(candidate.Root);
            }

            if (v == null)
            {
                var root = new GameObject(def.Name);
                root.transform.SetParent(transform, false);
                var body = root.AddComponent<SpriteRenderer>();
                var animator = root.AddComponent<PixelAnimator>();
                animator.SetSheet(SpriteSheet.Load(def.Sheet));

                var shadow = Child(root.transform, "Shadow", SpriteSheet.Load("fx_shadow").First, -500, new Vector3(0f, 0.02f), def.Boss ? 2.2f : 1.1f);
                var icon = Child(root.transform, "Icon", SpriteSheet.Load("fx_mark").First, 0, new Vector3(0f, def.Boss ? 2.1f : 1.25f), 1f);
                var ice = Child(root.transform, "Ice", SpriteSheet.Load("fx_ice").First, 0, new Vector3(0f, 0.45f), def.Boss ? 2f : 1f);
                v = new Visual { Root = root, Animator = animator, Shadow = shadow, Icon = icon, Ice = ice, Type = type };
            }

            v.Root.SetActive(true);
            v.Anim = (EnemyAnim)255;
            v.Shadow.enabled = true;
            v.Animator.Renderer.color = Color.white;
            v.Animator.Play("idle", force: true);

            if (elite || def.Boss)
            {
                if (v.Glow == null)
                {
                    var lightGo = new GameObject("Glow");
                    lightGo.transform.SetParent(v.Root.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 0.6f);
                    v.Glow = lightGo.AddComponent<Light2D>();
                    v.Glow.lightType = Light2D.LightType.Point;
                }
                v.Glow.enabled = true;
                v.Glow.color = def.Boss ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.55f, 0.2f);
                v.Glow.intensity = def.Boss ? 1.4f : 0.9f;
                v.Glow.pointLightOuterRadius = def.Boss ? 4.5f : 2.2f;
                v.Glow.pointLightInnerRadius = 0.2f;
            }
            else if (v.Glow != null)
            {
                v.Glow.enabled = false;
            }
            return v;
        }

        private static SpriteRenderer Child(Transform parent, string name, Sprite sprite, int order, Vector3 offset, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void Release(Visual v)
        {
            v.Root.SetActive(false);
            _pool.Push(v);
        }
    }
}
