using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The map's extras: keys (won from the champion and from puzzles, shared by the team), the locked treasure room,
    /// the champion's room, the two light puzzles (pressure plates, guard the crystal) and the secret room behind a
    /// cracked wall, which holds the old map of the area.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float PlateHold = 0.5f;
        public const float PlateRadius = 1.3f;
        public const float CrystalSeconds = 30f;
        public const float CrystalReach = 1.6f;
        public const float CrystalBite = 7f;     // damage per second from each monster at the crystal (scaled)
        public const float UnlockRadius = 2.2f;
        public const float ChampionHealth = 3f;  // on top of an elite's
        public const float ThiefSeconds = 20f;
        public const float ImpPanic = 8f;      // the imp runs from anyone this close
        /// <summary>Plates stay lit this long after you step off: solo you have to run between them.</summary>
        public float PlateWindow => Players.Count <= 1 ? 8f : 5f;

        /// <summary>Keys the team holds (one opens one treasure room).</summary>
        public int Keys { get; private set; }
        /// <summary>The old map from the secret room: the whole area shows on everyone's map.</summary>
        public bool MapRevealed { get; private set; }

        private readonly Dictionary<int, List<int>> _puzzleProps = new();
        private readonly Dictionary<int, int> _crackedWalls = new(); // wall monster id -> secret room id
        private readonly HashSet<int> _impsCaught = new(); // rooms whose imp was caught (not escaped)

        /// <summary>The plates lie around the middle of a plate hall (the layout keeps these spots clear).</summary>
        public static readonly Vector2[] PlateSpots = { new(-8f, -3.5f), new(8f, -3.5f), new(0f, 3.4f) }; // clear of the doorways
        public static readonly Vector2 CrystalSpot = new(0f, 0.5f);
        public static readonly Vector2 GauntletSpot = new(0f, 0.5f);

        /// <summary>Champions of the mini-boss rooms, per area.</summary>
        private static readonly EnemyType[] Champions = { EnemyType.DreadKnight, EnemyType.Ghoul, EnemyType.Necromancer };

        /// <summary>A new area: its puzzles and walls are new, the old map is of the old area. Keys carry over.</summary>
        private void ResetPuzzles()
        {
            _puzzleProps.Clear();
            _crackedWalls.Clear();
            _impsCaught.Clear();
            MapRevealed = false;
        }

        // ------------------------------------------------------------------ Waking the rooms

        /// <summary>A puzzle room wakes: its plates or its crystal appear (the monsters come by the usual spawning).</summary>
        private void WakePuzzle(RoomState room)
        {
            var props = new List<int>();
            _puzzleProps[room.Id] = props;
            if (room.Objective is RoomObjective.Plates or RoomObjective.Gauntlet or RoomObjective.Twin)
            {
                // A gauntlet has one plate, in the middle of its traps.
                var spots = room.Objective == RoomObjective.Gauntlet ? new[] { GauntletSpot } : room.Objective == RoomObjective.Twin ? TwinSpots : PlateSpots;
                foreach (var spot in spots)
                {
                    int id = Enemies.Spawn(EnemyType.PressurePlate, room.Center + spot, 1f, 1f, false);
                    ref var plate = ref Enemies[Enemies.IndexOf(id)];
                    plate.RoomId = room.Id;
                    plate.Health = 0f;
                    plate.MaxHealth = 1f;
                    props.Add(id);
                }
                room.Target = room.Objective == RoomObjective.Twin ? TwinHold : spots.Length;
            }
            else if (room.Objective == RoomObjective.Thief)
            {
                int id = Enemies.Spawn(EnemyType.TreasureImp, room.Center + new Vector2(0f, 2f), HealthMultiplier(room.Index, PlayersIn(room)), 1f, false);
                Enemies[Enemies.IndexOf(id)].RoomId = room.Id;
                props.Add(id);
                room.Target = ThiefSeconds;
            }
            else
            {
                int id = Enemies.Spawn(EnemyType.WardCrystal, room.Center + CrystalSpot, HealthMultiplier(room.Index, 1), 1f, false);
                Enemies[Enemies.IndexOf(id)].RoomId = room.Id;
                props.Add(id);
                room.Target = CrystalSeconds;
            }
            room.SpawnTimer = 2f;
        }

        /// <summary>The champion's room: the champion and an escort sized to who walked in.</summary>
        private void SpawnChampion()
        {
            var type = Champions[Mathf.Clamp(Room.Area, 0, Champions.Length - 1)];
            PendingSpawns.Add(new PendingSpawn { Room = Room.Id, Type = type, Position = Room.Center + new Vector2(0f, 2.5f), Timer = 1.2f, Elite = true, Champion = true });
            Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = Room.Center + new Vector2(0f, 2.5f), Int = (int)type, Value = 1.2f });
            int escort = 1 + PlayersIn(Room) + Room.Area;
            for (int i = 0; i < escort; i++) QueueSpawn(RandomEnemyType(), false);
        }

        /// <summary>A champion arrives: much tougher than an elite and a little bigger.</summary>
        private void MakeChampion(int index)
        {
            ref var e = ref Enemies[index];
            e.Champion = true;
            e.Health *= ChampionHealth;
            e.MaxHealth *= ChampionHealth;
            e.Radius *= 1.15f;
        }

        // ------------------------------------------------------------------ Per tick

        private void StepPuzzles(float dt)
        {
            foreach (var room in RoomStates)
            {
                if (!room.Fighting || !_puzzleProps.TryGetValue(room.Id, out var props)) continue;
                Room = room;
                if (room.Objective is RoomObjective.Plates or RoomObjective.Gauntlet) StepPlates(room, props, dt);
                else if (room.Objective == RoomObjective.Twin) StepTwin(room, props, dt); // RunSimulation.Coop.cs
                else if (room.Objective == RoomObjective.Crystal) StepCrystal(room, props, dt);
                else if (room.Objective == RoomObjective.Thief) StepThief(room, props, dt);
            }
            UpdateSealedDoors();
        }

        private void StepPlates(RoomState room, List<int> props, float dt)
        {
            int lit = 0;
            foreach (int id in props)
            {
                int i = Enemies.IndexOf(id);
                if (i < 0) continue;
                ref var plate = ref Enemies[i];
                bool on = false;
                foreach (var p in Players)
                    if (p.Alive && (p.Position - plate.Position).sqrMagnitude <= PlateRadius * PlateRadius) on = true;
                // StateTimer: seconds it stays lit; DotAccumulator: how long it has been stood on.
                plate.DotAccumulator = on ? plate.DotAccumulator + dt : 0f;
                if (on && plate.DotAccumulator >= PlateHold)
                {
                    if (plate.StateTimer <= 0f) Events.Add(new SimEvent(SimEventType.TrialEvent) { Int = (int)TrialMoment.SigilLit, Pos = plate.Position });
                    plate.StateTimer = PlateWindow;
                }
                else plate.StateTimer -= dt;
                plate.Moving = plate.StateTimer > 0f; // shown lit
                plate.Health = plate.Moving ? Mathf.Clamp01(plate.StateTimer / PlateWindow) : Mathf.Clamp01(plate.DotAccumulator / PlateHold) * 0.5f;
                if (plate.Moving) lit++;
            }
            room.Progress = lit;
            if (lit < props.Count) return;
            EndPuzzle(room, solved: true);
        }

        private void StepCrystal(RoomState room, List<int> props, float dt)
        {
            int i = props.Count > 0 ? Enemies.IndexOf(props[0]) : -1;
            if (i < 0 || Enemies[i].Dead || Enemies[i].Health <= 0f)
            {
                EndPuzzle(room, solved: false);
                return;
            }
            if (PlayersIn(room, alive: true) > 0) room.Progress += dt;
            if (room.Progress >= room.Target) EndPuzzle(room, solved: true);
        }

        /// <summary>The imp gets away when the time is up; caught (killed), it was worth a key.</summary>
        private void StepThief(RoomState room, List<int> props, float dt)
        {
            int i = props.Count > 0 ? Enemies.IndexOf(props[0]) : -1;
            if (i < 0)
            {
                EndPuzzle(room, solved: _impsCaught.Contains(room.Id));
                return;
            }
            if (PlayersIn(room, alive: true) > 0) room.Progress += dt;
            if (room.Progress < room.Target) return;
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Smoke, Pos = Enemies[i].Position, Value = 1.5f });
            EndPuzzle(room, solved: false); // it slipped away
        }

        /// <summary>The imp was caught: its loot spills out (a key comes with the puzzle's end).</summary>
        private void ImpCaught(in Enemy imp)
        {
            _impsCaught.Add(imp.RoomId);
            int total = Mathf.RoundToInt((12f + 6f * Area) * (1f + 0.5f * Mathf.Max(0, Players.Count - 1)));
            for (int s = 0; s < 6; s++) DropSoul(imp.Position + Rotate(Vector2.right, s * 60f + 30f) * 0.9f, Mathf.Max(1, total / 6));
            foreach (var p in Players)
                if (p.RoomId == imp.RoomId) DropFor(p, RollItem(p, 1.5f, Rarity.Magic), imp.Position + new Vector2(_lootRng.Range(-1f, 1f), _lootRng.Range(-0.6f, 0.6f)));
        }

        /// <summary>The imp runs from the nearest player in its room, sliding along walls rather than getting stuck in corners.</summary>
        private void Flee(ref Enemy e, EnemyDef def, float dt)
        {
            SimPlayer near = NearestLivingPlayer(e.Position, e.RoomId);
            Vector2 dir;
            if (near != null && (near.Position - e.Position).sqrMagnitude < ImpPanic * ImpPanic)
            {
                Vector2 away = e.Position - near.Position;
                dir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
                // Near a wall the "away" points into it: turn along it instead.
                var b = Bounds;
                if (e.Position.x < b.xMin + 1.5f && dir.x < 0f || e.Position.x > b.xMax - 1.5f && dir.x > 0f) dir = new Vector2(0f, Mathf.Sign(dir.y + 0.01f * (e.Id % 2 == 0 ? 1 : -1)));
                if (e.Position.y < b.yMin + 1.5f && dir.y < 0f || e.Position.y > b.yMax - 1.5f && dir.y > 0f) dir = new Vector2(Mathf.Sign(dir.x + 0.01f * (e.Id % 2 == 0 ? 1 : -1)), 0f);
                // A little zig-zag so it doesn't run in a straight line.
                dir = Rotate(dir, Mathf.Sin(Elapsed * 3f + e.Id) * 25f);
            }
            else
            {
                Vector2 toCenter = Room.Center - e.Position;
                dir = toCenter.sqrMagnitude > 4f ? toCenter.normalized * 0.4f : Vector2.zero;
            }
            if (dir == Vector2.zero) return;
            if (Mathf.Abs(dir.x) > 0.1f) e.Facing = Mathf.Sign(dir.x);
            dir = Arena.Steer(Layout, e.Position, dir, e.Radius, e.Id);
            e.Position += dir * def.Speed * (e.HitStun > 0f ? 1.6f : 1f) * dt; // a hit makes it bolt
            e.Moving = true;
        }

        /// <summary>Health left on the crystal of a guard puzzle (0..1), or -1 when there is none.</summary>
        public float CrystalHealth(RoomState room)
        {
            if (room.Objective != RoomObjective.Crystal || !_puzzleProps.TryGetValue(room.Id, out var props) || props.Count == 0) return -1f;
            int i = Enemies.IndexOf(props[0]);
            return i < 0 || Enemies[i].Dead ? 0f : Mathf.Clamp01(Enemies[i].Health / Enemies[i].MaxHealth);
        }

        /// <summary>
        /// Monsters in a guard room go for the crystal unless someone stands right in their way; at the crystal they
        /// chip at it. Returns true when it took care of this monster's move this tick.
        /// </summary>
        private bool ChaseCrystal(ref Enemy e, EnemyDef def, SimPlayer nearestPlayer, float dt)
        {
            if (Room.Objective != RoomObjective.Crystal || !Room.Fighting || e.State != EnemyState.Chase) return false;
            if (!_puzzleProps.TryGetValue(Room.Id, out var props) || props.Count == 0) return false;
            int ci = Enemies.IndexOf(props[0]);
            if (ci < 0 || Enemies[ci].Dead) return false;
            if (nearestPlayer != null && (nearestPlayer.Position - e.Position).sqrMagnitude < 2.5f * 2.5f) return false;

            Vector2 crystal = Enemies[ci].Position;
            Vector2 to = crystal - e.Position;
            float reach = CrystalReach + e.Radius;
            if (to.sqrMagnitude > reach * reach)
            {
                Vector2 dir = to.normalized;
                if (Mathf.Abs(dir.x) > 0.1f) e.Facing = Mathf.Sign(dir.x);
                if (!def.Flying) dir = Arena.Steer(Layout, e.Position, dir, e.Radius, e.Id);
                e.Position += dir * def.Speed * dt;
                e.Moving = true;
                return true;
            }
            ref var c = ref Enemies[ci];
            float bite = CrystalBite * e.DamageMultiplier * dt;
            c.Health -= bite;
            c.DotAccumulator += bite;
            if (c.DotAccumulator >= 4f)
            {
                Events.Add(new SimEvent(SimEventType.EnemyHit) { Id = c.Id, Pos = c.Position, Value = c.DotAccumulator, Int = HitInfo.Pack(HitFlags.DamageOverTime, null) });
                c.DotAccumulator = 0f;
            }
            if (c.Health <= 0f) c.Dead = true;
            return true;
        }

        /// <summary>A puzzle is over: solved, it gives a key; either way the room is cleared.</summary>
        private void EndPuzzle(RoomState room, bool solved)
        {
            Events.Add(new SimEvent(SimEventType.PuzzleEnded) { Id = room.Id, Int = solved ? 1 : 0, Pos = room.Center });
            if (_puzzleProps.TryGetValue(room.Id, out var props))
                foreach (int id in props)
                {
                    int i = Enemies.IndexOf(id);
                    if (i >= 0) Enemies[i].Dead = true;
                }
            _puzzleProps.Remove(room.Id);
            if (solved) GainKey(room, room.Center);
            ClearRoom(room);
        }

        private void GainKey(RoomState room, Vector2 at)
        {
            Keys++;
            Events.Add(new SimEvent(SimEventType.KeyGained) { Id = room.Id, Int = Keys, Pos = at });
        }

        /// <summary>The champion's room is cleared: the key, and something good for everyone who fought there.</summary>
        private void ChampionDefeated(RoomState room)
        {
            GainKey(room, room.Center);
            GrantBlessings();
            foreach (var p in Players)
                if (p.RoomId == room.Id) DropFor(p, RollItem(p, 2f, Rarity.Rare), room.Center + new Vector2(_lootRng.Range(-1.5f, 1.5f), _lootRng.Range(-1f, 1f)));
        }

        // ------------------------------------------------------------------ Sealed doors

        /// <summary>The door from a room's neighbour into a sealed room, and where someone stands to open it.</summary>
        private bool SealedDoor(MapRoom sealedRoom, out MapRoom outside, out int dir)
        {
            for (int d = 0; d < 4; d++)
            {
                if (sealedRoom.Neighbor[d] < 0) continue;
                outside = Map[sealedRoom.Neighbor[d]];
                dir = Dir.Opposite(d); // from outside towards the sealed room
                return true;
            }
            outside = null;
            dir = 0;
            return false;
        }

        /// <summary>Where you stand to open a locked treasure door (just inside the room next to it).</summary>
        public Vector2 UnlockSpot(MapRoom treasure) =>
            SealedDoor(treasure, out var outside, out int dir) ? DungeonMap.EntryPoint(outside, dir) : treasure.Origin;

        /// <summary>
        /// A treasure door opens when someone with the team's key stands at it; the cracked wall of a secret room
        /// shows up the first time someone enters the room beside it.
        /// </summary>
        private void UpdateSealedDoors()
        {
            foreach (var r in Map.Rooms)
            {
                var state = RoomStates[r.Id];
                if (!r.Sealed || state.Unlocked || !SealedDoor(r, out var outside, out int dir)) continue;
                if (r.Kind == RoomKind.Treasure)
                {
                    if (Keys <= 0) continue;
                    Vector2 spot = DungeonMap.EntryPoint(outside, dir);
                    foreach (var p in Players)
                    {
                        if (!p.Alive || (p.Position - spot).sqrMagnitude > UnlockRadius * UnlockRadius) continue;
                        Keys--;
                        state.Unlocked = true;
                        Events.Add(new SimEvent(SimEventType.DoorOpened) { Int = 2, Pos = DungeonMap.DoorPosition(outside, dir), Id = r.Id });
                        break;
                    }
                }
                else if (RoomStates[outside.Id].Visited && !_crackedWalls.ContainsValue(r.Id))
                {
                    // The cracked wall stands just inside the door it hides.
                    int id = Enemies.Spawn(EnemyType.CrackedWall, DungeonMap.EntryPoint(outside, dir) + (Vector2)Dir.Step(dir) * 0.4f, HealthMultiplier(r.Index, 1), 1f, false);
                    Enemies[Enemies.IndexOf(id)].RoomId = outside.Id;
                    _crackedWalls[id] = r.Id;
                }
            }
        }

        /// <summary>A cracked wall broke: its secret room is open.</summary>
        private void WallBroken(in Enemy wall)
        {
            if (!_crackedWalls.TryGetValue(wall.Id, out int secret)) return;
            RoomStates[secret].Unlocked = true;
            var room = Map[secret];
            SealedDoor(room, out var outside, out int dir);
            Events.Add(new SimEvent(SimEventType.DoorOpened) { Int = 3, Pos = DungeonMap.DoorPosition(outside, dir), Id = secret });
        }

        /// <summary>The secret room's find: the old map of the area (for the whole team).</summary>
        private void RevealMap() => MapRevealed = true;
    }
}
