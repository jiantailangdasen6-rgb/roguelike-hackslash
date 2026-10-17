using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The charged strike (溜め攻撃): holding the attack keeps the blows coming as before, and charges as well; let go
    /// after <see cref="ChargeOne"/> seconds and a heavy strike follows (stronger after <see cref="ChargeTwo"/>). Close
    /// fighters cleave a wide arc around them, the others loose a heavy piercing wave. It wears stances down hard.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float ChargeOne = 1.2f;
        public const float ChargeTwo = 2.4f;

        public static bool IsMeleeClass(ClassId c) => c is ClassId.Warrior or ClassId.Guardian or ClassId.Assassin or ClassId.Paladin;

        /// <summary>Each tick: the hold charges; letting go of a charged hold strikes.</summary>
        private void StepCharge(SimPlayer p, float dt)
        {
            if (p.Input.Attack && p.CagedBy == 0)
            {
                p.AttackHold += dt;
                return;
            }
            float held = p.AttackHold;
            p.AttackHold = 0f;
            if (held >= ChargeOne) ChargedStrike(p, held >= ChargeTwo ? 2 : 1);
        }

        private void ChargedStrike(SimPlayer p, int level)
        {
            Vector2 dir = AimDirection(p);
            float might = p.Stats.Get(StatType.Might);
            float area = p.Stats.Get(StatType.Area);
            float damage = p.Class.BasicDamage * might * (level >= 2 ? 5f : 3f);
            float reach;
            if (IsMeleeClass(p.ClassId))
            {
                reach = Mathf.Max(2.2f, p.Class.BasicRange) * area * (level >= 2 ? 1.9f : 1.5f);
                Enemies.QueryCone(p.Position, dir, reach, level >= 2 ? 360f : 200f, _hits);
                foreach (int i in _hits.ToArray())
                {
                    if (Enemies[i].Dead || Enemies[i].Def.Friendly) continue;
                    DamageEnemy(i, damage, p, HitFlags.Heavy, (Enemies[i].Position - p.Position).normalized, level >= 2 ? 10f : 7f);
                }
            }
            else
            {
                reach = 12f;
                float width = level >= 2 ? 1.1f : 0.8f;
                FireProjectile(p, ProjectileKind.SwordWave, p.Position + dir * 0.6f, dir * 16f, 0.75f, width, damage, 99, HitFlags.Heavy);
            }
            Events.Add(new SimEvent(SimEventType.ChargedStrike) { ClientId = p.ClientId, Int = level, Pos = p.Position, Vec = dir, Value = reach });
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(IsMeleeClass(p.ClassId) && level >= 2 ? AttackAnim.Spin : AttackAnim.BasicB), Pos = p.Position, Vec = dir, Value = reach });
        }
    }
}
