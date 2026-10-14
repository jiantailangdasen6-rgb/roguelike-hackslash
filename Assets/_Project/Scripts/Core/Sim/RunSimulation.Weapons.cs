using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Each class's other weapon (ClassCatalog.AltWeapons, worn as "weapon_alt" gear): its own basic attack. The warrior's
    /// great axe and the paladin's greatsword swing slow and heavy, the mage's tome looses three bolts, the ranger's
    /// crossbow a heavy piercing bolt, the guardian's lance thrusts along a line, the assassin throws daggers, the
    /// alchemist's bombs burst wide and the deathcaller's scythe sweeps and drinks a little life.
    /// </summary>
    public sealed partial class RunSimulation
    {
        private void BasicAlt(SimPlayer p, Vector2 dir, float might, float area)
        {
            var w = ClassCatalog.AltWeapon(p.ClassId);
            float damage = w.Damage * might;
            switch (p.ClassId)
            {
                case ClassId.Warrior:
                case ClassId.Paladin:
                {
                    float range = w.Range * area;
                    Enemies.QueryCone(p.Position, dir, range, w.Arc, _hits);
                    int hit = 0;
                    foreach (int i in _hits.ToArray())
                    {
                        DamageEnemy(i, damage, p, HitFlags.Heavy, (Enemies[i].Position - p.Position).normalized, 8f);
                        hit++;
                    }
                    // The greatsword: each blow that lands hardens the bearer a little.
                    if (p.ClassId == ClassId.Paladin && hit > 0) p.Shield = Mathf.Min(30f, p.Shield + 4f);
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicB : AttackAnim.BasicA), Pos = p.Position, Vec = dir, Value = range });
                    break;
                }
                case ClassId.Mage:
                {
                    int count = 3 + p.PerkLevel(PerkId.Multicast);
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 d = Rotate(dir, (k - (count - 1) * 0.5f) * (w.Arc / (count - 1)));
                        var bolt = FireProjectile(p, ProjectileKind.Bolt, p.Position + d * 0.5f, d * ClassCatalog.BoltSpeed,
                            w.Range / ClassCatalog.BoltSpeed, 0.3f, damage, 0, HitFlags.None);
                        bolt.Splits = p.PerkLevel(PerkId.SplitBolt) > 0;
                    }
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicA : AttackAnim.BasicB), Pos = p.Position, Vec = dir });
                    break;
                }
                case ClassId.Ranger:
                {
                    int pierce = 1 + p.PerkLevel(PerkId.Piercing);
                    FireProjectile(p, ProjectileKind.PierceArrow, p.Position + dir * 0.5f, dir * ClassCatalog.PierceArrowSpeed,
                        w.Range / ClassCatalog.PierceArrowSpeed, 0.3f, damage, pierce, HitFlags.Heavy);
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.BasicA, Pos = p.Position, Vec = dir });
                    break;
                }
                case ClassId.Guardian:
                {
                    // The lance: everything on a line in front.
                    float length = w.Range * area, half = w.Arc * 0.5f;
                    Enemies.QueryCircle(p.Position + dir * length * 0.5f, length * 0.5f + half + 0.6f, _hits);
                    foreach (int i in _hits.ToArray())
                    {
                        Vector2 to = Enemies[i].Position - p.Position;
                        float along = Vector2.Dot(to, dir), across = Mathf.Abs(dir.x * to.y - dir.y * to.x);
                        if (along < -0.3f || along > length + Enemies[i].Radius || across > half + Enemies[i].Radius) continue;
                        DamageEnemy(i, damage, p, HitFlags.None, dir, 3f);
                    }
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)AttackAnim.BasicA, Pos = p.Position, Vec = dir, Value = length });
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Thrust, Pos = p.Position + dir * 0.4f, Vec = dir, Value = length });
                    break;
                }
                case ClassId.Assassin:
                {
                    int count = 1 + Mathf.RoundToInt(p.Stats.Get(StatType.Amount));
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 d = Rotate(dir, (k - (count - 1) * 0.5f) * 8f);
                        FireProjectile(p, ProjectileKind.Dagger, p.Position + d * 0.5f, d * 17f, w.Range / 17f, 0.25f, damage, 0, HitFlags.None);
                    }
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicA : AttackAnim.BasicB), Pos = p.Position, Vec = dir });
                    break;
                }
                case ClassId.Alchemist:
                    FireProjectile(p, ProjectileKind.Bomb, p.Position + dir * 0.5f, dir * 10f, w.Range / 10f, 0.35f, damage, 0, HitFlags.None);
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicA : AttackAnim.BasicB), Pos = p.Position, Vec = dir });
                    break;
                case ClassId.Deathcaller:
                {
                    float range = w.Range * area;
                    Enemies.QueryCone(p.Position, dir, range, w.Arc, _hits);
                    int hit = 0;
                    foreach (int i in _hits.ToArray())
                    {
                        DamageEnemy(i, damage, p, HitFlags.None, (Enemies[i].Position - p.Position).normalized, 4f);
                        hit++;
                    }
                    // The scythe drinks a little of what it reaps.
                    if (hit > 0)
                    {
                        float before = p.Hp;
                        p.Heal(p.MaxHp * 0.015f * Mathf.Min(hit, 4));
                        if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
                    }
                    p.Combo = (p.Combo + 1) % 2;
                    Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicB : AttackAnim.BasicA), Pos = p.Position, Vec = dir, Value = range });
                    break;
                }
            }
        }

        /// <summary>The alchemist's bomb bursts wide and heavy (BreakFlask).</summary>
        private void BurstBomb(Projectile proj, SimPlayer owner)
        {
            float r = ClassCatalog.AltWeapon(ClassId.Alchemist).Arc * owner.Stats.Get(StatType.Area) * (1f + 0.25f * owner.PerkLevel(PerkId.VolatileMix));
            Enemies.QueryCircle(proj.Position, r, _hits);
            foreach (int i in _hits.ToArray())
                if (!Enemies[i].Dead) DamageEnemy(i, proj.Damage, owner, HitFlags.Heavy, (Enemies[i].Position - proj.Position).normalized, 6f);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = owner.ClientId, Int = (int)AreaKind.Explosion, Pos = proj.Position, Value = r });
        }
    }
}
