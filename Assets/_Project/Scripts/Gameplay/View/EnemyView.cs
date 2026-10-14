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
            public SpriteRenderer Ward;
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

        /// <summary>Where the lord is drawn (null when none is in sight).</summary>
        public Vector2? BossPosition()
        {
            foreach (var v in _visuals.Values)
                if (EnemyCatalog.Get(v.Type).Boss && v.Root.activeSelf) return v.Root.transform.position;
            return null;
        }

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

        /// <summary>Closest thing to hit (floor runes don't count). With <paramref name="only"/>: closest of that kind, unlit if it's a sigil.</summary>
        public Vector2? Nearest(Vector2 position, EnemyType? only = null)
        {
            Vector2? best = null;
            float bestSq = float.MaxValue;
            // Only what is in the room around the point (the rest of the map is far away behind walls).
            var room = RunDirector.Instance != null ? RunDirector.Instance.Map.RoomAt(position) : null;
            foreach (var v in _visuals.Values)
            {
                if (room != null && !room.Bounds.Contains((Vector2)v.Root.transform.position)) continue;
                if (only.HasValue ? v.Type != only.Value || v.Anim == EnemyAnim.Walk && v.Type is EnemyType.BloodSigil or EnemyType.PressurePlate : EnemyCatalog.Get(v.Type).Untargetable) continue;
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

        /// <summary>Closest thing drawn within <paramref name="radius"/> of a spot that matches (pings: a monster, a chest...).</summary>
        public Vector2? NearestWhere(Vector2 position, float radius, System.Func<EnemyDef, bool> match)
        {
            Vector2? best = null;
            float bestSq = radius * radius;
            foreach (var v in _visuals.Values)
            {
                Vector2 p = v.Root.transform.position;
                float sq = (p - position).sqrMagnitude;
                if (sq > bestSq || !match(EnemyCatalog.Get(v.Type))) continue;
                bestSq = sq;
                best = p;
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
            Codex.SeeEnemy(s.Type, s.Elite, s.Position);
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
            var def = EnemyCatalog.Get(s.Type);
            r.flipX = s.FacingLeft && !def.Prop;
            r.sortingOrder = def.Flat ? -930 : SortOrder(t.position.y);

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
            if (s.Cursed) tint = Color.Lerp(tint, new Color(0.78f, 0.55f, 1f), 0.55f); // a deathcaller's curse
            // A bone servant (on the players' side): pale green, a little see-through.
            if (def.Friendly) tint = new Color(0.62f, 1f, 0.72f, 0.88f);
            // Wraiths are half-there, and nearly vanish while gathering for a blink.
            if (s.Type is EnemyType.Wraith or EnemyType.ShadeStalker) tint.a = s.Anim == EnemyAnim.Windup ? 0.3f : 0.78f;
            TrialLook(v, s, def, ref tint);
            float seen = DarkHallView.Visibility(v.Root.transform.position); // out of the lantern's light in a dark hall: a shadow
            tint = new Color(tint.r * seen, tint.g * seen, tint.b * Mathf.Lerp(1f, seen, 0.8f), tint.a);
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

        /// <summary>
        /// Lower on screen draws in front. Measured from the middle of the room (rooms sit a map pitch apart), so
        /// the orders stay within the room's range and above the floor (-1000) in every room of the map.
        /// </summary>
        public static int SortOrder(float y)
        {
            float local = y - Mathf.Round(y / DungeonMap.PitchY) * DungeonMap.PitchY;
            return Mathf.RoundToInt(-local * 32f);
        }

        /// <summary>
        /// Boss trials: a warded boss sits in a pulsing ring (bone white for the King, blood for the Bishop) and the
        /// Lich turns half-there while his soul hides in the vessel; blood sigils brighten as they are lit.
        /// </summary>
        private static void TrialLook(Visual v, in EnemyRenderState s, EnemyDef def, ref Color tint)
        {
            var director = RunDirector.Instance;
            var trial = director != null && director.IsSpawned ? director.Trial.Value : BossTrial.None;
            if (def.Boss)
            {
                bool ring = trial is BossTrial.BoneWard or BossTrial.BloodRitual;
                if (ring && v.Ward == null)
                    v.Ward = Child(v.Root.transform, "Ward", SpriteSheet.Load("fx_revive_ring").First, 0, new Vector3(0f, 1.1f), 1.5f);
                if (v.Ward != null)
                {
                    v.Ward.enabled = ring;
                    if (ring)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                        var c = trial == BossTrial.BoneWard ? new Color(0.75f, 1f, 0.95f) : new Color(1f, 0.25f, 0.25f);
                        c.a = 0.45f + 0.35f * pulse;
                        v.Ward.color = c;
                        v.Ward.transform.localScale = Vector3.one * (1.45f + 0.1f * pulse);
                        v.Ward.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 40f);
                        v.Ward.sortingOrder = v.Animator.Renderer.sortingOrder + 3;
                        tint = Color.Lerp(tint, c, 0.25f);
                    }
                }
                if (trial == BossTrial.Phylactery) tint = new Color(0.6f, 0.9f, 1f, 0.4f + 0.1f * Mathf.Sin(Time.time * 8f));
                if (v.Glow != null && trial == BossTrial.BloodRitual) v.Glow.intensity = 2f + Mathf.Sin(Time.time * 6f) * 0.6f;
                // Red-hot iron: the colossus glows like a forge while he burns.
                if (trial == BossTrial.Overheat)
                {
                    float heat = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                    tint = Color.Lerp(tint, new Color(1f, 0.32f, 0.12f), 0.45f + 0.15f * heat);
                    if (v.Glow != null) { v.Glow.color = new Color(1f, 0.4f, 0.1f); v.Glow.intensity = 1.1f + 0.4f * heat; }
                }
                return;
            }
            if (s.Type is EnemyType.BloodSigil or EnemyType.PressurePlate)
            {
                bool lit = s.Anim == EnemyAnim.Walk;
                float charge = lit ? 1f : s.Health;
                // Unlit runes breathe so they catch the eye; charging ones redden toward the lit look.
                float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                tint = lit ? Color.white : Color.Lerp(new Color(1f, 0.9f, 0.85f, 0.75f + 0.25f * breathe), s.Type == EnemyType.PressurePlate ? new Color(0.6f, 0.95f, 1f, 1f) : new Color(1f, 0.45f, 0.4f, 1f), charge);
                if (v.Glow != null)
                {
                    v.Glow.intensity = lit ? 1.4f + 0.3f * Mathf.Sin(Time.time * 7f) : 0.45f + 0.25f * breathe + charge * 0.9f;
                    v.Glow.pointLightOuterRadius = lit ? 3f : 1.8f;
                }
            }
        }

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
            // The lords (and the queen's reflections) stand a head taller than their art: they should loom.
            v.Root.transform.localScale = Vector3.one * (type == EnemyType.AshenKing ? 1.55f : def.Boss || type == EnemyType.IceMirror ? BossScale : 1f);
            v.Anim = (EnemyAnim)255;
            v.Shadow.enabled = !def.Flat && type != EnemyType.IceMirror; // the queen's reflections cast no shadow
            v.Animator.Renderer.color = Color.white;
            v.Animator.Play("idle", force: true);

            bool trialProp = type is EnemyType.BoneBrazier or EnemyType.BloodSigil or EnemyType.SoulVessel or EnemyType.PressurePlate or EnemyType.WardCrystal;
            bool glows = def.Type is EnemyType.Wraith or EnemyType.ShadeStalker or EnemyType.AbyssEye or EnemyType.TideWitch or EnemyType.FrostWitch or EnemyType.FireImp or EnemyType.SlagBloater or EnemyType.IceMirror;
            if (elite || def.Boss || glows || trialProp)
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
                v.Glow.color = def.Type is EnemyType.Lich or EnemyType.Wraith ? new Color(0.45f, 0.8f, 1f) : def.Type is EnemyType.ShadeStalker or EnemyType.AbyssEye ? new Color(0.7f, 0.4f, 1f) : def.Boss ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.55f, 0.2f);
                v.Glow.intensity = def.Boss ? 1.4f : def.Type is EnemyType.Wraith or EnemyType.ShadeStalker or EnemyType.AbyssEye && !elite ? 0.6f : 0.9f;
                v.Glow.pointLightOuterRadius = def.Boss ? 4.5f : 2.2f;
                v.Glow.pointLightInnerRadius = 0.2f;
                v.Glow.transform.localPosition = new Vector3(0f, 0.6f);
                // The new lands: drowned teal, cold blue, forge orange (a reflection glows just like the queen).
                switch (def.Type)
                {
                    case EnemyType.DrownedSaint: case EnemyType.TideWitch: v.Glow.color = new Color(0.35f, 1f, 0.85f); break;
                    case EnemyType.FrostQueen: case EnemyType.IceMirror: case EnemyType.FrostWitch: v.Glow.color = new Color(0.6f, 0.85f, 1f); break;
                    case EnemyType.ForgeColossus: case EnemyType.FireImp: case EnemyType.SlagBloater: case EnemyType.AshenKing: v.Glow.color = new Color(1f, 0.5f, 0.15f); break;
                }
                // Small glowing monsters keep their light faint (a strong light on a small sprite washes it out).
                if (!def.Boss && !elite && def.Type is EnemyType.TideWitch or EnemyType.FrostWitch or EnemyType.FireImp or EnemyType.SlagBloater)
                {
                    v.Glow.intensity = 0.45f;
                    v.Glow.pointLightOuterRadius = 1.8f;
                    v.Glow.transform.localPosition = new Vector3(0f, 1.2f);
                }
                if (def.Type == EnemyType.IceMirror)
                {
                    v.Glow.intensity = 1.4f;
                    v.Glow.pointLightOuterRadius = 4.5f;
                }
                if (trialProp)
                {
                    v.Glow.color = type == EnemyType.BloodSigil ? new Color(1f, 0.2f, 0.15f) : new Color(0.5f, 0.95f, 1f);
                    v.Glow.intensity = 1.1f;
                    v.Glow.pointLightOuterRadius = 2.8f;
                    v.Glow.transform.localPosition = new Vector3(0f, type == EnemyType.BloodSigil ? 0.1f : 0.9f);
                }
            }
            else if (v.Glow != null)
            {
                v.Glow.enabled = false;
            }
            return v;
        }

        private const float BossScale = 1.3f;

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
