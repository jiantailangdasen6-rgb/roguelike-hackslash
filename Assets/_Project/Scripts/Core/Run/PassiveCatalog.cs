namespace Game.Core
{
    public enum PassiveId : byte
    {
        Vitality,
        Swiftness,
        Strength,
        Haste,
        Reach,
        Magnet,
        Guard,
        Recovery,
    }

    /// <summary>A level-up passive: each level adds the same modifier again.</summary>
    public sealed class PassiveDef
    {
        public readonly PassiveId Id;
        public readonly string Name;
        public readonly StatModifier PerLevel;

        public PassiveDef(PassiveId id, string name, StatModifier perLevel)
        {
            Id = id;
            Name = name;
            PerLevel = perLevel;
        }
    }

    public static class PassiveCatalog
    {
        public const int MaxLevel = 5;
        public const int MaxPassiveSlots = 6;

        public static readonly PassiveDef[] All =
        {
            new(PassiveId.Vitality, "頑健の心臓", new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, 0.15f)),
            new(PassiveId.Swiftness, "疾風の靴", new StatModifier(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.08f)),
            new(PassiveId.Strength, "剛力の籠手", new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.10f)),
            new(PassiveId.Haste, "時の砂", new StatModifier(StatType.Cooldown, ModifierKind.PercentAdd, -0.07f)),
            new(PassiveId.Reach, "巨人の腕輪", new StatModifier(StatType.Area, ModifierKind.PercentAdd, 0.10f)),
            new(PassiveId.Magnet, "磁石の石", new StatModifier(StatType.PickupRange, ModifierKind.PercentAdd, 0.25f)),
            new(PassiveId.Guard, "守りの盾", new StatModifier(StatType.Armor, ModifierKind.Flat, 1f)),
            new(PassiveId.Recovery, "癒しの苔", new StatModifier(StatType.Regen, ModifierKind.Flat, 0.4f)),
        };

        public static PassiveDef Get(PassiveId id) => All[(int)id];
    }
}
