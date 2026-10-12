using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>Plays a sprite-sheet clip on a UI Image (class previews in the lobby).</summary>
    [RequireComponent(typeof(Image))]
    public sealed class UISpriteAnimator : MonoBehaviour
    {
        private Image _image;
        private Sprite[] _frames;
        private float _fps = 6f;
        private float _time;

        public void Play(string sheet, string clip)
        {
            _image = GetComponent<Image>();
            var s = SpriteSheet.Load(sheet);
            _frames = s?.Sprites(clip);
            if (s != null && s.TryGetClip(clip, out var info)) _fps = info.Fps;
            _time = 0f;
            Apply();
        }

        /// <summary>Stops animating (the Image keeps whatever sprite is set on it).</summary>
        public void Stop() => _frames = null;

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            if (_frames == null || _frames.Length == 0 || _image == null) return;
            _image.sprite = _frames[Mathf.FloorToInt(_time * _fps) % _frames.Length];
        }
    }
}
