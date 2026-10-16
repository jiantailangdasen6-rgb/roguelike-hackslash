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
        /// <summary>Camp facilities that work during the run (CampUpgrades): better loaners, more healing on clearing a room.</summary>
        public int ForgeLevel { get; private set; }
        public int ApothecaryLevel { get; private set; }
        public int Level { get; private set; } = 1;
        /// <summary>Max HP boosts from "vigor" reward doors this run.</summary>
        public int VigorStacks { get; private set; }

        public Vector2 Position;
        public Vector2 Aim = Vector2.right;
        public float Hp;
        public float Shield;
        /// <summary>When this player last used a skill (sim time), and when it may take part in a union again (RunSimulation.Unions).</summary>
        public float LastSkillAt = -99f;
        public float UnionReadyAt;
        public bool Downed;
        /// <summary>Where the body lies while downed; the player moves on as a ghost (RunSimulation.Ghosts).</summary>
        public Vector2 BodyPosition;
        /// <summary>Where teammates go to help: the body while downed.</summary>
        public Vector2 FallenAt => Downed ? BodyPosition : Position;
        public float GhostTimer;
        public float ReviveProgress;
        public float InvulnerableTimer;
        public bool SecondWindUsed;
        /// <summary>Healing from the rest room's fire not yet shown as a number.</summary>
        public float HealedByFire;
        public bool PhoenixUsed;
        /// <summary>Id of the bone cage holding this player (0 = free) and how long until it crushes them.</summary>
        public int CagedBy;
        public float CageTimer;
        /// <summary>Map room the player stands in (-1 before the first step).</summary>
        public int RoomId = -1;
        /// <summary>Time spent on a pedestal (it works after a short hold).</summary>
        public float PedestalHold;
        /// <summary>Curses taken at curse altars (stacks per kind; personal).</summary>
        public readonly int[] CurseStacks = new int[RewardCatalog.CurseKinds];

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
        public float ShadowStepTimer;  // assassin: >0 until the arrival slash lands (the owner moves itself)
        public float SureCritTimer;    // assassin: smoke bomb, every blow is critical
        public bool BashStuns;         // guardian: the dash in progress is a shield bash
        public bool BashAflame;        // guardian: the bash went through burning ground (combo)

        // Buffs
        public float ValorTimer;
        public float MercyTimer;
        public int BloodlustStacks;
        public float BloodlustTimer;

        /// <summary>The perk choice on this player's screen right now (null when none).</summary>
        public PerkOption[] PendingOffer;
        /// <summary>What <see cref="PendingOffer"/> was given for, and how long until the host picks the first option.</summary>
        public ChoiceKind OfferKind;
        public float OfferTimer;
        /// <summary>Picks earned but not shown yet: level-ups bank up during a fight and are chosen between rooms.</summary>
        public readonly Queue<ChoiceKind> StockedChoices = new();
        public int PicksLeft => StockedChoices.Count + (PendingOffer != null ? 1 : 0);

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

            // The camp's facilities (CampUpgrades), grown at home with soul shards.
            int hearth = CampUpgrades.Level(loadout.camp, CampUpgrade.Hearth);
            if (hearth > 0) Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, CampUpgrades.HearthHealth * hearth), LoadoutSource);
            ForgeLevel = CampUpgrades.Level(loadout.camp, CampUpgrade.Forge);
            ApothecaryLevel = CampUpgrades.Level(loadout.camp, CampUpgrade.Apothecary);
            if (CampUpgrades.Level(loadout.camp, CampUpgrade.Altar) > 0) HasStartPerk = true;

            foreach (var id in loadout.trinkets)
            {
                var trinket = MetaCatalog.GetTrinket(id);
                if (trinket == null) continue;
                foreach (var mod in trinket.Modifiers) Stats.AddModifier(mod, LoadoutSource);
                Phoenix |= trinket.Phoenix;
                HealOnKill += trinket.HealOnKill;
            }

            // Gear brought from home (worn set). A weapon takes the shape of whatever class carries it.
            for (int s = 0; s < SlotCount; s++) Equipped[s] = null;
            if (loadout.gear != null)
                foreach (var hex in loadout.gear)
                {
                    var item = GearVault.Unpack(hex);
                    if (item == null || item.loaner || Equipped[(int)item.Slot] != null) continue;
                    if (item.Slot == EquipSlot.Weapon) item.classId = Class.Id;
                    Equipped[(int)item.Slot] = item;
                }
            for (int s = 0; s < SlotCount; s++) ApplyGear(s);

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

        /// <summary>Offerings made to the wandering spirit: each takes a tenth of max HP for the rest of the run.</summary>
        public int Offerings { get; private set; }
        private readonly object _offeringSource = new();

        public void AddOffering()
        {
            Offerings++;
            Stats.RemoveModifiersFrom(_offeringSource);
            Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, -RunSimulation.SpiritOffering * Offerings), _offeringSource);
            Hp = Mathf.Clamp(Hp, Downed ? 0f : 1f, MaxHp);
        }

        public void AddVigor(int stacks)
        {
            VigorStacks += stacks;
            Stats.RemoveModifiersFrom(_vigorSource);
            Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, RunSimulation.VigorPerStack * VigorStacks), _vigorSource);
            Hp = Mathf.Min(Hp, MaxHp);
        }

        /// <summary>Blessings of a god this player holds (two awaken its favour).</summary>
        public int GodPieces(GodId god) => GodCatalog.Pieces(PerkLevels, god);
        public bool Favored(GodId god) => GodPieces(god) >= GodCatalog.FavorPieces;
        public bool Has(PerkId id) => PerkLevels[(int)id] > 0;

        // Blessing counters (RunSimulation.Gods)
        public int StormHits;
        public float FrostWardTimer;
        public float VeilTimer;
        public bool RiseUsed;
        public float RiseTimer;

        // Paladin: devotion binds one teammate (two with the "天秤の" sword) by client id + 1.
        public float DevotionTimer;
        public ulong DevotedTo;
        public ulong DevotedTo2;

        public bool CanTakePerk(PerkDef def) =>
            (!def.Class.HasValue || def.Class.Value == Class.Id)
            && (!def.Locked || UnlockedPerks.Contains(def.Id))
            && PerkLevels[(int)def.Id] < def.MaxLevel;

        public void Heal(float amount)
        {
            if (!Downed) Hp = Mathf.Min(MaxHp, Hp + amount * HealFactor);
        }

        // ------------------------------------------------------------------ Curses (curse doors)

        private readonly object _curseSource = new();
        /// <summary>Thirst: healing is weaker.</summary>
        public float HealFactor { get; private set; } = 1f;
        /// <summary>Weariness: the dodge recovers slower.</summary>
        public float DodgeFactor { get; private set; } = 1f;

        private static readonly int[] NoCurses = new int[RewardCatalog.CurseKinds];
        private IReadOnlyList<int> _curses = NoCurses;
        private DifficultyDef _difficulty;

        public void SetCurses(IReadOnlyList<int> stacks)
        {
            _curses = stacks;
            Stats.RemoveModifiersFrom(_curseSource);
            int frail = stacks[(int)CurseKind.Frailty], hunted = stacks[(int)CurseKind.Hunted];
            if (frail > 0) Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, -RewardCatalog.FrailtyPerStack * frail), _curseSource);
            if (hunted > 0) Stats.AddModifier(new StatModifier(StatType.DamageTaken, ModifierKind.PercentAdd, RewardCatalog.HuntedPerStack * hunted), _curseSource);
            HealFactor = Mathf.Pow(RewardCatalog.ThirstFactor, stacks[(int)CurseKind.Thirst]);
            DodgeFactor = 1f + RewardCatalog.WearinessPerStack * stacks[(int)CurseKind.Weariness];

            // The difficulty's handicaps ride along with the curses.
            if (_difficulty != null)
            {
                if (_difficulty.Has(DifficultyMod.Frailty))
                    Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierKind.PercentAdd, -DifficultyCatalog.FrailtyFactor), _curseSource);
                if (_difficulty.Has(DifficultyMod.Thirst)) HealFactor *= DifficultyCatalog.ThirstFactor;
                if (_difficulty.Has(DifficultyMod.Weariness)) DodgeFactor *= DifficultyCatalog.WearinessFactor;
            }
            Hp = Mathf.Min(Hp, MaxHp);
        }

        /// <summary>The run's difficulty: its handicaps (thirst, frailty, weariness) apply to this player.</summary>
        public void SetDifficulty(DifficultyDef def)
        {
            bool fresh = Hp >= MaxHp - 0.01f;
            _difficulty = def;
            SetCurses(_curses);
            if (fresh) Hp = MaxHp;
        }

        // ------------------------------------------------------------------ Buffs

        public void GrantValor(float duration)
        {
            ValorTimer = Mathf.Max(ValorTimer, duration);
            Stats.RemoveModifiersFrom(ValorSource);
            Stats.AddModifier(new StatModifier(StatType.Might, ModifierKind.PercentAdd, ClassCatalog.ValorDamage), ValorSource);
            Stats.AddModifier(new StatModifier(StatType.DamageTaken, ModifierKind.PercentAdd, -ClassCatalog.ValorDamageReduction), ValorSource);
        }

        private readonly object _drumSource = new();
        public float DrumTimer;

        /// <summary>Legendary "鬨の": allies who hear the war cry attack faster for a while.</summary>
        public void GrantDrum(float duration)
        {
            DrumTimer = Mathf.Max(DrumTimer, duration);
            Stats.RemoveModifiersFrom(_drumSource);
            Stats.AddModifier(new StatModifier(StatType.AttackSpeed, ModifierKind.PercentAdd, 0.2f), _drumSource);
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

        // ------------------------------------------------------------------ Gear (personal loot)

        public const int BagSize = 12;
        public const int SlotCount = 6; // weapon, armour, amulet, ring, and a rune for each skill

        /// <summary>Picked-up items waiting to be equipped (between rooms) or given to a teammate.</summary>
        public readonly List<ItemInstance> Bag = new();
        /// <summary>Indexed by <see cref="EquipSlot"/>.</summary>
        public readonly ItemInstance[] Equipped = new ItemInstance[SlotCount];
        private readonly object[] _gearSources = { new(), new(), new(), new(), new(), new() };

        /// <summary>The kind of weapon worn: 0 the class's usual one, 1 its other one (WeaponCatalog, "weapon_alt").</summary>
        public int WeaponKind => Equipped[(int)EquipSlot.Weapon]?.baseId == "weapon_alt" ? 1 : 0;

        /// <summary>The rune set into a skill (1 or 2) and how strongly it works.</summary>
        public (RuneId Rune, float Power) RuneIn(int skill)
        {
            var item = Equipped[skill == 1 ? (int)EquipSlot.Rune1 : (int)EquipSlot.Rune2];
            return item == null ? (RuneId.None, 0f) : (item.rune, ItemCatalog.RunePower(item.rarity));
        }
        /// <summary>Gear "迅雷": the first hit within this window after a dodge deals extra damage.</summary>
        public float DodgeStrikeTimer;

        public bool BagFull => Bag.Count >= BagSize;

        public bool HasLegendary(LegendaryId id)
        {
            foreach (var item in Equipped) if (item != null && item.legendary == id) return true;
            return false;
        }

        public bool AnyLegendaryEquipped
        {
            get
            {
                foreach (var item in Equipped) if (item != null && item.rarity == Rarity.Legendary) return true;
                return false;
            }
        }

        /// <summary>Puts the bag item on; whatever was in that slot goes back into the bag in its place.</summary>
        public bool Equip(int bagIndex)
        {
            if (bagIndex < 0 || bagIndex >= Bag.Count) return false;
            var item = Bag[bagIndex];
            int slot = (int)item.Slot;
            var old = Equipped[slot];
            Bag.RemoveAt(bagIndex);
            if (old != null && !old.loaner) Bag.Insert(bagIndex, old); // a loaner is simply handed back
            Equipped[slot] = item;
            ApplyGear(slot);
            return true;
        }

        /// <summary>Puts an item straight into its (empty) slot - loaners at the start of a run.</summary>
        public void Wear(ItemInstance item)
        {
            int slot = (int)item.Slot;
            if (Equipped[slot] != null) return;
            Equipped[slot] = item;
            ApplyGear(slot);
        }

        public bool Unequip(EquipSlot slot)
        {
            var item = Equipped[(int)slot];
            if (item == null || BagFull) return false;
            Equipped[(int)slot] = null;
            Bag.Add(item);
            ApplyGear((int)slot);
            return true;
        }

        /// <summary>Gear above this item level works at this level (set per room; see RunSimulation.GearLevelCap).</summary>
        public int GearLevelCap { get; private set; } = int.MaxValue;

        public void SetGearLevelCap(int cap)
        {
            if (cap == GearLevelCap) return;
            GearLevelCap = cap;
            for (int s = 0; s < SlotCount; s++) ApplyGear(s);
        }

        private void ApplyGear(int slot)
        {
            float before = MaxHp;
            Stats.RemoveModifiersFrom(_gearSources[slot]);
            var item = Equipped[slot];
            if (item != null)
                foreach (var mod in item.GetModifiers(GearLevelCap)) Stats.AddModifier(mod, _gearSources[slot]);
            // More max HP heals by the difference (like levelling); less never kills.
            if (MaxHp > before && !Downed) Hp += MaxHp - before;
            Hp = Mathf.Clamp(Hp, Downed ? 0f : 1f, MaxHp);
        }

        public void TickBuffs(float dt)
        {
            if (DodgeStrikeTimer > 0f) DodgeStrikeTimer -= dt;
            if (DrumTimer > 0f && (DrumTimer -= dt) <= 0f) Stats.RemoveModifiersFrom(_drumSource);
            if (ValorTimer > 0f && (ValorTimer -= dt) <= 0f) Stats.RemoveModifiersFrom(ValorSource);
            if (MercyTimer > 0f && (MercyTimer -= dt) <= 0f) Stats.RemoveModifiersFrom(MercySource);
            if (BloodlustTimer > 0f && (BloodlustTimer -= dt) <= 0f)
            {
                BloodlustStacks = 0;
                Stats.RemoveModifiersFrom(BloodlustSource);
            }
            EvasiveTimer -= dt;
            if (SureCritTimer > 0f) SureCritTimer -= dt;
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

        // The merchant's wares that aren't perks.
        public const byte ShopTreasureId = 254;
        public const byte ShopVigorId = 253;
        public bool IsShopTreasure => Perk == ShopTreasureId;
        public bool IsShopVigor => Perk == ShopVigorId;
        public bool IsPerk => !IsHeal && !IsShopTreasure && !IsShopVigor;

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
            if (IsShopTreasure) return ("宝物", "希少以上の装備を1つ");
            if (IsShopVigor) return ("生命の霊薬", $"最大HP +{RunSimulation.VigorPerStack * 100f:0}%・全回復");
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
                if (def.God.HasValue || !player.CanTakePerk(def)) continue;
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

        /// <summary>
        /// Gods' blessings: up to three, each from a different god. Gods with one blessing taken already are likelier
        /// (their favour is one pick away). Null when every blessing is taken.
        /// </summary>
        public static PerkOption[] RollBlessing(GameRandom rng, SimPlayer player)
        {
            var gods = new List<(GodId God, int Weight)>();
            foreach (var god in GodCatalog.All)
            {
                bool open = false;
                foreach (var def in GodCatalog.Blessings(god.Id)) open |= player.CanTakePerk(def);
                if (!open) continue;
                int pieces = player.GodPieces(god.Id);
                gods.Add((god.Id, pieces == GodCatalog.FavorPieces - 1 ? 30 : 10));
            }
            var result = new List<PerkOption>(OptionCount);
            while (result.Count < OptionCount && gods.Count > 0)
            {
                int total = 0;
                foreach (var g in gods) total += g.Weight;
                int pick = rng.Range(0, total), index = 0;
                while (pick >= gods[index].Weight)
                {
                    pick -= gods[index].Weight;
                    index++;
                }
                var open = new List<PerkDef>();
                foreach (var def in GodCatalog.Blessings(gods[index].God)) if (player.CanTakePerk(def)) open.Add(def);
                var chosen = open[rng.Range(0, open.Count)];
                result.Add(new PerkOption((byte)chosen.Id, 1));
                gods.RemoveAt(index);
            }
            return result.Count > 0 ? result.ToArray() : null;
        }

        /// <summary>The merchant: two perks and either a piece of treasure or an elixir of life.</summary>
        public static PerkOption[] RollShop(GameRandom rng, SimPlayer player)
        {
            var perks = Roll(rng, player);
            var wares = new List<PerkOption>(OptionCount);
            for (int i = 0; i < perks.Length && wares.Count < OptionCount - 1; i++)
                if (perks[i].IsPerk) wares.Add(perks[i]);
            wares.Add(new PerkOption(rng.Chance(0.5f) ? PerkOption.ShopTreasureId : PerkOption.ShopVigorId, 1));
            return wares.ToArray();
        }

        public static void Apply(SimPlayer player, PerkOption option)
        {
            if (option.IsHeal) player.Heal(player.MaxHp * 0.4f);
            else player.AddPerk((PerkId)option.Perk);
        }
    }
}
