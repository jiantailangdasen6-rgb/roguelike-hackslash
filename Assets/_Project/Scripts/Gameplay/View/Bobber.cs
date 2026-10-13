using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Floats a decoration gently up and down around where it was placed (the wandering spirit).</summary>
    public sealed class Bobber : MonoBehaviour
    {
        [SerializeField] private float height = 0.18f;
        [SerializeField] private float speed = 1.6f;

        private Vector3 _home;
        private float _phase;

        private void Start()
        {
            _home = transform.localPosition;
            _phase = Random.value * 6f;
        }

        private void Update() =>
            transform.localPosition = _home + new Vector3(0f, height * Mathf.Sin(Time.time * speed + _phase));
    }
}
