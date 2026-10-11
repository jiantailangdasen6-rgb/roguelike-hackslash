using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>The walkable area of the current scene: the camp, or the run's arena with its room layout.</summary>
    public static class PlayArea
    {
        public static bool InCamp => CampDirector.Instance != null;

        public static Rect Bounds => InCamp ? Camp.Bounds : RunSimulation.DefaultBounds;

        public static RoomLayout Layout =>
            InCamp ? Camp.Layout : RunDirector.Instance != null ? Arena.Get(RunDirector.Instance.LayoutId.Value) : null;
    }
}
