using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Screen shake through a CinemachineCameraOffset on the follow camera.</summary>
    public sealed class CameraShake : MonoBehaviour
    {
        private static CameraShake _instance;
        [SerializeField] private CinemachineCameraOffset offset;
        private float _trauma;

        private void Awake() => _instance = this;

        public static void Add(float amount)
        {
            if (_instance != null) _instance._trauma = Mathf.Min(1f, _instance._trauma + amount * GameSettings.Shake);
        }

        private void LateUpdate()
        {
            if (offset == null) return;
            _trauma = Mathf.Max(0f, _trauma - Time.deltaTime * 2.2f);
            float shake = _trauma * _trauma * 0.35f;
            float t = Time.time * 40f;
            offset.Offset = new Vector3((Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * shake, (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * shake, 0f);
        }
    }

}
