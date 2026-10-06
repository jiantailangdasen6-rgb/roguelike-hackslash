using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Turns simulation events into visuals on every peer: projectiles, slashes, death puffs,
    /// XP gems, damage numbers and the exit portal.
    /// </summary>
    public sealed class EventPresenter : MonoBehaviour
    {
        private const int MaxDamageNumbers = 40;

        private sealed class Mover
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life;
            public float Spin;
        }

        private sealed class Fader
        {
            public SpriteRenderer Renderer;
            public float Age;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public Color Color;
        }

        private sealed class FlyingGem
        {
            public SpriteRenderer Renderer;
            public Transform Target;
            public float T;
            public Vector3 Start;
        }

        private sealed class DamageNumber
        {
            public Text Text;
            public Vector3 World;
            public float Age;
        }

        private readonly Dictionary<int, Mover> _projectiles = new();
        private readonly List<int> _expired = new();
        private readonly List<Fader> _faders = new();
        private readonly Dictionary<int, SpriteRenderer> _gems = new();
        private readonly List<FlyingGem> _flyingGems = new();
        private readonly List<DamageNumber> _numbers = new();
        private readonly Stack<Text> _freeNumbers = new();

        private SpritePool _pool;
        private SpriteRenderer _exit;
        private RectTransform _numberLayer;
        private Camera _camera;

        private static GameArt Art => GameArt.Instance;

        private void Awake()
        {
            _pool = new SpritePool(transform);
            _numberLayer = (RectTransform)UIKit.CreateCanvas("DamageNumbers", 10, transform).transform;
            _exit = _pool.Get(Art.Ring, new Color(0.5f, 0.9f, 1f, 0.8f), 2, Vector2.zero, RunSimulation.ExitRadius * 2f);
            _exit.gameObject.SetActive(false);
        }

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.AxeSpawn:
                    AddProjectile(e.Id, e.Pos, e.Vec, e.Value, Art.Triangle, new Color(0.85f, 0.85f, 0.9f), 0.55f, spin: 900f);
                    break;
                case SimEventType.ShotSpawn:
                    AddProjectile(e.Id, e.Pos, e.Vec, e.Value, Art.Circle, new Color(1f, 0.3f, 0.3f), 0.3f, spin: 0f);
                    break;
                case SimEventType.AxeEnd:
                case SimEventType.ShotEnd:
                    RemoveProjectile(e.Id);
                    break;
                case SimEventType.Slash:
                {
                    var r = _pool.Get(Art.Arc, Color.white, 8, e.Pos, e.Value * 2f);
                    float angle = Mathf.Atan2(e.Vec.y, e.Vec.x) * Mathf.Rad2Deg - 90f;
                    r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    _faders.Add(new Fader { Renderer = r, Duration = 0.18f, StartScale = e.Value * 1.7f, EndScale = e.Value * 2.1f, Color = new Color(1f, 1f, 1f, 0.55f) });
                    break;
                }
                case SimEventType.EnemyDied:
                {
                    var type = (EnemyType)e.Int;
                    float size = EnemyCatalog.Get(type).Radius * 2f;
                    var r = _pool.Get(Art.Circle, Color.white, 4, e.Pos, size);
                    _faders.Add(new Fader { Renderer = r, Duration = 0.25f, StartScale = size, EndScale = size * 1.8f, Color = GameArt.EnemyColor(type, false) });
                    break;
                }
                case SimEventType.EnemyHit:
                    ShowDamage(e.Pos, e.Value);
                    break;
                case SimEventType.GemSpawn:
                {
                    float scale = e.Int >= 10 ? 0.5f : e.Int >= 3 ? 0.4f : 0.3f;
                    var color = e.Int >= 10 ? new Color(1f, 0.4f, 0.9f) : e.Int >= 3 ? new Color(0.4f, 1f, 0.5f) : new Color(0.4f, 0.8f, 1f);
                    _gems[e.Id] = _pool.Get(Art.Diamond, color, 3, e.Pos, scale);
                    break;
                }
                case SimEventType.GemCollected:
                    if (_gems.Remove(e.Id, out var gem))
                    {
                        var target = FindPlayer(e.ClientId);
                        if (target != null) _flyingGems.Add(new FlyingGem { Renderer = gem, Target = target.transform, Start = gem.transform.position });
                        else _pool.Release(gem);
                    }
                    break;
                case SimEventType.PlayerHurt:
                    FindPlayer(e.ClientId)?.FlashHurt();
                    break;
                case SimEventType.Teleport:
                    if (NetPlayer.Local != null && NetPlayer.Local.OwnerClientId == e.ClientId)
                        NetPlayer.Local.Mover.Teleport(e.Pos);
                    break;
            }
        }

        private static NetPlayer FindPlayer(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        private void AddProjectile(int id, Vector2 pos, Vector2 velocity, float life, Sprite sprite, Color color, float scale, float spin)
        {
            RemoveProjectile(id);
            var r = _pool.Get(sprite, color, 7, pos, scale);
            _projectiles[id] = new Mover { Renderer = r, Velocity = velocity, Life = life, Spin = spin };
        }

        private void RemoveProjectile(int id)
        {
            if (_projectiles.Remove(id, out var m)) _pool.Release(m.Renderer);
        }

        private void ShowDamage(Vector2 world, float amount)
        {
            if (_numbers.Count >= MaxDamageNumbers) return;
            var text = _freeNumbers.Count > 0 ? _freeNumbers.Pop() : UIKit.Label(_numberLayer, "", 26, TextAnchor.MiddleCenter);
            text.gameObject.SetActive(true);
            text.rectTransform.sizeDelta = new Vector2(120f, 40f);
            text.text = Mathf.RoundToInt(amount).ToString();
            text.color = amount >= 25f ? UIKit.AccentColor : Color.white;
            _numbers.Add(new DamageNumber { Text = text, World = world + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f) });
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_camera == null) _camera = Camera.main;

            _expired.Clear();
            foreach (var (id, m) in _projectiles)
            {
                m.Life -= dt;
                var t = m.Renderer.transform;
                t.position += (Vector3)(m.Velocity * dt);
                if (m.Spin != 0f) t.Rotate(0f, 0f, m.Spin * dt);
                if (m.Life <= 0f) _expired.Add(id);
            }
            foreach (int id in _expired) RemoveProjectile(id);

            for (int i = _faders.Count - 1; i >= 0; i--)
            {
                var f = _faders[i];
                f.Age += dt;
                float k = f.Age / f.Duration;
                if (k >= 1f)
                {
                    _pool.Release(f.Renderer);
                    _faders.RemoveAt(i);
                    continue;
                }
                f.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(f.StartScale, f.EndScale, k);
                var c = f.Color;
                c.a *= 1f - k;
                f.Renderer.color = c;
            }

            for (int i = _flyingGems.Count - 1; i >= 0; i--)
            {
                var g = _flyingGems[i];
                g.T += dt / 0.15f;
                if (g.T >= 1f || g.Target == null)
                {
                    _pool.Release(g.Renderer);
                    _flyingGems.RemoveAt(i);
                    continue;
                }
                g.Renderer.transform.position = Vector3.Lerp(g.Start, g.Target.position, g.T);
            }

            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                var n = _numbers[i];
                n.Age += dt;
                if (n.Age >= 0.6f || _camera == null)
                {
                    n.Text.gameObject.SetActive(false);
                    _freeNumbers.Push(n.Text);
                    _numbers.RemoveAt(i);
                    continue;
                }
                Vector3 screen = _camera.WorldToScreenPoint(n.World + Vector3.up * n.Age * 1.2f);
                n.Text.transform.position = screen;
                var c = n.Text.color;
                c.a = 1f - n.Age / 0.6f;
                n.Text.color = c;
            }

            UpdateExit();
        }

        private void UpdateExit()
        {
            var director = RunDirector.Instance;
            bool open = director != null && director.RoomCleared.Value && director.Phase.Value == RunPhase.Playing;
            UIKit.Toggle(_exit.gameObject, open);
            if (!open) return;
            _exit.transform.position = director.ExitPosition.Value;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 5f);
            _exit.transform.localScale = Vector3.one * RunSimulation.ExitRadius * 2f * pulse;
            _exit.transform.Rotate(0f, 0f, 60f * Time.deltaTime);
        }
    }
}
