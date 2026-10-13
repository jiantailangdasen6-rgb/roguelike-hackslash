using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>
    /// Lets the point lights shade the art with its normal maps (NormalMaps in the art generator) when the picture
    /// quality is high. Lights are made in many places (candles, torches, glows, effects), so this looks over the
    /// scene's lights a few times a second and sets the new ones up; one object lives in every scene.
    /// </summary>
    public sealed class LightingQuality : MonoBehaviour
    {
        /// <summary>How far above the floor the lights hang: lower makes the relief stronger.</summary>
        private const float SmallLightHeight = 1.4f, BigLightHeight = 2.6f;

        // Light2D only reads these two (they are set in the inspector), so they are written directly.
        private static readonly FieldInfo QualityField = typeof(Light2D).GetField("m_NormalMapQuality", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DistanceField = typeof(Light2D).GetField("m_NormalMapDistance", BindingFlags.NonPublic | BindingFlags.Instance);

        private float _next;
        private int _logged;
        private bool _high;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (_, _) => Create();
            Create();
        }

        private static void Create()
        {
            if (FindAnyObjectByType<LightingQuality>() != null) return;
            new GameObject("Lighting Quality").AddComponent<LightingQuality>();
        }

        private void OnEnable() => GameSettings.Changed += Refresh;
        private void OnDisable() => GameSettings.Changed -= Refresh;

        private void Refresh() => _next = 0f;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.4f;
            _high = GameSettings.HighQuality;
            // Each hero carries a faint light at high quality (the rooms are darker then): it shades them and the floor round them.
            foreach (var player in NetPlayer.All)
            {
                if (player == null) continue;
                var aura = player.transform.Find("Aura");
                if (aura == null)
                {
                    var go = new GameObject("Aura");
                    go.transform.SetParent(player.transform, false);
                    go.transform.localPosition = new Vector3(-0.6f, 1.6f); // a little above and to the left: lit from the top-left like the art
                    var l = go.AddComponent<Light2D>();
                    l.lightType = Light2D.LightType.Point;
                    l.color = new Color(1f, 0.9f, 0.78f);
                    l.intensity = 0.5f;
                    l.pointLightOuterRadius = 4.5f;
                    l.pointLightInnerRadius = 0.3f;
                    l.falloffIntensity = 0.7f;
                    aura = go.transform;
                }
                var light = aura.GetComponent<Light2D>();
                bool on = _high && !player.Downed.Value;
                if (light.enabled != on) light.enabled = on;
            }
            foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (light.lightType == Light2D.LightType.Global) continue;
                var quality = _high ? Light2D.NormalMapQuality.Accurate : Light2D.NormalMapQuality.Disabled;
                if (light.normalMapQuality != quality) QualityField?.SetValue(light, quality);
                float distance = light.pointLightOuterRadius > 5f ? BigLightHeight : SmallLightHeight;
                if (!Mathf.Approximately(light.normalMapDistance, distance)) DistanceField?.SetValue(light, distance);
                if (_logged < 3 && CommandLine.Has("-lightlog")) { _logged++; Debug.Log($"[Lighting] {light.name} quality={light.normalMapQuality} distance={light.normalMapDistance} field={(QualityField != null)}"); }
            }
        }
    }
}
