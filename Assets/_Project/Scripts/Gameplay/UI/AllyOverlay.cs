using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Screen-space markers that follow teammates: a small HP bar over each ally's head, a revive dial over
    /// anyone who is down (filling while a teammate stands close), and arrows at the screen edge pointing to
    /// allies who are off screen — big and red when they are down.
    /// </summary>
    public sealed class AllyOverlay : MonoBehaviour
    {
        private const float EdgeMargin = 70f;

        private sealed class Marker
        {
            public RectTransform Head;
            public UIKit.Bar Hp;
            public RectTransform HpRoot;
            public Text Name;
            public Image Dial;
            public Image DialBack;
            public Text DialText;
            public RectTransform Arrow;
            public Image ArrowImage;
            public Text ArrowText;
            public float ShownProgress;
        }

        private readonly Dictionary<NetPlayer, Marker> _markers = new();
        private readonly List<NetPlayer> _gone = new();
        private RectTransform _root;
        private Camera _camera;

        private void Awake()
        {
            _root = (RectTransform)UIKit.CreateCanvas("AllyOverlay", 15, transform).transform;
        }

        private Marker Create(NetPlayer p)
        {
            var m = new Marker();
            m.Head = UIKit.Rect("Ally", _root);
            m.Head.sizeDelta = new Vector2(120f, 120f);

            m.Name = UIKit.Label(m.Head, "", 18, TextAnchor.LowerCenter, p.Color);
            m.Name.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(200f, 26f), new Vector2(0f, 22f));
            m.Hp = UIKit.CreateBar(m.Head, "Hp", new Color(0.78f, 0.16f, 0.16f), new Color(0.05f, 0.02f, 0.03f, 0.85f));
            m.HpRoot = m.Hp.Root;
            m.HpRoot.Place(new Vector2(0.5f, 0.5f), new Vector2(76f, 9f), new Vector2(0f, 6f));

            // Revive dial: ring that fills as teammates help.
            m.DialBack = Image(m.Head, UIKit.RingSprite, new Color(0f, 0f, 0f, 0.6f), 74f, new Vector2(0f, 30f));
            m.Dial = Image(m.Head, UIKit.RingSprite, new Color(0.5f, 1f, 0.55f), 74f, new Vector2(0f, 30f));
            m.Dial.type = UnityEngine.UI.Image.Type.Filled;
            m.Dial.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            m.Dial.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            m.Dial.fillClockwise = true;
            m.DialText = UIKit.Title(m.Head, "", 20, TextAnchor.MiddleCenter, Color.white);
            m.DialText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(160f, 30f), new Vector2(0f, 30f));

            m.Arrow = UIKit.Rect("Arrow", _root);
            m.Arrow.sizeDelta = new Vector2(46f, 46f);
            m.ArrowImage = m.Arrow.gameObject.AddComponent<Image>();
            m.ArrowImage.sprite = UIKit.ArrowSprite;
            m.ArrowImage.raycastTarget = false;
            m.ArrowText = UIKit.Title(_root, "", 20, TextAnchor.MiddleCenter, Color.white);
            m.ArrowText.rectTransform.sizeDelta = new Vector2(160f, 30f);
            return m;
        }

        private static Image Image(Transform parent, Sprite sprite, Color color, float size, Vector2 offset)
        {
            var rect = UIKit.Rect("Img", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(size, size), offset);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            _gone.Clear();
            foreach (var p in _markers.Keys) if (p == null || !p.IsSpawned) _gone.Add(p);
            foreach (var p in _gone)
            {
                var m = _markers[p];
                if (m.Head != null) Destroy(m.Head.gameObject);
                if (m.Arrow != null) Destroy(m.Arrow.gameObject);
                if (m.ArrowText != null) Destroy(m.ArrowText.gameObject);
                _markers.Remove(p);
            }

            var local = NetPlayer.Local;
            bool running = RunDirector.Instance != null && RunDirector.Instance.Phase.Value == RunPhase.Playing;
            foreach (var p in NetPlayer.All)
            {
                if (!_markers.TryGetValue(p, out var m)) _markers[p] = m = Create(p);
                bool self = p == local;
                bool downed = p.Downed.Value;
                Vector3 world = p.transform.position + Vector3.up * 1.55f;
                Vector3 screen = _camera.WorldToScreenPoint(world);
                bool onScreen = screen.z > 0f && screen.x > 0f && screen.x < Screen.width && screen.y > 0f && screen.y < Screen.height;

                // Overhead: allies get name + HP; anyone down gets the revive dial (including yourself).
                m.Head.gameObject.SetActive(running && onScreen && (!self || downed));
                if (m.Head.gameObject.activeSelf)
                {
                    m.Head.position = screen;
                    m.Name.text = $"{p.DisplayName} {p.ClassDef.Name}";
                    m.Name.enabled = !self && !downed;
                    m.HpRoot.gameObject.SetActive(!self && !downed);
                    m.Hp.Fill = p.Hp.Value / Mathf.Max(1f, p.MaxHp.Value);

                    m.Dial.enabled = m.DialBack.enabled = m.DialText.enabled = downed;
                    if (downed)
                    {
                        m.ShownProgress = Mathf.MoveTowards(m.ShownProgress, p.ReviveProgress.Value, Time.deltaTime * 1.5f);
                        bool helped = p.BeingRevived;
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                        m.Dial.fillAmount = m.ShownProgress;
                        m.Dial.color = helped ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.85f, 0.4f);
                        m.DialBack.color = helped ? new Color(0f, 0.15f, 0.05f, 0.7f) : new Color(0.5f, 0.05f, 0.05f, 0.45f + 0.35f * pulse);
                        m.DialText.text = helped ? $"{m.ShownProgress * 100f:0}%" : self ? "助けを待て" : "助けて!";
                        m.DialText.color = helped ? new Color(0.75f, 1f, 0.75f) : new Color(1f, 0.6f + 0.3f * pulse, 0.55f);
                    }
                    else
                    {
                        m.ShownProgress = 0f;
                    }
                }

                // Off-screen: arrow at the edge toward the ally.
                bool showArrow = running && !self && !onScreen;
                m.Arrow.gameObject.SetActive(showArrow);
                m.ArrowText.gameObject.SetActive(showArrow);
                if (!showArrow) continue;

                Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 dir = (Vector2)screen - center;
                if (screen.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                float scaleX = (Screen.width * 0.5f - EdgeMargin) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
                float scaleY = (Screen.height * 0.5f - EdgeMargin) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                Vector2 at = center + dir * Mathf.Min(scaleX, scaleY);
                m.Arrow.position = at;
                m.Arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                float distance = local != null ? Vector2.Distance(local.transform.position, p.transform.position) : 0f;
                if (downed)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                    m.Arrow.localScale = Vector3.one * (1.3f + 0.25f * pulse);
                    m.ArrowImage.color = Color.Lerp(new Color(1f, 0.2f, 0.15f), Color.white, pulse * 0.4f);
                    m.ArrowText.text = $"{p.DisplayName} 瀕死 {distance:0}m";
                    m.ArrowText.color = new Color(1f, 0.5f, 0.45f);
                }
                else
                {
                    m.Arrow.localScale = Vector3.one * 0.8f;
                    m.ArrowImage.color = new Color(p.Color.r, p.Color.g, p.Color.b, 0.8f);
                    m.ArrowText.text = p.DisplayName;
                    m.ArrowText.color = p.Color;
                }
                m.ArrowText.rectTransform.position = at - dir.normalized * 46f;
            }
        }
    }
}
