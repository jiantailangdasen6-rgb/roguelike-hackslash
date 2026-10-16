using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Breaking (体勢崩し): elites, champions and lords have a stance as well as health. Blows wear it down - heavy
    /// blows, shatters, teamwork and ultimates much more - and it slowly steadies when left alone. Worn through, the
    /// monster is broken: it reels, stunned, and takes <see cref="BreakBonus"/> more damage for a while; then its
    /// stance comes back (a lord's a little sturdier each time). A goal besides the health bar.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float BreakTime = 2.6f;
        public const float BossBreakTime = 3.2f;
        public const float BreakBonus = 0.5f;
        public const float PoiseSteadyDelay = 3f;
        public const float PoiseSteadyRate = 0.18f;   // share of the stance back per second
        public const float HeavyPoise = 2.5f;
        public const float ShatterPoise = 3f;
        public const float TeamworkPoise = 1.5f;
        public const float UltimatePoise = 2f;

        /// <summary>Who has a stance to break, and how much it holds (in points of damage).</summary>
        public static float PoiseOf(in Enemy e)
        {
            if (e.Def.Prop || e.Def.Friendly || e.Def.Untargetable) return 0f;
            if (e.Def.Boss) return e.MaxHealth * 0.18f;
            if (e.Champion) return e.MaxHealth * 0.3f;
            if (e.Elite) return e.MaxHealth * 0.4f;
            return 0f;
        }

        private static void InitPoise(ref Enemy e)
        {
            if (e.PoiseSet) return;
            e.PoiseSet = true;
            e.PoiseMax = PoiseOf(e);
            e.Poise = e.PoiseMax;
        }

        /// <summary>From DamageEnemy: the blow wears the stance down; worn through, the monster is broken.</summary>
        private float PoiseDamage(int index, float damage, HitFlags flags)
        {
            ref var e = ref Enemies[index];
            InitPoise(ref e);
            if (e.PoiseMax <= 0f) return damage;
            if (e.Broken > 0f) return damage * (1f + BreakBonus);
            if ((flags & HitFlags.DamageOverTime) != 0) return damage;
            float wear = damage;
            if ((flags & HitFlags.Heavy) != 0) wear *= HeavyPoise;
            if ((flags & HitFlags.Shatter) != 0) wear *= ShatterPoise;
            if ((flags & HitFlags.Teamwork) != 0) wear *= TeamworkPoise;
            if (_ultimateCasting) wear *= UltimatePoise;
            e.Poise -= wear;
            e.PoiseQuiet = PoiseSteadyDelay;
            if (e.Poise > 0f) return damage;
            Break(index);
            return damage;
        }

        private void Break(int index)
        {
            ref var e = ref Enemies[index];
            bool boss = e.Def.Boss;
            e.Broken = boss ? BossBreakTime : BreakTime;
            e.Poise = 0f;
            CancelAttack(ref e);
            e.Stunned = Mathf.Max(e.Stunned, e.Broken);
            if (boss) e.PoiseMax *= 1.2f; // a lord steadies a little more each time
            Events.Add(new SimEvent(SimEventType.EnemyBroken) { Id = e.Id, Pos = e.Position, Int = (int)e.Type, Value = e.Broken });
        }

        /// <summary>Each tick: the broken recover, the stance steadies when left alone.</summary>
        private void StepPoise(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead) continue;
                InitPoise(ref e);
                if (e.PoiseMax <= 0f) continue;
                if (e.Broken > 0f)
                {
                    e.Broken -= dt;
                    if (e.Broken <= 0f) e.Poise = e.PoiseMax; // back on its feet, steady again
                    continue;
                }
                if (e.PoiseQuiet > 0f)
                {
                    e.PoiseQuiet -= dt;
                    continue;
                }
                e.Poise = Mathf.Min(e.PoiseMax, e.Poise + e.PoiseMax * PoiseSteadyRate * dt);
            }
        }

        /// <summary>The lord's stance (0..1; 0 while broken), for the boss bar.</summary>
        public float BossPoiseFraction()
        {
            int b = BossIndex();
            if (b < 0) return -1f;
            var e = Enemies[b];
            if (e.PoiseMax <= 0f) return -1f;
            return e.Broken > 0f ? 0f : Mathf.Clamp01(e.Poise / e.PoiseMax);
        }
    }
}
