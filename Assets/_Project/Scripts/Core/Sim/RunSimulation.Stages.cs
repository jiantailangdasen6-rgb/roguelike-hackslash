using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The lords' stages. Every lord fights in three: at <see cref="StageTwoAt"/> of its health it flies into a rage,
    /// at <see cref="StageThreeAt"/> it makes its last stand. Each turn waits until no trial is running, sets the
    /// hall itself against the players (the floor splits and marked spots burst, faster and more of them in the
    /// last stand, in the land's own way: bone, blood, void, water, ice, fire, spores, ink) and makes the lord
    /// quicker. The presentation reads <see cref="BossStage"/> (RunDirector mirrors it) and the TrialEvent moments.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float StageTwoAt = 0.6f;
        public const float StageThreeAt = 0.25f;
        public const float QuakeRadius = 1.5f;
        public const float QuakeDelay = 1.2f;

        /// <summary>0 outside a lord's fight, then 1..3.</summary>
        public int BossStage { get; private set; }

        private float _quakeTimer;

        private void ResetBossStages()
        {
            BossStage = 0;
            _quakeTimer = 0f;
        }

        /// <summary>After the trials each tick of a lord's fight.</summary>
        private void StepBossStages(float dt)
        {
            int b = BossIndex();
            if (b < 0)
            {
                BossStage = 0;
                return;
            }
            ref var boss = ref Enemies[b];
            if (BossStage == 0) BossStage = 1;
            float hp = boss.Health / boss.MaxHealth;
            int want = hp <= StageThreeAt ? 3 : hp <= StageTwoAt ? 2 : 1;
            if (want > BossStage && ActiveTrial == BossTrial.None)
            {
                BossStage = want;
                _quakeTimer = 2.2f; // a breath after the roar before the floor starts to break
                TrialEvent(want == 2 ? TrialMoment.StageRage : TrialMoment.StageLast, boss.Position, (int)Biome);
            }
            if (BossStage < 2 || ActiveTrial != BossTrial.None) return;

            _quakeTimer -= dt;
            if (_quakeTimer > 0f) return;
            bool last = BossStage >= 3;
            _quakeTimer = (last ? 3.1f : 4.6f) * (Wrath ? 0.85f : 1f);
            Quake(last ? 3 : 2, last);
        }

        /// <summary>Marked spots on the hall floor that burst a moment later: one under a player, the rest anywhere.</summary>
        private void Quake(int count, bool last)
        {
            var area = PlayRect;
            float damage = (last ? 16f : 12f) * EnemyDamageScale;
            float radius = QuakeRadius + (last ? 0.2f : 0f);
            var target = RandomLivingPlayer();
            for (int k = 0; k < count; k++)
            {
                Vector2 at = default;
                for (int tries = 0; tries < 6; tries++)
                {
                    at = k == 0 && target != null
                        ? target.Position + Rotate(Vector2.right, _aiRng.Range(0f, 360f)) * _aiRng.Range(0f, 0.6f)
                        : new Vector2(_aiRng.Range(area.xMin + 1.5f, area.xMax - 1.5f), _aiRng.Range(area.yMin + 1.5f, area.yMax - 1.5f));
                    if (!Arena.Overlaps(Layout, at, 0.5f)) break;
                }
                QueueBurst(at, radius, QuakeDelay, damage, AreaKind.HallQuake);
            }
        }

        /// <summary>Raging and last-stand lords attack sooner and wind up faster.</summary>
        private float StageCooldown => BossStage >= 3 ? 0.6f : BossStage == 2 ? 0.75f : 1f;
        private float StageWindup => BossStage >= 3 ? 0.7f : BossStage == 2 ? 0.8f : 1f;
    }
}
