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

        /// <summary>True once the host has started the run; new connections are refused.</summary>
        public static bool InGame { get; private set; }

        /// <summary>Message to show in the lobby after returning (e.g. disconnected).</summary>
        public static string LobbyMessage;

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
            // Put this device's loadout into the connection request.
            Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ProfileStore.Current.ToLoadout().ToJson());
        }

        // ------------------------------------------------------------------ Solo / LAN

        public static bool StartSolo()
        {
            Prepare();
            Network.NetworkConfig.NetworkTransport = Network.GetComponent<SinglePlayerTransport>();
            if (!Network.StartHost()) return false;
            StartGame();
            return true;
        }

        public static bool StartLanHost()
        {
            Prepare();
            // -localonly (automated tests) avoids opening a LAN port and the firewall prompt that comes with it.
            string listen = CommandLine.Has("-localonly") ? "127.0.0.1" : "0.0.0.0";
            Udp(false).SetConnectionData(listen, Port, listen);
            return Network.StartHost();
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

        /// <summary>Host only: move everyone into the game scene.</summary>
        public static void StartGame()
        {
            if (!Network.IsServer || InGame) return;
            InGame = true;
            Network.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
        }

        public static void ReturnToLobby(string message = null)
        {
            InGame = false;
            JoinCode = null;
            LobbyMessage = message;
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsClient)) nm.Shutdown();
            SceneManager.LoadScene(LobbyScene);
        }

        private static void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = NetworkManager.Singleton;
            bool isHostItself = request.ClientNetworkId == nm.LocalClientId;
            bool full = nm.ConnectedClientsIds.Count >= MaxPlayers;
            response.Approved = isHostItself || (!InGame && !full);
            response.CreatePlayerObject = false; // RunDirector spawns players when the run starts
            if (response.Approved && request.Payload != null && request.Payload.Length > 0)
                Loadouts[request.ClientNetworkId] = Loadout.FromJson(Encoding.UTF8.GetString(request.Payload));
            if (!response.Approved) response.Reason = InGame ? "ゲームはすでに始まっています" : "満員です（最大3人）";
        }

        private static void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer) return;
            // We are a client and lost the host (or were refused).
            string reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "ホストとの接続が切れました" : nm.DisconnectReason;
            ReturnToLobby(reason);
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
