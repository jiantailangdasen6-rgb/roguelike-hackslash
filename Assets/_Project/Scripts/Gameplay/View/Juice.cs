using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Little touches that give bodies weight: puffs of dust under running feet and landing dodges, and
    /// fading afterimages behind dodges and dashes. Presentation only; one per scene, made on first use.
    /// </summary>
    public sealed class Juice : MonoBehaviour
    {
        private sealed class Bit
        {
            public SpriteRenderer R;
            public Sprite[] Frames;
            public float Fps;
            public float Age;
            public float Life;
            public Color Color;
            public Vector2 Velocity;
            public float Scale0;
            public float Scale1;
            public Material Default; // the lit sprite material a new renderer starts with
        }

        private static Juice _instance;
        private static Material _silhouette;
        private static bool _loaded;
        private readonly List<Bit> _live = new();
        private readonly Stack<Bit> _free = new();
        private Sprite[] _puff;

        private static Juice Instance
        {
            get
            {
                if (_instance == null) _instance = new GameObject("Juice").AddComponent<Juice>();
                return _instance;
            }
        }

        /// <summary>A small puff of dust at someone's feet (running steps, landing a dodge).</summary>
        public static void Dust(Vector2 at, float scale = 0.45f, float alpha = 0.32f)
        {
            var self = Instance;
            self._puff ??= SpriteSheet.Load("fx_puff")?.Sprites("default");
            if (self._puff == null || self._puff.Length == 0) return;
            var b = self.Take();
            b.Frames = self._puff;
            b.Fps = 16f;
            b.Life = self._puff.Length / 16f;
            b.Color = new Color(0.78f, 0.72f, 0.66f, alpha);
            b.Velocity = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.15f, 0.35f));
            b.Scale0 = scale * 0.7f;
            b.Scale1 = scale * 1.15f;
            b.R.sharedMaterial = b.Default;
            b.R.sprite = b.Frames[0];
            b.R.flipX = Random.value < 0.5f;
            b.R.sortingOrder = EnemyView.SortOrder(at.y) - 1;
            b.R.transform.position = at + new Vector2(Random.Range(-0.12f, 0.12f), 0.05f);
        }

        /// <summary>A coloured silhouette of the sprite as it is now, left behind to fade (dodges, dashes, blinks).</summary>
        public static void AfterImage(SpriteRenderer source, Color tint, float life = 0.26f)
        {
            if (source == null || source.sprite == null) return;
            var self = Instance;
            if (!_loaded)
            {
                _loaded = true;
                _silhouette = Resources.Load<Material>("SpriteFlash");
            }
            var b = self.Take();
            b.Frames = null;
            b.Life = life;
            b.Color = tint;
            b.Velocity = Vector2.zero;
            b.Scale0 = b.Scale1 = 1f;
            if (_silhouette != null) b.R.sharedMaterial = _silhouette;
            b.R.sprite = source.sprite;
            b.R.flipX = source.flipX;
            b.R.sortingOrder = source.sortingOrder - 1;
            b.R.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            b.R.transform.localScale = source.transform.lossyScale;
            b.Scale0 = b.Scale1 = source.transform.lossyScale.x;
        }

        private Bit Take()
        {
            Bit b;
            if (_free.Count > 0)
            {
                b = _free.Pop();
                b.R.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Bit");
                go.transform.SetParent(transform, false);
                b = new Bit { R = go.AddComponent<SpriteRenderer>() };
                b.Default = b.R.sharedMaterial;
            }
            b.Age = 0f;
            b.R.transform.localScale = Vector3.one;
            b.R.transform.rotation = Quaternion.identity;
            _live.Add(b);
            return b;
        }

        private void Update()
        {
            float dt = HitStop.DeltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var b = _live[i];
                b.Age += dt;
                float k = b.Age / b.Life;
                if (k >= 1f)
                {
                    b.R.gameObject.SetActive(false);
                    _live.RemoveAt(i);
                    _free.Push(b);
                    continue;
                }
                if (b.Frames != null)
                {
                    b.R.sprite = b.Frames[Mathf.Min(b.Frames.Length - 1, (int)(b.Age * b.Fps))];
                    b.R.transform.position += (Vector3)(b.Velocity * dt);
                    float s = Mathf.Lerp(b.Scale0, b.Scale1, k);
                    b.R.transform.localScale = new Vector3(s, s, 1f);
                }
                var c = b.Color;
                c.a *= (1f - k) * (1f - k);
                b.R.color = c;
            }
        }
    }
}
