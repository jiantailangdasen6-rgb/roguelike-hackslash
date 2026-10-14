using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>An item lying on the floor. Personal: only its owner sees it and can pick it up.</summary>
    public sealed class LootDrop
    {
        public int Id;
        public ulong Owner;
        public ItemInstance Item;
        public Vector2 Position;
    }

    /// <summary>
    /// Gear (personal loot). Monsters drop items for each player separately; walking over your own drop puts it in
    /// your bag. Equipping, giving to a teammate and salvaging for soul shards happen between fights only, like the
    /// perk picks. Clients learn their bag through <see cref="SimEventType.GearSlot"/> events (one per slot).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float NormalDropChance = 0.03f;
        public const float EliteDropChance = 0.35f;
        public const float LootPickupRadius = 1.1f;

        public readonly List<LootDrop> Loot = new();
        /// <summary>Multiplies drop chances (automated screenshots: -lootrate).</summary>
        public float DropChanceScale = 1f;
        private readonly GameRandom _lootRng;
        private int _nextLootId = 1;

        /// <summary>Slot numbers in GearSlot events: equipped 0..3, then the bag.</summary>
        public const int BagSlotOffset = SimPlayer.SlotCount;

        /// <summary>A legendary effect fired (clients flash, play a sting, and draw chains from <paramref name="at"/> to <paramref name="to"/>).</summary>
        private void Proc(SimPlayer p, LegendaryId id, Vector2 at, Vector2 to = default) =>
            Events.Add(new SimEvent(SimEventType.LegendaryProc) { ClientId = p.ClientId, Int = (int)id, Pos = at, Vec = to });

        /// <summary>Gear brought from home works at most this many levels above the current room.</summary>
        public const int GearLevelGrace = 2;

        /// <summary>
        /// Empty slots get a plain piece on loan for this run (common or fine, at the room's level), so a friend with
        /// an empty stash isn't left far behind. Loaners are handed back when replaced and never go home.
        /// </summary>
        private void GiveLoaners(SimPlayer p)
        {
            for (int s = 0; s < SimPlayer.SlotCount; s++)
            {
                if (p.Equipped[s] != null || s >= (int)EquipSlot.Rune1) continue; // runes are never lent
                // The camp's forge lends better pieces (fine, then rare).
                var least = p.ForgeLevel >= 2 ? Rarity.Rare : p.ForgeLevel >= 1 ? Rarity.Magic : Rarity.Common;
                var most = p.ForgeLevel >= 2 ? Rarity.Rare : Rarity.Magic;
                var item = ItemGenerator.Generate(_lootRng.NextULong() | 1ul, Room.Index + 1, 0f, p.ClassId, least, (EquipSlot)s);
                if (item.rarity > most)
                {
                    item.rarity = most;
                    item.legendary = LegendaryId.None;
                    int keep = most == Rarity.Rare ? 3 : 1;
                    if (item.affixes.Count > keep) item.affixes.RemoveRange(keep, item.affixes.Count - keep);
                }
                item.loaner = true;
                p.Wear(item);
            }
        }

        /// <summary>Better finds deeper in the labyrinth and on harder difficulties.</summary>
        private float LootBoost => 0.35f * Area + DifficultyDef.LootBoost;

        private ItemInstance RollItem(SimPlayer owner, float boost, Rarity minRarity = Rarity.Common) =>
            ItemGenerator.Generate(_lootRng.NextULong() | 1ul, Room.Index + 1, boost + LootBoost, owner.ClassId, minRarity);

        /// <summary>A monster died: each player rolls their own drop.</summary>
        private void RollLoot(in Enemy e)
        {
            float chance = Mathf.Min(1f, (e.Def.Boss ? 1f : e.Elite ? EliteDropChance : NormalDropChance) * DropChanceScale * OmenLoot);
            float boost = e.Def.Boss ? 2.5f : e.Elite ? 1f : 0f;
            for (int i = 0; i < Players.Count; i++)
            {
                if (!_lootRng.Chance(chance)) continue;
                var p = Players[i];
                var offset = new Vector2(_lootRng.Range(-0.6f, 0.6f), _lootRng.Range(-0.4f, 0.4f));
                DropFor(p, RollItem(p, boost), e.Position + offset);
            }
        }

        private void DropFor(SimPlayer owner, ItemInstance item, Vector2 at)
        {
            var drop = new LootDrop { Id = _nextLootId++, Owner = owner.ClientId, Item = item, Position = ClampToFloor(at) };
            Loot.Add(drop);
            Events.Add(new SimEvent(SimEventType.LootDropped)
            {
                Id = drop.Id, ClientId = owner.ClientId, Pos = drop.Position, Seed = ItemCodec.Pack(item), Int = (int)item.rarity,
            });
        }

        private Vector2 ClampToFloor(Vector2 p)
        {
            var b = Map.RoomAt(p)?.Bounds ?? Bounds;
            return new(Mathf.Clamp(p.x, b.xMin + 0.5f, b.xMax - 0.5f), Mathf.Clamp(p.y, b.yMin + 0.5f, b.yMax - 0.5f));
        }

        /// <summary>Owners pick up their own drops by walking over them (if the bag has room).</summary>
        private void CollectLoot()
        {
            for (int i = Loot.Count - 1; i >= 0; i--)
            {
                var drop = Loot[i];
                var p = GetPlayer(drop.Owner);
                if (p == null || !p.Alive || p.BagFull) continue;
                if ((p.Position - drop.Position).sqrMagnitude > LootPickupRadius * LootPickupRadius) continue;
                PickUp(p, i);
            }
        }

        private void PickUp(SimPlayer p, int lootIndex)
        {
            var drop = Loot[lootIndex];
            Loot.RemoveAt(lootIndex);
            p.Bag.Add(drop.Item);
            Events.Add(new SimEvent(SimEventType.LootPicked) { Id = drop.Id, ClientId = p.ClientId, Int = (int)drop.Item.rarity });
            SyncGear(p);
        }

        /// <summary>Leaving an area: everyone's drops follow them (as long as their bag has room).</summary>
        private void SweepLoot()
        {
            for (int i = Loot.Count - 1; i >= 0; i--)
            {
                var p = GetPlayer(Loot[i].Owner);
                if (p != null && !p.BagFull) PickUp(p, i);
            }
            foreach (var drop in Loot) Events.Add(new SimEvent(SimEventType.LootPicked) { Id = drop.Id, ClientId = drop.Owner, Int = -1 });
            Loot.Clear();
        }

        /// <summary>One rare-or-better piece for one player (the merchant).</summary>
        private void GrantTreasureTo(SimPlayer p, int slot)
        {
            var item = RollItem(p, 2f, Rarity.Rare);
            if (!p.BagFull)
            {
                p.Bag.Add(item);
                Events.Add(new SimEvent(SimEventType.LootPicked) { Id = 0, ClientId = p.ClientId, Int = (int)item.rarity });
            }
            else DropFor(p, item, p.Position + new Vector2(0.6f * (slot + 1), 0f));
            SyncGear(p);
        }

        // ------------------------------------------------------------------ Bag actions (between fights only)

        public bool EquipFromBag(ulong clientId, int bagIndex)
        {
            var p = GetPlayer(clientId);
            if (p == null || !CanChoose(p) || !p.Equip(bagIndex)) return false;
            SyncGear(p);
            return true;
        }

        public bool UnequipSlot(ulong clientId, EquipSlot slot)
        {
            var p = GetPlayer(clientId);
            if (p == null || !CanChoose(p) || !p.Unequip(slot)) return false;
            SyncGear(p);
            return true;
        }

        /// <summary>Hands a bag item to a teammate; a weapon takes the shape of the new owner's class.</summary>
        public bool GiveItem(ulong clientId, int bagIndex, ulong toClientId)
        {
            var from = GetPlayer(clientId);
            var to = GetPlayer(toClientId);
            if (from == null || to == null || from == to || !CanChoose(from) || !CanChoose(to)) return false;
            if (bagIndex < 0 || bagIndex >= from.Bag.Count || to.BagFull) return false;
            var item = from.Bag[bagIndex];
            if (item.loaner) return false; // lent to this player only
            from.Bag.RemoveAt(bagIndex);
            if (item.Slot == EquipSlot.Weapon) item.classId = to.ClassId;
            to.Bag.Add(item);
            Events.Add(new SimEvent(SimEventType.ItemGiven) { ClientId = clientId, Id = (int)toClientId, Int = (int)item.rarity });
            SyncGear(from);
            SyncGear(to);
            return true;
        }

        public static int SalvageValue(Rarity r) => r switch
        {
            Rarity.Common => 1,
            Rarity.Magic => 2,
            Rarity.Rare => 4,
            _ => 8,
        };

        /// <summary>Breaks a bag item down into soul shards (paid out with the run).</summary>
        public bool SalvageItem(ulong clientId, int bagIndex)
        {
            var p = GetPlayer(clientId);
            if (p == null || !CanChoose(p) || bagIndex < 0 || bagIndex >= p.Bag.Count) return false;
            var item = p.Bag[bagIndex];
            p.Bag.RemoveAt(bagIndex);
            int shards = item.loaner ? 0 : SalvageValue(item.rarity);
            BonusShards += shards;
            Events.Add(new SimEvent(SimEventType.ItemSalvaged) { ClientId = clientId, Value = shards });
            SyncGear(p);
            return true;
        }

        /// <summary>Tells the owner what is in every gear slot (on changes, and when they join or come back).</summary>
        public void SyncGear(SimPlayer p)
        {
            for (int s = 0; s < SimPlayer.SlotCount; s++)
                Events.Add(new SimEvent(SimEventType.GearSlot) { ClientId = p.ClientId, Int = s, Seed = p.Equipped[s] != null ? ItemCodec.Pack(p.Equipped[s]) : 0 });
            for (int b = 0; b < SimPlayer.BagSize; b++)
                Events.Add(new SimEvent(SimEventType.GearSlot) { ClientId = p.ClientId, Int = BagSlotOffset + b, Seed = b < p.Bag.Count ? ItemCodec.Pack(p.Bag[b]) : 0 });
        }
    }
}
