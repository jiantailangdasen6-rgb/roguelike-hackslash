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
        private InputAction _ultimate;

        public Vector2 Move => TouchInput.Active && TouchInput.Move.sqrMagnitude > 0.0001f ? TouchInput.Move : _move.ReadValue<Vector2>();
        public Vector2 AimScreenPosition => _aimPoint.ReadValue<Vector2>();
        public Vector2 AimStick => _aimStick.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed() || TouchInput.AttackHeld;

        /// <summary>True when the latest input came from keyboard/mouse, so aiming follows the cursor.</summary>
        public bool UsingMouse { get => _usingMouse && !TouchInput.Active; private set => _usingMouse = value; }
        private bool _usingMouse = true;

        public event Action Skill1Pressed;
        public event Action Skill2Pressed;
        public event Action DodgePressed;
        public event Action UltimatePressed;

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
            _ultimate = _map.FindAction("Ultimate", false);
            ApplyKeys();
        }

        /// <summary>The keyboard keys chosen in the settings replace the first keyboard binding of each skill / the dodge.</summary>
        private void ApplyKeys()
        {
            Rebind(_skill1, KeyAction.Skill1);
            Rebind(_skill2, KeyAction.Skill2);
            Rebind(_dodge, KeyAction.Dodge);
            if (_ultimate != null) Rebind(_ultimate, KeyAction.Ultimate);
        }

        private static void Rebind(InputAction action, KeyAction key)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (!action.bindings[i].path.StartsWith("<Keyboard>")) continue;
                action.ApplyBindingOverride(i, GameSettings.Path(GameSettings.KeyFor(key)));
                return;
            }
        }

        private void OnEnable()
        {
            GameSettings.Changed += ApplyKeys;
            _map.actionTriggered += OnAny;
            _skill1.performed += OnSkill1;
            _skill2.performed += OnSkill2;
            _dodge.performed += OnDodge;
            if (_ultimate != null) _ultimate.performed += OnUltimate;
            _map.Enable();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= ApplyKeys;
            _map.Disable();
            _map.actionTriggered -= OnAny;
            _skill1.performed -= OnSkill1;
            _skill2.performed -= OnSkill2;
            _dodge.performed -= OnDodge;
            if (_ultimate != null) _ultimate.performed -= OnUltimate;
        }

        private void OnAny(InputAction.CallbackContext ctx)
        {
            var device = ctx.control?.device;
            if (device is Keyboard || device is Mouse) UsingMouse = true;
            else if (device is Gamepad || device is Touchscreen) UsingMouse = false;
        }

        // The on-screen buttons press the same skills.
        private void Update()
        {
            if (TouchInput.Pressed(TouchButton.Skill1)) Skill1Pressed?.Invoke();
            if (TouchInput.Pressed(TouchButton.Skill2)) Skill2Pressed?.Invoke();
            if (TouchInput.Pressed(TouchButton.Dodge)) DodgePressed?.Invoke();
            if (TouchInput.Pressed(TouchButton.Ultimate)) UltimatePressed?.Invoke();
        }

        private void OnSkill1(InputAction.CallbackContext _) => Skill1Pressed?.Invoke();
        private void OnSkill2(InputAction.CallbackContext _) => Skill2Pressed?.Invoke();
        private void OnDodge(InputAction.CallbackContext _) => DodgePressed?.Invoke();
        private void OnUltimate(InputAction.CallbackContext _) => UltimatePressed?.Invoke();
    }
}
