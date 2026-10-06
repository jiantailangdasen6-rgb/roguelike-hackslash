using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public sealed class WeaponState
    {
        public readonly WeaponId Id;
        public int Level = 1;
        public float Timer;

        public WeaponState(WeaponId id) => Id = id;

        public WeaponLevel Stats => WeaponCatalog.Get(Id).At(Level);
    }

    /// <summary>Server-side state of one player in the run. Position is fed in from the network each tick.</summary>
    public sealed class SimPlayer
    {
        public const float BodyRadius = 0.4f;

        private static readonly object EquipmentSource = new();
        private readonly object[] _passiveSources;

        public readonly ulong ClientId;
        public readonly StatSheet Stats = new();
        public readonly List<WeaponState> Weapons = new();
        public readonly int[] PassiveLevels = new int[PassiveCatalog.All.Length];

        public Vector2 Position;
        public Vector2 Facing = Vector2.down;
        public float Hp;
        public bool Downed;
        public float ReviveProgress;
        public float InvulnerableTimer;

        /// <summary>Level-up options waiting for this player's choice (null when none).</summary>
        public UpgradeOption[] PendingOffer;

        public float MaxHp => Stats.Get(StatType.MaxHealth);
        public bool Alive => !Downed;

        public SimPlayer(ulong clientId, Vector2 position)
        {
            ClientId = clientId;
            Position = position;

            // Warrior base stats (the only class for now).
            Stats.SetBase(StatType.MaxHealth, 120f);
            Stats.SetBase(StatType.MoveSpeed, 4.5f);
            Stats.SetBase(StatType.Might, 1f);
            Stats.SetBase(StatType.Cooldown, 1f);
            Stats.SetBase(StatType.Area, 1f);
            Stats.SetBase(StatType.PickupRange, 1.8f);
            Stats.SetBase(StatType.Armor, 1f);
            Stats.SetBase(StatType.Regen, 0f);
            Stats.SetBase(StatType.Amount, 0f);
            Hp = MaxHp;

            _passiveSources = new object[PassiveLevels.Length];
            for (int i = 0; i < _passiveSources.Length; i++) _passiveSources[i] = new object();

            AddWeapon(WeaponId.Greatsword);
        }

        public float CooldownMultiplier => Mathf.Max(0.35f, Stats.Get(StatType.Cooldown));

        public WeaponState GetWeapon(WeaponId id)
        {
            foreach (var w in Weapons) if (w.Id == id) return w;
            return null;
        }

        public void AddWeapon(WeaponId id)
        {
            if (GetWeapon(id) == null && Weapons.Count < WeaponCatalog.MaxWeaponSlots)
                Weapons.Add(new WeaponState(id));
        }

        public void SetPassiveLevel(PassiveId id, int level)
        {
            float hpBefore = MaxHp;
            int i = (int)id;
            PassiveLevels[i] = level;
            Stats.RemoveModifiersFrom(_passiveSources[i]);
            var perLevel = PassiveCatalog.Get(id).PerLevel;
            Stats.AddModifier(new StatModifier(perLevel.Stat, perLevel.Kind, perLevel.Value * level), _passiveSources[i]);
            KeepHealthRatio(hpBefore);
        }

        /// <summary>Replaces all equipment modifiers (sent by the owning client).</summary>
        public void SetEquipment(IEnumerable<StatModifier> modifiers)
        {
            float hpBefore = MaxHp;
            Stats.RemoveModifiersFrom(EquipmentSource);
            foreach (var mod in modifiers) Stats.AddModifier(mod, EquipmentSource);
            KeepHealthRatio(hpBefore);
        }

        public void Heal(float amount)
        {
            if (!Downed) Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        private void KeepHealthRatio(float maxBefore)
        {
            if (maxBefore > 0f) Hp = Mathf.Min(MaxHp, Hp * MaxHp / maxBefore);
        }
    }
}
