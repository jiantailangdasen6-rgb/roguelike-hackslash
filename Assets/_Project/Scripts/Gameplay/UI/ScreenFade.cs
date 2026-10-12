using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Every scene (title, camp, a run) comes in from black instead of popping on. Lives across scene loads.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private const float FadeIn = 0.7f;
        private Image _black;
        private float _since;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("ScreenFade");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenFade>();
        }

        private void Awake()
        {
            var canvas = UIKit.CreateCanvas("ScreenFade", 95, transform).transform;
            _black = UIKit.Panel(canvas, "Black", Color.black);
            _black.rectTransform.Stretch();
            _black.raycastTarget = false;
            SceneManager.sceneLoaded += OnLoaded;
            _since = 0f;
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnLoaded;

        private void OnLoaded(Scene scene, LoadSceneMode mode) => _since = 0f;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_since / FadeIn);
            float a = 1f - k * k * (3f - 2f * k); // smooth
            _black.color = new Color(0f, 0f, 0f, a);
            _black.enabled = a > 0.001f;
        }
    }
}
