using System.Collections.Generic;
using Game.Core;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// Networked player avatar. Movement is owner-authoritative (NetworkTransform in Owner mode); combat state
    /// is written by the host from the simulation. Drives the pixel-art animator from movement and events.
    /// </summary>
    public sealed class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new();
        public static NetPlayer Local { get; private set; }

        public readonly NetworkVariable<int> Slot = new();
        public readonly NetworkVariable<ClassId> Class = new();
        public readonly NetworkVariable<byte> Skin = new();
        public readonly NetworkVariable<float> Hp = new();
        public readonly NetworkVariable<float> MaxHp = new();
        public readonly NetworkVariable<float> Shield = new();
        public readonly NetworkVariable<bool> Downed = new();
        public readonly NetworkVariable<float> ReviveProgress = new();
        public readonly NetworkVariable<float> MoveSpeed = new(4.6f);
        public readonly NetworkVariable<float> Skill1Cooldown = new();
        public readonly NetworkVariable<float> Skill2Cooldown = new();
        public readonly NetworkVariable<float> DodgeCooldown = new();
        public readonly NetworkVariable<bool> Valor = new();
        /// <summary>Perk picks earned and not made yet (banked during fights, chosen between rooms).</summary>
        public readonly NetworkVariable<int> Picks = new();
        public readonly NetworkVariable<bool> FacingLeft = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [SerializeField] private PixelAnimator animator;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private Light2D glow;
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private PlayerInputReader input;

        private Vector3 _lastPosition;
        private float _actionUntil;
        private bool _wasDowned;

        public string DisplayName => $"P{Slot.Value + 1}";
        public Color Color => GameArt.PlayerColors[Mathf.Clamp(Slot.Value, 0, GameArt.PlayerColors.Length - 1)];
        public ClassDef ClassDef => ClassCatalog.Get(Class.Value);
        public PlayerInputReader Input => input;
        public PlayerMover Mover => mover;
        public PixelAnimator Animator => animator;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            ApplySheet();
            Class.OnValueChanged += (_, _) => ApplySheet();
            Skin.OnValueChanged += (_, _) => ApplySheet();
            shadow.sprite = SpriteSheet.Load("fx_shadow")?.First;
            marker.sprite = SpriteSheet.Load("fx_circle")?.First;
            marker.color = new Color(Color.r, Color.g, Color.b, 0.35f);
            CreateDownedVisuals();
            _lastPosition = transform.position;
            RunDirector.EventReceived += OnEvent;

            bool owner = IsOwner;
            mover.enabled = owner;
            input.enabled = owner;
            if (!owner) return;

            Local = this;
            var follow = FindAnyObjectByType<CinemachineCamera>();
            if (follow != null) follow.Follow = CameraTarget.Create(transform).transform;
        }

        private void ApplySheet() => animator.SetSheet(SpriteSheet.Load(SkinCatalog.Sheet(Class.Value, Skin.Value)) ?? SpriteSheet.Load(ClassDef.Sheet));

        // ------------------------------------------------------------------ Downed: easy to find, clear how to help

        private SpriteRenderer _reviveRing;
        private SpriteRenderer _reviveFill;
        private SpriteRenderer _downedIcon;
        private Light2D _beacon;

        private void CreateDownedVisuals()
        {
            SpriteRenderer Child(string name, string sheet, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = SpriteSheet.Load(sheet)?.First;
                r.sortingOrder = order;
                r.enabled = false;
                return r;
            }

            // fx_circle is 2 units across: scale = radius.
            _reviveRing = Child("ReviveRing", "fx_revive_ring", -480);
            _reviveRing.transform.localScale = Vector3.one * RunSimulation.ReviveRadius;
            _reviveFill = Child("ReviveFill", "fx_circle", -481);
            _downedIcon = Child("DownedIcon", "fx_downed", 3500);

            var lightGo = new GameObject("Beacon");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.6f);
            _beacon = lightGo.AddComponent<Light2D>();
            _beacon.lightType = Light2D.LightType.Point;
            _beacon.pointLightOuterRadius = 3.2f;
            _beacon.pointLightInnerRadius = 0.2f;
            _beacon.color = new Color(1f, 0.25f, 0.2f);
            _beacon.enabled = false;
        }

        /// <summary>True while a living teammate stands close enough to help this downed player up.</summary>
        public bool BeingRevived
        {
            get
            {
                foreach (var other in All)
                    if (other != this && !other.Downed.Value &&
                        ((Vector2)(other.transform.position - transform.position)).sqrMagnitude <= RunSimulation.ReviveRadius * RunSimulation.ReviveRadius)
                        return true;
                return false;
            }
        }

        private float _shownProgress;

        private void UpdateDownedVisuals(bool downed)
        {
            _reviveRing.enabled = _reviveFill.enabled = _downedIcon.enabled = _beacon.enabled = downed;
            if (!downed)
            {
                _shownProgress = 0f;
                return;
            }
            bool helped = BeingRevived;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (helped ? 10f : 5f));
            _shownProgress = Mathf.MoveTowards(_shownProgress, ReviveProgress.Value, Time.deltaTime * 1.5f);

            // The ring marks where to stand; the inner disc grows with revive progress.
            _reviveRing.color = helped ? new Color(0.55f, 1f, 0.6f, 0.55f) : new Color(1f, 0.35f, 0.3f, 0.4f + 0.3f * pulse);
            _reviveFill.transform.localScale = Vector3.one * Mathf.Max(0.05f, _shownProgress) * RunSimulation.ReviveRadius;
            _reviveFill.color = new Color(0.45f, 1f, 0.55f, 0.25f);
            _downedIcon.transform.localPosition = new Vector3(0f, 1.45f + 0.08f * Mathf.Sin(Time.time * 4f));
            _downedIcon.color = Color.Lerp(new Color(1f, 0.4f, 0.35f), Color.white, pulse * 0.6f);
            _beacon.intensity = 0.7f + 0.8f * pulse;
            _beacon.color = helped ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.25f, 0.2f);
        }

        public override void OnNetworkDespawn()
        {
            RunDirector.EventReceived -= OnEvent;
            All.Remove(this);
            if (Local == this) Local = null;
        }

        /// <summary>Owner → host, every network tick. Unreliable: presses are counters, so a dropped packet loses nothing.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        public void SendInputRpc(Vector2 aim, bool attack, byte skill1, byte skill2, byte dodge, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            RunDirector.Instance?.Sim?.SetPlayerInput(OwnerClientId, new PlayerInput
            {
                Aim = Vector2.ClampMagnitude(aim, 20f), Attack = attack, Skill1Count = skill1, Skill2Count = skill2, DodgeCount = dodge,
            });
        }

        private void PlayAction(AttackAnim anim)
        {
            string clip = anim switch
            {
                AttackAnim.BasicA or AttackAnim.Spin => "attack",
                AttackAnim.BasicB => "attack2",
                AttackAnim.Skill1 => "skill1",
                AttackAnim.Skill2 => "skill2",
                _ => "dodge",
            };
            animator.Play(clip, force: true, speed: anim == AttackAnim.Dodge ? 1.2f : 1f);
            _actionUntil = Time.time + 0.35f;
        }

        // Clients see their own actions a round trip late; the owner starts the animation on the press instead
        // and the host's confirmation then doesn't restart it. (The host itself has no delay to hide.)
        private AttackAnim _predicted;
        private float _predictedAt = -10f;
        private const float PredictionWindow = 0.6f;

        public void PredictAction(AttackAnim anim)
        {
            if (!IsOwner || IsServer) return;
            PlayAction(anim);
            _predicted = anim;
            _predictedAt = Time.time;
        }

        private bool ConsumePrediction(AttackAnim confirmed)
        {
            if (!IsOwner || Time.time - _predictedAt > PredictionWindow) return false;
            bool basic(AttackAnim a) => a is AttackAnim.BasicA or AttackAnim.BasicB or AttackAnim.Spin;
            bool same = confirmed == _predicted || (basic(confirmed) && basic(_predicted));
            if (same) _predictedAt = -10f;
            return same;
        }

        private void OnEvent(SimEvent e)
        {
            if (e.ClientId != OwnerClientId) return;
            switch (e.Type)
            {
                case SimEventType.PlayerAttack:
                {
                    var anim = (AttackAnim)e.Int;
                    if (ConsumePrediction(anim)) break; // already playing since the button press
                    PlayAction(anim);
                    if (Mathf.Abs(e.Vec.x) > 0.05f && !IsOwner) animator.Renderer.flipX = e.Vec.x < 0f;
                    break;
                }
                case SimEventType.PlayerHurt:
                    animator.Flash(0.1f);
                    if (Time.time > _actionUntil) animator.Play("hurt", force: true);
                    break;
                case SimEventType.Teleport when IsOwner:
                    mover.Teleport(e.Pos);
                    break;
            }
        }

        private void Update()
        {
            var r = animator.Renderer;
            r.sortingOrder = EnemyView.SortOrder(transform.position.y);
            if (IsOwner || Time.time > _actionUntil) r.flipX = FacingLeft.Value;

            bool downed = Downed.Value;
            if (downed && !_wasDowned) animator.Play("death", force: true, hold: true);
            if (!downed && _wasDowned) animator.Play("idle", force: true);
            _wasDowned = downed;

            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            if (!downed)
            {
                bool moving = delta.sqrMagnitude > 0.00004f * Mathf.Max(1f, Time.deltaTime * 60f);
                animator.SetBase(moving ? "run" : "idle");
            }

            // Valor (war cry) glows gold; downed players fade.
            r.color = downed ? new Color(0.7f, 0.7f, 0.75f, 0.85f) : Color.white;
            glow.color = Valor.Value ? new Color(1f, 0.8f, 0.4f) : new Color(1f, 0.75f, 0.55f);
            glow.intensity = Valor.Value ? 1.3f : 0.9f;
            shadow.enabled = !downed;
            UpdateDownedVisuals(downed);
        }
    }
}
