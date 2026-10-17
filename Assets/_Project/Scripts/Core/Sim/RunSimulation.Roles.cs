using UnityEngine;

namespace Game.Core
{
    /// <summary>A part a monster plays in a warband (RunSimulation.Roles).</summary>
    public enum EnemyRole : byte
    {
        None,
        Guard,  // 盾役: keeps between the team and the one it guards; allies beside it take less
        Healer, // 癒し手: mends the monsters around it now and then
    }

    public static class EnemyRoleCatalog
    {
        public static string Name(EnemyRole r) => r switch
        {
            EnemyRole.Guard => "盾役",
            EnemyRole.Healer => "癒し手",
            _ => "",
        };
    }

    /// <summary>
    /// Warbands (敵の連携): from the second room of an area, a normal wave may bring a band - one at the back (a
    /// shooter, or a healer that mends the others every few seconds) and two guards that keep themselves between it
    /// and the team, while monsters beside a guard take less harm. Which to kill first becomes the question.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float GuardCover = 0.3f;        // less harm for monsters beside a guard
        public const float GuardReach = 1.7f;
        public const float HealEvery = 5f;
        public const float HealRadius = 4.5f;
        public const float HealShare = 0.12f;        // of max health
        public const float WarbandChance = 0.45f;
        /// <summary>Tests and screenshots (-warband): every normal wave brings a band.</summary>
        public bool ForcedWarband;

        /// <summary>A band: its core at the back and two guards before it (queued like any spawn).</summary>
        private bool SpawnWarband()
        {
            EnemyType? melee = null, ranged = null;
            foreach (var (t, _, min) in BiomeCatalog.Get(Biome).Pool)
            {
                if (Room.Index < min) continue;
                var m = EnemyCatalog.Get(t);
                if (m.Boss || m.Prop || m.Flying || IsChampionKind(t)) continue;
                if (m.PreferredDistance > 0f) ranged ??= t;
                else melee ??= t;
            }
            if (melee == null) return false;
            Vector2 at = RandomSpawnPoint();
            var core = ranged ?? melee.Value;
            Queue(core, _spawnRng.Chance(0.5f) || ranged == null ? EnemyRole.Healer : EnemyRole.None, at);
            for (int k = 0; k < 2; k++)
                Queue(melee.Value, EnemyRole.Guard, ClampToRoom(at + new Vector2(k == 0 ? -1.3f : 1.3f, -0.6f), 0.6f));
            return true;

            void Queue(EnemyType type, EnemyRole role, Vector2 where)
            {
                QueueSpawn(type, false, Arena.Resolve(Layout, where, 0.5f));
                var s = PendingSpawns[PendingSpawns.Count - 1];
                s.Role = role;
                PendingSpawns[PendingSpawns.Count - 1] = s;
            }
        }

        /// <summary>A guard picks someone of its band to stand before (the nearest shooter or healer).</summary>
        private bool GuardSpot(in Enemy e, SimPlayer target, out Vector2 spot)
        {
            spot = default;
            if (e.Role != EnemyRole.Guard || target == null) return false;
            int ward = -1;
            float best = 7f * 7f;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var o = Enemies[i];
                if (o.Dead || o.Id == e.Id || o.RoomId != e.RoomId || (o.Role != EnemyRole.Healer && o.Def.PreferredDistance <= 0f)) continue;
                float sq = (o.Position - e.Position).sqrMagnitude;
                if (sq < best)
                {
                    best = sq;
                    ward = i;
                }
            }
            if (ward < 0) return false;
            Vector2 from = Enemies[ward].Position;
            Vector2 toward = target.Position - from;
            spot = from + (toward.sqrMagnitude > 0.01f ? toward.normalized : Vector2.down) * 1.6f + new Vector2((e.Id & 1) == 0 ? -0.5f : 0.5f, 0f);
            return true;
        }

        /// <summary>From DamageEnemy: a monster beside a guard (not a guard itself) takes less.</summary>
        private float GuardedDamage(int index, float damage, ref HitFlags flags)
        {
            var e = Enemies[index];
            if (e.Role == EnemyRole.Guard || e.Def.Boss) return damage;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var g = Enemies[i];
                if (g.Dead || g.Role != EnemyRole.Guard || g.RoomId != e.RoomId) continue;
                if ((g.Position - e.Position).sqrMagnitude > GuardReach * GuardReach) continue;
                return damage * (1f - GuardCover);
            }
            return damage;
        }

        /// <summary>Each tick: healers mend those around them.</summary>
        private void StepHealers(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Role != EnemyRole.Healer || e.Frozen > 0f || e.Stunned > 0f) continue;
                e.RoleTimer -= dt;
                if (e.RoleTimer > 0f) continue;
                e.RoleTimer = HealEvery;
                bool any = false;
                for (int j = 0; j < Enemies.Count; j++)
                {
                    ref var o = ref Enemies[j];
                    if (j == i || o.Dead || o.Def.Prop || o.Def.Friendly || o.RoomId != e.RoomId || o.Health >= o.MaxHealth) continue;
                    if ((o.Position - e.Position).sqrMagnitude > HealRadius * HealRadius) continue;
                    o.Health = Mathf.Min(o.MaxHealth, o.Health + o.MaxHealth * HealShare);
                    any = true;
                }
                if (any) Events.Add(new SimEvent(SimEventType.EnemyRole) { Id = e.Id, Int = 100, Pos = e.Position, Value = HealRadius }); // the mending, seen
                e = ref Enemies[i];
            }
        }
    }
}
