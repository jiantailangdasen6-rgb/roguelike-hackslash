using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Enemy AI: chase or keep distance, then a telegraphed attack (windup → strike → recover).
    /// Every attack is announced with an EnemyTelegraph event showing exactly where it will land.
    /// </summary>
    public sealed partial class RunSimulation
    {
        private const float KnockbackDecay = 9f;

        private void StepEnemies(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead) continue;
                var def = e.Def;

                TickStatuses(i, dt);
                e = ref Enemies[i];
                if (e.Dead) continue;

                // Knockback slides the enemy and fades out.
                if (e.Knockback.sqrMagnitude > 0.0001f)
                {
                    e.Position += e.Knockback * dt;
                    e.Knockback *= Mathf.Exp(-KnockbackDecay * dt);
                }
                e.HitStun -= dt;
                e.Moving = false;

                if (e.Disabled)
                {
                    e.Position = Enemies.ClampToBounds(e.Position, e.Radius);
                    continue;
                }

                var target = NearestLivingPlayer(e.Position);
                switch (e.State)
                {
                    case EnemyState.Chase:
                        Chase(ref e, def, target, dt);
                        break;
                    case EnemyState.Windup:
                        e.StateTimer -= dt;
                        if (e.StateTimer <= 0f) Strike(i, def);
                        break;
                    case EnemyState.Strike:
                        StepLunge(ref e, def, dt);
                        break;
                    case EnemyState.Recover:
                        e.StateTimer -= dt;
                        if (e.StateTimer <= 0f)
                        {
                            e.State = EnemyState.Chase;
                            e.Cooldown = def.AttackCooldown * _aiRng.Range(0.8f, 1.25f) * (BossEnraged(e) ? 0.7f : 1f);
                        }
                        break;
                }

                e = ref Enemies[i];
                if (e.State != EnemyState.Strike) e.Position += Enemies.Separation(i) * dt;
                e.Position = Enemies.ClampToBounds(e.Position, e.Radius);
            }
        }

        private static bool BossEnraged(in Enemy e) => e.Def.Boss && e.Health < e.MaxHealth * 0.5f;

        private SimPlayer NearestLivingPlayer(Vector2 position)
        {
            SimPlayer best = null;
            float bestSq = float.MaxValue;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                float sq = (p.Position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = p;
                }
            }
            return best;
        }

        private void Chase(ref Enemy e, EnemyDef def, SimPlayer target, float dt)
        {
            e.Cooldown -= dt;
            if (target == null) return;

            Vector2 to = target.Position - e.Position;
            float distance = to.magnitude;
            Vector2 dir = distance > 0.001f ? to / distance : Vector2.right;
            if (Mathf.Abs(dir.x) > 0.1f) e.Facing = Mathf.Sign(dir.x);

            int attack = ChooseAttack(ref e, def, distance);
            var a = def.Attacks[attack];

            // Movement: ranged enemies hold their distance, melee ones close in.
            Vector2 move = Vector2.zero;
            if (def.PreferredDistance > 0f)
            {
                if (distance > def.PreferredDistance + 0.8f) move = dir;
                else if (distance < def.PreferredDistance - 1.2f) move = -dir;
                else move = new Vector2(-dir.y, dir.x) * ((e.Id & 1) == 0 ? 0.5f : -0.5f);
            }
            else if (distance > a.Range * 0.8f)
            {
                move = dir;
            }

            float speed = def.Speed * (e.HitStun > 0f ? 0.25f : 1f);
            if (move != Vector2.zero)
            {
                e.Position += move * speed * dt;
                e.Moving = true;
            }

            if (e.Cooldown <= 0f && distance <= a.Range && e.HitStun <= 0f)
                BeginAttack(ref e, def, attack, target);
        }

        /// <summary>Bosses rotate through their moves; others have one attack.</summary>
        private int ChooseAttack(ref Enemy e, EnemyDef def, float distance)
        {
            if (!def.Boss) return 0;
            // 0 cleave, 1 slam, 2 charge, 3 summon
            if (e.AttackCounter % 5 == 4 && Enemies.Count < 6) return 3;
            if (distance > 4.5f) return 2;
            return e.AttackCounter % 2 == 0 ? 0 : 1;
        }

        private void BeginAttack(ref Enemy e, EnemyDef def, int attackIndex, SimPlayer target)
        {
            var a = def.Attacks[attackIndex];
            e.State = EnemyState.Windup;
            e.AttackIndex = attackIndex;
            e.StateTimer = a.Windup * (BossEnraged(e) ? 0.75f : 1f);
            e.AimDirection = (target.Position - e.Position).sqrMagnitude > 0.0001f ? (target.Position - e.Position).normalized : Vector2.right;
            e.AimTarget = target.Position;
            e.AttackCounter++;
            if (Mathf.Abs(e.AimDirection.x) > 0.1f) e.Facing = Mathf.Sign(e.AimDirection.x);

            Vector2 origin = a.Shape == AttackShape.Circle ? e.AimTarget : e.Position;
            Events.Add(new SimEvent(SimEventType.EnemyTelegraph)
            {
                Id = e.Id, Int = (int)a.Shape, Pos = origin, Vec = e.AimDirection,
                Size = new Vector2(a.SizeA, a.SizeB), Value = e.StateTimer,
            });
        }

        private void CancelAttack(ref Enemy e)
        {
            e.State = EnemyState.Chase;
            e.Cooldown = 0.8f;
        }

        private void Strike(int index, EnemyDef def)
        {
            ref var e = ref Enemies[index];
            var a = def.Attacks[e.AttackIndex];
            float damage = a.Damage * e.DamageMultiplier;
            e.State = EnemyState.Recover;
            e.StateTimer = a.Recover;
            Events.Add(new SimEvent(SimEventType.EnemyStrike) { Id = e.Id, Int = (int)a.Kind, Pos = a.Shape == AttackShape.Circle ? e.AimTarget : e.Position, Vec = e.AimDirection });

            switch (a.Kind)
            {
                case AttackKind.Melee:
                    foreach (var p in Players)
                        if (p.Alive && InShape(a, e.Position, e.AimDirection, e.AimTarget, p.Position)) HurtPlayer(p, damage);
                    if (a.Shape != AttackShape.Cone)
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Slam, Pos = a.Shape == AttackShape.Circle ? e.AimTarget : e.Position, Value = a.SizeA });
                    break;
                case AttackKind.Projectile:
                    FireHostile(e.Position + e.AimDirection * 0.5f, e.AimDirection * a.ProjectileSpeed, a.SizeA / a.ProjectileSpeed, damage);
                    break;
                case AttackKind.Lunge:
                    e.State = EnemyState.Strike;
                    e.LungeLeft = a.SizeA;
                    e.HitPlayersMask = 0;
                    break;
                case AttackKind.Summon:
                    Vector2 center = e.Position;
                    for (int s = 0; s < a.SummonCount + Room.Area; s++)
                    {
                        Vector2 at = center + Rotate(Vector2.right, s * 360f / (a.SummonCount + Room.Area) + 30f) * 2.2f;
                        QueueSpawn(s % 2 == 0 ? EnemyType.Skeleton : EnemyType.SkeletonArcher, false, Enemies.ClampToBounds(at, 0.5f));
                    }
                    break;
            }
        }

        private void StepLunge(ref Enemy e, EnemyDef def, float dt)
        {
            var a = def.Attacks[e.AttackIndex];
            float step = Mathf.Min(e.LungeLeft, a.ProjectileSpeed * dt);
            e.LungeLeft -= step;
            Vector2 before = e.Position;
            e.Position = Enemies.ClampToBounds(e.Position + e.AimDirection * step, e.Radius);
            e.Moving = true;
            bool blocked = (e.Position - before).sqrMagnitude < step * step * 0.25f;

            float hitRadius = a.SizeB * 0.5f + SimPlayer.BodyRadius;
            for (int pi = 0; pi < Players.Count; pi++)
            {
                var p = Players[pi];
                if (!p.Alive || (e.HitPlayersMask & (1 << pi)) != 0) continue;
                if ((p.Position - e.Position).sqrMagnitude > hitRadius * hitRadius) continue;
                e.HitPlayersMask |= 1 << pi;
                HurtPlayer(p, a.Damage * e.DamageMultiplier);
            }

            if (e.LungeLeft <= 0f || blocked)
            {
                e.State = EnemyState.Recover;
                e.StateTimer = a.Recover;
            }
        }

        private static bool InShape(EnemyAttack a, Vector2 origin, Vector2 dir, Vector2 target, Vector2 point)
        {
            float r = SimPlayer.BodyRadius;
            switch (a.Shape)
            {
                case AttackShape.Cone:
                {
                    Vector2 to = point - origin;
                    float d = to.magnitude;
                    if (d > a.SizeA + r) return false;
                    if (d < 0.5f) return true;
                    return Vector2.Angle(dir, to) <= a.SizeB * 0.5f + Mathf.Rad2Deg * Mathf.Atan2(r, d);
                }
                case AttackShape.Circle:
                    return (point - target).sqrMagnitude <= (a.SizeA + r) * (a.SizeA + r);
                case AttackShape.SelfCircle:
                    return (point - origin).sqrMagnitude <= (a.SizeA + r) * (a.SizeA + r);
                default:
                {
                    Vector2 to = point - origin;
                    float along = Vector2.Dot(to, dir);
                    float across = Mathf.Abs(dir.x * to.y - dir.y * to.x);
                    return along >= -r && along <= a.SizeA + r && across <= a.SizeB * 0.5f + r;
                }
            }
        }

        private void TickStatuses(int index, float dt)
        {
            ref var e = ref Enemies[index];
            e.Frozen -= dt;
            e.Stunned -= dt;
            e.Marked -= dt;

            float dot = 0f;
            if (e.Burning > 0f)
            {
                e.Burning -= dt;
                dot += e.BurnDps * dt;
            }
            if (e.Poisoned > 0f)
            {
                e.Poisoned -= dt;
                dot += e.PoisonDps * dt;
            }
            if (dot <= 0f) return;

            // Apply damage-over-time in chunks so the event stream stays small.
            e.DotAccumulator += dot;
            if (e.DotAccumulator >= 3f || (e.Burning <= 0f && e.Poisoned <= 0f))
            {
                float amount = e.DotAccumulator;
                e.DotAccumulator = 0f;
                DamageEnemy(index, amount, null, HitFlags.DamageOverTime, Vector2.zero, 0f);
            }
        }

        // ------------------------------------------------------------------ Damage to players

        public void HurtPlayer(SimPlayer p, float damage)
        {
            if (!p.Alive || p.InvulnerableTimer > 0f) return;
            float dealt = Mathf.Max(1f, damage - p.Stats.Get(StatType.Armor)) * Mathf.Max(0.2f, p.Stats.Get(StatType.DamageTaken));

            if (p.Shield > 0f)
            {
                float absorbed = Mathf.Min(p.Shield, dealt);
                p.Shield -= absorbed;
                dealt -= absorbed;
            }

            p.Hp -= dealt;
            p.InvulnerableTimer = HitInvulnerability;
            Events.Add(new SimEvent(SimEventType.PlayerHurt) { ClientId = p.ClientId, Pos = p.Position, Value = dealt });
            if (p.Hp > 0f) return;

            if (p.PerkLevel(PerkId.SecondWind) > 0 && !p.SecondWindUsed)
            {
                p.SecondWindUsed = true;
                p.Hp = 1f;
                p.InvulnerableTimer = 1.2f;
                return;
            }
            if (p.Phoenix && !p.PhoenixUsed)
            {
                p.PhoenixUsed = true;
                Revive(p, 0.5f, guardian: true);
                return;
            }

            p.Hp = 0f;
            p.Downed = true;
            p.ReviveProgress = 0f;
            p.Shield = 0f;
            p.DashSlashTimer = 0f;
            Events.Add(new SimEvent(SimEventType.PlayerDowned) { ClientId = p.ClientId, Pos = p.Position });
        }
    }
}
