using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// A downed player is not out of the game: their body stays where they fell (teammates revive it there) and they
    /// drift on as a ghost inside that room. A ghost slows the monsters it hovers near, throws small soul sparks and
    /// still gathers souls. Monsters ignore ghosts; ghosts take no harm.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float GhostSlow = 0.55f;
        public const float HauntRadius = 1.8f;
        public const float WispInterval = 0.55f;
        public const float WispDamage = 5f;
        public const float WispSpeed = 10f;
        public const float WispRange = 8f;

        private void StepGhost(SimPlayer p, float dt)
        {
            // Hovering close chills the monsters around (they move a lot slower).
            Enemies.QueryCircle(p.Position, HauntRadius, _hits);
            foreach (int i in _hits)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Def.Prop) continue;
                e.Haunted = 0.25f;
            }

            p.GhostTimer -= dt;
            if (!p.Input.Attack || p.GhostTimer > 0f) return;
            p.GhostTimer = WispInterval;
            Vector2 dir = AimDirection(p);
            FireProjectile(p, ProjectileKind.Wisp, p.Position + dir * 0.4f, dir * WispSpeed, WispRange / WispSpeed, 0.3f,
                WispDamage * p.Stats.Get(StatType.Might), 0, HitFlags.None);
        }

        /// <summary>Fell: the body stays here.</summary>
        private static void LeaveBody(SimPlayer p) => p.BodyPosition = p.Position;
    }
}
