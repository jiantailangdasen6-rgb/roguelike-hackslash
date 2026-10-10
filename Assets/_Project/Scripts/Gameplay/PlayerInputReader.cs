using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Reads the "Player" action map and exposes device-agnostic input. Keyboard+mouse aims with the cursor,
    /// a gamepad aims with the right stick (falling back to the move direction).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public const string MapName = "Player";

        [SerializeField] private InputActionAsset actions;

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _aimPoint;
        private InputAction _aimStick;
        private InputAction _attack;
        private InputAction _skill1;
        private InputAction _skill2;
        private InputAction _dodge;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 AimScreenPosition => _aimPoint.ReadValue<Vector2>();
        public Vector2 AimStick => _aimStick.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed();

        /// <summary>True when the latest input came from keyboard/mouse, so aiming follows the cursor.</summary>
        public bool UsingMouse { get; private set; } = true;

        public event Action Skill1Pressed;
        public event Action Skill2Pressed;
        public event Action DodgePressed;

        private void Awake()
        {
            _map = actions.FindActionMap(MapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", true);
            _aimPoint = _map.FindAction("AimPoint", true);
            _aimStick = _map.FindAction("AimStick", true);
            _attack = _map.FindAction("Attack", true);
            _skill1 = _map.FindAction("Skill1", true);
            _skill2 = _map.FindAction("Skill2", true);
            _dodge = _map.FindAction("Dodge", true);
        }

        private void OnEnable()
        {
            _map.actionTriggered += OnAny;
            _skill1.performed += OnSkill1;
            _skill2.performed += OnSkill2;
            _dodge.performed += OnDodge;
            _map.Enable();
        }

        private void OnDisable()
        {
            _map.Disable();
            _map.actionTriggered -= OnAny;
            _skill1.performed -= OnSkill1;
            _skill2.performed -= OnSkill2;
            _dodge.performed -= OnDodge;
        }

        private void OnAny(InputAction.CallbackContext ctx)
        {
            var device = ctx.control?.device;
            if (device is Keyboard || device is Mouse) UsingMouse = true;
            else if (device is Gamepad || device is Touchscreen) UsingMouse = false;
        }

        private void OnSkill1(InputAction.CallbackContext _) => Skill1Pressed?.Invoke();
        private void OnSkill2(InputAction.CallbackContext _) => Skill2Pressed?.Invoke();
        private void OnDodge(InputAction.CallbackContext _) => DodgePressed?.Invoke();
    }
}
