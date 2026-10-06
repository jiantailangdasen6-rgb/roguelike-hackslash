using System.Collections.Generic;

namespace Game.Core
{
    public enum UpgradeKind : byte
    {
        NewWeapon,
        WeaponLevel,
        Passive,
        /// <summary>Fallback when everything is maxed.</summary>
        Heal,
    }

    public readonly struct UpgradeOption
    {
        public readonly UpgradeKind Kind;
        public readonly byte Id;
        /// <summary>Level after taking the upgrade.</summary>
        public readonly byte NewLevel;

        public UpgradeOption(UpgradeKind kind, byte id, byte newLevel)
        {
            Kind = kind;
            Id = id;
            NewLevel = newLevel;
        }

        private int Packed => ((int)Kind << 16) | (Id << 8) | NewLevel;

        private static UpgradeOption Unpack(int packed) =>
            new((UpgradeKind)((packed >> 16) & 0xFF), (byte)((packed >> 8) & 0xFF), (byte)(packed & 0xFF));

        /// <summary>Packs up to 3 options (+count) into one ulong for compact network events.</summary>
        public static ulong Pack(IReadOnlyList<UpgradeOption> options)
        {
            ulong packed = (ulong)options.Count;
            for (int i = 0; i < options.Count && i < 3; i++)
                packed |= (ulong)(uint)options[i].Packed << (2 + i * 20);
            return packed;
        }

        public static UpgradeOption[] UnpackAll(ulong packed)
        {
            int count = (int)(packed & 0x3);
            var result = new UpgradeOption[count];
            for (int i = 0; i < count; i++)
                result[i] = Unpack((int)((packed >> (2 + i * 20)) & 0xFFFFF));
            return result;
        }
    }

    public static class UpgradeRoller
    {
        public const int OptionCount = 3;

        public static UpgradeOption[] Roll(GameRandom rng, SimPlayer player)
        {
            var candidates = new List<(UpgradeOption Option, int Weight)>();

            foreach (var weapon in player.Weapons)
            {
                var def = WeaponCatalog.Get(weapon.Id);
                if (weapon.Level < def.MaxLevel)
                    candidates.Add((new UpgradeOption(UpgradeKind.WeaponLevel, (byte)weapon.Id, (byte)(weapon.Level + 1)), 12));
            }

            if (player.Weapons.Count < WeaponCatalog.MaxWeaponSlots)
            {
                foreach (var def in WeaponCatalog.All)
                    if (player.GetWeapon(def.Id) == null)
                        candidates.Add((new UpgradeOption(UpgradeKind.NewWeapon, (byte)def.Id, 1), 10));
            }

            int ownedPassives = 0;
            foreach (var level in player.PassiveLevels) if (level > 0) ownedPassives++;
            foreach (var def in PassiveCatalog.All)
            {
                int level = player.PassiveLevels[(int)def.Id];
                bool canTake = level > 0 ? level < PassiveCatalog.MaxLevel : ownedPassives < PassiveCatalog.MaxPassiveSlots;
                if (canTake)
                    candidates.Add((new UpgradeOption(UpgradeKind.Passive, (byte)def.Id, (byte)(level + 1)), 7));
            }

            var result = new List<UpgradeOption>(OptionCount);
            while (result.Count < OptionCount && candidates.Count > 0)
            {
                int total = 0;
                foreach (var c in candidates) total += c.Weight;
                int pick = rng.Range(0, total);
                int index = 0;
                while (pick >= candidates[index].Weight)
                {
                    pick -= candidates[index].Weight;
                    index++;
                }
                result.Add(candidates[index].Option);
                candidates.RemoveAt(index);
            }

            if (result.Count == 0) result.Add(new UpgradeOption(UpgradeKind.Heal, 0, 0));
            return result.ToArray();
        }

        public static void Apply(SimPlayer player, UpgradeOption option)
        {
            switch (option.Kind)
            {
                case UpgradeKind.NewWeapon:
                    player.AddWeapon((WeaponId)option.Id);
                    break;
                case UpgradeKind.WeaponLevel:
                    var weapon = player.GetWeapon((WeaponId)option.Id);
                    if (weapon != null) weapon.Level = option.NewLevel;
                    break;
                case UpgradeKind.Passive:
                    player.SetPassiveLevel((PassiveId)option.Id, option.NewLevel);
                    break;
                case UpgradeKind.Heal:
                    player.Heal(player.MaxHp * 0.3f);
                    break;
            }
        }

        public static (string Title, string Body) Describe(UpgradeOption option)
        {
            switch (option.Kind)
            {
                case UpgradeKind.NewWeapon:
                {
                    var def = WeaponCatalog.Get((WeaponId)option.Id);
                    return ($"{def.Name}（新規）", def.Description);
                }
                case UpgradeKind.WeaponLevel:
                {
                    var def = WeaponCatalog.Get((WeaponId)option.Id);
                    return ($"{def.Name} Lv{option.NewLevel}", def.At(option.NewLevel).UpgradeText);
                }
                case UpgradeKind.Passive:
                {
                    var def = PassiveCatalog.Get((PassiveId)option.Id);
                    string title = option.NewLevel == 1 ? $"{def.Name}（新規）" : $"{def.Name} Lv{option.NewLevel}";
                    return (title, ItemText.Describe(def.PerLevel));
                }
                default:
                    return ("回復", "HPを30%回復する");
            }
        }
    }
}
