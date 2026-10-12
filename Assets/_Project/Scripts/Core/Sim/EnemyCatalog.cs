namespace Game.Core
{
    public enum EnemyType : byte
    {
        Skeleton,
        SkeletonArcher,
        Ghoul,
        Cultist,
        BoneKing,
        Bat,
        Bloater,
        DreadKnight,
        Necromancer,
        Wraith,
        BloodBishop,
        Lich,
        PowderKeg,
        TreasureChest,
        // Objects of the bosses' trials
        BoneBrazier,  // feeds the Bone King's ward
        BoneCage,     // closes around a player
        BloodSigil,   // a rune to light by standing on it
        SoulVessel,   // the Lich's phylactery
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
        Melee,         // instant hit in the shape
        Projectile,    // fires along the line (Count/Spread for fans; Spread 360 = all around)
        Lunge,         // dashes along the line, hitting what it touches
        Summon,        // calls minions
        Rain,          // circles drop on every player (+Extra random spots)
        Blink,         // vanishes and reappears inside the telegraphed circle, hitting there
        SelfDestruct,  // bursts (SelfCircle) and dies
        Cage,          // a bone cage closes on whoever stands in the circle (teammates break it open)
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
        public ProjectileKind Projectile = ProjectileKind.EnemyArrow;
        public int Count = 1;        // projectiles per volley
        public float Spread;         // fan angle (degrees)
        public int Extra;            // rain: extra random circles
        public int SummonCount;
        public EnemyType Summon1 = EnemyType.Skeleton;
        public EnemyType Summon2 = EnemyType.SkeletonArcher;
        public bool HitsEnemies;     // explosions also hurt other monsters
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
        public bool Flying;   // ignores obstacles, no ground shadow contact
        public bool Armored;  // takes much less damage unless frozen, stunned or marked
        public bool Prop;     // a breakable object, not a monster (no AI, no XP)
        public bool Untargetable; // attacks pass through it (floor runes)
        public bool Flat;     // drawn on the floor beneath everything
        public string Title;  // boss subtitle
        public EnemyAttack[] Attacks;
    }

    public static class EnemyCatalog
    {
        public const float EliteHealthMultiplier = 3.5f;
        public const float EliteDamageMultiplier = 1.4f;
        public const float EliteRadiusMultiplier = 1.25f;
        public const int EliteXpMultiplier = 4;
        public const float ArmorFactor = 0.4f;

        /// <summary>The boss waiting at the end of each area.</summary>
        public static readonly EnemyType[] AreaBosses = { EnemyType.BoneKing, EnemyType.BloodBishop, EnemyType.Lich };

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
                Type = EnemyType.BoneKing, Name = "骸骨の騎士王", Title = "朽ちた納骨堂の主", Sheet = "bone_king", Health = 3200f, Speed = 1.8f, Radius = 0.9f, Xp = 40,
                AttackCooldown = 0.9f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 3.4f, SizeB = 140f, Range = 2.8f, Windup = 0.85f, Recover = 0.7f, Damage = 26f },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.SelfCircle, SizeA = 3.2f, Range = 3f, Windup = 1.1f, Recover = 0.9f, Damage = 30f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 9f, SizeB = 1.6f, Range = 9f, Windup = 1.0f, Recover = 1.0f, Damage = 24f, ProjectileSpeed = 18f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 3 },
                    new EnemyAttack { Kind = AttackKind.Cage, Shape = AttackShape.Circle, SizeA = 1.15f, Range = 99f, Windup = 1.0f, Recover = 0.5f },
                },
            },
            new()
            {
                Type = EnemyType.Bat, Name = "吸血蝙蝠", Sheet = "bat", Health = 22f, Speed = 4.3f, Radius = 0.32f, Xp = 1,
                AttackCooldown = 1.3f, Flying = true,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 3.2f, SizeB = 0.7f, Range = 3.0f, Windup = 0.4f, Recover = 0.55f, Damage = 8f, ProjectileSpeed = 15f } },
            },
            new()
            {
                Type = EnemyType.Bloater, Name = "腐れ膨れ", Sheet = "bloater", Health = 75f, Speed = 1.55f, Radius = 0.5f, Xp = 3,
                AttackCooldown = 0.4f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.SelfDestruct, Shape = AttackShape.SelfCircle, SizeA = 2.5f, Range = 1.7f, Windup = 1.0f, Recover = 0.2f, Damage = 30f, HitsEnemies = true } },
            },
            new()
            {
                Type = EnemyType.DreadKnight, Name = "亡者の騎士", Sheet = "dread_knight", Health = 170f, Speed = 1.75f, Radius = 0.5f, Xp = 6,
                AttackCooldown = 2.1f, Armored = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 2.5f, SizeB = 120f, Range = 2.1f, Windup = 0.9f, Recover = 0.7f, Damage = 26f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 4.5f, SizeB = 1.2f, Range = 5f, Windup = 0.9f, Recover = 0.9f, Damage = 22f, ProjectileSpeed = 14f },
                },
            },
            new()
            {
                Type = EnemyType.Necromancer, Name = "死霊術師", Sheet = "necromancer", Health = 62f, Speed = 1.8f, Radius = 0.42f, Xp = 6,
                PreferredDistance = 7.5f, AttackCooldown = 2.6f,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 10f, SizeB = 0.5f, Range = 11f, Windup = 0.9f, Recover = 0.5f, Damage = 13f, ProjectileSpeed = 7.5f, Projectile = ProjectileKind.SoulOrb, Count = 3, Spread = 24f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 1.6f, Range = 99f, Windup = 1.1f, Recover = 0.6f, SummonCount = 2 },
                },
            },
            new()
            {
                Type = EnemyType.Wraith, Name = "怨霊", Sheet = "wraith", Health = 58f, Speed = 2.6f, Radius = 0.4f, Xp = 5,
                AttackCooldown = 2.6f, Flying = true,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Blink, Shape = AttackShape.Circle, SizeA = 1.5f, Range = 8f, Windup = 0.85f, Recover = 0.6f, Damage = 20f } },
            },
            new()
            {
                Type = EnemyType.BloodBishop, Name = "血の司教", Title = "血塗られた礼拝堂の主", Sheet = "blood_bishop", Health = 3300f, Speed = 1.6f, Radius = 0.8f, Xp = 50,
                PreferredDistance = 5.5f, AttackCooldown = 0.95f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 12f, SizeB = 0.5f, Range = 13f, Windup = 0.8f, Recover = 0.6f, Damage = 16f, ProjectileSpeed = 9f, Projectile = ProjectileKind.BloodOrb, Count = 7, Spread = 70f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.8f, Range = 99f, Windup = 1.2f, Recover = 0.7f, Damage = 26f, Extra = 2 },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.Cultist, Summon2 = EnemyType.Bloater },
                    new EnemyAttack { Kind = AttackKind.Blink, Shape = AttackShape.Circle, SizeA = 2.6f, Range = 99f, Windup = 1.0f, Recover = 0.8f, Damage = 22f },
                },
            },
            new()
            {
                Type = EnemyType.Lich, Name = "冥王リッチ", Title = "奈落の玉座の主", Sheet = "lich", Health = 3700f, Speed = 1.45f, Radius = 0.85f, Xp = 60,
                PreferredDistance = 4.5f, AttackCooldown = 0.9f, Boss = true, Flying = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.SelfCircle, SizeA = 2.4f, Range = 99f, Windup = 1.0f, Recover = 0.7f, Damage = 16f, ProjectileSpeed = 6.5f, Projectile = ProjectileKind.SoulOrb, Count = 16, Spread = 360f },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 6f, SizeB = 70f, Range = 6f, Windup = 1.1f, Recover = 0.8f, Damage = 32f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.6f, Range = 99f, Windup = 1.3f, Recover = 0.6f, Damage = 28f, Extra = 3 },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.1f, Recover = 0.6f, SummonCount = 3, Summon1 = EnemyType.Wraith, Summon2 = EnemyType.Skeleton },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.SelfCircle, SizeA = 3.4f, Range = 3.2f, Windup = 1.15f, Recover = 0.9f, Damage = 34f },
                },
            },
            new()
            {
                Type = EnemyType.PowderKeg, Name = "火薬樽", Sheet = "powder_keg", Health = 1f, Speed = 0f, Radius = 0.42f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // Rest rooms: a few hits break it open and souls spill out (the team's XP).
                Type = EnemyType.TreasureChest, Name = "宝箱", Sheet = "treasure_chest", Health = 30f, Speed = 0f, Radius = 0.55f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // Bone King: while any stands, his ward holds.
                Type = EnemyType.BoneBrazier, Name = "骨の燭台", Sheet = "bone_brazier", Health = 70f, Speed = 0f, Radius = 0.5f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                Type = EnemyType.BoneCage, Name = "骨の檻", Sheet = "bone_cage", Health = 55f, Speed = 0f, Radius = 0.6f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // Health carries how far it is lit (0..1).
                Type = EnemyType.BloodSigil, Name = "血の紋章", Sheet = "blood_sigil", Health = 1f, Speed = 0f, Radius = 0.3f, Xp = 0, Prop = true, Untargetable = true, Flat = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                Type = EnemyType.SoulVessel, Name = "魂の器", Sheet = "soul_vessel", Health = 260f, Speed = 0f, Radius = 0.55f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
        };

        public static EnemyDef Get(EnemyType type) => All[(int)type];
    }
}
