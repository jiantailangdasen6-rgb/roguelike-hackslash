using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>Candle/torch flicker for a 2D light.</summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class LightFlicker : MonoBehaviour
    {
        [SerializeField] private float baseIntensity = 0.9f;
        [SerializeField] private float amount = 0.18f;
        private Light2D _light;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * 6f);
            _light.intensity = baseIntensity * (1f - amount + amount * 2f * n);
        }
    }

}
