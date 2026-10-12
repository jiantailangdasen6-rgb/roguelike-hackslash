using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The area map at run time. Each area is a <see cref="DungeonMap"/> of rooms joined by doors; players walk
    /// through doors on their own (the owner moves, the host just sees which room they stand in) and may split up.
    /// A fight room wakes when someone walks in and keeps sending waves until it is cleared; pedestal rooms give
    /// each player a reward once; the campfire heals; the boss door opens when every living player stands at it;
    /// after the boss, a way down opens to the next area.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float PedestalRadius = 1.6f;
        public const float PedestalHoldTime = 0.8f;
        public const float BossDoorRadius = 3.2f;
        /// <summary>The way down to the next area, opened in the boss room once the lord falls.</summary>
        public static readonly Vector2 ExitOffset = new(0f, 1.5f);

        public readonly ulong Seed;
        public DungeonMap Map { get; private set; }
        public int Area { get; private set; }
        public readonly List<RoomState> RoomStates = new();

        /// <summary>The door from the campfire to the boss has been opened (everyone came together).</summary>
        public bool BossDoorOpen { get; private set; }
        /// <summary>The boss of this area is down: the way to the next area is open in its room.</summary>
        public bool ExitOpen { get; private set; }
        /// <summary>How long the team has gathered at the boss door / the way down (seconds).</summary>
        public float GatherHold { get; private set; }

        /// <summary>Seed of an area's map: every peer generates the same map from it.</summary>
        public ulong MapSeed => Seed ^ 0x5DEECE66Dul;

        /// <summary>Item level gear works at: the deepest tier the team has reached (+ grace).</summary>
        public int GearLevelCap => _deepestIndex + 1 + GearLevelGrace;
        private int _deepestIndex;

        /// <summary>The deepest strength tier the team has walked into (how far the run got).</summary>
        public int DeepestIndex => _deepestIndex;

        /// <summary>Waves a fight room sends (the same on every peer, for the HUD).</summary>
        public static int WaveCountFor(RoomKind kind, int area) => kind == RoomKind.Elite ? 3 : 2 + (area >= 1 ? 1 : 0);

        /// <summary>Rooms (by bit) whose pedestal this player has used.</summary>
        public int PedestalsUsedBy(SimPlayer p)
        {
            int mask = 0;
            foreach (var r in RoomStates) if (r.UsedBy.Contains(p.ClientId)) mask |= 1 << r.Id;
            return mask;
        }

        /// <summary>Generates an area's map and its room states (nothing moves yet).</summary>
        private void BuildArea(int area)
        {
            Area = area;
            Map = DungeonMap.Generate(MapSeed, area);
            RoomStates.Clear();
            foreach (var r in Map.Rooms)
            {
                var state = new RoomState
                {
                    Id = r.Id, Kind = r.Kind, Index = r.Index, Layout = r.Layout, Bounds = r.Bounds,
                    Objective = r.Kind switch
                    {
                        RoomKind.Combat or RoomKind.Elite or RoomKind.MiniBoss => RoomObjective.Waves,
                        RoomKind.Puzzle => r.Puzzle switch
                        {
                            PuzzleKind.Plates => RoomObjective.Plates,
                            PuzzleKind.Thief => RoomObjective.Thief,
                            PuzzleKind.Gauntlet => RoomObjective.Gauntlet,
                            _ => RoomObjective.Crystal,
                        },
                        RoomKind.Boss => RoomObjective.Boss,
                        RoomKind.Campfire => RoomObjective.Rest,
                        _ => RoomObjective.Quiet,
                    },
                };
                state.Cleared = !state.IsFight;
                if (state.Objective == RoomObjective.Waves)
                {
                    state.WaveCount = r.Kind == RoomKind.MiniBoss ? 1 : WaveCountFor(r.Kind, area);
                    state.Target = state.WaveCount;
                }
                if (state.Objective == RoomObjective.Boss) state.Target = 1f;
                RoomStates.Add(state);
            }
            Room = RoomStates[Map.Start.Id];
            _deepestIndex = Mathf.Max(_deepestIndex, Room.Index);
            BossDoorOpen = ExitOpen = false;
            GatherHold = 0f;
            ResetHazards();
            ResetBossTrials();
            ResetPuzzles();
        }

        /// <summary>The team arrives in an area (the first, or the next after a boss): everyone stands in <paramref name="arrival"/>.</summary>
        private void EnterArea(MapRoom arrival)
        {
            // Whatever is left of the previous area goes.
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Dead = true;
            ProcessDeaths(giveXp: false);
            foreach (var soul in Souls)
            {
                TeamXp += soul.Value;
                Events.Add(new SimEvent(SimEventType.SoulCollected) { Id = soul.Id, ClientId = Players.Count > 0 ? Players[0].ClientId : 0 });
            }
            Souls.Clear();
            SweepLoot();
            PendingSpawns.Clear();
            foreach (var proj in Projectiles) Events.Add(new SimEvent(SimEventType.ProjectileEnd) { Id = proj.Id, Pos = proj.Position });
            Projectiles.Clear();
            _delayedAreas.Clear();
            _zones.Clear();
            _rainTargets.Clear();
            ClearBursts();

            Events.Add(new SimEvent(SimEventType.RoomStarted) { Int = Area * RoomsPerArea, Id = arrival.Id, Seed = MapSeed });
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Position = arrival.Origin + StartPosition(i);
                p.RoomId = -1;
                p.CagedBy = 0;
                if (p.Downed) Revive(p, 0.35f, guardian: false); // everyone who makes it through gets back up
                Events.Add(new SimEvent(SimEventType.Teleport) { ClientId = p.ClientId, Pos = p.Position });
            }
            UpdateRooms(0f);
        }

        /// <summary>Which room each player stands in; walking into a room wakes it.</summary>
        private void UpdateRooms(float dt)
        {
            foreach (var p in Players)
            {
                var at = Map.RoomAt(p.Position);
                if (at == null || at.Id == p.RoomId) continue;
                p.RoomId = at.Id;
                p.PedestalHold = 0f;
                EnterRoom(p, RoomStates[at.Id]);
            }

            foreach (var room in RoomStates)
            {
                Room = room;
                if (room.Objective == RoomObjective.Rest) WarmByTheFire(room, dt);
                if (Map[room.Id].HasPedestal) StepPedestal(room, dt);
            }
            OfferAll(); // someone may have stepped out of a fight into a quiet room
        }

        private void EnterRoom(SimPlayer p, RoomState room)
        {
            Room = room;
            if (!room.Visited)
            {
                room.Visited = true;
                _deepestIndex = Mathf.Max(_deepestIndex, room.Index);
                foreach (var mate in Players) mate.SetGearLevelCap(GearLevelCap);
                SpawnProps(room);
                Events.Add(new SimEvent(SimEventType.RoomEntered) { ClientId = p.ClientId, Id = room.Id, Int = room.Index, Value = (int)room.Kind });
            }
            foreach (var mate in Players)
            {
                // Blessing: a shield on stepping into a fight.
                if (mate != p || !room.IsFight || room.Cleared) continue;
                int blessing = p.PerkLevel(PerkId.Blessing);
                if (blessing > 0 && p.Alive) p.Shield = Mathf.Max(p.Shield, BlessingShield * blessing);
                p.SecondWindUsed = false;
            }
            if (room.IsFight && !room.Cleared && !room.Active && p.Alive) WakeRoom(room);
        }

        /// <summary>Someone walked into a fight room: the monsters come.</summary>
        private void WakeRoom(RoomState room)
        {
            room.Active = true;
            room.Progress = 0f;
            room.Wave = 0;
            room.WaveTimer = 1.2f;
            room.SpawnTimer = 1.5f;
            StartHazards(room);
            if (room.Objective is RoomObjective.Plates or RoomObjective.Crystal or RoomObjective.Thief or RoomObjective.Gauntlet) WakePuzzle(room);
            if (room.Objective == RoomObjective.Boss)
            {
                room.SpawnTimer = 12f;
                ResetBossTrials();
                Vector2 at = room.Center + new Vector2(0f, 3f);
                PendingSpawns.Add(new PendingSpawn { Room = room.Id, Type = AreaBoss, Position = at, Timer = 1.6f });
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = at, Int = (int)AreaBoss, Value = 1.6f });
            }
        }

        /// <summary>Clears fight rooms whose job is done.</summary>
        private void UpdateObjectives(float dt)
        {
            foreach (var room in RoomStates)
            {
                if (!room.Fighting) continue;
                Room = room;
                switch (room.Objective)
                {
                    case RoomObjective.Survive:
                        if (PlayersIn(room, alive: true) > 0) room.Progress += dt;
                        if (room.Progress >= room.Target) ClearRoom(room);
                        break;
                    case RoomObjective.Waves:
                        room.Progress = Mathf.Max(0, room.Wave - 1);
                        if (room.Wave >= room.WaveCount && HostilesIn(room) == 0 && PendingIn(room) == 0)
                        {
                            room.Progress = room.WaveCount;
                            ClearRoom(room);
                        }
                        break;
                    case RoomObjective.Boss:
                        if (room.Progress >= 1f) ClearRoom(room);
                        break;
                }
            }
        }

        private void ClearRoom(RoomState room)
        {
            Room = room;
            room.Cleared = true;
            room.Active = false;
            RoomsCleared++;

            // Leftover monsters crumble (no XP) and their pending spawns are cancelled.
            for (int i = 0; i < Enemies.Count; i++)
                if (Enemies[i].RoomId == room.Id && !Enemies[i].Def.Prop) Enemies[i].Dead = true;
            ProcessDeaths(giveXp: false);
            PendingSpawns.RemoveAll(s => s.Room == room.Id);
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var proj = Projectiles[i];
                if (!proj.Hostile || !room.Bounds.Contains(proj.Position)) continue;
                Events.Add(new SimEvent(SimEventType.ProjectileEnd) { Id = proj.Id, Pos = proj.Position });
                Projectiles.RemoveAt(i);
            }
            StopHazards(room);

            foreach (var p in Players)
                if (p.Alive && p.RoomId == room.Id)
                {
                    float before = p.Hp;
                    p.Heal(p.MaxHp * 0.2f);
                    if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
                }

            if (room.Kind == RoomKind.MiniBoss) ChampionDefeated(room);
            if (room.Objective == RoomObjective.Boss)
            {
                RemoveTrialProps();
                ActiveTrial = BossTrial.None;
                ClearBursts();
                if (Area >= AreaCount - 1)
                {
                    Phase = RunPhase.Victory;
                    Events.Add(new SimEvent(SimEventType.RunEnded) { Int = 1 });
                    return;
                }
                ExitOpen = true;
                GatherHold = 0f;
                Events.Add(new SimEvent(SimEventType.DoorOpened) { Int = 1, Pos = room.Center + ExitOffset });
            }
            Events.Add(new SimEvent(SimEventType.RoomCleared) { Int = room.Index, Id = room.Id, Value = (int)room.Kind });
            OfferAll(); // the banked level-ups
        }

        // ------------------------------------------------------------------ Doors that need the whole team

        /// <summary>Where the campfire's door to the boss is (on the campfire room's wall).</summary>
        public Vector2 BossDoorPosition
        {
            get
            {
                var fire = Map.Campfire;
                for (int d = 0; d < 4; d++)
                    if (fire.Neighbor[d] == Map.Boss.Id) return DungeonMap.DoorPosition(fire, d);
                return fire.Origin;
            }
        }

        /// <summary>The way down in the boss room (after the boss).</summary>
        public Vector2 ExitPosition => Map.Boss.Origin + ExitOffset;

        /// <summary>
        /// The boss door opens when every living player stands at it together; the way down to the next area works
        /// the same once the boss is beaten (downed players are carried along).
        /// </summary>
        private void UpdateDoors(float dt)
        {
            if (Phase != RunPhase.Playing) return;
            Vector2 spot;
            float radius;
            if (!BossDoorOpen) { spot = BossDoorPosition; radius = BossDoorRadius; }
            else if (ExitOpen) { spot = ExitPosition; radius = ExitRadius * 1.6f; }
            else return;

            bool all = false;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                if ((p.Position - spot).sqrMagnitude > radius * radius)
                {
                    all = false;
                    break;
                }
                all = true;
            }
            GatherHold = all ? GatherHold + dt : 0f;
            if (GatherHold < ExitHoldTime) return;
            GatherHold = 0f;

            if (!BossDoorOpen)
            {
                BossDoorOpen = true;
                Events.Add(new SimEvent(SimEventType.DoorOpened) { Int = 0, Pos = spot });
                return;
            }
            BuildArea(Area + 1);
            EnterArea(Map.Start);
            CheckLevelUp();
        }

        // ------------------------------------------------------------------ Pedestals

        /// <summary>The pedestal of a room (shrine, spring, merchant...) stands in its middle.</summary>
        public static Vector2 PedestalPosition(Rect roomBounds) => roomBounds.center + new Vector2(0f, 0.6f);

        /// <summary>Standing on the pedestal for a moment uses it (once per player).</summary>
        private void StepPedestal(RoomState room, float dt)
        {
            Vector2 at = PedestalPosition(room.Bounds);
            foreach (var p in Players)
            {
                if (p.RoomId != room.Id || !p.Alive || room.UsedBy.Contains(p.ClientId)) continue;
                bool on = (p.Position - at).sqrMagnitude <= PedestalRadius * PedestalRadius;
                p.PedestalHold = on ? p.PedestalHold + dt : 0f;
                if (p.PedestalHold < PedestalHoldTime) continue;
                p.PedestalHold = 0f;
                room.UsedBy.Add(p.ClientId);
                UsePedestal(p, room);
            }
        }

        private void UsePedestal(SimPlayer p, RoomState room)
        {
            float amount = 0f;
            switch (room.Kind)
            {
                case RoomKind.Shrine:
                    p.StockedChoices.Enqueue(ChoiceKind.Boon);
                    break;
                case RoomKind.Whetstone:
                    p.StockedChoices.Enqueue(ChoiceKind.Upgrade);
                    break;
                case RoomKind.Spring:
                {
                    float before = p.Hp;
                    p.AddVigor(1);
                    p.Hp = p.MaxHp;
                    amount = VigorPerStack * 100f;
                    if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
                    break;
                }
                case RoomKind.CurseAltar:
                {
                    var kind = (CurseKind)_perkRng.Range(0, RewardCatalog.CurseKinds);
                    p.CurseStacks[(int)kind]++;
                    p.SetCurses(p.CurseStacks);
                    for (int i = 0; i < RewardCatalog.CursedBoons; i++) p.StockedChoices.Enqueue(ChoiceKind.Boon);
                    amount = (int)kind;
                    break;
                }
                case RoomKind.Merchant:
                    p.StockedChoices.Enqueue(ChoiceKind.Shop);
                    break;
                case RoomKind.Treasure:
                    GrantTreasureTo(p, 0);
                    break;
                case RoomKind.Secret:
                    GrantTreasureTo(p, 0);
                    RevealMap();
                    break;
            }
            Events.Add(new SimEvent(SimEventType.PedestalUsed) { ClientId = p.ClientId, Id = room.Id, Int = (int)room.Kind, Value = amount, Pos = PedestalPosition(room.Bounds) });
            OfferNext(p);
        }

        // ------------------------------------------------------------------ Tests and debug tools

        /// <summary>The room a player stands in (after a step has seen them there).</summary>
        public RoomState RoomOf(SimPlayer p) => p.RoomId >= 0 && p.RoomId < RoomStates.Count ? RoomStates[p.RoomId] : RoomStates[Map.Start.Id];

        /// <summary>The first room of a kind on this area's map (null if there is none).</summary>
        public MapRoom FindRoom(RoomKind kind) => Map.Rooms.Find(r => r.Kind == kind);

        /// <summary>Moves the whole team into a room of the map (it wakes on the next step); the boss door opens for it.</summary>
        public void MoveTeamTo(MapRoom room)
        {
            if (room.Kind == RoomKind.Boss) BossDoorOpen = true;
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Position = room.Origin + StartPosition(i);
                Events.Add(new SimEvent(SimEventType.Teleport) { ClientId = p.ClientId, Pos = p.Position });
            }
        }

        /// <summary>Tests: puts a layout into the start room (where tests stand) and starts its traps.</summary>
        public void UseLayoutForTests(int layoutId)
        {
            var start = RoomStates[Map.Start.Id];
            start.Layout = Arena.Get(layoutId).Placed(Map.Start.Origin);
            Map.Start.Layout = start.Layout;
            Room = start;
            ResetHazards();
            StartHazards(start);
        }

        /// <summary>Debug: on to the next area's entrance at once.</summary>
        public void GoToNextArea()
        {
            if (Area >= AreaCount - 1) return;
            BuildArea(Area + 1);
            EnterArea(Map.Start);
        }

        // ------------------------------------------------------------------ Campfire

        public const float RestHealRadius = 3.4f;
        /// <summary>Share of max HP restored per second while sitting by the campfire.</summary>
        public const float RestHealPerSecond = 0.12f;
        private float _restHealPulse;

        /// <summary>The campfire heals everyone close to it (and gets the fallen back up).</summary>
        private void WarmByTheFire(RoomState room, float dt)
        {
            _restHealPulse -= dt;
            bool pulse = _restHealPulse <= 0f;
            if (pulse) _restHealPulse = 1f;
            Vector2 fire = room.Center + Arena.RestFire;
            foreach (var p in Players)
            {
                if ((p.Position - fire).sqrMagnitude > RestHealRadius * RestHealRadius) continue;
                if (!p.Alive)
                {
                    Revive(p, 0.35f, guardian: false);
                    continue;
                }
                float before = p.Hp;
                p.Heal(p.MaxHp * RestHealPerSecond * dt);
                p.HealedByFire += p.Hp - before;
                if (pulse && p.HealedByFire > 0.5f)
                {
                    Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.HealedByFire });
                    p.HealedByFire = 0f;
                }
            }
        }

        /// <summary>A campfire chest broke open: a ring of souls (about a wave's worth of XP for the team).</summary>
        private void SpillChest(in Enemy chest)
        {
            int total = Mathf.RoundToInt((10f + 6f * Area) * (1f + 0.5f * Mathf.Max(0, Players.Count - 1)));
            const int pieces = 6;
            for (int s = 0; s < pieces; s++)
                DropSoul(chest.Position + Rotate(Vector2.right, s * 60f + 30f) * 0.9f, Mathf.Max(1, total / pieces));
            // And a piece of gear for each player (personal loot, fine or better).
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                float angle = (i - (Players.Count - 1) * 0.5f) * 40f - 90f;
                DropFor(p, RollItem(p, 1f, Rarity.Magic), chest.Position + Rotate(Vector2.right, angle) * 1.3f);
            }
        }
    }
}
