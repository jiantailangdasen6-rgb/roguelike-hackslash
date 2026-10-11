using System.Collections.Generic;
using Game.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// Gear lying on the floor - only your own (loot is personal). Each drop bobs above a soft light in its rarity
    /// colour; rare and legendary pieces also send up a pillar of light so they are easy to spot mid-fight.
    /// </summary>
    public sealed class LootView : MonoBehaviour
    {
        private sealed class Drop
        {
            public GameObject Root;
            public Transform Icon;
            public SpriteRenderer Beam;
            public Light2D Light;
            public float Born;
            public Rarity Rarity;
        }

        private readonly Dictionary<int, Drop> _drops = new();

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        private static bool IsMine(ulong clientId) => NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == clientId;

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.LootDropped when IsMine(e.ClientId):
                    Spawn(e.Id, e.Pos, ItemCodec.Unpack(e.Seed));
                    break;
                case SimEventType.LootPicked when _drops.TryGetValue(e.Id, out var d):
                    Destroy(d.Root);
                    _drops.Remove(e.Id);
                    break;
                case SimEventType.RoomStarted:
                    foreach (var drop in _drops.Values) Destroy(drop.Root);
                    _drops.Clear();
                    break;
            }
        }

        private void Spawn(int id, Vector2 at, ItemInstance item)
        {
            if (item == null || _drops.ContainsKey(id)) return;
            var color = GameIcons.RarityColor(item.rarity);
            var root = new GameObject($"Loot{id}");
            root.transform.SetParent(transform, false);
            root.transform.position = at;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(root.transform, false);
            var icon = iconGo.AddComponent<SpriteRenderer>();
            icon.sprite = GameIcons.Item(item);
            icon.sortingOrder = EnemyView.SortOrder(at.y) + 2;

            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.28f);
            shadow.sprite = SpriteSheet.Load("fx_shadow")?.First;
            shadow.sortingOrder = -900;
            shadow.color = new Color(0f, 0f, 0f, 0.5f);

            SpriteRenderer beam = null;
            if (item.rarity >= Rarity.Rare)
            {
                beam = new GameObject("Beam").AddComponent<SpriteRenderer>();
                beam.transform.SetParent(root.transform, false);
                beam.transform.localPosition = new Vector3(0f, 1.2f);
                beam.transform.localScale = new Vector3(0.18f, 2.6f, 1f);
                beam.sprite = SpriteSheet.Load("fx_circle")?.First;
                beam.sortingOrder = EnemyView.SortOrder(at.y) + 1;
                beam.color = new Color(color.r, color.g, color.b, 0.35f);
            }

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightOuterRadius = item.rarity >= Rarity.Rare ? 2.2f : 1.4f;
            light.pointLightInnerRadius = 0.1f;
            light.intensity = item.rarity >= Rarity.Rare ? 1.1f : 0.6f;

            _drops[id] = new Drop { Root = root, Icon = iconGo.transform, Beam = beam, Light = light, Born = Time.time, Rarity = item.rarity };
        }

        private void Update()
        {
            foreach (var d in _drops.Values)
            {
                float age = Time.time - d.Born;
                // Pops up out of the corpse, then bobs.
                float pop = age < 0.35f ? Mathf.Sin(age / 0.35f * Mathf.PI) * 0.5f : 0f;
                d.Icon.localPosition = new Vector3(0f, 0.1f + pop + 0.06f * Mathf.Sin(Time.time * 3f + d.Born));
                float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 5f + d.Born * 3f);
                d.Light.intensity = (d.Rarity >= Rarity.Rare ? 1.1f : 0.6f) * flicker;
                if (d.Beam != null)
                {
                    var c = d.Beam.color;
                    c.a = 0.22f + 0.12f * flicker;
                    d.Beam.color = c;
                }
            }
        }
    }
}
