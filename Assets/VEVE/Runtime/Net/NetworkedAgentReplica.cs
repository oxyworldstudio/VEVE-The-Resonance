using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace VEVE.Net
{
    /// <summary>
    /// Pure replica rules: NPC brains (EnemyAwareness etc.) run on the server/host
    /// (or offline) exactly once; remote clients disable the brain and only render
    /// the replicated transform stream. Dual simulation is structurally impossible.
    /// </summary>
    public static class NetAgentReplicaRules
    {
        public static bool ShouldSimulate(bool sessionOnline, bool hasAuthority)
        {
            if (!sessionOnline) return true;
            return hasAuthority;
        }

        public static bool ShouldReplicateTransform(bool sessionOnline, bool spawned, bool isRemoteClient)
        {
            if (!sessionOnline) return false;
            return spawned && isRemoteClient;
        }
    }

    /// <summary>
    /// C4e remote AI visibility: keeps EnemyAwareness/AI brain simulation
    /// exclusively where authority lives, and disables local brain components
    /// on pure clients, leaving NetworkTransform (authored scene-side next to
    /// the NetworkObject) to carry movement. Offline: brain on, no network
    /// components active - single player is untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkedAgentReplica : NetworkBehaviour
    {
        private EnemyAwareness brain;

        /// <summary>Host-authoritative and simulating right now? (for HUD/debug)</summary>
        public bool SimulatingOwnAI { get; private set; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            bool online = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            SimulatingOwnAI = NetAgentReplicaRules.ShouldSimulate(online, IsServer || IsHost);
            if (brain == null) brain = GetComponent<EnemyAwareness>();
            if (brain != null) brain.enabled = SimulatingOwnAI;

            if (!SimulatingOwnAI)
            {
                // Remote-only: freeze any host-authored nav driving done via
                // transform writes (awareness is already off); ensure NT exists
                // offline-first (authored) and is active to pull the stream.
                EnsureNetworkTransform();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (brain != null) brain.enabled = true; // restore safe default offline
            SimulatingOwnAI = false;
            base.OnNetworkDespawn();
        }

        private void Awake()
        {
            if (brain == null) brain = GetComponent<EnemyAwareness>();
        }

        private void EnsureNetworkTransform()
        {
            if (GetComponent<NetworkTransform>() == null)
                gameObject.AddComponent<NetworkTransform>();
        }

        public bool HasTransformStream => GetComponent<NetworkTransform>() != null;
    }
}
