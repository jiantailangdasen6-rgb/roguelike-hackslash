using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Hand-built sprites that are not part of a generated sheet. Lives in Resources so runtime code can find it.</summary>
    [CreateAssetMenu(menuName = "Game/Game Art")]
    public sealed class GameArt : ScriptableObject
    {
        public Sprite Circle;
        public Sprite Triangle;
        public Sprite Diamond;
        public Sprite Arc;
        public Sprite Ring;

        private static GameArt _instance;

        public static GameArt Instance => _instance != null ? _instance : _instance = Resources.Load<GameArt>("GameArt");

        /// <summary>Per-player marker colours (chosen to stay distinct from enemies).</summary>
        public static readonly Color[] PlayerColors =
        {
            new(0.35f, 0.7f, 1f),
            new(0.85f, 0.5f, 1f),
            new(1f, 0.5f, 0.7f),
        };
    }
}
