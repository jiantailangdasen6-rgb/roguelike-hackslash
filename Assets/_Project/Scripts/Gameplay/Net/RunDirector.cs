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
        public readonly NetworkVariable<int> RoomIndex = new();
        public readonly NetworkVariable<RoomObjective> Objective = new();
        public readonly NetworkVariable<int> Wave = new();
        public readonly NetworkVariable<int> WaveCount = new();
        public readonly NetworkVariable<float> Progress = new();
        public readonly NetworkVariable<float> Target = new();
        public readonly NetworkVariable<bool> RoomCleared = new();
        /// <summary>Exits of the cleared room (<see cref="Door.Pack"/>); positions come from <see cref="RunSimulation.DoorPosition"/>.</summary>
        public readonly NetworkVariable<int> DoorsPacked = new();
        public readonly NetworkVariable<int> HoldDoor = new(-1);
        public readonly NetworkVariable<float> ExitHold = new();
        public readonly NetworkVariable<bool> RoomTrial = new();
        public readonly NetworkVariable<int> BonusShards = new();
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
        public readonly NetworkVariable<int> LayoutId = new();
        public readonly NetworkVariable<Difficulty> DifficultyLevel = new(Difficulty.Normal);
        public readonly NetworkVariable<EnemyType> BossType = new();

        /// <summary>Host only.</summary>
        public RunSimulation Sim { get; private set; }

        /// <summary>Client only: latest enemy states from snapshots, keyed by enemy id.</summary>
        public readonly Dictionary<ushort, (EnemyRenderState State, float Time)> ClientEnemies = new();

        public bool IsRunning => Started.Value && Phase.Value == RunPhase.Playing;

        /// <summary>Exits currently open (none while the room is being fought).</summary>
        public Door[] Doors => RoomCleared.Value ? Door.Unpack(DoorsPacked.Value) : System.Array.Empty<Door>();

        public static Vector2 DoorPosition(int index, int count) => RunSimulation.DoorPosition(index, count, RunSimulation.DefaultBounds);

        private readonly List<EnemyRenderState> _snapshotBuffer = new();
        private readonly List<ulong> _remoteClients = new();
        private float _accumulator;
        private int _tick;
        private float _spawnTime;
        private float _startTime;

        public override void OnNetworkSpawn()
        {
            Instance = this;
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

        private void StartRun()
        {
            if (Started.Value) return;

            DifficultyLevel.Value = ChosenDifficulty();
            Sim = new RunSimulation((ulong)DateTime.UtcNow.Ticks, null, DifficultyLevel.Value);
            Sim.DropChanceScale = CommandLine.GetFloat("-lootrate", 1f);
            int slot = 0;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                var loadout = GameSession.LoadoutFor(clientId);
                SpawnPlayer(clientId, Sim.AddPlayer(clientId, loadout), loadout, slot++);
            }

            Sim.Start((int)CommandLine.GetFloat("-startroom", 0f));
            foreach (var sp in Sim.Players) Sim.SyncGear(sp); // the gear everyone set out with (and any loaners)
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
            netPlayer.Skin.Value = (byte)loadout.skin;
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
                SetIfChanged(p.ReviveProgress, Quantize(sp.ReviveProgress));
                SetIfChanged(p.MoveSpeed, sp.Stats.Get(StatType.MoveSpeed));
                SetIfChanged(p.Skill1Cooldown, Mathf.Max(0f, Quantize(sp.Skill1Cooldown)));
                SetIfChanged(p.Skill2Cooldown, Mathf.Max(0f, Quantize(sp.Skill2Cooldown)));
                SetIfChanged(p.DodgeCooldown, Mathf.Max(0f, Quantize(sp.DodgeCooldown)));
                SetIfChanged(p.Valor, sp.ValorTimer > 0f);
                SetIfChanged(p.Picks, sp.PicksLeft);
                SetIfChanged(p.Legendary, sp.AnyLegendaryEquipped);
            }

            var room = Sim.Room;
            SetIfChanged(TeamLevel, Sim.TeamLevel);
            SetIfChanged(XpFraction, Quantize(Sim.TeamXp / Sim.XpToNext));
            SetIfChanged(RoomIndex, room.Index);
            SetIfChanged(Objective, room.Objective);
            SetIfChanged(Wave, room.Wave);
            SetIfChanged(WaveCount, room.WaveCount);
            SetIfChanged(Progress, room.Objective == RoomObjective.Survive ? Mathf.Floor(room.Progress) : room.Progress);
            SetIfChanged(Target, room.Target);
            SetIfChanged(RoomCleared, room.Cleared);
            SetIfChanged(DoorsPacked, room.Cleared ? Door.Pack(room.Doors) : 0);
            SetIfChanged(HoldDoor, room.HoldDoor);
            SetIfChanged(ExitHold, Quantize(room.ExitHold));
            SetIfChanged(RoomTrial, room.Trial);
            SetIfChanged(BonusShards, Sim.BonusShards);
            SetIfChanged(Paused, Sim.AwaitingChoices);
            SetIfChanged(Elapsed, Mathf.Floor(Sim.Elapsed));
            SetIfChanged(Phase, Sim.Phase);
            SetIfChanged(BossHealth, BossHealthFraction());
            SetIfChanged(Kills, Sim.Kills);
            SetIfChanged(BossesKilled, Sim.BossesKilled);
            SetIfChanged(RoomsCleared, Sim.RoomsCleared);
            SetIfChanged(LayoutId, Sim.Layout.Id);
            SetIfChanged(BossType, Sim.AreaBoss);
        }

        private float BossHealthFraction()
        {
            if (Sim.Room.Objective != RoomObjective.Boss || Sim.Room.Cleared) return -1f;
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
