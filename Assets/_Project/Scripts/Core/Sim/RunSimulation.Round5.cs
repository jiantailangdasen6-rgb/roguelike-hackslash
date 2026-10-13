using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The deathcaller (soul bolts, bone servants on the players' side, a curse that raises the fallen) and the
    /// paladin (holy sword, judgment of light, devotion: half a teammate's wounds taken on oneself).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float ServantReach = 1.05f;
        public const float ServantInterval = 0.8f;
        public const float BonestormRadius = 2f;
        public const float BonestormDamage = 16f;

        private struct Raise
        {
            public SimPlayer Owner;
            public int Room;
            public Vector2 Position;
            public float Timer;
        }

        private struct Smite
        {
            public SimPlayer Owner;
            public Vector2 Position;
            public float Radius;
            public float Timer;
        }

        private readonly List<Raise> _raises = new();
        private readonly List<Smite> _smites = new();

        // ------------------------------------------------------------------ Basic attacks

        private void BasicRound5(SimPlayer p, Vector2 dir, float might, float area)
        {
            var c = p.Class;
            if (c.Id == ClassId.Deathcaller)
            {
                FireProjectile(p, ProjectileKind.SoulBolt, p.Position + dir * 0.5f, dir * ClassCatalog.SoulBoltSpeed,
                    c.BasicRange / ClassCatalog.SoulBoltSpeed, 0.32f, c.BasicDamage * might, 0, HitFlags.None);
                p.Combo = (p.Combo + 1) % 2;
                Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(p.Combo == 0 ? AttackAnim.BasicA : AttackAnim.BasicB), Pos = p.Position, Vec = dir });
                return;
            }

            // Paladin: two sweeping cuts; the second can loose a wave of light (radiance).
            bool second = p.Combo == 1;
            float range = c.BasicRange * area * (second ? 1.1f : 1f);
            float damage = c.BasicDamage * might * (second ? 1.25f : 1f);
            Enemies.QueryCone(p.Position, dir, range, c.BasicArc, _hits);
            foreach (int i in _hits.ToArray())
                DamageEnemy(i, damage, p, second ? HitFlags.Heavy : HitFlags.None, (Enemies[i].Position - p.Position).normalized, second ? 5f : 3f);
            int radiance = p.PerkLevel(PerkId.Radiance);
            if (second && radiance > 0)
                FireProjectile(p, ProjectileKind.SwordWave, p.Position + dir * 0.6f, dir * 12f, 0.6f, 0.6f, c.BasicDamage * might * 0.5f * radiance, 99, HitFlags.None);
            p.Combo = (p.Combo + 1) % 2;
            p.ComboResetTimer = 1.1f;
            if (second) p.BasicTimer *= 1.2f;
            Events.Add(new SimEvent(SimEventType.PlayerAttack) { ClientId = p.ClientId, Int = (int)(second ? AttackAnim.BasicB : AttackAnim.BasicA), Pos = p.Position, Vec = dir, Value = range });
        }

        // ------------------------------------------------------------------ Skills

        /// <returns>The cooldown to use.</returns>
        private float Skill1Round5(SimPlayer p, Vector2 dir, float cooldown)
        {
            if (p.Class.Id == ClassId.Deathcaller)
            {
                Vector2 at = AimPoint(p, ClassCatalog.RaiseRange);
                int count = ClassCatalog.RaiseCount + (p.PerkLevel(PerkId.BoneLegion) > 0 ? 1 : 0);
                for (int k = 0; k < count; k++)
                    _raises.Add(new Raise { Owner = p, Room = p.RoomId, Position = at + Rotate(Vector2.right, k * 360f / count + 30f) * 0.8f, Timer = 0.15f + 0.1f * k });
                Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Raise, Pos = at, Value = 1.6f });
                return cooldown;
            }

            // Paladin: a column of light falls where you aim (three times with the "日輪の" sword).
            int zeal = p.PerkLevel(PerkId.Zeal);
            float radius = ClassCatalog.JudgmentRadius * p.Stats.Get(StatType.Area) * (1f + 0.2f * zeal);
            Vector2 spot = AimPoint(p, ClassCatalog.JudgmentRange);
            int strikes = p.HasLegendary(LegendaryId.Sunburst) ? 3 : 1;
            if (strikes > 1) Proc(p, LegendaryId.Sunburst, spot);
            for (int k = 0; k < strikes; k++)
                _smites.Add(new Smite { Owner = p, Position = spot, Radius = radius, Timer = ClassCatalog.JudgmentDelay + 0.4f * k });
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.JudgmentMark, Pos = spot, Value = radius, Size = new Vector2(ClassCatalog.JudgmentDelay, 0f) });
            return cooldown * (zeal > 0 ? 0.85f : 1f);
        }

        private void Skill2Round5(SimPlayer p, Vector2 dir, float area)
        {
            if (p.Class.Id == ClassId.Deathcaller)
            {
                // The curse: cursed monsters take more from everyone, and rise as servants when they fall.
                int plague = p.PerkLevel(PerkId.PlagueCurse);
                bool underworld = p.HasLegendary(LegendaryId.Underworld);
                float radius = ClassCatalog.CurseRadius * area * (1f + 0.3f * plague) * (underworld ? 1.6f : 1f);
                Vector2 at = AimPoint(p, ClassCatalog.CurseRange);
                if (underworld) Proc(p, LegendaryId.Underworld, at);
                Enemies.QueryCircle(at, radius, _hits);
                foreach (int i in _hits.ToArray())
                {
                    ref var e = ref Enemies[i];
                    if (e.Dead || e.Def.Prop) continue;
                    e.CursedBy = p.ClientId + 1;
                    ApplyStatus(i, StatusKind.Cursed, ClassCatalog.CurseDuration + 3f * p.PerkLevel(PerkId.DeathKnell));
                    if (plague > 0)
                    {
                        e = ref Enemies[i];
                        e.PoisonDps = Mathf.Max(e.PoisonDps, 4f * plague * p.Stats.Get(StatType.Might));
                        ApplyStatus(i, StatusKind.Poisoned, ClassCatalog.CurseDuration);
                    }
                }
                Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Curse, Pos = at, Value = radius });
                return;
            }

            // Paladin: devotion. Bind the nearest teammate (two with the "天秤の" sword); alone, a big shield instead.
            var bound = new List<SimPlayer>();
            foreach (var mate in Players)
            {
                if (mate == p || !mate.Alive || (mate.Position - p.Position).sqrMagnitude > ClassCatalog.DevotionRange * ClassCatalog.DevotionRange) continue;
                bound.Add(mate);
            }
            bound.Sort((a, b) => (a.Position - p.Position).sqrMagnitude.CompareTo((b.Position - p.Position).sqrMagnitude));
            int keep = p.HasLegendary(LegendaryId.Scales) ? 2 : 1;
            if (bound.Count > keep) bound.RemoveRange(keep, bound.Count - keep);
            p.DevotedTo = bound.Count > 0 ? bound[0].ClientId + 1 : 0;
            p.DevotedTo2 = bound.Count > 1 ? bound[1].ClientId + 1 : 0;
            p.DevotionTimer = ClassCatalog.DevotionDuration;
            if (bound.Count > 1) Proc(p, LegendaryId.Scales, p.Position);
            p.Shield = Mathf.Max(p.Shield, ClassCatalog.DevotionShield * (bound.Count == 0 ? 2f : 1f));
            foreach (var mate in bound)
            {
                mate.Shield = Mathf.Max(mate.Shield, ClassCatalog.DevotionShield + 15f * p.PerkLevel(PerkId.LightShield));
                Events.Add(new SimEvent(SimEventType.AreaEffect)
                {
                    ClientId = p.ClientId, Int = (int)AreaKind.Devotion, Pos = mate.Position, Value = ClassCatalog.DevotionDuration, Size = new Vector2(mate.ClientId + 1, 0f),
                });
            }
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.HolyLight, Pos = p.Position, Value = 1.6f });
        }

        // ------------------------------------------------------------------ Per tick

        private void StepRound5(float dt)
        {
            foreach (var p in Players)
                if (p.DevotionTimer > 0f && (p.DevotionTimer -= dt) <= 0f) p.DevotedTo = p.DevotedTo2 = 0;

            for (int k = _raises.Count - 1; k >= 0; k--)
            {
                var r = _raises[k];
                r.Timer -= dt;
                if (r.Timer > 0f)
                {
                    _raises[k] = r;
                    continue;
                }
                _raises.RemoveAt(k);
                RaiseServant(r.Owner, r.Room, r.Position, r.Owner != null && r.Owner.HasLegendary(LegendaryId.Underworld));
            }

            for (int k = _smites.Count - 1; k >= 0; k--)
            {
                var s = _smites[k];
                s.Timer -= dt;
                if (s.Timer > 0f)
                {
                    _smites[k] = s;
                    continue;
                }
                _smites.RemoveAt(k);
                Judge(s);
            }
        }

        private void Judge(Smite s)
        {
            var p = s.Owner;
            if (p == null) return;
            float might = p.Stats.Get(StatType.Might);
            Enemies.QueryCircle(s.Position, s.Radius, _hits);
            foreach (int i in _hits.ToArray())
            {
                if (Enemies[i].Dead) continue;
                bool boss = Enemies[i].Def.Boss;
                DamageEnemy(i, ClassCatalog.JudgmentDamage * might, p, HitFlags.Heavy, (Enemies[i].Position - s.Position).normalized, 4f);
                if (!Enemies[i].Dead) ApplyStatus(i, StatusKind.Stunned, boss ? 0.3f : ClassCatalog.JudgmentStun, p);
            }
            foreach (var mate in Players)
            {
                if (!mate.Alive || (mate.Position - s.Position).sqrMagnitude > s.Radius * s.Radius) continue;
                float before = mate.Hp;
                mate.Heal(ClassCatalog.JudgmentHeal);
                if (mate.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = mate.ClientId, Pos = mate.Position, Value = mate.Hp - before });
            }
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Judgment, Pos = s.Position, Value = s.Radius });
        }

        // ------------------------------------------------------------------ Bone servants

        /// <summary>The player a stored key (client id + 1) names, or null.</summary>
        private SimPlayer PlayerByKey(ulong key)
        {
            foreach (var p in Players) if (p.ClientId + 1 == key) return p;
            return null;
        }

        private int ServantCap(SimPlayer p) => ClassCatalog.ServantCap + p.PerkLevel(PerkId.BoneLegion);

        /// <summary>A servant rises for <paramref name="owner"/>; at the cap, the oldest crumbles to make room.</summary>
        private void RaiseServant(SimPlayer owner, int room, Vector2 at, bool overCap = false)
        {
            if (owner == null || room < 0 || room >= RoomStates.Count) return;
            int count = 0, oldest = -1;
            float oldestLife = float.MaxValue;
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Type != EnemyType.BoneServant || e.Master != owner.ClientId + 1) continue;
                count++;
                if (e.Life < oldestLife) { oldestLife = e.Life; oldest = i; }
            }
            if (count >= ServantCap(owner) + (overCap ? 2 : 0) && oldest >= 0) Enemies[oldest].Life = 0f;

            Room = RoomStates[room];
            at = Arena.Resolve(Layout, ClampToRoom(at, 0.5f), 0.36f);
            int id = Enemies.Spawn(EnemyType.BoneServant, at, 1f, 1f, false);
            ref var s = ref Enemies[Enemies.IndexOf(id)];
            s.RoomId = room;
            s.Master = owner.ClientId + 1;
            s.Life = ClassCatalog.ServantLife * (owner.HasLegendary(LegendaryId.BoneThrone) ? 1.5f : 1f);
            s.Cooldown = 0.3f;
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = owner.ClientId, Int = (int)AreaKind.Raise, Pos = at, Value = 0.7f });
        }

        /// <summary>A servant's turn: hunt the nearest monster in its room and cut at it; it crumbles when its time is up.</summary>
        private void StepServant(int index, float dt)
        {
            ref var e = ref Enemies[index];
            e.Life -= dt;
            var owner = PlayerByKey(e.Master);
            if (e.Life <= 0f || owner == null)
            {
                e.Dead = true;
                return;
            }
            // The master went through a door: the servants follow.
            if (owner.RoomId >= 0 && owner.RoomId != e.RoomId && owner.Alive)
            {
                e.RoomId = owner.RoomId;
                e.Position = owner.Position + Rotate(Vector2.right, e.Id * 77f) * 1.1f;
                Room = RoomStates[e.RoomId];
            }
            e.Cooldown -= dt;
            if (e.StateTimer > 0f && (e.StateTimer -= dt) <= 0f) e.State = EnemyState.Chase;

            int target = -1;
            float best = 12f * 12f;
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var m = ref Enemies[i];
                if (m.Dead || m.RoomId != e.RoomId || m.Def.Prop || m.Def.Friendly || m.Def.Untargetable) continue;
                float d = (m.Position - e.Position).sqrMagnitude;
                if (d < best) { best = d; target = i; }
            }
            e = ref Enemies[index];
            if (target < 0)
            {
                // Nothing to fight: keep close to the master.
                Vector2 toOwner = owner.Position - e.Position;
                if (toOwner.sqrMagnitude > 2.5f * 2.5f)
                {
                    Vector2 dir = Arena.Steer(Layout, e.Position, toOwner.normalized, e.Radius, e.Id);
                    e.Position += dir * e.Def.Speed * dt;
                    e.Moving = true;
                    if (Mathf.Abs(dir.x) > 0.1f) e.Facing = Mathf.Sign(dir.x);
                }
                return;
            }

            Vector2 to = Enemies[target].Position - e.Position;
            float reach = ServantReach + Enemies[target].Radius;
            if (Mathf.Abs(to.x) > 0.05f) e.Facing = Mathf.Sign(to.x);
            if (to.sqrMagnitude > reach * reach)
            {
                Vector2 dir = Arena.Steer(Layout, e.Position, to.normalized, e.Radius, e.Id);
                e.Position += dir * e.Def.Speed * dt;
                e.Moving = true;
                return;
            }
            if (e.Cooldown > 0f) return;
            e.Cooldown = ServantInterval;
            e.State = EnemyState.Strike;
            e.StateTimer = 0.3f;
            float damage = ClassCatalog.ServantDamage * owner.Stats.Get(StatType.Might) * (owner.HasLegendary(LegendaryId.BoneThrone) ? 2f : 1f);
            DamageEnemy(target, damage, owner, HitFlags.DamageOverTime, to.normalized, 2f);
        }

        /// <summary>A servant crumbled (its time, or the room fell quiet): with the bonestorm, its bones burst.</summary>
        private void ServantGone(in Enemy servant)
        {
            var owner = PlayerByKey(servant.Master);
            if (owner == null || owner.PerkLevel(PerkId.Bonestorm) <= 0) return;
            Enemies.QueryCircle(servant.Position, BonestormRadius, _godHits);
            foreach (int i in _godHits.ToArray())
                if (!Enemies[i].Dead) DamageEnemy(i, BonestormDamage * owner.Stats.Get(StatType.Might), owner, HitFlags.None, (Enemies[i].Position - servant.Position).normalized, 3f);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = owner.ClientId, Int = (int)AreaKind.BoneBurst, Pos = servant.Position, Value = BonestormRadius });
        }

        /// <summary>A cursed monster fell: it rises again on its curser's side (or always, with the "冥界の" staff).</summary>
        private void CursedFell(in Enemy e)
        {
            var owner = PlayerByKey(e.CursedBy);
            if (owner == null || e.Def.Boss) return;
            _raises.Add(new Raise { Owner = owner, Room = e.RoomId, Position = e.Position, Timer = 0.5f });
        }

        // ------------------------------------------------------------------ Devotion

        /// <summary>The paladin bound to this player by devotion (alive, still bound), or null.</summary>
        private SimPlayer DevotedPaladin(SimPlayer victim)
        {
            foreach (var p in Players)
                if (p != victim && p.Alive && p.DevotionTimer > 0f && (p.DevotedTo == victim.ClientId + 1 || p.DevotedTo2 == victim.ClientId + 1)) return p;
            return null;
        }

        /// <summary>Health taken straight off a player (shield first): the paladin's share of a devoted teammate's wound.</summary>
        private void TakeShare(SimPlayer p, float amount)
        {
            if (p.PerkLevel(PerkId.Martyr) > 0) amount *= 0.7f;
            if (p.Shield > 0f)
            {
                float absorbed = Mathf.Min(p.Shield, amount);
                p.Shield -= absorbed;
                amount -= absorbed;
            }
            if (amount <= 0f) return;
            // Devotion never kills the one who gives it: the paladin stays standing at 1.
            p.Hp = Mathf.Max(1f, p.Hp - amount);
            Events.Add(new SimEvent(SimEventType.PlayerHurt) { ClientId = p.ClientId, Pos = p.Position, Value = amount });
        }
    }
}
