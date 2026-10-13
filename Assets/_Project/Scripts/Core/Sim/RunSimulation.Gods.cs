using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Gods' blessings (GodCatalog): every player picks one after a champion or a lord falls. Each blessing hooks into
    /// hits, kills, dodges, skills or being hurt; two of the same god awaken its favour.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float BrandBurnDps = 6f;
        public const float BrandBurnTime = 3f;
        public const float PyreRadius = 2.2f;
        public const float PyreDamage = 20f;
        public const float FlameStepRadius = 2.4f;
        public const float FlameStepDamage = 18f;
        public const float FrostBiteChance = 0.1f;
        public const float FrostBiteTime = 1.2f;
        public const float FrostWardRadius = 2.8f;
        public const float FrostWardCooldown = 8f;
        public const float FrostBreathRadius = 3.2f;
        public const float FrostBurstRadius = 2.2f;
        public const int StormFistEvery = 4;
        public const float SmiteDamage = 16f;
        public const float SmiteReach = 7f;
        public const float JudgmentDamage = 30f;
        public const float JudgmentRadius = 1.9f;
        public const float JudgmentRange = 8f;
        public const float FavorStun = 0.5f;
        public const float ShareHeal = 2f;
        public const float ShareRadius = 7f;
        public const float VeilEvery = 12f;
        public const float VeilShare = 0.15f;
        public const float RiseDelay = 3f;
        public const float RiseHealth = 0.4f;

        private struct GodBurst
        {
            public GodId God;
            public SimPlayer Owner;
            public Vector2 Position;
            public float Timer;
            public int Depth;
        }

        /// <summary>Bursts set off by kills, a tick later (chains ripple outwards instead of recursing inside a hit).</summary>
        private readonly List<GodBurst> _godBursts = new();
        private readonly List<int> _godHits = new();

        /// <summary>Champions and lords grant every player a blessing, chosen when their room is quiet.</summary>
        /// <summary>Blessings handed out so far (a newcomer mid-run catches up on them).</summary>
        public int BlessingsGranted { get; private set; }

        private void GrantBlessings()
        {
            BlessingsGranted++;
            foreach (var p in Players) p.StockedChoices.Enqueue(ChoiceKind.Blessing);
        }

        /// <summary>A new area: the prayer of rebirth works again.</summary>
        private void ResetGods()
        {
            foreach (var p in Players)
            {
                p.RiseUsed = false;
                p.RiseTimer = 0f;
            }
            _godBursts.Clear();
            _mimicWakes.Clear(); // RunSimulation.Events.cs
            _vanished.Clear();
        }

        private void BlessingTaken(SimPlayer p, PerkId id)
        {
            var def = PerkCatalog.Get(id);
            if (!def.God.HasValue) return;
            var god = def.God.Value;
            Sign(p, GodSignKind.Taken, god, p.Position, (int)id);
            if (p.GodPieces(god) == GodCatalog.FavorPieces) Sign(p, GodSignKind.Favor, god, p.Position);
            if (id == PerkId.LifeVeil) p.VeilTimer = 0f;
        }

        private void Sign(SimPlayer p, GodSignKind kind, GodId god, Vector2 at, float value = 0f, Vector2 vec = default) =>
            Events.Add(new SimEvent(SimEventType.GodSign) { ClientId = p?.ClientId ?? 0, Int = (int)kind, Id = (int)god, Pos = at, Value = value, Vec = vec });

        private void StepGods(float dt)
        {
            foreach (var p in Players)
            {
                p.FrostWardTimer -= dt;
                if (!p.Alive)
                {
                    if (p.RiseTimer > 0f && (p.RiseTimer -= dt) <= 0f)
                    {
                        Revive(p, RiseHealth, guardian: true);
                        Sign(p, GodSignKind.Rise, GodId.Life, p.Position);
                    }
                    continue;
                }
                if (p.Has(PerkId.LifeVeil) && (p.VeilTimer -= dt) <= 0f)
                {
                    p.VeilTimer = VeilEvery;
                    float shield = p.MaxHp * VeilShare;
                    if (p.Shield < shield)
                    {
                        p.Shield = shield;
                        Sign(p, GodSignKind.Veil, GodId.Life, p.Position);
                    }
                }
                if (p.Favored(GodId.Life))
                    foreach (var mate in Players)
                        if (mate.Alive && (mate.Position - p.Position).sqrMagnitude <= GodCatalog.FavorLifeRadius * GodCatalog.FavorLifeRadius)
                            mate.Heal(GodCatalog.FavorLifeRegen * dt);
            }

            for (int b = _godBursts.Count - 1; b >= 0; b--)
            {
                var burst = _godBursts[b];
                burst.Timer -= dt;
                if (burst.Timer > 0f)
                {
                    _godBursts[b] = burst;
                    continue;
                }
                _godBursts.RemoveAt(b);
                if (burst.God == GodId.Flame) FireBurst(burst.Owner, burst.Position, PyreRadius, PyreDamage, burst.Depth);
                else FrostBurst(burst.Owner, burst.Position, FrostBurstRadius, 0f, burst.Depth);
            }
        }

        // ------------------------------------------------------------------ Hooks (called from the combat code)

        /// <summary>Damage bonuses of favours, applied with the attacker's other bonuses.</summary>
        private static float FavorDamage(SimPlayer attacker, in Enemy e) =>
            attacker.Favored(GodId.Flame) && e.Burning > 0f ? 1f + GodCatalog.FavorFlameBonus : 1f;

        /// <summary>A direct hit landed and the monster still stands.</summary>
        private void BlessingOnHit(int index, SimPlayer attacker)
        {
            ref var e = ref Enemies[index];
            if (e.Dead || e.Def.Prop) return;
            if (attacker.Has(PerkId.FlameBrand) && e.Burning < 1f)
            {
                e.BurnDps = Mathf.Max(e.BurnDps, BrandBurnDps * attacker.Stats.Get(StatType.Might));
                ApplyStatus(index, StatusKind.Burning, BrandBurnTime);
            }
            if (attacker.Has(PerkId.FrostBite) && !e.Def.Boss && e.Frozen <= 0f && _combatRng.Chance(FrostBiteChance))
            {
                ApplyStatus(index, StatusKind.Frozen, FrostBiteTime, attacker);
                Sign(attacker, GodSignKind.Frost, GodId.Frost, e.Position, 0.8f);
            }
        }

        /// <summary>A monster fell to this player (burning / frozen at the moment of death).</summary>
        private void BlessingOnKill(SimPlayer attacker, Vector2 at, bool burning, bool frozen, int depth = 0)
        {
            if (burning && attacker.Has(PerkId.FlamePyre) && depth < 3)
                _godBursts.Add(new GodBurst { God = GodId.Flame, Owner = attacker, Position = at, Timer = 0.12f, Depth = depth + 1 });
            if (frozen && attacker.Favored(GodId.Frost) && depth < 3)
                _godBursts.Add(new GodBurst { God = GodId.Frost, Owner = attacker, Position = at, Timer = 0.12f, Depth = depth + 1 });
            if (attacker.Has(PerkId.LifeShare))
                foreach (var mate in Players)
                    if (mate.Alive && (mate.Position - attacker.Position).sqrMagnitude <= ShareRadius * ShareRadius)
                        mate.Heal(ShareHeal);
        }

        private int _burstDepth;

        private void BlessingOnDodge(SimPlayer p)
        {
            if (p.Has(PerkId.FlameStep)) FireBurst(p, p.Position, FlameStepRadius, FlameStepDamage, 0);
            if (p.Has(PerkId.StormStep)) SmiteNearest(p, p.Position, 2);
        }

        private void BlessingOnBasic(SimPlayer p)
        {
            if (!p.Has(PerkId.StormFist)) return;
            if (++p.StormHits < StormFistEvery) return;
            p.StormHits = 0;
            SmiteNearest(p, p.Position, 3);
        }

        private void BlessingOnSkill1(SimPlayer p)
        {
            if (!p.Has(PerkId.StormJudgment)) return;
            Vector2 at = AimPoint(p, JudgmentRange);
            bool favored = p.Favored(GodId.Storm);
            float damage = JudgmentDamage * p.Stats.Get(StatType.Might);
            Enemies.QueryCircle(at, JudgmentRadius, _godHits);
            foreach (int i in _godHits.ToArray())
            {
                if (Enemies[i].Dead) continue;
                DamageEnemy(i, damage, p, HitFlags.Heavy, (Enemies[i].Position - at).normalized, 3f);
                if (favored) Stagger(i, p);
            }
            Sign(p, GodSignKind.Lightning, GodId.Storm, at, JudgmentRadius);
        }

        private void BlessingOnSkill2(SimPlayer p)
        {
            if (p.Has(PerkId.FrostBreath)) FrostBurst(p, p.Position, FrostBreathRadius * p.Stats.Get(StatType.Area), 6f, 0);
        }

        /// <summary>Took a blow (still standing or not).</summary>
        private void BlessingOnHurt(SimPlayer p)
        {
            if (!p.Has(PerkId.FrostWard) || p.FrostWardTimer > 0f) return;
            p.FrostWardTimer = FrostWardCooldown;
            FrostBurst(p, p.Position, FrostWardRadius, 0f, 0);
        }

        /// <summary>Just went down: the prayer of rebirth starts counting.</summary>
        private void BlessingOnDowned(SimPlayer p)
        {
            if (!p.Has(PerkId.LifeRise) || p.RiseUsed) return;
            p.RiseUsed = true;
            p.RiseTimer = RiseDelay;
        }

        // ------------------------------------------------------------------ Effects

        /// <summary>Lightning onto the nearest monsters around a point (one more, and stunning, with the storm's favour).</summary>
        private void SmiteNearest(SimPlayer p, Vector2 around, int count)
        {
            bool favored = p.Favored(GodId.Storm);
            if (favored) count++;
            float damage = SmiteDamage * p.Stats.Get(StatType.Might);
            Enemies.QueryCircle(around, SmiteReach, _godHits);
            var targets = new List<int>(_godHits);
            targets.RemoveAll(i => Enemies[i].Dead || Enemies[i].Def.Prop || Enemies[i].Def.Untargetable);
            targets.Sort((a, b) => (Enemies[a].Position - around).sqrMagnitude.CompareTo((Enemies[b].Position - around).sqrMagnitude));
            for (int k = 0; k < targets.Count && k < count; k++)
            {
                int i = targets[k];
                Vector2 at = Enemies[i].Position;
                DamageEnemy(i, damage, p, HitFlags.None, Vector2.zero, 0f);
                if (favored) Stagger(i, p);
                Sign(p, GodSignKind.Lightning, GodId.Storm, at);
            }
        }

        private void Stagger(int index, SimPlayer by)
        {
            if (Enemies[index].Dead) return;
            ApplyStatus(index, StatusKind.Stunned, Enemies[index].Def.Boss ? 0.2f : FavorStun, by);
        }

        private void FireBurst(SimPlayer p, Vector2 at, float radius, float damage, int depth)
        {
            if (p == null) return;
            float might = p.Stats.Get(StatType.Might);
            Enemies.QueryCircle(at, radius, _godHits);
            foreach (int i in _godHits.ToArray())
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Def.Prop) continue;
                e.BurnDps = Mathf.Max(e.BurnDps, BrandBurnDps * might);
                ApplyStatus(i, StatusKind.Burning, BrandBurnTime);
                _burstDepth = depth;
                DamageEnemy(i, damage * might, p, HitFlags.None, (Enemies[i].Position - at).normalized, 3f);
                _burstDepth = 0;
            }
            Sign(p, GodSignKind.Fire, GodId.Flame, at, radius);
        }

        private void FrostBurst(SimPlayer p, Vector2 at, float radius, float damage, int depth)
        {
            if (p == null) return;
            float might = p.Stats.Get(StatType.Might);
            Enemies.QueryCircle(at, radius, _godHits);
            foreach (int i in _godHits.ToArray())
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Def.Prop) continue;
                _burstDepth = depth;
                if (damage > 0f) DamageEnemy(i, damage * might, p, HitFlags.None, Vector2.zero, 0f);
                _burstDepth = 0;
                if (!Enemies[i].Dead) ApplyStatus(i, Enemies[i].Def.Boss ? StatusKind.Stunned : StatusKind.Frozen, Enemies[i].Def.Boss ? 0.4f : FrostBiteTime + 0.3f, p);
            }
            Sign(p, GodSignKind.Frost, GodId.Frost, at, radius);
        }
    }
}
