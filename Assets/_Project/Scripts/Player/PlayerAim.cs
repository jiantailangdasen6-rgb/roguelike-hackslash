using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Resolves the aim direction from mouse cursor, right stick, or (fallback) facing direction,
    /// and positions the aim indicator.
    /// </summary>
    public sealed class PlayerAim : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private Transform indicator;
        [SerializeField] private float indicatorDistance = 0.8f;
        [SerializeField] private float stickDeadZone = 0.3f;

        private Camera _camera;

        public Vector2 AimDirection { get; private set; } = Vector2.down;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<PlayerController>();
        }

        private void Start() => _camera = Camera.main;

        private void Update()
        {
            AimDirection = ResolveAim();

            if (indicator != null)
            {
                indicator.localPosition = AimDirection * indicatorDistance;
                float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg - 90f;
                indicator.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private Vector2 ResolveAim()
        {
            if (input.UsingMouseAim && _camera != null)
            {
                Vector2 cursorWorld = _camera.ScreenToWorldPoint(input.AimScreenPosition);
                Vector2 toCursor = cursorWorld - (Vector2)transform.position;
                if (toCursor.sqrMagnitude > 0.0001f) return toCursor.normalized;
            }

            Vector2 stick = input.AimStick;
            if (stick.sqrMagnitude > stickDeadZone * stickDeadZone) return stick.normalized;

            // TODO(M1): auto-aim at the nearest enemy (default for touch controls).
            return controller.FacingDirection;
        }
    }
}
