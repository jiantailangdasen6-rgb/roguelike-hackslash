using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Co-op pings. F / middle click / d-pad up pings what is under the cursor: a monster ("danger"), a pedestal,
    /// door or chest ("look here"), anywhere else ("come"). With the big map open, clicking a room calls everyone
    /// there ("gather"). A ping in your room shows as a glowing mark (an arrow at the screen edge when off screen);
    /// one elsewhere as a toast and a pulse on the map. Every ping chimes.
    /// </summary>
    public sealed class PingView : MonoBehaviour
    {
        private const float Cooldown = 0.45f;
        private const float PickRadius = 1.9f;

        private sealed class Marker
        {
            public Pings.Mark Mark;
            public GameObject Root;
            public Transform Icon;
            public SpriteRenderer IconRenderer;
            public SpriteRenderer[] Rings;
            public Light2D Light;
            public Text Label;
            public Image Edge;
            public Image EdgeIcon;
        }

        private readonly List<Marker> _markers = new();
        private RectTransform _layer;
        private Camera _camera;
        private Material _unlit;
        private float _nextPing;
        private readonly List<float> _testPings = new();
        private readonly List<float> _testRoomPings = new();

        private void Awake()
        {
            Pings.Active.Clear();
            _layer = (RectTransform)UIKit.CreateCanvas("Pings", 12, transform).transform;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null) _unlit = new Material(shader);
            // Screenshots/tests (seconds after the run scene loads): -pingat <s1,s2..> pings beside the player,
            // -pingroomat <s1,..> calls everyone to the campfire room.
            ReadTimes("-pingat", _testPings);
            ReadTimes("-pingroomat", _testRoomPings);
        }

        private static void ReadTimes(string flag, List<float> into)
        {
            string times = CommandLine.GetValue(flag);
            if (times == null) return;
            foreach (var t in times.Split(','))
                if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s))
                    into.Add(Time.time + s);
            into.Sort();
        }

        private void OnEnable() => Pings.Added += Show;
        private void OnDisable() => Pings.Added -= Show;

        private void OnDestroy()
        {
            foreach (var m in _markers) Destroy(m.Root);
            _markers.Clear();
            Pings.Active.Clear();
        }

        // ------------------------------------------------------------------ Sending

        private static bool CanPing(RunDirector d, NetPlayer local) =>
            d != null && d.IsRunning && local != null && !PlayArea.InCamp && !PauseMenuView.IsOpen && !PerkView.IsShowing && !BagView.IsOpen && !CodexView.IsOpen && !SettingsView.IsOpen;

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            var director = RunDirector.Instance;
            var local = NetPlayer.Local;
            if (CanPing(director, local)) ReadInput(director, local);
            UpdateMarkers(director);
        }

        private void ReadInput(RunDirector director, NetPlayer local)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            bool key = GameSettings.Pressed(KeyAction.Ping) || mouse != null && mouse.middleButton.wasPressedThisFrame
                       || pad != null && pad.dpad.up.wasPressedThisFrame;
            var hud = HudView.Instance;
            bool onMap = hud != null && hud.BigMapOpen;

            if (_testRoomPings.Count > 0 && Time.time >= _testRoomPings[0])
            {
                _testRoomPings.RemoveAt(0);
                _nextPing = 0f;
                Send(local, director.Map.Campfire.Bounds.center, PingKind.Gather);
            }
            if (_testPings.Count > 0 && Time.time >= _testPings[0])
            {
                _testPings.RemoveAt(0);
                _nextPing = 0f;
                // In turn: beside the player, the nearest monster, far off screen, the nearest doorway.
                Vector2 me = local.transform.position;
                Vector2 at = (_testPingIndex++ % 4) switch
                {
                    1 => EnemyView.Instance != null && EnemyView.Instance.Nearest(me) is Vector2 monster ? monster : me + new Vector2(-3f, 0f),
                    2 => me + new Vector2(me.x > director.LocalRoom.Bounds.center.x ? -19f : 19f, 0f),
                    3 => NearestDoor(director.LocalRoom, me),
                    _ => me + new Vector2(2f, 1f),
                };
                var kind = Classify(director, ref at);
                Send(local, at, kind);
            }

            if (onMap)
            {
                // The big map: a click (or F) on a room calls everyone there.
                bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
                if ((click || key) && mouse != null && hud.BigMapRoomAt(mouse.position.ReadValue()) is MapRoom room)
                    Send(local, room.Bounds.center, PingKind.Gather);
                return;
            }
            if (!key) return;

            Vector2 spot;
            var reader = local.GetComponent<PlayerInputReader>();
            if (mouse != null && (reader == null || reader.UsingMouse) && _camera != null)
                spot = _camera.ScreenToWorldPoint(mouse.position.ReadValue());
            else
            {
                Vector2 stick = reader != null ? reader.AimStick : Vector2.zero;
                spot = (Vector2)local.transform.position + (stick.sqrMagnitude > 0.1f ? stick.normalized * 4f : Vector2.zero);
            }
            var what = Classify(director, ref spot);
            Send(local, spot, what);
        }

        private int _testPingIndex;

        private static Vector2 NearestDoor(MapRoom room, Vector2 from)
        {
            Vector2 best = room.Bounds.center;
            float bestSq = float.MaxValue;
            for (int d = 0; d < 4; d++)
            {
                if (!room.HasDoor(d)) continue;
                Vector2 at = DungeonMap.EntryPoint(room, d);
                if ((at - from).sqrMagnitude < bestSq) { bestSq = (at - from).sqrMagnitude; best = at; }
            }
            return best;
        }

        private void Send(NetPlayer local, Vector2 at, PingKind kind)
        {
            if (Time.time < _nextPing) return;
            _nextPing = Time.time + Cooldown;
            local.PingRpc(at, (byte)kind);
        }

        /// <summary>What the spot is about; snaps it onto the thing pointed at.</summary>
        private static PingKind Classify(RunDirector director, ref Vector2 at)
        {
            var view = EnemyView.Instance;
            if (view != null)
            {
                if (view.NearestWhere(at, PickRadius, d => !d.Prop && !d.Untargetable) is Vector2 monster)
                {
                    at = monster;
                    return PingKind.Danger;
                }
                if (view.NearestWhere(at, PickRadius, d => d.Prop) is Vector2 prop)
                {
                    at = prop;
                    return PingKind.Look;
                }
            }
            var room = director.Map.RoomAt(at);
            if (room == null) return PingKind.Come;
            if (RoomCatalog.HasPedestal(room.Kind))
            {
                Vector2 pedestal = RunSimulation.PedestalPosition(room.Bounds);
                if ((pedestal - at).sqrMagnitude < 2.4f * 2.4f)
                {
                    at = pedestal;
                    return PingKind.Look;
                }
            }
            for (int d = 0; d < 4; d++)
            {
                if (!room.HasDoor(d)) continue;
                Vector2 inside = DungeonMap.EntryPoint(room, d);
                if ((inside - at).sqrMagnitude < 2.4f * 2.4f)
                {
                    at = inside;
                    return PingKind.Look;
                }
            }
            return PingKind.Come;
        }

        // ------------------------------------------------------------------ Showing

        private void Show(Pings.Mark mark)
        {
            var director = RunDirector.Instance;
            if (director == null || mark.From == null) return;
            float pitch = mark.Kind switch { PingKind.Danger => 0.72f, PingKind.Look => 1.25f, PingKind.Gather => 0.9f, _ => 1f };
            SfxPlayer.Instance?.Play("ping", null, pitch);

            var room = director.Map.RoomAt(mark.Position);
            if (mark.From != NetPlayer.Local && HudView.Instance != null)
            {
                string where = room == null || room == director.LocalRoom ? "" : $"　<color=#bfb6aa>─ {(director.IsVisited(room.Id) ? RoomCatalog.Name(room.Kind) : "未踏の部屋")}</color>";
                string hex = ColorUtility.ToHtmlStringRGB(Pings.ColorOf(mark));
                HudView.Instance.Toast($"<color=#{ColorUtility.ToHtmlStringRGB(mark.From.Color)}>{mark.From.DisplayName}（{mark.From.ClassDef.Name}）</color> <color=#{hex}>{Pings.Words(mark.Kind)}</color>{where}");
            }

            // One mark per player and kind (a new ping replaces the old one).
            for (int i = _markers.Count - 1; i >= 0; i--)
                if (_markers[i].Mark.From == mark.From && _markers[i].Mark.Kind == mark.Kind) Remove(i);
            _markers.Add(CreateMarker(mark));
        }

        private Marker CreateMarker(Pings.Mark mark)
        {
            var color = Pings.ColorOf(mark);
            var root = new GameObject("Ping");
            root.transform.SetParent(transform, false);
            root.transform.position = mark.Position;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(root.transform, false);
            var icon = iconGo.AddComponent<SpriteRenderer>();
            var sprite = SpriteSheet.Load(Pings.Sheet(mark.Kind))?.First;
            icon.sprite = sprite;
            icon.color = color;
            icon.sortingOrder = 2950;
            if (_unlit != null) icon.sharedMaterial = _unlit;

            var ringSprite = SpriteSheet.Load("fx_circle")?.First;
            var rings = new SpriteRenderer[2];
            for (int i = 0; i < rings.Length; i++)
            {
                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(root.transform, false);
                rings[i] = ringGo.AddComponent<SpriteRenderer>();
                rings[i].sprite = ringSprite;
                rings[i].sortingOrder = -340;
                if (_unlit != null) rings[i].sharedMaterial = _unlit;
            }

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightInnerRadius = 0.3f;
            light.pointLightOuterRadius = 3.4f;

            var label = UIKit.Label(_layer, $"{mark.From.DisplayName} {Pings.Words(mark.Kind)}", 28, TextAnchor.LowerCenter);
            label.font = UIKit.TitleFont;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(360f, 44f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var edge = UIKit.Panel(_layer, "PingEdge", color);
            edge.sprite = UIKit.ArrowSprite;
            edge.rectTransform.sizeDelta = new Vector2(46f, 46f);
            edge.raycastTarget = false;
            var edgeIcon = UIKit.Panel(_layer, "PingEdgeIcon", color);
            edgeIcon.sprite = sprite;
            edgeIcon.preserveAspect = true;
            edgeIcon.rectTransform.sizeDelta = new Vector2(54f, 54f);
            edgeIcon.raycastTarget = false;

            return new Marker
            {
                Mark = mark, Root = root, Icon = iconGo.transform, IconRenderer = icon, Rings = rings, Light = light,
                Label = label, Edge = edge, EdgeIcon = edgeIcon,
            };
        }

        private void Remove(int index)
        {
            var m = _markers[index];
            Destroy(m.Root);
            Destroy(m.Label.gameObject);
            Destroy(m.Edge.gameObject);
            Destroy(m.EdgeIcon.gameObject);
            _markers.RemoveAt(index);
        }

        private void UpdateMarkers(RunDirector director)
        {
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                float age = m.Mark.Age;
                if (age > Pings.Life || m.Mark.From == null || director == null)
                {
                    Remove(i);
                    continue;
                }
                var color = Pings.ColorOf(m.Mark);
                float fade = Mathf.Clamp01(Pings.Life - age); // the last second fades
                bool here = director.Map.RoomAt(m.Mark.Position) == director.LocalRoom;
                m.Root.SetActive(here);

                // Pops in big, then bobs; the rings ripple out on the floor.
                float pop = age < 0.25f ? Mathf.Lerp(1.9f, 1f, age / 0.25f) : 1f;
                m.Icon.localScale = Vector3.one * (1.3f * pop);
                // A monster or a chest stays visible: the mark floats over it. A pin stands on the spot.
                float lift = m.Mark.Kind == PingKind.Danger ? 1.25f : m.Mark.Kind == PingKind.Look ? 0.8f : 0.15f;
                m.Icon.localPosition = new Vector3(0f, lift + Mathf.Abs(Mathf.Sin(age * 4.5f)) * 0.22f, 0f);
                m.IconRenderer.color = new Color(color.r, color.g, color.b, fade);
                for (int r = 0; r < m.Rings.Length; r++)
                {
                    float phase = Mathf.Repeat(age * 1.1f + r * 0.5f, 1f);
                    m.Rings[r].transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.25f, phase);
                    m.Rings[r].color = new Color(color.r, color.g, color.b, (1f - phase) * 0.45f * fade);
                }
                m.Light.intensity = (1.1f + 0.4f * Mathf.Sin(age * 6f)) * fade;

                bool onScreen = false;
                Vector3 screen = Vector3.zero;
                if (here && _camera != null)
                {
                    screen = _camera.WorldToScreenPoint(m.Mark.Position + Vector2.up * (m.Mark.Kind == PingKind.Danger ? 2.5f : m.Mark.Kind == PingKind.Look ? 2f : 1.35f));
                    onScreen = screen.z > 0f && screen.x > 0f && screen.x < Screen.width && screen.y > 0f && screen.y < Screen.height;
                }
                m.Label.enabled = here && onScreen;
                m.Label.color = new Color(color.r, color.g, color.b, fade);
                if (m.Label.enabled) m.Label.transform.position = screen;

                // Off screen in this room: an arrow at the edge, pointing at it.
                bool edge = here && !onScreen && _camera != null;
                m.Edge.enabled = m.EdgeIcon.enabled = edge;
                if (edge)
                {
                    Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
                    Vector2 dir = (Vector2)screen - center;
                    if (screen.z < 0f) dir = -dir;
                    float margin = Mathf.Min(Screen.width, Screen.height) * 0.07f;
                    float kx = (Screen.width * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
                    float ky = (Screen.height * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                    Vector2 at = center + dir * Mathf.Min(kx, ky);
                    m.Edge.transform.position = at;
                    m.Edge.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    m.EdgeIcon.transform.position = at - dir.normalized * margin * 0.75f;
                    float blink = 0.6f + 0.4f * Mathf.Sin(age * 8f);
                    m.Edge.color = new Color(color.r, color.g, color.b, fade * blink);
                    m.EdgeIcon.color = new Color(color.r, color.g, color.b, fade);
                }
            }
        }
    }
}
