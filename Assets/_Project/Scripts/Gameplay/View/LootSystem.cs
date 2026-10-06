using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Item drops for the local player only (each player gets their own drops). The host sends a seed;
    /// the item is rebuilt here, shown as a light beam, and added to this device's stash on pickup.
    /// Anything left on the floor is picked up automatically when the room changes.
    /// </summary>
    public sealed class LootSystem : MonoBehaviour
    {
        private const float PickupRadius = 1.1f;

        private sealed class Drop
        {
            public ItemInstance Item;
            public SpriteRenderer Beam;
            public SpriteRenderer Gem;
        }

        private readonly List<Drop> _drops = new();
        private SpritePool _pool;

        /// <summary>Items picked up during the current run (for the results screen).</summary>
        public static int ItemsFoundThisRun { get; private set; }

        private void Awake()
        {
            _pool = new SpritePool(transform);
            ItemsFoundThisRun = 0;
        }

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        private void OnEvent(SimEvent e)
        {
            var local = NetPlayer.Local;
            switch (e.Type)
            {
                case SimEventType.LootDrop when local != null && e.ClientId == local.OwnerClientId:
                    Spawn(ItemGenerator.Generate(e.Seed, e.Int, e.Value), e.Pos);
                    break;
                case SimEventType.RoomStarted:
                case SimEventType.RunEnded:
                    for (int i = _drops.Count - 1; i >= 0; i--) PickUp(i);
                    break;
            }
        }

        private void Spawn(ItemInstance item, Vector2 position)
        {
            var color = GameArt.RarityColor(item.rarity);
            var beam = _pool.Get(GameArt.Instance.Circle, new Color(color.r, color.g, color.b, 0.35f), 9, position + Vector2.up * 0.9f);
            beam.transform.localScale = new Vector3(0.25f, 2f, 1f);
            var gem = _pool.Get(GameArt.Instance.Diamond, color, 9, position, 0.55f);
            _drops.Add(new Drop { Item = item, Beam = beam, Gem = gem });

            if (item.rarity >= Rarity.Rare) HudView.Instance?.Banner($"{ItemText.RarityName(item.rarity)}アイテムがドロップ！", 1.5f);
        }

        private void Update()
        {
            var local = NetPlayer.Local;
            float bob = Mathf.Sin(Time.time * 4f) * 0.08f;
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                d.Gem.transform.Rotate(0f, 0f, 90f * Time.deltaTime);
                var beamPos = d.Gem.transform.position + Vector3.up * (0.9f + bob);
                d.Beam.transform.position = beamPos;

                if (local != null && !local.Downed.Value &&
                    ((Vector2)(local.transform.position - d.Gem.transform.position)).sqrMagnitude <= PickupRadius * PickupRadius)
                    PickUp(i);
            }
        }

        private void PickUp(int index)
        {
            var d = _drops[index];
            _drops.RemoveAt(index);
            _pool.Release(d.Beam);
            _pool.Release(d.Gem);

            if (StashStore.Current.Add(d.Item)) ItemsFoundThisRun++;
            NetPlayer.Local?.SubmitEquipment();
            HudView.Instance?.Toast($"獲得: {ItemText.ColoredTitle(d.Item)}");
        }
    }
}
