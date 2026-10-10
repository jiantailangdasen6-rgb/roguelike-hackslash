using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>Starting/joining/leaving sessions. The NetworkManager comes from Resources/NetworkManager.prefab.</summary>
    public static class GameSession
    {
        public const ushort Port = 7777;
        public const ushort SoloPort = 7787;
        public const int MaxPlayers = 3;
        public const string LobbyScene = "Lobby";
        public const string GameScene = "Game";

        /// <summary>True once the host has started the run; new connections are refused.</summary>
        public static bool InGame { get; private set; }

        /// <summary>Message to show in the lobby after returning (e.g. disconnected).</summary>
        public static string LobbyMessage;

        public static NetworkManager Network => EnsureNetworkManager();

        /// <summary>Loadouts sent by clients in their connection request (host side).</summary>
        private static readonly Dictionary<ulong, Loadout> Loadouts = new();

        /// <summary>The class/mastery/trinkets a player brings. The host uses its own profile directly.</summary>
        public static Loadout LoadoutFor(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && clientId == nm.LocalClientId) return ProfileStore.Current.ToLoadout();
            return Loadouts.TryGetValue(clientId, out var l) ? l : new Loadout();
        }

        /// <summary>Puts this device's loadout into the connection request.</summary>
        private static void PrepareConnectionData() =>
            Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ProfileStore.Current.ToLoadout().ToJson());

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

        private static UnityTransport Transport => Network.GetComponent<UnityTransport>();

        public static bool StartSolo()
        {
            PrepareConnectionData();
            Transport.SetConnectionData("127.0.0.1", SoloPort, "127.0.0.1");
            if (!Network.StartHost()) return false;
            StartGame();
            return true;
        }

        public static bool StartHost()
        {
            InGame = false;
            // -localonly (automated tests) avoids opening a LAN port and the firewall prompt that comes with it.
            string listen = CommandLine.Has("-localonly") ? "127.0.0.1" : "0.0.0.0";
            PrepareConnectionData();
            Transport.SetConnectionData(listen, Port, listen);
            return Network.StartHost();
        }

        public static bool StartClient(string address)
        {
            InGame = false;
            PrepareConnectionData();
            Transport.SetConnectionData(address.Trim(), Port);
            return Network.StartClient();
        }

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
