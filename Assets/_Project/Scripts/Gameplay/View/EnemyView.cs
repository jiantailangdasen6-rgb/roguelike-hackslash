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
            // Client: last snapshot and the speed between the last two, to draw where the host has it now.
            public float SnapTime = -1f;
            public Vector2 SnapPos;
            public Vector2 Velocity;
            public Vector3 RestPos; // where it was drawn when a hit stop began
        }

        /// <summary>How far ahead of the last snapshot clients draw enemies (covers snapshot spacing and half the ping).</summary>
        private const float MaxExtrapolation = 0.12f;

        private float _halfPing;

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
                    if (_visuals.TryGetValue((ushort)e.Id, out var v)) v.Animator.Flash(EnemyCatalog.Get(v.Type).Boss ? 0.035f : 0.07f); // bosses get hit constantly: keep the flash subtle
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
                // Stale = missing from recent snapshots (dead/removed), judged against the newest snapshot so a
                // paused or briefly silent host doesn't make every enemy vanish.
                float newest = director.LastSnapshotTime;
                var nm = Unity.Netcode.NetworkManager.Singleton;
                _halfPing = nm != null && nm.NetworkConfig.NetworkTransport != null
                    ? nm.NetworkConfig.NetworkTransport.GetCurrentRtt(Unity.Netcode.NetworkManager.ServerClientId) * 0.0005f
                    : 0f;
                foreach (var (_, entry) in director.ClientEnemies)
                    if (newest - entry.Time <= StaleAfterSeconds) Show(entry.State, snap: false, entry.Time, director.Halted);
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
                c.Age += HitStop.DeltaTime;
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

        private void Show(in EnemyRenderState s, bool snap, float snapTime = -1f, bool hostHalted = false)
        {
            bool fresh = false;
            if (!_visuals.TryGetValue(s.Id, out var v))
            {
                v = Acquire(s.Type, s.Elite);
                _visuals.Add(s.Id, v);
                v.SnapTime = -1f;
                snap = true;
                fresh = true;
            }

            v.SeenFrame = _frame;
            var t = v.Root.transform;
            Vector3 target = s.Position;
            if (snapTime >= 0f)
            {
                if (snapTime != v.SnapTime)
                {
                    float dt = snapTime - v.SnapTime;
                    Vector2 moved = s.Position - v.SnapPos;
                    v.Velocity = v.SnapTime >= 0f && dt > 0.01f && moved.sqrMagnitude < 2.5f * 2.5f ? moved / dt : Vector2.zero;
                    v.SnapTime = snapTime;
                    v.SnapPos = s.Position;
                }
                if (!hostHalted) target += (Vector3)(v.Velocity * Mathf.Min(Time.time - snapTime + _halfPing, MaxExtrapolation));
            }
            bool jump = (t.position - target).sqrMagnitude > 2.5f * 2.5f; // blinks/teleports shouldn't glide
            if (jump) snap = true;
            if (HitStop.Frozen && !fresh && !jump)
            {
                // Hit stop: everyone holds still and the one that took the blow shudders; afterwards they catch up,
                // which snaps the knockback in.
                t.position = s.Id == HitStop.Victim
                    ? v.RestPos + new Vector3(Random.Range(-0.07f, 0.07f), Random.Range(-0.04f, 0.04f))
                    : v.RestPos;
            }
            else
            {
                t.position = snap ? target : Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
                v.RestPos = t.position;
            }

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
            // Wraiths are half-there, and nearly vanish while gathering for a blink.
            if (s.Type == EnemyType.Wraith) tint.a = s.Anim == EnemyAnim.Windup ? 0.3f : 0.78f;
            r.color = tint;

            // Status icon over the head: red mark or spinning stun stars.
            bool stunned = s.Anim == EnemyAnim.Stunned;
            v.Icon.enabled = s.Marked || stunned;
            if (v.Icon.enabled)
            {
                v.Icon.color = stunned ? new Color(1f, 0.95f, 0.55f) : new Color(1f, 0.62f, 0.2f);
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

                float shadowSize = def.Boss ? 2.2f : def.Type == EnemyType.Bat ? 0.6f : def.Flying ? 0.85f : 1.1f;
                var shadow = Child(root.transform, "Shadow", SpriteSheet.Load("fx_shadow").First, -500, new Vector3(0f, 0.02f), shadowSize);
                var icon = Child(root.transform, "Icon", SpriteSheet.Load("fx_mark").First, 0, new Vector3(0f, def.Boss ? 2.6f : 1.25f), 1f);
                var ice = Child(root.transform, "Ice", SpriteSheet.Load("fx_ice").First, 0, new Vector3(0f, 0.45f), def.Boss ? 2f : 1f);
                v = new Visual { Root = root, Animator = animator, Shadow = shadow, Icon = icon, Ice = ice, Type = type };
            }

            v.Root.SetActive(true);
            v.Anim = (EnemyAnim)255;
            v.Shadow.enabled = true;
            v.Animator.Renderer.color = Color.white;
            v.Animator.Play("idle", force: true);

            if (elite || def.Boss || def.Type == EnemyType.Wraith)
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
                v.Glow.color = def.Type is EnemyType.Lich or EnemyType.Wraith ? new Color(0.45f, 0.8f, 1f) : def.Boss ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.55f, 0.2f);
                v.Glow.intensity = def.Boss ? 1.4f : def.Type == EnemyType.Wraith && !elite ? 0.6f : 0.9f;
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
