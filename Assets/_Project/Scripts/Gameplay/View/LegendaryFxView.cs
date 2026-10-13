using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// Shows legendary effects firing: an orange burst ring with a flash of light and the item's name rising from
    /// where it happened, and jagged lightning for chained bolts (the "雷鳴の" staff, and the chain-bolt perk in blue).
    /// </summary>
    public sealed class LegendaryFxView : MonoBehaviour
    {
        private static readonly Color Legendary = new(1f, 0.6f, 0.2f);
        private static readonly Color Bolt = new(0.65f, 0.85f, 1f);
        private static readonly Color StormColor = new(1f, 0.88f, 0.4f);

        private sealed class Fx
        {
            public GameObject Root;
            public SpriteRenderer Ring;
            public Light2D Light;
            public UnityEngine.UI.Text Label;
            public Vector3 LabelWorld;
            public List<SpriteRenderer> Segments;
            public float Born;
            public float Life;
            public Color Color;
        }

        private readonly List<Fx> _live = new();
        private RectTransform _labels;
        private Camera _camera;
        private Sprite _ring;
        private Sprite _dot;
        private Sprite _bar;

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        private void OnEvent(SimEvent e)
        {
            if (e.Type == SimEventType.GodSign && (GodSignKind)e.Int == GodSignKind.Lightning)
            {
                // The storm god's lightning comes straight down from above the screen.
                _bar ??= Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f); // a plain bar: crisp bolts from the sky
                bool big = e.Value > 0f;
                Vector2 sky = e.Pos + new Vector2(Random.Range(-1.2f, 1.2f), big ? 9f : 7f);
                Lightning(sky, e.Pos, big ? new Color(1f, 0.85f, 0.3f) : StormColor, big ? 0.55f : 0.42f, big ? 1.25f : 0.8f, 8, _bar);
                return;
            }
            if (e.Type != SimEventType.LegendaryProc) return;
            _ring ??= SpriteSheet.Load("fx_revive_ring", quiet: true)?.First;
            _dot ??= SpriteSheet.Load("fx_circle", quiet: true)?.First;
            var id = (LegendaryId)e.Int;
            bool chain = e.Vec.sqrMagnitude > 0.0001f && (id == LegendaryId.ThunderStaff || id == LegendaryId.None);
            if (chain) Lightning(e.Pos, e.Vec, id == LegendaryId.None ? Bolt : Color.Lerp(Bolt, Legendary, 0.35f));
            if (id != LegendaryId.None) Burst(chain ? e.Vec : e.Pos, ItemCatalog.GetLegendary(id));
        }

        private void Burst(Vector2 at, LegendaryDef def)
        {
            var fx = New(at, 0.6f, Legendary);
            fx.Ring = Child(fx.Root, "Ring", _ring, 4000);
            fx.Light = AddLight(fx.Root, Legendary, 2.6f);
            if (def != null)
            {
                _labels ??= (RectTransform)UIKit.CreateCanvas("LegendaryLabels", 12, transform).transform;
                fx.Label = UIKit.Title(_labels, def.Name.TrimEnd('の'), 34, TextAnchor.MiddleCenter, Legendary);
                fx.Label.rectTransform.sizeDelta = new Vector2(400f, 60f);
                fx.LabelWorld = (Vector3)at + new Vector3(0f, 1.3f);
            }
        }

        /// <summary>A jagged bolt from one point to another, built from thin stretched dots.</summary>
        private void Lightning(Vector2 from, Vector2 to, Color color, float jitter = 0.4f, float width = 1f, int joints = 6, Sprite sprite = null)
        {
            var fx = New(from, 0.32f, color);
            fx.Segments = new List<SpriteRenderer>();
            Vector2 dir = to - from;
            Vector2 normal = new Vector2(-dir.y, dir.x).normalized;
            Vector2 prev = from;
            sprite ??= _dot;
            float unit = Mathf.Max(0.01f, sprite != null ? sprite.bounds.size.x : 1f);
            for (int k = 1; k <= joints; k++)
            {
                float t = k / (float)joints;
                Vector2 next = Vector2.Lerp(from, to, t) + (k < joints ? normal * Random.Range(-jitter, jitter) : Vector2.zero);
                Vector2 d = next - prev;
                // A pale glow around a white-hot core.
                foreach (var (thickness, order) in new[] { (0.75f, 4050), (0.28f, 4051) })
                {
                    var seg = Child(fx.Root, "Bolt", sprite, order);
                    seg.transform.position = (prev + next) * 0.5f;
                    seg.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    seg.transform.localScale = new Vector3(d.magnitude / unit + 0.1f, thickness * width * 0.5f / unit, 1f);
                    fx.Segments.Add(seg);
                }
                prev = next;
            }
            fx.Light = AddLight(fx.Root, color, 1.6f);
            fx.Light.transform.position = to;
        }

        private Fx New(Vector2 at, float life, Color color)
        {
            var root = new GameObject("LegendaryFx");
            root.transform.SetParent(transform, false);
            root.transform.position = at;
            var fx = new Fx { Root = root, Born = Time.time, Life = life, Color = color };
            _live.Add(fx);
            return fx;
        }

        private static SpriteRenderer Child(GameObject parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private static Light2D AddLight(GameObject parent, Color color, float radius)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent.transform, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = 0.1f;
            light.intensity = 2f;
            return light;
        }

        private void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var fx = _live[i];
                float t = (Time.time - fx.Born) / fx.Life;
                if (t >= 1f)
                {
                    Destroy(fx.Root);
                    if (fx.Label != null) Destroy(fx.Label.gameObject);
                    _live.RemoveAt(i);
                    continue;
                }
                float fade = 1f - t;
                if (fx.Ring != null)
                {
                    float s = Mathf.Lerp(0.6f, 2.4f, 1f - (1f - t) * (1f - t));
                    fx.Ring.transform.localScale = Vector3.one * s;
                    fx.Ring.color = new Color(fx.Color.r, fx.Color.g, fx.Color.b, 0.9f * fade);
                }
                if (fx.Label != null)
                {
                    if (_camera == null) _camera = Camera.main;
                    if (_camera != null) fx.Label.transform.position = _camera.WorldToScreenPoint(fx.LabelWorld + new Vector3(0f, 0.6f * t));
                    fx.Label.color = new Color(fx.Color.r, fx.Color.g, fx.Color.b, Mathf.Clamp01(fade * 1.6f));
                }
                if (fx.Segments != null)
                    for (int s = 0; s < fx.Segments.Count; s++)
                    {
                        bool glow = s % 2 == 0;
                        var c = glow ? fx.Color : Color.Lerp(Color.white, fx.Color, t);
                        // Flickers like real lightning before fading.
                        float flick = 0.7f + 0.3f * Mathf.Sin((Time.time - fx.Born) * 90f);
                        fx.Segments[s].color = new Color(c.r, c.g, c.b, (glow ? 0.45f : 1f) * fade * flick);
                    }
                if (fx.Light != null) fx.Light.intensity = 2f * fade;
            }
        }
    }
}
