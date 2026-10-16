namespace Game.Core
{
    public enum ClassId : byte
    {
        Warrior,
        Mage,
        Ranger,
        // Unlocked by requests (see QuestCatalog); keep appending (saves, sprites and gear index by id).
        Guardian,
        Assassin,
        Alchemist,
        Deathcaller, // 死霊術師: calls bone servants, curses (round 5)
        Paladin,     // 聖騎士: holy sword, judgment, devotion
    }

    public enum AbilitySlot : byte
    {
        Basic,
        Skill1,
        Skill2,
        Dodge,
    }

    /// <summary>Static description of a playable class: base stats, per-level growth, ability numbers and texts.</summary>
    public sealed class ClassDef
    {
        public ClassId Id;
        public string Name;
        public string Role;
        public string Sheet;           // sprite sheet name

        public float Health;
        public float MoveSpeed;
        public float Armor;

        // Growth curve (per team level above 1). Mastery can raise these.
        public float HealthPerLevel;
        public float MightPerLevel;

        // Basic attack
        public string BasicName;
        public float BasicDamage;
        public float BasicInterval;
        public float BasicRange;       // melee reach or projectile range
        public float BasicArc;         // melee cone (degrees)

        // Skills
        public string Skill1Name;
        public string Skill1Text;
        public float Skill1Cooldown;
        public string Skill2Name;
        public string Skill2Text;
        public float Skill2Cooldown;
    }

    /// <summary>A class's other weapon: its own basic attack (Arc doubles as width or burst radius where it fits).</summary>
    public sealed class WeaponDef
    {
        public string Name;
        public string BasicName;
        public string Text;
        public float Damage;
        public float Interval;
        public float Range;
        public float Arc;
    }

    public static class ClassCatalog
    {
        // The dodge (2026-10-17 rework: it used to come back every 0.9 s, so pressing it over and over beat most attacks).
        // Two charges; each comes back after DodgeRechargeTime. Untouchable only while travelling; a hit that would
        // have landed in that moment is a perfect dodge: the charge comes straight back (RunSimulation.Combat).
        public const int DodgeCharges = 2;
        public const float DodgeRechargeTime = 2.4f;
        public const float DodgeCooldown = 0.3f; // the shortest gap between two dodges
        public const float DodgeDuration = 0.22f;
        public const float DodgeSpeed = 15f;
        public const float DodgeInvulnerability = 0.22f;

        // Warrior
        public const float DashSlashDistance = 4.5f;
        public const float DashSlashDuration = 0.18f;
        public const float DashSlashDamage = 32f;
        public const float WarCryRadius = 3.5f;
        public const float WarCryAllyRadius = 7f;
        public const float ValorDuration = 6f;
        public const float ValorDamage = 0.25f;
        public const float ValorDamageReduction = 0.25f;

        // Mage
        public const float BoltSpeed = 12f;
        public const float FrostNovaRange = 7f;
        public const float FrostNovaRadius = 2.4f;
        public const float FrostNovaDelay = 0.35f;
        public const float FrostNovaDamage = 14f;
        public const float FreezeDuration = 2.5f;
        public const float HealRadius = 3.8f;
        public const float HealAmount = 40f;

        // Ranger
        public const float ArrowSpeed = 17f;
        public const float PierceArrowSpeed = 24f;
        public const float PierceArrowDamage = 42f;
        public const float MarkRange = 8f;
        public const float MarkRadius = 3f;
        public const float MarkDuration = 6f;
        public const float MarkDamageBonus = 0.35f;

        // Guardian
        public const float ShieldBashDistance = 3.2f;
        public const float ShieldBashDuration = 0.16f;
        public const float ShieldBashDamage = 18f;
        public const float ShieldBashStun = 1.2f;
        public const float SanctuaryRadius = 3.2f;
        public const float SanctuaryDuration = 5f;
        public const float SanctuaryDamageReduction = 0.4f;
        public const float SanctuaryRegen = 4f; // HP per second

        // Assassin
        public const float ShadowStepRange = 6f;
        public const float ShadowStepRadius = 1.8f;
        public const float ShadowStepDamage = 26f;
        public const float SmokeRadius = 3f;
        public const float SmokeStun = 1.6f;
        public const float SureCritDuration = 4f;

        // Alchemist
        public const float FlaskSpeed = 11f;
        public const float FlaskBurstRadius = 1.3f;
        public const float ThrowRange = 7f;
        public const float ThrowSpeed = 13f;
        public const float FirePoolRadius = 2.2f;
        public const float FirePoolDuration = 4f;
        public const float FirePoolTick = 0.5f;
        public const float FirePoolDamage = 9f; // per tick
        public const float PotionRadius = 2.6f;
        public const float PotionHeal = 35f;

        // Deathcaller
        public const float SoulBoltSpeed = 11f;
        public const float RaiseRange = 7f;
        public const int RaiseCount = 2;
        public const int ServantCap = 4;
        public const float ServantLife = 14f;
        public const float ServantDamage = 9f;
        public const float CurseRange = 8f;
        public const float CurseRadius = 2.8f;
        public const float CurseDuration = 6f;
        public const float CurseDamageBonus = 0.25f;

        // Paladin
        public const float JudgmentRange = 7.5f;
        public const float JudgmentRadius = 2f;
        public const float JudgmentDelay = 0.45f;
        public const float JudgmentDamage = 30f;
        public const float JudgmentStun = 0.9f;
        public const float JudgmentHeal = 18f;
        public const float DevotionRange = 7f;
        public const float DevotionDuration = 6f;
        public const float DevotionShare = 0.5f;
        public const float DevotionShield = 20f;

        // Synergy: heavy hits shatter frozen enemies.
        public const float ShatterMultiplier = 2.5f;
        public const float ShatterRadius = 1.6f;

        public static readonly ClassDef[] All =
        {
            new()
            {
                Id = ClassId.Warrior, Name = "戦士", Role = "前衛・連携の要", Sheet = "warrior",
                Health = 160f, MoveSpeed = 4.6f, Armor = 2f, HealthPerLevel = 8f, MightPerLevel = 0.03f,
                BasicName = "斬撃（3連撃）", BasicDamage = 15f, BasicInterval = 0.42f, BasicRange = 2.1f, BasicArc = 120f,
                Skill1Name = "突進斬り", Skill1Text = "照準方向へ突進し、通り道の敵を斬る（無敵）", Skill1Cooldown = 6f,
                Skill2Name = "鬨の声", Skill2Text = "周囲の敵を吹き飛ばして気絶させ、近くの仲間の攻撃力と防御を上げる", Skill2Cooldown = 14f,
            },
            new()
            {
                Id = ClassId.Mage, Name = "魔法使い", Role = "凍結と回復の支援", Sheet = "mage",
                Health = 105f, MoveSpeed = 4.6f, Armor = 0f, HealthPerLevel = 5f, MightPerLevel = 0.04f,
                BasicName = "魔弾", BasicDamage = 11f, BasicInterval = 0.36f, BasicRange = 9f, BasicArc = 0f,
                Skill1Name = "氷結陣", Skill1Text = "照準地点の敵を凍らせる。凍った敵は戦士の斬撃などで「砕ける」", Skill1Cooldown = 9f,
                Skill2Name = "癒しの光", Skill2Text = "自分と近くの仲間を回復し、ダウン中の仲間の復活を早める", Skill2Cooldown = 16f,
            },
            new()
            {
                Id = ClassId.Ranger, Name = "狩人", Role = "遠距離・弱点を暴く", Sheet = "ranger",
                Health = 115f, MoveSpeed = 5.0f, Armor = 0f, HealthPerLevel = 6f, MightPerLevel = 0.035f,
                BasicName = "速射", BasicDamage = 9f, BasicInterval = 0.28f, BasicRange = 11f, BasicArc = 0f,
                Skill1Name = "貫通の一矢", Skill1Text = "全ての敵を貫く強力な矢。凍った敵を砕ける", Skill1Cooldown = 7f,
                Skill2Name = "狩人の印", Skill2Text = "照準地点の敵に印を付け、全員から受けるダメージを増やす", Skill2Cooldown = 12f,
            },
            new()
            {
                Id = ClassId.Guardian, Name = "守護騎士", Role = "盾役・仲間を守る", Sheet = "guardian",
                Health = 190f, MoveSpeed = 4.3f, Armor = 4f, HealthPerLevel = 9f, MightPerLevel = 0.028f,
                BasicName = "鎚打ち", BasicDamage = 19f, BasicInterval = 0.55f, BasicRange = 1.9f, BasicArc = 110f,
                Skill1Name = "盾の突進", Skill1Text = "盾を構えて短く突進し、ぶつかった敵を気絶させる（無敵）", Skill1Cooldown = 7f,
                Skill2Name = "守りの陣", Skill2Text = "足元に5秒間の聖域を張る。中の仲間は受けるダメージが減り、傷が癒える", Skill2Cooldown = 16f,
            },
            new()
            {
                Id = ClassId.Assassin, Name = "暗殺者", Role = "素早い近接・崩れた敵を仕留める", Sheet = "assassin",
                Health = 110f, MoveSpeed = 5.2f, Armor = 0f, HealthPerLevel = 5f, MightPerLevel = 0.04f,
                BasicName = "双剣", BasicDamage = 8f, BasicInterval = 0.22f, BasicRange = 1.6f, BasicArc = 100f,
                Skill1Name = "影渡り", Skill1Text = "照準地点へ瞬時に移り、周りの敵を斬る。凍結・気絶・印の敵には必ず会心", Skill1Cooldown = 6f,
                Skill2Name = "煙玉", Skill2Text = "周りの敵を気絶させ、4秒間すべての攻撃が会心になる", Skill2Cooldown = 13f,
            },
            new()
            {
                Id = ClassId.Alchemist, Name = "錬金術師", Role = "燃やして守る・投げて癒す", Sheet = "alchemist",
                Health = 115f, MoveSpeed = 4.6f, Armor = 0f, HealthPerLevel = 5.5f, MightPerLevel = 0.04f,
                BasicName = "薬瓶投げ", BasicDamage = 12f, BasicInterval = 0.5f, BasicRange = 8f, BasicArc = 0f,
                Skill1Name = "火炎瓶", Skill1Text = "照準地点に火炎瓶を投げ、4秒間燃える床を作る（火薬樽にも火がつく）", Skill1Cooldown = 9f,
                Skill2Name = "回復薬", Skill2Text = "照準地点に回復薬を投げ、浴びた仲間を癒す。倒れた仲間の復活も早める", Skill2Cooldown = 14f,
            },
            new()
            {
                Id = ClassId.Deathcaller, Name = "死霊術師", Role = "骨の兵を率いる・呪いで弱らせる", Sheet = "deathcaller",
                Health = 108f, MoveSpeed = 4.5f, Armor = 0f, HealthPerLevel = 5f, MightPerLevel = 0.04f,
                BasicName = "魂の矢", BasicDamage = 10f, BasicInterval = 0.42f, BasicRange = 9f, BasicArc = 0f,
                Skill1Name = "骨の兵", Skill1Text = "照準地点に骨の兵を2体呼ぶ（14秒、最大4体）。兵は近くの敵に斬りかかる", Skill1Cooldown = 10f,
                Skill2Name = "死の呪い", Skill2Text = "照準地点の敵を呪う。呪われた敵は全員から受けるダメージが増え、倒れると骨の兵になる", Skill2Cooldown = 13f,
            },
            new()
            {
                Id = ClassId.Paladin, Name = "聖騎士", Role = "光で裁き・仲間の傷を肩代わり", Sheet = "paladin",
                Health = 170f, MoveSpeed = 4.4f, Armor = 3f, HealthPerLevel = 8f, MightPerLevel = 0.03f,
                BasicName = "聖剣", BasicDamage = 14f, BasicInterval = 0.46f, BasicRange = 2.05f, BasicArc = 115f,
                Skill1Name = "裁きの光", Skill1Text = "照準地点に光を落とす。敵を打って気絶させ、中の仲間を癒す", Skill1Cooldown = 8f,
                Skill2Name = "献身", Skill2Text = "近くの仲間と光の鎖でつながる（6秒）。仲間の受けるダメージの半分を肩代わりし、2人にシールド", Skill2Cooldown = 15f,
            },
        };

        /// <summary>
        /// Each class's other weapon ("weapon_alt" gear, RunSimulation.Weapons): it changes the basic attack and the
        /// character's look. By ClassId.
        /// </summary>
        public static readonly WeaponDef[] AltWeapons =
        {
            new() { Name = "大斧", BasicName = "大振り", Text = "遅いが重い一撃を広く振るう。凍った敵を砕き、敵を大きく吹き飛ばす", Damage = 34f, Interval = 0.78f, Range = 2.4f, Arc = 170f },
            new() { Name = "魔導書", BasicName = "三連の魔弾", Text = "魔弾を3方向へ同時に放つ。届く距離は短い", Damage = 6.5f, Interval = 0.46f, Range = 7f, Arc = 26f },
            new() { Name = "弩", BasicName = "重い矢", Text = "ゆっくりだが、敵を2体まで貫く重い矢を放つ", Damage = 27f, Interval = 0.72f, Range = 12f },
            new() { Name = "大槍", BasicName = "突き", Text = "まっすぐ長く突き、線の上の敵をすべて打つ", Damage = 18f, Interval = 0.5f, Range = 3.3f, Arc = 0.9f },
            new() { Name = "投げ短剣", BasicName = "投げ短剣", Text = "短剣を投げて離れて戦う。凍結・気絶・印の敵には必ず会心", Damage = 7.5f, Interval = 0.26f, Range = 7.5f },
            new() { Name = "爆薬瓶", BasicName = "爆薬瓶", Text = "ゆっくりだが、大きく爆ぜる瓶を投げる。凍った敵を砕く", Damage = 21f, Interval = 0.85f, Range = 8f, Arc = 2.2f },
            new() { Name = "大鎌", BasicName = "薙ぎ払い", Text = "近くを大きく薙ぎ払い、当たった敵の数だけ少し回復する", Damage = 15f, Interval = 0.6f, Range = 2.4f, Arc = 150f },
            new() { Name = "大剣", BasicName = "大振り", Text = "遅いが重い一撃。当てるたびに少しのシールドを得る", Damage = 33f, Interval = 0.8f, Range = 2.3f, Arc = 160f },
        };

        public static WeaponDef AltWeapon(ClassId id) => AltWeapons[(int)id];

        /// <summary>The basic attack's name with this weapon kind (0 usual, 1 the other weapon).</summary>
        public static string BasicName(ClassId id, int weaponKind) => weaponKind == 1 ? AltWeapon(id).BasicName : Get(id).BasicName;

        /// <summary>Frame of an ability in the icons_skill sheet: the first six classes, then the dodge and the bag, then the rest.</summary>
        public static int SkillIcon(ClassId id, int slot) => (int)id < 6 ? (int)id * 3 + slot : 20 + ((int)id - 6) * 3 + slot;
        public const int DodgeIcon = 18;
        public const int BagIcon = 19;

        public static ClassDef Get(ClassId id) => All[(int)id];

        /// <summary>The three classes everyone starts with; the rest are unlocked by requests.</summary>
        public static bool IsStarter(ClassId id) => id is ClassId.Warrior or ClassId.Mage or ClassId.Ranger;

        /// <summary>Unlock id kept in the profile ("class_guardian" ...).</summary>
        public static string UnlockId(ClassId id) => "class_" + id.ToString().ToLowerInvariant();

        public static string AbilityName(ClassId id, AbilitySlot slot)
        {
            var c = Get(id);
            return slot switch
            {
                AbilitySlot.Basic => c.BasicName,
                AbilitySlot.Skill1 => c.Skill1Name,
                AbilitySlot.Skill2 => c.Skill2Name,
                _ => "回避",
            };
        }
    }
}
