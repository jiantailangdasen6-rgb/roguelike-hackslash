using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Player abilities, projectiles, damage to enemies, statuses and class synergies.</summary>
    public sealed partial class RunSimulation
    {
        private struct DelayedArea
        {
            public AreaKind Kind;
            public SimPlayer Owner;
            public Vector2 Position;
            public float Radius;
            public float Timer;
        }

        private readonly List<DelayedArea> _delayedAreas = new();
        private readonly List<int> _splash = new();

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static Vector2 AimDirection(SimPlayer p) =>
            p.Aim.sqrMagnitude > 0.0001f ? p.Aim.normalized : Vector2.right;

        /// <summary>Target point for placed skills: the cursor, clamped to the skill's range.</summary>
        private static Vector2 AimPoint(SimPlayer p, float range) =>
            p.Position + Vector2.ClampMagnitude(p.Aim, range);

        // ------------------------------------------------------------------ Per-player ability loop

        private void StepPlayersCombat(float dt)
        {
            foreach (var p in Players)
            {
                p.BasicTimer -= dt;
                p.Skill1Cooldown -= dt;
                p.Skill2Cooldown -= dt;
                p.DodgeCooldown -= dt;
                if (p.DodgeWindow > 0f) p.DodgeWindow -= dt;
                if (p.DodgeCharges < ClassCatalog.DodgeCharges && (p.DodgeRecharge -= dt) <= 0f)
                {
                    p.DodgeCharges++;
                    if (p.DodgeCharges < ClassCatalog.DodgeCharges) p.DodgeRecharge += DodgeRechargeTime(p);
                }
                if ((p.ComboResetTimer -= dt) <= 0f) p.Combo = 0;

                if (!p.Alive)
                {
                    // Keep counters in sync so presses made while down don't fire on revive.
                    p.LastSkill1 = p.Input.Skill1Count;
                    p.LastSkill2 = p.Input.Skill2Count;
                    p.LastDodge = p.Input.DodgeCount;
                    p.LastUlt = p.Input.UltCount;
                    p.UltTimer = 0f;
                    StepGhost(p, dt); // RunSimulation.Ghosts.cs
                    continue;
                }

                if (p.Input.DodgeCount != p.LastDodge)
                {
                    p.LastDodge = p.Input.DodgeCount;
                    StepGhost(p, dt); // RunSimulation.Ghosts.cs
                    if (p.DodgeCooldown <= 0f && p.DodgeCharges > 0) Dodge(p);
                }
                if (p.Input.UltCount != p.LastUlt)
                {
                    p.LastUlt = p.Input.UltCount;
                    _ultimateCasting = true;
                    TryUltimate(p, AimDirection(p)); // RunSimulation.Ultimates.cs
                    _ultimateCasting = false;
                }
                if (p.UltTimer > 0f)
                {
                    _ultimateCasting = true;
                    StepUltimate(p, dt);
                    _ultimateCasting = false;
                }
                if (p.Input.Skill1Count != p.LastSkill1)
                {
                    p.LastSkill1 = p.Input.Skill1Count;
                    if (p.Skill1Cooldown <= 0f) Skill1(p);
                }
                if (p.Input.Skill2Count != p.LastSkill2)
                {
                    p.LastSkill2 = p.Input.Skill2Count;
                    if (p.Skill2Cooldown <= 0f) Skill2(p);
                }
                if (p.Input.Attack && p.BasicTimer <= 0f) Basic(p);

                if (p.DashSlashTimer > 0f)
                {
                    if (p.BashStuns) StepShieldBash(p, dt);
                    else StepDashSlash(p, dt);
                }
                if (p.ShadowStepTimer > 0f && (p.ShadowStepTimer -= dt) <= 0f) ShadowStrike(p);
            }
        }

        private void Dodge(SimPlayer p)
        {
            bool evasive = p.PerkLevel(PerkId.Evasive) > 0;
            if (p.DodgeCharges >= ClassCatalog.DodgeCharges) p.DodgeRecharge = DodgeRechargeTime(p);
            p.DodgeCharges--;
            p.DodgeCooldown = ClassCatalog.DodgeCooldown;
            p.InvulnerableTimer = Mathf.Max(p.InvulnerableTimer, ClassCatalog.DodgeInvulnerability);
            p.DodgeWindow = ClassCatalog.DodgeInvulnerability;
            p.PerfectThisDodge = false;
            if (evasive) p.EvasiveTimer = 0.5f;
            BlessingOnDodge(p);
            if (p.Stats.Get(StatType.DodgeStrike) > 0f) p.DodgeStrikeTimer = 2f; // gear: the next blow lands harder
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.Dodge, Pos = p.Position, Vec = AimDirection(p) });
        }

        /// <summary>How long one dodge charge takes to come back (the ranger's light feet, curses and the difficulty change it).</summary>
        public float DodgeRechargeTime(SimPlayer p) =>
            ClassCatalog.DodgeRechargeTime * (p.PerkLevel(PerkId.Evasive) > 0 ? 0.6f : 1f) * p.DodgeFactor;

        /// <summary>A hit that would have landed while dodging: the charge comes straight back, and everyone sees it.</summary>
        private void PerfectDodge(SimPlayer p)
        {
            p.PerfectThisDodge = true;
            GainFocus(p, FocusPerfectDodge);
            if (p.DodgeCharges < ClassCatalog.DodgeCharges)
            {
                p.DodgeCharges++;
                if (p.DodgeCharges >= ClassCatalog.DodgeCharges) p.DodgeRecharge = 0f;
            }
            p.DodgeCooldown = 0f;
            Events.Add(new SimEvent(SimEventType.PerfectDodge) { ClientId = p.ClientId, Pos = p.Position });
        }

        private float AttackInterval(SimPlayer p)
        {
            float speed = p.Stats.Get(StatType.AttackSpeed) * (p.EvasiveTimer > 0f ? 2f : 1f);
            float interval = p.WeaponKind == 1 ? ClassCatalog.AltWeapon(p.ClassId).Interval : p.Class.BasicInterval; // RunSimulation.Weapons.cs
            return interval / Mathf.Max(0.2f, speed);
        }

        private void Basic(SimPlayer p)
        {
            p.BasicTimer = AttackInterval(p);
            Vector2 dir = AimDirection(p);
            float might = p.Stats.Get(StatType.Might);
            float area = p.Stats.Get(StatType.Area);
            var c = p.Class;
            if (p.WeaponKind == 1)
            {
                BasicAlt(p, dir, might, area); // the class's other weapon (RunSimulation.Weapons.cs)
                BlessingOnBasic(p);
                return;
            }

            switch (c.Id)
            {
                case ClassId.Warrior:
                {
                    int combo = p.Combo;
                    bool finisher = combo == 2;
                    bool spin = finisher && p.PerkLevel(PerkId.Whirlwind) > 0;
                    float range = c.BasicRange * area * (finisher ? 1.15f : 1f);
                    float damage = c.BasicDamage * might * (finisher ? 1.6f : 1f);
                    if (spin) Enemies.QueryCircle(p.Position, range, _hits);
                    else Enemies.QueryCone(p.Position, dir, range, c.BasicArc, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        Vector2 push = (Enemies[i].Position - p.Position).normalized;
                        DamageEnemy(i, damage, p, HitFlags.Heavy, push, finisher ? 7f : 3.5f);
                    }

                    int wave = p.PerkLevel(PerkId.SwordWave);
                    if (finisher && wave > 0)
                        FireProjectile(p, ProjectileKind.SwordWave, p.Position + dir * 0.6f, dir * 13f, 0.55f, 0.7f,
                            c.BasicDamage * might * 0.6f * wave, 99, HitFlags.Heavy);

                    p.Combo = (combo + 1) % 3;
                    p.ComboResetTimer = 1.1f;
                    if (finisher) p.BasicTimer *= 1.35f;
                    var anim = spin ? AttackAnim.Spin : combo == 1 ? AttackAnim.BasicB : AttackAnim.BasicA;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)anim, Pos = p.Position, Vec = dir, Value = range });
                    break;
                }
                case ClassId.Mage:
                {
                    int count = 1 + p.PerkLevel(PerkId.Multicast) + Mathf.RoundToInt(p.Stats.Get(StatType.Amount));
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 d = Rotate(dir, (k - (count - 1) * 0.5f) * 9f);
                        var bolt = FireProjectile(p, ProjectileKind.Bolt, p.Position + d * 0.5f, d * ClassCatalog.BoltSpeed,
                            c.BasicRange / ClassCatalog.BoltSpeed, 0.3f, c.BasicDamage * might, 0, HitFlags.None);
                        bolt.Splits = p.PerkLevel(PerkId.SplitBolt) > 0;
                    }
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicA : AttackAnim.BasicB), Pos = p.Position, Vec = dir });
                    break;
                }
                case ClassId.Ranger:
                {
                    int count = 1 + 2 * p.PerkLevel(PerkId.Spread) + Mathf.RoundToInt(p.Stats.Get(StatType.Amount));
                    int pierce = p.PerkLevel(PerkId.Piercing);
                    bool ricochet = p.PerkLevel(PerkId.Ricochet) > 0;
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 d = Rotate(dir, (k - (count - 1) * 0.5f) * 7f);
                        var arrow = FireProjectile(p, ProjectileKind.Arrow, p.Position + d * 0.5f, d * ClassCatalog.ArrowSpeed,
                            c.BasicRange / ClassCatalog.ArrowSpeed, 0.25f, c.BasicDamage * might, pierce, HitFlags.None);
                        arrow.Ricochet = ricochet;
                    }
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.BasicA, Pos = p.Position, Vec = dir });
                    break;
                }
                default:
                    if (c.Id >= ClassId.Deathcaller) BasicRound5(p, dir, might, area); // RunSimulation.Round5.cs
                    else BasicNewClass(p, dir, might, area); // RunSimulation.Classes.cs
                    break;
            }
            BlessingOnBasic(p);
        }

        private void Skill1(SimPlayer p)
        {
            var c = p.Class;
            float cd = c.Skill1Cooldown * p.CooldownMultiplier;
            Vector2 dir = AimDirection(p);
            float might = p.Stats.Get(StatType.Might);

            switch (c.Id)
            {
                case ClassId.Warrior:
                    cd *= 1f - 0.2f * p.PerkLevel(PerkId.Rush);
                    // The owner moves itself (owner-authoritative movement); we sweep the path it travels.
                    p.DashSlashTimer = ClassCatalog.DashSlashDuration + 0.08f;
                    p.DashSlashFrom = p.Position;
                    p.DashSlashDir = dir;
                    p.DashSlashHits.Clear();
                    p.InvulnerableTimer = Mathf.Max(p.InvulnerableTimer, ClassCatalog.DashSlashDuration + 0.15f);
                    break;
                case ClassId.Mage:
                {
                    float radius = ClassCatalog.FrostNovaRadius * p.Stats.Get(StatType.Area) * (1f + 0.2f * p.PerkLevel(PerkId.Permafrost));
                    _delayedAreas.Add(new DelayedArea
                    {
                        Kind = AreaKind.FrostNova, Owner = p, Radius = radius, Timer = ClassCatalog.FrostNovaDelay,
                        Position = p.HasLegendary(LegendaryId.FrostCrown) ? p.Position : AimPoint(p, ClassCatalog.FrostNovaRange),
                    });
                    break;
                }
                case ClassId.Ranger:
                {
                    // Legendary "影の": a fan of three.
                    int arrows = p.HasLegendary(LegendaryId.ShadowQuiver) ? 3 : 1;
                    if (arrows > 1) Proc(p, LegendaryId.ShadowQuiver, p.Position);
                    for (int k = 0; k < arrows; k++)
                    {
                        Vector2 d = Rotate(dir, (k - (arrows - 1) * 0.5f) * 12f);
                        FireProjectile(p, ProjectileKind.PierceArrow, p.Position + d * 0.5f, d * ClassCatalog.PierceArrowSpeed,
                            0.6f, 0.45f, ClassCatalog.PierceArrowDamage * might, 99, HitFlags.Heavy);
                    }
                    break;
                }
                default:
                    cd = c.Id >= ClassId.Deathcaller ? Skill1Round5(p, dir, cd) : Skill1NewClass(p, dir, cd);
                    break;
            }

            p.Skill1Cooldown = RuneCooldown(p, 1, cd);
            RuneCast(p, 1, dir); // RunSimulation.Runes.cs
            TryUnion(p); // RunSimulation.Unions.cs
            BlessingOnSkill1(p);
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.Skill1, Pos = p.Position, Vec = dir });
        }

        private void Skill2(SimPlayer p)
        {
            var c = p.Class;
            Vector2 dir = AimDirection(p);
            float area = p.Stats.Get(StatType.Area);
            float might = p.Stats.Get(StatType.Might);

            switch (c.Id)
            {
                case ClassId.Warrior:
                {
                    bool drum = p.HasLegendary(LegendaryId.WarDrum);
                    float radius = ClassCatalog.WarCryRadius * area * (drum ? 2f : 1f);
                    float allyReach = ClassCatalog.WarCryAllyRadius * (drum ? 2f : 1f);
                    if (drum) Proc(p, LegendaryId.WarDrum, p.Position);
                    Enemies.QueryCircle(p.Position, radius, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        ref var e = ref Enemies[i];
                        ApplyStatus(i, StatusKind.Stunned, e.Def.Boss ? 0.35f : 1.1f, p);
                        DamageEnemy(i, 8f * might, p, HitFlags.None, (e.Position - p.Position).normalized, 9f);
                    }
                    int bulwark = p.PerkLevel(PerkId.Bulwark);
                    foreach (var ally in Players)
                    {
                        if (!ally.Alive || (ally.Position - p.Position).sqrMagnitude > allyReach * allyReach) continue;
                        ally.GrantValor(ClassCatalog.ValorDuration);
                        if (drum) ally.GrantDrum(ClassCatalog.ValorDuration);
                        if (bulwark > 0) ally.Shield = Mathf.Max(ally.Shield, 25f * bulwark);
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.WarCry, Pos = p.Position, Value = radius });
                    HolyWarCry(p);
                    break;
                }
                case ClassId.Mage:
                {
                    float amount = ClassCatalog.HealAmount * (1f + 0.4f * p.PerkLevel(PerkId.Mercy));
                    float r = ClassCatalog.HealRadius * area;
                    foreach (var ally in Players)
                    {
                        if ((ally.FallenAt - p.Position).sqrMagnitude > r * r) continue;
                        if (!ally.Alive)
                        {
                            ally.ReviveProgress += 0.5f;
                            continue;
                        }
                        float before = ally.Hp;
                        ally.Heal(amount);
                        if (p.PerkLevel(PerkId.Mercy) > 0) ally.GrantMercy(5f, 0.15f * p.PerkLevel(PerkId.Mercy));
                        Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = ally.ClientId, Pos = ally.Position, Value = ally.Hp - before });
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Heal, Pos = p.Position, Value = r });
                    break;
                }
                case ClassId.Ranger:
                {
                    int deep = p.PerkLevel(PerkId.DeepMark);
                    float r = ClassCatalog.MarkRadius * area * (1f + 0.25f * deep);
                    Vector2 at = AimPoint(p, ClassCatalog.MarkRange);
                    Enemies.QueryCircle(at, r, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        ref var e = ref Enemies[i];
                        e.MarkBonus = ClassCatalog.MarkDamageBonus + 0.15f * deep;
                        ApplyStatus(i, StatusKind.Marked, ClassCatalog.MarkDuration * (1f + p.Stats.Get(StatType.MarkDuration)), p);
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Mark, Pos = at, Value = r });
                    // Legendary "狩神の": the nearest unmarked monster outside the circle is marked too.
                    if (p.HasLegendary(LegendaryId.HuntGodMark))
                    {
                        int best = -1;
                        float bestDistance = 8f * 8f;
                        for (int i = 0; i < Enemies.Count; i++)
                        {
                            ref var e = ref Enemies[i];
                            if (e.Dead || e.Def.Prop || e.Marked > 0f) continue;
                            float d = (e.Position - at).sqrMagnitude;
                            if (d < bestDistance) { bestDistance = d; best = i; }
                        }
                        if (best >= 0)
                        {
                            Enemies[best].MarkBonus = ClassCatalog.MarkDamageBonus + 0.15f * deep;
                            ApplyStatus(best, StatusKind.Marked, ClassCatalog.MarkDuration * (1f + p.Stats.Get(StatType.MarkDuration)), p);
                            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Mark, Pos = Enemies[best].Position, Value = 1.2f });
                            Proc(p, LegendaryId.HuntGodMark, at, Enemies[best].Position);
                        }
                    }
                    break;
                }
                default:
                    if (c.Id >= ClassId.Deathcaller) Skill2Round5(p, dir, area); // RunSimulation.Round5.cs
                    else Skill2NewClass(p, dir, area); // RunSimulation.Classes.cs
                    break;
            }

            p.Skill2Cooldown = RuneCooldown(p, 2, c.Skill2Cooldown * p.CooldownMultiplier);
            RuneCast(p, 2, dir); // RunSimulation.Runes.cs
            TryUnion(p); // RunSimulation.Unions.cs
            BlessingOnSkill2(p);
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.Skill2, Pos = p.Position, Vec = dir });
        }

        private void StepDashSlash(SimPlayer p, float dt)
        {
            p.DashSlashTimer -= dt;
            if (p.DashSlashTimer <= 0f)
            {
                Earthshake(p);
                if (p.HasLegendary(LegendaryId.WhirlwindBlade))
                {
                    WhirlwindFinish(p);
                    Proc(p, LegendaryId.WhirlwindBlade, p.Position);
                }
            }
            float damage = ClassCatalog.DashSlashDamage * p.Stats.Get(StatType.Might) * (1f + 0.2f * p.PerkLevel(PerkId.Rush));
            // Sweep from the start point to where the player is now (they travel on their own client).
            Vector2 from = p.DashSlashFrom, to = p.Position;
            Vector2 seg = to - from;
            float len = seg.magnitude;
            Vector2 mid = (from + to) * 0.5f;
            Enemies.QueryCircle(mid, len * 0.5f + 1.1f, _hits);
            foreach (int i in _hits.ToArray())
            {
                ref var e = ref Enemies[i];
                if (p.DashSlashHits.Contains(e.Id)) continue;
                float t = len > 0.01f ? Mathf.Clamp01(Vector2.Dot(e.Position - from, seg) / (len * len)) : 0f;
                if ((from + seg * t - e.Position).sqrMagnitude > (1.1f + e.Radius) * (1.1f + e.Radius)) continue;
                p.DashSlashHits.Add(e.Id);
                DamageEnemy(i, damage, p, HitFlags.Heavy, p.DashSlashDir, 6f);
            }
        }

        /// <summary>Legendary "旋風の": the dash-slash ends in a full spin.</summary>
        private void WhirlwindFinish(SimPlayer p)
        {
            float range = p.Class.BasicRange * 1.4f * p.Stats.Get(StatType.Area);
            float damage = ClassCatalog.DashSlashDamage * 0.8f * p.Stats.Get(StatType.Might);
            Enemies.QueryCircle(p.Position, range, _hits);
            foreach (int i in _hits.ToArray())
            {
                if (Enemies[i].Dead) continue;
                DamageEnemy(i, damage, p, HitFlags.Heavy, (Enemies[i].Position - p.Position).normalized, 6f);
            }
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.Spin, Pos = p.Position, Vec = p.DashSlashDir, Value = range });
        }

        /// <summary>"Earthshaker": the dash-slash ends with a stomp that stuns everything around.</summary>
        private void Earthshake(SimPlayer p)
        {
            int level = p.PerkLevel(PerkId.Earthshaker);
            if (level <= 0) return;
            float radius = 2.4f * p.Stats.Get(StatType.Area);
            float damage = 12f * level * p.Stats.Get(StatType.Might);
            Enemies.QueryCircle(p.Position, radius, _hits);
            foreach (int i in _hits.ToArray())
            {
                ref var e = ref Enemies[i];
                if (e.Dead) continue;
                Vector2 push = (e.Position - p.Position).normalized;
                ApplyStatus(i, StatusKind.Stunned, e.Def.Boss ? 0.3f : 0.9f, p);
                DamageEnemy(i, damage, p, HitFlags.Heavy, push, 5f);
            }
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Slam, Pos = p.Position, Value = radius });
        }

        private void StepDelayedAreas(float dt)
        {
            for (int a = _delayedAreas.Count - 1; a >= 0; a--)
            {
                var area = _delayedAreas[a];
                area.Timer -= dt;
                if (area.Timer > 0f)
                {
                    _delayedAreas[a] = area;
                    continue;
                }
                _delayedAreas.RemoveAt(a);

                if (area.Kind == AreaKind.FrostNova)
                {
                    var p = area.Owner;
                    if (p.HasLegendary(LegendaryId.FrostCrown))
                    {
                        area.Position = p.Position; // "霜の王の": the field moves with its caster
                        Proc(p, LegendaryId.FrostCrown, p.Position);
                    }
                    float duration = (ClassCatalog.FreezeDuration + p.PerkLevel(PerkId.Permafrost)) * (1f + p.Stats.Get(StatType.FreezeDuration));
                    Enemies.QueryCircle(area.Position, area.Radius, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        ref var e = ref Enemies[i];
                        DamageEnemy(i, ClassCatalog.FrostNovaDamage * p.Stats.Get(StatType.Might), p, HitFlags.None, Vector2.zero, 0f);
                        if (!e.Dead) ApplyStatus(i, e.Def.Boss ? StatusKind.Stunned : StatusKind.Frozen, e.Def.Boss ? 0.6f : duration, p);
                    }
                    int armor = p.PerkLevel(PerkId.FrostArmor);
                    if (armor > 0)
                    {
                        float reach = area.Radius + 1.5f;
                        foreach (var ally in Players)
                            if (ally.Alive && (ally.Position - area.Position).sqrMagnitude <= reach * reach)
                                ally.Shield = Mathf.Max(ally.Shield, 15f * armor);
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.FrostNova, Pos = area.Position, Value = area.Radius });
                    SteamBursts(p, area.Position, area.Radius);
                }
            }
        }

        // ------------------------------------------------------------------ Projectiles

        private Projectile FireProjectile(SimPlayer owner, ProjectileKind kind, Vector2 pos, Vector2 velocity, float life,
            float radius, float damage, int pierce, HitFlags flags)
        {
            var proj = new Projectile
            {
                Id = _nextProjectileId++, Kind = kind, Hostile = false, Owner = owner.ClientId,
                Position = pos, Velocity = velocity, Life = life, Radius = radius, Damage = damage, PierceLeft = pierce, Flags = flags,
            };
            Projectiles.Add(proj);
            Events.Add(new SimEvent(SimEventType.ProjectileSpawn) { Id = proj.Id, Int = (int)kind, Pos = pos, Vec = velocity, Value = life });
            return proj;
        }

        private void FireHostile(ProjectileKind kind, Vector2 pos, Vector2 velocity, float life, float damage)
        {
            var proj = new Projectile
            {
                Id = _nextProjectileId++, Kind = kind, Hostile = true,
                Position = pos, Velocity = velocity, Life = life, Radius = kind == ProjectileKind.EnemyArrow ? 0.2f : 0.3f, Damage = damage,
            };
            Projectiles.Add(proj);
            Events.Add(new SimEvent(SimEventType.ProjectileSpawn) { Id = proj.Id, Int = (int)proj.Kind, Pos = pos, Vec = velocity, Value = life });
        }

        private void StepProjectiles(float dt)
        {
            for (int k = Projectiles.Count - 1; k >= 0; k--)
            {
                var proj = Projectiles[k];
                proj.Position += proj.Velocity * dt;
                proj.Life -= dt;
                bool spent = false;

                if (proj.Hostile)
                {
                    foreach (var p in Players)
                    {
                        if (!p.Alive) continue;
                        float r = SimPlayer.BodyRadius + proj.Radius;
                        if ((p.Position - proj.Position).sqrMagnitude > r * r) continue;
                        HurtPlayer(p, proj.Damage);
                        spent = true;
                        break;
                    }
                }
                else if (!FliesOver(proj.Kind))
                {
                    var owner = GetPlayer(proj.Owner);
                    Enemies.QueryCircle(proj.Position, proj.Radius, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        int id = Enemies[i].Id;
                        if (proj.HitIds.Contains(id)) continue;
                        proj.HitIds.Add(id);
                        OnProjectileHit(proj, owner, i);
                        if (--proj.PierceLeft < 0)
                        {
                            spent = true;
                            break;
                        }
                    }
                }

                // Lobbed flasks sail over cover; everything else stops at it.
                // Shots stop at the walls of the room they fly in (rooms sit apart on the map).
                var inRoom = Map.RoomAt(proj.Position, 0f);
                bool blocked = inRoom == null ? !Map.InCorridor(proj.Position) // shots fly down the corridors too
                    : !FliesOver(proj.Kind) && Arena.BlocksShot(inRoom.Layout, proj.Position);
                if (spent || proj.Life <= 0f || blocked)
                {
                    if (!proj.Hostile && IsFlask(proj.Kind)) BreakFlask(proj); // RunSimulation.Classes.cs
                    Events.Add(new SimEvent(SimEventType.ProjectileEnd) { Id = proj.Id, Pos = proj.Position });
                    Projectiles.RemoveAt(k);
                }
            }
        }

        private void OnProjectileHit(Projectile proj, SimPlayer owner, int index)
        {
            Vector2 dir = proj.Velocity.normalized;
            if (proj.Kind is ProjectileKind.Flask or ProjectileKind.Bomb) return; // it bursts instead (BreakFlask)
            DamageEnemy(index, proj.Damage, owner, proj.Flags, dir, proj.Kind == ProjectileKind.PierceArrow ? 5f : 1.5f);
            if (owner == null) return;
            ref var e = ref Enemies[index];

            if (proj.Kind == ProjectileKind.SoulBolt && owner.PerkLevel(PerkId.SoulSiphon) > 0) owner.Heal(owner.PerkLevel(PerkId.SoulSiphon)); // deathcaller
            int ignite = owner.PerkLevel(PerkId.Ignite);
            if (proj.Kind == ProjectileKind.Bolt && ignite > 0 && !e.Dead)
            {
                e.BurnDps = 4f * ignite * owner.Stats.Get(StatType.Might);
                ApplyStatus(index, StatusKind.Burning, 3f);
            }
            int venom = owner.PerkLevel(PerkId.Venom);
            if ((proj.Kind == ProjectileKind.Arrow || proj.Kind == ProjectileKind.PierceArrow) && venom > 0 && !e.Dead)
            {
                e.PoisonDps = 3f * venom * owner.Stats.Get(StatType.Might);
                ApplyStatus(index, StatusKind.Poisoned, 4f);
            }

            int chain = owner.PerkLevel(PerkId.ChainBolt) + (owner.HasLegendary(LegendaryId.ThunderStaff) ? 2 : 0);
            if (proj.Kind == ProjectileKind.Bolt && chain > 0)
            {
                int lastId = e.Id;
                Vector2 from = e.Position;
                for (int c = 0; c < chain; c++)
                {
                    int next = Enemies.FindNearest(from, 3.5f, lastId);
                    if (next < 0 || proj.HitIds.Contains(Enemies[next].Id)) break;
                    proj.HitIds.Add(Enemies[next].Id);
                    Vector2 to = Enemies[next].Position;
                    DamageEnemy(next, proj.Damage * 0.6f, owner, HitFlags.None, (to - from).normalized, 1f);
                    Proc(owner, owner.HasLegendary(LegendaryId.ThunderStaff) ? LegendaryId.ThunderStaff : LegendaryId.None, from, to);
                    lastId = Enemies[next].Id;
                    from = to;
                }
            }

            if (proj.Ricochet)
            {
                proj.Ricochet = false;
                int next = Enemies.FindNearest(proj.Position, 5f, e.Id);
                if (next >= 0 && !proj.HitIds.Contains(Enemies[next].Id))
                {
                    Vector2 to = (Enemies[next].Position - proj.Position).normalized;
                    var child = FireProjectile(owner, ProjectileKind.Arrow, proj.Position, to * ClassCatalog.ArrowSpeed, 0.4f, 0.25f, proj.Damage * 0.7f, 0, HitFlags.None);
                    child.HitIds.AddRange(proj.HitIds);
                }
            }

            if (proj.Splits)
            {
                proj.Splits = false;
                for (int s = -1; s <= 1; s += 2)
                {
                    var child = FireProjectile(owner, ProjectileKind.Bolt, proj.Position, Rotate(proj.Velocity, 35f * s), 0.45f, 0.25f, proj.Damage * 0.5f, 0, HitFlags.None);
                    child.HitIds.AddRange(proj.HitIds);
                }
            }
        }

        // ------------------------------------------------------------------ Damage & statuses

        /// <param name="by">The player who caused it (remembered for freeze, stun and mark so a teammate's follow-up counts as teamwork).</param>
        public void ApplyStatus(int index, StatusKind status, float duration, SimPlayer by = null)
        {
            ref var e = ref Enemies[index];
            if (e.Dead || e.Def.Prop) return;
            if (by != null && status is StatusKind.Frozen or StatusKind.Stunned or StatusKind.Marked) e.StatusBy = by.ClientId + 1;
            switch (status)
            {
                case StatusKind.Frozen:
                    e.Frozen = Mathf.Max(e.Frozen, duration);
                    if (e.State == EnemyState.Windup) CancelAttack(ref e);
                    break;
                case StatusKind.Stunned:
                    e.Stunned = Mathf.Max(e.Stunned, duration);
                    if (e.State == EnemyState.Windup) CancelAttack(ref e);
                    break;
                case StatusKind.Marked: e.Marked = Mathf.Max(e.Marked, duration); break;
                case StatusKind.Burning: e.Burning = Mathf.Max(e.Burning, duration); break;
                case StatusKind.Poisoned: e.Poisoned = Mathf.Max(e.Poisoned, duration); break;
                case StatusKind.Cursed: e.Cursed = Mathf.Max(e.Cursed, duration); break;
            }
            Events.Add(new SimEvent(SimEventType.StatusApplied) { Id = e.Id, Int = (int)status, Value = duration });
        }

        /// <param name="damage">Already includes the attacker's Might.</param>
        public void DamageEnemy(int index, float damage, SimPlayer attacker, HitFlags flags, Vector2 pushDirection, float knockback)
        {
            ref var e = ref Enemies[index];
            if (e.Dead || e.Def.Untargetable) return;
            if (e.Disguised)
            {
                WakeMimic(ref e); // the chest was a mimic (RunSimulation.Events.cs)
                return;
            }
            if (e.Type == EnemyType.ForbiddenTome)
            {
                if (attacker != null && (flags & HitFlags.DamageOverTime) == 0) TomeStruck(index); // read, never broken (RunSimulation.Lands2.cs)
                return;
            }

            bool disabled = e.Frozen > 0f || e.Marked > 0f || e.Stunned > 0f;
            bool wasFrozen = e.Frozen > 0f;
            if (attacker != null)
            {
                bool direct = (flags & HitFlags.DamageOverTime) == 0;
                if (direct && disabled && e.StatusBy != 0 && e.StatusBy != attacker.ClientId + 1) flags |= HitFlags.Teamwork;
                if (direct && disabled && IsArmored(e)) flags |= HitFlags.ArmorBreak;
                if ((flags & HitFlags.DamageOverTime) == 0 && RollCrit(attacker, disabled))
                {
                    damage *= CritMultiplier(attacker);
                    flags |= HitFlags.Crit;
                }
                int catalyst = attacker.PerkLevel(PerkId.Catalyst);
                if (catalyst > 0 && (e.Burning > 0f || e.Frozen > 0f)) damage *= 1f + 0.2f * catalyst;
                int resonance = attacker.PerkLevel(PerkId.Resonance);
                if (resonance > 0 && disabled) damage *= 1f + 0.25f * resonance;
                int executioner = attacker.PerkLevel(PerkId.Executioner);
                if (executioner > 0 && e.Health < e.MaxHealth * 0.35f) damage *= 1f + 0.3f * executioner;
                int comradery = attacker.PerkLevel(PerkId.Comradery);
                if (comradery > 0) damage *= 1f + 0.1f * comradery * AlliesNear(attacker, 6f);
                damage *= FavorDamage(attacker, e);
                if (attacker.PerkLevel(PerkId.Bloodlust) > 0 && (flags & HitFlags.DamageOverTime) == 0) attacker.AddBloodlust();
                // Gear effects that ride on the class synergies.
                if (e.Frozen > 0f) damage *= 1f + attacker.Stats.Get(StatType.FrozenDamage);
                if (e.Marked > 0f) damage *= 1f + attacker.Stats.Get(StatType.MarkedDamage);
                float allyBonus = attacker.Stats.Get(StatType.AllyDamage);
                if (allyBonus > 0f) damage *= 1f + allyBonus * AlliesNear(attacker, 6f);
                if (attacker.DodgeStrikeTimer > 0f && (flags & HitFlags.DamageOverTime) == 0)
                {
                    damage *= 1f + attacker.Stats.Get(StatType.DodgeStrike);
                    attacker.DodgeStrikeTimer = 0f;
                }
            }
            if (e.Marked > 0f) damage *= 1f + e.MarkBonus;
            if (e.Cursed > 0f) damage *= 1f + ClassCatalog.CurseDamageBonus; // a deathcaller's curse
            // A dark hall: out of the lantern's light, blows mostly glance off (RunSimulation.Coop.cs).
            if (InDarkness(e) && !e.Def.Prop)
            {
                damage *= DarkDamageFactor;
                flags |= HitFlags.Blocked;
            }
            // Armoured monsters shrug off blows until a teammate freezes, stuns or marks them.
            if (IsArmored(e) && !disabled && (flags & HitFlags.DamageOverTime) == 0)
            {
                damage *= EnemyCatalog.ArmorFactor;
                flags |= HitFlags.Blocked;
            }

            if (e.Frozen > 0f)
            {
                int permafrost = MaxPerkInTeam(PerkId.Permafrost);
                damage *= 1f + 0.15f * permafrost;
                if ((flags & HitFlags.Heavy) != 0)
                {
                    // Synergy: a heavy hit shatters the ice.
                    int crusher = attacker?.PerkLevel(PerkId.Crusher) ?? 0;
                    damage *= ClassCatalog.ShatterMultiplier * (1f + 0.5f * crusher);
                    flags |= HitFlags.Shatter;
                    e.Frozen = 0f;
                    float r = ClassCatalog.ShatterRadius * (1f + 0.3f * crusher);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = attacker?.ClientId ?? 0, Int = (int)AreaKind.Shatter, Pos = e.Position, Value = r });
                    Enemies.QueryCircle(e.Position, r, _splash);
                    int self = index;
                    float splashDamage = damage * 0.4f;
                    Vector2 center = e.Position;
                    foreach (int j in _splash.ToArray())
                        if (j != self) DamageEnemy(j, splashDamage, attacker, HitFlags.None, (Enemies[j].Position - center).normalized, 3f);
                    e = ref Enemies[index];
                }
            }

            if (e.Def.Boss)
            {
                damage = BossDamage(index, damage, ref flags);
                e = ref Enemies[index];
            }
            else if (IsChampionKind(e.Type))
            {
                damage = ChampionDamage(index, damage, ref flags); // RunSimulation.Champions.cs
                e = ref Enemies[index];
            }
            if (e.Affixes != EliteAffix.None)
            {
                damage = AffixDamage(index, damage, ref flags); // RunSimulation.Affixes.cs
                e = ref Enemies[index];
            }

            if (attacker != null && !_ultimateCasting && !e.Def.Prop) GainFocus(attacker, damage * FocusPerDamage); // RunSimulation.Ultimates.cs
            e.Health -= damage;
            Events.Add(new SimEvent(SimEventType.EnemyHit) { Id = e.Id, Pos = e.Position, Value = damage, Int = HitInfo.Pack(flags, attacker) });

            if (!e.Def.Boss && !e.Def.Prop && knockback > 0f && e.State != EnemyState.Strike)
            {
                e.Knockback += pushDirection * knockback * (e.Elite ? 0.5f : 1f);
                e.HitStun = 0.14f;
            }

            if (e.Health > 0f && attacker != null && (flags & HitFlags.DamageOverTime) == 0)
            {
                BlessingOnHit(index, attacker); // RunSimulation.Gods.cs
                e = ref Enemies[index];
            }

            if (e.Health <= 0f)
            {
                e.Dead = true;
                if (attacker != null && !e.Def.Prop)
                {
                    BlessingOnKill(attacker, e.Position, e.Burning > 0f, wasFrozen, _burstDepth);
                    int steel = attacker.PerkLevel(PerkId.BlessedSteel); // paladin: a kill heals the teammates around
                    if (steel > 0)
                        foreach (var mate in Players)
                            if (mate.Alive && (mate.Position - attacker.Position).sqrMagnitude <= 36f) mate.Heal(3f * steel);
                    float heal = 3f * attacker.PerkLevel(PerkId.Lifesteal) + attacker.HealOnKill + attacker.Stats.Get(StatType.KillHeal);
                    if (heal > 0f) attacker.Heal(heal);
                    float killShield = attacker.Stats.Get(StatType.KillShield);
                    if (killShield > 0f && attacker.Alive) attacker.Shield = Mathf.Min(attacker.Shield + killShield, attacker.MaxHp * 0.3f);
                }
            }
        }

        private int AlliesNear(SimPlayer p, float radius)
        {
            int count = 0;
            foreach (var other in Players)
                if (other != p && other.Alive && (other.Position - p.Position).sqrMagnitude <= radius * radius) count++;
            return count;
        }

        private int MaxPerkInTeam(PerkId perk)
        {
            int best = 0;
            foreach (var p in Players) best = Mathf.Max(best, p.PerkLevel(perk));
            return best;
        }

        private void ProcessDeaths(bool giveXp)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (!e.Dead) continue;
                var def = e.Def;
                // Left over from the area just left (whose map may have had more rooms): keep the current room.
                if (e.RoomId >= 0 && e.RoomId < RoomStates.Count) Room = RoomStates[e.RoomId];
                bool vanished = _vanished.Remove(e.Id); // a mimic's chest: no treasure, the mimic stands in its place
                Events.Add(new SimEvent(SimEventType.EnemyDied) { Id = e.Id, Pos = e.Position, Int = (int)e.Type, Value = giveXp && !vanished ? 0f : 1f });
                if (vanished) continue;
                if (def.Friendly)
                {
                    if (giveXp) ServantGone(e); // RunSimulation.Round5.cs
                    continue;
                }
                if (!giveXp) continue;
                if (def.Prop)
                {
                    if (def.Type is EnemyType.PowderKeg or EnemyType.LitKeg) IgniteKeg(e);
                    else if (def.Type == EnemyType.TreasureChest) SpillChest(e);
                    else if (def.Type == EnemyType.CrackedWall) WallBroken(e);
                    else if (def.Type == EnemyType.Captive) CaptiveFreed(e);
                    else if (def.Type == EnemyType.QuenchCistern) CisternBroken(e); // RunSimulation.Lands.cs
                    else if (def.Type == EnemyType.Urn) UrnBroken(e);
                    else if (def.Type == EnemyType.Coffin) CoffinBroken(e); // RunSimulation.Lords2.cs
                    continue;
                }

                if (def.Type == EnemyType.IceMirror)
                {
                    MirrorShattered(e); // a reflection: no reward
                    continue;
                }
                if (ChampionFell(e)) continue; // it will be back (or was only a double): RunSimulation.Champions.cs
                if (e.Affixes != EliteAffix.None) AffixFell(e); // RunSimulation.Affixes.cs
                Kills++;
                LandMonsterFell(e);
                RollLoot(e);
                if (def.Type == EnemyType.TreasureImp) ImpCaught(e);
                if (def.Type == EnemyType.Mimic) MimicSlain(e);
                if (e.Cursed > 0f && e.CursedBy != 0) CursedFell(e); // rises as a servant
                if (!def.Boss && (DifficultyDef.Has(DifficultyMod.Vengeance) || OmenVengeance))
                    QueueBurst(e.Position, DifficultyCatalog.VengeanceRadius, DifficultyCatalog.VengeanceDelay, DifficultyCatalog.VengeanceDamage * EnemyDamageScale, AreaKind.SoulBurst);
                int xp = OmenSouls(def.Xp * (e.Elite ? EnemyCatalog.EliteXpMultiplier : 1));
                if (def.Boss)
                {
                    BossesKilled++;
                    Room.Progress = 1f;
                    // Bosses burst into several souls.
                    for (int s = 0; s < 5; s++) DropSoul(e.Position + Rotate(Vector2.right, s * 72f) * 0.8f, xp / 5);
                }
                else
                {
                    DropSoul(e.Position, xp);
                }
            }
            Enemies.RemoveDead();
        }
    }
}
