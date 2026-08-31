using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using VEVE.Content;

namespace VEVE.Net
{
    public enum NetSessionMode { Offline = 0, Host = 1, Client = 2 }

    /// <summary>Pure join/authority decisions (NGO-free, unit-testable).</summary>
    public static class NetFlowRules
    {
        public const int MaxClients = 3; // 1 host + 3 clients session cap

        public static bool CanAcceptClient(NetSessionMode role, int alreadyConnected, bool sessionLive)
        {
            if (role != NetSessionMode.Host || !sessionLive) return false;
            return alreadyConnected >= 0 && alreadyConnected < MaxClients;
        }

        /// <summary>Host and offline run the authoritative loop; pure clients never do.</summary>
        public static bool ShouldRunAuthoritativeLoop(NetSessionMode mode)
        {
            return mode != NetSessionMode.Client;
        }

        public static bool CanStartNewSession(NetSessionMode current) => current == NetSessionMode.Offline;
    }

    /// <summary>
    /// Session bootstrap: builds NetworkManager/transport at runtime (no scene
    /// prefabs required), hands CampaignLoopController its authority flag, and
    /// registers the adapter's journal/mirror so late joiners replay from the
    /// live host session.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkGameFlow : MonoBehaviour
    {
        [SerializeField] private ushort defaultPort = 7777;

        public NetSessionMode Mode { get; private set; } = NetSessionMode.Offline;
        public NetworkManager Manager { get; private set; }
        public MissionTransportAdapter Adapter { get; private set; }
        public MissionCommandJournal Journal => Adapter != null ? Adapter.Journal : _journal;
        public NetMissionMirror Mirror => Adapter != null ? Adapter.Mirror : _mirror;

        private MissionCommandJournal _journal = new MissionCommandJournal();
        private NetMissionMirror _mirror = new NetMissionMirror();

        private static NetworkGameFlow _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool StartHostSession(string listenAddress = "0.0.0.0")
        {
            if (!NetFlowRules.CanStartNewSession(Mode)) return false;
            EnsureInfra(listenAddress);
            if (!Manager.StartHost()) return false;
            Mode = NetSessionMode.Host;
            ApplyAuthority();
            SpawnAdapterIfNeeded();
            return true;
        }

        public bool JoinSession(string serverAddress, ushort port)
        {
            if (!NetFlowRules.CanStartNewSession(Mode)) return false;
            EnsureInfra(serverAddress, port);
            if (!Manager.StartClient()) return false;
            Mode = NetSessionMode.Client;
            ApplyAuthority();
            return true;
        }

        public void Shutdown()
        {
            if (Manager != null) Manager.Shutdown();
            Mode = NetSessionMode.Offline;
            ApplyAuthority();
        }

        private void EnsureInfra(string address, ushort? port = null)
        {
            Manager = FindObjectOfType<NetworkManager>();
            if (Manager == null)
            {
                GameObject go = new GameObject("NetworkManager");
                DontDestroyOnLoad(go);
                Manager = go.AddComponent<NetworkManager>();
            }

            UnityTransport transport = Manager.GetComponent<UnityTransport>();
            if (transport == null) transport = Manager.gameObject.AddComponent<UnityTransport>();
            transport.SetConnectionData(address, port ?? defaultPort);
        }

        private void ApplyAuthority()
        {
            CampaignLoopController loop = FindFirstObjectByType<CampaignLoopController>();
            if (loop != null) loop.Authoritative = NetFlowRules.ShouldRunAuthoritativeLoop(Mode);
        }

        private void SpawnAdapterIfNeeded()
        {
            Adapter = FindFirstObjectByType<MissionTransportAdapter>();
            if (Adapter == null)
            {
                GameObject go = new GameObject("MissionNetAdapter");
                DontDestroyOnLoad(go);
                go.AddComponent<NetworkObject>();
                Adapter = go.AddComponent<MissionTransportAdapter>();
            }
            Adapter.Attach(Journal, Mirror);
            NetworkObject netObj = Adapter.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned) netObj.Spawn(false);
        }

        /// <summary>Host-side join gate for lobby UI (connected count passed by caller).</summary>
        public bool AcceptClient(int connectedNow)
        {
            return NetFlowRules.CanAcceptClient(Mode, connectedNow, Manager != null && Manager.IsHost);
        }
    }
}
