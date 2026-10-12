using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Combination techniques: two classes' abilities meeting on the field make something new.
    /// Frost nova on burning ground (steam burst), shadow step onto a marked enemy (execution), war cry inside
    /// holy ground (every ally in it shielded, the ground renewed), shield charge through burning ground (flames).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float SteamDamage = 40f;
        public const float SteamStun = 1.6f;
        public const float ExecutionMultiplier = 2.5f;
        public const float HolyWarCryShield = 30f;
        public const float HolyWarCryExtend = 2f;
        public const float FlameChargeMultiplier = 1.5f;
        public const float FlameChargeBurn = 3f;

        private void Combo(SimPlayer by, ComboKind kind, Vector2 at, float radius) =>
            Events.Add(new SimEvent(SimEventType.Combo) { ClientId = by.ClientId, Int = (int)kind, Pos = at, Value = radius });

        /// <summary>Mage + alchemist: ice meeting burning ground bursts into scalding steam that stuns.</summary>
        private void SteamBursts(SimPlayer caster, Vector2 center, float radius)
        {
            float might = caster.Stats.Get(StatType.Might);
            for (int z = _zones.Count - 1; z >= 0; z--)
            {
                var zone = _zones[z];
                if (zone.Kind != AreaKind.FirePool) continue;
                float reach = radius + zone.Radius;
                if ((zone.Position - center).sqrMagnitude > reach * reach) continue;
                _zones.RemoveAt(z); // the fire is put out
                float r = zone.Radius + 1.4f;
                Enemies.QueryCircle(zone.Position, r, _hits);
                foreach (int i in _hits.ToArray())
                {
                    ref var e = ref Enemies[i];
                    if (e.Dead) continue;
                    // Ice from the nova melts into the steam: they are stunned instead of frozen.
                    e.Frozen = 0f;
                    ApplyStatus(i, StatusKind.Stunned, e.Def.Boss ? 0.5f : SteamStun, caster);
                    DamageEnemy(i, SteamDamage * might, caster, HitFlags.Heavy, (e.Position - zone.Position).normalized, 6f);
                }
                Combo(caster, ComboKind.SteamBurst, zone.Position, r);
            }
        }

        /// <summary>Assassin + ranger: arriving on a marked enemy cuts it far deeper and the step is ready again.</summary>
        private bool ShadowExecution(SimPlayer p, float radius)
        {
            bool any = false;
            Enemies.QueryCircle(p.Position, radius, _hits);
            foreach (int i in _hits.ToArray())
                if (!Enemies[i].Dead && Enemies[i].Marked > 0f) any = true;
            if (!any) return false;
            p.Skill1Cooldown = 0f;
            Combo(p, ComboKind.ShadowExecution, p.Position, radius);
            return true;
        }

        /// <summary>Warrior + guardian: a war cry inside holy ground shields everyone standing in it and renews the ground.</summary>
        private void HolyWarCry(SimPlayer p)
        {
            for (int z = 0; z < _zones.Count; z++)
            {
                var zone = _zones[z];
                if (zone.Kind != AreaKind.Sanctuary || (p.Position - zone.Position).sqrMagnitude > zone.Radius * zone.Radius) continue;
                zone.Timer += HolyWarCryExtend;
                _zones[z] = zone;
                foreach (var ally in Players)
                    if (ally.Alive && (ally.Position - zone.Position).sqrMagnitude <= zone.Radius * zone.Radius)
                        ally.Shield = Mathf.Max(ally.Shield, HolyWarCryShield);
                // Enemies standing on the holy ground are struck down too.
                Enemies.QueryCircle(zone.Position, zone.Radius, _hits);
                foreach (int i in _hits.ToArray())
                {
                    ref var e = ref Enemies[i];
                    if (!e.Dead) ApplyStatus(i, StatusKind.Stunned, e.Def.Boss ? 0.4f : 1.4f, p);
                }
                Combo(p, ComboKind.HolyWarCry, zone.Position, zone.Radius);
                return;
            }
        }

        /// <summary>Guardian + alchemist: charging through burning ground carries the flames into everyone hit.</summary>
        private void CatchFire(SimPlayer p)
        {
            if (p.BashAflame) return;
            foreach (var zone in _zones)
            {
                if (zone.Kind != AreaKind.FirePool || (p.Position - zone.Position).sqrMagnitude > zone.Radius * zone.Radius) continue;
                p.BashAflame = true;
                Combo(p, ComboKind.FlameCharge, p.Position, 1.5f);
                return;
            }
        }
    }
}
