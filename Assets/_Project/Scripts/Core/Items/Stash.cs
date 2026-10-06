using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>All items a player owns (kept between runs) and which ones are equipped.</summary>
    public sealed class Stash
    {
        public const int CurrentVersion = 1;

        [Serializable]
        private sealed class Data
        {
            public int version = CurrentVersion;
            public List<ItemInstance> items = new();
            public List<string> equipped = new();
        }

        private Data _data = new();

        public event Action Changed;

        public IReadOnlyList<ItemInstance> Items => _data.items;

        public bool Contains(string uid) => _data.items.Any(i => i.uid == uid);

        public bool IsEquipped(string uid) => _data.equipped.Contains(uid);

        public ItemInstance GetEquipped(EquipSlot slot) =>
            _data.items.FirstOrDefault(i => i.Slot == slot && IsEquipped(i.uid));

        /// <summary>Adds the item; auto-equips it if that slot is empty. Returns false for duplicates.</summary>
        public bool Add(ItemInstance item)
        {
            if (Contains(item.uid)) return false;
            _data.items.Add(item);
            if (GetEquipped(item.Slot) == null) _data.equipped.Add(item.uid);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Equips the item, replacing whatever was in the same slot.</summary>
        public void Equip(string uid)
        {
            var item = _data.items.FirstOrDefault(i => i.uid == uid);
            if (item == null || IsEquipped(uid)) return;
            var current = GetEquipped(item.Slot);
            if (current != null) _data.equipped.Remove(current.uid);
            _data.equipped.Add(uid);
            Changed?.Invoke();
        }

        public void Unequip(string uid)
        {
            if (_data.equipped.Remove(uid)) Changed?.Invoke();
        }

        public void Discard(string uid)
        {
            _data.equipped.Remove(uid);
            if (_data.items.RemoveAll(i => i.uid == uid) > 0) Changed?.Invoke();
        }

        public List<StatModifier> GetEquippedModifiers()
        {
            var result = new List<StatModifier>();
            foreach (var item in _data.items)
                if (IsEquipped(item.uid)) result.AddRange(item.GetModifiers());
            return result;
        }

        public string ToJson() => JsonUtility.ToJson(_data);

        public static Stash FromJson(string json)
        {
            var stash = new Stash();
            if (string.IsNullOrEmpty(json)) return stash;

            var data = JsonUtility.FromJson<Data>(json);
            if (data == null) return stash;
            data.items ??= new List<ItemInstance>();
            data.equipped ??= new List<string>();
            data.items.RemoveAll(i => i == null || ItemCatalog.GetBase(i.baseId) == null);
            data.equipped.RemoveAll(uid => data.items.All(i => i.uid != uid));
            stash._data = data;
            return stash;
        }
    }
}
