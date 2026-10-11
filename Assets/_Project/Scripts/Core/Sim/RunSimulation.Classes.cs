using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The classes unlocked by requests: guardian (shield and hammer), assassin (twin blades) and alchemist (flasks).
    /// Their abilities, the lasting zones they leave (holy ground, burning ground) and the thrown flasks.
    /// </summary>
    public sealed partial class RunSimulation
    {
        private struct Zone
        {
            public AreaKind Kind;
            public SimPlayer Owner;
            public Vector2 Position;
            public float Radius;
            public float Timer;
            public float Tick;
        }

        private readonly List<Zone> _zones = new();

        // ------------------------------------------------------------------ Basic attacks

        private void BasicNewClass(SimPlayer p, Vector2 dir, float might, float area)
        {
            var c = p.Class;
            switch (c.Id)
            {
                case ClassId.Guardian:
                {
                    // Heavy hammer blows: slow, wide, and they shatter frozen enemies.
                    float range = c.BasicRange * area;
                    Enemies.QueryCone(p.Position, dir, range, c.BasicArc, _hits);
                    foreach (int i in _hits.ToArray())
                        DamageEnemy(i, c.BasicDamage * might, p, HitFlags.Heavy, (Enemies[i].Position - p.Position).normalized, 5f);
                    int holy = p.PerkLevel(PerkId.HolyHammer);
                    if (holy > 0 && p.Combo == 2)
                    {
                        float r = 2.2f * area;
                        Enemies.QueryCircle(p.Position + dir * 0.8f, r, _hits);
                        foreach (int i in _hits.ToArray())
                            if (!Enemies[i].Dead) DamageEnemy(i, 14f * holy * might, p, HitFlags.Heavy, (Enemies[i].Position - p.Position).normalized, 4f);
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Slam, Pos = p.Position + dir * 0.8f, Value = r });
                    }
                    p.Combo = (p.Combo + 1) % 3;
                    p.ComboResetTimer = 1.4f;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 2 ? AttackAnim.BasicB : AttackAnim.BasicA), Pos = p.Position, Vec = dir, Value = range });
                    break;
                }
                case ClassId.Assassin:
                {
                    // Quick twin cuts; the class's edge is that crippled enemies always take critical blows.
                    float range = c.BasicRange * area;
                    float damage = c.BasicDamage * might;
                    int poison = p.PerkLevel(PerkId.PoisonBlades);
                    bool twin = p.PerkLevel(PerkId.Twinfang) > 0 && p.Combo == 2;
                    Enemies.QueryCone(p.Position, dir, range, c.BasicArc, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        Vector2 push = (Enemies[i].Position - p.Position).normalized;
                        DamageEnemy(i, damage, p, HitFlags.None, push, 1.5f);
                        if (twin && !Enemies[i].Dead) DamageEnemy(i, damage * 0.8f, p, HitFlags.None, push, 1f);
                        ref var e = ref Enemies[i];
                        if (poison > 0 && !e.Dead)
                        {
                            e.PoisonDps = 3f * poison * might;
                            ApplyStatus(i, StatusKind.Poisoned, 3f);
                        }
                    }
                    p.Combo = (p.Combo + 1) % 3;
                    p.ComboResetTimer = 0.9f;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo % 2 == 0 ? AttackAnim.BasicA : AttackAnim.BasicB), Pos = p.Position, Vec = dir, Value = range });
                    break;
                }
                case ClassId.Alchemist:
                {
                    int count = 1 + p.PerkLevel(PerkId.TwinFlask) + Mathf.RoundToInt(p.Stats.Get(StatType.Amount));
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 d = Rotate(dir, (k - (count - 1) * 0.5f) * 10f);
                        FireProjectile(p, ProjectileKind.Flask, p.Position + d * 0.5f, d * ClassCatalog.FlaskSpeed,
                            c.BasicRange / ClassCatalog.FlaskSpeed, 0.3f, c.BasicDamage * might, 0, HitFlags.None);
                    }
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicA : AttackAnim.BasicB), Pos = p.Position, Vec = dir });
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Skill 1

        /// <returns>The cooldown to use.</returns>
        private float Skill1NewClass(SimPlayer p, Vector2 dir, float cooldown)
        {
            switch (p.Class.Id)
            {
                case ClassId.Guardian:
                    // Like the warrior's dash, the owner moves itself; we sweep the path and stun what it hits.
                    p.BashStuns = true;
                    p.DashSlashTimer = ClassCatalog.ShieldBashDuration + 0.08f;
                    p.DashSlashFrom = p.Position;
                    p.DashSlashDir = dir;
                    p.DashSlashHits.Clear();
                    p.InvulnerableTimer = Mathf.Max(p.InvulnerableTimer, ClassCatalog.ShieldBashDuration + 0.15f);
                    break;
                case ClassId.Assassin:
                    // The owner steps through the shadows on its own client; the slash lands where it arrives.
                    p.ShadowStepTimer = 0.12f;
                    p.InvulnerableTimer = Mathf.Max(p.InvulnerableTimer, 0.35f);
                    cooldown *= 1f - 0.2f * p.PerkLevel(PerkId.ShadowDance);
                    break;
                case ClassId.Alchemist:
                    Throw(p, ProjectileKind.FireFlask);
                    break;
            }
            return cooldown;
        }

        /// <summary>A flask lobbed at the cursor: it flies over everything and breaks where it lands.</summary>
        private void Throw(SimPlayer p, ProjectileKind kind)
        {
            Vector2 target = AimPoint(p, ClassCatalog.ThrowRange);
            Vector2 to = target - p.Position;
            float distance = Mathf.Max(0.3f, to.magnitude);
            Vector2 dir = to.sqrMagnitude > 0.0001f ? to / to.magnitude : AimDirection(p);
            FireProjectile(p, kind, p.Position, dir * ClassCatalog.ThrowSpeed, distance / ClassCatalog.ThrowSpeed, 0f, 0f, 99, HitFlags.None);
        }

        private void StepShieldBash(SimPlayer p, float dt)
        {
            p.DashSlashTimer -= dt;
            if (p.DashSlashTimer <= 0f) p.BashStuns = false;
            int retribution = p.PerkLevel(PerkId.Retribution);
            float damage = ClassCatalog.ShieldBashDamage * p.Stats.Get(StatType.Might) * (1f + 0.4f * retribution);
            Vector2 from = p.DashSlashFrom, to = p.Position;
            Vector2 seg = to - from;
            float len = seg.magnitude;
            Enemies.QueryCircle((from + to) * 0.5f, len * 0.5f + 1.1f, _hits);
            foreach (int i in _hits.ToArray())
            {
                ref var e = ref Enemies[i];
                if (p.DashSlashHits.Contains(e.Id)) continue;
                float t = len > 0.01f ? Mathf.Clamp01(Vector2.Dot(e.Position - from, seg) / (len * len)) : 0f;
                if ((from + seg * t - e.Position).sqrMagnitude > (1.1f + e.Radius) * (1.1f + e.Radius)) continue;
                p.DashSlashHits.Add(e.Id);
                ApplyStatus(i, StatusKind.Stunned, e.Def.Boss ? 0.4f : ClassCatalog.ShieldBashStun + 0.5f * retribution);
                DamageEnemy(i, damage, p, HitFlags.Heavy, p.DashSlashDir, 7f);
            }
        }

        /// <summary>The assassin has arrived: cut everything around.</summary>
        private void ShadowStrike(SimPlayer p)
        {
            float r = ClassCatalog.ShadowStepRadius * p.Stats.Get(StatType.Area);
            float damage = ClassCatalog.ShadowStepDamage * p.Stats.Get(StatType.Might) * (1f + 0.3f * p.PerkLevel(PerkId.ShadowDance));
            Enemies.QueryCircle(p.Position, r, _hits);
            foreach (int i in _hits.ToArray())
                if (!Enemies[i].Dead) DamageEnemy(i, damage, p, HitFlags.Heavy, (Enemies[i].Position - p.Position).normalized, 3f);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.ShadowStep, Pos = p.Position, Value = r });
        }

        // ------------------------------------------------------------------ Skill 2

        private void Skill2NewClass(SimPlayer p, Vector2 dir, float area)
        {
            switch (p.Class.Id)
            {
                case ClassId.Guardian:
                {
                    float r = ClassCatalog.SanctuaryRadius * area * (1f + 0.2f * p.PerkLevel(PerkId.ShieldWall));
                    _zones.Add(new Zone { Kind = AreaKind.Sanctuary, Owner = p, Position = p.Position, Radius = r, Timer = ClassCatalog.SanctuaryDuration });
                    int aegis = p.PerkLevel(PerkId.Aegis);
                    if (aegis > 0)
                        foreach (var ally in Players)
                            if (ally.Alive && (ally.Position - p.Position).sqrMagnitude <= r * r)
                                ally.Shield = Mathf.Max(ally.Shield, 20f * aegis);
                    Events.Add(new SimEvent(SimEventType.AreaEffect)
                    {
                        ClientId = p.ClientId, Int = (int)AreaKind.Sanctuary, Pos = p.Position, Value = r, Size = new Vector2(ClassCatalog.SanctuaryDuration, 0f),
                    });
                    break;
                }
                case ClassId.Assassin:
                {
                    bool veil = p.PerkLevel(PerkId.SmokeVeil) > 0;
                    float r = ClassCatalog.SmokeRadius * area * (veil ? 1.4f : 1f);
                    Enemies.QueryCircle(p.Position, r, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        ref var e = ref Enemies[i];
                        ApplyStatus(i, StatusKind.Stunned, e.Def.Boss ? 0.4f : ClassCatalog.SmokeStun);
                    }
                    p.SureCritTimer = ClassCatalog.SureCritDuration;
                    if (veil)
                        foreach (var ally in Players)
                            if (ally.Alive && (ally.Position - p.Position).sqrMagnitude <= r * r)
                                ally.InvulnerableTimer = Mathf.Max(ally.InvulnerableTimer, 1.5f);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Smoke, Pos = p.Position, Value = r });
                    break;
                }
                case ClassId.Alchemist:
                    Throw(p, ProjectileKind.HealFlask);
                    break;
            }
        }

        // ------------------------------------------------------------------ Flasks breaking

        /// <summary>A thrown or shot flask reached its end (hit, landed, or struck cover).</summary>
        private void BreakFlask(Projectile proj)
        {
            var owner = GetPlayer(proj.Owner);
            if (owner == null) return;
            float area = owner.Stats.Get(StatType.Area);
            switch (proj.Kind)
            {
                case ProjectileKind.Flask:
                {
                    float r = ClassCatalog.FlaskBurstRadius * area * (1f + 0.25f * owner.PerkLevel(PerkId.VolatileMix));
                    Enemies.QueryCircle(proj.Position, r, _hits);
                    foreach (int i in _hits.ToArray())
                        if (!Enemies[i].Dead) DamageEnemy(i, proj.Damage, owner, HitFlags.None, (Enemies[i].Position - proj.Position).normalized, 2f);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = owner.ClientId, Int = (int)AreaKind.FlaskBurst, Pos = proj.Position, Value = r });
                    break;
                }
                case ProjectileKind.FireFlask:
                {
                    int napalm = owner.PerkLevel(PerkId.Napalm);
                    float r = ClassCatalog.FirePoolRadius * area * (1f + 0.15f * napalm);
                    float duration = ClassCatalog.FirePoolDuration + 1.5f * napalm;
                    _zones.Add(new Zone { Kind = AreaKind.FirePool, Owner = owner, Position = proj.Position, Radius = r, Timer = duration });
                    Events.Add(new SimEvent(SimEventType.AreaEffect)
                    {
                        ClientId = owner.ClientId, Int = (int)AreaKind.FirePool, Pos = proj.Position, Value = r, Size = new Vector2(duration, 0f),
                    });
                    break;
                }
                case ProjectileKind.HealFlask:
                {
                    int remedy = owner.PerkLevel(PerkId.Remedy);
                    float r = ClassCatalog.PotionRadius * area;
                    float amount = ClassCatalog.PotionHeal * (1f + 0.4f * remedy);
                    foreach (var ally in Players)
                    {
                        if ((ally.Position - proj.Position).sqrMagnitude > r * r) continue;
                        if (!ally.Alive)
                        {
                            ally.ReviveProgress += 0.5f;
                            continue;
                        }
                        float before = ally.Hp;
                        ally.Heal(amount);
                        if (remedy > 0) ally.Shield = Mathf.Max(ally.Shield, 10f * remedy);
                        Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = ally.ClientId, Pos = ally.Position, Value = ally.Hp - before });
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = owner.ClientId, Int = (int)AreaKind.Potion, Pos = proj.Position, Value = r });
                    break;
                }
            }
        }

        private static bool IsFlask(ProjectileKind kind) => kind is ProjectileKind.Flask or ProjectileKind.FireFlask or ProjectileKind.HealFlask;

        /// <summary>Lobbed flasks fly over monsters; only the basic flask breaks on the first one it meets.</summary>
        private static bool FliesOver(ProjectileKind kind) => kind is ProjectileKind.FireFlask or ProjectileKind.HealFlask;

        // ------------------------------------------------------------------ Lasting zones

        private void StepZones(float dt)
        {
            for (int z = _zones.Count - 1; z >= 0; z--)
            {
                var zone = _zones[z];
                zone.Timer -= dt;
                switch (zone.Kind)
                {
                    case AreaKind.Sanctuary:
                        foreach (var ally in Players)
                            if (ally.Alive && (ally.Position - zone.Position).sqrMagnitude <= zone.Radius * zone.Radius)
                                ally.Heal(ClassCatalog.SanctuaryRegen * dt);
                        break;
                    case AreaKind.FirePool:
                        zone.Tick -= dt;
                        if (zone.Tick <= 0f)
                        {
                            zone.Tick += ClassCatalog.FirePoolTick;
                            float damage = ClassCatalog.FirePoolDamage * (zone.Owner?.Stats.Get(StatType.Might) ?? 1f);
                            Enemies.QueryCircle(zone.Position, zone.Radius, _hits);
                            foreach (int i in _hits.ToArray())
                            {
                                ref var e = ref Enemies[i];
                                if (e.Dead || e.Def.Flying) continue;
                                if (!e.Def.Prop) ApplyStatus(i, StatusKind.Burning, 0.7f);
                                // Kegs catch fire too (DamageEnemy breaks them).
                                DamageEnemy(i, damage, zone.Owner, HitFlags.DamageOverTime, Vector2.zero, 0f);
                            }
                        }
                        break;
                }
                if (zone.Timer <= 0f) _zones.RemoveAt(z);
                else _zones[z] = zone;
            }
        }

        /// <summary>Holy ground: the share of damage a player still takes (1 = all of it).</summary>
        private float SanctuaryFactor(SimPlayer p)
        {
            float best = 0f;
            foreach (var zone in _zones)
            {
                if (zone.Kind != AreaKind.Sanctuary || (p.Position - zone.Position).sqrMagnitude > zone.Radius * zone.Radius) continue;
                float reduction = ClassCatalog.SanctuaryDamageReduction + 0.15f * (zone.Owner?.PerkLevel(PerkId.ShieldWall) ?? 0);
                best = Mathf.Max(best, reduction);
            }
            return 1f - Mathf.Min(0.8f, best);
        }

        /// <summary>Critical hits: the assassin never misses one on crippled prey (or while the smoke lasts).</summary>
        private bool RollCrit(SimPlayer attacker, bool crippled) =>
            (attacker.ClassId == ClassId.Assassin && (crippled || attacker.SureCritTimer > 0f))
            || _combatRng.Chance(attacker.Stats.Get(StatType.CritChance));

        private static float CritMultiplier(SimPlayer attacker) => 2f + 0.5f * attacker.PerkLevel(PerkId.Finisher);
    }
}
