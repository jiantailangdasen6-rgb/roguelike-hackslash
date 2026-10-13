using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Past the Lich: the throne room opens two ways. Home (the run ends in victory) or deeper: the deep layers
    /// ("深層"), areas past the third in the abyss's look, every one stronger, each ending with a lord of the
    /// labyrinth again. Once the Lich is down the run counts as conquered however it ends.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public static readonly Vector2 HomeOffset = new(-5.5f, 1.5f);
        public static readonly Vector2 DeepOffset = new(5.5f, 1.5f);

        /// <summary>The Lich has fallen this run.</summary>
        public bool Conquered { get; private set; }

        /// <summary>The right-hand way on from a beaten lord's hall (the left one is <see cref="HomeOffset"/>: home, or another land).</summary>
        public static Vector2 ExitSpot(int area) => DeepOffset;

        /// <summary>A beaten final (or deep) lord: home or deeper.</summary>
        public static bool OffersHome(int area) => area >= AreaCount - 1;

        public Vector2 HomePosition => Map.Boss.Origin + HomeOffset;

        /// <summary>Deep layer of the current area (1 = the first past the Lich, 0 before).</summary>
        public int DeepLayer => Mathf.Max(0, Area - (AreaCount - 1));

        /// <summary>Every land has its lord (the deep layers go through the lands again).</summary>
        private EnemyType LordOf(int area) => BiomeCatalog.Get(BiomeOf(area)).Lord;

        /// <summary>Everyone alive stands within <paramref name="radius"/> of a spot.</summary>
        private bool AllAt(Vector2 spot, float radius)
        {
            bool any = false;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                if ((p.Position - spot).sqrMagnitude > radius * radius) return false;
                any = true;
            }
            return any;
        }

        private void EndInVictory()
        {
            SplitRunEnded();
            Phase = RunPhase.Victory;
            Events.Add(new SimEvent(SimEventType.RunEnded) { Int = 1 });
        }
    }
}
