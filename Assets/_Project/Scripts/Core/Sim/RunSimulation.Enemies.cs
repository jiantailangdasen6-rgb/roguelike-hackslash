using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Enemy AI: chase or keep distance, then a telegraphed attack (windup → strike → recover).
    /// Every attack is announced with an EnemyTelegraph event showing exactly where it will land.
    /// Walkers slide around obstacles; flyers (bats, wraiths, the Lich) ignore them.
    /// </summary>
    public sealed partial class RunSimulation
    {
        private const float KnockbackDecay = 9f;
        private const float PillarStun = 1.6f;
        private const int SummonCap = 14;

        /// <summary>Rain attacks drop several circles; their centres are kept here until the strike.</summary>
        private readonly Dictionary<int, List<Vector2>> _rainTargets = new();

        private void StepEnemies(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead) continue;
                var def = e.Def;
                if (def.Prop) continue;
                Room = RoomStates[e.RoomId];

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
                    e.Position = Confine(e.Position, e.Radius, def.Flying);
                    continue;
                }
                if (def.Boss && BossChannelling)
                {
                    e.State = EnemyState.Chase;
                    e.Moving = false;
                    continue;
                }

                var target = NearestLivingPlayer(e.Position, e.RoomId);
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
                            e.Cooldown = def.AttackCooldown * _aiRng.Range(0.8f, 1.25f) * (BossEnraged(e) ? 0.7f : 1f) * (def.Boss && BossWarded ? 1.3f : 1f);
                        }
                        break;
                }

                e = ref Enemies[i];
                if (e.Dead) continue;
                if (e.State != EnemyState.Strike) e.Position += Enemies.Separation(i) * dt;
                e.Position = Confine(e.Position, e.Radius, def.Flying);
            }
        }

        private Vector2 Confine(Vector2 p, float radius, bool flying)
        {
            // Flyers may hover over the fallen floor; walkers keep to what is left of it.
            p = flying ? new Vector2(Mathf.Clamp(p.x, Bounds.xMin + radius, Bounds.xMax - radius), Mathf.Clamp(p.y, Bounds.yMin + radius, Bounds.yMax - radius)) : ClampToRoom(p, radius);
            return flying ? p : Arena.Resolve(Layout, p, radius);
        }

        private static bool BossEnraged(in Enemy e) => e.Def.Boss && e.Health < e.MaxHealth * 0.5f;

        /// <summary>Monsters only go after players in their own room (the others are elsewhere on the map).</summary>
        private SimPlayer NearestLivingPlayer(Vector2 position, int room)
        {
            SimPlayer best = null;
            float bestSq = float.MaxValue;
            foreach (var p in Players)
            {
                if (!p.Alive || p.RoomId != room) continue;
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
            else if (distance > Mathf.Min(a.Range, 4f) * 0.8f)
            {
                move = dir;
            }

            float speed = def.Speed * (e.HitStun > 0f ? 0.25f : 1f) * (DifficultyDef.Has(DifficultyMod.Swiftness) ? DifficultyCatalog.SwiftnessSpeed : 1f);
            if (move != Vector2.zero)
            {
                if (!def.Flying) move = Arena.Steer(Layout, e.Position, move, e.Radius, e.Id);
                e.Position += move * speed * dt;
                e.Moving = true;
            }

            if (e.Cooldown <= 0f && distance <= a.Range && e.HitStun <= 0f)
                BeginAttack(ref e, def, attack, target);
        }

        /// <summary>Which move comes next. Bosses run through patterns; others pick by distance.</summary>
        private int ChooseAttack(ref Enemy e, EnemyDef def, float distance)
        {
            int n = e.AttackCounter;
            bool roomForMinions = Enemies.HostileCount < 7;
            switch (def.Type)
            {
                case EnemyType.BoneKing:
                    // 0 cleave, 1 slam, 2 charge, 3 summon, 4 bone cage
                    if (n % 5 == 4 && roomForMinions) return 3;
                    if (n % 5 == 2) return 4;
                    if (distance > 4.5f) return 2;
                    return n % 2 == 0 ? 0 : 1;
                case EnemyType.BloodBishop:
                    // 0 blood fan, 1 blood rain, 2 summon, 3 blink
                    switch (n % 6)
                    {
                        case 1: case 4: return 1;
                        case 3: return 3;
                        case 5: return roomForMinions ? 2 : 0;
                        default: return 0;
                    }
                case EnemyType.Lich:
                    // 0 soul ring, 1 death cone, 2 soul rain, 3 summon, 4 nova
                    if (distance < 3f && n % 2 == 0) return 4;
                    switch (n % 5)
                    {
                        case 1: case 3: return 2;
                        case 2: return distance < 6f ? 1 : 0;
                        case 4: return roomForMinions ? 3 : 0;
                        default: return 0;
                    }
                case EnemyType.DreadKnight:
                    return distance > def.Attacks[0].Range + 0.3f && n % 2 == 1 ? 1 : 0;
                case EnemyType.Necromancer:
                    return n % 3 == 2 && Enemies.HostileCount < SummonCap ? 1 : 0;
                default:
                    return 0;
            }
        }

        private void BeginAttack(ref Enemy e, EnemyDef def, int attackIndex, SimPlayer target)
        {
            var a = def.Attacks[attackIndex];
            e.State = EnemyState.Windup;
            e.AttackIndex = attackIndex;
            e.StateTimer = a.Windup * (BossEnraged(e) ? 0.75f : 1f) * (DifficultyDef.Has(DifficultyMod.Ferocity) ? DifficultyCatalog.FerocityWindup : 1f);
            e.AimDirection = (target.Position - e.Position).sqrMagnitude > 0.0001f ? (target.Position - e.Position).normalized : Vector2.right;
            e.AimTarget = target.Position;
            e.AttackCounter++;
            if (Mathf.Abs(e.AimDirection.x) > 0.1f) e.Facing = Mathf.Sign(e.AimDirection.x);

            switch (a.Kind)
            {
                case AttackKind.Cage:
                {
                    var victim = RandomLivingPlayer() ?? target;
                    e.AimTarget = victim.Position;
                    Telegraph(e, AttackShape.Circle, e.AimTarget, e.AimDirection, a);
                    return;
                }
                case AttackKind.Rain:
                {
                    var spots = new List<Vector2>();
                    foreach (var p in Players)
                        if (p.Alive && p.RoomId == e.RoomId) spots.Add(p.Position);
                    for (int k = 0; k < a.Extra; k++)
                    {
                        var near = spots.Count > 0 ? spots[_aiRng.Range(0, spots.Count)] : e.Position;
                        spots.Add(ClampToRoom(near + Rotate(Vector2.right, _aiRng.Range(0f, 360f)) * _aiRng.Range(2f, 4.5f), 1f));
                    }
                    _rainTargets[e.Id] = spots;
                    foreach (var s in spots) Telegraph(e, AttackShape.Circle, s, e.AimDirection, a);
                    return;
                }
                case AttackKind.Blink:
                    // Wraiths appear right where the target stands; the bishop jumps to a spot away from the group.
                    e.AimTarget = def.Boss ? BlinkSpot(target.Position) : target.Position;
                    Telegraph(e, AttackShape.Circle, e.AimTarget, e.AimDirection, a);
                    return;
                case AttackKind.Projectile when a.Count > 1:
                    if (a.Spread >= 360f)
                    {
                        Telegraph(e, AttackShape.SelfCircle, e.Position, e.AimDirection, a);
                        return;
                    }
                    for (int k = 0; k < a.Count; k += a.Count > 4 ? 2 : 1)
                        Telegraph(e, AttackShape.Line, e.Position, Rotate(e.AimDirection, FanAngle(a, k)), a);
                    return;
            }

            Vector2 origin = a.Shape == AttackShape.Circle ? e.AimTarget : e.Position;
            Telegraph(e, a.Shape, origin, e.AimDirection, a);
        }

        private void Telegraph(in Enemy e, AttackShape shape, Vector2 origin, Vector2 direction, EnemyAttack a)
        {
            Events.Add(new SimEvent(SimEventType.EnemyTelegraph)
            {
                Id = e.Id, Int = (int)shape, Pos = origin, Vec = direction,
                Size = new Vector2(a.SizeA, a.SizeB), Value = e.StateTimer,
            });
        }

        private static float FanAngle(EnemyAttack a, int k) =>
            a.Spread >= 360f ? k * 360f / a.Count : a.Count <= 1 ? 0f : (k / (a.Count - 1f) - 0.5f) * a.Spread;

        private Vector2 BlinkSpot(Vector2 near)
        {
            Vector2 best = near;
            float bestScore = -1f;
            for (int k = 0; k < 8; k++)
            {
                var p = ClampToRoom(near + Rotate(Vector2.right, _aiRng.Range(0f, 360f)) * _aiRng.Range(4.5f, 7f), 1.5f);
                if (Arena.Overlaps(Layout, p, 1f)) continue;
                float score = (p - near).magnitude;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        private void CancelAttack(ref Enemy e)
        {
            e.State = EnemyState.Chase;
            e.Cooldown = 0.8f;
            _rainTargets.Remove(e.Id);
        }

        private void Strike(int index, EnemyDef def)
        {
            ref var e = ref Enemies[index];
            var a = def.Attacks[e.AttackIndex];
            float damage = a.Damage * e.DamageMultiplier;
            e.State = EnemyState.Recover;
            e.StateTimer = a.Recover;
            Vector2 strikeAt = a.Shape == AttackShape.Circle || a.Kind == AttackKind.Blink ? e.AimTarget : e.Position;
            Events.Add(new SimEvent(SimEventType.EnemyStrike) { Id = e.Id, Int = (int)a.Kind, Pos = strikeAt, Vec = e.AimDirection });

            switch (a.Kind)
            {
                case AttackKind.Melee:
                    foreach (var p in Players)
                        if (p.Alive && InShape(a, e.Position, e.AimDirection, e.AimTarget, p.Position)) HurtPlayer(p, damage);
                    if (a.Shape != AttackShape.Cone)
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Slam, Pos = strikeAt, Value = a.SizeA });
                    break;
                case AttackKind.Projectile:
                    for (int k = 0; k < a.Count; k++)
                    {
                        Vector2 d = Rotate(e.AimDirection, FanAngle(a, k));
                        float life = (a.Spread >= 360f ? 12f : a.SizeA) / a.ProjectileSpeed;
                        FireHostile(a.Projectile, e.Position + d * 0.5f, d * a.ProjectileSpeed, life, damage);
                    }
                    break;
                case AttackKind.Lunge:
                    e.State = EnemyState.Strike;
                    e.LungeLeft = a.SizeA;
                    e.HitPlayersMask = 0;
                    break;
                case AttackKind.Summon:
                {
                    Vector2 center = e.Position;
                    int count = a.SummonCount + (def.Boss ? Room.Area : 0);
                    for (int s = 0; s < count; s++)
                    {
                        Vector2 at = center + Rotate(Vector2.right, s * 360f / count + 30f) * 2.2f;
                        at = Arena.Resolve(Layout, ClampToRoom(at, 0.5f), 0.5f);
                        QueueSpawn(s % 2 == 0 ? a.Summon1 : a.Summon2, false, at);
                    }
                    break;
                }
                case AttackKind.Cage:
                    CloseCages(e.AimTarget, a.SizeA);
                    break;
                case AttackKind.Rain:
                    if (_rainTargets.Remove(e.Id, out var spots))
                        foreach (var s in spots)
                        {
                            foreach (var p in Players)
                                if (p.Alive && (p.Position - s).sqrMagnitude <= (a.SizeA + SimPlayer.BodyRadius) * (a.SizeA + SimPlayer.BodyRadius))
                                    HurtPlayer(p, damage);
                            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Slam, Pos = s, Value = a.SizeA });
                        }
                    break;
                case AttackKind.Blink:
                    e.Position = Confine(e.AimTarget, e.Radius, def.Flying);
                    foreach (var p in Players)
                        if (p.Alive && (p.Position - e.AimTarget).sqrMagnitude <= (a.SizeA + SimPlayer.BodyRadius) * (a.SizeA + SimPlayer.BodyRadius))
                            HurtPlayer(p, damage);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Slam, Pos = e.AimTarget, Value = a.SizeA });
                    break;
                case AttackKind.SelfDestruct:
                {
                    foreach (var p in Players)
                        if (p.Alive && InShape(a, e.Position, e.AimDirection, e.AimTarget, p.Position)) HurtPlayer(p, damage);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Explosion, Pos = e.Position, Value = a.SizeA });
                    Vector2 center = e.Position;
                    int self = e.Id;
                    e.Dead = true;
                    if (a.HitsEnemies)
                    {
                        Enemies.QueryCircle(center, a.SizeA, _splash);
                        foreach (int j in _splash.ToArray())
                        {
                            if (Enemies[j].Id == self) continue;
                            Vector2 push = (Enemies[j].Position - center).sqrMagnitude > 0.0001f ? (Enemies[j].Position - center).normalized : Vector2.up;
                            DamageEnemy(j, a.Damage * 2.5f * DifficultyDef.EnemyDamage, null, HitFlags.Heavy, push, 7f);
                        }
                    }
                    break;
                }
            }
        }

        private void StepLunge(ref Enemy e, EnemyDef def, float dt)
        {
            var a = def.Attacks[e.AttackIndex];
            float step = Mathf.Min(e.LungeLeft, a.ProjectileSpeed * dt);
            e.LungeLeft -= step;
            Vector2 before = e.Position;
            Vector2 next = ClampToRoom(e.Position + e.AimDirection * step, e.Radius);
            bool hitPillar = !def.Flying && Arena.Overlaps(Layout, next, e.Radius);
            e.Position = def.Flying ? next : Arena.Resolve(Layout, next, e.Radius);
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
                // Charging into a pillar knocks the charger senseless — lure big ones into them.
                if (hitPillar && blocked && e.Def.Boss)
                {
                    e.Stunned = Mathf.Max(e.Stunned, PillarStun);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Slam, Pos = e.Position + e.AimDirection * e.Radius, Value = 1.2f });
                    Events.Add(new SimEvent(SimEventType.StatusApplied) { Id = e.Id, Int = (int)StatusKind.Stunned, Value = PillarStun });
                }
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
            dealt *= SanctuaryFactor(p); // a guardian's holy ground
            if (p.Hp < p.MaxHp * 0.5f) dealt *= 1f - Mathf.Min(0.6f, p.Stats.Get(StatType.LastStand)); // gear

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
