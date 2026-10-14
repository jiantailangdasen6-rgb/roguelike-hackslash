using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Runes set into the two skills (RuneId, gear in the Rune1 / Rune2 slots): each answers its skill where the skill
    /// lands. A dash or a shot lands along a line (fire leaves a trail, frost freezes along it); a placed skill at the
    /// cursor; the others at the player. Rarity is how strongly the rune works (ItemCatalog.RunePower).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float RuneFireRadius = 1.5f;
        public const float RuneFireTime = 3f;
        public const float RuneFrostRadius = 1.9f;
        public const float RuneStormReach = 5f;
        public const float RuneHealReach = 4.5f;

        private readonly List<int> _runeHits = new();

        /// <summary>Where a skill lands: a line for the dashes and the piercing shot, a point (From == To) for the rest.</summary>
        private static (Vector2 From, Vector2 To) SkillLanding(SimPlayer p, int skill, Vector2 dir)
        {
            Vector2 at = p.Position;
            if (skill == 1)
                switch (p.ClassId)
                {
                    case ClassId.Warrior: return (at, at + dir * ClassCatalog.DashSlashDistance);
                    case ClassId.Guardian: return (at, at + dir * ClassCatalog.ShieldBashDistance);
                    case ClassId.Ranger: return (at, at + dir * 6f);
                    case ClassId.Mage: at = AimPoint(p, ClassCatalog.FrostNovaRange); break;
                    case ClassId.Assassin: at = AimPoint(p, ClassCatalog.ShadowStepRange); break;
                    case ClassId.Alchemist: at = AimPoint(p, ClassCatalog.ThrowRange); break;
                    case ClassId.Deathcaller: at = AimPoint(p, ClassCatalog.RaiseRange); break;
                    case ClassId.Paladin: at = AimPoint(p, ClassCatalog.JudgmentRange); break;
                }
            else
                switch (p.ClassId)
                {
                    case ClassId.Ranger: at = AimPoint(p, ClassCatalog.MarkRange); break;
                    case ClassId.Alchemist: at = AimPoint(p, ClassCatalog.ThrowRange); break;
                    case ClassId.Deathcaller: at = AimPoint(p, ClassCatalog.CurseRange); break;
                }
            return (at, at);
        }

        /// <summary>The swift rune shortens the skill's cooldown.</summary>
        private static float RuneCooldown(SimPlayer p, int skill, float cooldown)
        {
            var (rune, power) = p.RuneIn(skill);
            return rune == RuneId.Swift ? cooldown / (1f + 0.35f * power) : cooldown;
        }

        /// <summary>A skill was used: its rune answers.</summary>
        private void RuneCast(SimPlayer p, int skill, Vector2 dir)
        {
            var (rune, power) = p.RuneIn(skill);
            if (rune == RuneId.None) return;
            var (from, to) = SkillLanding(p, skill, dir);
            bool line = (to - from).sqrMagnitude > 0.01f;
            var points = new List<Vector2>();
            if (line) for (int k = 1; k <= 3; k++) points.Add(Room != null ? ClampToRoom(Vector2.Lerp(from, to, k / 3f), 0.3f) : Vector2.Lerp(from, to, k / 3f));
            else points.Add(to);
            float might = p.Stats.Get(StatType.Might);
            Vector2 flareAt = points[points.Count - 1];

            switch (rune)
            {
                case RuneId.Ember:
                    foreach (var at in points)
                    {
                        float r = RuneFireRadius * (0.85f + 0.15f * power);
                        _zones.Add(new Zone { Kind = AreaKind.FirePool, Owner = p, Position = at, Radius = r, Timer = RuneFireTime });
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.FirePool, Pos = at, Value = r, Size = new Vector2(RuneFireTime, 0f) });
                    }
                    break;
                case RuneId.Frost:
                    foreach (var at in points)
                    {
                        Enemies.QueryCircle(at, RuneFrostRadius, _runeHits);
                        foreach (int i in _runeHits.ToArray())
                        {
                            if (Enemies[i].Dead) continue;
                            ApplyStatus(i, StatusKind.Frozen, (Enemies[i].Def.Boss ? 0.4f : 1.1f) * power, p);
                            DamageEnemy(i, 8f * power * might, p, HitFlags.None, Vector2.zero, 0f);
                        }
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.FrostNova, Pos = at, Value = RuneFrostRadius });
                    }
                    break;
                case RuneId.Storm:
                {
                    // Lightning leaps from where the skill lands to the closest monsters around it.
                    Vector2 centre = line ? (from + to) * 0.5f : to;
                    Enemies.QueryCircle(centre, RuneStormReach + (line ? 1.5f : 0f), _runeHits);
                    var targets = new List<int>(_runeHits);
                    targets.RemoveAll(i => Enemies[i].Dead || Enemies[i].Def.Prop);
                    targets.Sort((a, b) => (Enemies[a].Position - centre).sqrMagnitude.CompareTo((Enemies[b].Position - centre).sqrMagnitude));
                    int count = 2 + Mathf.RoundToInt(power);
                    for (int k = 0; k < targets.Count && k < count; k++)
                    {
                        int i = targets[k];
                        Vector2 hit = Enemies[i].Position;
                        DamageEnemy(i, 18f * power * might, p, HitFlags.None, (hit - centre).normalized, 2f);
                        if (i < Enemies.Count && !Enemies[i].Dead) ApplyStatus(i, StatusKind.Stunned, Enemies[i].Def.Boss ? 0.15f : 0.4f, p);
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Lightning, Pos = centre, Vec = hit, Value = 1f });
                    }
                    flareAt = centre;
                    break;
                }
                case RuneId.Vital:
                    foreach (var ally in Players)
                    {
                        if (!ally.Alive || (ally.Position - p.Position).sqrMagnitude > RuneHealReach * RuneHealReach) continue;
                        float before = ally.Hp;
                        ally.Heal(ally.MaxHp * 0.05f * power);
                        if (ally.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = ally.ClientId, Pos = ally.Position, Value = ally.Hp - before });
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Heal, Pos = p.Position, Value = RuneHealReach });
                    flareAt = p.Position;
                    break;
                case RuneId.Echo:
                    // Now and then the skill is ready again straight away.
                    if (!_combatRng.Chance(0.15f + 0.1f * power)) return;
                    if (skill == 1) p.Skill1Cooldown = Mathf.Min(p.Skill1Cooldown, 0.3f);
                    else p.Skill2Cooldown = Mathf.Min(p.Skill2Cooldown, 0.3f);
                    flareAt = p.Position;
                    break;
                case RuneId.Swift:
                    return; // its work is done on the cooldown (RuneCooldown)
                case RuneId.Shadow:
                    p.DodgeCooldown = 0f;
                    p.SureCritTimer = Mathf.Max(p.SureCritTimer, 1.4f * power);
                    flareAt = p.Position;
                    break;
                case RuneId.Soul:
                {
                    int count = 4 + Mathf.RoundToInt(2f * power);
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 d = Rotate(Vector2.right, k * 360f / count + 15f);
                        FireProjectile(p, ProjectileKind.SoulBolt, to + d * 0.4f, d * 9f, 0.75f, 0.3f, 9f * power * might, 0, HitFlags.None);
                    }
                    break;
                }
            }
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.RuneFlare, Pos = flareAt, Value = 1.2f, Size = new Vector2((int)rune, 0f) });
        }
    }
}
