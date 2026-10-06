using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Placeholder sprites and colors. Lives in Resources so runtime-created objects can find it.</summary>
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

        public static readonly Color[] PlayerColors =
        {
            new(0.35f, 0.7f, 1f),
            new(0.85f, 0.5f, 1f),
            new(1f, 0.5f, 0.7f),
        };

        public static Color EnemyColor(EnemyType type, bool elite)
        {
            if (elite) return new Color(1f, 0.82f, 0.25f);
            return type switch
            {
                EnemyType.Slime => new Color(0.45f, 0.85f, 0.4f),
                EnemyType.GoblinArcher => new Color(0.85f, 0.45f, 0.3f),
                _ => new Color(0.2f, 0.6f, 0.25f),
            };
        }

        public static Color RarityColor(Rarity rarity)
        {
            ColorUtility.TryParseHtmlString(ItemText.RarityColor(rarity), out var color);
            return color;
        }
    }
}
