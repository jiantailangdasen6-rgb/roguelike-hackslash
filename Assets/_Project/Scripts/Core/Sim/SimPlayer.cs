using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Host-side state of one player in the run. Position and input are fed in from the network each tick.</summary>
    public sealed class SimPlayer
    {
        public const float BodyRadius = 0.38f;

        private static readonly object GrowthSource = new();
        private static readonly object LoadoutSource = new();
        private static readonly object ValorSource = new();
        private static readonly object MercySource = new();
        private static readonly object BloodlustSource = new();
        private readonly object _vigorSource = new();
        private readonly object[] _perkSources = new object[PerkCatalog.Count];

        /// <summary>Network id; changes when the same player reconnects (see RunSimulation.JoinMidRun).</summary>
        public ulong ClientId { get; internal set; }
        public readonly StatSheet Stats = new();
        public readonly int[] PerkLevels = new int[PerkCatalog.Count];
        public readonly HashSet<PerkId> UnlockedPerks = new();

        public ClassDef Class { get; private set; }
        public float HealthGrowth { get; private set; }
        public float MightGrowth { get; private set; }
        public bool HasStartPerk { get; private set; }
        public bool Phoenix { get; private set; }
        public float HealOnKill { get; private set; }
        public int Level { get; private set; } = 1;
        /// <summary>Max HP boosts from "vigor" reward doors this run.</summary>
        public int VigorStacks { get; private set; }

        public Vector2 Position;
        public Vector2 Aim = Vector2.right;
        public float Hp;
        public float Shield;
        public bool Downed;
        public float ReviveProgress;
        public float InvulnerableTimer;
        public bool SecondWindUsed;
        public bool PhoenixUsed;

        // Input edge detection
        public PlayerInput Input;
        public byte LastSkill1;
        public byte LastSkill2;
        public byte LastDodge;

        // Abilities
        public float BasicTimer;
        public int Combo;
        public float ComboResetTimer;
        public float Skill1Cooldown;
        public float Skill2Cooldown;
        public float DodgeCooldown;
        public float DashSlashTimer;   // >0 while the dash-slash travels
        public Vector2 DashSlashFrom;
        public Vector2 DashSlashDir;
        public readonly List<int> DashSlashHits = new();
        public float EvasiveTimer;

        // Buffs
        public float ValorTimer;
        public float MercyTimer;
        public int BloodlustStacks;
        public float BloodlustTimer;

        /// <summary>Perk choices waiting for this player (null when none).</summary>
        public PerkOption[] PendingOffer;

        public float MaxHp => Stats.Get(StatType.MaxHealth);
        public bool Alive => !Downed;
        public ClassId ClassId => Class.Id;

        public SimPlayer(ulong clientId, Vector2 position, Loadout loadout = null)
        {
            ClientId = clientId;
            Position = position;
            for (int i = 0; i < _perkSources.Length; i++) _perkSources[i] = new object();
            Configure(loadout ?? new Loadout());
        }

        /// <summary>Applies class base stats, mastery (growth, stats, unlocked perks) and trinkets.</summary>
        public void Configure(Loadout loadout)
        {
            Class = ClassCatalog.Get(loadout.classId);
            Stats.SetBase(StatType.MaxHealth, Class.Health);
            Stats.SetBase(StatType.MoveSpeed, Class.MoveSpeed);
            Stats.SetBase(StatType.Might, 1f);
            Stats.SetBase(StatType.Cooldown, 1f);
            Stats.SetBase(StatType.Area, 1f);
            Stats.SetBase(StatType.PickupRange, 2.2f);
            Stats.SetBase(StatType.Armor, Class.Armor);
            Stats.SetBase(StatType.Regen, 0f);
            Stats.SetBase(StatType.Amount, 0f);
            Stats.SetBase(StatType.AttackSpeed, 1f);
            Stats.SetBase(StatType.CritChance, 0.05f);
            Stats.SetBase(StatType.ReviveSpeed, 1f);
            Stats.SetBase(StatType.DamageTaken, 1f);

            HealthGrowth = Class.HealthPerLevel;
            MightGrowth = Class.MightPerLevel;
            HasStartPerk = false;
            Phoenix = false;
            HealOnKill = 0f;
            UnlockedPerks.Clear();
            Stats.RemoveModifiersFrom(LoadoutSource);

            foreach (var id in loadout.mastery)
            {
                var node = MetaCatalog.GetNode(id);
                if (node == null || node.Class != Class.Id) continue;
                switch (node.Kind)
                {
                    case MasteryKind.UnlockPerk: UnlockedPerks.Add(node.Perk); break;
                    case MasteryKind.HealthGrowth: HealthGrowth += node.Value; break;
                    case MasteryKind.MightGrowth: MightGrowth += node.Value; break;
                    case MasteryKind.StartPerk: HasStartPerk = true; break;
                    case MasteryKind.Stat: Stats.AddModifier(node.Modifier, LoadoutSource); break;
                }
            }

            foreach (var id in loadout.trinkets)
            {
                var trinket = MetaCatalog.GetTrinket(id);
                if (trinket == null) continue;
                foreach (var mod in trinket.Modifiers) Stats.AddModifier(mod, LoadoutSource);
                Phoenix |= trinket.Phoenix;
                HealOnKill += trinket.HealOnKill;
            }

            SetLevel(Level);
            Hp = MaxHp;
        }

        /// <summary>Growth curve: stats rise with the team level.</summary>
        public void SetLevel(int level)
        {
            float before = MaxHp;
            Level = level;
            Stats.RemoveModifiersFrom(GrowthSource);
            Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.Flat, HealthGrowth * (level - 1)), GrowthSource);
            Stats.AddModifier(new StatModifier(StatType.Might, ModifierKind.PercentAdd, MightGrowth * (level - 1)), GrowthSource);
            // Gaining max HP also heals by the same amount.
            if (MaxHp > before && !Downed) Hp += MaxHp - before;
            Hp = Mathf.Min(Hp, MaxHp);
        }

        public int PerkLevel(PerkId id) => PerkLevels[(int)id];

        public void AddPerk(PerkId id)
        {
            var def = PerkCatalog.Get(id);
            int level = Mathf.Min(def.MaxLevel, PerkLevels[(int)id] + 1);
            float before = MaxHp;
            PerkLevels[(int)id] = level;
            if (def.PerLevel.HasValue)
            {
                var src = _perkSources[(int)id];
                Stats.RemoveModifiersFrom(src);
                var m = def.PerLevel.Value;
                Stats.AddModifier(new StatModifier(m.Stat, m.Kind, m.Value * level), src);
            }
            if (MaxHp > before && !Downed) Hp += MaxHp - before;
        }

        public void AddVigor(int stacks)
        {
            VigorStacks += stacks;
            Stats.RemoveModifiersFrom(_vigorSource);
            Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, RunSimulation.VigorPerStack * VigorStacks), _vigorSource);
            Hp = Mathf.Min(Hp, MaxHp);
        }

        public bool CanTakePerk(PerkDef def) =>
            (!def.Class.HasValue || def.Class.Value == Class.Id)
            && (!def.Locked || UnlockedPerks.Contains(def.Id))
            && PerkLevels[(int)def.Id] < def.MaxLevel;

        public void Heal(float amount)
        {
            if (!Downed) Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        // ------------------------------------------------------------------ Buffs

        public void GrantValor(float duration)
        {
            ValorTimer = Mathf.Max(ValorTimer, duration);
            Stats.RemoveModifiersFrom(ValorSource);
            Stats.AddModifier(new StatModifier(StatType.Might, ModifierKind.PercentAdd, ClassCatalog.ValorDamage), ValorSource);
            Stats.AddModifier(new StatModifier(StatType.DamageTaken, ModifierKind.PercentAdd, -ClassCatalog.ValorDamageReduction), ValorSource);
        }

        public void GrantMercy(float duration, float bonus)
        {
            MercyTimer = Mathf.Max(MercyTimer, duration);
            Stats.RemoveModifiersFrom(MercySource);
            Stats.AddModifier(new StatModifier(StatType.Might, ModifierKind.PercentAdd, bonus), MercySource);
        }

        public void AddBloodlust()
        {
            BloodlustStacks = Mathf.Min(10, BloodlustStacks + 1);
            BloodlustTimer = 3f;
            Stats.RemoveModifiersFrom(BloodlustSource);
            Stats.AddModifier(new StatModifier(StatType.Might, ModifierKind.PercentAdd, 0.03f * BloodlustStacks), BloodlustSource);
        }

        public void TickBuffs(float dt)
        {
            if (ValorTimer > 0f && (ValorTimer -= dt) <= 0f) Stats.RemoveModifiersFrom(ValorSource);
            if (MercyTimer > 0f && (MercyTimer -= dt) <= 0f) Stats.RemoveModifiersFrom(MercySource);
            if (BloodlustTimer > 0f && (BloodlustTimer -= dt) <= 0f)
            {
                BloodlustStacks = 0;
                Stats.RemoveModifiersFrom(BloodlustSource);
            }
            EvasiveTimer -= dt;
        }

        public float CooldownMultiplier => Mathf.Max(0.3f, Stats.Get(StatType.Cooldown));
    }

    /// <summary>One option of a level-up: a perk and the level it would reach. Perk id 255 = heal (fallback).</summary>
    public readonly struct PerkOption
    {
        public const byte HealId = 255;

        public readonly byte Perk;
        public readonly byte Level;

        public PerkOption(byte perk, byte level)
        {
            Perk = perk;
            Level = level;
        }

        public bool IsHeal => Perk == HealId;

        public static ulong Pack(IReadOnlyList<PerkOption> options)
        {
            ulong packed = (ulong)Mathf.Min(options.Count, 3);
            for (int i = 0; i < options.Count && i < 3; i++)
                packed |= ((ulong)options[i].Perk << 8 | options[i].Level) << (2 + i * 16);
            return packed;
        }

        public static PerkOption[] Unpack(ulong packed)
        {
            int count = (int)(packed & 0x3);
            var result = new PerkOption[count];
            for (int i = 0; i < count; i++)
            {
                ulong v = (packed >> (2 + i * 16)) & 0xFFFF;
                result[i] = new PerkOption((byte)(v >> 8), (byte)(v & 0xFF));
            }
            return result;
        }

        public (string Title, string Body) Describe()
        {
            if (IsHeal) return ("休息", "HPを40%回復する");
            var def = PerkCatalog.Get((PerkId)Perk);
            string title = def.MaxLevel > 1 ? $"{def.Name} Lv{Level}" : def.Name;
            if (Level == 1 && def.MaxLevel > 1) title += "（新規）";
            return (title, def.Describe(Level));
        }
    }

    public static class PerkRoller
    {
        public const int OptionCount = 3;

        public static PerkOption[] Roll(GameRandom rng, SimPlayer player)
        {
            var candidates = new List<(PerkOption Option, int Weight)>();
            foreach (var def in PerkCatalog.All)
            {
                if (!player.CanTakePerk(def)) continue;
                int weight = def.Class.HasValue ? 12 : 8;
                if (player.PerkLevel(def.Id) > 0) weight += 4; // favour deepening a build
                candidates.Add((new PerkOption((byte)def.Id, (byte)(player.PerkLevel(def.Id) + 1)), weight));
            }

            var result = new List<PerkOption>(OptionCount);
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

            if (result.Count == 0) result.Add(new PerkOption(PerkOption.HealId, 0));
            return result.ToArray();
        }

        /// <summary>Up to three perks the player already has, each one level higher. Null when none can grow.</summary>
        public static PerkOption[] RollUpgrade(GameRandom rng, SimPlayer player)
        {
            var owned = new List<PerkOption>();
            foreach (var def in PerkCatalog.All)
            {
                int level = player.PerkLevel(def.Id);
                if (level > 0 && level < def.MaxLevel) owned.Add(new PerkOption((byte)def.Id, (byte)(level + 1)));
            }
            if (owned.Count == 0) return null;
            for (int i = owned.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (owned[i], owned[j]) = (owned[j], owned[i]);
            }
            if (owned.Count > OptionCount) owned.RemoveRange(OptionCount, owned.Count - OptionCount);
            return owned.ToArray();
        }

        public static void Apply(SimPlayer player, PerkOption option)
        {
            if (option.IsHeal) player.Heal(player.MaxHp * 0.4f);
            else player.AddPerk((PerkId)option.Perk);
        }
    }
}
