using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Room choice: a cleared room opens two or three doors, each promising a reward that the team receives on
    /// walking through (everyone has to stand at the same door). Trial doors double the reward but make the room
    /// behind them tougher. Before a boss one door always offers healing.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float TrialHealth = 1.35f;
        public const float TrialDamage = 1.15f;
        public const float TrialEliteChance = 0.15f;
        public const float VigorPerStack = 0.1f;
        public const float BlessingShield = 20f;

        private readonly GameRandom _doorRng;
        private bool _enterTrial;

        /// <summary>Soul shards collected from reward doors, added to the run's payout.</summary>
        public int BonusShards { get; private set; }

        public static int ShardReward(int area) => 8 + 4 * area;

        /// <summary>Doors stand along the top wall; the layouts keep these spots clear.</summary>
        public static Vector2 DoorPosition(int index, int count, Rect bounds)
        {
            float spacing = count >= 3 ? 6f : 9f;
            return new Vector2((index - (count - 1) * 0.5f) * spacing, bounds.yMax - 1.2f);
        }

        /// <summary>The door this point stands at, or -1.</summary>
        public int DoorAt(Vector2 position)
        {
            for (int i = 0; i < Room.Doors.Count; i++)
                if ((DoorPosition(i, Room.Doors.Count, Bounds) - position).sqrMagnitude <= ExitRadius * ExitRadius)
                    return i;
            return -1;
        }

        private void RollDoors()
        {
            Room.Doors.Clear();
            int next = Room.Index + 1;
            bool nextIsBoss = next % RoomsPerArea == RoomsPerArea - 1;
            int count = Room.Area >= 1 && !nextIsBoss && _doorRng.Chance(0.35f) ? 3 : 2;

            var pool = new List<(RoomReward Reward, int Weight)> { (RoomReward.Boon, 40), (RoomReward.Vigor, 20), (RoomReward.Shards, 14), (RoomReward.Treasure, 22) };
            if (AnyUpgradeable()) pool.Add((RoomReward.Upgrade, 28));

            // Before a boss the team can always choose to heal up instead of growing stronger.
            if (nextIsBoss)
            {
                Room.Doors.Add(new Door(RoomReward.Vigor, false));
                pool.RemoveAll(e => e.Reward == RoomReward.Vigor);
            }

            while (Room.Doors.Count < count && pool.Count > 0)
            {
                int total = 0;
                foreach (var e in pool) total += e.Weight;
                int pick = _doorRng.Range(0, total);
                int index = 0;
                while (pick >= pool[index].Weight)
                {
                    pick -= pool[index].Weight;
                    index++;
                }
                Room.Doors.Add(new Door(pool[index].Reward, false));
                pool.RemoveAt(index);
            }

            // Shuffle so the healing door before a boss isn't always on the left.
            for (int i = Room.Doors.Count - 1; i > 0; i--)
            {
                int j = _doorRng.Range(0, i + 1);
                (Room.Doors[i], Room.Doors[j]) = (Room.Doors[j], Room.Doors[i]);
            }

            if (!nextIsBoss && next >= 2 && _doorRng.Chance(0.4f))
            {
                int t = _doorRng.Range(0, Room.Doors.Count);
                var d = Room.Doors[t];
                d.Trial = true;
                Room.Doors[t] = d;
            }
        }

        private bool AnyUpgradeable()
        {
            foreach (var p in Players)
                foreach (var def in PerkCatalog.All)
                {
                    int level = p.PerkLevel(def.Id);
                    if (level > 0 && level < def.MaxLevel) return true;
                }
            return false;
        }

        private void GrantReward(RoomReward reward, int times)
        {
            float amount = 0f;
            switch (reward)
            {
                case RoomReward.Boon:
                case RoomReward.Upgrade:
                    StockForAll(reward == RoomReward.Boon ? ChoiceKind.Boon : ChoiceKind.Upgrade, times);
                    amount = times;
                    break;
                case RoomReward.Vigor:
                    foreach (var p in Players)
                    {
                        float before = p.Hp;
                        p.AddVigor(times);
                        if (p.Alive) p.Hp = p.MaxHp;
                        if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
                    }
                    amount = VigorPerStack * 100f * times;
                    break;
                case RoomReward.Treasure:
                    GrantTreasure(times);
                    amount = times;
                    break;
                case RoomReward.Shards:
                    int shards = ShardReward(Room.Area) * times;
                    BonusShards += shards;
                    amount = shards;
                    break;
                default:
                    return;
            }
            Events.Add(new SimEvent(SimEventType.RewardGranted) { Int = (int)reward, Value = amount, Id = times > 1 ? 1 : 0 });
        }
    }
}
