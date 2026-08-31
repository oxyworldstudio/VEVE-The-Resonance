using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using VEVE;

namespace VEVE.Net
{
    /// <summary>
    /// Pure ownership matrix for avatar input authority. Offline or not-yet-
    /// spawned objects MUST keep local input: single-player is structurally
    /// identical to a hosted game in which nobody joined yet.
    /// </summary>
    public static class NetAvatarRules
    {
        public static bool ShouldAcceptLocalInput(bool sessionOnline, bool spawned, bool isLocalPlayer)
        {
            if (!sessionOnline) return true;
            if (!spawned) return true;
            return isLocalPlayer;
        }

        public static bool ShouldDriveTransform(bool sessionOnline, bool spawned, bool isRemoteClient)
        {
            if (!sessionOnline) return false;
            return spawned && isRemoteClient;
        }
    }

    /// <summary>
    /// Attached to the player rig: ensures a NetworkObject, mounts NGO's
    /// NetworkTransform for position sync, and flips PlayerController.LocalInputEnabled
    /// per client authority. AI-driven scene elements remain host-authoritative;
    /// remote NPC replication is the next wire segment (C4e) and never affects local play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkedPlayerAvatar : NetworkBehaviour
    {
        private PlayerController controller;
        private NetworkTransform netTransform;

        public bool LocalInputActive => controller != null && controller.LocalInputEnabled;
        public bool NetworkedSync { get; private set; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            EnsureController();
            EnsureNetworkTransform();
            NetIsOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;
            controller.LocalInputEnabled =
                NetAvatarRules.ShouldAcceptLocalInput(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening, true, IsLocalPlayer);
            NetworkedSync = controller.LocalInputEnabled == false;
        }

        public override void OnNetworkDespawn()
        {
            if (controller != null) controller.LocalInputEnabled = true; // degrade safe on shutdown
            NetworkedSync = false;
            base.OnNetworkDespawn();
        }

        private void Awake()
        {
            EnsureController();
        }

        private void EnsureController()
        {
            if (controller != null) return;
            controller = GetComponent<PlayerController>();
            if (controller == null) controller = GetComponentInChildren<PlayerController>();
        }

        private void EnsureNetworkTransform()
        {
            if (netTransform != null) return;
            netTransform = GetComponent<NetworkTransform>();
            if (netTransform == null) netTransform = gameObject.AddComponent<NetworkTransform>();
        }

        public bool NetIsOnline { get; private set; }
    }
}
