using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// A paladin's devotion: a chain of golden light between the paladin and the teammate whose wounds they share,
    /// for as long as it lasts. Beads of light run along it from the paladin towards the teammate.
    /// </summary>
    public sealed class TetherView : MonoBehaviour
    {
        private const int Beads = 14;

        private sealed class Tether
        {
            public ulong From, To;
            public float Until;
            public SpriteRenderer[] Beads;
            public Light2D Light;
            public GameObject Root;
        }

        private static readonly List<(ulong From, ulong To, float Seconds)> Pending = new();
        private readonly List<Tether> _live = new();
        private Sprite _dot;

        /// <summary>Called by the event presenter when a devotion begins.</summary>
        public static void Bind(ulong paladin, ulong teammate, float seconds) => Pending.Add((paladin, teammate, seconds));

        private static NetPlayer Find(ulong client)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == client) return p;
            return null;
        }

        private void Update()
        {
            foreach (var (from, to, seconds) in Pending)
            {
                _live.RemoveAll(t =>
                {
                    if (t.From != from || t.To != to) return false;
                    Destroy(t.Root);
                    return true;
                });
                _dot ??= Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                var root = new GameObject("Tether");
                root.transform.SetParent(transform, false);
                var t = new Tether { From = from, To = to, Until = Time.time + seconds, Root = root, Beads = new SpriteRenderer[Beads] };
                for (int i = 0; i < Beads; i++)
                {
                    var go = new GameObject("Bead");
                    go.transform.SetParent(root.transform, false);
                    t.Beads[i] = go.AddComponent<SpriteRenderer>();
                    t.Beads[i].sprite = _dot;
                    t.Beads[i].sortingOrder = 2900;
                }
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(root.transform, false);
                t.Light = lightGo.AddComponent<Light2D>();
                t.Light.lightType = Light2D.LightType.Point;
                t.Light.color = new Color(1f, 0.85f, 0.5f);
                t.Light.pointLightOuterRadius = 3f;
                t.Light.intensity = 0.8f;
                _live.Add(t);
            }
            Pending.Clear();

            for (int k = _live.Count - 1; k >= 0; k--)
            {
                var t = _live[k];
                var a = Find(t.From);
                var b = Find(t.To);
                if (Time.time > t.Until || a == null || b == null || a.Downed.Value || b.Downed.Value)
                {
                    Destroy(t.Root);
                    _live.RemoveAt(k);
                    continue;
                }
                Vector2 from = (Vector2)a.transform.position + new Vector2(0f, 0.7f);
                Vector2 to = (Vector2)b.transform.position + new Vector2(0f, 0.7f);
                float left = t.Until - Time.time;
                float fade = Mathf.Clamp01(left / 0.6f) * Mathf.Clamp01((Time.time - (t.Until - 6f)) / 0.3f + 0.2f);
                for (int i = 0; i < Beads; i++)
                {
                    float u = (i + (Time.time * 1.8f) % 1f) / Beads;
                    // A gentle sag in the middle, like a chain.
                    Vector2 at = Vector2.Lerp(from, to, u) + Vector2.down * Mathf.Sin(u * Mathf.PI) * 0.35f;
                    var r = t.Beads[i];
                    r.transform.position = at;
                    float size = i % 3 == 0 ? 0.14f : 0.09f;
                    r.transform.localScale = Vector3.one * size;
                    r.color = new Color(1f, 0.9f, 0.55f, (i % 3 == 0 ? 0.95f : 0.7f) * fade);
                }
                t.Light.transform.position = (from + to) * 0.5f;
                t.Light.intensity = 0.8f * fade;
            }
        }
    }
}
