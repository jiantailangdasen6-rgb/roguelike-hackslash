using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Game.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>
    /// Starting/joining/leaving sessions. Three ways to play:
    /// solo (SinglePlayerTransport, works in browsers), LAN by IP (desktop only), and online through
    /// Unity Relay with a room code (WebSockets, so browsers and desktops can play together).
    /// The NetworkManager comes from Resources/NetworkManager.prefab.
    /// </summary>
    public static class GameSession
    {
        public const ushort Port = 7777;
        public const int MaxPlayers = 3;
        public const string LobbyScene = "Lobby";
        public const string GameScene = "Game";
        public const string CampScene = "Camp";

        /// <summary>True while the party is in a run (Game scene) rather than in the camp.</summary>
        public static bool InGame { get; private set; }

        /// <summary>Message to show in the lobby after returning (e.g. disconnected).</summary>
        public static string LobbyMessage;

        /// <summary>True after the host turned us away with a reason (full, started, other version).</summary>
        public static bool Refused { get; private set; }

        private static bool _leaving;

        /// <summary>Room code of the current online session (host and clients), or null.</summary>
        public static string JoinCode { get; private set; }

        /// <summary>Browsers can't listen on sockets: no LAN hosting, and every connection must be a WebSocket.</summary>
        public static bool IsBrowser => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>Online play needs this build to be linked to a Unity Cloud project (Relay).</summary>
        public static bool OnlineAvailable => !string.IsNullOrEmpty(Application.cloudProjectId);

        public static NetworkManager Network => EnsureNetworkManager();

        private static readonly Dictionary<ulong, Loadout> Loadouts = new();

        public static NetworkManager EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return NetworkManager.Singleton;

            var prefab = Resources.Load<NetworkManager>("NetworkManager");
            var nm = UnityEngine.Object.Instantiate(prefab);
            nm.name = "NetworkManager";
            nm.SetSingleton();
            nm.ConnectionApprovalCallback = Approve;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            nm.OnTransportFailure += OnTransportFailure;
            return nm;
        }

        /// <summary>The class/mastery/trinkets a player brings. The host uses its own profile directly.</summary>
        public static Loadout LoadoutFor(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && clientId == nm.LocalClientId) return ProfileStore.Current.ToLoadout();
            return Loadouts.TryGetValue(clientId, out var l) ? l : new Loadout();
        }

        // ------------------------------------------------------------------ Transports

        private static UnityTransport Udp(bool webSockets)
        {
            var t = Network.GetComponent<UnityTransport>();
            t.UseWebSockets = webSockets || IsBrowser;
            Network.NetworkConfig.NetworkTransport = t;
            return t;
        }

        private static void Prepare()
        {
            InGame = false;
            JoinCode = null;
            Loadouts.Clear();
            Refused = false;
            _leaving = false;
            var profile = ProfileStore.Current;
            if (string.IsNullOrEmpty(profile.playerKey))
            {
                profile.playerKey = Guid.NewGuid().ToString("N").Substring(0, 16);
                ProfileStore.Save();
            }
            // Put this build's stamp and this device's loadout into the connection request.
            Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(BuildStamp + "\n" + ProfileStore.Current.ToLoadout().ToJson());
        }

        private static string _buildStamp;

        /// <summary>Identifies the builder run this build came from (see PrototypeBuilder.WriteBuildStamp).</summary>
        /// <remarks>-stamp &lt;s&gt; overrides it (automated test of the version-mismatch refusal).</remarks>
        public static string BuildStamp => _buildStamp ??= CommandLine.GetValue("-stamp") ?? Resources.Load<TextAsset>("BuildStamp")?.text.Trim() ?? "dev";

        // ------------------------------------------------------------------ Solo / LAN

        public static bool StartSolo()
        {
            Prepare();
            Network.NetworkConfig.NetworkTransport = Network.GetComponent<SinglePlayerTransport>();
            if (!Network.StartHost()) return false;
            EnterCamp();
            return true;
        }

        public static bool StartLanHost()
        {
            Prepare();
            // -localonly (automated tests) avoids opening a LAN port and the firewall prompt that comes with it.
            string listen = CommandLine.Has("-localonly") ? "127.0.0.1" : "0.0.0.0";
            Udp(false).SetConnectionData(listen, Port, listen);
            if (!Network.StartHost()) return false;
            EnterCamp();
            return true;
        }

        public static bool StartLanClient(string address)
        {
            Prepare();
            Udp(false).SetConnectionData(address.Trim(), Port);
            return Network.StartClient();
        }

        // ------------------------------------------------------------------ Online (Relay)

        private static async Task EnsureServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                // Separate anonymous identities per -profile, so several instances on one PC are different players.
                string profile = new string((CommandLine.GetValue("-profile") ?? "default").Where(char.IsLetterOrDigit).Take(30).ToArray());
                options.SetProfile(string.IsNullOrEmpty(profile) ? "default" : profile);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>Creates a Relay allocation, starts hosting over secure WebSockets and returns the room code.</summary>
        public static async Task<string> StartOnlineHostAsync()
        {
            if (!OnlineAvailable) throw new InvalidOperationException("オンライン協力はまだ準備中です（Unity Cloud の設定待ち）");
            Prepare();
            await EnsureServicesAsync();
            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Udp(true).SetRelayServerData(allocation.ToRelayServerData("wss"));
            if (!Network.StartHost()) throw new InvalidOperationException("部屋を作れませんでした");
            JoinCode = code;
            EnterCamp();
            return code;
        }

        public static async Task StartOnlineClientAsync(string code)
        {
            if (!OnlineAvailable) throw new InvalidOperationException("オンライン協力はまだ準備中です（Unity Cloud の設定待ち）");
            Prepare();
            code = code.Trim().ToUpperInvariant();
            await EnsureServicesAsync();
            var join = await RelayService.Instance.JoinAllocationAsync(code);
            Udp(true).SetRelayServerData(join.ToRelayServerData("wss"));
            if (!Network.StartClient()) throw new InvalidOperationException("接続を開始できませんでした");
            JoinCode = code;
        }

        // ------------------------------------------------------------------ Flow

        /// <summary>Host only: take the party to the camp (after starting a session, and after each run).</summary>
        private static void EnterCamp()
        {
            InGame = false;
            Network.SceneManager.LoadScene(CampScene, LoadSceneMode.Single);
        }

        /// <summary>Host only: everyone sets out from the camp into a run.</summary>
        public static void StartGame()
        {
            if (!Network.IsServer || InGame) return;
            InGame = true;
            Network.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
        }

        /// <summary>Host only: after a run (or abandoning it) the party goes back to the camp together.</summary>
        public static void ReturnToCamp()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !InGame) return;
            BagView.BankNow();
            EnterCamp();
        }

        /// <summary>A player changed class, mastery or trinkets in the camp.</summary>
        public static void SetLoadout(ulong clientId, Loadout loadout) => Loadouts[clientId] = loadout;

        /// <summary>Room code of the last online session this device was in (offered for rejoining).</summary>
        public static string LastJoinCode { get; private set; }

        public static void ReturnToLobby(string message = null)
        {
            BagView.BankNow(); // gear found this run goes home first, however the run ends
            if (JoinCode != null) LastJoinCode = JoinCode;
            InGame = false;
            JoinCode = null;
            LobbyMessage = message;
            var nm = NetworkManager.Singleton;
            // Our own shutdown also raises the disconnect callback (a frame later): ignore it until the next session
            // starts, or it would reload the camp a second time and look like a refusal.
            _leaving = true;
            if (nm != null && (nm.IsListening || nm.IsClient)) nm.Shutdown();
            SceneManager.LoadScene(LobbyScene);
        }

        private static void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = NetworkManager.Singleton;
            bool isHostItself = request.ClientNetworkId == nm.LocalClientId;
            bool full = nm.ConnectedClientsIds.Count >= MaxPlayers;
            string payload = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : "";
            int split = payload.IndexOf('\n');
            string stamp = split >= 0 ? payload.Substring(0, split) : "";
            // A different build can't play along (its copy of the game scene doesn't match ours).
            bool sameBuild = isHostItself || stamp == BuildStamp;
            // Joining a run in progress is fine (RunDirector adds them once their scene is in sync), but not after it ended.
            var director = RunDirector.Instance;
            bool closed = InGame && director != null && director.Started.Value && director.Phase.Value != RunPhase.Playing;
            response.Approved = isHostItself || (!closed && !full && sameBuild);
            response.CreatePlayerObject = false; // RunDirector spawns players when the run starts
            if (response.Approved && split >= 0)
                Loadouts[request.ClientNetworkId] = Loadout.FromJson(payload.Substring(split + 1));
            if (!response.Approved)
                response.Reason = !sameBuild ? "ゲームのバージョンが部屋と違います。全員ページを再読み込み（F5）してから、部屋を作り直してください"
                    : closed ? "仲間は冒険を終えたところです。拠点に戻ってから、もう一度参加してください" : "満員です（最大3人）";
        }

        private static void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || _leaving) return;
            // We are a client and lost the host (or were refused).
            Refused = !string.IsNullOrEmpty(nm.DisconnectReason);
            string reason = Refused ? nm.DisconnectReason : "ホストとの接続が切れました";
            ReturnToLobby(reason);
        }

        /// <summary>
        /// The connection itself broke (Relay lost, transport queues overflowed). Netcode shuts down on its own; without
        /// this the host was left on a frozen game screen.
        /// </summary>
        private static void OnTransportFailure()
        {
            if (_leaving) return;
            Debug.LogWarning("[GameSession] Transport failure: back to the title");
            ReturnToLobby("通信が切れました（回線が不安定だったようです）。もう一度部屋を作って集まってください");
        }

        /// <summary>Best guess of this device's LAN address, for showing to friends who want to join.</summary>
        public static string LocalAddress()
        {
            if (IsBrowser) return "（ブラウザでは不明）";
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string s = addr.Address.ToString();
                        if (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("172.")) return s;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSession] Could not read network interfaces: {ex.Message}");
            }
            return "不明";
        }
    }
}
