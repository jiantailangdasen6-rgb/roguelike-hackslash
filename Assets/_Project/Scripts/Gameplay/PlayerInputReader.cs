using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Reads the "Player" action map and exposes device-agnostic input to gameplay code.
    /// Keyboard, gamepad and the on-screen touch controls (which emulate a gamepad)
    /// all arrive through the same actions. Attacks are automatic, so only movement is read.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public const string MapName = "Player";

        [SerializeField] private InputActionAsset actions;

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _dash;
        private InputAction _inventory;

        public Vector2 Move => _move.ReadValue<Vector2>();

        public event Action DashPressed;
        public event Action InventoryPressed;

        private void Awake()
        {
            _map = actions.FindActionMap(MapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", throwIfNotFound: true);
            _dash = _map.FindAction("Dash", throwIfNotFound: true);
            _inventory = _map.FindAction("Inventory", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _dash.performed += OnDash;
            _inventory.performed += OnInventory;
            _map.Enable();
        }

        private void OnDisable()
        {
            _map.Disable();
            _dash.performed -= OnDash;
            _inventory.performed -= OnInventory;
        }

        private void OnDash(InputAction.CallbackContext _) => DashPressed?.Invoke();
        private void OnInventory(InputAction.CallbackContext _) => InventoryPressed?.Invoke();
    }
}
