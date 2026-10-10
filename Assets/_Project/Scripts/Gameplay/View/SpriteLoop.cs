using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Loops a sheet clip on a SpriteRenderer (props, portals) without the full animator.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteLoop : MonoBehaviour
    {
        [SerializeField] private string sheet;
        [SerializeField] private float fps = 8f;
        private Sprite[] _frames;
        private SpriteRenderer _renderer;
        private float _offset;

        public void Configure(string sheetName, float framesPerSecond)
        {
            sheet = sheetName;
            fps = framesPerSecond;
            _frames = null;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _offset = Random.value;
        }

        private void Update()
        {
            if (_frames == null)
            {
                var s = SpriteSheet.Load(sheet);
                if (s == null) return;
                _frames = s.Sprites(s.Clips[0].Name);
            }
            _renderer.sprite = _frames[Mathf.FloorToInt((Time.time + _offset) * fps) % _frames.Length];
        }
    }
}
