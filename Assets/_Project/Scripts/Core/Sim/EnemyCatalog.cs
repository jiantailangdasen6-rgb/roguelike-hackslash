namespace Game.Core
{
    public enum EnemyType : byte
    {
        Skeleton,
        SkeletonArcher,
        Ghoul,
        Cultist,
        BoneKing,
    }

    /// <summary>Shape of an attack's danger zone, drawn as a telegraph before it lands.</summary>
    public enum AttackShape : byte
    {
        Cone,       // Size.x = range, Size.y = angle (deg)
        Circle,     // Size.x = radius (centered on Target)
        SelfCircle, // Size.x = radius (centered on the attacker)
        Line,       // Size.x = length, Size.y = width (projectile or lunge)
    }

    public enum AttackKind : byte
    {
        Melee,       // instant hit in the shape
        Projectile,  // fires along the line
        Lunge,       // dashes along the line, hitting what it touches
        Summon,      // calls skeletons
    }

    public sealed class EnemyAttack
    {
        public AttackKind Kind;
        public AttackShape Shape;
        public float SizeA;          // see AttackShape
        public float SizeB;
        public float Range;          // starts the attack when the target is this close
        public float Windup;         // telegraph time
        public float Recover;
        public float Damage;
        public float ProjectileSpeed;
        public int SummonCount;
    }

    public sealed class EnemyDef
    {
        public EnemyType Type;
        public string Name;
        public string Sheet;
        public float Health;
        public float Speed;
        public float Radius;
        public int Xp;
        public float PreferredDistance; // ranged enemies keep this distance
        public float AttackCooldown;
        public bool Boss;
        public EnemyAttack[] Attacks;
    }

    public static class EnemyCatalog
    {
        public const float EliteHealthMultiplier = 3.5f;
        public const float EliteDamageMultiplier = 1.4f;
        public const float EliteRadiusMultiplier = 1.25f;
        public const int EliteXpMultiplier = 4;

        public static readonly EnemyDef[] All =
        {
            new()
            {
                Type = EnemyType.Skeleton, Name = "スケルトン兵", Sheet = "skeleton", Health = 62f, Speed = 2.3f, Radius = 0.4f, Xp = 2,
                AttackCooldown = 1.4f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 1.8f, SizeB = 100f, Range = 1.5f, Windup = 0.6f, Recover = 0.45f, Damage = 14f } },
            },
            new()
            {
                Type = EnemyType.SkeletonArcher, Name = "骸骨弓兵", Sheet = "skeleton_archer", Health = 42f, Speed = 2.1f, Radius = 0.4f, Xp = 3,
                PreferredDistance = 6.5f, AttackCooldown = 2.2f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 11f, SizeB = 0.35f, Range = 10f, Windup = 0.8f, Recover = 0.4f, Damage = 12f, ProjectileSpeed = 13f } },
            },
            new()
            {
                Type = EnemyType.Ghoul, Name = "グール", Sheet = "ghoul", Health = 50f, Speed = 3.4f, Radius = 0.42f, Xp = 3,
                AttackCooldown = 2f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 4.8f, SizeB = 1.0f, Range = 4.2f, Windup = 0.55f, Recover = 0.6f, Damage = 16f, ProjectileSpeed = 19f } },
            },
            new()
            {
                Type = EnemyType.Cultist, Name = "狂信者", Sheet = "cultist", Health = 54f, Speed = 1.9f, Radius = 0.4f, Xp = 4,
                PreferredDistance = 7f, AttackCooldown = 3.2f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Circle, SizeA = 1.7f, Range = 10f, Windup = 1.1f, Recover = 0.5f, Damage = 22f } },
            },
            new()
            {
                Type = EnemyType.BoneKing, Name = "骸骨の騎士王", Sheet = "bone_king", Health = 3200f, Speed = 1.8f, Radius = 0.9f, Xp = 40,
                AttackCooldown = 0.9f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 3.4f, SizeB = 140f, Range = 2.8f, Windup = 0.85f, Recover = 0.7f, Damage = 26f },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.SelfCircle, SizeA = 3.2f, Range = 3f, Windup = 1.1f, Recover = 0.9f, Damage = 30f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 9f, SizeB = 1.6f, Range = 9f, Windup = 1.0f, Recover = 1.0f, Damage = 24f, ProjectileSpeed = 18f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 3 },
                },
            },
        };

        public static EnemyDef Get(EnemyType type) => All[(int)type];
    }
}
