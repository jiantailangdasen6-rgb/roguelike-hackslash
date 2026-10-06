namespace Game.Core
{
    public enum WeaponId : byte
    {
        Greatsword,
        ThrowingAxe,
        HolyAura,
    }

    /// <summary>Stats of a weapon at one level. Unused fields stay 0.</summary>
    public readonly struct WeaponLevel
    {
        public readonly float Damage;
        public readonly float Cooldown;
        /// <summary>Radius for slash/aura; projectile hit radius for axes.</summary>
        public readonly float Area;
        public readonly int Count;
        public readonly int Pierce;
        public readonly string UpgradeText;

        public WeaponLevel(float damage, float cooldown, float area, int count, int pierce, string upgradeText)
        {
            Damage = damage;
            Cooldown = cooldown;
            Area = area;
            Count = count;
            Pierce = pierce;
            UpgradeText = upgradeText;
        }
    }

    public sealed class WeaponDef
    {
        public readonly WeaponId Id;
        public readonly string Name;
        public readonly string Description;
        public readonly WeaponLevel[] Levels;

        public int MaxLevel => Levels.Length;

        public WeaponDef(WeaponId id, string name, string description, params WeaponLevel[] levels)
        {
            Id = id;
            Name = name;
            Description = description;
            Levels = levels;
        }

        /// <param name="level">1-based.</param>
        public WeaponLevel At(int level) => Levels[System.Math.Clamp(level, 1, MaxLevel) - 1];
    }

    public static class WeaponCatalog
    {
        public const int MaxWeaponSlots = 6;
        public const float AxeSpeed = 10f;
        public const float AxeLifetime = 1.4f;
        public const float AxeTargetRange = 11f;
        /// <summary>Half-angle of the greatsword arc in degrees.</summary>
        public const float SlashHalfAngle = 65f;

        public static readonly WeaponDef[] All =
        {
            new(WeaponId.Greatsword, "大剣", "向いている方向を扇状になぎ払う",
                new WeaponLevel(16f, 1.35f, 2.3f, 1, 0, ""),
                new WeaponLevel(20f, 1.30f, 2.5f, 1, 0, "ダメージ +4、範囲 +10%"),
                new WeaponLevel(26f, 1.25f, 2.7f, 1, 0, "ダメージ +6、範囲 +10%"),
                new WeaponLevel(32f, 1.15f, 2.9f, 1, 0, "ダメージ +6、クールダウン短縮"),
                new WeaponLevel(42f, 1.05f, 3.3f, 1, 0, "ダメージ +10、範囲 +15%")),
            new(WeaponId.ThrowingAxe, "投げ斧", "最寄りの敵へ貫通する斧を投げる",
                new WeaponLevel(13f, 1.5f, 0.35f, 1, 1, ""),
                new WeaponLevel(15f, 1.45f, 0.35f, 2, 1, "本数 +1"),
                new WeaponLevel(18f, 1.40f, 0.40f, 2, 2, "ダメージ +3、貫通 +1"),
                new WeaponLevel(20f, 1.30f, 0.40f, 3, 2, "本数 +1"),
                new WeaponLevel(26f, 1.20f, 0.45f, 3, 4, "ダメージ +6、貫通 +2")),
            new(WeaponId.HolyAura, "聖なるオーラ", "周囲の敵に継続ダメージを与える",
                new WeaponLevel(4f, 0.5f, 1.6f, 0, 0, ""),
                new WeaponLevel(5f, 0.5f, 1.8f, 0, 0, "ダメージ +1、範囲 +12%"),
                new WeaponLevel(6f, 0.45f, 2.0f, 0, 0, "ダメージ +1、間隔短縮"),
                new WeaponLevel(8f, 0.45f, 2.2f, 0, 0, "ダメージ +2、範囲 +10%"),
                new WeaponLevel(10f, 0.4f, 2.6f, 0, 0, "ダメージ +2、範囲 +18%")),
        };

        public static WeaponDef Get(WeaponId id) => All[(int)id];
    }
}
