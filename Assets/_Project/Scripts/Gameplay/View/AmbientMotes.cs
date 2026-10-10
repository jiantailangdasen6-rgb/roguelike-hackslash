using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Embers drifting up through the crypt. Unlit so they glow in the dark.</summary>
    public sealed class AmbientMotes : MonoBehaviour
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private Material unlit;
        [SerializeField] private int count = 46;
        [SerializeField] private Rect area = new(-15f, -9f, 30f, 19f);

        private Transform[] _motes;
        private SpriteRenderer[] _renderers;
        private Vector2[] _drift;
        private float[] _phase;

        private void Start()
        {
            _motes = new Transform[count];
            _renderers = new SpriteRenderer[count];
            _drift = new Vector2[count];
            _phase = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                if (unlit != null) r.sharedMaterial = unlit;
                r.sortingOrder = 2800;
                _motes[i] = go.transform;
                _renderers[i] = r;
                Respawn(i, anywhere: true);
            }
        }

        private void Respawn(int i, bool anywhere)
        {
            _motes[i].position = new Vector3(
                Random.Range(area.xMin, area.xMax),
                anywhere ? Random.Range(area.yMin, area.yMax) : area.yMin - 0.5f);
            _drift[i] = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.15f, 0.45f));
            _phase[i] = Random.value * 10f;
            _motes[i].localScale = Vector3.one * Random.Range(0.6f, 1.1f);
        }

        private void Update()
        {
            if (_motes == null) return;
            float t = Time.time;
            for (int i = 0; i < _motes.Length; i++)
            {
                var m = _motes[i];
                Vector2 sway = new(Mathf.Sin(t * 0.8f + _phase[i]) * 0.12f, 0f);
                m.position += (Vector3)((_drift[i] + sway) * Time.deltaTime);
                float flicker = 0.35f + 0.45f * Mathf.PerlinNoise(_phase[i], t * 1.5f);
                _renderers[i].color = new Color(1f, 0.75f, 0.45f, flicker);
                if (m.position.y > area.yMax) Respawn(i, anywhere: false);
            }
        }
    }
}
