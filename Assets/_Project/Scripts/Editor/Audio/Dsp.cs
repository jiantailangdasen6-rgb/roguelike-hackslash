using System;
using System.IO;
using UnityEngine;

namespace Game.EditorTools.Audio
{
    /// <summary>Small offline DSP toolkit for rendering the game's sounds and music in the editor.</summary>
    public static class Dsp
    {
        public const float TwoPi = Mathf.PI * 2f;

        public static float MidiToHz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        /// <summary>Deterministic white noise.</summary>
        public sealed class Noise
        {
            private uint _state;
            public Noise(uint seed) => _state = seed * 747796405u + 2891336453u;

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / (float)0x7FFFFF - 1f;
            }
        }

        /// <summary>Band-limited sawtooth (PolyBLEP).</summary>
        public static float Saw(ref float phase, float freq, float rate)
        {
            float dt = freq / rate;
            phase += dt;
            if (phase >= 1f) phase -= 1f;
            float v = 2f * phase - 1f;
            return v - PolyBlep(phase, dt);
        }

        public static float Square(ref float phase, float freq, float rate, float width = 0.5f)
        {
            float dt = freq / rate;
            phase += dt;
            if (phase >= 1f) phase -= 1f;
            float v = phase < width ? 1f : -1f;
            v += PolyBlep(phase, dt);
            float p2 = phase - width;
            if (p2 < 0f) p2 += 1f;
            v -= PolyBlep(p2, dt);
            return v;
        }

        private static float PolyBlep(float t, float dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }
            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }
            return 0f;
        }

        public static float Sine(ref float phase, float freq, float rate)
        {
            phase += freq / rate;
            if (phase >= 1f) phase -= 1f;
            return Mathf.Sin(phase * TwoPi);
        }

        public static float Triangle(ref float phase, float freq, float rate)
        {
            phase += freq / rate;
            if (phase >= 1f) phase -= 1f;
            return 1f - 4f * Mathf.Abs(phase - 0.5f);
        }

        /// <summary>Attack/decay/sustain/release envelope; t in seconds, gate = held length.</summary>
        public static float Adsr(float t, float gate, float a, float d, float s, float r)
        {
            float level;
            if (t < a) level = t / Mathf.Max(0.0001f, a);
            else if (t < a + d) level = 1f - (1f - s) * (t - a) / Mathf.Max(0.0001f, d);
            else level = s;
            if (t > gate)
            {
                float held = gate < a ? gate / Mathf.Max(0.0001f, a) : gate < a + d ? 1f - (1f - s) * (gate - a) / Mathf.Max(0.0001f, d) : s;
                level = held * Mathf.Max(0f, 1f - (t - gate) / Mathf.Max(0.0001f, r));
            }
            return level;
        }

        public static float SoftClip(float x) => x / (1f + Mathf.Abs(x));

        /// <summary>State-variable filter (Chamberlin, oversampled twice): low/band/high outputs.</summary>
        public sealed class Svf
        {
            private float _low, _band;
            public float Low, Band, High;

            public void Process(float input, float cutoff, float q, float rate)
            {
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, rate * 0.2f) / (rate * 2f));
                float damp = 1f / Mathf.Max(0.5f, q);
                for (int i = 0; i < 2; i++)
                {
                    _low += f * _band;
                    float high = input - _low - damp * _band;
                    _band += f * high;
                    High = high;
                }
                Low = _low;
                Band = _band;
            }
        }

        /// <summary>One-pole low-pass.</summary>
        public sealed class OnePole
        {
            private float _y;
            public float Process(float x, float cutoff, float rate)
            {
                float a = 1f - Mathf.Exp(-TwoPi * cutoff / rate);
                return _y += a * (x - _y);
            }
        }

        /// <summary>Freeverb-style stereo reverb.</summary>
        public sealed class Reverb
        {
            private static readonly int[] CombTunings = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            private static readonly int[] AllpassTunings = { 556, 441, 341, 225 };
            private const int StereoSpread = 23;

            private readonly float[][] _combL, _combR, _apL, _apR;
            private readonly int[] _ciL, _ciR, _aiL, _aiR;
            private readonly float[] _filterL, _filterR;
            private readonly float _feedback, _damp;

            public Reverb(int rate, float roomSize = 0.84f, float damp = 0.3f)
            {
                float scale = rate / 44100f;
                _combL = new float[CombTunings.Length][];
                _combR = new float[CombTunings.Length][];
                for (int i = 0; i < CombTunings.Length; i++)
                {
                    _combL[i] = new float[Mathf.Max(1, (int)(CombTunings[i] * scale))];
                    _combR[i] = new float[Mathf.Max(1, (int)((CombTunings[i] + StereoSpread) * scale))];
                }
                _apL = new float[AllpassTunings.Length][];
                _apR = new float[AllpassTunings.Length][];
                for (int i = 0; i < AllpassTunings.Length; i++)
                {
                    _apL[i] = new float[Mathf.Max(1, (int)(AllpassTunings[i] * scale))];
                    _apR[i] = new float[Mathf.Max(1, (int)((AllpassTunings[i] + StereoSpread) * scale))];
                }
                _ciL = new int[CombTunings.Length];
                _ciR = new int[CombTunings.Length];
                _aiL = new int[AllpassTunings.Length];
                _aiR = new int[AllpassTunings.Length];
                _filterL = new float[CombTunings.Length];
                _filterR = new float[CombTunings.Length];
                _feedback = roomSize;
                _damp = damp;
            }

            public void Process(float input, out float left, out float right)
            {
                float l = 0f, r = 0f;
                input *= 0.015f;
                for (int i = 0; i < _combL.Length; i++)
                {
                    l += Comb(_combL[i], ref _ciL[i], ref _filterL[i], input);
                    r += Comb(_combR[i], ref _ciR[i], ref _filterR[i], input);
                }
                for (int i = 0; i < _apL.Length; i++)
                {
                    l = Allpass(_apL[i], ref _aiL[i], l);
                    r = Allpass(_apR[i], ref _aiR[i], r);
                }
                left = l;
                right = r;
            }

            private float Comb(float[] buf, ref int idx, ref float filter, float input)
            {
                float output = buf[idx];
                filter = output * (1f - _damp) + filter * _damp;
                buf[idx] = input + filter * _feedback;
                if (++idx >= buf.Length) idx = 0;
                return output;
            }

            private static float Allpass(float[] buf, ref int idx, float input)
            {
                float b = buf[idx];
                float output = -input + b;
                buf[idx] = input + b * 0.5f;
                if (++idx >= buf.Length) idx = 0;
                return output;
            }
        }

        // ------------------------------------------------------------------ Buffers

        public static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (var s in data) max = Mathf.Max(max, Mathf.Abs(s));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        /// <summary>Writes 16-bit PCM; interleaved when channels = 2.</summary>
        public static void WriteWav(string path, float[] samples, int channels, int rate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
            w.Flush();
            Art.ArtIO.WriteBytes(path, stream.ToArray());
        }
    }
}
