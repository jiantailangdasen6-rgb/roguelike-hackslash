namespace Game.Core
{
    public enum EnemyType : byte
    {
        Slime,
        GoblinArcher,
        KingSlime,
    }

    public sealed class EnemyDef
    {
        public readonly EnemyType Type;
        public readonly string Name;
        public readonly float Health;
        public readonly float Speed;
        public readonly float Radius;
        public readonly float ContactDamage;
        public readonly int Xp;

        // Ranged attack (0 cooldown = melee only)
        public readonly float PreferredDistance;
        public readonly float FireCooldown;
        public readonly float ShotSpeed;
        public readonly float ShotDamage;
        /// <summary>Shots per volley; more than 1 fires a full ring.</summary>
        public readonly int ShotCount;

        public bool IsRanged => FireCooldown > 0f;
        public bool IsBoss => Type == EnemyType.KingSlime;

        public EnemyDef(EnemyType type, string name, float health, float speed, float radius, float contactDamage, int xp,
            float preferredDistance = 0f, float fireCooldown = 0f, float shotSpeed = 0f, float shotDamage = 0f, int shotCount = 1)
        {
            Type = type;
            Name = name;
            Health = health;
            Speed = speed;
            Radius = radius;
            ContactDamage = contactDamage;
            Xp = xp;
            PreferredDistance = preferredDistance;
            FireCooldown = fireCooldown;
            ShotSpeed = shotSpeed;
            ShotDamage = shotDamage;
            ShotCount = shotCount;
        }
    }

    public static class EnemyCatalog
    {
        public const float EliteHealthMultiplier = 6f;
        public const float EliteRadiusMultiplier = 1.35f;
        public const float EliteDamageMultiplier = 1.5f;
        public const int EliteXpMultiplier = 6;

        public static readonly EnemyDef[] All =
        {
            new(EnemyType.Slime, "スライム", health: 10f, speed: 1.7f, radius: 0.4f, contactDamage: 7f, xp: 1),
            new(EnemyType.GoblinArcher, "ゴブリン弓兵", health: 14f, speed: 2.0f, radius: 0.4f, contactDamage: 5f, xp: 2,
                preferredDistance: 6f, fireCooldown: 2.8f, shotSpeed: 5.5f, shotDamage: 9f),
            new(EnemyType.KingSlime, "キングスライム", health: 700f, speed: 1.25f, radius: 1.5f, contactDamage: 20f, xp: 40,
                preferredDistance: 0f, fireCooldown: 3.5f, shotSpeed: 4.5f, shotDamage: 12f, shotCount: 10),
        };

        public static EnemyDef Get(EnemyType type) => All[(int)type];
    }
}
