using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Reads the "Player" action map and exposes device-agnostic input to gameplay code.
    /// Keyboard/mouse, gamepad and the on-screen touch controls (which emulate a gamepad)
    /// all arrive through the same actions.
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
        private InputAction _dodge;
        private InputAction _skill1;
        private InputAction _skill2;
        private InputAction _interact;
        private InputAction _potion;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 AimScreenPosition => _aimPoint.ReadValue<Vector2>();
        public Vector2 AimStick => _aimStick.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed();

        /// <summary>True when the latest input came from keyboard/mouse, so aiming should follow the cursor.</summary>
        public bool UsingMouseAim { get; private set; }

        public event Action AttackPressed;
        public event Action DodgePressed;
        public event Action Skill1Pressed;
        public event Action Skill2Pressed;
        public event Action InteractPressed;
        public event Action PotionPressed;

        private void Awake()
        {
            _map = actions.FindActionMap(MapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", throwIfNotFound: true);
            _aimPoint = _map.FindAction("AimPoint", throwIfNotFound: true);
            _aimStick = _map.FindAction("AimStick", throwIfNotFound: true);
            _attack = _map.FindAction("Attack", throwIfNotFound: true);
            _dodge = _map.FindAction("Dodge", throwIfNotFound: true);
            _skill1 = _map.FindAction("Skill1", throwIfNotFound: true);
            _skill2 = _map.FindAction("Skill2", throwIfNotFound: true);
            _interact = _map.FindAction("Interact", throwIfNotFound: true);
            _potion = _map.FindAction("Potion", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _map.actionTriggered += OnAnyAction;
            _attack.performed += OnAttack;
            _dodge.performed += OnDodge;
            _skill1.performed += OnSkill1;
            _skill2.performed += OnSkill2;
            _interact.performed += OnInteract;
            _potion.performed += OnPotion;
            _map.Enable();
        }

        private void OnDisable()
        {
            _map.Disable();
            _map.actionTriggered -= OnAnyAction;
            _attack.performed -= OnAttack;
            _dodge.performed -= OnDodge;
            _skill1.performed -= OnSkill1;
            _skill2.performed -= OnSkill2;
            _interact.performed -= OnInteract;
            _potion.performed -= OnPotion;
        }

        private void OnAnyAction(InputAction.CallbackContext ctx)
        {
            var device = ctx.control?.device;
            if (device is Keyboard || device is Mouse)
                UsingMouseAim = true;
            else if (device is Gamepad || device is Touchscreen)
                UsingMouseAim = false;
        }

        private void OnAttack(InputAction.CallbackContext _) => AttackPressed?.Invoke();
        private void OnDodge(InputAction.CallbackContext _) => DodgePressed?.Invoke();
        private void OnSkill1(InputAction.CallbackContext _) => Skill1Pressed?.Invoke();
        private void OnSkill2(InputAction.CallbackContext _) => Skill2Pressed?.Invoke();
        private void OnInteract(InputAction.CallbackContext _) => InteractPressed?.Invoke();
        private void OnPotion(InputAction.CallbackContext _) => PotionPressed?.Invoke();
    }
}
