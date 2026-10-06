using System;
using UnityEngine;

namespace Game.Player
{
    /// <summary>Top-down movement and dodge (dash) for the player.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float moveSpeed = 6f;
        [Tooltip("Units/sec² used to reach the target velocity. Higher = snappier.")]
        [SerializeField] private float acceleration = 60f;

        [Header("Dodge")]
        [SerializeField] private float dodgeSpeed = 16f;
        [SerializeField] private float dodgeDuration = 0.18f;
        [SerializeField] private float dodgeCooldown = 0.45f;

        [SerializeField] private PlayerInputReader input;

        private Rigidbody2D _body;
        private Vector2 _dodgeDirection;
        private float _dodgeEndTime;
        private float _nextDodgeTime;

        /// <summary>Last non-zero movement direction (normalized).</summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.down;

        /// <summary>True during the dash; damage code will treat this as invulnerable.</summary>
        public bool IsDodging => Time.time < _dodgeEndTime;

        public event Action DodgeStarted;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (input == null) input = GetComponent<PlayerInputReader>();
        }

        private void OnEnable() => input.DodgePressed += TryDodge;
        private void OnDisable() => input.DodgePressed -= TryDodge;

        private void Update()
        {
            Vector2 move = input.Move;
            if (!IsDodging && move.sqrMagnitude > 0.01f)
                FacingDirection = move.normalized;
        }

        private void FixedUpdate()
        {
            if (IsDodging)
            {
                _body.linearVelocity = _dodgeDirection * dodgeSpeed;
                return;
            }

            Vector2 target = Vector2.ClampMagnitude(input.Move, 1f) * moveSpeed;
            _body.linearVelocity = Vector2.MoveTowards(_body.linearVelocity, target, acceleration * Time.fixedDeltaTime);
        }

        private void TryDodge()
        {
            if (IsDodging || Time.time < _nextDodgeTime) return;

            Vector2 move = input.Move;
            _dodgeDirection = move.sqrMagnitude > 0.01f ? move.normalized : FacingDirection;
            _dodgeEndTime = Time.time + dodgeDuration;
            _nextDodgeTime = _dodgeEndTime + dodgeCooldown;
            DodgeStarted?.Invoke();
        }
    }
}
