using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The heart of the labyrinth (the story's end). Once someone in the team carries the memories of all six lords
    /// (Loadout.memories), the third lord's hall opens a third way in the middle: down into the heart, a short land
    /// where the Ashen King waits. He fights with every lord's arts and goes through their trials in turn (the Bone
    /// King's ward, the Blood Bishop's ritual, the Lich's falling floor and phylactery); when he falls the heart is
    /// lit again and the run ends in the true ending (RunEnded Int = 3).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public static readonly Vector2 HeartOffset = new(0f, 1.5f);

        /// <summary>The team carries all six memories: the way to the heart opens after the third lord (set by the host).</summary>
        public bool HeartOpen { get; set; }

        /// <summary>The third lord's hall offers the heart too.</summary>
        public bool OffersHeart(int area) => HeartOpen && area == AreaCount - 1 && Biome != Biome.Heart;

        public Vector2 HeartPosition => Map.Boss.Origin + HeartOffset;

        /// <summary>The run reached the heart and lit it again.</summary>
        public bool TrueEnding { get; private set; }

        /// <summary>The Ashen King fell: the heart is lit and the run ends (all of it, no way deeper).</summary>
        private void HeartLit()
        {
            TrueEnding = true;
            Conquered = true;
            SplitRunEnded();
            Phase = RunPhase.Victory;
            Events.Add(new SimEvent(SimEventType.RunEnded) { Int = 3, Id = ContractRank });
        }

        /// <summary>The Ashen King's trials: the first three lords' in turn, and a heart to break at the end.</summary>
        private void AshenTrials(int bossIndex, float hp)
        {
            if (Threshold(0, hp, 0.75f)) BeginBoneWard(bossIndex);
            else if (Threshold(1, hp, 0.5f)) BeginRitual(bossIndex);
            else if (Threshold(2, hp, 0.25f)) BeginCollapse();
        }

        /// <summary>His moves go round the lords' arts.</summary>
        private static int AshenAttack(float distance, int n, bool roomForMinions)
        {
            // 0 cleave, 1 blood fan, 2 soul ring, 3 rain, 4 summon, 5 charge, 6 bone cage
            switch (n % 7)
            {
                case 0: return distance < 4f ? 0 : 1;
                case 1: case 6: return 3;
                case 2: return 2;
                case 3: return distance > 5f ? 5 : 0;
                case 4: return 6;
                default: return roomForMinions ? 4 : 1;
            }
        }
    }
}
