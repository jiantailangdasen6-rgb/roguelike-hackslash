using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Presentation-only hit stop: sprites, effects and enemy drawing pause for a few frames on big blows
    /// (the simulation and everyone's movement keep running, so co-op stays in sync). Views multiply their
    /// frame time by <see cref="Scale"/>. Small stops are spaced out so rapid attacks never stutter.
    /// </summary>
    public static class HitStop
    {
        private const float MaxFreeze = 0.16f;
        private const float MinGap = 0.12f; // between small stops

        private static float _freezeUntil;
        private static float _slowUntil;
        private static float _slowFactor = 1f;

        /// <summary>The enemy that took the blow (shakes while everything holds still), or -1.</summary>
        public static int Victim { get; private set; } = -1;

        public static bool Frozen => Time.unscaledTime < _freezeUntil;

        public static float Scale => Frozen ? 0f : Time.unscaledTime < _slowUntil ? _slowFactor : 1f;

        public static float DeltaTime => Time.deltaTime * Scale;

        /// <param name="major">Shatters, boss kills: may extend a running stop and skip the spacing.</param>
        public static void Freeze(float seconds, int victim = -1, bool major = false)
        {
            float now = Time.unscaledTime;
            if (!major && now < _freezeUntil + MinGap) return;
            float until = now + Mathf.Min(seconds, MaxFreeze);
            if (until <= _freezeUntil) return;
            _freezeUntil = until;
            Victim = victim;
        }

        /// <summary>Slow motion after the freeze (boss kills).</summary>
        public static void Slow(float seconds, float factor)
        {
            _slowUntil = Mathf.Max(_slowUntil, Time.unscaledTime + seconds);
            _slowFactor = Mathf.Clamp(factor, 0.05f, 1f);
        }

        public static void Reset()
        {
            _freezeUntil = 0f;
            _slowUntil = 0f;
            Victim = -1;
        }
    }
}
