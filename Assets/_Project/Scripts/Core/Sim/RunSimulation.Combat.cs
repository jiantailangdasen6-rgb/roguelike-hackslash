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
                if ((p.ComboResetTimer -= dt) <= 0f) p.Combo = 0;

                if (!p.Alive)
                {
                    // Keep counters in sync so presses made while down don't fire on revive.
                    p.LastSkill1 = p.Input.Skill1Count;
                    p.LastSkill2 = p.Input.Skill2Count;
                    p.LastDodge = p.Input.DodgeCount;
                    continue;
                }

                if (p.Input.DodgeCount != p.LastDodge)
                {
                    p.LastDodge = p.Input.DodgeCount;
                    if (p.DodgeCooldown <= 0f) Dodge(p);
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

                if (p.DashSlashTimer > 0f) StepDashSlash(p, dt);
            }
        }

        private void Dodge(SimPlayer p)
        {
            bool evasive = p.PerkLevel(PerkId.Evasive) > 0;
            p.DodgeCooldown = ClassCatalog.DodgeCooldown * (evasive ? 0.6f : 1f);
            p.InvulnerableTimer = Mathf.Max(p.InvulnerableTimer, ClassCatalog.DodgeInvulnerability);
            if (evasive) p.EvasiveTimer = 0.5f;
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.Dodge, Pos = p.Position, Vec = AimDirection(p) });
        }

        private float AttackInterval(SimPlayer p)
        {
            float speed = p.Stats.Get(StatType.AttackSpeed) * (p.EvasiveTimer > 0f ? 2f : 1f);
            return p.Class.BasicInterval / Mathf.Max(0.2f, speed);
        }

        private void Basic(SimPlayer p)
        {
            p.BasicTimer = AttackInterval(p);
            Vector2 dir = AimDirection(p);
            float might = p.Stats.Get(StatType.Might);
            float area = p.Stats.Get(StatType.Area);
            var c = p.Class;

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
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 d = Rotate(dir, (k - (count - 1) * 0.5f) * 7f);
                        FireProjectile(p, ProjectileKind.Arrow, p.Position + d * 0.5f, d * ClassCatalog.ArrowSpeed,
                            c.BasicRange / ClassCatalog.ArrowSpeed, 0.25f, c.BasicDamage * might, pierce, HitFlags.None);
                    }
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.BasicA, Pos = p.Position, Vec = dir });
                    break;
                }
            }
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
                        Position = AimPoint(p, ClassCatalog.FrostNovaRange),
                    });
                    break;
                }
                case ClassId.Ranger:
                    FireProjectile(p, ProjectileKind.PierceArrow, p.Position + dir * 0.5f, dir * ClassCatalog.PierceArrowSpeed,
                        0.6f, 0.45f, ClassCatalog.PierceArrowDamage * might, 99, HitFlags.Heavy);
                    break;
            }

            p.Skill1Cooldown = cd;
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
                    float radius = ClassCatalog.WarCryRadius * area;
                    Enemies.QueryCircle(p.Position, radius, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        ref var e = ref Enemies[i];
                        ApplyStatus(i, StatusKind.Stunned, e.Def.Boss ? 0.35f : 1.1f);
                        DamageEnemy(i, 8f * might, p, HitFlags.None, (e.Position - p.Position).normalized, 9f);
                    }
                    int bulwark = p.PerkLevel(PerkId.Bulwark);
                    foreach (var ally in Players)
                    {
                        if (!ally.Alive || (ally.Position - p.Position).sqrMagnitude > ClassCatalog.WarCryAllyRadius * ClassCatalog.WarCryAllyRadius) continue;
                        ally.GrantValor(ClassCatalog.ValorDuration);
                        if (bulwark > 0) ally.Shield = Mathf.Max(ally.Shield, 25f * bulwark);
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.WarCry, Pos = p.Position, Value = radius });
                    break;
                }
                case ClassId.Mage:
                {
                    float amount = ClassCatalog.HealAmount * (1f + 0.4f * p.PerkLevel(PerkId.Mercy));
                    float r = ClassCatalog.HealRadius * area;
                    foreach (var ally in Players)
                    {
                        if ((ally.Position - p.Position).sqrMagnitude > r * r) continue;
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
                        ApplyStatus(i, StatusKind.Marked, ClassCatalog.MarkDuration);
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Mark, Pos = at, Value = r });
                    break;
                }
            }

            p.Skill2Cooldown = c.Skill2Cooldown * p.CooldownMultiplier;
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.Skill2, Pos = p.Position, Vec = dir });
        }

        private void StepDashSlash(SimPlayer p, float dt)
        {
            p.DashSlashTimer -= dt;
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
                    float duration = ClassCatalog.FreezeDuration + p.PerkLevel(PerkId.Permafrost);
                    Enemies.QueryCircle(area.Position, area.Radius, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        ref var e = ref Enemies[i];
                        DamageEnemy(i, ClassCatalog.FrostNovaDamage * p.Stats.Get(StatType.Might), p, HitFlags.None, Vector2.zero, 0f);
                        if (!e.Dead) ApplyStatus(i, e.Def.Boss ? StatusKind.Stunned : StatusKind.Frozen, e.Def.Boss ? 0.6f : duration);
                    }
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.FrostNova, Pos = area.Position, Value = area.Radius });
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
                else
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

                if (spent || proj.Life <= 0f || !Bounds.Contains(proj.Position) || Arena.BlocksShot(Layout, proj.Position))
                {
                    Events.Add(new SimEvent(SimEventType.ProjectileEnd) { Id = proj.Id, Pos = proj.Position });
                    Projectiles.RemoveAt(k);
                }
            }
        }

        private void OnProjectileHit(Projectile proj, SimPlayer owner, int index)
        {
            Vector2 dir = proj.Velocity.normalized;
            DamageEnemy(index, proj.Damage, owner, proj.Flags, dir, proj.Kind == ProjectileKind.PierceArrow ? 5f : 1.5f);
            if (owner == null) return;
            ref var e = ref Enemies[index];

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

            int chain = owner.PerkLevel(PerkId.ChainBolt);
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
                    lastId = Enemies[next].Id;
                    from = to;
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

        public void ApplyStatus(int index, StatusKind status, float duration)
        {
            ref var e = ref Enemies[index];
            if (e.Dead || e.Def.Prop) return;
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
            }
            Events.Add(new SimEvent(SimEventType.StatusApplied) { Id = e.Id, Int = (int)status, Value = duration });
        }

        /// <param name="damage">Already includes the attacker's Might.</param>
        public void DamageEnemy(int index, float damage, SimPlayer attacker, HitFlags flags, Vector2 pushDirection, float knockback)
        {
            ref var e = ref Enemies[index];
            if (e.Dead) return;

            bool disabled = e.Frozen > 0f || e.Marked > 0f || e.Stunned > 0f;
            if (attacker != null)
            {
                if ((flags & HitFlags.DamageOverTime) == 0 && _combatRng.Chance(attacker.Stats.Get(StatType.CritChance)))
                {
                    damage *= 2f;
                    flags |= HitFlags.Crit;
                }
                int resonance = attacker.PerkLevel(PerkId.Resonance);
                if (resonance > 0 && disabled) damage *= 1f + 0.25f * resonance;
                if (attacker.PerkLevel(PerkId.Bloodlust) > 0 && (flags & HitFlags.DamageOverTime) == 0) attacker.AddBloodlust();
            }
            if (e.Marked > 0f) damage *= 1f + e.MarkBonus;
            // Armoured monsters shrug off blows until a teammate freezes, stuns or marks them.
            if (e.Def.Armored && !disabled && (flags & HitFlags.DamageOverTime) == 0)
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

            e.Health -= damage;
            Events.Add(new SimEvent(SimEventType.EnemyHit) { Id = e.Id, Pos = e.Position, Value = damage, Int = (int)flags });

            if (!e.Def.Boss && !e.Def.Prop && knockback > 0f && e.State != EnemyState.Strike)
            {
                e.Knockback += pushDirection * knockback * (e.Elite ? 0.5f : 1f);
                e.HitStun = 0.14f;
            }

            if (e.Health <= 0f)
            {
                e.Dead = true;
                if (attacker != null && !e.Def.Prop)
                {
                    float heal = 3f * attacker.PerkLevel(PerkId.Lifesteal) + attacker.HealOnKill;
                    if (heal > 0f) attacker.Heal(heal);
                }
            }
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
                Events.Add(new SimEvent(SimEventType.EnemyDied) { Id = e.Id, Pos = e.Position, Int = (int)e.Type });
                if (!giveXp) continue;
                if (def.Prop)
                {
                    if (def.Type == EnemyType.PowderKeg) IgniteKeg(e);
                    continue;
                }

                Kills++;
                int xp = def.Xp * (e.Elite ? EnemyCatalog.EliteXpMultiplier : 1);
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
