using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>The walkable area of the current scene: the camp, or the room of the area map the local player is in.</summary>
    public static class PlayArea
    {
        public static bool InCamp => CampDirector.Instance != null;

        /// <summary>In a run: the floor of the local player's room (the Lich's collapse shrinks the boss room's).</summary>
        public static Rect Bounds
        {
            get
            {
                if (InCamp) return Camp.Bounds;
                var director = RunDirector.Instance;
                if (director == null) return RunSimulation.DefaultBounds;
                var room = director.LocalRoom;
                return room.Kind == RoomKind.Boss ? RunSimulation.SafeArea(room.Bounds, director.CollapseStage.Value) : room.Bounds;
            }
        }

        public static RoomLayout Layout =>
            InCamp ? Camp.Layout : RunDirector.Instance != null ? RunDirector.Instance.LocalRoom.Layout : null;
    }
}
