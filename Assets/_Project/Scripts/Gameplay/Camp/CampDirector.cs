using System.Collections.Generic;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Scene-placed network object in the Camp scene. The host spawns everyone's avatar by the fire (late joiners
    /// too), keeps each player's loadout up to date as they change class, mastery or trinkets, and sets out for the
    /// labyrinth once the whole party has stood in the gate for a few seconds.
    /// </summary>
    public sealed class CampDirector : NetworkBehaviour
    {
        private const float SpawnFallbackSeconds = 8f;

        public static CampDirector Instance { get; private set; }

        [SerializeField] private NetworkObject playerPrefab;

        /// <summary>The host's difficulty, shown to everyone at the gate.</summary>
        public readonly NetworkVariable<Difficulty> DifficultyLevel = new(Difficulty.Normal);
        /// <summary>The host sets out into this week's labyrinth (Weekly).</summary>
        public readonly NetworkVariable<bool> WeeklyRun = new();
        /// <summary>Seconds the whole party has been standing in the gate (0 while someone is missing).</summary>
        public readonly NetworkVariable<float> DepartProgress = new();
        public readonly NetworkVariable<int> InGate = new();

        private float _spawnTime;
        private bool _spawned;
        private float _autoDepartAt = -1f;
        private float _departProgress;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            ProfileStore.Changed += OnProfileChanged;
            if (!IsServer) return;

            _spawnTime = Time.time;
            DifficultyLevel.Value = ChosenDifficulty();
            WeeklyRun.Value = ProfileStore.Current.weekly;
            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        }

        public override void OnNetworkDespawn()
        {
            ProfileStore.Changed -= OnProfileChanged;
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ Host: avatars

        private void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == GameSession.CampScene) SpawnAll();
        }

        /// <summary>Someone joined while we were already in the camp.</summary>
        private void OnSceneEvent(SceneEvent e)
        {
            if (e.SceneEventType != SceneEventType.SynchronizeComplete || !_spawned) return;
            if (e.ClientId == NetworkManager.LocalClientId || AvatarOf(e.ClientId) != null) return;
            Spawn(e.ClientId, FreeSlot());
        }

        private void SpawnAll()
        {
            if (_spawned) return;
            _spawned = true;
            int slot = 0;
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (AvatarOf(id) == null) Spawn(id, slot++);
            Debug.Log($"[CampDirector] Party of {NetworkManager.ConnectedClientsIds.Count} in the camp");
        }

        private void Spawn(ulong clientId, int slot)
        {
            var loadout = GameSession.LoadoutFor(clientId);
            var instance = Instantiate(playerPrefab, Camp.SpawnPoint(slot), Quaternion.identity);
            var p = instance.GetComponent<NetPlayer>();
            p.Slot.Value = slot;
            Apply(p, loadout);
            instance.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        }

        private static void Apply(NetPlayer p, Loadout loadout)
        {
            p.Class.Value = loadout.classId;
            p.Skin.Value = (byte)loadout.skin;
            p.MoveSpeed.Value = ClassCatalog.Get(loadout.classId).MoveSpeed;
        }

        private static NetPlayer AvatarOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        private static int FreeSlot()
        {
            for (int s = 0; s < GameSession.MaxPlayers; s++)
            {
                bool taken = false;
                foreach (var p in NetPlayer.All) taken |= p.Slot.Value == s;
                if (!taken) return s;
            }
            return 0;
        }

        // ------------------------------------------------------------------ Loadouts

        private void OnProfileChanged()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                DifficultyLevel.Value = ChosenDifficulty();
                WeeklyRun.Value = ProfileStore.Current.weekly;
                var mine = AvatarOf(NetworkManager.LocalClientId);
                if (mine != null) Apply(mine, ProfileStore.Current.ToLoadout());
            }
            else
            {
                UpdateLoadoutRpc(ProfileStore.Current.ToLoadout().ToJson());
            }
        }

        [Rpc(SendTo.Server)]
        private void UpdateLoadoutRpc(string json, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var loadout = Loadout.FromJson(json);
            loadout.playerKey = GameSession.LoadoutFor(sender).playerKey; // keeps the reconnect identity
            GameSession.SetLoadout(sender, loadout);
            var avatar = AvatarOf(sender);
            if (avatar != null) Apply(avatar, loadout);
        }

        public static Difficulty ChosenDifficulty()
        {
            string flag = CommandLine.GetValue("-difficulty");
            if (flag != null && System.Enum.TryParse(flag, true, out Difficulty d)) return d;
            return ProfileStore.Current.difficulty;
        }

        // ------------------------------------------------------------------ Host: setting out

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;
            if (!_spawned)
            {
                if (Time.time - _spawnTime > SpawnFallbackSeconds) SpawnAll();
                return;
            }

            int inGate = 0;
            foreach (var p in NetPlayer.All) if (Camp.InGate(p.transform.position)) inGate++;
            if (InGate.Value != inGate) InGate.Value = inGate;
            bool everyone = NetPlayer.All.Count > 0 && inGate == NetPlayer.All.Count && NetPlayer.All.Count == NetworkManager.ConnectedClientsIds.Count;

            _departProgress = everyone ? _departProgress + Time.deltaTime : 0f;
            if (AutoDepartDue()) _departProgress = Camp.DepartSeconds;
            float shown = Mathf.Min(Mathf.Round(_departProgress * 10f) / 10f, Camp.DepartSeconds);
            if (DepartProgress.Value != shown) DepartProgress.Value = shown;
            if (_departProgress >= Camp.DepartSeconds) GameSession.StartGame();
        }

        /// <summary>
        /// Automated runs (-autosolo / -autohost n / -onlinehost n) set out by themselves once the party is complete,
        /// after -campwait &lt;s&gt; seconds in the camp (for screenshots).
        /// </summary>
        private bool AutoDepartDue()
        {
            int wanted = CommandLine.Has("-autosolo") ? 1
                : CommandLine.Has("-autohost") ? (int)CommandLine.GetFloat("-autohost", 2)
                : CommandLine.Has("-onlinehost") ? (int)CommandLine.GetFloat("-onlinehost", 2) : 0;
            if (wanted <= 0) return false;
            if (NetPlayer.All.Count < Mathf.Clamp(wanted, 1, GameSession.MaxPlayers)) return false;
            if (_autoDepartAt < 0f) _autoDepartAt = Time.time + CommandLine.GetFloat("-campwait", 0.5f);
            return Time.time >= _autoDepartAt;
        }
    }
}
