using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Union techniques (UnionCatalog): two players who use a skill (either one) within <see cref="UnionWindow"/> of
    /// each other and no further apart than <see cref="UnionRange"/> join them into one blow between them - a burst
    /// carrying both classes' touch (ice, fire, stun, the mark, the curse, holy light...) - and are each shielded.
    /// Each player can take part in one union every <see cref="UnionCooldown"/> seconds.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float UnionWindow = 0.8f;
        public const float UnionRange = 6f;
        public const float UnionCooldown = 10f;
        public const float UnionRadius = 3.4f;
        public const float UnionDamage = 70f;
        public const float UnionShield = 15f;

        /// <summary>Right after a player's skill: does it meet a friend's?</summary>
        private void TryUnion(SimPlayer p)
        {
            float now = Elapsed;
            if (now < p.UnionReadyAt)
            {
                p.LastSkillAt = now;
                return;
            }
            foreach (var q in Players)
            {
                if (q == p || !q.Alive || now - q.LastSkillAt > UnionWindow || now < q.UnionReadyAt) continue;
                if ((q.Position - p.Position).sqrMagnitude > UnionRange * UnionRange) continue;
                Union(p, q);
                GainFocus(p, FocusUnion);
                GainFocus(q, FocusUnion);
                p.LastSkillAt = q.LastSkillAt = -99f;
                p.UnionReadyAt = q.UnionReadyAt = now + UnionCooldown;
                return;
            }
            p.LastSkillAt = now;
        }

        private void Union(SimPlayer p, SimPlayer q)
        {
            Vector2 at = (p.Position + q.Position) * 0.5f;
            int pair = UnionCatalog.Pair(p.ClassId, q.ClassId);
            float might = (p.Stats.Get(StatType.Might) + q.Stats.Get(StatType.Might)) * 0.5f;
            Enemies.QueryCircle(at, UnionRadius, _hits);
            foreach (int i in _hits.ToArray())
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Def.Friendly) continue;
                Touch(i, p.ClassId, p);
                Touch(i, q.ClassId, q);
                if (Enemies[i].Dead) continue;
                DamageEnemy(i, UnionDamage * might, p, HitFlags.Heavy | HitFlags.Teamwork, (Enemies[i].Position - at).normalized, 5f);
            }
            foreach (var ally in Players)
            {
                if (!ally.Alive) continue;
                bool caster = ally == p || ally == q;
                bool inside = (ally.Position - at).sqrMagnitude <= UnionRadius * UnionRadius;
                if (caster) ally.Shield = Mathf.Max(ally.Shield, UnionShield);
                // Holy classes in the pair mend everyone standing in the light.
                if (inside && (p.ClassId == ClassId.Paladin || q.ClassId == ClassId.Paladin || p.ClassId == ClassId.Guardian && q.ClassId == ClassId.Guardian))
                    ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + ally.MaxHp * 0.12f);
            }
            Events.Add(new SimEvent(SimEventType.Combo)
            {
                ClientId = p.ClientId, Int = (int)ComboKind.Union, Pos = at, Value = UnionRadius, Id = pair,
                Vec = q.Position, Size = p.Position,
            });
        }

        /// <summary>What each class brings to a union.</summary>
        private void Touch(int i, ClassId c, SimPlayer by)
        {
            bool boss = Enemies[i].Def.Boss;
            float k = boss ? 0.3f : 1f;
            switch (c)
            {
                case ClassId.Warrior: ApplyStatus(i, StatusKind.Stunned, 1.2f * k, by); break;
                case ClassId.Mage: ApplyStatus(i, StatusKind.Frozen, 2f * k, by); break;
                case ClassId.Ranger: ApplyStatus(i, StatusKind.Marked, 5f, by); break;
                case ClassId.Guardian: ApplyStatus(i, StatusKind.Stunned, 1.5f * k, by); break;
                case ClassId.Assassin: ApplyStatus(i, StatusKind.Marked, 4f, by); break;
                case ClassId.Alchemist: ApplyStatus(i, StatusKind.Burning, 4f, by); break;
                case ClassId.Deathcaller: ApplyStatus(i, StatusKind.Cursed, 5f, by); break;
                case ClassId.Paladin: ApplyStatus(i, StatusKind.Stunned, 0.8f * k, by); break;
            }
        }
    }
}
