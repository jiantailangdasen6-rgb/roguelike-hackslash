using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Squash and stretch on a sprite: a kick sets how far it is pulled (positive = tall and thin, negative = short
    /// and wide) and a damped spring brings it back, wobbling once. Holds during hit stop. Sprites stand on their
    /// feet (pivot at the ground), so a squash pushes them into the floor rather than shrinking them in the air.
    /// </summary>
    public sealed class Squash : MonoBehaviour
    {
        private const float Stiffness = 320f;
        private const float Damping = 16f;

        private float _amount;
        private float _speed;
        private Vector3 _base = Vector3.one;
        private bool _hasBase;

        /// <summary>The scale it rests at (enemies change it when they are reused).</summary>
        public Vector3 Base
        {
            get => _hasBase ? _base : transform.localScale;
            set
            {
                _base = value;
                _hasBase = true;
                _amount = _speed = 0f;
                transform.localScale = value;
            }
        }

        public static void Kick(Component on, float amount)
        {
            if (on == null) return;
            var s = on.GetComponent<Squash>();
            if (s == null) s = on.gameObject.AddComponent<Squash>();
            s.Kick(amount);
        }

        public void Kick(float amount)
        {
            if (!_hasBase) Base = transform.localScale;
            // A new kick replaces a weaker one still ringing, and never stacks into a rubber band.
            if (Mathf.Abs(amount) >= Mathf.Abs(_amount)) _amount = Mathf.Clamp(amount, -0.45f, 0.45f);
            _speed = 0f;
            enabled = true;
        }

        private void Update()
        {
            float dt = Mathf.Min(HitStop.DeltaTime, 1f / 30f);
            if (dt <= 0f) return;
            _speed += (-Stiffness * _amount - Damping * _speed) * dt;
            _amount += _speed * dt;
            if (Mathf.Abs(_amount) < 0.002f && Mathf.Abs(_speed) < 0.05f)
            {
                _amount = _speed = 0f;
                transform.localScale = _base;
                enabled = false;
                return;
            }
            // Keeps the area: taller is thinner.
            transform.localScale = new Vector3(_base.x / (1f + _amount), _base.y * (1f + _amount), _base.z);
        }
    }
}
