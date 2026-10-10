using System;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Procedural sound effects (no audio files): noise bursts, pitch sweeps, plucked strings and drones,
    /// shaped with envelopes and simple one-pole filters. Generated once at startup.
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 44100;
        private static readonly System.Random Rng = new(1234);

        private static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);

        private static AudioClip Make(string name, float seconds, Func<float, float, float> sample, bool loop = false)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = sample(t, t / seconds);
            }
            Normalize(data, 0.9f);
            if (loop) data = CrossfadeLoop(data, Rate / 10);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (var s in data) max = Mathf.Max(max, Mathf.Abs(s));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        /// <summary>Blends the tail into the head and drops the tail, so the clip loops without a click.</summary>
        private static float[] CrossfadeLoop(float[] data, int fade)
        {
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = data[i] * k + data[data.Length - fade + i] * (1f - k);
            }
            Array.Resize(ref data, data.Length - fade);
            return data;
        }

        // Envelopes
        private static float Decay(float k, float sharpness) => Mathf.Exp(-k * sharpness);
        private static float AttackDecay(float k, float attack) => k < attack ? k / attack : Mathf.Exp(-(k - attack) * 6f);

        /// <summary>Stateful one-pole low-pass; cutoff 0..1 (fraction of the previous sample kept).</summary>
        private sealed class LowPass
        {
            private float _y;
            public float Step(float x, float alpha) => _y += alpha * (x - _y);
        }

        public static AudioClip Swing()
        {
            var lp = new LowPass();
            return Make("swing", 0.2f, (t, k) => lp.Step(Noise(), Mathf.Lerp(0.05f, 0.35f, Mathf.Sin(k * Mathf.PI))) * Mathf.Sin(k * Mathf.PI));
        }

        public static AudioClip Hit()
        {
            var lp = new LowPass();
            return Make("hit", 0.14f, (t, k) =>
                Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 55f, k) * t) * Decay(k, 7f) * 0.9f
                + lp.Step(Noise(), 0.5f) * Decay(k, 22f) * 0.7f);
        }

        public static AudioClip HeavyHit()
        {
            var lp = new LowPass();
            return Make("heavyhit", 0.28f, (t, k) =>
                Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 38f, k) * t) * Decay(k, 5f)
                + lp.Step(Noise(), 0.3f) * Decay(k, 12f) * 0.8f
                + Noise() * Decay(k, 40f) * 0.4f);
        }

        public static AudioClip Bolt()
        {
            return Make("bolt", 0.22f, (t, k) =>
            {
                float f = Mathf.Lerp(1200f, 380f, Mathf.Sqrt(k));
                return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 1.5f * t) * 0.25f + Noise() * 0.15f) * AttackDecay(k, 0.05f);
            });
        }

        /// <summary>Karplus-Strong pluck for the bow string.</summary>
        public static AudioClip Arrow()
        {
            int period = Rate / 180;
            var buffer = new float[period];
            for (int i = 0; i < period; i++) buffer[i] = Noise();
            int idx = 0;
            var lp = new LowPass();
            return Make("arrow", 0.25f, (t, k) =>
            {
                float s = buffer[idx];
                int next = (idx + 1) % period;
                buffer[idx] = 0.497f * (buffer[idx] + buffer[next]);
                idx = next;
                return s * Decay(k, 4f) + lp.Step(Noise(), 0.2f) * Decay(k, 30f) * 0.6f;
            });
        }

        public static AudioClip Shatter()
        {
            var pings = new float[6];
            for (int i = 0; i < pings.Length; i++) pings[i] = 1800f + (float)Rng.NextDouble() * 3200f;
            return Make("shatter", 0.45f, (t, k) =>
            {
                float s = Noise() * Decay(k, 9f) * 0.6f;
                for (int i = 0; i < pings.Length; i++)
                {
                    float start = i * 0.05f;
                    if (k > start) s += Mathf.Sin(2f * Mathf.PI * pings[i] * t) * Decay(k - start, 14f) * 0.25f;
                }
                return s;
            });
        }

        public static AudioClip Death()
        {
            var lp = new LowPass();
            return Make("death", 0.45f, (t, k) =>
            {
                float rattle = (Mathf.Sin(2f * Mathf.PI * 18f * t) > 0.6f ? 1f : 0.2f) * Noise();
                return lp.Step(rattle, 0.25f) * Decay(k, 5f) + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, k) * t) * Decay(k, 9f) * 0.5f;
            });
        }

        public static AudioClip Hurt()
        {
            return Make("hurt", 0.2f, (t, k) =>
            {
                float s = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 90f, k) * t);
                return Mathf.Clamp(s * 2.5f, -1f, 1f) * Decay(k, 6f) * 0.8f + Noise() * Decay(k, 25f) * 0.4f;
            });
        }

        public static AudioClip Slam()
        {
            var lp = new LowPass();
            return Make("slam", 0.6f, (t, k) =>
                Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 28f, k) * t) * Decay(k, 3.5f)
                + lp.Step(Noise(), 0.08f) * Decay(k, 4f) * 1.5f);
        }

        public static AudioClip Telegraph()
        {
            var lp = new LowPass();
            return Make("telegraph", 0.3f, (t, k) => lp.Step(Noise(), 0.04f) * Mathf.Sin(k * Mathf.PI) * 2f
                                                    + Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Sin(k * Mathf.PI) * 0.2f);
        }

        public static AudioClip Chime(string name, float[] notes, float step, float length)
        {
            return Make(name, length, (t, k) =>
            {
                float s = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * step;
                    if (t < start) continue;
                    float tt = t - start;
                    float env = Mathf.Exp(-tt * 5f);
                    s += (Mathf.Sin(2f * Mathf.PI * notes[i] * tt) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * tt)) * env;
                }
                return s;
            });
        }

        public static AudioClip WarCry()
        {
            return Make("warcry", 0.7f, (t, k) =>
            {
                float saw(float f) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
                float s = saw(110f) * 0.5f + saw(110f * 1.5f) * 0.35f + saw(220f * 1.005f) * 0.25f;
                return s * AttackDecay(k, 0.1f) * 0.8f;
            });
        }

        public static AudioClip Nova()
        {
            var lp = new LowPass();
            return Make("nova", 0.5f, (t, k) =>
                (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(600f, 1600f, k) * t) * 0.3f + lp.Step(Noise(), 0.6f) * 0.6f) * AttackDecay(k, 0.08f));
        }

        public static AudioClip Soul() => Chime("soul", new[] { 1320f }, 0f, 0.12f);

        public static AudioClip Ambient()
        {
            var lp = new LowPass();
            var lp2 = new LowPass();
            return Make("ambient", 8f, (t, k) =>
            {
                float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 55.6f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 82.4f * t) * 0.2f;
                float wind = lp2.Step(lp.Step(Noise(), 0.02f), 0.05f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t));
                return drone * 0.35f + wind * 6f;
            }, loop: true);
        }
    }
}
