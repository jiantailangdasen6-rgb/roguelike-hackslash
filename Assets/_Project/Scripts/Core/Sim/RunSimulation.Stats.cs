using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// How long a run took, area by area (the aim is about 15 minutes for the three): when each area was entered,
    /// when its lord woke and fell, when it was left, and how often someone went down. Shown on the result screen
    /// and logged, so pace can be tuned from real runs.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public sealed class AreaSplit
        {
            public float Start;
            public float BossStart = -1f;
            public float BossEnd = -1f;
            public float End = -1f;
        }

        public readonly List<AreaSplit> Splits = new();
        /// <summary>Times anyone went down this run.</summary>
        public int Downs { get; private set; }
        /// <summary>Bumped whenever the splits change (the host mirrors them only then).</summary>
        public int SplitsVersion { get; private set; }

        private AreaSplit CurrentSplit => Splits.Count > 0 ? Splits[Splits.Count - 1] : null;

        private void SplitAreaEntered()
        {
            if (CurrentSplit is { End: < 0f } last) last.End = Elapsed;
            Splits.Add(new AreaSplit { Start = Elapsed });
            SplitsVersion++;
        }

        private void SplitBossWoke()
        {
            if (CurrentSplit is { BossStart: < 0f } s) s.BossStart = Elapsed;
            SplitsVersion++;
        }

        private void SplitBossFell()
        {
            if (CurrentSplit is { } s) s.BossEnd = Elapsed;
            SplitsVersion++;
        }

        private void SplitRunEnded()
        {
            if (CurrentSplit is { End: < 0f } s) s.End = Elapsed;
            SplitsVersion++;
        }

        private void CountDown()
        {
            Downs++;
            SplitsVersion++;
        }

        /// <summary>Compact form for the network: "start,bossStart,bossEnd,end;...|downs" in whole seconds (-1 = not yet).</summary>
        public string SplitsText()
        {
            var sb = new StringBuilder();
            foreach (var s in Splits)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(Sec(s.Start)).Append(',').Append(Sec(s.BossStart)).Append(',').Append(Sec(s.BossEnd)).Append(',').Append(Sec(s.End));
            }
            return sb.Append('|').Append(Downs).ToString();
        }

        private static int Sec(float t) => t < 0f ? -1 : Mathf.FloorToInt(t);

        /// <summary>Reads <see cref="SplitsText"/> back (any peer).</summary>
        public static (List<int[]> Areas, int Downs) ParseSplits(string text)
        {
            var areas = new List<int[]>();
            int downs = 0;
            if (string.IsNullOrEmpty(text)) return (areas, downs);
            var halves = text.Split('|');
            if (halves.Length > 1) int.TryParse(halves[1], out downs);
            foreach (var part in halves[0].Split(';'))
            {
                var n = part.Split(',');
                if (n.Length != 4) continue;
                var row = new int[4];
                for (int i = 0; i < 4; i++) int.TryParse(n[i], out row[i]);
                areas.Add(row);
            }
            return (areas, downs);
        }
    }
}
