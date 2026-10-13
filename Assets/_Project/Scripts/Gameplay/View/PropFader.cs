using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Tall props turn see-through while a player stands behind them, so nobody gets lost behind a pillar.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PropFader : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _alpha = 1f;

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        private void Update()
        {
            var sprite = _renderer.sprite;
            if (sprite == null) return;
            var bounds = _renderer.bounds;
            Vector2 basePos = transform.position;
            bool hidden = false;
            foreach (var p in NetPlayer.All)
            {
                Vector2 at = p.transform.position;
                // Behind = above the prop's base line and inside its silhouette.
                if (at.y > basePos.y + 0.05f && at.y < bounds.max.y - 0.2f && at.x > bounds.min.x - 0.1f && at.x < bounds.max.x + 0.1f)
                {
                    hidden = true;
                    break;
                }
            }
            _alpha = Mathf.MoveTowards(_alpha, hidden ? 0.45f : 1f, Time.deltaTime * 4f);
            // Out of the lantern's light in a dark hall, props sink into the dark too.
            float seen = DarkHallView.Visibility(basePos);
            var c = new Color(seen, seen, Mathf.Lerp(1f, seen, 0.8f), 1f);
            c.a = _alpha;
            _renderer.color = c;
        }
    }
}
