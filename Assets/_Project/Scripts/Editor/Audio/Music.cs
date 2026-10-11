using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Audio
{
    public enum Inst
    {
        Taiko,
        Snare,
        Hat,
        Cymbal,
        Bass,
        Strings,
        Pad,
        Choir,
        Organ,
        Bell,
        Harp,
        Brass,
        Drone,
    }

    public struct Note
    {
        public Inst Inst;
        public float Start;  // beats
        public float Length; // beats
        public float Pitch;  // midi
        public float Velocity;
    }

    /// <summary>A piece of music as notes on a beat grid, plus tempo and length.</summary>
    public sealed class Song
    {
        public string Name;
        public float Bpm;
        public int Bars;
        public bool Loop = true;
        public readonly List<Note> Notes = new();

        public float BeatSeconds => 60f / Bpm;
        public float Seconds => Bars * 4 * BeatSeconds;

        public void Add(Inst inst, float start, float length, float pitch, float velocity = 1f) =>
            Notes.Add(new Note { Inst = inst, Start = start, Length = length, Pitch = pitch, Velocity = velocity });

        public void Chord(Inst inst, float start, float length, IEnumerable<float> pitches, float velocity = 1f)
        {
            foreach (var p in pitches) Add(inst, start, length, p, velocity);
        }

        /// <summary>16 steps per bar: 'X' accent, 'x' normal, 'o' soft, '.' rest.</summary>
        public void Pattern(Inst inst, int bar, string steps, float pitch, float velocity = 1f)
        {
            for (int i = 0; i < steps.Length && i < 16; i++)
            {
                char c = steps[i];
                if (c == '.') continue;
                float v = c == 'X' ? 1f : c == 'x' ? 0.75f : 0.45f;
                Add(inst, bar * 4 + i * 0.25f, 0.25f, pitch, v * velocity);
            }
        }
    }

    /// <summary>Renders a <see cref="Song"/> to stereo samples with a shared hall reverb; loops are made seamless.</summary>
    public static class MusicRenderer
    {
        public const int Rate = 32000;

        private struct Patch
        {
            public float Gain, Pan, Send, Release;
        }

        private static Patch PatchOf(Inst inst) => inst switch
        {
            Inst.Taiko => new Patch { Gain = 0.9f, Pan = 0f, Send = 0.35f, Release = 0.9f },
            Inst.Snare => new Patch { Gain = 0.38f, Pan = 0.15f, Send = 0.35f, Release = 0.25f },
            Inst.Hat => new Patch { Gain = 0.12f, Pan = -0.3f, Send = 0.15f, Release = 0.08f },
            Inst.Cymbal => new Patch { Gain = 0.22f, Pan = 0.25f, Send = 0.5f, Release = 2.5f },
            Inst.Bass => new Patch { Gain = 0.42f, Pan = 0f, Send = 0.08f, Release = 0.12f },
            Inst.Strings => new Patch { Gain = 0.16f, Pan = -0.35f, Send = 0.4f, Release = 0.15f },
            Inst.Pad => new Patch { Gain = 0.1f, Pan = 0.1f, Send = 0.6f, Release = 1.4f },
            Inst.Choir => new Patch { Gain = 0.2f, Pan = 0.3f, Send = 0.75f, Release = 1.2f },
            Inst.Organ => new Patch { Gain = 0.11f, Pan = -0.1f, Send = 0.7f, Release = 0.5f },
            Inst.Bell => new Patch { Gain = 0.16f, Pan = 0.4f, Send = 0.6f, Release = 3f },
            Inst.Harp => new Patch { Gain = 0.3f, Pan = -0.25f, Send = 0.5f, Release = 1.6f },
            Inst.Brass => new Patch { Gain = 0.2f, Pan = 0.2f, Send = 0.5f, Release = 0.3f },
            _ => new Patch { Gain = 0.25f, Pan = 0f, Send = 0.5f, Release = 2f },
        };

        public static float[] Render(Song song)
        {
            int length = Mathf.CeilToInt(song.Seconds * Rate);
            int tail = Rate * 4;
            var left = new float[length + tail];
            var right = new float[length + tail];
            var send = new float[length + tail];
            var voice = new float[Rate * 12];
            int seed = 1;

            foreach (var note in song.Notes)
            {
                var patch = PatchOf(note.Inst);
                float gate = note.Length * song.BeatSeconds;
                int n = Mathf.Min(voice.Length, Mathf.CeilToInt((gate + patch.Release) * Rate));
                Array.Clear(voice, 0, n);
                RenderVoice(note.Inst, Dsp.MidiToHz(note.Pitch), gate, voice, n, seed++);
                int start = Mathf.RoundToInt(note.Start * song.BeatSeconds * Rate);
                float g = patch.Gain * note.Velocity;
                float pl = Mathf.Sqrt(0.5f * (1f - patch.Pan)), pr = Mathf.Sqrt(0.5f * (1f + patch.Pan));
                for (int i = 0; i < n; i++)
                {
                    int at = start + i;
                    if (at >= left.Length) break;
                    float s = voice[i] * g;
                    left[at] += s * pl;
                    right[at] += s * pr;
                    send[at] += s * patch.Send;
                }
            }

            var hall = new Dsp.Reverb(Rate, 0.88f, 0.35f);
            for (int i = 0; i < left.Length; i++)
            {
                hall.Process(send[i], out float l, out float r);
                left[i] += l * 2.2f;
                right[i] += r * 2.2f;
            }

            int total = song.Loop ? length : left.Length;
            if (song.Loop)
                for (int i = 0; i < tail; i++)
                {
                    // Seamless loop: what rings past the end continues at the start.
                    left[i] += left[length + i];
                    right[i] += right[length + i];
                }

            var stereo = new float[total * 2];
            for (int i = 0; i < total; i++)
            {
                stereo[i * 2] = Dsp.SoftClip(left[i] * 1.2f);
                stereo[i * 2 + 1] = Dsp.SoftClip(right[i] * 1.2f);
            }
            Dsp.Normalize(stereo, 0.85f);
            return stereo;
        }

        private static void RenderVoice(Inst inst, float hz, float gate, float[] o, int n, int seed)
        {
            var noise = new Dsp.Noise((uint)seed);
            float rate = Rate;
            switch (inst)
            {
                case Inst.Taiko:
                {
                    float ph = 0f;
                    var lp = new Dsp.OnePole();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float f = hz * (1f + 0.8f * Mathf.Exp(-t * 30f));
                        ph += f / rate;
                        float body = Mathf.Sin(ph * Dsp.TwoPi) * Mathf.Exp(-t * 6f);
                        float skin = lp.Process(noise.Next(), 900f, rate) * Mathf.Exp(-t * 20f) * 2f;
                        o[i] = Dsp.SoftClip((body + skin) * 1.6f);
                    }
                    break;
                }
                case Inst.Snare:
                {
                    var f = new Dsp.Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        f.Process(noise.Next(), 2200f, 0.9f, rate);
                        o[i] = f.Band * Mathf.Exp(-t * 18f) * 1.5f + Mathf.Sin(t * 190f * Dsp.TwoPi) * Mathf.Exp(-t * 30f) * 0.7f;
                    }
                    break;
                }
                case Inst.Hat:
                {
                    var f = new Dsp.Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        f.Process(noise.Next(), 7000f, 0.7f, rate);
                        o[i] = f.High * Mathf.Exp(-t * 60f);
                    }
                    break;
                }
                case Inst.Cymbal:
                {
                    var f = new Dsp.Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        f.Process(noise.Next(), 6000f, 0.6f, rate);
                        float env = t < gate ? t / Mathf.Max(0.01f, gate) : Mathf.Exp(-(t - gate) * 2.5f); // reverse swell then decay
                        o[i] = f.High * env * env;
                    }
                    break;
                }
                case Inst.Bass:
                {
                    float a = 0f, b = 0f;
                    var f = new Dsp.Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float env = Dsp.Adsr(t, gate, 0.005f, 0.12f, 0.6f, 0.1f);
                        float s = Dsp.Saw(ref a, hz, rate) * 0.6f + Dsp.Sine(ref b, hz * 0.5f, rate) * 0.8f;
                        f.Process(s, 220f + 900f * Mathf.Exp(-t * 12f), 1.2f, rate);
                        o[i] = f.Low * env;
                    }
                    break;
                }
                case Inst.Strings:
                {
                    var ph = new float[3];
                    var f = new Dsp.Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float env = Dsp.Adsr(t, gate, 0.012f, 0.12f, 0.45f, 0.15f);
                        float s = Dsp.Saw(ref ph[0], hz * 0.996f, rate) + Dsp.Saw(ref ph[1], hz, rate) + Dsp.Saw(ref ph[2], hz * 1.005f, rate);
                        f.Process(s, 2400f, 0.8f, rate);
                        o[i] = f.Low * env * 0.5f;
                    }
                    break;
                }
                case Inst.Pad:
                {
                    var ph = new float[4];
                    var f = new Dsp.Svf();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float env = Dsp.Adsr(t, gate, 0.7f, 0.5f, 0.85f, 1.3f);
                        float s = 0f;
                        for (int v = 0; v < 4; v++) s += Dsp.Saw(ref ph[v], hz * (1f + (v - 1.5f) * 0.004f), rate);
                        f.Process(s, 1300f + 300f * Mathf.Sin(t * 0.7f), 0.7f, rate);
                        o[i] = f.Low * env * 0.35f;
                    }
                    break;
                }
                case Inst.Choir:
                {
                    // "Aah": detuned saws with vibrato through three vowel formants.
                    var ph = new float[3];
                    var f1 = new Dsp.Svf();
                    var f2 = new Dsp.Svf();
                    var f3 = new Dsp.Svf();
                    float vib = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float env = Dsp.Adsr(t, gate, 0.35f, 0.4f, 0.85f, 1.1f);
                        float v = 1f + 0.006f * Mathf.Sin(Dsp.TwoPi * (vib += 5.3f / rate)) * Mathf.Clamp01(t * 2f);
                        float s = 0f;
                        for (int k = 0; k < 3; k++) s += Dsp.Saw(ref ph[k], hz * v * (1f + (k - 1) * 0.005f), rate);
                        f1.Process(s, 700f, 6f, rate);
                        f2.Process(s, 1150f, 7f, rate);
                        f3.Process(s, 2600f, 8f, rate);
                        o[i] = (f1.Band + f2.Band * 0.6f + f3.Band * 0.25f) * env * 0.35f;
                    }
                    break;
                }
                case Inst.Organ:
                {
                    var ph = new float[6];
                    float[] mult = { 0.5f, 1f, 2f, 3f, 4f, 6f };
                    float[] amp = { 0.6f, 1f, 0.7f, 0.35f, 0.4f, 0.15f };
                    float trem = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float env = Dsp.Adsr(t, gate, 0.04f, 0.1f, 0.9f, 0.45f);
                        float s = 0f;
                        for (int k = 0; k < 6; k++) s += Dsp.Sine(ref ph[k], hz * mult[k], rate) * amp[k];
                        float tr = 1f + 0.08f * Mathf.Sin(Dsp.TwoPi * (trem += 5.8f / rate));
                        o[i] = s * env * tr * 0.3f;
                    }
                    break;
                }
                case Inst.Bell:
                {
                    float c = 0f, m = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        m += hz * 3.5f / rate;
                        float mod = Mathf.Sin(m * Dsp.TwoPi) * 2.5f * Mathf.Exp(-t * 4f);
                        c += hz / rate;
                        o[i] = Mathf.Sin(c * Dsp.TwoPi + mod) * Mathf.Exp(-t * 1.6f);
                    }
                    break;
                }
                case Inst.Harp:
                {
                    int period = Mathf.Max(2, Mathf.RoundToInt(rate / hz));
                    var buf = new float[period];
                    for (int i = 0; i < period; i++) buf[i] = noise.Next();
                    int idx = 0;
                    var lp = new Dsp.OnePole();
                    for (int i = 0; i < n; i++)
                    {
                        float s = buf[idx];
                        int next = (idx + 1) % period;
                        buf[idx] = 0.4985f * (buf[idx] + buf[next]);
                        idx = next;
                        o[i] = lp.Process(s, 3500f, rate) * Mathf.Min(1f, i / 30f);
                    }
                    break;
                }
                case Inst.Brass:
                {
                    var ph = new float[2];
                    var f = new Dsp.Svf();
                    float vib = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float env = Dsp.Adsr(t, gate, 0.08f, 0.25f, 0.8f, 0.25f);
                        float v = 1f + 0.005f * Mathf.Sin(Dsp.TwoPi * (vib += 5f / rate)) * Mathf.Clamp01((t - 0.3f) * 3f);
                        float s = Dsp.Saw(ref ph[0], hz * v, rate) + Dsp.Saw(ref ph[1], hz * v * 1.003f, rate);
                        f.Process(s, 350f + 2400f * env * env, 1.1f, rate);
                        o[i] = Dsp.SoftClip(f.Low * 0.9f) * env * 0.6f;
                    }
                    break;
                }
                default: // Drone
                {
                    float a = 0f, b = 0f;
                    var lp = new Dsp.OnePole();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / rate;
                        float env = Dsp.Adsr(t, gate, 1.5f, 0.5f, 0.9f, 2f);
                        float s = Dsp.Saw(ref a, hz, rate) * 0.5f + Dsp.Sine(ref b, hz * 1.5f, rate) * 0.4f;
                        o[i] = lp.Process(s, 400f + 150f * Mathf.Sin(t * 0.5f), rate) * env;
                    }
                    break;
                }
            }
        }
    }
}
