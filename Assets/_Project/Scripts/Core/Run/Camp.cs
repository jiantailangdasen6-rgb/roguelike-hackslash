using UnityEngine;

namespace Game.Core
{
    /// <summary>Places in the camp that open a preparation panel (or, for the gate, start the run).</summary>
    public enum CampStation : byte
    {
        Gate,    // 出発の門: everyone stands in it to set out
        Tablet,  // 迷宮の碑: the host picks the difficulty
        Mirror,  // 鏡: class and look
        Master,  // 師範: class mastery
        Charms,  // お守り職人: trinkets
        Vault,   // 倉庫: gear kept between runs (VaultView)
        Board,   // 依頼の掲示板: requests from the camp's people (QuestCatalog)
        Hearth,  // 焚き火: grow the camp's facilities with soul shards (CampUpgrades)
    }

    public struct CampStationDef
    {
        public CampStation Id;
        public string Name;
        public string Action;      // what the prompt offers
        public Vector2 Position;   // where the thing stands (sprite anchor)
        public Vector2 Front;      // where a player stands to use it
        public float Radius;
    }

    /// <summary>
    /// The camp (拠点) between runs: a walkable courtyard in front of a ruined chapel, shared by the whole party.
    /// Pure data, used by the camp scene, owner-side movement and the builder that draws it.
    /// </summary>
    public static class Camp
    {
        /// <summary>Walkable floor (same tile grid as the arena: whole units).</summary>
        public static readonly Rect Bounds = new(-13f, -8f, 26f, 15f);

        public static readonly Vector2 FirePosition = new(0f, -0.6f);
        public const float GateRadius = 1.7f;
        public const float DepartSeconds = 3f;

        public static readonly CampStationDef[] Stations =
        {
            new() { Id = CampStation.Gate, Name = "出発の門", Action = "全員で門に立つと出発", Position = new Vector2(0f, 6.9f), Front = new Vector2(0f, 4.9f), Radius = GateRadius },
            new() { Id = CampStation.Tablet, Name = "迷宮の碑", Action = "難易度を選ぶ", Position = new Vector2(3.6f, 5.2f), Front = new Vector2(3.6f, 4.2f), Radius = 1.3f },
            new() { Id = CampStation.Mirror, Name = "古い鏡", Action = "クラスと見た目を変える", Position = new Vector2(-9.5f, 3.4f), Front = new Vector2(-9.5f, 2.2f), Radius = 1.6f },
            new() { Id = CampStation.Master, Name = "師範", Action = "熟練を習う", Position = new Vector2(-9f, -3.6f), Front = new Vector2(-7.8f, -4.4f), Radius = 1.7f },
            new() { Id = CampStation.Charms, Name = "お守り職人", Action = "お守りを選ぶ・作る", Position = new Vector2(9f, -3.6f), Front = new Vector2(7.8f, -4.4f), Radius = 1.7f },
            new() { Id = CampStation.Vault, Name = "倉庫", Action = "装備を整える", Position = new Vector2(9.5f, 3.2f), Front = new Vector2(9.5f, 2.1f), Radius = 1.6f },
            new() { Id = CampStation.Board, Name = "依頼の掲示板", Action = "依頼を見る・報酬を受け取る", Position = new Vector2(5.2f, -5.3f), Front = new Vector2(5.2f, -4.2f), Radius = 1.6f },
            new() { Id = CampStation.Hearth, Name = "焚き火", Action = "拠点を育てる", Position = FirePosition, Front = FirePosition + new Vector2(0f, -1.4f), Radius = 1.05f },
        };

        public static CampStationDef Get(CampStation id)
        {
            foreach (var s in Stations) if (s.Id == id) return s;
            return Stations[0];
        }

        /// <summary>Solid things in the camp (players walk around them).</summary>
        public static readonly RoomLayout Layout = new()
        {
            Id = -1,
            Name = "camp",
            Obstacles = new[]
            {
                Box(FirePosition.x, FirePosition.y, 1.5f, 0.9f),       // campfire
                Box(-9.5f, 3.4f, 1.4f, 0.6f),                          // mirror
                Box(9.5f, 3.4f, 1.8f, 0.7f),                           // vault chest
                Box(5.2f, -5.3f, 1.8f, 0.5f), Box(7.0f, -5.1f, 0.6f, 0.4f), // notice board, gravekeeper
                Box(-9.6f, -3.5f, 1.2f, 0.6f),                         // master + weapon rack
                Box(-11.4f, -3.4f, 1.4f, 0.6f),
                Box(-6.6f, -1.9f, 0.6f, 0.4f),                         // training dummy
                Box(9.6f, -3.5f, 2.4f, 0.8f),                          // charm maker's table
                Box(3.6f, 5.2f, 1.2f, 0.6f),                           // tablet
                Box(-2.3f, 6.3f, 1.0f, 0.7f), Box(2.3f, 6.3f, 1.0f, 0.7f), // gate posts
                Box(-11.2f, 5.6f, 2.6f, 1.2f),                         // tent (back left)
                Box(11.2f, 5.6f, 2.6f, 1.2f),                          // tent (back right)
                Box(-3.2f, -2.4f, 1.8f, 0.4f), Box(3.2f, -2.4f, 1.8f, 0.4f), // log benches
                Box(-11.6f, -6.6f, 1.4f, 0.8f), Box(11.6f, -6.6f, 1.4f, 0.8f), // crates
            },
        };

        private static Obstacle Box(float x, float y, float w, float h) =>
            new() { Kind = ObstacleKind.Crates, Position = new Vector2(x, y), Size = new Vector2(w, h) };

        /// <summary>Where each party slot appears around the fire.</summary>
        public static Vector2 SpawnPoint(int slot) => FirePosition + new Vector2((slot - 1) * 1.8f, -2.6f - (slot == 1 ? 0.4f : 0f));

        public static bool InGate(Vector2 p) => (p - Get(CampStation.Gate).Front).sqrMagnitude <= GateRadius * GateRadius;

        /// <summary>The station a player at <paramref name="p"/> can use (gate excluded), or null.</summary>
        public static CampStationDef? StationAt(Vector2 p)
        {
            CampStationDef? best = null;
            float bestD = float.MaxValue;
            foreach (var s in Stations)
            {
                if (s.Id == CampStation.Gate) continue;
                float d = (p - s.Front).sqrMagnitude;
                if (d <= s.Radius * s.Radius && d < bestD)
                {
                    best = s;
                    bestD = d;
                }
            }
            return best;
        }
    }
}
