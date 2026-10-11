using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Turns simulation events into visuals on every peer: projectiles, attack telegraphs, hit sparks,
    /// damage numbers, spell areas, souls, spawn runes, the exit portal and screen shake.
    /// </summary>
    public sealed class EventPresenter : MonoBehaviour
    {
        private const int MaxNumbers = 50;
        private static readonly Color DangerColor = new(1f, 0.16f, 0.12f, 1f);

        public static EventPresenter Instance { get; private set; }

        private sealed class Fx
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Light2D Light;
            public Sprite[] Frames;
            public float Fps;
            public float Age;
            public float Life;
            public Vector2 Velocity;
            public float StartScale;
            public float EndScale;
            public Color Color;
            public bool FadeOut;
            public Transform Follow;
            public Vector3 FollowFrom;
        }

        private sealed class Telegraph
        {
            public Fx Outline;
            public Fx Fill;
            public AttackShape Shape;
            public Vector2 Origin;
            public Vector2 Direction;
            public Vector2 Size;
            public float Duration;
            public float Age;
            public int EnemyId;
            public bool Square;
        }

        /// <summary>Telegraphs of room hazards use negative ids so enemy strikes never clear them.</summary>
        private static int HazardKey(int hazardId) => -1 - hazardId;

        private sealed class Number
        {
            public Text Text;
            public Vector3 World;
            public float Age;
            public float Life;
        }

        private readonly List<Fx> _effects = new();
        private readonly Dictionary<int, Fx> _projectiles = new();
        private readonly Dictionary<int, Fx> _souls = new();
        private readonly List<Telegraph> _telegraphs = new();
        private readonly List<Number> _numbers = new();
        private readonly Stack<Text> _freeTexts = new();
        private readonly Stack<Fx> _pool = new();
        private readonly List<int> _expired = new();

        private RectTransform _numberLayer;
        private Camera _camera;

        private void Awake()
        {
            Instance = this;
            _numberLayer = (RectTransform)UIKit.CreateCanvas("DamageNumbers", 10, transform).transform;
        }

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        // ------------------------------------------------------------------ Bot helper

        /// <summary>True if the point is inside an active telegraph; <paramref name="away"/> points out of it.</summary>
        public bool DangerAt(Vector2 point, out Vector2 away)
        {
            foreach (var t in _telegraphs)
            {
                Vector2 o = t.Shape == AttackShape.SelfCircle && EnemyView.Instance != null && EnemyView.Instance.TryGetPosition(t.EnemyId, out var ep) ? ep : t.Origin;
                Vector2 to = point - o;
                bool inside = t.Square ? Mathf.Abs(to.x) <= t.Size.x * 0.5f + 0.5f && Mathf.Abs(to.y) <= t.Size.y * 0.5f + 0.5f : t.Shape switch
                {
                    AttackShape.Circle or AttackShape.SelfCircle => to.magnitude <= t.Size.x + 0.5f,
                    AttackShape.Cone => to.magnitude <= t.Size.x + 0.5f && Vector2.Angle(t.Direction, to) <= t.Size.y * 0.5f + 15f,
                    _ => Vector2.Dot(to, t.Direction) is var along && along > -0.5f && along < t.Size.x + 0.5f
                         && Mathf.Abs(t.Direction.x * to.y - t.Direction.y * to.x) < t.Size.y * 0.5f + 0.7f,
                };
                if (!inside) continue;
                away = t.Shape == AttackShape.Line ? new Vector2(-t.Direction.y, t.Direction.x) * Mathf.Sign(t.Direction.x * to.y - t.Direction.y * to.x + 0.001f) : to.normalized;
                if (away.sqrMagnitude < 0.01f) away = Vector2.up;
                return true;
            }
            away = default;
            return false;
        }

        // ------------------------------------------------------------------ Events

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.ProjectileSpawn: SpawnProjectile(e); break;
                case SimEventType.ProjectileEnd:
                    if (_projectiles.Remove(e.Id, out var proj))
                    {
                        Release(proj);
                        Spark(e.Pos, 0.6f, new Color(1f, 0.95f, 0.8f));
                    }
                    break;
                case SimEventType.EnemyHit: OnEnemyHit(e); break;
                case SimEventType.EnemyDied:
                    Burst("fx_puff", e.Pos + Vector2.up * 0.4f, 14f, 1.4f, Color.white);
                    if ((EnemyType)e.Int == EnemyType.TreasureChest && e.Value < 0.5f)
                    {
                        var glint = Spawn("fx_holy", e.Pos, 14f, 0.6f, 0.6f, 1.1f, new Color(1f, 0.85f, 0.45f), order: 2400);
                        AddLight(glint, new Color(1f, 0.8f, 0.4f), 1.6f, 3f);
                    }
                    break;
                case SimEventType.EnemyTelegraph: AddTelegraph(e); break;
                case SimEventType.EnemyStrike:
                    RemoveTelegraph(e.Id);
                    if ((AttackKind)e.Int == AttackKind.Melee) CameraShake.Add(0.08f);
                    if ((AttackKind)e.Int == AttackKind.Blink) Burst("fx_puff", e.Pos + Vector2.up * 0.3f, 16f, 1.5f, new Color(0.7f, 0.85f, 1f));
                    break;
                case SimEventType.HazardWarn: OnHazardWarn(e); break;
                case SimEventType.HazardFire: OnHazardFire(e); break;
                case SimEventType.SpawnWarning:
                {
                    var fx = Spawn("fx_portal", e.Pos, 8f, e.Value + 0.2f, 0.4f, 0.75f, new Color(1f, 0.4f, 0.4f, 0.9f), order: -400);
                    fx.FadeOut = true;
                    AddLight(fx, new Color(1f, 0.2f, 0.15f), 0.8f, 1.6f);
                    break;
                }
                case SimEventType.AreaEffect: OnArea(e); break;
                case SimEventType.PlayerHealed:
                    if (e.Value >= 1f) ShowNumber(e.Pos + Vector2.up * 1.2f, $"+{Mathf.RoundToInt(e.Value)}", new Color(0.5f, 1f, 0.55f), 24);
                    break;
                case SimEventType.PlayerHurt:
                    ShowNumber(e.Pos + Vector2.up * 1.2f, Mathf.RoundToInt(e.Value).ToString(), new Color(1f, 0.3f, 0.3f), 28);
                    if (NetPlayer.Local != null && NetPlayer.Local.OwnerClientId == e.ClientId) CameraShake.Add(0.35f);
                    break;
                case SimEventType.PlayerDowned:
                    Burst("fx_puff", e.Pos + Vector2.up * 0.4f, 12f, 1.8f, new Color(1f, 0.6f, 0.6f));
                    break;
                case SimEventType.PlayerRevived:
                {
                    var fx = Spawn("fx_heal", e.Pos + Vector2.up * 0.4f, 1f, 0.5f, 0.3f, 1.4f, new Color(1f, 1f, 1f, 0.9f));
                    fx.FadeOut = true;
                    break;
                }
                case SimEventType.SoulSpawn:
                {
                    var fx = Spawn("fx_soul", e.Pos + Vector2.up * 0.3f, 8f, 9999f, e.Int >= 10 ? 1.4f : 1f, e.Int >= 10 ? 1.4f : 1f, Color.white);
                    AddLight(fx, new Color(0.4f, 0.75f, 1f), 0.6f, 1.1f);
                    _souls[e.Id] = fx;
                    break;
                }
                case SimEventType.SoulCollected:
                    if (_souls.Remove(e.Id, out var soul))
                    {
                        var target = FindPlayer(e.ClientId);
                        if (target != null)
                        {
                            soul.Follow = target.transform;
                            soul.FollowFrom = soul.Go.transform.position;
                            soul.Age = 0f;
                            soul.Life = 0.2f;
                        }
                        else
                        {
                            soul.Life = 0f;
                        }
                    }
                    break;
                case SimEventType.BossSpawned:
                    CameraShake.Add(0.6f);
                    break;
                case SimEventType.RoomStarted:
                    foreach (var t in _telegraphs) { Release(t.Outline); Release(t.Fill); }
                    _telegraphs.Clear();
                    break;
            }
        }

        private static NetPlayer FindPlayer(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        private void OnEnemyHit(in SimEvent e)
        {
            var flags = (HitFlags)e.Int;
            Vector2 at = e.Pos + Vector2.up * 0.45f + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.15f));
            if ((flags & HitFlags.DamageOverTime) != 0)
            {
                ShowNumber(at, Mathf.RoundToInt(e.Value).ToString(), new Color(1f, 0.6f, 0.3f), 18);
                return;
            }

            bool crit = (flags & HitFlags.Crit) != 0;
            bool shatter = (flags & HitFlags.Shatter) != 0;
            if ((flags & HitFlags.Blocked) != 0 && !shatter)
            {
                // Armour: dull sparks and a grey number tell players to freeze/stun/mark it first.
                Spark(at, 0.8f, new Color(0.7f, 0.75f, 0.85f));
                ShowNumber(at + Vector2.up * 0.3f, Mathf.RoundToInt(e.Value).ToString(), new Color(0.62f, 0.64f, 0.7f), 20);
                return;
            }
            Spark(at, shatter ? 1.8f : crit ? 1.3f : 0.9f, shatter ? new Color(0.7f, 0.9f, 1f) : Color.white);
            var color = shatter ? new Color(0.55f, 0.9f, 1f) : crit ? new Color(1f, 0.85f, 0.3f) : Color.white;
            int size = shatter ? 40 : crit ? 32 : 24;
            ShowNumber(at + Vector2.up * 0.3f, shatter ? $"砕! {Mathf.RoundToInt(e.Value)}" : Mathf.RoundToInt(e.Value) + (crit ? "!" : ""), color, size);
            if (shatter) CameraShake.Add(0.25f);
            else if ((flags & HitFlags.Heavy) != 0) CameraShake.Add(0.04f);
        }

        private void OnArea(in SimEvent e)
        {
            float r = e.Value;
            switch ((AreaKind)e.Int)
            {
                case AreaKind.FrostNova:
                {
                    var fx = Spawn("fx_nova", e.Pos, 1f, 0.45f, r * 0.3f, r, Color.white);
                    fx.FadeOut = true;
                    AddLight(fx, new Color(0.5f, 0.8f, 1f), 1.6f, r + 1.5f);
                    for (int i = 0; i < 8; i++) Shard(e.Pos, i * 45f + 20f, new Color(0.7f, 0.9f, 1f));
                    break;
                }
                case AreaKind.Heal:
                {
                    var fx = Spawn("fx_heal", e.Pos + Vector2.up * 0.2f, 1f, 0.6f, r * 0.2f, r, Color.white);
                    fx.FadeOut = true;
                    AddLight(fx, new Color(0.5f, 1f, 0.6f), 1.3f, r + 1f);
                    break;
                }
                case AreaKind.WarCry:
                {
                    var fx = Spawn("fx_warcry", e.Pos + Vector2.up * 0.3f, 1f, 0.4f, r * 0.2f, r * 1.1f, new Color(1f, 0.85f, 0.5f, 0.7f));
                    fx.FadeOut = true;
                    AddLight(fx, new Color(1f, 0.8f, 0.4f), 1.5f, r + 1f);
                    CameraShake.Add(0.2f);
                    break;
                }
                case AreaKind.Mark:
                {
                    var fx = Spawn("fx_circle", e.Pos, 1f, 0.7f, r * 0.6f, r, new Color(1f, 0.68f, 0.25f, 0.55f), order: -300);
                    fx.FadeOut = true;
                    var icon = Spawn("fx_mark", e.Pos + Vector2.up * 0.5f, 1f, 0.7f, 2.5f, 1.2f, new Color(1f, 0.7f, 0.3f));
                    icon.FadeOut = true;
                    break;
                }
                case AreaKind.Shatter:
                {
                    for (int i = 0; i < 10; i++) Shard(e.Pos + Vector2.up * 0.4f, i * 36f + Random.Range(0f, 20f), new Color(0.8f, 0.95f, 1f));
                    var fx = Spawn("fx_nova", e.Pos + Vector2.up * 0.4f, 1f, 0.3f, 0.3f, r, new Color(1f, 1f, 1f, 0.8f));
                    fx.FadeOut = true;
                    AddLight(fx, new Color(0.6f, 0.9f, 1f), 2f, r + 2f);
                    break;
                }
                case AreaKind.Slam:
                {
                    Burst("fx_puff", e.Pos, 14f, r * 0.9f, new Color(0.8f, 0.7f, 0.65f));
                    var ring = Spawn("fx_circle", e.Pos, 1f, 0.35f, r * 0.5f, r * 1.05f, new Color(1f, 0.5f, 0.3f, 0.8f), order: -300);
                    ring.FadeOut = true;
                    CameraShake.Add(0.3f);
                    break;
                }
                case AreaKind.Explosion:
                {
                    var s = SpriteSheet.Load("fx_explosion");
                    int frames = s != null && s.Clips.Count > 0 ? s.Clips[0].Frames : 8;
                    var boom = Spawn("fx_explosion", e.Pos + Vector2.up * 0.3f, 18f, frames / 18f, r * 0.75f, r * 0.75f, Color.white);
                    boom.Renderer.sortingOrder = 2700;
                    AddLight(boom, new Color(1f, 0.55f, 0.2f), 3f, r + 3f);
                    var scorch = Spawn("fx_circle", e.Pos, 1f, 0.5f, r * 0.6f, r, new Color(1f, 0.45f, 0.15f, 0.7f), order: -300);
                    scorch.FadeOut = true;
                    for (int i = 0; i < 8; i++) Ember(e.Pos, i * 45f + Random.Range(0f, 30f));
                    CameraShake.Add(0.5f);
                    break;
                }
                case AreaKind.Sanctuary:
                {
                    // Holy ground: a golden circle that stays as long as it protects, and a column of light.
                    float life = Mathf.Max(0.5f, e.Size.x);
                    var ground = Spawn("fx_circle", e.Pos, 0f, life, r, r, new Color(1f, 0.85f, 0.45f, 0.22f), order: -320);
                    ground.FadeOut = true;
                    var ring = Spawn("fx_revive_ring", e.Pos, 0f, life, r, r, new Color(1f, 0.9f, 0.55f, 0.75f), order: -319);
                    ring.FadeOut = true;
                    AddLight(ring, new Color(1f, 0.85f, 0.5f), 1.1f, r + 1.5f);
                    var column = Spawn("fx_holy", e.Pos + Vector2.up * 0.6f, 12f, 0.9f, 1.4f, 2f, new Color(1f, 0.95f, 0.75f));
                    column.FadeOut = true;
                    break;
                }
                case AreaKind.Smoke:
                {
                    for (int i = 0; i < 9; i++)
                    {
                        Vector2 at = e.Pos + Random.insideUnitCircle * r * 0.7f;
                        Burst("fx_puff", at + Vector2.up * 0.3f, Random.Range(7f, 11f), r * Random.Range(0.5f, 0.8f), new Color(0.45f, 0.42f, 0.5f, 0.9f));
                    }
                    var ring = Spawn("fx_circle", e.Pos, 1f, 0.5f, r * 0.4f, r, new Color(0.35f, 0.3f, 0.45f, 0.5f), order: -300);
                    ring.FadeOut = true;
                    break;
                }
                case AreaKind.ShadowStep:
                {
                    Burst("fx_puff", e.Pos + Vector2.up * 0.4f, 16f, 1.4f, new Color(0.45f, 0.3f, 0.7f));
                    var slash = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.3f, 0.3f, r, new Color(0.75f, 0.55f, 1f, 0.8f));
                    slash.FadeOut = true;
                    AddLight(slash, new Color(0.6f, 0.4f, 1f), 1.6f, r + 1.5f);
                    break;
                }
                case AreaKind.FirePool:
                {
                    // Burning ground: scorched circle, a few licking flames, warm light, for as long as it burns.
                    float life = Mathf.Max(0.5f, e.Size.x);
                    var scorch = Spawn("fx_circle", e.Pos, 0f, life, r, r, new Color(1f, 0.4f, 0.12f, 0.3f), order: -320);
                    scorch.FadeOut = true;
                    AddLight(scorch, new Color(1f, 0.5f, 0.2f), 1.6f, r + 2f);
                    int flames = 4 + Mathf.RoundToInt(r * 2f);
                    for (int i = 0; i < flames; i++)
                    {
                        float a = i / (float)flames * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                        Vector2 at = e.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * r * Random.Range(0.2f, 0.8f);
                        var flame = Spawn("prop_campfire", at, Random.Range(9f, 12f), life, 0.45f, 0.3f, Color.white, order: EnemyView.SortOrder(at.y));
                        flame.FadeOut = true;
                    }
                    break;
                }
                case AreaKind.Potion:
                {
                    var fx = Spawn("fx_heal", e.Pos + Vector2.up * 0.2f, 1f, 0.6f, r * 0.3f, r, Color.white);
                    fx.FadeOut = true;
                    AddLight(fx, new Color(0.5f, 1f, 0.6f), 1.4f, r + 1f);
                    for (int i = 0; i < 6; i++) Shard(e.Pos + Vector2.up * 0.3f, i * 60f + 15f, new Color(0.6f, 1f, 0.65f));
                    break;
                }
                case AreaKind.FlaskBurst:
                {
                    var boom = Spawn("fx_explosion", e.Pos + Vector2.up * 0.2f, 22f, 0.36f, r * 0.45f, r * 0.55f, new Color(0.85f, 1f, 0.55f));
                    boom.Renderer.sortingOrder = 2700;
                    AddLight(boom, new Color(0.7f, 1f, 0.4f), 1.4f, r + 1f);
                    break;
                }
                case AreaKind.HolyLight:
                {
                    var fx = Spawn("fx_holy", e.Pos + Vector2.up * 0.6f, 12f, 1.2f, 1.5f, 2.2f, Color.white);
                    fx.FadeOut = true;
                    AddLight(fx, new Color(0.6f, 1f, 0.75f), 2.2f, r + 2f);
                    var ring = Spawn("fx_heal", e.Pos, 1f, 0.8f, r * 0.3f, r, new Color(1f, 1f, 1f, 0.8f));
                    ring.FadeOut = true;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Room hazards

        private void OnHazardWarn(in SimEvent e)
        {
            switch ((GimmickKind)e.Int)
            {
                case GimmickKind.SpikeTrap:
                    AddTelegraph(new SimEvent { Id = HazardKey(e.Id), Int = (int)AttackShape.Circle, Pos = e.Pos, Size = e.Size, Value = e.Value }, square: true);
                    break;
                case GimmickKind.FlameJet:
                    AddTelegraph(new SimEvent { Id = HazardKey(e.Id), Int = (int)AttackShape.Line, Pos = e.Pos, Vec = e.Vec, Size = e.Size, Value = e.Value });
                    break;
                case GimmickKind.PowderKeg:
                {
                    AddTelegraph(new SimEvent { Id = HazardKey(e.Id), Int = (int)AttackShape.Circle, Pos = e.Pos, Size = e.Size, Value = e.Value });
                    // The fuse: a sputtering spark where the keg was.
                    var fuse = Spawn("fx_hit", e.Pos + Vector2.up * 0.5f, 30f, e.Value, 0.7f, 0.4f, new Color(1f, 0.8f, 0.3f));
                    fuse.Renderer.sortingOrder = 2800;
                    AddLight(fuse, new Color(1f, 0.6f, 0.2f), 1.2f, 1.8f);
                    break;
                }
            }
        }

        private void OnHazardFire(in SimEvent e)
        {
            RemoveTelegraph(HazardKey(e.Id));
            switch ((GimmickKind)e.Int)
            {
                case GimmickKind.FlameJet:
                {
                    var fx = Spawn("fx_flame", e.Pos, 16f, e.Value, 1f, 1f, Color.white, order: EnemyView.SortOrder(e.Pos.y) + 10);
                    fx.Go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(e.Vec.y, e.Vec.x) * Mathf.Rad2Deg);
                    fx.Go.transform.localScale = new Vector3(e.Size.x / 2f, e.Size.y / 0.5f, 1f);
                    fx.StartScale = fx.EndScale = 0f; // keep the stretched scale
                    AddLight(fx, new Color(1f, 0.55f, 0.2f), 2.2f, 3.5f);
                    fx.Light.transform.localPosition = new Vector3(1f, 0f);
                    break;
                }
                case GimmickKind.SpikeTrap:
                    Burst("fx_puff", e.Pos, 20f, 0.8f, new Color(0.7f, 0.65f, 0.6f));
                    break;
            }
        }

        private void Ember(Vector2 at, float angle)
        {
            var fx = Spawn("fx_shard", at, 0f, 0.6f, 1f, 0.5f, new Color(1f, 0.6f, 0.2f));
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            fx.Velocity = dir * Random.Range(4f, 8f);
            fx.Go.transform.rotation = Quaternion.Euler(0f, 0f, angle - 45f);
            fx.FadeOut = true;
            fx.Renderer.sortingOrder = 2600;
        }

        // ------------------------------------------------------------------ Projectiles

        private void SpawnProjectile(in SimEvent e)
        {
            var kind = (ProjectileKind)e.Int;
            float angle = Mathf.Atan2(e.Vec.y, e.Vec.x) * Mathf.Rad2Deg;
            Fx fx;
            switch (kind)
            {
                case ProjectileKind.Bolt:
                    fx = Spawn("fx_bolt", e.Pos, 16f, e.Value + 0.05f, 1f, 1f, Color.white);
                    AddLight(fx, new Color(0.45f, 0.75f, 1f), 1.1f, 1.6f);
                    break;
                case ProjectileKind.Arrow:
                    fx = Spawn("fx_arrow", e.Pos, 0f, e.Value + 0.05f, 1f, 1f, Color.white);
                    break;
                case ProjectileKind.PierceArrow:
                    fx = Spawn("fx_arrow", e.Pos, 0f, e.Value + 0.05f, 1.6f, 1.6f, new Color(1f, 0.85f, 0.4f));
                    AddLight(fx, new Color(1f, 0.8f, 0.4f), 1.4f, 2f);
                    break;
                case ProjectileKind.SwordWave:
                    fx = Spawn("fx_nova", e.Pos, 0f, e.Value + 0.05f, 0.5f, 0.9f, new Color(0.9f, 0.95f, 1f, 0.8f));
                    fx.Renderer.sprite = GameArt.Instance.Arc;
                    angle -= 90f;
                    break;
                case ProjectileKind.BloodOrb:
                    fx = Spawn("fx_orb", e.Pos, 12f, e.Value + 0.05f, 1f, 1f, new Color(1f, 0.35f, 0.35f));
                    AddLight(fx, new Color(1f, 0.15f, 0.15f), 1f, 1.5f);
                    break;
                case ProjectileKind.SoulOrb:
                    fx = Spawn("fx_orb", e.Pos, 12f, e.Value + 0.05f, 1f, 1f, new Color(0.55f, 0.95f, 1f));
                    AddLight(fx, new Color(0.4f, 0.8f, 1f), 1f, 1.5f);
                    break;
                case ProjectileKind.Flask:
                case ProjectileKind.FireFlask:
                case ProjectileKind.HealFlask:
                {
                    // A tumbling bottle (the sheet's frames spin it); lobbed ones are bigger.
                    var tint = kind == ProjectileKind.FireFlask ? new Color(1f, 0.6f, 0.35f) : kind == ProjectileKind.HealFlask ? new Color(0.6f, 1f, 0.65f) : Color.white;
                    float size = kind == ProjectileKind.Flask ? 1f : 1.25f;
                    fx = Spawn("fx_flask", e.Pos, 16f, e.Value + 0.05f, size, size, tint);
                    AddLight(fx, kind == ProjectileKind.FireFlask ? new Color(1f, 0.5f, 0.2f) : new Color(0.6f, 1f, 0.5f), 0.9f, 1.4f);
                    angle = 0f;
                    break;
                }
                default:
                    fx = Spawn("fx_bonearrow", e.Pos, 0f, e.Value + 0.05f, 1f, 1f, Color.white);
                    AddLight(fx, new Color(1f, 0.25f, 0.2f), 0.6f, 1f);
                    break;
            }
            fx.Velocity = e.Vec;
            fx.Go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _projectiles[e.Id] = fx;
        }

        // ------------------------------------------------------------------ Telegraphs

        private void AddTelegraph(in SimEvent e, bool square = false)
        {
            var shape = (AttackShape)e.Int;
            string sheet = square ? "fx_square" : shape switch
            {
                AttackShape.Cone => "fx_cone",
                AttackShape.Line => "fx_line",
                _ => "fx_circle",
            };
            var outline = Spawn(sheet, e.Pos, 0f, e.Value, 1f, 1f, new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.45f), order: -300);
            var fill = Spawn(sheet, e.Pos, 0f, e.Value, 1f, 1f, new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.32f), order: -299);
            var t = new Telegraph
            {
                Outline = outline, Fill = fill, Shape = shape, Origin = e.Pos, Direction = e.Vec, Size = e.Size,
                Duration = Mathf.Max(0.05f, e.Value), EnemyId = e.Id, Square = square,
            };
            _telegraphs.Add(t);
            LayoutTelegraph(t);
        }

        private void RemoveTelegraph(int enemyId)
        {
            for (int i = _telegraphs.Count - 1; i >= 0; i--)
            {
                if (_telegraphs[i].EnemyId != enemyId) continue;
                Release(_telegraphs[i].Outline);
                Release(_telegraphs[i].Fill);
                _telegraphs.RemoveAt(i);
            }
        }

        /// <summary>Scales the generic shape sprites to the attack's real size; the fill grows as the strike approaches.</summary>
        private void LayoutTelegraph(Telegraph t)
        {
            float k = Mathf.Clamp01(t.Age / t.Duration);
            float angle = Mathf.Atan2(t.Direction.y, t.Direction.x) * Mathf.Rad2Deg;
            Vector2 origin = t.Origin;
            if (t.Shape == AttackShape.SelfCircle && EnemyView.Instance != null && EnemyView.Instance.TryGetPosition(t.EnemyId, out var pos)) origin = pos;

            void Place(Fx fx, float scaleK)
            {
                var tr = fx.Go.transform;
                tr.position = origin;
                if (t.Square)
                {
                    // fx_square is one unit across.
                    tr.rotation = Quaternion.identity;
                    tr.localScale = new Vector3(t.Size.x, t.Size.y, 1f) * scaleK;
                    return;
                }
                switch (t.Shape)
                {
                    case AttackShape.Cone:
                    {
                        // Sprite: 90° sector of radius 2 units. Stretch Y so the sector spans the attack's angle.
                        float r = t.Size.x * scaleK;
                        float spread = Mathf.Tan(Mathf.Min(170f, t.Size.y) * 0.5f * Mathf.Deg2Rad);
                        tr.rotation = Quaternion.Euler(0f, 0f, angle);
                        tr.localScale = new Vector3(r / 2f, r / 2f * Mathf.Clamp(spread, 0.2f, 3f), 1f);
                        break;
                    }
                    case AttackShape.Line:
                        tr.rotation = Quaternion.Euler(0f, 0f, angle);
                        tr.localScale = new Vector3(t.Size.x / 2f * scaleK, t.Size.y / 0.25f, 1f);
                        break;
                    default:
                        tr.rotation = Quaternion.identity;
                        tr.localScale = Vector3.one * t.Size.x * scaleK;
                        break;
                }
            }

            Place(t.Outline, 1f);
            Place(t.Fill, Mathf.Lerp(0.1f, 1f, k));
            float pulse = 0.35f + 0.2f * Mathf.Sin(Time.time * 18f) * k;
            t.Outline.Renderer.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, pulse);
        }

        // ------------------------------------------------------------------ Small effects

        private void Spark(Vector2 at, float scale, Color color)
        {
            var fx = Spawn("fx_hit", at, 24f, 5f / 24f, scale, scale, color);
            fx.Renderer.sortingOrder = 3000;
        }

        private void Burst(string sheet, Vector2 at, float fps, float scale, Color color)
        {
            var s = SpriteSheet.Load(sheet);
            int frames = s != null && s.Clips.Count > 0 ? s.Clips[0].Frames : 6;
            Spawn(sheet, at, fps, frames / fps, scale, scale, color).Renderer.sortingOrder = 2500;
        }

        private void Shard(Vector2 at, float angle, Color color)
        {
            var fx = Spawn("fx_shard", at, 0f, 0.4f, 1f, 0.6f, color);
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            fx.Velocity = dir * Random.Range(3f, 6f);
            fx.Go.transform.rotation = Quaternion.Euler(0f, 0f, angle - 45f);
            fx.FadeOut = true;
            fx.Renderer.sortingOrder = 2600;
        }

        private Fx Spawn(string sheet, Vector2 at, float fps, float life, float startScale, float endScale, Color color, int order = 2000)
        {
            Fx fx = _pool.Count > 0 ? _pool.Pop() : CreateFx();
            fx.Go.SetActive(true);
            var s = SpriteSheet.Load(sheet);
            fx.Frames = s != null ? s.Sprites(s.Clips[0].Name) : null;
            fx.Fps = fps;
            fx.Age = 0f;
            fx.Life = life;
            fx.Velocity = Vector2.zero;
            fx.StartScale = startScale;
            fx.EndScale = endScale;
            fx.Color = color;
            fx.FadeOut = false;
            fx.Follow = null;
            fx.Go.transform.SetPositionAndRotation(at, Quaternion.identity);
            fx.Go.transform.localScale = Vector3.one * startScale;
            fx.Renderer.sprite = fx.Frames != null && fx.Frames.Length > 0 ? fx.Frames[0] : null;
            fx.Renderer.color = color;
            fx.Renderer.sortingOrder = order;
            if (fx.Light != null) fx.Light.enabled = false;
            _effects.Add(fx);
            return fx;
        }

        private static void AddLight(Fx fx, Color color, float intensity, float radius)
        {
            if (fx.Light == null)
            {
                var go = new GameObject("Light");
                go.transform.SetParent(fx.Go.transform, false);
                fx.Light = go.AddComponent<Light2D>();
                fx.Light.lightType = Light2D.LightType.Point;
                fx.Light.pointLightInnerRadius = 0f;
            }
            fx.Light.enabled = true;
            fx.Light.color = color;
            fx.Light.intensity = intensity;
            fx.Light.pointLightOuterRadius = radius;
        }

        private Fx CreateFx()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            return new Fx { Go = go, Renderer = go.AddComponent<SpriteRenderer>() };
        }

        private void Release(Fx fx)
        {
            fx.Life = -1f; // collected in Update
        }

        private void ShowNumber(Vector2 world, string text, Color color, int size)
        {
            if (_numbers.Count >= MaxNumbers) return;
            var label = _freeTexts.Count > 0 ? _freeTexts.Pop() : UIKit.Label(_numberLayer, "", 24, TextAnchor.MiddleCenter);
            label.gameObject.SetActive(true);
            label.font = UIKit.TitleFont;
            label.rectTransform.sizeDelta = new Vector2(220f, 60f);
            label.text = text;
            label.fontSize = size;
            label.color = color;
            _numbers.Add(new Number { Text = label, World = world, Life = size > 30 ? 0.9f : 0.65f });
        }

        // ------------------------------------------------------------------ Frame

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_camera == null) _camera = Camera.main;

            for (int i = _telegraphs.Count - 1; i >= 0; i--)
            {
                var t = _telegraphs[i];
                t.Age += dt;
                if (t.Age > t.Duration + 0.1f)
                {
                    Release(t.Outline);
                    Release(t.Fill);
                    _telegraphs.RemoveAt(i);
                    continue;
                }
                LayoutTelegraph(t);
            }

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var fx = _effects[i];
                fx.Age += dt;
                if (fx.Life < 0f || fx.Age >= fx.Life)
                {
                    fx.Go.SetActive(false);
                    _effects.RemoveAt(i);
                    _pool.Push(fx);
                    continue;
                }

                float k = fx.Life > 0f && fx.Life < 999f ? fx.Age / fx.Life : 0f;
                var tr = fx.Go.transform;
                if (fx.Follow != null)
                {
                    tr.position = Vector3.Lerp(fx.FollowFrom, fx.Follow.position + Vector3.up * 0.5f, k);
                }
                else
                {
                    tr.position += (Vector3)(fx.Velocity * dt);
                    if (fx.StartScale != fx.EndScale) tr.localScale = Vector3.one * Mathf.Lerp(fx.StartScale, fx.EndScale, Mathf.Sqrt(k));
                }

                if (fx.Frames != null && fx.Frames.Length > 1 && fx.Fps > 0f)
                {
                    int frame = Mathf.FloorToInt(fx.Age * fx.Fps);
                    fx.Renderer.sprite = fx.Frames[fx.Life < 999f && fx.Life * fx.Fps <= fx.Frames.Length + 0.5f ? Mathf.Min(frame, fx.Frames.Length - 1) : frame % fx.Frames.Length];
                }
                if (fx.FadeOut)
                {
                    var c = fx.Color;
                    c.a *= 1f - k;
                    fx.Renderer.color = c;
                    if (fx.Light != null && fx.Light.enabled) fx.Light.intensity *= 1f - dt * 3f;
                }
            }

            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                var n = _numbers[i];
                n.Age += dt;
                if (n.Age >= n.Life || _camera == null)
                {
                    n.Text.gameObject.SetActive(false);
                    _freeTexts.Push(n.Text);
                    _numbers.RemoveAt(i);
                    continue;
                }
                float rise = 0.9f * (1f - Mathf.Pow(1f - n.Age / n.Life, 2f));
                n.Text.transform.position = _camera.WorldToScreenPoint(n.World + Vector3.up * rise);
                var c = n.Text.color;
                c.a = n.Age < n.Life * 0.6f ? 1f : 1f - (n.Age - n.Life * 0.6f) / (n.Life * 0.4f);
                n.Text.color = c;
            }

            UpdateDoors();
        }

        // ------------------------------------------------------------------ Reward doors

        /// <summary>One open exit: a portal tinted by its reward, a gathering ring, the reward medallion and its name.</summary>
        private sealed class DoorFx
        {
            public Fx Portal;
            public Fx Ring;
            public Fx Icon;
            public Fx Skull;
            public Text Label;
            public Vector2 Position;
            public Color Color;
        }

        private readonly List<DoorFx> _doors = new();
        private int _doorsKey;

        private Fx SpawnIcon(Sprite sprite, Vector2 at, float scale, int order)
        {
            var fx = Spawn("icons_reward", at, 0f, 99999f, scale, scale, Color.white, order);
            if (sprite != null)
            {
                fx.Frames = new[] { sprite };
                fx.Renderer.sprite = sprite;
            }
            return fx;
        }

        private void ClearDoors()
        {
            foreach (var d in _doors)
            {
                Release(d.Portal);
                Release(d.Ring);
                Release(d.Icon);
                if (d.Skull != null) Release(d.Skull);
                Destroy(d.Label.gameObject);
            }
            _doors.Clear();
        }

        private void UpdateDoors()
        {
            var director = RunDirector.Instance;
            bool open = director != null && director.Phase.Value == RunPhase.Playing;
            var doors = open ? director.Doors : System.Array.Empty<Door>();
            int key = doors.Length == 0 ? 0 : director.DoorsPacked.Value;
            if (key != _doorsKey)
            {
                ClearDoors();
                _doorsKey = key;
                int area = director != null ? (director.RoomIndex.Value + 1) / RunSimulation.RoomsPerArea : 0;
                for (int i = 0; i < doors.Length; i++) _doors.Add(CreateDoor(doors[i], RunDirector.DoorPosition(i, doors.Length), area));
            }
            if (_doors.Count == 0) return;

            int held = director.HoldDoor.Value;
            float hold = Mathf.Clamp01(director.ExitHold.Value / RunSimulation.ExitHoldTime);
            for (int i = 0; i < _doors.Count; i++)
            {
                var d = _doors[i];
                bool chosen = i == held;
                float pulse = 1.6f + 0.08f * Mathf.Sin(Time.time * 4f + i * 1.7f) + (chosen ? 0.25f * hold : 0f);
                d.Portal.Go.transform.localScale = Vector3.one * pulse;
                d.Portal.StartScale = d.Portal.EndScale = pulse;
                if (d.Portal.Light != null) d.Portal.Light.intensity = chosen ? 1.8f + 1.6f * hold : 1.5f;

                var ringColor = d.Color;
                ringColor.a = chosen ? 0.45f + 0.4f * hold : 0.22f + 0.06f * Mathf.Sin(Time.time * 3f + i);
                d.Ring.Renderer.color = ringColor;

                Vector2 iconAt = d.Position + new Vector2(0f, 0.2f + 0.1f * Mathf.Sin(Time.time * 2.2f + i * 2f));
                d.Icon.Go.transform.position = iconAt;
                if (d.Skull != null) d.Skull.Go.transform.position = iconAt + new Vector2(0.55f, 0.45f);

                if (_camera != null)
                {
                    d.Label.transform.position = _camera.WorldToScreenPoint(d.Position + new Vector2(0f, -1.75f));
                    var c = d.Label.color;
                    c.a = chosen ? 1f : 0.88f;
                    d.Label.color = c;
                }
            }
        }

        private DoorFx CreateDoor(Door door, Vector2 at, int area)
        {
            var color = RewardCatalog.Color(door.Reward);
            if (door.Trial) color = Color.Lerp(color, RewardCatalog.TrialColor, 0.55f);
            var d = new DoorFx { Position = at, Color = color };

            d.Ring = Spawn("fx_circle", at, 1f, 99999f, RunSimulation.ExitRadius, RunSimulation.ExitRadius, color, order: -360);
            d.Portal = Spawn("fx_portal", at, 8f, 99999f, 1.6f, 1.6f, Color.Lerp(Color.white, color, 0.6f), order: -350);
            AddLight(d.Portal, color, 1.5f, 3.6f);
            d.Icon = SpawnIcon(GameIcons.Reward(door.Reward), at + Vector2.up * 0.2f, 1.6f, 2600);
            if (door.Trial) d.Skull = SpawnIcon(GameIcons.Trial, at + new Vector2(0.55f, 0.65f), 1f, 2601);

            d.Label = UIKit.Label(_numberLayer, "", 32, TextAnchor.UpperCenter);
            d.Label.font = UIKit.TitleFont;
            d.Label.rectTransform.sizeDelta = new Vector2(380f, 110f);
            d.Label.rectTransform.pivot = new Vector2(0.5f, 1f);
            string trial = door.Trial ? "<color=#ff6a5a>試練 </color>" : "";
            d.Label.text = $"{trial}{RewardCatalog.Name(door.Reward)}\n<size=22><color=#e8e0d4>{RewardCatalog.Describe(door.Reward, area, door.Trial)}</color></size>";
            d.Label.color = color;
            var shadow = d.Label.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return d;
        }
    }
}
