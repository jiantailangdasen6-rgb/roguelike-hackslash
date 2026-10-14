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
        // Objects of the map's rooms
        PressurePlate, // puzzle: light it by standing on it
        WardCrystal,   // puzzle: monsters go for it, the team keeps it whole
        CrackedWall,   // hides a secret room: break it open
        TreasureImp,   // puzzle: runs off with treasure (never fights back)
        // One or two monsters of each area's own (round 4)
        ShieldSkeleton, // crypt: holds a great shield up (armoured)
        Flagellant,     // chapel: a long whip lash in a line
        AbyssEye,       // abyss: floats and drops curse circles under players
        ShadeStalker,   // abyss: blinks in and lunges
        // Room events (round 5)
        Mimic,          // a campfire chest that bites back
        Captive,        // an adventurer in a cage: break the bars to set them free
        // A deathcaller's summons (on the players' side)
        BoneServant,
        // The new lands (round 6): three monsters, a lord and the lord's trial object each
        Drowned,        // sunken: a waterlogged corpse, slow and heavy-handed, spits water
        CarrionCrab,    // sunken: an armoured scuttler that lunges sideways
        TideWitch,      // sunken: calls bursts of water under players
        DrownedSaint,   // sunken lord
        FloodSeal,      // the saint's flood: break the seals to drain it
        FrostWolf,      // frost: runs in pairs and lunges
        Icebound,       // frost: frozen dead; shatters into ice when it falls
        FrostWitch,     // frost: ice spikes erupt along a line
        FrostQueen,     // frost lord
        IceMirror,      // the queen's reflection: no shadow, shatters when struck
        IronSentinel,   // forge: an armoured guard with a hammer
        FireImp,        // forge: a flying imp throwing fire, in pairs
        SlagBloater,    // forge: bursts into molten slag that keeps burning
        ForgeColossus,  // forge lord
        QuenchCistern,  // the colossus' overheat: break it to flood the floor and lure him in
        // Room dressing that breaks (round 7)
        Urn,            // a burial urn: smash it, sometimes souls spill out
        // The heart of the labyrinth (round 8)
        AshenKing,      // the last lord: the six lords' moves and trials, in turn
        // Each land's second lord (round 9) and their trial objects
        GraveReaper,    // crypt: hides in one of the coffins
        ChoirMistress,  // chapel: sweeping beams of holy light
        VoidEye,        // abyss: a gaze that only pillars stop
        GreatCrab,      // sunken: a shell only a powder keg cracks
        GreatWolf,      // frost: charges the marked one; lure it into a pillar
        Executioner,    // forge: rows of falling blades; break the chain posts
        Coffin,         // the reaper's coffins (one holds him)
        ChainPost,      // the executioner's chain posts
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
        public bool Friendly;     // on the players' side (a deathcaller's bone servants): untouchable, hunts monsters
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
                Type = EnemyType.BoneKing, Name = "骸骨の騎士王", Title = "朽ちた納骨堂の主", Sheet = "bone_king", Health = 2600f, Speed = 1.8f, Radius = 0.9f, Xp = 40,
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
                Type = EnemyType.BloodBishop, Name = "血の司教", Title = "血塗られた礼拝堂の主", Sheet = "blood_bishop", Health = 2700f, Speed = 1.6f, Radius = 0.8f, Xp = 50,
                PreferredDistance = 5.5f, AttackCooldown = 0.95f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 12f, SizeB = 0.5f, Range = 13f, Windup = 0.8f, Recover = 0.6f, Damage = 14f, ProjectileSpeed = 9f, Projectile = ProjectileKind.BloodOrb, Count = 7, Spread = 70f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.8f, Range = 99f, Windup = 1.2f, Recover = 0.7f, Damage = 22f, Extra = 2 },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.Cultist, Summon2 = EnemyType.Bloater },
                    new EnemyAttack { Kind = AttackKind.Blink, Shape = AttackShape.Circle, SizeA = 2.6f, Range = 99f, Windup = 1.0f, Recover = 0.8f, Damage = 22f },
                },
            },
            new()
            {
                Type = EnemyType.Lich, Name = "冥王リッチ", Title = "奈落の玉座の主", Sheet = "lich", Health = 3000f, Speed = 1.45f, Radius = 0.85f, Xp = 60,
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
            new()
            {
                // Health carries how long it stays lit (0..1).
                Type = EnemyType.PressurePlate, Name = "押し板", Sheet = "pressure_plate", Health = 1f, Speed = 0f, Radius = 0.3f, Xp = 0, Prop = true, Untargetable = true, Flat = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // The team can't hurt it; monsters chip at it (RunSimulation.Puzzles).
                Type = EnemyType.WardCrystal, Name = "守りの結晶", Sheet = "ward_crystal", Health = 320f, Speed = 0f, Radius = 0.7f, Xp = 0, Prop = true, Untargetable = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                Type = EnemyType.CrackedWall, Name = "ひび割れた壁", Sheet = "cracked_wall", Health = 45f, Speed = 0f, Radius = 0.8f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // Flees instead of fighting (RunSimulation.Puzzles); a little slower than a player.
                Type = EnemyType.TreasureImp, Name = "宝を抱えた小鬼", Sheet = "treasure_imp", Health = 150f, Speed = 4.1f, Radius = 0.38f, Xp = 8,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                Type = EnemyType.ShieldSkeleton, Name = "骨の盾兵", Sheet = "shield_skeleton", Health = 45f, Speed = 1.7f, Radius = 0.45f, Xp = 4,
                AttackCooldown = 1.9f, Armored = true,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 1.9f, SizeB = 90f, Range = 1.6f, Windup = 0.8f, Recover = 0.6f, Damage = 16f } },
            },
            new()
            {
                Type = EnemyType.Flagellant, Name = "鞭打ちの修道士", Sheet = "flagellant", Health = 70f, Speed = 2.6f, Radius = 0.42f, Xp = 4,
                AttackCooldown = 2.2f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Line, SizeA = 4.4f, SizeB = 0.75f, Range = 4f, Windup = 0.65f, Recover = 0.55f, Damage = 18f } },
            },
            new()
            {
                Type = EnemyType.AbyssEye, Name = "奈落の眼", Sheet = "abyss_eye", Health = 64f, Speed = 1.6f, Radius = 0.45f, Xp = 5,
                PreferredDistance = 7f, AttackCooldown = 3.2f, Flying = true,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.4f, Range = 12f, Windup = 1.1f, Recover = 0.6f, Damage = 20f } },
            },
            new()
            {
                Type = EnemyType.ShadeStalker, Name = "影の狩人", Sheet = "shade_stalker", Health = 72f, Speed = 3f, Radius = 0.4f, Xp = 5,
                AttackCooldown = 2.4f,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Blink, Shape = AttackShape.Circle, SizeA = 1.3f, Range = 8f, Windup = 0.75f, Recover = 0.5f, Damage = 16f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 4.6f, SizeB = 1f, Range = 4.4f, Windup = 0.5f, Recover = 0.6f, Damage = 18f, ProjectileSpeed = 18f },
                },
            },
            new()
            {
                // A campfire chest that was never a chest: hops after you and bites (RunSimulation.Events).
                Type = EnemyType.Mimic, Name = "ミミック", Title = "宝箱に化けた魔物", Sheet = "mimic", Health = 150f, Speed = 2.9f, Radius = 0.55f, Xp = 14,
                AttackCooldown = 1.7f,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 5f, SizeB = 1.2f, Range = 4.6f, Windup = 0.6f, Recover = 0.7f, Damage = 22f, ProjectileSpeed = 17f },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.SelfCircle, SizeA = 1.9f, Range = 1.8f, Windup = 0.7f, Recover = 0.6f, Damage = 20f },
                },
            },
            new()
            {
                // A fight room's prisoner: break the cage to set them free (they thank the team with gear).
                Type = EnemyType.Captive, Name = "囚われた冒険者", Sheet = "captive", Health = 45f, Speed = 0f, Radius = 0.6f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // A deathcaller's summon: a skeleton raised on the players' side (RunSimulation.Deathcaller).
                Type = EnemyType.BoneServant, Name = "骨の兵", Sheet = "bone_servant", Health = 1f, Speed = 3.7f, Radius = 0.36f, Xp = 0, Friendly = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },

            // ---------------------------------------------------------------- Sunken catacombs
            new()
            {
                Type = EnemyType.Drowned, Name = "溺れた亡者", Sheet = "drowned", Health = 88f, Speed = 1.9f, Radius = 0.45f, Xp = 3,
                AttackCooldown = 1.9f,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 2.0f, SizeB = 110f, Range = 1.7f, Windup = 0.75f, Recover = 0.55f, Damage = 18f },
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 7f, SizeB = 0.5f, Range = 6.5f, Windup = 0.85f, Recover = 0.5f, Damage = 12f, ProjectileSpeed = 8f, Projectile = ProjectileKind.WaterOrb },
                },
            },
            new()
            {
                Type = EnemyType.CarrionCrab, Name = "屍喰い蟹", Sheet = "carrion_crab", Health = 52f, Speed = 3.1f, Radius = 0.42f, Xp = 3,
                AttackCooldown = 2.1f, Armored = true,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 4.4f, SizeB = 1.0f, Range = 4.2f, Windup = 0.5f, Recover = 0.7f, Damage = 15f, ProjectileSpeed = 17f } },
            },
            new()
            {
                Type = EnemyType.TideWitch, Name = "水底の魔女", Sheet = "tide_witch", Health = 58f, Speed = 1.8f, Radius = 0.4f, Xp = 5,
                PreferredDistance = 7f, AttackCooldown = 3f,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.6f, Range = 11f, Windup = 1.15f, Recover = 0.6f, Damage = 20f, Extra = 1 },
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 10f, SizeB = 0.5f, Range = 10f, Windup = 0.9f, Recover = 0.5f, Damage = 12f, ProjectileSpeed = 7f, Projectile = ProjectileKind.WaterOrb, Count = 3, Spread = 30f },
                },
            },
            new()
            {
                Type = EnemyType.DrownedSaint, Name = "水底の聖女", Title = "水没した地下墓地の主", Sheet = "drowned_saint", Health = 2700f, Speed = 1.55f, Radius = 0.85f, Xp = 50,
                PreferredDistance = 5f, AttackCooldown = 0.95f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 12f, SizeB = 0.5f, Range = 13f, Windup = 0.85f, Recover = 0.6f, Damage = 14f, ProjectileSpeed = 8.5f, Projectile = ProjectileKind.WaterOrb, Count = 9, Spread = 100f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 2.0f, Range = 99f, Windup = 1.2f, Recover = 0.7f, Damage = 22f, Extra = 2 },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.SelfCircle, SizeA = 3.0f, Range = 2.8f, Windup = 1.0f, Recover = 0.8f, Damage = 28f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.Drowned, Summon2 = EnemyType.CarrionCrab },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 8f, SizeB = 1.6f, Range = 8f, Windup = 1.0f, Recover = 1.0f, Damage = 24f, ProjectileSpeed = 14f },
                },
            },
            new()
            {
                // The flood holds while any stands; nothing else hurts it much.
                Type = EnemyType.FloodSeal, Name = "封じの栓", Sheet = "flood_seal", Health = 75f, Speed = 0f, Radius = 0.55f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },

            // ---------------------------------------------------------------- Frozen mausoleum
            new()
            {
                Type = EnemyType.FrostWolf, Name = "雪狼", Sheet = "frost_wolf", Health = 40f, Speed = 4.0f, Radius = 0.4f, Xp = 2,
                AttackCooldown = 1.7f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 4.4f, SizeB = 0.9f, Range = 4f, Windup = 0.45f, Recover = 0.6f, Damage = 13f, ProjectileSpeed = 18f } },
            },
            new()
            {
                Type = EnemyType.Icebound, Name = "氷漬けの亡者", Sheet = "icebound", Health = 95f, Speed = 1.45f, Radius = 0.48f, Xp = 4,
                AttackCooldown = 2.2f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 2.2f, SizeB = 100f, Range = 1.8f, Windup = 0.85f, Recover = 0.6f, Damage = 20f } },
            },
            new()
            {
                Type = EnemyType.FrostWitch, Name = "霜の魔女", Sheet = "frost_witch", Health = 56f, Speed = 1.9f, Radius = 0.4f, Xp = 5,
                PreferredDistance = 7.5f, AttackCooldown = 2.8f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Line, SizeA = 9f, SizeB = 0.9f, Range = 9f, Windup = 1.0f, Recover = 0.6f, Damage = 20f } },
            },
            new()
            {
                Type = EnemyType.FrostQueen, Name = "凍てつく女王", Title = "凍てつく霊廟の主", Sheet = "frost_queen", Health = 2700f, Speed = 1.7f, Radius = 0.8f, Xp = 50,
                PreferredDistance = 4.5f, AttackCooldown = 0.9f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 6f, SizeB = 60f, Range = 6f, Windup = 1.0f, Recover = 0.8f, Damage = 30f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.6f, Range = 99f, Windup = 1.2f, Recover = 0.6f, Damage = 24f, Extra = 3 },
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 12f, SizeB = 0.45f, Range = 12f, Windup = 0.8f, Recover = 0.5f, Damage = 16f, ProjectileSpeed = 11f, Projectile = ProjectileKind.IceShard, Count = 5, Spread = 50f },
                    new EnemyAttack { Kind = AttackKind.Blink, Shape = AttackShape.Circle, SizeA = 2.4f, Range = 99f, Windup = 0.9f, Recover = 0.7f, Damage = 22f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.FrostWolf, Summon2 = EnemyType.Icebound },
                },
            },
            new()
            {
                // The queen's double in the ice: looks the same but casts no shadow, and breaks when struck.
                Type = EnemyType.IceMirror, Name = "凍てつく女王", Title = "氷の写し身", Sheet = "frost_queen", Health = 60f, Speed = 1.9f, Radius = 0.8f, Xp = 0,
                PreferredDistance = 4.5f, AttackCooldown = 1.6f,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 12f, SizeB = 0.45f, Range = 12f, Windup = 0.9f, Recover = 0.6f, Damage = 12f, ProjectileSpeed = 10f, Projectile = ProjectileKind.IceShard, Count = 3, Spread = 30f },
                },
            },

            // ---------------------------------------------------------------- Burnt forge
            new()
            {
                Type = EnemyType.IronSentinel, Name = "鉄の番兵", Sheet = "iron_sentinel", Health = 150f, Speed = 1.6f, Radius = 0.52f, Xp = 6,
                AttackCooldown = 2.3f, Armored = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.SelfCircle, SizeA = 2.3f, Range = 2.0f, Windup = 1.0f, Recover = 0.8f, Damage = 26f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 4.2f, SizeB = 1.2f, Range = 4.6f, Windup = 0.85f, Recover = 0.9f, Damage = 20f, ProjectileSpeed = 13f },
                },
            },
            new()
            {
                Type = EnemyType.FireImp, Name = "火の小鬼", Sheet = "fire_imp", Health = 30f, Speed = 3.6f, Radius = 0.32f, Xp = 2,
                PreferredDistance = 6f, AttackCooldown = 2.1f, Flying = true,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 9f, SizeB = 0.4f, Range = 9f, Windup = 0.7f, Recover = 0.4f, Damage = 11f, ProjectileSpeed = 10f, Projectile = ProjectileKind.FireBolt } },
            },
            new()
            {
                Type = EnemyType.SlagBloater, Name = "溶鉄の膨れ", Sheet = "slag_bloater", Health = 80f, Speed = 1.5f, Radius = 0.5f, Xp = 3,
                AttackCooldown = 0.4f,
                Attacks = new[] { new EnemyAttack { Kind = AttackKind.SelfDestruct, Shape = AttackShape.SelfCircle, SizeA = 2.6f, Range = 1.7f, Windup = 1.0f, Recover = 0.2f, Damage = 26f, HitsEnemies = true } },
            },
            new()
            {
                Type = EnemyType.ForgeColossus, Name = "炉の巨人", Title = "燃え落ちた鍛冶場の主", Sheet = "forge_colossus", Health = 3000f, Speed = 1.5f, Radius = 0.95f, Xp = 60,
                AttackCooldown = 1.0f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Circle, SizeA = 2.4f, Range = 3.6f, Windup = 1.1f, Recover = 0.8f, Damage = 34f },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.SelfCircle, SizeA = 4.2f, Range = 3.5f, Windup = 1.3f, Recover = 1.0f, Damage = 26f },
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 11f, SizeB = 0.45f, Range = 11f, Windup = 0.9f, Recover = 0.6f, Damage = 14f, ProjectileSpeed = 9f, Projectile = ProjectileKind.FireBolt, Count = 7, Spread = 80f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 10f, SizeB = 1.8f, Range = 10f, Windup = 1.0f, Recover = 1.1f, Damage = 28f, ProjectileSpeed = 17f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.FireImp, Summon2 = EnemyType.IronSentinel },
                },
            },
            new()
            {
                Type = EnemyType.QuenchCistern, Name = "水甕", Sheet = "quench_cistern", Health = 40f, Speed = 0f, Radius = 0.6f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // A few stand by the walls of most rooms: one blow breaks them (RunSimulation.Lands).
                Type = EnemyType.Urn, Name = "壺", Sheet = "urn", Health = 6f, Speed = 0f, Radius = 0.38f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                // What the heart's nightmare made of the old king: he fights with every lord's arts (RunSimulation.Heart).
                Type = EnemyType.AshenKing, Name = "灰の王", Title = "迷宮の心臓に巣食うもの", Sheet = "ashen_king", Health = 3400f, Speed = 1.6f, Radius = 1.0f, Xp = 90,
                PreferredDistance = 4f, AttackCooldown = 0.85f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 3.8f, SizeB = 150f, Range = 3.2f, Windup = 0.85f, Recover = 0.7f, Damage = 30f },
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 12f, SizeB = 0.5f, Range = 13f, Windup = 0.85f, Recover = 0.6f, Damage = 15f, ProjectileSpeed = 9f, Projectile = ProjectileKind.BloodOrb, Count = 9, Spread = 90f },
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.SelfCircle, SizeA = 2.4f, Range = 99f, Windup = 1.0f, Recover = 0.7f, Damage = 16f, ProjectileSpeed = 6.5f, Projectile = ProjectileKind.SoulOrb, Count = 18, Spread = 360f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.8f, Range = 99f, Windup = 1.2f, Recover = 0.6f, Damage = 26f, Extra = 3 },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.Wraith, Summon2 = EnemyType.IronSentinel },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 10f, SizeB = 1.8f, Range = 10f, Windup = 1.0f, Recover = 1.0f, Damage = 30f, ProjectileSpeed = 18f },
                    new EnemyAttack { Kind = AttackKind.Cage, Shape = AttackShape.Circle, SizeA = 1.15f, Range = 99f, Windup = 1.0f, Recover = 0.5f },
                },
            },

            // ---------------------------------------------------------------- Second lords (RunSimulation.Lords2)
            new()
            {
                Type = EnemyType.GraveReaper, Name = "墓守の大鎌", Title = "朽ちた納骨堂の主", Sheet = "grave_reaper", Health = 2600f, Speed = 2.0f, Radius = 0.8f, Xp = 40,
                AttackCooldown = 0.9f, Boss = true, Flying = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 3.6f, SizeB = 160f, Range = 3f, Windup = 0.8f, Recover = 0.7f, Damage = 28f },
                    new EnemyAttack { Kind = AttackKind.Blink, Shape = AttackShape.Circle, SizeA = 2.2f, Range = 99f, Windup = 0.9f, Recover = 0.7f, Damage = 24f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.6f, Range = 99f, Windup = 1.2f, Recover = 0.6f, Damage = 22f, Extra = 2 },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 3, Summon1 = EnemyType.Skeleton, Summon2 = EnemyType.Ghoul },
                },
            },
            new()
            {
                Type = EnemyType.ChoirMistress, Name = "堕ちた聖歌隊長", Title = "血塗られた礼拝堂の主", Sheet = "choir_mistress", Health = 2700f, Speed = 1.5f, Radius = 0.8f, Xp = 50,
                PreferredDistance = 5.5f, AttackCooldown = 0.95f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 12f, SizeB = 0.5f, Range = 13f, Windup = 0.8f, Recover = 0.6f, Damage = 14f, ProjectileSpeed = 9f, Projectile = ProjectileKind.BloodOrb, Count = 7, Spread = 80f },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Line, SizeA = 10f, SizeB = 1.2f, Range = 10f, Windup = 0.9f, Recover = 0.7f, Damage = 26f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 2.0f, Range = 99f, Windup = 1.2f, Recover = 0.7f, Damage = 22f, Extra = 2 },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.Cultist, Summon2 = EnemyType.Flagellant },
                },
            },
            new()
            {
                Type = EnemyType.VoidEye, Name = "虚ろの眼", Title = "奈落の玉座の主", Sheet = "void_eye", Health = 2800f, Speed = 1.2f, Radius = 1.0f, Xp = 60,
                PreferredDistance = 6f, AttackCooldown = 0.95f, Boss = true, Flying = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.SelfCircle, SizeA = 2.4f, Range = 99f, Windup = 1.0f, Recover = 0.7f, Damage = 15f, ProjectileSpeed = 6.5f, Projectile = ProjectileKind.SoulOrb, Count = 14, Spread = 360f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.6f, Range = 99f, Windup = 1.2f, Recover = 0.6f, Damage = 24f, Extra = 3 },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Line, SizeA = 12f, SizeB = 1.0f, Range = 12f, Windup = 1.1f, Recover = 0.8f, Damage = 30f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.AbyssEye, Summon2 = EnemyType.ShadeStalker },
                },
            },
            new()
            {
                Type = EnemyType.GreatCrab, Name = "沼の大蟹", Title = "水没した地下墓地の主", Sheet = "great_crab", Health = 2700f, Speed = 2.0f, Radius = 1.1f, Xp = 50,
                AttackCooldown = 0.95f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 3.2f, SizeB = 120f, Range = 2.8f, Windup = 0.8f, Recover = 0.7f, Damage = 28f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 9f, SizeB = 2.0f, Range = 9f, Windup = 0.9f, Recover = 1.0f, Damage = 26f, ProjectileSpeed = 16f },
                    new EnemyAttack { Kind = AttackKind.Projectile, Shape = AttackShape.Line, SizeA = 11f, SizeB = 0.5f, Range = 11f, Windup = 0.8f, Recover = 0.6f, Damage = 14f, ProjectileSpeed = 8.5f, Projectile = ProjectileKind.WaterOrb, Count = 5, Spread = 50f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.CarrionCrab, Summon2 = EnemyType.Drowned },
                },
            },
            new()
            {
                Type = EnemyType.GreatWolf, Name = "白き大狼", Title = "凍てつく霊廟の主", Sheet = "great_wolf", Health = 2600f, Speed = 3.0f, Radius = 1.0f, Xp = 50,
                AttackCooldown = 0.9f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 3f, SizeB = 100f, Range = 2.6f, Windup = 0.7f, Recover = 0.6f, Damage = 26f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 11f, SizeB = 1.8f, Range = 11f, Windup = 0.9f, Recover = 1.0f, Damage = 28f, ProjectileSpeed = 20f },
                    new EnemyAttack { Kind = AttackKind.Rain, Shape = AttackShape.Circle, SizeA = 1.6f, Range = 99f, Windup = 1.2f, Recover = 0.6f, Damage = 22f, Extra = 2 },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.FrostWolf, Summon2 = EnemyType.FrostWolf },
                },
            },
            new()
            {
                Type = EnemyType.Executioner, Name = "鎖の処刑人", Title = "燃え落ちた鍛冶場の主", Sheet = "executioner", Health = 3000f, Speed = 1.6f, Radius = 0.95f, Xp = 60,
                AttackCooldown = 1.0f, Boss = true,
                Attacks = new[]
                {
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Circle, SizeA = 2.2f, Range = 3.5f, Windup = 1.0f, Recover = 0.8f, Damage = 34f },
                    new EnemyAttack { Kind = AttackKind.Melee, Shape = AttackShape.Cone, SizeA = 4f, SizeB = 170f, Range = 3.2f, Windup = 1.1f, Recover = 0.9f, Damage = 28f },
                    new EnemyAttack { Kind = AttackKind.Lunge, Shape = AttackShape.Line, SizeA = 8f, SizeB = 1.8f, Range = 8f, Windup = 1.0f, Recover = 1.0f, Damage = 26f, ProjectileSpeed = 16f },
                    new EnemyAttack { Kind = AttackKind.Summon, Shape = AttackShape.SelfCircle, SizeA = 2f, Range = 99f, Windup = 1.0f, Recover = 0.6f, SummonCount = 2, Summon1 = EnemyType.IronSentinel, Summon2 = EnemyType.FireImp },
                },
            },
            new()
            {
                Type = EnemyType.Coffin, Name = "棺", Sheet = "coffin", Health = 50f, Speed = 0f, Radius = 0.6f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
            new()
            {
                Type = EnemyType.ChainPost, Name = "鎖の杭", Sheet = "chain_post", Health = 70f, Speed = 0f, Radius = 0.55f, Xp = 0, Prop = true,
                Attacks = System.Array.Empty<EnemyAttack>(),
            },
        };

        public static EnemyDef Get(EnemyType type) => All[(int)type];
    }
}
