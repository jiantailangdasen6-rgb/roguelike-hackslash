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
    public sealed partial class EventPresenter : MonoBehaviour
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
            public bool Pop; // callouts: burst in large, settle, drift up slowly
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
                    Remains(e); // EventPresenter.Remains.cs
                    if (e.Value < 0.5f && EnemyCatalog.Get((EnemyType)e.Int).Boss) BossFell(e.Pos);
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
                    if (IsLocal(e.ClientId))
                    {
                        CameraShake.Add(0.35f);
                        HitStop.Freeze(0.04f);
                    }
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
                case SimEventType.Combo: OnCombo(e); break;
                case SimEventType.EliteAffixes: OnEliteAffixes(e); break; // EventPresenter.Affixes.cs
                case SimEventType.EliteShield: OnEliteShield(e); break;
                case SimEventType.TrialEvent: OnTrial(e); break;
                case SimEventType.GodSign: OnGodSign(e); break; // EventPresenter.Gods.cs
                case SimEventType.RoomEvent: OnRoomEvent(e); break; // EventPresenter.Gods.cs
                case SimEventType.KeyGained:
                {
                    var glint = Spawn("fx_holy", e.Pos + Vector2.up * 0.8f, 12f, 1f, 1.4f, 2.6f, new Color(1f, 0.85f, 0.45f));
                    glint.FadeOut = true;
                    AddLight(glint, new Color(1f, 0.8f, 0.4f), 2.5f, 5f);
                    ShowCallout(e.Pos + Vector2.up * 2f, "鍵！", new Color(1f, 0.85f, 0.45f), 46);
                    break;
                }
                case SimEventType.BellRung:
                {
                    // Rings of sound rolling out from the bell.
                    for (int i = 0; i < 3; i++)
                    {
                        var ring = Spawn("fx_circle", e.Pos + Vector2.up * 1.2f, 1f, 0.9f + i * 0.25f, 0.4f + i * 0.3f, 4.5f + i, new Color(1f, 0.8f, 0.45f, 0.6f), order: -330);
                        ring.FadeOut = true;
                    }
                    ShowCallout(e.Pos + Vector2.up * 3f, "カーン！", new Color(1f, 0.85f, 0.5f), 40);
                    CameraShake.Add(0.12f);
                    break;
                }
                case SimEventType.DoorOpened when e.Int is 2 or 3:
                    for (int i = 0; i < 10; i++) Burst("fx_puff", e.Pos + Random.insideUnitCircle * 1.2f, Random.Range(8f, 12f), Random.Range(0.8f, 1.4f), new Color(0.7f, 0.65f, 0.6f, 0.85f));
                    CameraShake.Add(0.3f);
                    break;
                case SimEventType.PuzzleEnded when e.Int == 1:
                {
                    var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.5f, 1f, 0.6f, 0.5f, 7f, new Color(0.6f, 0.95f, 1f, 0.85f));
                    flash.FadeOut = true;
                    AddLight(flash, new Color(0.5f, 0.9f, 1f), 3f, 8f);
                    break;
                }
                case SimEventType.RoomStarted:
                    HitStop.Reset();
                    _calloutAt.Clear();
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

        private static readonly Color ShatterColor = new(0.55f, 0.9f, 1f);
        private static readonly Color TeamworkColor = new(1f, 0.78f, 0.3f);
        private static readonly Color ArmorBreakColor = new(1f, 0.55f, 0.25f);

        // Callouts are throttled so a busy fight never fills the screen with words.
        private readonly Dictionary<int, float> _calloutAt = new();
        private float _lastTeamworkCallout = -9f;

        private static bool IsLocal(ulong clientId) => NetPlayer.Local != null && NetPlayer.Local.OwnerClientId == clientId;

        private bool CalloutReady(int enemyId, float spacing)
        {
            if (_calloutAt.TryGetValue(enemyId, out float last) && Time.time - last < spacing) return false;
            _calloutAt[enemyId] = Time.time;
            if (_calloutAt.Count > 64) _calloutAt.Clear();
            return true;
        }

        private void OnEnemyHit(in SimEvent e)
        {
            var flags = HitInfo.Flags(e.Int);
            bool mine = HitInfo.TryAttacker(e.Int, out ulong attacker) && IsLocal(attacker);
            Vector2 at = e.Pos + Vector2.up * 0.45f + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.15f));
            if ((flags & HitFlags.DamageOverTime) != 0)
            {
                ShowNumber(at, Mathf.RoundToInt(e.Value).ToString(), new Color(1f, 0.6f, 0.3f), 18);
                return;
            }

            bool crit = (flags & HitFlags.Crit) != 0;
            bool shatter = (flags & HitFlags.Shatter) != 0;
            bool heavy = (flags & HitFlags.Heavy) != 0;
            bool teamwork = (flags & HitFlags.Teamwork) != 0;
            bool armorBreak = (flags & HitFlags.ArmorBreak) != 0;
            if ((flags & HitFlags.Blocked) != 0 && !shatter)
            {
                // Armour: dull sparks and a grey number tell players to freeze/stun/mark it first.
                Spark(at, 0.8f, new Color(0.7f, 0.75f, 0.85f));
                // A warded boss takes nothing at all: say so (now and then), the trial is the way through.
                if (e.Value < 0.5f) { if (CalloutReady(e.Id, 0.5f)) ShowNumber(at + Vector2.up * 0.3f, "無効", new Color(0.75f, 0.8f, 0.9f), 22); }
                else ShowNumber(at + Vector2.up * 0.3f, Mathf.RoundToInt(e.Value).ToString(), new Color(0.62f, 0.64f, 0.7f), 20);
                if (mine && heavy) HitStop.Freeze(0.03f, e.Id);
                return;
            }

            if (shatter)
            {
                // The signature combo: everyone's screen holds for a beat, ice bursts and the word pops.
                Spark(at, 2.2f, new Color(0.7f, 0.9f, 1f));
                var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.4f, 1f, 0.22f, 0.4f, 2.6f, new Color(0.85f, 0.95f, 1f, 0.9f));
                flash.FadeOut = true;
                AddLight(flash, new Color(0.6f, 0.9f, 1f), 3.2f, 5f);
                if (CalloutReady(e.Id, 0.6f)) ShowCallout(e.Pos + Vector2.up * 1.6f, "砕き！", ShatterColor, 46);
                ShowNumber(at + Vector2.up * 0.2f, Mathf.RoundToInt(e.Value).ToString(), ShatterColor, 30);
                HitStop.Freeze(mine ? 0.1f : 0.07f, e.Id, major: true);
                CameraShake.Add(0.3f);
                return;
            }

            Color sparkColor = armorBreak ? ArmorBreakColor : teamwork ? TeamworkColor : Color.white;
            Spark(at, crit ? 1.3f : heavy ? 1.1f : 0.9f, sparkColor);
            var color = crit ? new Color(1f, 0.85f, 0.3f) : Color.white;
            int size = crit ? 32 : 24;
            ShowNumber(at + Vector2.up * 0.3f, Mathf.RoundToInt(e.Value) + (crit ? "!" : ""), color, size);

            if (armorBreak && CalloutReady(e.Id, 3f))
            {
                // Someone broke the knight's guard: tell the whole team the window is open.
                ShowCallout(e.Pos + Vector2.up * 1.7f, "鎧崩し！", ArmorBreakColor, 38);
                for (int i = 0; i < 6; i++) Shard(e.Pos + Vector2.up * 0.5f, i * 60f + Random.Range(0f, 25f), ArmorBreakColor);
                HitStop.Freeze(0.06f, e.Id, major: true);
                CameraShake.Add(0.15f);
            }
            else if (teamwork && mine && Time.time - _lastTeamworkCallout > 0.5f && CalloutReady(e.Id, 2.5f))
            {
                // You cashed in a teammate's freeze, stun or mark.
                _lastTeamworkCallout = Time.time;
                ShowCallout(e.Pos + Vector2.up * 1.5f, "連携！", TeamworkColor, 32);
            }

            if (mine && heavy) HitStop.Freeze(crit ? 0.065f : 0.045f, e.Id);
            if (heavy) CameraShake.Add(mine ? 0.07f : 0.03f);
        }

        /// <summary>Two classes' abilities met: the name of the technique, a burst in its colours, a beat of hit stop.</summary>
        private void OnCombo(in SimEvent e)
        {
            if ((ComboKind)e.Int == ComboKind.Union)
            {
                OnUnion(e); // EventPresenter.Unions.cs
                return;
            }
            float r = Mathf.Max(1f, e.Value);
            switch ((ComboKind)e.Int)
            {
                case ComboKind.SteamBurst:
                {
                    for (int i = 0; i < 12; i++)
                    {
                        Vector2 at = e.Pos + Random.insideUnitCircle * r * 0.8f;
                        Burst("fx_puff", at + Vector2.up * 0.4f, Random.Range(8f, 12f), r * Random.Range(0.5f, 0.9f), new Color(0.92f, 0.95f, 1f, 0.9f));
                    }
                    var ring = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.4f, 0.4f, r * 1.1f, new Color(0.85f, 0.95f, 1f, 0.8f));
                    ring.FadeOut = true;
                    AddLight(ring, new Color(0.75f, 0.9f, 1f), 2.6f, r + 2f);
                    ShowCallout(e.Pos + Vector2.up * 1.8f, "蒸気爆発！", new Color(0.8f, 0.92f, 1f), 44);
                    HitStop.Freeze(0.09f, -1, major: true);
                    CameraShake.Add(0.4f);
                    break;
                }
                case ComboKind.ShadowExecution:
                {
                    var slash = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.35f, 0.3f, r * 1.2f, new Color(1f, 0.45f, 0.35f, 0.85f));
                    slash.FadeOut = true;
                    AddLight(slash, new Color(1f, 0.35f, 0.3f), 2.4f, r + 1.5f);
                    for (int i = 0; i < 8; i++) Shard(e.Pos + Vector2.up * 0.4f, i * 45f + Random.Range(0f, 20f), new Color(0.75f, 0.4f, 1f));
                    ShowCallout(e.Pos + Vector2.up * 1.7f, "影の処刑！", new Color(0.85f, 0.55f, 1f), 42);
                    HitStop.Freeze(0.08f, -1, major: true);
                    CameraShake.Add(0.25f);
                    break;
                }
                case ComboKind.HolyWarCry:
                {
                    var pillar = Spawn("fx_holy", e.Pos + Vector2.up * 0.8f, 12f, 1.1f, 2f, 3f, new Color(1f, 0.92f, 0.65f));
                    pillar.FadeOut = true;
                    AddLight(pillar, new Color(1f, 0.85f, 0.45f), 3f, r + 2.5f);
                    var ring = Spawn("fx_revive_ring", e.Pos, 1f, 0.6f, r * 0.6f, r * 1.05f, new Color(1f, 0.9f, 0.5f, 0.9f), order: -300);
                    ring.FadeOut = true;
                    ShowCallout(e.Pos + Vector2.up * 2.2f, "聖域の鬨！", new Color(1f, 0.88f, 0.5f), 42);
                    HitStop.Freeze(0.07f, -1, major: true);
                    CameraShake.Add(0.3f);
                    break;
                }
                case ComboKind.FlameCharge:
                {
                    for (int i = 0; i < 8; i++) Ember(e.Pos, i * 45f + Random.Range(0f, 30f));
                    var flare = Spawn("fx_explosion", e.Pos + Vector2.up * 0.3f, 22f, 0.36f, 0.8f, 1.1f, new Color(1f, 0.75f, 0.45f));
                    flare.Renderer.sortingOrder = 2700;
                    AddLight(flare, new Color(1f, 0.55f, 0.2f), 2.4f, 3.5f);
                    ShowCallout(e.Pos + Vector2.up * 1.8f, "炎の突進！", new Color(1f, 0.62f, 0.3f), 42);
                    CameraShake.Add(0.2f);
                    break;
                }
            }
        }

        /// <summary>Boss trials: the name of what to do, bursts when one is overcome or failed.</summary>
        private void OnTrial(in SimEvent e)
        {
            switch ((TrialMoment)e.Int)
            {
                case TrialMoment.CageClosed:
                {
                    var ring = Spawn("fx_revive_ring", e.Pos, 1f, 0.4f, 2f, 0.9f, new Color(0.9f, 0.85f, 0.7f, 0.9f), order: -300);
                    ring.FadeOut = true;
                    CameraShake.Add(0.25f);
                    ShowCallout(e.Pos + Vector2.up * 1.9f, IsLocal(e.ClientId) ? "檻を壊せ！" : "仲間が檻に！", new Color(0.95f, 0.88f, 0.7f), 36);
                    break;
                }
                case TrialMoment.CageBroken:
                    for (int i = 0; i < 8; i++) Shard(e.Pos + Vector2.up * 0.6f, i * 45f + Random.Range(0f, 20f), new Color(0.9f, 0.86f, 0.75f));
                    break;
                case TrialMoment.CageCrushed:
                    CameraShake.Add(0.4f);
                    Burst("fx_puff", e.Pos + Vector2.up * 0.5f, 12f, 1.6f, new Color(0.9f, 0.85f, 0.75f));
                    break;
                case TrialMoment.WardUp:
                {
                    var nova = Spawn("fx_nova", e.Pos + Vector2.up * 0.8f, 1f, 0.6f, 0.5f, 6f, new Color(0.75f, 1f, 0.95f, 0.8f));
                    nova.FadeOut = true;
                    AddLight(nova, new Color(0.5f, 1f, 0.9f), 3f, 8f);
                    CameraShake.Add(0.5f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "不死の加護", new Color(0.75f, 1f, 0.95f), 46);
                    break;
                }
                case TrialMoment.RitualStart:
                {
                    var nova = Spawn("fx_nova", e.Pos + Vector2.up * 0.8f, 1f, 0.6f, 0.5f, 7f, new Color(1f, 0.3f, 0.3f, 0.8f));
                    nova.FadeOut = true;
                    AddLight(nova, new Color(1f, 0.2f, 0.2f), 3f, 9f);
                    CameraShake.Add(0.5f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "血の儀式", new Color(1f, 0.4f, 0.35f), 46);
                    break;
                }
                case TrialMoment.SigilLit:
                {
                    var pillar = Spawn("fx_holy", e.Pos + Vector2.up * 0.8f, 12f, 0.8f, 1.4f, 2.4f, new Color(1f, 0.45f, 0.35f));
                    pillar.FadeOut = true;
                    AddLight(pillar, new Color(1f, 0.3f, 0.2f), 2.2f, 4f);
                    break;
                }
                case TrialMoment.WardBroken:
                case TrialMoment.RitualBroken:
                case TrialMoment.VesselBroken:
                {
                    bool vessel = (TrialMoment)e.Int == TrialMoment.VesselBroken;
                    var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.8f, 1f, 0.7f, 0.5f, 8f, new Color(1f, 0.95f, 0.8f, 0.9f));
                    flash.FadeOut = true;
                    AddLight(flash, new Color(1f, 0.9f, 0.6f), 4f, 10f);
                    for (int i = 0; i < 12; i++) Shard(e.Pos + Vector2.up * 1f, i * 30f + Random.Range(0f, 20f), new Color(1f, 0.9f, 0.65f));
                    HitStop.Freeze(0.12f, -1, major: true);
                    CameraShake.Add(0.7f);
                    if (!vessel) ShowCallout(e.Pos + Vector2.up * 3f, "隙あり！", new Color(1f, 0.85f, 0.4f), 52);
                    break;
                }
                case TrialMoment.RitualFailed:
                    ShowCallout(e.Pos + Vector2.up * 3f, "血の奔流", new Color(1f, 0.3f, 0.3f), 48);
                    break;
                case TrialMoment.CollapseWarn:
                    CameraShake.Add(0.35f);
                    ShowCallout(e.Pos + Vector2.up * 2f, "床が崩れる！ 中央へ", new Color(0.8f, 0.7f, 1f), 46);
                    break;
                case TrialMoment.Collapsed:
                {
                    CameraShake.Add(0.9f);
                    var area = RunSimulation.SafeArea(RunSimulation.DefaultBounds, e.Id);
                    for (int i = 0; i < 24; i++)
                    {
                        float t = i / 24f;
                        Vector2 at = t < 0.5f
                            ? new Vector2(Mathf.Lerp(area.xMin, area.xMax, t * 2f), i % 2 == 0 ? area.yMin : area.yMax)
                            : new Vector2(i % 2 == 0 ? area.xMin : area.xMax, Mathf.Lerp(area.yMin, area.yMax, (t - 0.5f) * 2f));
                        Burst("fx_puff", at, Random.Range(8f, 12f), Random.Range(1f, 1.6f), new Color(0.55f, 0.5f, 0.65f, 0.85f));
                    }
                    break;
                }
                case TrialMoment.VesselRise:
                {
                    var pillar = Spawn("fx_holy", e.Pos + Vector2.up * 0.8f, 12f, 1f, 1.6f, 3f, new Color(0.55f, 0.95f, 1f));
                    pillar.FadeOut = true;
                    AddLight(pillar, new Color(0.5f, 0.9f, 1f), 3f, 6f);
                    HitStop.Freeze(0.1f, -1, major: true);
                    ShowCallout(e.Pos + Vector2.up * 2.4f, AshenFight ? "灰の心臓" : "魂の器", new Color(0.6f, 0.95f, 1f), 46);
                    break;
                }
                case TrialMoment.LichRises:
                    CameraShake.Add(0.6f);
                    ShowCallout(e.Pos + Vector2.up * 3f, AshenFight ? "灰の王の復活" : "冥王の復活", new Color(0.6f, 0.95f, 1f), 46);
                    break;
                default:
                    LandTrial(e); // EventPresenter.Lands.cs
                    break;
            }
        }

        /// <summary>The final blow on a boss: a long hold, slow motion, a burst of light and souls.</summary>
        private void BossFell(Vector2 at)
        {
            HitStop.Freeze(0.16f, -1, major: true);
            HitStop.Slow(1.1f, 0.25f);
            CameraShake.Add(1f);
            var flash = Spawn("fx_nova", at + Vector2.up * 0.8f, 1f, 0.9f, 0.5f, 9f, new Color(1f, 0.95f, 0.85f, 0.85f));
            flash.FadeOut = true;
            AddLight(flash, new Color(1f, 0.85f, 0.6f), 6f, 14f);
            var holy = Spawn("fx_holy", at + Vector2.up * 1f, 10f, 1.4f, 2f, 3.4f, new Color(1f, 0.9f, 0.7f));
            holy.FadeOut = true;
            for (int i = 0; i < 16; i++) Ember(at + Vector2.up * 0.8f, i * 22.5f + Random.Range(0f, 15f));
            for (int i = 0; i < 12; i++) Shard(at + Vector2.up * 0.8f, i * 30f + Random.Range(0f, 20f), new Color(1f, 0.9f, 0.6f));
        }

        private void OnArea(in SimEvent e)
        {
            float r = e.Value;
            switch ((AreaKind)e.Int)
            {
                case AreaKind.SlagPool:
                case AreaKind.Steam:
                    LandArea(e); // EventPresenter.Lands.cs
                    break;
                case AreaKind.Thrust:
                case AreaKind.Lightning:
                case AreaKind.RuneFlare:
                    RuneArea(e); // EventPresenter.Runes.cs
                    break;
                case AreaKind.HallQuake:
                    QuakeArea(e); // EventPresenter.Stages.cs
                    break;
                case AreaKind.SporeCloud:
                case AreaKind.InkBurst:
                    Lands2Area(e); // EventPresenter.Lands2.cs
                    break;
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
                case AreaKind.SoulBurst:
                {
                    var ring = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.4f, r * 0.3f, r * 1.1f, new Color(0.7f, 0.5f, 1f, 0.85f));
                    ring.FadeOut = true;
                    AddLight(ring, new Color(0.6f, 0.4f, 1f), 1.8f, r + 1.5f);
                    for (int i = 0; i < 6; i++) Shard(e.Pos + Vector2.up * 0.3f, i * 60f + Random.Range(0f, 30f), new Color(0.75f, 0.6f, 1f));
                    break;
                }
                case AreaKind.BloodTorrent:
                {
                    var wave = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.8f, 1f, r, new Color(1f, 0.15f, 0.15f, 0.8f));
                    wave.FadeOut = true;
                    AddLight(wave, new Color(1f, 0.15f, 0.1f), 4f, r);
                    CameraShake.Add(0.9f);
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
                case AreaKind.Raise:
                {
                    // Bone servants claw their way up: green soul fire and dust.
                    var ring = Spawn("fx_circle", e.Pos, 1f, 0.6f, r * 0.4f, r, new Color(0.35f, 1f, 0.5f, 0.5f), order: -300);
                    ring.FadeOut = true;
                    AddLight(ring, new Color(0.4f, 1f, 0.55f), 1.6f, r + 2f);
                    Burst("fx_puff", e.Pos + Vector2.up * 0.3f, 14f, r, new Color(0.45f, 0.55f, 0.45f));
                    for (int i = 0; i < 6; i++) Shard(e.Pos + Vector2.up * 0.4f, i * 60f + Random.Range(0f, 30f), new Color(0.7f, 1f, 0.75f));
                    break;
                }
                case AreaKind.Curse:
                {
                    var ring = Spawn("fx_revive_ring", e.Pos, 1f, 0.9f, r * 0.3f, r, new Color(0.75f, 0.45f, 1f, 0.85f), order: -300);
                    ring.FadeOut = true;
                    var ground = Spawn("fx_circle", e.Pos, 1f, 1f, r, r, new Color(0.45f, 0.15f, 0.7f, 0.4f), order: -301);
                    ground.FadeOut = true;
                    AddLight(ground, new Color(0.6f, 0.3f, 1f), 1.8f, r + 1.5f);
                    for (int i = 0; i < 8; i++) Shard(e.Pos + Vector2.up * 0.3f, i * 45f + 10f, new Color(0.8f, 0.6f, 1f));
                    break;
                }
                case AreaKind.BoneBurst:
                {
                    for (int i = 0; i < 10; i++) Shard(e.Pos + Vector2.up * 0.4f, i * 36f + Random.Range(0f, 20f), new Color(0.92f, 0.88f, 0.76f));
                    var ring = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.3f, 0.3f, r, new Color(0.7f, 1f, 0.75f, 0.7f));
                    ring.FadeOut = true;
                    break;
                }
                case AreaKind.JudgmentMark:
                {
                    // Where the light will fall: a golden ring that tightens until it strikes.
                    float delay = Mathf.Max(0.2f, e.Size.x);
                    var mark = Spawn("fx_revive_ring", e.Pos, 1f, delay, r * 1.3f, r, new Color(1f, 0.85f, 0.4f, 0.85f), order: -300);
                    mark.FadeOut = false;
                    var ground = Spawn("fx_circle", e.Pos, 1f, delay, r, r, new Color(1f, 0.85f, 0.45f, 0.2f), order: -301);
                    ground.FadeOut = false;
                    break;
                }
                case AreaKind.Judgment:
                {
                    var column = Spawn("fx_holy", e.Pos + Vector2.up * 1.2f, 14f, 0.8f, r * 0.8f, r * 1.2f, new Color(1f, 0.78f, 0.3f));
                    column.FadeOut = true;
                    AddLight(column, new Color(1f, 0.9f, 0.6f), 3.5f, r + 4f);
                    var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.2f, 1f, 0.35f, 0.4f, r * 1.1f, new Color(1f, 0.95f, 0.7f, 0.9f));
                    flash.FadeOut = true;
                    for (int i = 0; i < 10; i++) Shard(e.Pos + Vector2.up * 0.3f, i * 36f + 10f, new Color(1f, 0.9f, 0.55f));
                    CameraShake.Add(0.3f);
                    break;
                }
                case AreaKind.Devotion:
                    TetherView.Bind(e.ClientId, (ulong)Mathf.RoundToInt(e.Size.x) - 1, e.Value);
                    break;
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
                case GimmickKind.Icicle:
                case GimmickKind.LavaCrack:
                case GimmickKind.SporeVent:
                case GimmickKind.Glyph:
                    LandHazardWarn(e); // EventPresenter.Lands.cs
                    break;
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
                case GimmickKind.Icicle:
                case GimmickKind.LavaCrack:
                case GimmickKind.SporeVent:
                case GimmickKind.Glyph:
                    LandHazardFire(e); // EventPresenter.Lands.cs
                    break;
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
                case ProjectileKind.WaterOrb:
                case ProjectileKind.IceShard:
                case ProjectileKind.FireBolt:
                    fx = LandProjectile(kind, e, ref angle); // EventPresenter.Lands.cs
                    break;
                case ProjectileKind.SporeOrb:
                case ProjectileKind.Page:
                    fx = Lands2Projectile(kind, e, ref angle); // EventPresenter.Lands2.cs
                    break;
                case ProjectileKind.SoulBolt:
                    fx = Spawn("fx_orb", e.Pos, 14f, e.Value + 0.05f, 0.85f, 0.85f, new Color(0.55f, 1f, 0.65f));
                    AddLight(fx, new Color(0.4f, 1f, 0.55f), 1f, 1.6f);
                    break;
                case ProjectileKind.Wisp:
                    fx = Spawn("fx_soul", e.Pos, 12f, e.Value + 0.05f, 0.7f, 0.5f, new Color(0.7f, 0.9f, 1f, 0.9f));
                    AddLight(fx, new Color(0.45f, 0.75f, 1f), 0.8f, 1.4f);
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
                case ProjectileKind.Bomb:
                    // A black bottle with a lit fuse, tumbling.
                    fx = Spawn("fx_flask", e.Pos, 16f, e.Value + 0.05f, 1.35f, 1.35f, new Color(0.45f, 0.42f, 0.5f));
                    AddLight(fx, new Color(1f, 0.6f, 0.25f), 1.2f, 1.6f);
                    angle = 0f;
                    break;
                case ProjectileKind.Dagger:
                    fx = Spawn("fx_shard", e.Pos, 0f, e.Value + 0.05f, 1.3f, 1.3f, new Color(0.85f, 0.88f, 0.95f));
                    angle -= 45f; // the shard sheet points up-right
                    break;
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
            // Fewer flashes (settings): the big bright bursts are drawn faint.
            if (sheet is "fx_nova" or "fx_holy" or "fx_explosion") { color.a *= GameSettings.FlashScale; fx.Color = color; }
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
            label.transform.localScale = Vector3.one;
            _numbers.Add(new Number { Text = label, World = world, Life = size > 30 ? 0.9f : 0.65f });
        }

        /// <summary>A word for a team play ("砕き！", "連携！"): always shown, even when the numbers are capped.</summary>
        private void ShowCallout(Vector2 world, string text, Color color, int size)
        {
            var label = _freeTexts.Count > 0 ? _freeTexts.Pop() : UIKit.Label(_numberLayer, "", 24, TextAnchor.MiddleCenter);
            label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            label.font = UIKit.TitleFont;
            label.rectTransform.sizeDelta = new Vector2(320f, 80f);
            label.text = text;
            label.fontSize = size;
            label.color = color;
            if (label.GetComponent<Outline>() == null)
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            _numbers.Add(new Number { Text = label, World = world, Life = 1.1f, Pop = true });
        }

        // ------------------------------------------------------------------ Frame

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_camera == null) _camera = Camera.main;
            UpdateStains(); // EventPresenter.Remains.cs
            UpdateBossRage();
            UpdateAffixLabels();
            UpdateLords2(); // EventPresenter.Lords2.cs
            UpdateLands2(); // EventPresenter.Lands2.cs

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

            float fxDt = HitStop.DeltaTime; // effects hold during a hit stop; telegraphs above keep real time
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var fx = _effects[i];
                fx.Age += fxDt;
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
                    tr.position += (Vector3)(fx.Velocity * fxDt);
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
                    if (fx.Light != null && fx.Light.enabled) fx.Light.intensity *= 1f - fxDt * 3f;
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
                float rise = (n.Pop ? 0.35f : 0.9f) * (1f - Mathf.Pow(1f - n.Age / n.Life, 2f));
                n.Text.transform.position = _camera.WorldToScreenPoint(n.World + Vector3.up * rise);
                if (n.Pop)
                {
                    float k = Mathf.Clamp01(n.Age / 0.14f);
                    n.Text.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - (1f - k) * (1f - k));
                }
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
            public Text Label;
            public Vector2 Position;
            public Color Color;
            public bool Dim;
            /// <summary>Needs the whole team standing on it (it brightens as they hold).</summary>
            public bool Gather;
            public float Scale = 1.6f;
            public float LabelBelow = -1.75f;
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
                if (d.Portal != null) Release(d.Portal);
                Release(d.Ring);
                if (d.Icon != null) Release(d.Icon);
                Destroy(d.Label.gameObject);
            }
            _doors.Clear();
        }

        /// <summary>
        /// The glowing marks of the room you stand in: its pedestal (shrine, spring, merchant...; dim once you used
        /// it), the ring before the boss door at the campfire, and the way down after the boss. Each has a label.
        /// </summary>
        private void UpdateDoors()
        {
            var director = RunDirector.Instance;
            bool open = director != null && director.IsSpawned && director.Phase.Value == RunPhase.Playing;
            var room = open ? director.LocalRoom : null;
            bool used = room != null && NetPlayer.Local != null && (NetPlayer.Local.UsedPedestals.Value & (1 << room.Id)) != 0;
            int key = room == null ? 0
                : (room.Id + 1) * 1000 + director.Area.Value * 100 + director.Ways.Value * 1009 + (director.HeartOpen.Value ? 77 : 0) + (used ? 1 : 0) + (director.BossDoorOpen.Value ? 2 : 0) + (director.ExitOpen.Value ? 4 : 0)
                  + (int)(director.MapSeed.Value % 97) * 10000 + director.Keys.Value * 7 + director.UnlockedRooms.Value * 131;
            if (key != _doorsKey)
            {
                ClearDoors();
                _doorsKey = key;
                if (room != null) CreateMarks(director, room, used);
            }
            if (_doors.Count == 0) return;

            float hold = Mathf.Clamp01(director.GatherHold.Value / RunSimulation.ExitHoldTime);
            for (int i = 0; i < _doors.Count; i++)
            {
                var d = _doors[i];
                float pulse = d.Scale * (1f + 0.05f * Mathf.Sin(Time.time * 4f + i * 1.7f) + (d.Gather ? 0.2f * hold : 0f));
                if (d.Portal != null)
                {
                    d.Portal.Go.transform.localScale = Vector3.one * pulse;
                    d.Portal.StartScale = d.Portal.EndScale = pulse;
                    if (d.Portal.Light != null) d.Portal.Light.intensity = (d.Dim ? 0.4f : 1.5f) + (d.Gather ? 1.6f * hold : 0f);
                }
                var ringColor = d.Color;
                ringColor.a = d.Dim ? 0.1f : d.Gather ? 0.3f + 0.5f * hold : 0.22f + 0.06f * Mathf.Sin(Time.time * 3f + i);
                d.Ring.Renderer.color = ringColor;

                if (d.Icon != null)
                {
                    Vector2 iconAt = d.Position + new Vector2(0f, 0.2f + 0.1f * Mathf.Sin(Time.time * 2.2f + i * 2f));
                    d.Icon.Go.transform.position = iconAt;
                }
                if (_camera != null)
                {
                    d.Label.transform.position = _camera.WorldToScreenPoint(d.Position + new Vector2(0f, d.LabelBelow));
                    d.Label.enabled = !PerkView.IsShowing; // the cards stand in front of the marks
                }
            }
        }

        private void CreateMarks(RunDirector director, MapRoom room, bool used)
        {
            int area = director.Area.Value;
            if (RoomCatalog.HasPedestal(room.Kind))
            {
                var reward = RoomCatalog.Reward(room.Kind);
                string text = used ? "<color=#bfb6aa>使用済み</color>" : RoomCatalog.ShortText(room.Kind);
                var mark = CreateMark(RunSimulation.PedestalPosition(room.Bounds), RoomCatalog.Color(room.Kind), GameIcons.Reward(reward), RunSimulation.PedestalRadius,
                    $"{RoomCatalog.Name(room.Kind)}\n<size=22><color=#e8e0d4>{text}</color></size>", used);
                _doors.Add(mark);
            }
            // A locked treasure door seen from the room next to it.
            for (int d = 0; d < 4; d++)
            {
                var next = director.Map[room.Neighbor[d]];
                if (next == null || next.Kind != RoomKind.Treasure || director.IsUnlocked(next.Id)) continue;
                int keys = director.Keys.Value;
                var mark = CreateMark(DungeonMap.EntryPoint(room, d), new Color(0.9f, 0.75f, 0.35f), GameIcons.Reward(RoomReward.Treasure), RunSimulation.UnlockRadius * 0.7f,
                    keys > 0 ? "宝物庫の扉\n<size=22><color=#e8e0d4>立つと鍵で開く</color></size>" : "宝物庫の扉\n<size=22><color=#e8e0d4>鍵が必要</color></size>", keys <= 0);
                mark.LabelBelow = d == Dir.S ? 1.6f : -1.4f;
                _doors.Add(mark);
            }
            if (room.Kind == RoomKind.Campfire && NetPlayer.All.Count > 1)
            {
                var bell = CreateMark(room.Bounds.center + RunSimulation.BellSpot, new Color(1f, 0.78f, 0.4f), null, RunSimulation.BellRadius,
                    "集合の鐘\n<size=22><color=#e8e0d4>仲間を呼ぶ</color></size>", true);
                bell.LabelBelow = -0.9f;
                _doors.Add(bell);
            }
            if (room.Kind == RoomKind.Campfire && !director.BossDoorOpen.Value)
            {
                var map = director.Map;
                for (int d = 0; d < 4; d++)
                {
                    if (room.Neighbor[d] != map.Boss.Id) continue;
                    Vector2 door = DungeonMap.EntryPoint(room, d);
                    var mark = CreateMark(door, new Color(1f, 0.3f, 0.25f), null, RunSimulation.BossDoorRadius * 0.6f,
                        NetPlayer.All.Count > 1 ? "主の扉\n<size=22><color=#e8e0d4>全員でここに立つと開く</color></size>" : "主の扉\n<size=22><color=#e8e0d4>ここに立つと開く</color></size>", false);
                    mark.Gather = true;
                    mark.LabelBelow = d == Dir.S ? 1.6f : -1.4f;
                    _doors.Add(mark);
                }
            }
            if (room.Kind == RoomKind.Boss && director.ExitOpen.Value)
            {
                bool home = RunSimulation.OffersHome(area);
                string all = NetPlayer.All.Count > 1 ? "全員で" : "";
                // Two ways down, each to a land (in its colour, with its name and what waits there). Past the third
                // lord the right one leads into the deep layers and a golden way on the left leads home.
                void Way(Vector2 offset, Color color, string title, string text)
                {
                    var mark = CreateMark(room.Origin + offset, color, null, RunSimulation.ExitRadius * 1.6f,
                        $"{title}\n<size=22><color=#e8e0d4>{text}</color></size>", false);
                    mark.Gather = true;
                    mark.Scale = 2.2f;
                    AddLight(mark.Ring, color, 2.5f, 6f);
                    _doors.Add(mark);
                }
                var right = BiomeCatalog.Get(director.Way(1));
                if (home)
                {
                    Way(RunSimulation.DeepOffset, new Color(0.72f, 0.5f, 1f), "深層へ", $"{right.Name}\n{all}ここに立つと、さらに深く（魔物はより強い）");
                    Way(RunSimulation.HomeOffset, new Color(1f, 0.85f, 0.45f), "帰還", $"{all}ここに立つと踏破して拠点へ");
                    // The six memories are together: the way down to the heart (the end of the story) in the middle.
                    if (director.HeartOpen.Value && director.Biome != Biome.Heart)
                        Way(RunSimulation.HeartOffset, new Color(1f, 0.45f, 0.25f), "迷宮の心臓へ", $"灰の王が待つ\n{all}ここに立って降りる");
                }
                else
                {
                    var left = BiomeCatalog.Get(director.Way(0));
                    Way(RunSimulation.HomeOffset, LandLook.Sign(left.Id), left.Name, $"{left.Hint}\n{all}ここに立って進む");
                    if (right.Id != left.Id)
                        Way(RunSimulation.DeepOffset, LandLook.Sign(right.Id), right.Name, $"{right.Hint}\n{all}ここに立って進む");
                }
            }
        }

        private DoorFx CreateMark(Vector2 at, Color color, Sprite icon, float ringRadius, string label, bool dim)
        {
            var d = new DoorFx { Position = at, Color = color, Dim = dim };
            d.Ring = Spawn("fx_circle", at, 1f, 99999f, ringRadius, ringRadius, color, order: -360);
            if (!dim)
            {
                d.Portal = Spawn("fx_portal", at, 8f, 99999f, 1.6f, 1.6f, Color.Lerp(Color.white, color, 0.6f), order: -350);
                AddLight(d.Portal, color, 1.5f, 3.6f);
            }
            if (icon != null)
            {
                d.Icon = SpawnIcon(icon, at + Vector2.up * 0.2f, 1.6f, 2600);
                if (dim) d.Icon.Renderer.color = new Color(1f, 1f, 1f, 0.35f);
            }

            d.Label = UIKit.Label(_numberLayer, "", 32, TextAnchor.UpperCenter);
            d.Label.font = UIKit.TitleFont;
            d.Label.rectTransform.sizeDelta = new Vector2(520f, 110f);
            d.Label.rectTransform.pivot = new Vector2(0.5f, 1f);
            d.Label.text = label;
            d.Label.color = dim ? new Color(color.r, color.g, color.b, 0.6f) : color;
            var shadow = d.Label.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return d;
        }
    }
}
