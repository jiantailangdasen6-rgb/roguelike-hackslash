using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// The dark hall (RunSimulation.Coop) for the local player standing in it: the room's light sinks to almost
    /// nothing, and a lantern - lying on the floor or carried by someone - throws a warm circle of light. A faint
    /// ring marks how far it reaches (out there, blows glance off the monsters). Twin plates get a band of light
    /// between them while both are held.
    /// </summary>
    public sealed class DarkHallView : MonoBehaviour
    {
        private const float Darkness = 0.1f;
        private static readonly Color LanternColor = new(1f, 0.74f, 0.4f);

        private Light2D _global;
        private float _base = 1f;
        private float _dark;
        private GameObject _lantern;
        private SpriteRenderer _lanternSprite;
        private Light2D _lanternLight;
        private Light2D _lanternCore;
        private SpriteRenderer[] _edge;
        private Sprite[] _frames;

        private void Start()
        {
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global) _global = l;

            _lantern = new GameObject("Lantern");
            _lantern.transform.SetParent(transform, false);
            _lanternSprite = _lantern.AddComponent<SpriteRenderer>();
            _frames = SpriteSheet.Load("prop_lantern", quiet: true)?.Sprites("default");
            _lanternLight = AddLight(_lantern, LanternColor, RunSimulation.LanternRadius, 1.15f, 1.5f);
            _lanternCore = AddLight(_lantern, new Color(1f, 0.9f, 0.6f), 2.2f, 1.2f, 0.2f);

            // The reach of the light: a ring of small drifting sparks on the floor (a big ring sprite would turn into blocks).
            _edgeRoot = new GameObject("LanternEdge").transform;
            _edgeRoot.SetParent(transform, false);
            var dot = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            _edge = new SpriteRenderer[EdgeDots];
            for (int i = 0; i < EdgeDots; i++)
            {
                var go = new GameObject("Spark");
                go.transform.SetParent(_edgeRoot, false);
                _edge[i] = go.AddComponent<SpriteRenderer>();
                _edge[i].sprite = dot;
                _edge[i].sortingOrder = -470;
                go.transform.localScale = Vector3.one * (i % 3 == 0 ? 0.1f : 0.0625f);
            }
            _lantern.SetActive(false);
            _edgeRoot.gameObject.SetActive(false);
        }

        private const int EdgeDots = 54;
        private Transform _edgeRoot;

        // What EnemyView needs to draw monsters in the dark (only one dark hall is shown at a time: the local one).
        private static bool _shrouding;
        private static Vector2 _lightAt;
        private static float _shroud;

        /// <summary>1 in the light; down to a shadow out of the lantern's light in a dark hall.</summary>
        public static float Visibility(Vector2 p)
        {
            if (!_shrouding || _shroud <= 0f) return 1f;
            float d = (p - _lightAt).magnitude;
            float outside = Mathf.Clamp01((d - (RunSimulation.LanternRadius - 1.2f)) / 1.2f);
            return Mathf.Lerp(1f, 0.22f, outside * _shroud);
        }

        private SpriteRenderer _beam;
        private SpriteRenderer _beamCore;
        private Light2D _beamLight;

        /// <summary>The twin plates: a band of light joins them as the seal charges.</summary>
        private void UpdateTwinBeam(RunDirector d, MapRoom room)
        {
            float charge = room != null && room.Kind == RoomKind.Puzzle && room.Puzzle == PuzzleKind.Twin && d.IsFighting(room.Id) ? d.PuzzleValue.Value : 0f;
            if (_beam == null)
            {
                var bar = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                SpriteRenderer Bar(string name, int order)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(transform, false);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = bar;
                    r.sortingOrder = order;
                    return r;
                }
                _beam = Bar("TwinBeam", -460);
                _beamCore = Bar("TwinBeamCore", -459);
                _beamLight = AddLight(gameObject, new Color(1f, 0.82f, 0.45f), 4f, 0f, 0.5f);

            }
            bool on = charge > 0.01f;
            _beam.enabled = _beamCore.enabled = _beamLight.enabled = on;
            if (!on) return;
            Vector2 a = room.Origin + RunSimulation.TwinSpots[0], b = room.Origin + RunSimulation.TwinSpots[1];
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (6f + 10f * charge));
            foreach (var (r, thick, alpha) in new[] { (_beam, 0.25f + 0.35f * charge, 0.35f), (_beamCore, 0.06f + 0.1f * charge, 0.95f) })
            {
                r.transform.position = (a + b) * 0.5f;
                r.transform.localScale = new Vector3((b - a).magnitude, thick, 1f);
                r.color = new Color(1f, 0.85f, 0.5f, alpha * pulse * Mathf.Clamp01(charge * 3f));
            }
            _beamLight.transform.position = (a + b) * 0.5f;
            _beamLight.intensity = 0.6f + 1.2f * charge;
            _beamLight.pointLightOuterRadius = 3f + 5f * charge;
        }

        private static Light2D AddLight(GameObject parent, Color color, float radius, float intensity, float inner)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.4f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = inner;
            light.intensity = intensity;
            return light;
        }

        private void Update()
        {
            var d = RunDirector.Instance;
            bool inDark = false;
            MapRoom room = null;
            if (d != null && d.IsSpawned && d.Started.Value)
            {
                room = d.LocalRoom;
                inDark = room != null && room.Dark && !d.IsCleared(room.Id);
            }

            // Fade the room's light (the area look sets the base; we only scale it while it is dark).
            if (_global != null)
            {
                if (_dark <= 0.001f) _base = _global.intensity;
                _dark = Mathf.MoveTowards(_dark, inDark ? 1f : 0f, Time.deltaTime * (inDark ? 1.2f : 0.8f));
                if (_dark > 0.001f) _global.intensity = _base * Mathf.Lerp(1f, Darkness, _dark);
                else _global.intensity = _base;
            }

            UpdateTwinBeam(d, room);

            bool show = inDark || (_dark > 0.01f && room != null && room.Dark);
            if (_lantern.activeSelf != show)
            {
                _lantern.SetActive(show);
                _edgeRoot.gameObject.SetActive(show);
            }
            _shrouding = show;
            _shroud = _dark;
            if (!show) return;

            // Carried: it swings at the carrier's side. On the floor: it bobs a little to say "pick me up".
            Vector2 at = d.LanternPos.Value;
            bool carried = false;
            ulong holder = d.LanternHolder.Value;
            if (holder != 0)
                foreach (var p in NetPlayer.All)
                    if (p.OwnerClientId == holder - 1)
                    {
                        at = (Vector2)p.transform.position + new Vector2(0.42f, 0.15f + 0.05f * Mathf.Sin(Time.time * 6f));
                        carried = true;
                        break;
                    }
            if (!carried) at += new Vector2(0f, 0.12f * Mathf.Sin(Time.time * 2.4f));
            _lantern.transform.position = at;

            if (_frames != null && _frames.Length > 0) _lanternSprite.sprite = _frames[(int)(Time.time * 8f) % _frames.Length];
            _lanternSprite.sortingOrder = EnemyView.SortOrder(at.y) + (carried ? 2 : 0);
            float flicker = 0.92f + 0.05f * Mathf.Sin(Time.time * 13f) + 0.03f * Mathf.Sin(Time.time * 31f);
            float fade = Mathf.Clamp01(_dark);
            _lanternLight.intensity = 1.15f * flicker * fade;
            _lanternCore.intensity = 1.2f * flicker;
            // The reach of the light: sparks circling slowly on its edge, a little brighter while nobody carries it.
            Vector2 center = carried ? at - new Vector2(0.42f, 0.15f) : at;
            _lightAt = center;
            _edgeRoot.position = center;
            float spin = Time.time * 0.12f;
            for (int i = 0; i < _edge.Length; i++)
            {
                float a = i / (float)_edge.Length * Mathf.PI * 2f + spin;
                float r = RunSimulation.LanternRadius + 0.12f * Mathf.Sin(Time.time * 1.7f + i * 1.3f);
                _edge[i].transform.localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f + i * 2.1f);
                _edge[i].color = new Color(1f, 0.8f, 0.45f, (carried ? 0.35f : 0.6f) * twinkle * fade);
            }
        }
    }
}
