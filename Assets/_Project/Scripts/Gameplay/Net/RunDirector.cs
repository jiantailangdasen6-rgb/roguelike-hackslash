using System;
using System.Collections.Generic;
using Game.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Scene-placed network object in the Game scene. On the host it owns the <see cref="RunSimulation"/>,
    /// feeds it player positions/inputs, mirrors state into NetworkVariables and streams events/snapshots.
    /// On every peer it decodes events and raises <see cref="EventReceived"/> for the presentation layer.
    /// </summary>
    public sealed class RunDirector : NetworkBehaviour
    {
        public const float TickRate = 30f;
        private const float TickDt = 1f / TickRate;
        private const int SnapshotEveryTicks = 2;
        private const int EventChunkBytes = 1000;
        private const float StartFallbackSeconds = 8f;

        public static RunDirector Instance { get; private set; }

        /// <summary>Raised on every peer (host included) for each simulation event.</summary>
        public static event Action<SimEvent> EventReceived;

        [SerializeField] private NetworkObject playerPrefab;

        public readonly NetworkVariable<int> TeamLevel = new(1);
        public readonly NetworkVariable<float> XpFraction = new();
        /// <summary>The deepest strength tier reached (area * 4 + 0..3): how far the run got.</summary>
        public readonly NetworkVariable<int> RoomIndex = new();
        /// <summary>The area map: every peer generates it from the seed (<see cref="DungeonMap.Generate"/>).</summary>
        public readonly NetworkVariable<ulong> MapSeed = new();
        public readonly NetworkVariable<int> Area = new();
        /// <summary>The land of every area so far, 4 bits each (RunSimulation.Route), and the two ways after the lord (left | right << 4).</summary>
        public readonly NetworkVariable<ulong> RouteBits = new();
        public readonly NetworkVariable<int> Ways = new();
        /// <summary>The run's omen (RunSimulation.Omens).</summary>
        public readonly NetworkVariable<int> OmenId = new();
        public Omen Omen => (Omen)OmenId.Value;
        /// <summary>The way to the heart opens past the third lord (someone carries all six memories).</summary>
        public readonly NetworkVariable<bool> HeartOpen = new();

        /// <summary>The land of an area this run went through (or is in).</summary>
        public Biome BiomeOf(int area) => area >= 0 && area < 16 ? (Biome)((RouteBits.Value >> (area * 4)) & 0xF) : BiomeCatalog.Classic(area);
        /// <summary>The land the team is in now.</summary>
        public Biome Biome => BiomeOf(Area.Value);
        /// <summary>The land a way out of the lord's hall leads to (0 left, 1 right).</summary>
        public Biome Way(int side) => (Biome)((Ways.Value >> (side * 4)) & 0xF);
        /// <summary>Rooms by bit: visited by anyone, cleared, being fought.</summary>
        public readonly NetworkVariable<int> VisitedRooms = new();
        public readonly NetworkVariable<int> ClearedRooms = new();
        public readonly NetworkVariable<int> FightingRooms = new();
        /// <summary>Wave number per room, 4 bits each (the HUD of whoever stands there).</summary>
        public readonly NetworkVariable<ulong> RoomWaves = new();
        public readonly NetworkVariable<bool> BossDoorOpen = new();
        /// <summary>Keys the team holds, sealed rooms opened (bits), the old map found, and the puzzle's state (crystal health or plates lit).</summary>
        public readonly NetworkVariable<int> Keys = new();
        public readonly NetworkVariable<int> UnlockedRooms = new();
        public readonly NetworkVariable<bool> MapRevealed = new();
        public readonly NetworkVariable<float> PuzzleValue = new();
        public readonly NetworkVariable<float> PuzzleTime = new();
        /// <summary>This week's labyrinth: the week (Weekly), -1 for an ordinary run.</summary>
        public readonly NetworkVariable<int> WeeklyWeek = new(-1);
        public readonly NetworkVariable<ulong> LanternHolder = new();
        public readonly NetworkVariable<Vector2> LanternPos = new();
        public readonly NetworkVariable<bool> ExitOpen = new();
        /// <summary>How long the team has gathered at the boss door / the way down.</summary>
        public readonly NetworkVariable<float> GatherHold = new();
        public readonly NetworkVariable<int> BonusShards = new();
        /// <summary>Someone has a choice waiting on screen.</summary>
        public readonly NetworkVariable<bool> Paused = new();
        /// <summary>The host's browser tab is in the background: the run is on hold until it comes back.</summary>
        public readonly NetworkVariable<bool> HostAway = new();
        public readonly NetworkVariable<float> Elapsed = new();
        public readonly NetworkVariable<RunPhase> Phase = new();
        public readonly NetworkVariable<float> BossHealth = new(-1f);
        public readonly NetworkVariable<bool> Started = new();
        public readonly NetworkVariable<int> Kills = new();
        public readonly NetworkVariable<int> BossesKilled = new();
        public readonly NetworkVariable<int> RoomsCleared = new();
        public readonly NetworkVariable<Difficulty> DifficultyLevel = new(Difficulty.Normal);
        public readonly NetworkVariable<EnemyType> BossType = new();
        /// <summary>The boss's trial in progress, seconds left on it, and its progress (done | total << 8).</summary>
        public readonly NetworkVariable<BossTrial> Trial = new();
        public readonly NetworkVariable<float> TrialTimer = new();
        public readonly NetworkVariable<int> TrialProgress = new();
        /// <summary>How much of the Lich's floor has fallen (players' movement is clamped to what's left).</summary>
        public readonly NetworkVariable<int> CollapseStage = new();
        /// <summary>Area times and downs so far (RunSimulation.SplitsText), for the result screen.</summary>
        public readonly NetworkVariable<FixedString512Bytes> RunSplits = new();
        private int _splitsVersion = -1;

        /// <summary>Host only.</summary>
        public RunSimulation Sim { get; private set; }

        /// <summary>Client only: latest enemy states from snapshots, keyed by enemy id.</summary>
        public readonly Dictionary<ushort, (EnemyRenderState State, float Time)> ClientEnemies = new();

        public bool IsRunning => Started.Value && Phase.Value == RunPhase.Playing;

        // ------------------------------------------------------------------ The map, on every peer

        private DungeonMap _map;
        private (ulong Seed, int Area, Biome Biome) _mapKey;

        /// <summary>The current area's map (the host's own; clients generate the same one from the seed).</summary>
        public DungeonMap Map
        {
            get
            {
                if (IsServer && Sim != null) return Sim.Map;
                var key = (MapSeed.Value, Area.Value, Biome);
                if (_map == null || key != _mapKey)
                {
                    _mapKey = key;
                    _map = DungeonMap.Generate(key.Item1, key.Item2, key.Item3);
                }
                return _map;
            }
        }

        /// <summary>The room the local player stands in (the start room before anyone has spawned).</summary>
        public MapRoom LocalRoom
        {
            get
            {
                var map = Map;
                var local = NetPlayer.Local;
                return (local != null ? map.RoomAt(local.transform.position) : null) ?? _lastLocalRoom ?? map.Start;
            }
        }
        private MapRoom _lastLocalRoom;

        public bool IsVisited(int room) => (VisitedRooms.Value & (1 << room)) != 0;
        public bool IsCleared(int room) => (ClearedRooms.Value & (1 << room)) != 0;
        public bool IsFighting(int room) => (FightingRooms.Value & (1 << room)) != 0;
        public bool IsUnlocked(int room) => (UnlockedRooms.Value & (1 << room)) != 0;
        public int WaveOf(int room) => (int)((RoomWaves.Value >> (room * 4)) & 0xF);

        /// <summary>
        /// Doors are open except the boss door (until the whole team has gathered at it) and the boss room's doors
        /// while the lord fights (no running away).
        /// </summary>
        public bool DoorOpen(MapRoom from, MapRoom to)
        {
            if (to == null) return false;
            if (to.Kind == RoomKind.Boss && !BossDoorOpen.Value) return false;
            if (to.Sealed && !IsUnlocked(to.Id)) return false; // locked treasure, hidden secret
            if (from.Kind == RoomKind.Boss && IsFighting(from.Id)) return false;
            return true;
        }

        /// <summary>A corridor can be walked when the doors at both of its ends are open.</summary>
        public bool CorridorOpen(Corridor c) => DoorOpen(Map[c.A], Map[c.B]) && DoorOpen(Map[c.B], Map[c.A]);

        /// <summary>The floor of a room as it stands (the Lich's hall shrinks as it falls).</summary>
        public Rect FloorOf(MapRoom room) => room.Kind == RoomKind.Boss ? RunSimulation.SafeArea(room.Bounds, CollapseStage.Value) : room.Bounds;

        /// <summary>A step on the map that stays on floors and in open corridors (sliding along walls).</summary>
        public Vector2 Walk(Vector2 from, Vector2 to, float radius) => Map.Step(from, to, radius, CorridorOpen, FloorOf);

        /// <summary>Can someone stand here (a room's floor or an open corridor)?</summary>
        public bool Walkable(Vector2 at, float radius) => Map.Walkable(at, radius, CorridorOpen, FloorOf);

        /// <summary>The local player stands where a fight is going on (picks and gear swaps wait).</summary>
        public bool LocalFighting => IsFighting(LocalRoom.Id);

        private void LateUpdate()
        {
            var local = NetPlayer.Local;
            if (local != null && IsSpawned) _lastLocalRoom = Map.RoomAt(local.transform.position) ?? _lastLocalRoom;
        }

        private readonly List<EnemyRenderState> _snapshotBuffer = new();
        private readonly List<ulong> _remoteClients = new();
        private float _accumulator;
        private int _tick;
        private float _spawnTime;
        private float _startTime;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            SpriteSheet.PreloadAll(); // behind the "on the way" curtain, not when a new room or monster first shows
            NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(NetMessageNames.Events, OnEventsMessage);
            if (!IsServer)
            {
                NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(NetMessageNames.Snapshot, OnSnapshotMessage);
                return;
            }

            _spawnTime = Time.time;
            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null && NetworkManager.CustomMessagingManager != null)
            {
                NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(NetMessageNames.Events);
                NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(NetMessageNames.Snapshot);
            }
            if (IsServer && NetworkManager != null)
            {
                if (NetworkManager.SceneManager != null)
                {
                    NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                    NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
                }
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ Host: run lifecycle

        private void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode,
            List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == GameSession.GameScene) StartRun();
        }

        /// <summary>
        /// The connection broke mid-run (host): everyone else is kept as a player who left (their character waits for
        /// them by profile key) and the run is handed to GameSession, to carry on in a new room.
        /// </summary>
        public bool SuspendForResume()
        {
            if (!IsServer || Sim == null || !Started.Value || Sim.Phase != RunPhase.Playing) return false;
            foreach (var p in Sim.Players.ToArray())
                if (p.ClientId != NetworkManager.LocalClientId) Sim.DetachPlayer(p.ClientId, GameSession.LoadoutFor(p.ClientId).playerKey);
            Sim.Events.Clear();
            GameSession.Suspend(new GameSession.SuspendedRun { Sim = Sim, Slots = new Dictionary<string, int>(_slotByKey), Online = GameSession.JoinCode != null });
            Debug.Log($"[RunDirector] Run suspended at {Sim.Elapsed:0}s (area {Sim.Area + 1})");
            return true;
        }

        /// <summary>A new room opened straight into a suspended run: the host's character, then the others as they rejoin.</summary>
        private bool ResumeRun(GameSession.SuspendedRun run)
        {
            ulong me = NetworkManager.LocalClientId;
            var mine = run.Sim.GetPlayer(me);
            if (mine == null) return false;
            Sim = run.Sim;
            DifficultyLevel.Value = Sim.Difficulty;
            foreach (var (key, slot) in run.Slots) _slotByKey[key] = slot;
            var loadout = GameSession.LoadoutFor(me);
            int mySlot = _slotByKey.TryGetValue(loadout.playerKey, out int s) ? s : 0;
            SpawnPlayer(me, mine, loadout, mySlot);
            Sim.SyncGear(mine);
            Sim.Events.Add(new SimEvent(SimEventType.Teleport) { ClientId = me, Pos = mine.Position });
            Started.Value = true;
            _startTime = Time.time;
            Debug.Log($"[RunDirector] Run resumed at {Sim.Elapsed:0}s (area {Sim.Area + 1})");
            return true;
        }

        private void StartRun()
        {
            if (Started.Value) return;
            var suspended = GameSession.TakeSuspended();
            if (suspended != null && ResumeRun(suspended)) return;

            DifficultyLevel.Value = ChosenDifficulty();
            ulong seed = CommandLine.Has("-seed") ? (ulong)CommandLine.GetFloat("-seed", 1f) : (ulong)DateTime.UtcNow.Ticks; // -seed n: the same maps (screenshots)
            // -startin twin (screenshots): look for a map whose puzzle is the twin plates.
            string wanted = CommandLine.GetValue("-startin")?.ToLowerInvariant();
            int wantedArea = (int)CommandLine.GetFloat("-startroom", 0f) / RunSimulation.RoomsPerArea;
            System.Predicate<MapRoom> has = wanted switch
            {
                "twin" => r => r.Kind == RoomKind.Puzzle && r.Puzzle == PuzzleKind.Twin,
                "spirit" => r => r.Kind == RoomKind.Spirit,
                _ => null,
            };
            if (has != null)
                for (int k = 0; k < 300 && !DungeonMap.Generate(new RunSimulation(seed).MapSeed, wantedArea).Rooms.Exists(has); k++) seed++;
            // This week's labyrinth (the host's choice at the camp's tablet, or -weekly): the week's maps, standard difficulty.
            if (ProfileStore.Current.weekly || CommandLine.Has("-weekly"))
            {
                WeeklyWeek.Value = Weekly.Current;
                seed = Weekly.Seed(WeeklyWeek.Value);
                DifficultyLevel.Value = Difficulty.Normal;
            }
            else WeeklyWeek.Value = -1;
            Sim = new RunSimulation(seed, DifficultyLevel.Value);
            // The lands: the first from the seed, the next chosen by the team. -route crypt,frost,... fixes them (screenshots).
            var planned = new List<Biome>();
            foreach (var name in (CommandLine.GetValue("-route") ?? "").Split(','))
                if (Enum.TryParse(name.Trim(), true, out Biome land)) planned.Add(land);
            // -lord GraveReaper... (screenshots): that lord wherever its land comes.
            if (Enum.TryParse(CommandLine.GetValue("-lord") ?? "", true, out EnemyType forcedLord)) Sim.ForcedLord = forcedLord;
            Sim.UseVariedRoute(planned.ToArray());
            // -omen bloodmoon|none... (screenshots, tests): this run's omen.
            if (Enum.TryParse(CommandLine.GetValue("-omen") ?? "", true, out Omen omen)) Sim.SetOmen(omen);
            Sim.DropChanceScale = CommandLine.GetFloat("-lootrate", 1f);
            Sim.BossStartHealth = CommandLine.GetFloat("-bosshp", 1f);
            if (CommandLine.Has("-mimic")) Sim.MimicOdds = 1f;
            int slot = 0;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                var loadout = GameSession.LoadoutFor(clientId);
                SpawnPlayer(clientId, Sim.AddPlayer(clientId, loadout), loadout, slot++);
            }

            Sim.Start((int)CommandLine.GetFloat("-startroom", 0f));
            // -startin <plates|crystal|miniboss|treasure|secret|...> (screenshots): begin in a room of that kind.
            string startIn = CommandLine.GetValue("-startin");
            if (!string.IsNullOrEmpty(startIn))
            {
                var map = Sim.Map;
                var room = startIn.ToLowerInvariant() switch
                {
                    "plates" => map.Rooms.Find(r => r.Kind == RoomKind.Puzzle && r.Puzzle == PuzzleKind.Plates) ?? map.Rooms.Find(r => r.Kind == RoomKind.Puzzle),
                    "crystal" => map.Rooms.Find(r => r.Kind == RoomKind.Puzzle && r.Puzzle == PuzzleKind.Crystal) ?? map.Rooms.Find(r => r.Kind == RoomKind.Puzzle),
                    "thief" => map.Rooms.Find(r => r.Kind == RoomKind.Puzzle && r.Puzzle == PuzzleKind.Thief) ?? map.Rooms.Find(r => r.Kind == RoomKind.Puzzle),
                    "gauntlet" => map.Rooms.Find(r => r.Kind == RoomKind.Puzzle && r.Puzzle == PuzzleKind.Gauntlet) ?? map.Rooms.Find(r => r.Kind == RoomKind.Puzzle),
                    "twin" => map.Rooms.Find(r => r.Kind == RoomKind.Puzzle && r.Puzzle == PuzzleKind.Twin) ?? map.Rooms.Find(r => r.Kind == RoomKind.Puzzle),
                    "dark" => map.DarkHall,
                    "captive" => map.Rooms.Find(r => r.Captive),
                    _ => Enum.TryParse(startIn, true, out RoomKind kind) ? map.Rooms.Find(r => r.Kind == kind) : null,
                };
                if (room != null) Sim.MoveTeamTo(room);
            }
            // -legendary (screenshots): everyone starts wearing a legendary piece for their class.
            if (CommandLine.Has("-legendary"))
                foreach (var sp in Sim.Players)
                {
                    sp.Bag.Add(ItemGenerator.Generate((ulong)(sp.ClientId + 7), 3, 9f, sp.ClassId, Rarity.Legendary, EquipSlot.Weapon));
                    sp.Equip(sp.Bag.Count - 1);
                }
            // -altweapon / -rune1 Ember -rune2 Storm (screenshots, tests): everyone starts with the other weapon or these runes.
            foreach (var sp in Sim.Players)
            {
                if (CommandLine.Has("-altweapon")) Wear(sp, ItemGenerator.Generate((ulong)(sp.ClientId + 11), 3, 2f, sp.ClassId, Rarity.Rare, EquipSlot.Weapon), "weapon_alt", RuneId.None);
                for (int skill = 1; skill <= 2; skill++)
                    if (Enum.TryParse(CommandLine.GetValue("-rune" + skill) ?? "", true, out RuneId rune))
                        Wear(sp, ItemGenerator.Generate(sp.ClientId + 13ul + (ulong)skill, 3, 2f, sp.ClassId, Rarity.Rare, skill == 1 ? EquipSlot.Rune1 : EquipSlot.Rune2), null, rune);
            }
            static void Wear(SimPlayer sp, ItemInstance item, string baseId, RuneId rune)
            {
                if (baseId != null) item.baseId = baseId;
                if (rune != RuneId.None) item.rune = rune;
                item.classId = sp.ClassId;
                sp.Bag.Add(item);
                int index = sp.Bag.Count - 1;
                sp.Equip(index);
                if (sp.Bag.Count > index) sp.Bag.RemoveAt(index); // the replaced piece is not kept (bots would put a rarer one back on)
            }
            // -blessings <FlameBrand,StormFist,...> / -offerblessing (screenshots, tests): gods' blessings from the start.
            string blessings = CommandLine.GetValue("-blessings");
            if (!string.IsNullOrEmpty(blessings))
                foreach (var name in blessings.Split(','))
                    if (Enum.TryParse(name.Trim(), true, out PerkId perk))
                        foreach (var sp in Sim.Players) sp.AddPerk(perk);
            if (CommandLine.Has("-offerblessing"))
                foreach (var sp in Sim.Players) sp.StockedChoices.Enqueue(ChoiceKind.Blessing);
            foreach (var sp in Sim.Players) Sim.SyncGear(sp); // the gear everyone set out with (and any loaners)
            // Someone carries the six lords' memories: the heart can be reached this run (-heartopen for screenshots).
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                if ((GameSession.LoadoutFor(clientId).memories & Lore.AllMemories) == Lore.AllMemories) Sim.HeartOpen = true;
            if (CommandLine.Has("-heartopen")) Sim.HeartOpen = true;
            Started.Value = true;
            _startTime = Time.time;
            Debug.Log($"[RunDirector] Run started with {Sim.Players.Count} player(s)");
        }

        private readonly Dictionary<string, int> _slotByKey = new();

        private void SpawnPlayer(ulong clientId, SimPlayer simPlayer, Loadout loadout, int slot)
        {
            if (!string.IsNullOrEmpty(loadout.playerKey)) _slotByKey[loadout.playerKey] = slot;
            var instance = Instantiate(playerPrefab, simPlayer.Position, Quaternion.identity);
            var netPlayer = instance.GetComponent<NetPlayer>();
            netPlayer.Slot.Value = slot;
            netPlayer.Class.Value = simPlayer.ClassId;
            netPlayer.Skin.Value = (byte)(CommandLine.Has("-skin") ? (int)CommandLine.GetFloat("-skin", 0f) : loadout.skin); // -skin <n>: screenshots of a skin
            netPlayer.Weapon.Value = (byte)simPlayer.WeaponKind;
            netPlayer.Title.Value = loadout.title ?? "";
            netPlayer.Hp.Value = simPlayer.Hp;
            netPlayer.MaxHp.Value = simPlayer.MaxHp;
            netPlayer.Downed.Value = simPlayer.Downed;
            netPlayer.MoveSpeed.Value = simPlayer.Stats.Get(StatType.MoveSpeed);
            instance.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        }

        /// <summary>A client finished syncing the game scene after the run began: a late joiner or someone coming back.</summary>
        private void OnSceneEvent(SceneEvent e)
        {
            if (e.SceneEventType != SceneEventType.SynchronizeComplete || !Started.Value || Sim == null) return;
            if (e.ClientId == NetworkManager.LocalClientId || Sim.GetPlayer(e.ClientId) != null) return;
            if (Phase.Value != RunPhase.Playing) return;

            var loadout = GameSession.LoadoutFor(e.ClientId);
            var simPlayer = Sim.JoinMidRun(e.ClientId, loadout, out bool returning);
            int slot = returning && _slotByKey.TryGetValue(loadout.playerKey, out int old) && !SlotTaken(old) ? old : FreeSlot();
            SpawnPlayer(e.ClientId, simPlayer, loadout, slot);
            Sim.SyncGear(simPlayer); // their bag, for a returning player
            Sim.Events.Add(new SimEvent(returning ? SimEventType.PlayerRejoined : SimEventType.PlayerJoined) { ClientId = e.ClientId, Pos = simPlayer.Position });
            Debug.Log($"[RunDirector] {(returning ? "Rejoined" : "Joined mid-run")}: client {e.ClientId} as P{slot + 1}");
        }

        private static bool SlotTaken(int slot)
        {
            foreach (var p in NetPlayer.All) if (p.Slot.Value == slot) return true;
            return false;
        }

        private static int FreeSlot()
        {
            for (int s = 0; s < GameSession.MaxPlayers; s++) if (!SlotTaken(s)) return s;
            return 0;
        }

        /// <summary>The host's pick in the camp (automated runs: -difficulty easy|normal|hard|nightmare).</summary>
        private static Difficulty ChosenDifficulty()
        {
            string flag = CommandLine.GetValue("-difficulty");
            if (flag != null && Enum.TryParse(flag, true, out Difficulty d)) return d;
            return ProfileStore.Current.difficulty;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (Sim == null || Sim.GetPlayer(clientId) == null) return;
            var p = Sim.GetPlayer(clientId);
            Sim.Events.Add(new SimEvent(SimEventType.PlayerLeft) { ClientId = clientId, Pos = p.Position });
            Sim.DetachPlayer(clientId, GameSession.LoadoutFor(clientId).playerKey);
        }

        [Rpc(SendTo.Server)]
        public void ChoosePerkRpc(int index, RpcParams rpcParams = default)
        {
            Sim?.ChoosePerk(rpcParams.Receive.SenderClientId, index);
        }

        // Bag actions (the simulation only allows them between fights).
        [Rpc(SendTo.Server)]
        public void EquipRpc(int bagIndex, RpcParams rpcParams = default) => Sim?.EquipFromBag(rpcParams.Receive.SenderClientId, bagIndex);

        [Rpc(SendTo.Server)]
        public void UnequipRpc(int slot, RpcParams rpcParams = default) => Sim?.UnequipSlot(rpcParams.Receive.SenderClientId, (EquipSlot)slot);

        [Rpc(SendTo.Server)]
        public void GiveRpc(int bagIndex, ulong toClientId, RpcParams rpcParams = default) => Sim?.GiveItem(rpcParams.Receive.SenderClientId, bagIndex, toClientId);

        [Rpc(SendTo.Server)]
        public void SalvageRpc(int bagIndex, RpcParams rpcParams = default) => Sim?.SalvageItem(rpcParams.Receive.SenderClientId, bagIndex);

        // ------------------------------------------------------------------ Host: tick

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            if (!Started.Value)
            {
                if (Time.time - _spawnTime > StartFallbackSeconds) StartRun();
                return;
            }

            // A background browser tab runs at a crawl: hold the run for everyone instead of slow motion.
            bool away = HostIsAway();
            SetIfChanged(HostAway, away);
            if (away)
            {
                _accumulator = 0f;
                return;
            }

            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, TickDt * 4);
            while (_accumulator >= TickDt)
            {
                _accumulator -= TickDt;
                Tick();
            }
        }

        /// <summary>Background tab, or the test window -awayat &lt;s&gt; -awayfor &lt;s&gt; (seconds after the run starts).</summary>
        private bool HostIsAway()
        {
            if (BrowserBridge.PageHidden) return true;
            if (!CommandLine.Has("-awayat")) return false;
            float t = Time.time - _startTime - CommandLine.GetFloat("-awayat", 0f);
            return t >= 0f && t < CommandLine.GetFloat("-awayfor", 8f);
        }

        private void Tick()
        {
            // Downed players too: the owner keeps moving until the Downed flag reaches it (latency, dodge travel),
            // so the body and its revive ring can settle away from where the sim knocked it down.
            foreach (var p in NetPlayer.All)
                Sim.SetPlayerPosition(p.OwnerClientId, p.transform.position);

            // -killbossat <s> (screenshots): from then on the lords (and the Lich's vessel) fall as soon as they stand.
            float killAt = CommandLine.GetFloat("-killbossat", -1f);
            if (killAt > 0f && Time.time - _startTime > killAt && !Sim.ExitOpen)
                for (int i = 0; i < Sim.Enemies.Count; i++)
                    if (!Sim.Enemies[i].Dead && (Sim.Enemies[i].Def.Boss || Sim.Enemies[i].Type == EnemyType.SoulVessel))
                        Sim.DamageEnemy(i, 1e7f, null, HitFlags.DamageOverTime, Vector2.zero, 0f);

            Sim.Step(TickDt);
            _tick++;

            MirrorState();
            FlushEvents();
            if (_tick % SnapshotEveryTicks == 0) SendSnapshot();
        }

        private void MirrorState()
        {
            foreach (var p in NetPlayer.All)
            {
                var sp = Sim.GetPlayer(p.OwnerClientId);
                if (sp == null) continue;
                SetIfChanged(p.Hp, Mathf.Min(Mathf.Ceil(sp.Hp), Mathf.Round(sp.MaxHp)));
                SetIfChanged(p.MaxHp, Mathf.Round(sp.MaxHp));
                SetIfChanged(p.Shield, Mathf.Ceil(sp.Shield));
                SetIfChanged(p.Downed, sp.Downed);
                if (sp.Downed) SetIfChanged(p.Body, sp.BodyPosition);
                SetIfChanged(p.ReviveProgress, Quantize(sp.ReviveProgress));
                SetIfChanged(p.MoveSpeed, sp.Stats.Get(StatType.MoveSpeed));
                SetIfChanged(p.Skill1Cooldown, Mathf.Max(0f, Quantize(sp.Skill1Cooldown)));
                SetIfChanged(p.Skill2Cooldown, Mathf.Max(0f, Quantize(sp.Skill2Cooldown)));
                SetIfChanged(p.DodgeCooldown, Mathf.Max(0f, Quantize(sp.DodgeCooldown)));
                SetIfChanged(p.Valor, sp.ValorTimer > 0f);
                SetIfChanged(p.Picks, sp.PicksLeft);
                SetIfChanged(p.Legendary, sp.AnyLegendaryEquipped);
                SetIfChanged(p.Weapon, (byte)sp.WeaponKind);
                SetIfChanged(p.Caged, sp.CagedBy != 0);
                SetIfChanged(p.Curses, RewardCatalog.PackCurses(sp.CurseStacks));
                SetIfChanged(p.UsedPedestals, Sim.PedestalsUsedBy(sp));
                if (_tick % (int)TickRate == 0) SetIfChanged(p.Ping, PingOf(p.OwnerClientId));
            }

            SetIfChanged(TeamLevel, Sim.TeamLevel);
            SetIfChanged(XpFraction, Quantize(Sim.TeamXp / Sim.XpToNext));
            SetIfChanged(RoomIndex, Sim.DeepestIndex);
            SetIfChanged(MapSeed, Sim.MapSeed);
            SetIfChanged(Area, Sim.Area);
            ulong route = 0;
            for (int i = 0; i < Sim.Route.Count && i < 16; i++) route |= (ulong)Sim.Route[i] << (i * 4);
            SetIfChanged(RouteBits, route);
            SetIfChanged(Ways, (int)Sim.NextWays[0] | (int)Sim.NextWays[1] << 4);
            SetIfChanged(OmenId, (int)Sim.Omen);
            SetIfChanged(HeartOpen, Sim.HeartOpen);
            int visited = 0, cleared = 0, fighting = 0, unlocked = 0;
            float puzzle = 0f;
            float puzzleTime = 0f;
            ulong waves = 0;
            foreach (var r in Sim.RoomStates)
            {
                if (r.Visited) visited |= 1 << r.Id;
                if (r.Cleared) cleared |= 1 << r.Id;
                if (r.Fighting) fighting |= 1 << r.Id;
                if (r.Unlocked) unlocked |= 1 << r.Id;
                if (r.Fighting && r.Objective == RoomObjective.Crystal)
                {
                    puzzle = Quantize(Sim.CrystalHealth(r));
                    puzzleTime = Mathf.Ceil(Mathf.Max(0f, r.Target - r.Progress));
                }
                if (r.Fighting && r.Objective == RoomObjective.Plates) puzzle = r.Progress;
                if (r.Fighting && r.Objective == RoomObjective.Twin) puzzle = Quantize(Mathf.Clamp01(r.Progress / Mathf.Max(0.1f, r.Target)));
                if (r.Fighting && r.Objective == RoomObjective.Thief) puzzleTime = Mathf.Ceil(Mathf.Max(0f, r.Target - r.Progress));
                waves |= (ulong)Mathf.Min(r.Wave, 15) << (r.Id * 4);
            }
            SetIfChanged(VisitedRooms, visited);
            SetIfChanged(ClearedRooms, cleared);
            SetIfChanged(FightingRooms, fighting);
            SetIfChanged(RoomWaves, waves);
            SetIfChanged(BossDoorOpen, Sim.BossDoorOpen);
            SetIfChanged(UnlockedRooms, unlocked);
            SetIfChanged(PuzzleValue, puzzle);
            SetIfChanged(PuzzleTime, puzzleTime);
            SetIfChanged(Keys, Sim.Keys);
            SetIfChanged(MapRevealed, Sim.MapRevealed);
            SetIfChanged(ExitOpen, Sim.ExitOpen);
            SetIfChanged(GatherHold, Quantize(Sim.GatherHold));
            SetIfChanged(BonusShards, Sim.BonusShards);
            // The dark hall: who carries the lantern (client + 1), or where it lies (clients follow the carrier themselves).
            var dark = Sim.DarkRoom;
            SetIfChanged(LanternHolder, dark?.LanternBy ?? 0ul);
            if (dark != null && dark.LanternBy == 0) SetIfChanged(LanternPos, dark.LanternPos);
            SetIfChanged(Paused, Sim.AwaitingChoices);
            SetIfChanged(Elapsed, Mathf.Floor(Sim.Elapsed));
            SetIfChanged(Phase, Sim.Phase);
            SetIfChanged(BossHealth, BossHealthFraction());
            SetIfChanged(Kills, Sim.Kills);
            SetIfChanged(BossesKilled, Sim.BossesKilled);
            SetIfChanged(RoomsCleared, Sim.RoomsCleared);
            SetIfChanged(BossType, Sim.AreaBoss);
            SetIfChanged(Trial, Sim.ActiveTrial);
            SetIfChanged(TrialTimer, Mathf.Ceil(Mathf.Max(0f, Sim.TrialTimer)));
            SetIfChanged(TrialProgress, Sim.TrialDone | Sim.TrialTotal << 8);
            SetIfChanged(CollapseStage, Sim.CollapseStage);
            if (Sim.SplitsVersion != _splitsVersion)
            {
                _splitsVersion = Sim.SplitsVersion;
                RunSplits.Value = new FixedString512Bytes(Sim.SplitsText());
            }
        }

        /// <summary>Round trip to a player in milliseconds (0 for the host's own player and in solo).</summary>
        private ushort PingOf(ulong clientId)
        {
            if (clientId == NetworkManager.LocalClientId) return 0;
            try
            {
                return (ushort)Mathf.Min(NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId), ushort.MaxValue);
            }
            catch (Exception)
            {
                return 0; // a client that is just leaving
            }
        }

        private float BossHealthFraction()
        {
            if (!Sim.RoomStates[Sim.Map.Boss.Id].Fighting) return -1f;
            for (int i = 0; i < Sim.Enemies.Count; i++)
            {
                ref var e = ref Sim.Enemies[i];
                if (e.Def.Boss && !e.Dead) return Quantize(e.Health / e.MaxHealth);
            }
            return -1f;
        }

        private static float Quantize(float v) => Mathf.Round(v * 20f) / 20f;

        private static void SetIfChanged<T>(NetworkVariable<T> variable, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(variable.Value, value)) variable.Value = value;
        }

        private void FlushEvents()
        {
            var events = Sim.Events;
            if (events.Count == 0) return;

            int i = 0;
            while (i < events.Count)
            {
                var writer = new FastBufferWriter(EventChunkBytes + 96, Allocator.Temp);
                try
                {
                    while (i < events.Count && writer.Length < EventChunkBytes)
                        EventCodec.Write(writer, events[i++]);
                    // Includes the host's own client, which is delivered locally.
                    NetworkManager.CustomMessagingManager.SendNamedMessageToAll(NetMessageNames.Events, writer, NetworkDelivery.ReliableSequenced);
                }
                finally
                {
                    writer.Dispose();
                }
            }
            events.Clear();
        }

        private void SendSnapshot()
        {
            _remoteClients.Clear();
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (id != NetworkManager.LocalClientId) _remoteClients.Add(id);
            if (_remoteClients.Count == 0) return;

            var enemies = Sim.Enemies;
            int sent = 0;
            do
            {
                int count = Mathf.Min(SnapshotCodec.EnemiesPerChunk, enemies.Count - sent);
                var writer = new FastBufferWriter(SnapshotCodec.EnemiesPerChunk * SnapshotCodec.BytesPerEnemy + 16, Allocator.Temp);
                try
                {
                    SnapshotCodec.WriteChunk(writer, enemies, sent, count);
                    NetworkManager.CustomMessagingManager.SendNamedMessage(NetMessageNames.Snapshot, _remoteClients, writer, NetworkDelivery.UnreliableSequenced);
                }
                finally
                {
                    writer.Dispose();
                }
                sent += count;
            } while (sent < enemies.Count);
        }

        // ------------------------------------------------------------------ All peers: receive

        private void OnEventsMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            while (reader.Position < reader.Length)
            {
                var e = EventCodec.Read(reader);
                if (e.Type == SimEventType.EnemyDied) ClientEnemies.Remove((ushort)e.Id);
                try
                {
                    EventReceived?.Invoke(e);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void OnSnapshotMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            SnapshotCodec.ReadChunk(reader, _snapshotBuffer);
            float now = Time.time;
            LastSnapshotTime = now;
            foreach (var state in _snapshotBuffer) ClientEnemies[state.Id] = (state, now);
        }

        /// <summary>Client only: when the last enemy snapshot arrived (the host sends them even with no enemies).</summary>
        public float LastSnapshotTime { get; private set; }

        /// <summary>Client only: the host has gone quiet mid-run (frozen tab, network hiccup).</summary>
        public bool HostStalled => !IsServer && IsRunning && LastSnapshotTime > 0f && Time.time - LastSnapshotTime > 1.5f;

        /// <summary>Nobody should act: the host is away or not answering.</summary>
        public bool Halted => HostAway.Value || HostStalled;
    }
}
