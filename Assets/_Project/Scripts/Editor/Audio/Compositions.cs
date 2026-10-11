using System.Collections.Generic;
using System.Linq;

namespace Game.EditorTools.Audio
{
    /// <summary>
    /// The soundtrack: camp (calm harp), three area themes (crypt, chapel, abyss), boss battle, victory and defeat.
    /// Written in code as chord progressions, ostinatos and short melodies.
    /// </summary>
    public static class Compositions
    {
        // A chord as semitone offsets from its root (root given as MIDI note).
        private static readonly int[] Minor = { 0, 3, 7 };
        private static readonly int[] Major = { 0, 4, 7 };

        private readonly struct Ch
        {
            public readonly int Root;
            public readonly int[] Shape;
            public Ch(int root, int[] shape) { Root = root; Shape = shape; }
            public IEnumerable<float> Voicing(int octaveShift = 0)
            {
                int root = Root + octaveShift * 12;
                return Shape.Select(s => (float)(root + s));
            }
        }

        private static Ch m(int root) => new(root, Minor);
        private static Ch M(int root) => new(root, Major);

        // MIDI roots around octave 3 (D3 = 50).
        private const int C3 = 48, Db3 = 49, D3 = 50, Eb3 = 51, E3 = 52, F3 = 53, G3 = 55, Ab3 = 56, A3 = 57, Bb3 = 58, B3 = 59;

        public static Dictionary<string, Song> All() => new()
        {
            ["music_camp"] = Camp(),
            ["music_crypt"] = Battle("crypt", 108f, new[] { m(D3), m(D3), M(Bb3 - 12), M(C3), m(D3), m(D3), m(G3 - 12), M(A3 - 12) },
                Inst.Brass, Inst.Strings, CryptMelody(), drums: 1, bells: false, organ: false),
            ["music_chapel"] = Battle("chapel", 100f, new[] { m(C3), M(Ab3 - 12), m(F3 - 12), M(G3 - 12), m(C3), M(Eb3), M(Bb3 - 12), M(G3 - 12) },
                Inst.Choir, Inst.Strings, ChapelMelody(), drums: 1, bells: true, organ: true),
            ["music_abyss"] = Battle("abyss", 118f, new[] { m(E3), M(F3), m(E3), M(F3), M(C3), m(D3), m(E3), M(F3) },
                Inst.Brass, Inst.Strings, AbyssMelody(), drums: 2, bells: false, organ: false),
            ["music_boss"] = Boss(),
            ["music_victory"] = Victory(),
            ["music_defeat"] = Defeat(),
        };

        // ------------------------------------------------------------------ Camp

        private static Song Camp()
        {
            var s = new Song { Name = "camp", Bpm = 70f, Bars = 16 };
            var prog = new[] { m(D3), M(Bb3 - 12), m(G3 - 12), M(A3 - 12) };
            for (int bar = 0; bar < 16; bar++)
            {
                var ch = prog[(bar / 2) % prog.Length];
                if (bar % 2 == 0)
                {
                    s.Chord(Inst.Pad, bar * 4, 8f, ch.Voicing(1), 0.8f);
                    s.Add(Inst.Drone, bar * 4, 8f, ch.Root - 12, 0.7f);
                }
                // Harp arpeggio: up two octaves and back.
                var tones = ch.Voicing(1).Concat(ch.Voicing(2)).ToArray();
                int[] order = { 0, 1, 2, 3, 4, 5, 4, 2 };
                for (int i = 0; i < 8; i++) s.Add(Inst.Harp, bar * 4 + i * 0.5f, 0.5f, tones[order[i]], i == 0 ? 1f : 0.7f);
                if (bar >= 8) s.Chord(Inst.Choir, bar * 4, 4f, ch.Voicing(1).Take(2), 0.45f);
                if (bar >= 8 && bar % 2 == 0) s.Add(Inst.Taiko, bar * 4, 1f, 43f, 0.35f);
            }
            float[] bells = { 81, 77, 79, 76, 81, 84, 82, 81 };
            for (int i = 0; i < bells.Length; i++) s.Add(Inst.Bell, i * 8 + 2f, 2f, bells[i], 0.55f);
            return s;
        }

        // ------------------------------------------------------------------ Area battles

        private static (float Beat, float Len, float Pitch)[] CryptMelody() => new[]
        {
            (0f, 2f, 62f), (2f, 1f, 65f), (3f, 1f, 64f), (4f, 3f, 62f), (7f, 1f, 57f),
            (8f, 2f, 58f), (10f, 1f, 60f), (11f, 1f, 62f), (12f, 4f, 60f),
            (16f, 2f, 62f), (18f, 1f, 65f), (19f, 1f, 67f), (20f, 3f, 69f), (23f, 0.5f, 67f), (23.5f, 0.5f, 65f),
            (24f, 2f, 64f), (26f, 1f, 62f), (27f, 1f, 64f), (28f, 4f, 61f),
        };

        private static (float, float, float)[] ChapelMelody() => new[]
        {
            (0f, 3f, 67f), (3f, 1f, 68f), (4f, 4f, 67f),
            (8f, 2f, 65f), (10f, 2f, 63f), (12f, 4f, 62f),
            (16f, 3f, 67f), (19f, 1f, 70f), (20f, 4f, 72f),
            (24f, 2f, 70f), (26f, 2f, 68f), (28f, 4f, 67f),
        };

        private static (float, float, float)[] AbyssMelody() => new[]
        {
            (0f, 1.5f, 64f), (1.5f, 0.5f, 65f), (2f, 2f, 64f), (4f, 1.5f, 62f), (5.5f, 0.5f, 64f), (6f, 2f, 65f),
            (8f, 2f, 64f), (10f, 1f, 60f), (11f, 1f, 62f), (12f, 4f, 64f),
            (16f, 1.5f, 71f), (17.5f, 0.5f, 72f), (18f, 2f, 71f), (20f, 2f, 69f), (22f, 2f, 67f),
            (24f, 2f, 65f), (26f, 1f, 64f), (27f, 1f, 65f), (28f, 4f, 64f),
        };

        /// <summary>
        /// 32 bars in four 8-bar sections: A (pad, bass, light drums) → B (+ostinato, choir) →
        /// C (full drums, lead melody) → D (breakdown with the melody an octave up, building back to A).
        /// </summary>
        private static Song Battle(string name, float bpm, Ch[] prog, Inst lead, Inst ostinato,
            (float Beat, float Len, float Pitch)[] melody, int drums, bool bells, bool organ)
        {
            var s = new Song { Name = name, Bpm = bpm, Bars = 32 };
            for (int bar = 0; bar < 32; bar++)
            {
                int section = bar / 8;
                var ch = prog[bar % prog.Length];
                float b0 = bar * 4;

                // Harmony
                s.Chord(organ ? Inst.Organ : Inst.Pad, b0, 4f, ch.Voicing(1), section == 3 ? 0.7f : 1f);
                if (section >= 1) s.Chord(Inst.Choir, b0, 4f, ch.Voicing(1), section == 2 ? 0.6f : 0.8f);
                if (bar % 2 == 0 && drums >= 2) s.Add(Inst.Drone, b0, 8f, ch.Root - 12, 0.6f);

                // Bass ostinato in eighths (root, octave, fifth)
                if (section != 3 || bar % 2 == 1)
                {
                    int[] bassSteps = { 0, 0, 12, 0, 0, 7, 0, 12 };
                    for (int i = 0; i < 8; i++) s.Add(Inst.Bass, b0 + i * 0.5f, 0.45f, ch.Root - 12 + bassSteps[i], i % 4 == 0 ? 1f : 0.75f);
                }

                // String ostinato in sixteenths from section B
                if (section is 1 or 2)
                {
                    int[] steps = { 0, 7, 12, 7, 0, 7, 12, 15, 0, 7, 12, 7, 0, 7, 12, 19 };
                    for (int i = 0; i < 16; i++) s.Add(ostinato, b0 + i * 0.25f, 0.22f, ch.Root + 12 + steps[i] - (steps[i] == 15 && ch.Shape == Major ? -1 : 0), i % 4 == 0 ? 0.9f : 0.6f);
                }
                if (section == 3) s.Chord(Inst.Strings, b0, 4f, ch.Voicing(2), 0.35f);

                // Drums
                switch (section)
                {
                    case 0:
                        s.Pattern(Inst.Taiko, bar, "X.......x.......", 41f);
                        s.Pattern(Inst.Hat, bar, "..o...o...o...o.", 0f);
                        break;
                    case 1:
                        s.Pattern(Inst.Taiko, bar, "X.....x.X.......", 41f);
                        s.Pattern(Inst.Snare, bar, "....x.......x...", 0f, 0.8f);
                        s.Pattern(Inst.Hat, bar, "o.o.o.o.o.o.o.o.", 0f);
                        break;
                    case 2:
                        s.Pattern(Inst.Taiko, bar, drums >= 2 ? "X..x..x.X.x.x..x" : "X..x..x.X...x.x.", 41f);
                        s.Pattern(Inst.Taiko, bar, "........x.....x.", 48f, 0.7f);
                        s.Pattern(Inst.Snare, bar, "....X.......X..o", 0f);
                        s.Pattern(Inst.Hat, bar, "xoxoxoxoxoxoxoxo", 0f);
                        break;
                    default:
                        s.Pattern(Inst.Taiko, bar, bar % 2 == 0 ? "X..............." : "X.......x.......", 41f, 0.8f);
                        if (bar == 31) s.Pattern(Inst.Taiko, bar, "........x.x.xxxx", 45f, 0.8f);
                        break;
                }
                if (bar % 8 == 0) s.Add(Inst.Cymbal, (bar == 0 ? s.Bars * 4 : b0) - 2f, 2f, 60f); // swell into each section (bar 0: wraps from the end)
                if (bells && bar % 4 == 0) s.Add(Inst.Bell, b0, 2f, ch.Root + 36, 0.5f);
            }

            // Lead melody in section C, again an octave up (lighter) in D.
            foreach (var (beat, len, pitch) in melody)
            {
                s.Add(lead, 64f + beat, len, pitch, 0.9f);
                s.Add(lead == Inst.Brass ? Inst.Choir : Inst.Bell, 96f + beat, len, pitch + 12, 0.6f);
            }
            return s;
        }

        // ------------------------------------------------------------------ Boss

        private static Song Boss()
        {
            var s = new Song { Name = "boss", Bpm = 140f, Bars = 24 };
            var prog = new[] { m(D3), M(Eb3), m(D3), M(C3), M(Bb3 - 12), M(C3), m(D3), M(A3 - 12) };
            for (int bar = 0; bar < 24; bar++)
            {
                var ch = prog[bar % prog.Length];
                float b0 = bar * 4;
                bool full = bar >= 8;
                s.Chord(Inst.Choir, b0, 4f, ch.Voicing(1), 0.9f);
                s.Chord(Inst.Pad, b0, 4f, ch.Voicing(0), 0.8f);
                int[] steps = { 0, 12, 7, 12, 0, 12, 8, 12, 0, 12, 7, 12, 3, 12, 7, 15 };
                for (int i = 0; i < 16; i++) s.Add(Inst.Strings, b0 + i * 0.25f, 0.22f, ch.Root + 12 + steps[i], i % 4 == 0 ? 1f : 0.65f);
                for (int i = 0; i < 8; i++) s.Add(Inst.Bass, b0 + i * 0.5f, 0.4f, ch.Root - 12 + (i % 4 == 3 ? 12 : 0), 1f);

                s.Pattern(Inst.Taiko, bar, full ? "X.xX..x.X.xX..xx" : "X..x..x.X..x..x.", 40f);
                s.Pattern(Inst.Taiko, bar, full ? "....X.......X.x." : "....x.......x...", 47f, 0.8f);
                if (full) s.Pattern(Inst.Snare, bar, "....X..o....X.oo", 0f);
                s.Pattern(Inst.Hat, bar, "xoxoxoxoxoxoxoxo", 0f, full ? 1f : 0.6f);
                // Brass stabs on the off-beats of each phrase
                if (full && bar % 2 == 1) s.Chord(Inst.Brass, b0 + 2.5f, 1f, ch.Voicing(1), 0.9f);
                if (bar % 4 == 0) s.Add(Inst.Cymbal, (bar == 0 ? s.Bars * 4 : b0) - 1f, 1f, 60f);
            }
            // Heroic-dark brass theme over bars 16-23
            float[] theme = { 62, 65, 69, 70, 69, 65, 67, 64, 62, 65, 69, 74, 72, 70, 69, 61 };
            for (int i = 0; i < theme.Length; i++) s.Add(Inst.Brass, 64f + i * 2f, 1.8f, theme[i], 1f);
            return s;
        }

        // ------------------------------------------------------------------ Stingers

        private static Song Victory()
        {
            var s = new Song { Name = "victory", Bpm = 80f, Bars = 3, Loop = false };
            s.Add(Inst.Cymbal, 0f, 0.5f, 60f);
            s.Add(Inst.Taiko, 0f, 1f, 41f);
            s.Chord(Inst.Brass, 0f, 1.5f, new float[] { 62, 66, 69 });
            s.Chord(Inst.Brass, 1.5f, 0.5f, new float[] { 64, 67, 71 });
            s.Chord(Inst.Brass, 2f, 6f, new float[] { 62, 66, 69, 74 });
            s.Chord(Inst.Choir, 2f, 7f, new float[] { 62, 66, 69, 74 });
            s.Chord(Inst.Pad, 2f, 7f, new float[] { 50, 57, 62 });
            s.Add(Inst.Taiko, 2f, 1f, 38f);
            foreach (var (b, p) in new[] { (2f, 86f), (2.5f, 90f), (3f, 93f), (3.5f, 98f) }) s.Add(Inst.Bell, b, 2f, p, 0.7f);
            return s;
        }

        private static Song Defeat()
        {
            var s = new Song { Name = "defeat", Bpm = 60f, Bars = 3, Loop = false };
            s.Add(Inst.Bell, 0f, 4f, 50f, 1f);
            s.Add(Inst.Bell, 2f, 4f, 50f, 0.8f);
            s.Chord(Inst.Choir, 0f, 4f, new float[] { 62, 65, 69 }, 0.8f);
            s.Chord(Inst.Choir, 4f, 6f, new float[] { 61, 64, 67 }, 0.8f);
            s.Add(Inst.Drone, 0f, 8f, 38f, 0.9f);
            s.Add(Inst.Taiko, 0f, 1f, 36f, 0.8f);
            return s;
        }
    }
}
