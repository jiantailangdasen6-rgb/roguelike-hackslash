using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Plays clips from a generated <see cref="SpriteSheet"/> on a SpriteRenderer. One-shot clips fall back
    /// to the looping base clip (idle/run/walk) when they finish. Supports a white hit-flash.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private SpriteSheet _sheet;
        private string _clip;
        private string _baseClip = "idle";
        private SpriteSheet.ClipInfo _info;
        private float _time;
        private float _flash;
        private bool _hold;     // freeze on the last frame of a one-shot (death, windup)
        private float _speed = 1f;

        public SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();
        public string Clip => _clip;
        public bool Finished => !_info.Loop && _time * _info.Fps >= _info.Frames;
        public bool Frozen { get; set; } // stops time (ice)

        public void SetSheet(SpriteSheet sheet)
        {
            _sheet = sheet;
            _clip = null;
            Play(_baseClip, force: true);
        }

        /// <summary>Looping clip to return to after one-shots.</summary>
        public void SetBase(string clip)
        {
            if (_baseClip == clip) return;
            bool wasOnBase = _clip == _baseClip;
            _baseClip = clip;
            if (wasOnBase) Play(clip);
        }

        /// <param name="hold">Stay on the last frame instead of returning to the base clip.</param>
        public void Play(string clip, bool force = false, bool hold = false, float speed = 1f)
        {
            if (_sheet == null || (!force && clip == _clip && _info.Loop)) return;
            if (!_sheet.TryGetClip(clip, out var info)) return;
            _clip = clip;
            _info = info;
            _time = 0f;
            _hold = hold;
            _speed = speed;
            Apply();
        }

        public void Flash(float seconds = 0.08f) => _flash = seconds;

        private void Update()
        {
            if (_sheet == null) return;
            float dt = HitStop.DeltaTime; // hit stop holds the pose and the white flash
            if (!Frozen) _time += dt * _speed;
            _flash -= dt;

            if (!_info.Loop && _time * _info.Fps >= _info.Frames && !_hold)
            {
                Play(_baseClip, force: true);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            var sprites = _sheet.Sprites(_clip, _flash > 0f);
            if (sprites == null || sprites.Length == 0) return;
            int frame = Mathf.FloorToInt(_time * _info.Fps);
            frame = _info.Loop ? frame % sprites.Length : Mathf.Min(frame, sprites.Length - 1);
            Renderer.sprite = sprites[frame];
        }
    }
}
