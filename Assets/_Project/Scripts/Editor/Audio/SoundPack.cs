using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Audio
{
    /// <summary>
    /// Builds the game's sounds from recorded CC0 / CC-BY material in Audio/Pack (credits: Docs/credits.md).
    /// Each sound is one or more variants; each variant layers clips (trimmed, re-pitched, delayed), then is
    /// levelled to a common loudness so nothing sticks out. Written over the synthesized files in
    /// Resources/Audio (sfx_name.wav, sfx_name_2.wav ...); music tracks are levelled and written as WAV too.
    ///   powershell -File Tools/RunMethod.ps1 -Method Game.EditorTools.Audio.SoundPack.Build -Tag '\[SoundPack\]'
    /// </summary>
    public static class SoundPack
    {
        public const string SourceDir = "Assets/_Project/Audio/Pack/";
        /// <summary>Bump when the recipes change so the builder renders the pack again.</summary>
        public const string Marker = "pack_v4.txt";
        private const int Rate = 44100;

        private struct Layer
        {
            public string File;
            public float Gain, Pitch, Delay, Start, Length, FadeOut;
        }

        private static Layer L(string file, float gain = 1f, float pitch = 1f, float delay = 0f, float start = -1f, float length = 0f, float fadeOut = 0.04f) =>
            new() { File = file, Gain = gain, Pitch = pitch, Delay = delay, Start = start, Length = length, FadeOut = fadeOut };

        private static Layer[] V(params Layer[] layers) => layers;

        /// <summary>One sound: its variants and how loud its loudest 50 ms should be (RMS, 0..1).</summary>
        private sealed class Sfx
        {
            public string Name;
            public float Loudness;
            public Layer[][] Variants;
        }

        private sealed class Track
        {
            public string Name;
            public string File;
            public float Loudness = 0.16f; // RMS over the whole track
            public float Start;
            public float Length;           // 0 = whole file (seamless loops stay seamless)
            public float FadeOut;
            // Variations of a track for another land: tape speed (pitch and tempo together), a low-pass, an echo, drive.
            public float Pitch = 1f;
            public float LowPass;          // cutoff in Hz (0 = none)
            public float Echo, EchoFeedback; // seconds, 0..1
            public float Drive;            // soft saturation (0 = none)
        }

        private static Sfx S(string name, float loudness, params Layer[][] variants) => new() { Name = name, Loudness = loudness, Variants = variants };

        // ------------------------------------------------------------------ Recipes

        private static IEnumerable<Sfx> Sounds()
        {
            const string K = "kenney/", J = "jc/", M = "leohpaz/", R = "rpgpack/", N = "starninjas/", Q = "qubodup/";

            // Blows: a meaty thud under a short punch so hits land with weight.
            yield return S("swing", 0.2f,
                V(L(J + "Sword_Swing_01.wav", length: 0.35f)), V(L(J + "Sword_Swing_02.wav", length: 0.35f)), V(L(J + "Sword_Swing_03.wav", length: 0.35f)));
            yield return S("hit", 0.26f,
                V(L(Q + "qubodupImpactMeat01.ogg"), L(K + "impactPunch_medium_000.ogg", 0.6f)),
                V(L(Q + "qubodupImpactMeat02.ogg"), L(K + "impactPunch_medium_001.ogg", 0.6f)),
                V(L(Q + "qubodupImpactMeat01.ogg", pitch: 0.9f), L(K + "impactPunch_medium_002.ogg", 0.6f)),
                V(L(Q + "qubodupImpactMeat02.ogg", pitch: 1.1f), L(K + "impactPunch_medium_003.ogg", 0.6f)));
            yield return S("heavyhit", 0.34f,
                V(L(K + "impactPunch_heavy_000.ogg"), L(Q + "qubodupImpactMeat01.ogg", 0.8f, 0.8f), L(J + "Heavy_Sword_Hit_Metal_01.wav", 0.25f, 0.8f)),
                V(L(K + "impactPunch_heavy_001.ogg"), L(Q + "qubodupImpactMeat02.ogg", 0.8f, 0.8f), L(J + "Heavy_Sword_Hit_Metal_02.wav", 0.25f, 0.8f)),
                V(L(K + "impactPunch_heavy_002.ogg"), L(Q + "qubodupImpactMeat01.ogg", 0.8f, 0.75f), L(J + "Heavy_Sword_Hit_Metal_03.wav", 0.25f, 0.8f)));
            yield return S("clank", 0.24f,
                V(L(N + "clash_1.ogg"), L(K + "impactPlate_heavy_000.ogg", 0.5f)),
                V(L(N + "clash_2.ogg"), L(K + "impactPlate_heavy_001.ogg", 0.5f)),
                V(L(N + "clash_3.ogg"), L(K + "impactPlate_heavy_002.ogg", 0.5f)));
            yield return S("shatter", 0.38f,
                V(L(J + "Ice_Shard_Projectile_Impact_01.wav"), L(K + "impactGlass_heavy_000.ogg", 0.8f), L(K + "impactPunch_heavy_003.ogg", 0.7f)),
                V(L(J + "Ice_Shard_Projectile_Impact_02.wav"), L(K + "impactGlass_heavy_001.ogg", 0.8f), L(K + "impactPunch_heavy_004.ogg", 0.7f)),
                V(L(J + "Ice_Shard_Projectile_Impact_03.wav"), L(K + "impactGlass_heavy_002.ogg", 0.8f), L(K + "impactPunch_heavy_000.ogg", 0.7f)));
            yield return S("death", 0.2f,
                V(L(K + "impactWood_medium_000.ogg"), L(K + "impactWood_light_001.ogg", 0.7f, delay: 0.07f)),
                V(L(K + "impactWood_medium_001.ogg"), L(K + "impactWood_light_002.ogg", 0.7f, delay: 0.06f)),
                V(L(K + "impactWood_medium_002.ogg"), L(K + "impactWood_light_003.ogg", 0.7f, delay: 0.08f)));
            yield return S("hurt", 0.32f,
                V(L(K + "impactSoft_heavy_000.ogg"), L(Q + "qubodupImpactMeat02.ogg", 0.8f, 0.7f)),
                V(L(K + "impactSoft_heavy_001.ogg"), L(Q + "qubodupImpactMeat01.ogg", 0.8f, 0.7f)));
            yield return S("downed", 0.3f,
                V(L(K + "impactSoft_heavy_002.ogg", pitch: 0.7f), L(K + "impactBell_heavy_000.ogg", 0.35f, 0.6f, delay: 0.05f, length: 1.6f, fadeOut: 0.6f)));

            // Skills and magic.
            yield return S("slam", 0.36f,
                V(L(M + "30_Earth_02.wav", length: 1.2f, fadeOut: 0.4f), L(K + "impactPunch_heavy_001.ogg", 0.8f, 0.7f)));
            yield return S("explosion", 0.4f,
                V(L(M + "04_Fire_explosion_04_medium.wav", length: 1.6f, fadeOut: 0.5f), L(J + "Fireball_Impact_01.wav", 0.6f)));
            yield return S("nova", 0.3f,
                V(L(M + "13_Ice_explosion_01.wav", length: 1.4f, fadeOut: 0.5f)));
            yield return S("flame", 0.24f,
                V(L(J + "Fireball_Launch_01.wav"), L(J + "Loop_Fireball_Hold.wav", 0.6f, start: 0.2f, length: 0.9f, fadeOut: 0.4f)));
            yield return S("fuse", 0.16f,
                V(L(J + "Loop_Fireball_Hold.wav", 0.8f, 1.5f, start: 0.5f, length: 0.7f, fadeOut: 0.3f)));
            yield return S("bolt", 0.16f,
                V(L(J + "Electric_Spell_Launch_01.wav", length: 0.45f, fadeOut: 0.15f)),
                V(L(J + "Electric_Spell_Launch_02.wav", length: 0.45f, fadeOut: 0.15f)),
                V(L(J + "Electric_Spell_Launch_03.wav", length: 0.45f, fadeOut: 0.15f)));
            yield return S("arrow", 0.2f,
                V(L(J + "Bow_Arrow_Shoot_01.wav", length: 0.5f)), V(L(J + "Bow_Arrow_Shoot_02.wav", length: 0.5f)));
            yield return S("orb", 0.16f,
                V(L(J + "Fireball_Launch_02.wav", 1f, 1.25f, length: 0.5f, fadeOut: 0.2f)), V(L(J + "Fireball_Launch_03.wav", 1f, 1.25f, length: 0.5f, fadeOut: 0.2f)));
            yield return S("blink", 0.22f, V(L(J + "Teleport_In.wav", length: 0.8f, fadeOut: 0.3f)), V(L(J + "Teleport_Out.wav", length: 0.8f, fadeOut: 0.3f)));
            yield return S("warcry", 0.34f,
                V(L(R + "giant3.wav", 1f, 1.15f, length: 1.2f, fadeOut: 0.4f), L(J + "Magic_Shield_Activation_01.wav", 0.5f, 0.8f)));
            yield return S("heal", 0.22f, V(L(J + "Healing_Chime_01.wav")), V(L(J + "Healing_Chime_02.wav")));
            yield return S("revive", 0.24f, V(L(J + "Healing_Chime_02.wav", pitch: 0.85f), L(J + "Magic_Shield_Activation_02.wav", 0.5f)));

            // Monsters and the room.
            yield return S("telegraph", 0.1f,
                V(L(K + "drawKnife1.ogg", length: 0.5f)), V(L(K + "drawKnife2.ogg", length: 0.5f)), V(L(K + "drawKnife3.ogg", length: 0.5f)));
            yield return S("screech", 0.2f,
                V(L(R + "mnstr1.wav", length: 0.7f, fadeOut: 0.25f)), V(L(R + "mnstr3.wav", length: 0.7f, fadeOut: 0.25f)), V(L(R + "mnstr5.wav", length: 0.7f, fadeOut: 0.25f)));
            yield return S("roar", 0.4f, V(L(R + "giant1.wav", pitch: 0.8f), L(R + "ogre2.wav", 0.6f, 0.7f)));
            yield return S("spikes", 0.26f,
                V(L(K + "impactMetal_medium_000.ogg"), L(K + "knifeSlice.ogg", 0.7f)), V(L(K + "impactMetal_medium_001.ogg"), L(K + "knifeSlice2.ogg", 0.7f)));

            // Rewards and the interface.
            yield return S("soul", 0.1f,
                V(L(K + "impactGlass_light_000.ogg", pitch: 1.6f)), V(L(K + "impactGlass_light_001.ogg", pitch: 1.7f)), V(L(K + "impactGlass_light_002.ogg", pitch: 1.5f)));
            yield return S("levelup", 0.22f, V(L(J + "Magic_Shield_Activation_01.wav"), L(J + "Healing_Chime_01.wav", 0.6f, 1.25f, delay: 0.1f)));
            yield return S("clear", 0.26f, V(L(K + "impactBell_heavy_001.ogg", length: 2.5f, fadeOut: 1f), L(J + "Healing_Chime_02.wav", 0.4f, 0.8f, delay: 0.15f)));
            yield return S("click", 0.14f, V(L(R + "interface1.wav", length: 0.25f)));
        }

        private static IEnumerable<Track> Tracks()
        {
            const string D = "music/";
            yield return new Track { Name = "music_camp", File = D + "ofdn_pavane.ogg", Loudness = 0.12f };
            yield return new Track { Name = "music_crypt", File = D + "ofdn_in_darkness.ogg" };
            yield return new Track { Name = "music_chapel", File = D + "ofdn_zwischenwelt.ogg" };
            yield return new Track { Name = "music_abyss", File = D + "ofdn_throne_room.ogg" };
            // The new lands play older pieces changed: slowed and drowned, quickened and cold, slowed and burnt.
            yield return new Track { Name = "music_sunken", File = D + "ofdn_throne_room.ogg", Pitch = 0.84f, LowPass = 1600f, Echo = 0.34f, EchoFeedback = 0.38f };
            yield return new Track { Name = "music_frost", File = D + "ofdn_zwischenwelt.ogg", Pitch = 1.12f, Echo = 0.52f, EchoFeedback = 0.42f };
            yield return new Track { Name = "music_forge", File = D + "ofdn_in_darkness.ogg", Pitch = 0.89f, LowPass = 3200f, Drive = 1.8f };
            // The heart: the long dark piece (only its opening plays on defeat), slowed and echoing.
            yield return new Track { Name = "music_heart", File = D + "ofdn_obscurium.ogg", Pitch = 0.9f, LowPass = 2600f, Echo = 0.45f, EchoFeedback = 0.36f };
            yield return new Track { Name = "music_boss", File = D + "helmet_boss_arcanos.mp3", Loudness = 0.18f };
            yield return new Track { Name = "music_victory", File = D + "ofdn_victory_stats.ogg", Loudness = 0.17f };
            yield return new Track { Name = "music_defeat", File = D + "ofdn_obscurium.ogg", Length = 9f, FadeOut = 4f, Loudness = 0.13f };
        }

        // ------------------------------------------------------------------ Build

        [MenuItem("Tools/Game/Build Sound Pack")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            string dir = AudioGenerator.Dir;
            Directory.CreateDirectory(dir);
            var written = new List<string>();

            foreach (var sfx in Sounds())
            {
                foreach (var stale in Directory.GetFiles(dir, $"sfx_{sfx.Name}_*.wav")) AssetDatabase.DeleteAsset(stale.Replace('\\', '/'));
                for (int v = 0; v < sfx.Variants.Length; v++)
                {
                    var mono = Mix(sfx.Variants[v]);
                    Level(mono, sfx.Loudness, 1, out float peak);
                    string path = $"{dir}/sfx_{sfx.Name}{(v == 0 ? "" : "_" + (v + 1))}.wav";
                    Dsp.WriteWav(path, mono, 1, Rate);
                    written.Add(path);
                    Debug.Log($"[SoundPack] {Path.GetFileName(path)} {mono.Length / (float)Rate:0.00}s peak {peak:0.00}");
                }
            }

            foreach (var t in Tracks())
            {
                var clip = Load(t.File);
                int ch = clip.channels;
                var data = Read(clip);
                if (t.Start > 0f || t.Length > 0f)
                {
                    int from = Mathf.Clamp(Mathf.RoundToInt(t.Start * clip.frequency), 0, clip.samples) * ch;
                    int count = t.Length > 0f ? Mathf.Min(Mathf.RoundToInt(t.Length * clip.frequency) * ch, data.Length - from) : data.Length - from;
                    var cut = new float[count];
                    System.Array.Copy(data, from, cut, 0, count);
                    data = cut;
                }
                data = Vary(data, ch, clip.frequency, t);
                if (t.FadeOut > 0f) FadeOut(data, Mathf.RoundToInt(t.FadeOut * clip.frequency), ch);
                float rms = Rms(data, 0, data.Length);
                float gain = t.Loudness / Mathf.Max(rms, 0.0001f);
                float max = 0f;
                foreach (var s in data) max = Mathf.Max(max, Mathf.Abs(s));
                gain = Mathf.Min(gain, 0.98f / Mathf.Max(max, 0.0001f));
                for (int i = 0; i < data.Length; i++) data[i] *= gain;
                string path = $"{dir}/{t.Name}.wav";
                Dsp.WriteWav(path, data, ch, clip.frequency);
                written.Add(path);
                Debug.Log($"[SoundPack] {t.Name}: {t.File} {data.Length / (float)ch / clip.frequency:0.0}s, {ch}ch {clip.frequency}Hz, rms {rms:0.000} gain {gain:0.00}");
            }

            foreach (var path in written) Import(path, music: Path.GetFileName(path).StartsWith("music_"));
            File.WriteAllText($"{dir}/{Marker}", "rendered by SoundPack.Build\n");
            AssetDatabase.SaveAssets();
            Debug.Log($"[SoundPack] Done: {written.Count} files");
        }

        private static void Import(string path, bool music)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.6f : 0.75f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ DSP

        /// <summary>
        /// A track changed for another land (interleaved samples). Everything wraps around the end, so a seamless loop
        /// stays seamless: the resampling reads past the end from the start, and the echo's tail falls on the beginning.
        /// </summary>
        private static float[] Vary(float[] data, int ch, int rate, Track t)
        {
            int frames = data.Length / ch;
            if (Mathf.Abs(t.Pitch - 1f) > 0.001f)
            {
                int outFrames = Mathf.RoundToInt(frames / t.Pitch);
                var resampled = new float[outFrames * ch];
                for (int i = 0; i < outFrames; i++)
                {
                    float src = i * t.Pitch;
                    int a = (int)src, b = (a + 1) % frames;
                    float k = src - a;
                    a %= frames;
                    for (int c = 0; c < ch; c++) resampled[i * ch + c] = Mathf.Lerp(data[a * ch + c], data[b * ch + c], k);
                }
                data = resampled;
                frames = outFrames;
            }
            if (t.LowPass > 0f)
            {
                float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * t.LowPass / rate);
                for (int c = 0; c < ch; c++)
                {
                    // Two passes round the loop so the filter has settled where the track starts.
                    float y = 0f;
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < frames; i++)
                        {
                            y += alpha * (data[i * ch + c] - y);
                            if (pass == 1) data[i * ch + c] = y;
                        }
                }
            }
            if (t.Echo > 0f && t.EchoFeedback > 0f)
            {
                int delay = Mathf.Max(1, Mathf.RoundToInt(t.Echo * rate));
                var wet = new float[data.Length];
                for (int pass = 0; pass < 3; pass++) // the tail wraps round several times
                    for (int i = 0; i < frames; i++)
                    {
                        int from = ((i - delay) % frames + frames) % frames;
                        for (int c = 0; c < ch; c++) wet[i * ch + c] = (data[from * ch + c] + wet[from * ch + c]) * t.EchoFeedback;
                    }
                for (int i = 0; i < data.Length; i++) data[i] += wet[i] * 0.8f;
            }
            if (t.Drive > 0f)
                for (int i = 0; i < data.Length; i++) data[i] = Dsp.SoftClip(data[i] * t.Drive) / Dsp.SoftClip(t.Drive);
            return data;
        }

        private static AudioClip Load(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SourceDir + file);
            if (clip == null) throw new FileNotFoundException("[SoundPack] missing source " + SourceDir + file);
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                AssetDatabase.ImportAsset(SourceDir + file, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SourceDir + file);
            }
            clip.LoadAudioData();
            return clip;
        }

        private static float[] Read(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) throw new System.Exception("[SoundPack] could not read " + clip.name);
            return data;
        }

        /// <summary>Mono at <see cref="Rate"/>: each layer trimmed of leading silence, cut, re-pitched, delayed, summed.</summary>
        private static float[] Mix(Layer[] layers)
        {
            var parts = new List<(float[] Samples, int Offset)>();
            int total = 0;
            foreach (var layer in layers)
            {
                var clip = Load(layer.File);
                var raw = Read(clip);
                int ch = clip.channels, frames = clip.samples;
                var mono = new float[frames];
                for (int i = 0; i < frames; i++)
                {
                    float sum = 0f;
                    for (int c = 0; c < ch; c++) sum += raw[i * ch + c];
                    mono[i] = sum / ch;
                }

                int start = layer.Start >= 0f ? Mathf.RoundToInt(layer.Start * clip.frequency) : FirstSound(mono);
                // Resample: source frames advance by step per output sample (pitch > 1 = higher and shorter).
                double step = clip.frequency / (double)Rate * layer.Pitch;
                int count = (int)((frames - start) / step);
                if (layer.Length > 0f) count = Mathf.Min(count, Mathf.RoundToInt(layer.Length * Rate));
                var outSamples = new float[Mathf.Max(0, count)];
                for (int i = 0; i < outSamples.Length; i++)
                {
                    double pos = start + i * step;
                    int a = (int)pos;
                    float f = (float)(pos - a);
                    float s0 = mono[Mathf.Min(a, frames - 1)], s1 = mono[Mathf.Min(a + 1, frames - 1)];
                    outSamples[i] = (s0 + (s1 - s0) * f) * layer.Gain;
                }
                int fadeIn = Mathf.Min(outSamples.Length, 88);
                for (int i = 0; i < fadeIn; i++) outSamples[i] *= i / (float)fadeIn;
                if (layer.FadeOut > 0f) FadeOut(outSamples, Mathf.RoundToInt(layer.FadeOut * Rate), 1);
                int offset = Mathf.RoundToInt(layer.Delay * Rate);
                parts.Add((outSamples, offset));
                total = Mathf.Max(total, offset + outSamples.Length);
            }

            var mix = new float[total];
            foreach (var (samples, offset) in parts)
                for (int i = 0; i < samples.Length; i++) mix[offset + i] += samples[i];
            return TrimTail(mix);
        }

        /// <summary>First sample above -46 dB, backed off 2 ms so the attack stays whole.</summary>
        private static int FirstSound(float[] mono)
        {
            for (int i = 0; i < mono.Length; i++)
                if (Mathf.Abs(mono[i]) > 0.005f) return Mathf.Max(0, i - 88);
            return 0;
        }

        private static float[] TrimTail(float[] mix)
        {
            float max = 0f;
            foreach (var s in mix) max = Mathf.Max(max, Mathf.Abs(s));
            int end = mix.Length;
            while (end > 1 && Mathf.Abs(mix[end - 1]) < max * 0.004f) end--;
            end = Mathf.Min(mix.Length, end + 220);
            if (end == mix.Length) return mix;
            var cut = new float[end];
            System.Array.Copy(mix, cut, end);
            FadeOut(cut, Mathf.Min(220, end), 1);
            return cut;
        }

        private static void FadeOut(float[] data, int frames, int channels)
        {
            int total = data.Length / channels;
            frames = Mathf.Min(frames, total);
            for (int i = 0; i < frames; i++)
            {
                float k = i / (float)frames; // 0 at the very end
                int frame = total - 1 - i;
                for (int c = 0; c < channels; c++) data[frame * channels + c] *= k * k;
            }
        }

        private static float Rms(float[] data, int from, int count)
        {
            double sum = 0;
            for (int i = from; i < from + count; i++) sum += data[i] * data[i];
            return count > 0 ? Mathf.Sqrt((float)(sum / count)) : 0f;
        }

        /// <summary>Scales so the loudest 50 ms window has the target RMS, never letting the peak pass 0.98.</summary>
        private static void Level(float[] data, float loudness, int channels, out float peak)
        {
            int window = Mathf.Max(1, Rate / 20) * channels;
            float loudest = 0f;
            for (int i = 0; i < data.Length; i += window / 2)
                loudest = Mathf.Max(loudest, Rms(data, i, Mathf.Min(window, data.Length - i)));
            float max = 0f;
            foreach (var s in data) max = Mathf.Max(max, Mathf.Abs(s));
            float gain = Mathf.Min(loudness / Mathf.Max(loudest, 0.0001f), 0.98f / Mathf.Max(max, 0.0001f));
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            peak = max * gain;
        }
    }
}
