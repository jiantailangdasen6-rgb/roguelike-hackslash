using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Audio
{
    /// <summary>
    /// Sound effects, rendered offline: layered transients, filtered noise, FM and Karplus-Strong voices,
    /// then a short room reverb so they sit in the crypt instead of sounding dry and synthetic.
    /// </summary>
    public static class SfxDesign
    {
        public const int Rate = 44100;

        private delegate float Voice(float t, float k);

        /// <summary>name → (mono samples)</summary>
        public static Dictionary<string, float[]> All() => new()
        {
            ["swing"] = Render(0.24f, 0.08f, Swing()),
            ["hit"] = Render(0.22f, 0.12f, Hit(1f)),
            ["heavyhit"] = Render(0.38f, 0.16f, HeavyHit()),
            ["bolt"] = Render(0.32f, 0.2f, Bolt()),
            ["arrow"] = Render(0.3f, 0.1f, Arrow()),
            ["shatter"] = Render(0.7f, 0.25f, Shatter()),
            ["death"] = Render(0.6f, 0.15f, BoneBreak()),
            ["hurt"] = Render(0.3f, 0.12f, Hurt()),
            ["slam"] = Render(0.9f, 0.25f, Slam()),
            ["telegraph"] = Render(0.35f, 0.2f, Telegraph()),
            ["warcry"] = Render(0.9f, 0.3f, WarCry()),
            ["nova"] = Render(0.7f, 0.3f, Nova()),
            ["heal"] = Render(1.3f, 0.35f, Chime(new[] { 72f, 76f, 79f, 84f, 88f }, 0.06f, choir: true)),
            ["levelup"] = Render(1.6f, 0.35f, Chime(new[] { 67f, 72f, 76f, 79f, 84f }, 0.08f, choir: true)),
            ["clear"] = Render(2.2f, 0.4f, Chime(new[] { 62f, 69f, 74f, 77f, 81f, 86f }, 0.11f, choir: true)),
            ["soul"] = Render(0.25f, 0.2f, Chime(new[] { 88f }, 0f, choir: false)),
            ["explosion"] = Render(1.4f, 0.3f, Explosion()),
            ["spikes"] = Render(0.35f, 0.12f, Spikes()),
            ["flame"] = Render(1.2f, 0.15f, Flame()),
            ["clank"] = Render(0.3f, 0.15f, Clank()),
            ["blink"] = Render(0.5f, 0.3f, Blink()),
            ["orb"] = Render(0.4f, 0.2f, Orb()),
            ["screech"] = Render(0.35f, 0.15f, Screech()),
            ["roar"] = Render(1.6f, 0.3f, Roar()),
            ["downed"] = Render(1.2f, 0.3f, Downed()),
            ["revive"] = Render(1.2f, 0.35f, Revive()),
            ["fuse"] = Render(0.55f, 0.05f, Fuse()),
            ["click"] = Render(0.08f, 0.05f, Click()),
        };

        private static float[] Render(float seconds, float wet, Voice voice)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            int tail = (int)(Rate * 0.35f);
            var dry = new float[n + tail];
            for (int i = 0; i < n; i++) dry[i] = voice(i / (float)Rate, i / (float)n);
            var reverb = new Dsp.Reverb(Rate, 0.62f, 0.45f);
            var output = new float[dry.Length];
            for (int i = 0; i < dry.Length; i++)
            {
                reverb.Process(dry[i], out float l, out float r);
                output[i] = dry[i] + (l + r) * 0.5f * wet * 3f;
            }
            Dsp.Normalize(output, 0.92f);
            // Trim silence at the end.
            int last = output.Length - 1;
            while (last > n && Mathf.Abs(output[last]) < 0.002f) last--;
            Array.Resize(ref output, last + 1);
            // Short fade-out to avoid a click.
            int fade = Mathf.Min(400, output.Length / 4);
            for (int i = 0; i < fade; i++) output[output.Length - 1 - i] *= i / (float)fade;
            return output;
        }

        private static float Exp(float x, float rate) => Mathf.Exp(-x * rate);

        // ------------------------------------------------------------------ Combat

        private static Voice Swing()
        {
            var noise = new Dsp.Noise(1);
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI);
                f.Process(noise.Next(), Mathf.Lerp(600f, 3200f, Mathf.Sin(k * Mathf.PI)), 2.2f, Rate);
                return f.Band * env * 1.4f;
            };
        }

        private static Voice Hit(float pitch)
        {
            var noise = new Dsp.Noise(2);
            var f = new Dsp.Svf();
            float ph = 0f;
            return (t, k) =>
            {
                float thump = Mathf.Sin(TwoPiPhase(ref ph, Mathf.Lerp(190f, 70f, Mathf.Clamp01(t * 12f)) * pitch)) * Exp(t, 22f);
                f.Process(noise.Next(), 2400f * pitch, 1.2f, Rate);
                float crack = f.Band * Exp(t, 45f) * 1.6f;
                float crunch = noise.Next() * Exp(t, 90f) * 0.5f;
                return thump + crack + crunch;
            };
        }

        private static float TwoPiPhase(ref float phase, float freq)
        {
            phase += freq / Rate;
            if (phase > 1f) phase -= 1f;
            return phase * Dsp.TwoPi;
        }

        private static Voice HeavyHit()
        {
            var noise = new Dsp.Noise(3);
            var f = new Dsp.Svf();
            var lp = new Dsp.OnePole();
            float ph = 0f;
            return (t, k) =>
            {
                float body = Mathf.Sin(TwoPiPhase(ref ph, Mathf.Lerp(130f, 45f, Mathf.Clamp01(t * 6f)))) * Exp(t, 9f) * 1.2f;
                f.Process(noise.Next(), 1600f, 1f, Rate);
                float crack = f.Band * Exp(t, 30f) * 2f;
                float rumble = lp.Process(noise.Next(), 300f, Rate) * Exp(t, 8f) * 2.5f;
                return Dsp.SoftClip((body + crack + rumble) * 1.5f);
            };
        }

        private static Voice Bolt()
        {
            var noise = new Dsp.Noise(4);
            var f = new Dsp.Svf();
            float c = 0f, m = 0f;
            return (t, k) =>
            {
                float freq = Mathf.Lerp(1400f, 520f, Mathf.Sqrt(k));
                m += freq * 1.5f / Rate; if (m > 1f) m -= 1f;
                c += (freq + Mathf.Sin(m * Dsp.TwoPi) * freq * 2.2f * Exp(t, 12f)) / Rate; if (c > 1f) c -= 1f;
                f.Process(noise.Next(), 6000f, 3f, Rate);
                float sparkle = f.Band * 0.25f * (0.5f + 0.5f * Mathf.Sin(t * 180f));
                return (Mathf.Sin(c * Dsp.TwoPi) * 0.7f + sparkle) * Dsp.Adsr(t, 0.12f, 0.005f, 0.05f, 0.5f, 0.15f);
            };
        }

        private static Voice Arrow()
        {
            // Bow string (Karplus-Strong) + a fast air whoosh.
            int period = Rate / 196;
            var buffer = new float[period];
            var noise = new Dsp.Noise(5);
            for (int i = 0; i < period; i++) buffer[i] = noise.Next();
            int idx = 0;
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float s = buffer[idx];
                int next = (idx + 1) % period;
                buffer[idx] = 0.496f * (buffer[idx] + buffer[next]);
                idx = next;
                f.Process(noise.Next(), Mathf.Lerp(4000f, 1200f, k), 1.5f, Rate);
                return s * Exp(t, 7f) * 0.9f + f.Band * Exp(t, 9f) * 0.8f;
            };
        }

        private static Voice Shatter()
        {
            var noise = new Dsp.Noise(6);
            var rng = new System.Random(6);
            var pings = new (float Freq, float Start)[14];
            for (int i = 0; i < pings.Length; i++) pings[i] = (1800f + (float)rng.NextDouble() * 4800f, (float)rng.NextDouble() * 0.25f);
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                f.Process(noise.Next(), 5000f, 0.8f, Rate);
                float s = f.High * Exp(t, 10f) * 0.9f;
                foreach (var (freq, start) in pings)
                    if (t > start) s += Mathf.Sin((t - start) * freq * Dsp.TwoPi) * Exp(t - start, 16f) * 0.22f;
                return s + Mathf.Sin(t * 90f * Dsp.TwoPi) * Exp(t, 20f) * 0.6f;
            };
        }

        private static Voice BoneBreak()
        {
            // A clatter of dry bone clicks tumbling down.
            var noise = new Dsp.Noise(7);
            var rng = new System.Random(7);
            var clicks = new (float Start, float Freq)[9];
            float at = 0f;
            for (int i = 0; i < clicks.Length; i++)
            {
                clicks[i] = (at, 900f + (float)rng.NextDouble() * 1800f);
                at += 0.025f + (float)rng.NextDouble() * 0.06f;
            }
            var f = new Dsp.Svf();
            float ph = 0f;
            return (t, k) =>
            {
                float s = Mathf.Sin(TwoPiPhase(ref ph, Mathf.Lerp(110f, 50f, k))) * Exp(t, 10f) * 0.6f;
                foreach (var (start, freq) in clicks)
                {
                    if (t < start) continue;
                    float dt = t - start;
                    f.Process(noise.Next(), freq, 6f, Rate);
                    s += Mathf.Sin(dt * freq * Dsp.TwoPi) * Exp(dt, 70f) * 0.5f + noise.Next() * Exp(dt, 160f) * 0.4f;
                }
                return s;
            };
        }

        private static Voice Hurt()
        {
            var noise = new Dsp.Noise(8);
            float ph = 0f;
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float grunt = Dsp.Saw(ref ph, Mathf.Lerp(170f, 95f, k), Rate);
                f.Process(grunt, 900f, 2f, Rate);
                return Dsp.SoftClip(f.Band * 2.5f) * Exp(t, 9f) + noise.Next() * Exp(t, 40f) * 0.6f;
            };
        }

        private static Voice Slam()
        {
            var noise = new Dsp.Noise(9);
            var lp = new Dsp.OnePole();
            var f = new Dsp.Svf();
            float ph = 0f;
            return (t, k) =>
            {
                float boom = Mathf.Sin(TwoPiPhase(ref ph, Mathf.Lerp(80f, 30f, Mathf.Clamp01(t * 3f)))) * Exp(t, 4f) * 1.4f;
                float rubble = lp.Process(noise.Next(), 500f, Rate) * Exp(t, 3.5f) * 3f;
                f.Process(noise.Next(), 2000f, 1f, Rate);
                return Dsp.SoftClip((boom + rubble + f.Band * Exp(t, 25f)) * 1.3f);
            };
        }

        private static Voice Telegraph()
        {
            // A low, ominous swell (something is about to strike).
            float a = 0f, b = 0f;
            var noise = new Dsp.Noise(10);
            var lp = new Dsp.OnePole();
            return (t, k) =>
            {
                float env = Mathf.Sin(k * Mathf.PI);
                float tone = Dsp.Saw(ref a, 110f, Rate) * 0.4f + Dsp.Saw(ref b, 110f * 1.498f, Rate) * 0.3f;
                return lp.Process(tone + noise.Next() * 0.4f, Mathf.Lerp(300f, 1400f, k), Rate) * env;
            };
        }

        private static Voice WarCry()
        {
            // Brassy horn blast with a growl.
            var p = new float[4];
            float vib = 0f;
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float v = 1f + 0.01f * Mathf.Sin(TwoPiPhase(ref vib, 6f));
                float s = Dsp.Saw(ref p[0], 98f * v, Rate) + Dsp.Saw(ref p[1], 146.8f * v, Rate) * 0.8f
                          + Dsp.Saw(ref p[2], 196f * v * 1.004f, Rate) * 0.6f + Dsp.Saw(ref p[3], 49f, Rate) * 0.5f;
                float env = Dsp.Adsr(t, 0.55f, 0.06f, 0.2f, 0.75f, 0.3f);
                f.Process(s, 300f + 2200f * env, 1.3f, Rate);
                return Dsp.SoftClip(f.Low * 0.6f) * env;
            };
        }

        private static Voice Nova()
        {
            var noise = new Dsp.Noise(11);
            var f = new Dsp.Svf();
            var rng = new System.Random(11);
            var pings = new float[6];
            for (int i = 0; i < pings.Length; i++) pings[i] = 2200f + (float)rng.NextDouble() * 3000f;
            return (t, k) =>
            {
                f.Process(noise.Next(), Mathf.Lerp(8000f, 1500f, k), 1.5f, Rate);
                float s = f.Band * Dsp.Adsr(t, 0.2f, 0.01f, 0.1f, 0.5f, 0.4f) * 1.4f;
                for (int i = 0; i < pings.Length; i++)
                {
                    float st = i * 0.04f;
                    if (t > st) s += Mathf.Sin((t - st) * pings[i] * Dsp.TwoPi) * Exp(t - st, 9f) * 0.18f;
                }
                return s;
            };
        }

        private static Voice Chime(float[] notes, float step, bool choir)
        {
            // FM bells (+ a soft choir pad underneath for the big moments).
            var phases = new float[notes.Length * 2];
            var choirPh = new float[6];
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float s = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * step;
                    if (t < start) continue;
                    float dt = t - start;
                    float hz = Dsp.MidiToHz(notes[i]);
                    phases[i * 2 + 1] += hz * 3.5f / Rate;
                    float mod = Mathf.Sin(phases[i * 2 + 1] * Dsp.TwoPi) * 2.2f * Exp(dt, 6f);
                    phases[i * 2] += hz / Rate;
                    s += Mathf.Sin(phases[i * 2] * Dsp.TwoPi + mod) * Exp(dt, 3.2f) * 0.35f;
                }
                if (choir)
                {
                    float c = 0f;
                    for (int v = 0; v < choirPh.Length; v++)
                        c += Dsp.Saw(ref choirPh[v], Dsp.MidiToHz(notes[0] + (v % 3) * 4 - 12) * (1f + (v - 2.5f) * 0.003f), Rate);
                    f.Process(c, 900f, 3f, Rate);
                    s += f.Band * 0.12f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                }
                return s;
            };
        }

        // ------------------------------------------------------------------ World

        private static Voice Explosion()
        {
            var noise = new Dsp.Noise(12);
            var lp = new Dsp.OnePole();
            var lp2 = new Dsp.OnePole();
            var f = new Dsp.Svf();
            float ph = 0f;
            return (t, k) =>
            {
                float boom = Mathf.Sin(TwoPiPhase(ref ph, Mathf.Lerp(70f, 25f, Mathf.Clamp01(t * 2f)))) * Exp(t, 3f) * 1.6f;
                float roar = lp.Process(noise.Next(), Mathf.Lerp(2500f, 300f, k), Rate) * Exp(t, 2.5f) * 3.5f;
                float crackle = (noise.Next() > 0.97f ? noise.Next() : 0f) * Exp(t, 2f) * 1.5f;
                f.Process(noise.Next(), 4000f, 0.8f, Rate);
                float snap = f.Band * Exp(t, 35f) * 2f;
                return Dsp.SoftClip((boom + roar + crackle + snap + lp2.Process(noise.Next(), 120f, Rate) * Exp(t, 1.8f) * 4f) * 1.4f);
            };
        }

        private static Voice Spikes()
        {
            var noise = new Dsp.Noise(13);
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                f.Process(noise.Next(), 3500f, 4f, Rate);
                float scrape = f.Band * Exp(t, 14f) * 1.5f;
                float ring = (Mathf.Sin(t * 2100f * Dsp.TwoPi) + Mathf.Sin(t * 3170f * Dsp.TwoPi) * 0.6f) * Exp(t, 18f) * 0.35f;
                float thud = Mathf.Sin(t * 120f * Dsp.TwoPi) * Exp(t, 25f) * 0.8f;
                return scrape + ring + thud;
            };
        }

        private static Voice Flame()
        {
            var noise = new Dsp.Noise(14);
            var lp = new Dsp.OnePole();
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float env = Dsp.Adsr(t, 0.85f, 0.05f, 0.2f, 0.8f, 0.3f);
                f.Process(noise.Next(), 900f + 400f * Mathf.Sin(t * 13f), 0.9f, Rate);
                float roar = lp.Process(noise.Next(), 600f, Rate) * 3f;
                float crackle = noise.Next() > 0.985f ? noise.Next() * 1.2f : 0f;
                return (f.Band * 1.3f + roar + crackle) * env;
            };
        }

        private static Voice Clank()
        {
            float[] partials = { 523f, 1187f, 1873f, 2741f };
            return (t, k) =>
            {
                float s = 0f;
                for (int i = 0; i < partials.Length; i++) s += Mathf.Sin(t * partials[i] * Dsp.TwoPi) * Exp(t, 9f + i * 6f) / (i + 1);
                return s + Mathf.Sin(t * 160f * Dsp.TwoPi) * Exp(t, 30f) * 0.5f;
            };
        }

        private static Voice Blink()
        {
            var noise = new Dsp.Noise(15);
            var f = new Dsp.Svf();
            float ph = 0f;
            return (t, k) =>
            {
                f.Process(noise.Next(), Mathf.Lerp(300f, 5000f, k * k), 3f, Rate);
                float whoosh = f.Band * Mathf.Sin(k * Mathf.PI) * 1.4f;
                float tone = Mathf.Sin(TwoPiPhase(ref ph, Mathf.Lerp(200f, 900f, k))) * Mathf.Sin(k * Mathf.PI) * 0.3f;
                return whoosh + tone;
            };
        }

        private static Voice Orb()
        {
            float c = 0f, m = 0f;
            return (t, k) =>
            {
                float freq = Mathf.Lerp(320f, 180f, k);
                m += freq * 0.5f / Rate; if (m > 1f) m -= 1f;
                c += (freq + Mathf.Sin(m * Dsp.TwoPi) * freq * 1.5f) / Rate; if (c > 1f) c -= 1f;
                return Mathf.Sin(c * Dsp.TwoPi) * Dsp.Adsr(t, 0.2f, 0.02f, 0.1f, 0.6f, 0.15f);
            };
        }

        private static Voice Screech()
        {
            float ph = 0f;
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float freq = 2600f + 900f * Mathf.Sin(k * Mathf.PI) + 300f * Mathf.Sin(t * 90f);
                f.Process(Dsp.Saw(ref ph, freq, Rate), 3500f, 4f, Rate);
                return f.Band * Mathf.Sin(k * Mathf.PI) * 0.8f;
            };
        }

        private static Voice Roar()
        {
            var p = new float[5];
            var noise = new Dsp.Noise(16);
            var f1 = new Dsp.Svf();
            var f2 = new Dsp.Svf();
            float lfo = 0f;
            return (t, k) =>
            {
                float wob = 1f + 0.04f * Mathf.Sin(TwoPiPhase(ref lfo, 7f));
                float baseHz = Mathf.Lerp(70f, 55f, k) * wob;
                float s = 0f;
                for (int i = 0; i < p.Length; i++) s += Dsp.Saw(ref p[i], baseHz * (1f + i * 0.007f) * (i == 4 ? 0.5f : 1f), Rate);
                s += noise.Next() * 0.8f;
                f1.Process(s, 500f, 4f, Rate);
                f2.Process(s, 1100f, 4f, Rate);
                float env = Dsp.Adsr(t, 1.1f, 0.15f, 0.3f, 0.8f, 0.4f);
                return Dsp.SoftClip((f1.Band + f2.Band * 0.6f) * 0.8f) * env;
            };
        }

        private static Voice Downed()
        {
            var p = new float[3];
            var noise = new Dsp.Noise(17);
            var lp = new Dsp.OnePole();
            return (t, k) =>
            {
                float thud = Mathf.Sin(t * 70f * Dsp.TwoPi) * Exp(t, 8f) + lp.Process(noise.Next(), 400f, Rate) * Exp(t, 10f) * 2f;
                float toll = 0f;
                float[] hz = { 146.8f, 174.6f, 220f };
                for (int i = 0; i < 3; i++) toll += Dsp.Triangle(ref p[i], hz[i] * 0.5f, Rate) * Exp(t, 2.2f);
                return thud + toll * 0.35f * Mathf.Clamp01(t * 8f);
            };
        }

        private static Voice Revive()
        {
            var phases = new float[8];
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                float rise = Mathf.Lerp(0f, 12f, Mathf.Clamp01(k * 1.5f));
                float s = 0f;
                float[] chord = { 60f, 64f, 67f, 72f };
                for (int i = 0; i < chord.Length; i++)
                {
                    s += Dsp.Saw(ref phases[i], Dsp.MidiToHz(chord[i] + rise), Rate);
                    s += Dsp.Saw(ref phases[i + 4], Dsp.MidiToHz(chord[i] + rise) * 1.004f, Rate);
                }
                f.Process(s, 1200f + 2000f * k, 2f, Rate);
                return f.Band * 0.25f * Dsp.Adsr(t, 0.8f, 0.15f, 0.2f, 0.8f, 0.4f);
            };
        }

        private static Voice Fuse()
        {
            var noise = new Dsp.Noise(18);
            var f = new Dsp.Svf();
            return (t, k) =>
            {
                f.Process(noise.Next(), 6000f, 1f, Rate);
                float sizzle = f.High * (0.6f + 0.4f * Mathf.Sin(t * 120f)) * (0.4f + 0.6f * k);
                return sizzle;
            };
        }

        private static Voice Click()
        {
            return (t, k) => Mathf.Sin(t * 1800f * Dsp.TwoPi) * Exp(t, 80f) + Mathf.Sin(t * 600f * Dsp.TwoPi) * Exp(t, 50f) * 0.5f;
        }
    }
}
