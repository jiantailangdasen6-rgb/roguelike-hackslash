using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>The walkable area of the current scene: the camp, or the run's arena with its room layout.</summary>
    public static class PlayArea
    {
        public static bool InCamp => CampDirector.Instance != null;

        /// <summary>In a run: the floor left standing (the Lich's collapse shrinks it).</summary>
        public static Rect Bounds => InCamp ? Camp.Bounds
            : RunSimulation.SafeArea(RunSimulation.DefaultBounds, RunDirector.Instance != null ? RunDirector.Instance.CollapseStage.Value : 0);

        public static RoomLayout Layout =>
            InCamp ? Camp.Layout : RunDirector.Instance != null ? Arena.Get(RunDirector.Instance.LayoutId.Value) : null;
    }
}
