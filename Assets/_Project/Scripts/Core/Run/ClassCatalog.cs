namespace Game.Core
{
    public enum ClassId : byte
    {
        Warrior,
        Mage,
        Ranger,
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

    public static class ClassCatalog
    {
        public const float DodgeCooldown = 0.9f;
        public const float DodgeDuration = 0.22f;
        public const float DodgeSpeed = 15f;
        public const float DodgeInvulnerability = 0.3f;

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
        };

        public static ClassDef Get(ClassId id) => All[(int)id];

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
