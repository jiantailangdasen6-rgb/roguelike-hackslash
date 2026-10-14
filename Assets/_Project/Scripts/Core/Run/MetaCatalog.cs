using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    public enum MasteryKind : byte
    {
        UnlockPerk,
        HealthGrowth,  // +Value max HP per level
        MightGrowth,   // +Value damage per level
        StartPerk,     // a free perk choice when the run starts
        Stat,          // permanent stat modifier
    }

    /// <summary>Out-of-run growth for one class, bought with soul shards.</summary>
    public sealed class MasteryNode
    {
        public string Id;
        public ClassId Class;
        public string Name;
        public string Text;
        public int Cost;
        public MasteryKind Kind;
        public float Value;
        public PerkId Perk;
        public StatModifier Modifier;
    }

    /// <summary>A fixed-effect charm chosen before a run (two slots).</summary>
    public sealed class TrinketDef
    {
        public string Id;
        public string Name;
        public string Text;
        public int Cost; // 0 = owned from the start
        public StatModifier[] Modifiers = Array.Empty<StatModifier>();
        public bool Phoenix;      // auto-revive once per run
        public float HealOnKill;
    }

    public static class MetaCatalog
    {
        public const int TrinketSlots = 2;

        private static StatModifier M(StatType s, ModifierKind k, float v) => new(s, k, v);

        public static readonly MasteryNode[] Mastery =
        {
            // Warrior
            new() { Id = "w_hp", Class = ClassId.Warrior, Name = "鍛えた肉体", Text = "レベルごとの最大HP成長 +4", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 4f },
            new() { Id = "w_might", Class = ClassId.Warrior, Name = "剣技の研鑽", Text = "レベルごとの攻撃力成長 +1%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.01f },
            new() { Id = "w_crusher", Class = ClassId.Warrior, Name = "技：粉砕", Text = "パーク「粉砕」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Crusher },
            new() { Id = "w_armor", Class = ClassId.Warrior, Name = "不動", Text = "防御 +2", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Armor, ModifierKind.Flat, 2f) },
            new() { Id = "w_blood", Class = ClassId.Warrior, Name = "技：血の渇き", Text = "パーク「血の渇き」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Bloodlust },
            new() { Id = "w_start", Class = ClassId.Warrior, Name = "歴戦", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },

            // Mage
            new() { Id = "m_hp", Class = ClassId.Mage, Name = "魔力の護り", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "m_might", Class = ClassId.Mage, Name = "秘術の研鑽", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "m_chain", Class = ClassId.Mage, Name = "技：連鎖する魔弾", Text = "パーク「連鎖する魔弾」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.ChainBolt },
            new() { Id = "m_cd", Class = ClassId.Mage, Name = "高速詠唱", Text = "スキルのクールダウン −8%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Cooldown, ModifierKind.PercentAdd, -0.08f) },
            new() { Id = "m_ignite", Class = ClassId.Mage, Name = "技：燃焼", Text = "パーク「燃焼」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Ignite },
            new() { Id = "m_start", Class = ClassId.Mage, Name = "博識", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },

            // Ranger
            new() { Id = "r_hp", Class = ClassId.Ranger, Name = "野生の体", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "r_might", Class = ClassId.Ranger, Name = "弓術の研鑽", Text = "レベルごとの攻撃力成長 +1.2%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.012f },
            new() { Id = "r_venom", Class = ClassId.Ranger, Name = "技：毒矢", Text = "パーク「毒矢」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Venom },
            new() { Id = "r_speed", Class = ClassId.Ranger, Name = "俊足", Text = "移動速度 +6%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.06f) },
            new() { Id = "r_evasive", Class = ClassId.Ranger, Name = "技：身軽", Text = "パーク「身軽」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Evasive },
            new() { Id = "r_start", Class = ClassId.Ranger, Name = "狩りの心得", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },

            // Guardian
            new() { Id = "g_hp", Class = ClassId.Guardian, Name = "鋼の骨", Text = "レベルごとの最大HP成長 +5", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 5f },
            new() { Id = "g_might", Class = ClassId.Guardian, Name = "鎚の研鑽", Text = "レベルごとの攻撃力成長 +1%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.01f },
            new() { Id = "g_hammer", Class = ClassId.Guardian, Name = "技：聖鎚", Text = "パーク「聖鎚」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.HolyHammer },
            new() { Id = "g_armor", Class = ClassId.Guardian, Name = "城門の構え", Text = "防御 +3", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Armor, ModifierKind.Flat, 3f) },
            new() { Id = "g_aegis", Class = ClassId.Guardian, Name = "技：聖盾", Text = "パーク「聖盾」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Aegis },
            new() { Id = "g_start", Class = ClassId.Guardian, Name = "誓いの剣", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },

            // Assassin
            new() { Id = "a_hp", Class = ClassId.Assassin, Name = "しなやかな体", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "a_might", Class = ClassId.Assassin, Name = "刃の研鑽", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "a_veil", Class = ClassId.Assassin, Name = "技：煙幕", Text = "パーク「煙幕」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.SmokeVeil },
            new() { Id = "a_crit", Class = ClassId.Assassin, Name = "急所の心得", Text = "会心率 +8%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.CritChance, ModifierKind.Flat, 0.08f) },
            new() { Id = "a_twin", Class = ClassId.Assassin, Name = "技：双牙", Text = "パーク「双牙」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Twinfang },
            new() { Id = "a_start", Class = ClassId.Assassin, Name = "闇の作法", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },

            // Alchemist
            new() { Id = "x_hp", Class = ClassId.Alchemist, Name = "滋養の薬", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "x_might", Class = ClassId.Alchemist, Name = "調合の研鑽", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "x_catalyst", Class = ClassId.Alchemist, Name = "技：触媒", Text = "パーク「触媒」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Catalyst },
            new() { Id = "x_area", Class = ClassId.Alchemist, Name = "大きな瓶", Text = "スキルと攻撃の範囲 +10%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Area, ModifierKind.PercentAdd, 0.1f) },
            new() { Id = "x_twin", Class = ClassId.Alchemist, Name = "技：二本投げ", Text = "パーク「二本投げ」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.TwinFlask },
            new() { Id = "x_start", Class = ClassId.Alchemist, Name = "秘伝の書", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },

            // Deathcaller
            new() { Id = "d_hp", Class = ClassId.Deathcaller, Name = "屍の契り", Text = "レベルごとの最大HP成長 +3", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 3f },
            new() { Id = "d_might", Class = ClassId.Deathcaller, Name = "死霊術の研鑽", Text = "レベルごとの攻撃力成長 +1.5%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.015f },
            new() { Id = "d_plague", Class = ClassId.Deathcaller, Name = "技：疫病の呪い", Text = "パーク「疫病の呪い」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.PlagueCurse },
            new() { Id = "d_cd", Class = ClassId.Deathcaller, Name = "冥府の門", Text = "スキルのクールダウン −8%", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Cooldown, ModifierKind.PercentAdd, -0.08f) },
            new() { Id = "d_storm", Class = ClassId.Deathcaller, Name = "技：骨の嵐", Text = "パーク「骨の嵐」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Bonestorm },
            new() { Id = "d_start", Class = ClassId.Deathcaller, Name = "禁書", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },

            // Paladin
            new() { Id = "p_hp", Class = ClassId.Paladin, Name = "聖なる体", Text = "レベルごとの最大HP成長 +4", Cost = 30, Kind = MasteryKind.HealthGrowth, Value = 4f },
            new() { Id = "p_might", Class = ClassId.Paladin, Name = "剣の誓い", Text = "レベルごとの攻撃力成長 +1.2%", Cost = 40, Kind = MasteryKind.MightGrowth, Value = 0.012f },
            new() { Id = "p_martyr", Class = ClassId.Paladin, Name = "技：殉教", Text = "パーク「殉教」が出現するようになる", Cost = 60, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Martyr },
            new() { Id = "p_armor", Class = ClassId.Paladin, Name = "聖印の鎧", Text = "防御 +2", Cost = 70, Kind = MasteryKind.Stat, Modifier = M(StatType.Armor, ModifierKind.Flat, 2f) },
            new() { Id = "p_radiance", Class = ClassId.Paladin, Name = "技：光輝", Text = "パーク「光輝」が出現するようになる", Cost = 80, Kind = MasteryKind.UnlockPerk, Perk = PerkId.Radiance },
            new() { Id = "p_start", Class = ClassId.Paladin, Name = "叙任", Text = "ラン開始時にパークを1つ選べる", Cost = 100, Kind = MasteryKind.StartPerk },
        };

        public static readonly TrinketDef[] Trinkets =
        {
            new() { Id = "charm_hp", Name = "旅人のお守り", Text = "最大HP +15%", Cost = 0, Modifiers = new[] { M(StatType.MaxHealth, ModifierKind.PercentAdd, 0.15f) } },
            new() { Id = "wolf_fang", Name = "狼の牙", Text = "与えるダメージ +10%", Cost = 0, Modifiers = new[] { M(StatType.Might, ModifierKind.PercentAdd, 0.1f) } },
            new() { Id = "feather", Name = "風切り羽", Text = "移動速度 +10%", Cost = 40, Modifiers = new[] { M(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.1f) } },
            new() { Id = "iron_oath", Name = "鉄の誓約", Text = "防御 +3", Cost = 50, Modifiers = new[] { M(StatType.Armor, ModifierKind.Flat, 3f) } },
            new() { Id = "saint_ash", Name = "聖者の灰", Text = "仲間の復活速度 +60%", Cost = 60, Modifiers = new[] { M(StatType.ReviveSpeed, ModifierKind.PercentAdd, 0.6f) } },
            new() { Id = "soul_ring", Name = "吸魂の指輪", Text = "敵を倒すとHPが2回復", Cost = 70, HealOnKill = 2f },
            new() { Id = "hourglass", Name = "時の砂時計", Text = "スキルのクールダウン −12%", Cost = 80, Modifiers = new[] { M(StatType.Cooldown, ModifierKind.PercentAdd, -0.12f) } },
            new() { Id = "phoenix", Name = "不死鳥の羽", Text = "ランごとに1回、倒れてもその場でHP50%で復活", Cost = 120, Phoenix = true },
            new() { Id = "soul_lantern", Name = "魂の灯籠", Text = "魂を拾う範囲 +60%", Cost = 40, Modifiers = new[] { M(StatType.PickupRange, ModifierKind.PercentAdd, 0.6f) } },
            new() { Id = "bone_dice", Name = "骨のさいころ", Text = "会心率 +10%（会心はダメージ2倍）", Cost = 70, Modifiers = new[] { M(StatType.CritChance, ModifierKind.Flat, 0.1f) } },
            new() { Id = "elder_tome", Name = "古の魔導書", Text = "スキルと攻撃の範囲 +15%", Cost = 90, Modifiers = new[] { M(StatType.Area, ModifierKind.PercentAdd, 0.15f) } },
            new() { Id = "blood_chalice", Name = "血の聖杯", Text = "与えるダメージ +25%、ただし最大HP −15%", Cost = 100, Modifiers = new[] { M(StatType.Might, ModifierKind.PercentAdd, 0.25f), M(StatType.MaxHealth, ModifierKind.PercentAdd, -0.15f) } },
        };

        public static MasteryNode GetNode(string id) => Mastery.FirstOrDefault(n => n.Id == id);
        public static TrinketDef GetTrinket(string id) => Trinkets.FirstOrDefault(t => t.Id == id);
        public static IEnumerable<MasteryNode> NodesFor(ClassId c) => Mastery.Where(n => n.Class == c);

        /// <summary>Soul shards earned for a run.</summary>
        public static int ShardsForRun(int roomsCleared, int bossesKilled, int kills, bool victory, Difficulty difficulty = Difficulty.Normal) =>
            Mathf.RoundToInt((roomsCleared * 6 + bossesKilled * 20 + kills / 8 + (victory ? 40 : 0)) * DifficultyCatalog.Get(difficulty).Shards);
    }

    /// <summary>What a player brings into a run. Sent to the host when connecting.</summary>
    [Serializable]
    public sealed class Loadout
    {
        public ClassId classId;
        public int skin;
        public List<string> mastery = new();
        public List<string> trinkets = new();
        /// <summary>Stable per-profile id: lets a player who dropped out get their character back.</summary>
        public string playerKey = "";
        /// <summary>Worn gear when setting out (packed hex, see GearVault).</summary>
        public List<string> gear = new();
        /// <summary>Levels of the camp's facilities (CampUpgrade order), grown at the hearth.</summary>
        public List<int> camp = new();
        /// <summary>The lords' memories this player carries (Lore bits): all six open the way to the heart.</summary>
        public int memories;
        /// <summary>The title worn next to the name (an earned achievement's; empty for none).</summary>
        public string title = "";

        public string ToJson() => JsonUtility.ToJson(this);

        public static Loadout FromJson(string json)
        {
            try
            {
                var l = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Loadout>(json);
                if (l == null) return new Loadout();
                l.mastery ??= new List<string>();
                l.trinkets ??= new List<string>();
                l.camp ??= new List<int>();
                l.title ??= "";
                if (l.title.Length > 16) l.title = l.title.Substring(0, 16);
                for (int i = 0; i < l.camp.Count; i++) l.camp[i] = i < CampUpgrades.All.Length ? Mathf.Clamp(l.camp[i], 0, CampUpgrades.All[i].MaxLevel) : 0;
                // Drop unknown ids and trinkets beyond the slot count.
                l.mastery.RemoveAll(id => MetaCatalog.GetNode(id) == null);
                l.trinkets.RemoveAll(id => MetaCatalog.GetTrinket(id) == null);
                if (l.trinkets.Count > MetaCatalog.TrinketSlots) l.trinkets.RemoveRange(MetaCatalog.TrinketSlots, l.trinkets.Count - MetaCatalog.TrinketSlots);
                if (!Enum.IsDefined(typeof(ClassId), l.classId)) l.classId = ClassId.Warrior;
                if (l.skin < 0 || l.skin >= SkinCatalog.PerClass) l.skin = 0;
                return l;
            }
            catch (Exception)
            {
                return new Loadout();
            }
        }
    }

    /// <summary>This device's persistent progress.</summary>
    [Serializable]
    public sealed class ProfileData
    {
        public int version = 1;
        public int shards;
        public ClassId selectedClass;
        public List<string> unlocked = new();
        public List<string> equippedTrinkets = new();
        /// <summary>Selected skin index per class (indexed by ClassId).</summary>
        public List<int> skins = new();
        public Difficulty difficulty = Difficulty.Normal;
        /// <summary>Highest difficulty won so far (-1 = none).</summary>
        public int bestVictory = -1;
        /// <summary>Random id made once per profile (see Loadout.playerKey).</summary>
        public string playerKey = "";
        /// <summary>Requests from the camp's people (Quests.cs).</summary>
        public QuestLog quests = new();
        /// <summary>Gear kept between runs: worn set and stash (see GearVault).</summary>
        public GearVaultData gear = new();
        /// <summary>First-run hints already learnt (GuideHints): they stop showing.</summary>
        public List<string> guideSeen = new();
        /// <summary>Codex entries met so far (CodexCatalog ids).</summary>
        public List<string> codexSeen = new();

        public bool IsUnlocked(string id)
        {
            var trinket = MetaCatalog.GetTrinket(id);
            if (trinket != null && trinket.Cost == 0) return true;
            var skin = SkinCatalog.GetById(id);
            if (skin != null && skin.Cost == 0) return true;
            return unlocked.Contains(id);
        }

        /// <summary>Starter classes always; the others once a request has unlocked them.</summary>
        public bool IsClassOpen(ClassId c) => ClassCatalog.IsStarter(c) || unlocked.Contains(ClassCatalog.UnlockId(c));

        public int SkinFor(ClassId c) => (int)c < skins.Count ? skins[(int)c] : 0;

        public void SetSkin(ClassId c, int index)
        {
            while (skins.Count <= (int)c) skins.Add(0);
            skins[(int)c] = index;
        }

        public bool IsDifficultyOpen(Difficulty d)
        {
            var def = DifficultyCatalog.Get(d);
            return !def.UnlockedBy.HasValue || bestVictory >= (int)def.UnlockedBy.Value;
        }

        public void RecordVictory(Difficulty d) => bestVictory = Math.Max(bestVictory, (int)d);

        /// <summary>The lords' memories won (Lore bits, 1 &lt;&lt; land) and how many times the heart was lit (the true ending).</summary>
        public int memories;
        public int heartLit;

        /// <summary>Lines of the camp's people already told (CampStories).</summary>
        public List<string> storyRead = new();

        /// <summary>Achievements and the title worn next to the name (AchievementCatalog).</summary>
        public AchievementLog achievements = new();
        public string title = "";

        /// <summary>Levels of the camp's facilities (CampUpgrade order).</summary>
        public List<int> camp = new();

        public int CampLevel(CampUpgrade id) => CampUpgrades.Level(camp, id);

        /// <summary>Builds the next level of a facility when the shards are there.</summary>
        public bool TryUpgradeCamp(CampUpgrade id)
        {
            var def = CampUpgrades.Get(id);
            int level = CampLevel(id);
            if (level >= def.MaxLevel || shards < def.Costs[level]) return false;
            shards -= def.Costs[level];
            while (camp.Count <= (int)id) camp.Add(0);
            camp[(int)id] = level + 1;
            return true;
        }

        /// <summary>Set out into this week's labyrinth (the host's choice) instead of a fresh one.</summary>
        public bool weekly;
        /// <summary>This device's best in a week's labyrinth (Weekly): which week, how deep, how fast, cleared or not.</summary>
        public int weeklyWeek = -1;
        public int weeklyBestRoom = -1;
        public float weeklyBestTime;
        public bool weeklyCleared;

        /// <summary>A run of week <paramref name="week"/> ended: true when it is this week's new best.</summary>
        public bool RecordWeekly(int week, bool cleared, int room, float time)
        {
            if (weeklyWeek != week)
            {
                weeklyWeek = week;
                weeklyBestRoom = -1;
                weeklyCleared = false;
            }
            if (!Weekly.Better(cleared, room, time, weeklyCleared, weeklyBestRoom, weeklyBestTime)) return false;
            weeklyBestRoom = room;
            weeklyBestTime = time;
            weeklyCleared = cleared;
            return true;
        }

        public bool TryBuy(string id, int cost)
        {
            if (IsUnlocked(id) || shards < cost) return false;
            shards -= cost;
            unlocked.Add(id);
            return true;
        }

        public void ToggleTrinket(string id)
        {
            if (!IsUnlocked(id)) return;
            if (equippedTrinkets.Remove(id)) return;
            if (equippedTrinkets.Count >= MetaCatalog.TrinketSlots) equippedTrinkets.RemoveAt(0);
            equippedTrinkets.Add(id);
        }

        public Loadout ToLoadout() => new()
        {
            classId = IsClassOpen(selectedClass) ? selectedClass : ClassId.Warrior,
            skin = IsUnlocked(SkinCatalog.Get(selectedClass, SkinFor(selectedClass)).Id) ? SkinFor(selectedClass) : 0,
            mastery = MetaCatalog.NodesFor(selectedClass).Where(n => IsUnlocked(n.Id)).Select(n => n.Id).ToList(),
            trinkets = equippedTrinkets.Where(IsUnlocked).Take(MetaCatalog.TrinketSlots).ToList(),
            playerKey = playerKey,
            gear = GearVault.LoadoutGear(this.gear = GearVault.Normalize(this.gear)),
            camp = new List<int>(camp ?? new List<int>()),
            memories = memories,
            title = !string.IsNullOrEmpty(title) && achievements != null && achievements.Titles().Contains(title) ? title : "",
        };

        public string ToJson() => JsonUtility.ToJson(this);

        public static ProfileData FromJson(string json)
        {
            try
            {
                var p = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ProfileData>(json);
                if (p == null) return Default();
                p.unlocked ??= new List<string>();
                p.equippedTrinkets ??= new List<string>();
                p.camp ??= new List<int>();
                p.skins ??= new List<int>();
                p.quests ??= new QuestLog();
                p.quests.Repair();
                p.achievements ??= new AchievementLog();
                p.storyRead ??= new List<string>();
                p.achievements.Repair();
                p.title ??= "";
                if (!Enum.IsDefined(typeof(Difficulty), p.difficulty)) p.difficulty = Difficulty.Normal;
                // Classes added later are the rewards of requests some players have already turned in.
                if (p.quests.Step(QuestGiver.Crone) >= 5 && !p.unlocked.Contains(ClassCatalog.UnlockId(ClassId.Deathcaller))) p.unlocked.Add(ClassCatalog.UnlockId(ClassId.Deathcaller));
                if (p.quests.Step(QuestGiver.Master) >= 5 && !p.unlocked.Contains(ClassCatalog.UnlockId(ClassId.Paladin))) p.unlocked.Add(ClassCatalog.UnlockId(ClassId.Paladin));
                return p;
            }
            catch (Exception)
            {
                return Default();
            }
        }

        public static ProfileData Default() => new() { equippedTrinkets = new List<string> { "charm_hp" } };
    }
}
