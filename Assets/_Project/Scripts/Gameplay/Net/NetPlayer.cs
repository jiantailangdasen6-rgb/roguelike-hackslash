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
            animator.SetSheet(SpriteSheet.Load(ClassDef.Sheet));
            Class.OnValueChanged += (_, c) => animator.SetSheet(SpriteSheet.Load(ClassCatalog.Get(c).Sheet));
            shadow.sprite = SpriteSheet.Load("fx_shadow")?.First;
            marker.sprite = SpriteSheet.Load("fx_circle")?.First;
            marker.color = new Color(Color.r, Color.g, Color.b, 0.35f);
            _lastPosition = transform.position;
            RunDirector.EventReceived += OnEvent;

            bool owner = IsOwner;
            mover.enabled = owner;
            input.enabled = owner;
            if (!owner) return;

            Local = this;
            var follow = FindAnyObjectByType<CinemachineCamera>();
            if (follow != null) follow.Follow = transform;
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

        private void OnEvent(SimEvent e)
        {
            if (e.ClientId != OwnerClientId) return;
            switch (e.Type)
            {
                case SimEventType.PlayerAttack:
                {
                    var anim = (AttackAnim)e.Int;
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
        }
    }
}
