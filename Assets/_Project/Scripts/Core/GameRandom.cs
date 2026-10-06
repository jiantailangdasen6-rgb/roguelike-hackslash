using System;

namespace Game.Core
{
    /// <summary>
    /// Deterministic RNG (xorshift128+). Use this instead of UnityEngine.Random so a run
    /// can be reproduced from its seed. Give each system its own stream via <see cref="Fork"/>
    /// so that, e.g., extra loot rolls never change the dungeon layout.
    /// </summary>
    public sealed class GameRandom
    {
        private ulong _s0;
        private ulong _s1;

        public ulong Seed { get; }

        public GameRandom(ulong seed)
        {
            Seed = seed;
            // SplitMix64 expands the seed so that nearby seeds give unrelated sequences.
            ulong x = seed;
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        /// <summary>Creates an independent child stream, e.g. <c>runRng.Fork(StreamIds.Loot)</c>.</summary>
        public GameRandom Fork(ulong salt)
        {
            ulong x = Seed ^ (salt * 0x9E3779B97F4A7C15UL);
            return new GameRandom(SplitMix64(ref x));
        }

        public ulong NextULong()
        {
            ulong x = _s0;
            ulong y = _s1;
            _s0 = y;
            x ^= x << 23;
            _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
            return _s1 + y;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform float in [0, 1).</summary>
        // 24 bits so the result is exactly representable; casting NextDouble() could round up to 1f.
        public float Value() => (NextULong() >> 40) * (1f / (1 << 24));

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException($"maxExclusive ({maxExclusive}) must be greater than minInclusive ({minInclusive}).");
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % span));
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>Returns true with probability <paramref name="probability"/> (0..1).</summary>
        public bool Chance(float probability) => NextDouble() < probability;

        private static ulong SplitMix64(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
