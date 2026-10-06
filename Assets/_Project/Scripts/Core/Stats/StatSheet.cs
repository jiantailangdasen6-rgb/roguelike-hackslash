using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum StatType : byte
    {
        MaxHealth,
        MoveSpeed,
        /// <summary>Damage multiplier (base 1).</summary>
        Might,
        /// <summary>Cooldown multiplier (base 1; lower is faster).</summary>
        Cooldown,
        /// <summary>Area multiplier (base 1).</summary>
        Area,
        PickupRange,
        /// <summary>Flat damage reduction per hit.</summary>
        Armor,
        /// <summary>HP regenerated per second.</summary>
        Regen,
        /// <summary>Extra projectiles.</summary>
        Amount,
    }

    public enum ModifierKind : byte
    {
        Flat,
        PercentAdd,
        PercentMult,
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public ModifierKind Kind;
        public float Value;

        public StatModifier(StatType stat, ModifierKind kind, float value)
        {
            Stat = stat;
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>
    /// Base values plus modifiers from any source (equipment, passives, buffs).
    /// Final = (base + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult)
    /// </summary>
    public sealed class StatSheet
    {
        public static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length;

        private readonly float[] _base = new float[StatCount];
        private readonly float[] _cache = new float[StatCount];
        private readonly List<(object Source, StatModifier Mod)> _modifiers = new();
        private bool _dirty = true;

        public void SetBase(StatType stat, float value)
        {
            _base[(int)stat] = value;
            _dirty = true;
        }

        public void AddModifier(StatModifier modifier, object source)
        {
            _modifiers.Add((source, modifier));
            _dirty = true;
        }

        public void RemoveModifiersFrom(object source)
        {
            if (_modifiers.RemoveAll(m => Equals(m.Source, source)) > 0) _dirty = true;
        }

        public float Get(StatType stat)
        {
            if (_dirty) Recalculate();
            return _cache[(int)stat];
        }

        private void Recalculate()
        {
            Span<float> flat = stackalloc float[StatCount];
            Span<float> percentAdd = stackalloc float[StatCount];
            Span<float> percentMult = stackalloc float[StatCount];
            percentMult.Fill(1f);

            foreach (var (_, mod) in _modifiers)
            {
                int i = (int)mod.Stat;
                switch (mod.Kind)
                {
                    case ModifierKind.Flat: flat[i] += mod.Value; break;
                    case ModifierKind.PercentAdd: percentAdd[i] += mod.Value; break;
                    case ModifierKind.PercentMult: percentMult[i] *= 1f + mod.Value; break;
                }
            }

            for (int i = 0; i < StatCount; i++)
                _cache[i] = (_base[i] + flat[i]) * (1f + percentAdd[i]) * percentMult[i];
            _dirty = false;
        }
    }
}
