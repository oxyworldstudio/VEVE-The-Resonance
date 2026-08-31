using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using VEVE;

namespace VEVE.Net
{
    /// <summary>
    /// Pure ownership matrices for avatar/pawn input. Offline or not-yet-spawned
    /// objects MUST keep local input (single player is structurally the host
    /// session nobody joined yet). The scene rig yields to the auto-spawned
    /// per-connection pawn as soon as a session is live, host included: PlayerPrefab
    /// exists for every connection (NGO spawn-on-approve), so authority is
    /// unambiguous and there is never a rig-less host.
    /// </summary>
    public static class NetAvatarRules
    {
        public static bool ShouldAcceptLocalInput(bool sessionOnline, bool spawned, bool isLocalPlayer)
        {
            if (!sessionOnline) return true;
            if (!spawned) return true;
            return isLocalPlayer;
        }

        /// <summary>Scene-started rig input: active offline, dormant once a session grants real pawns.</summary>
        public static bool ShouldOwnSceneRig(bool sessionOnline)
        {
            return !sessionOnline;
        }

        public static bool ShouldDriveTransform(bool sessionOnline, bool spawned, bool isRemoteClient)
        {
            if (!sessionOnline) return false;
            return spawned && isRemoteClient;
        }

        public static bool ShouldEnableLocalCamera(bool controlsInput, bool isSceneRig)
        {
            return controlsInput;
        }
    }

    /// <summary>
    /// Attached to rig and to networked pawns: mounts NetworkTransform, flips
    /// PlayerController input, switches child camera/listener to the local
    /// controller only. The scene rig deactivates itself once a session spawns
    /// dedicated pawns (NGO's connection-approved spawn flow).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkedPlayerAvatar : NetworkBehaviour
    {
        [Tooltip("The authored-in-scene rig (not the pooled pawn): when false this object belongs to a connection.")]
        [SerializeField] private bool sceneRig;

        private PlayerController controller;
        private NetworkTransform netTransform;
        private Camera ownedCamera;
        private AudioListener[] ownedListeners;
        private Weapon[] ownedWeapons;
        private VEVE.UI.ScopeTelemetryBridge[] ownedTelemetry;

        public bool LocalInputActive => controller != null && controller.LocalInputEnabled;
        public bool NetworkedSync { get; private set; }
        public bool NetIsOnline { get; private set; }

        /// <summary>True when this instance represents the player's local view and input.</summary>
        public bool ControlsLocalPlayer { get; private set; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            EnsureController();
            EnsureNetworkTransform();
            NetIsOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            controller.LocalInputEnabled = NetAvatarRules.ShouldAcceptLocalInput(NetIsOnline, true, IsLocalPlayer);

            if (sceneRig && NetIsOnline && !ShouldRemainSceneHostView())
            {
                controller.LocalInputEnabled = false;
            }

            NetworkedSync = !controller.LocalInputEnabled;
            ControlsLocalPlayer = controller.LocalInputEnabled;
            ApplyLocalPresentation(ControlsLocalPlayer);
        }

        public override void OnNetworkDespawn()
        {
            if (controller != null) controller.LocalInputEnabled = true; // degrade safe after shutdown
            ApplyLocalPresentation(true);
            NetworkedSync = false;
            ControlsLocalPlayer = true;
            base.OnNetworkDespawn();
        }

        /// <summary>Host scene object is inert only for non-local players; the host still routes its own camera via NGO's local player spawn, and the original scene rig is fully restored offline.</summary>
        private bool ShouldRemainSceneHostView()
        {
            // In-session: a connection-owned pawn exists for the host too (PlayerPrefab),
            // so the scene rig must never double-control. If no pawn exists yet (pre-
            // spawn frame) the 3-arg matrix already returned true via !spawned.
            return false;
        }

        private void Awake()
        {
            EnsureController();
            CachePresentation();
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

        private void CachePresentation()
        {
            ownedCamera = GetComponentInChildren<Camera>();
            ownedListeners = GetComponentsInChildren<AudioListener>();
            ownedWeapons = GetComponentsInChildren<Weapon>(true);
            ownedTelemetry = GetComponentsInChildren<VEVE.UI.ScopeTelemetryBridge>(true);
        }

        private void ApplyLocalPresentation(bool local)
        {
            if (ownedCamera == null) CachePresentation();
            if (ownedCamera != null && ownedCamera.gameObject != null)
                ownedCamera.enabled = local;
            if (ownedListeners != null)
            {
                for (int i = 0; i < ownedListeners.Length; i++)
                    ownedListeners[i].enabled = local;
            }
            if (ownedWeapons != null)
            {
                for (int i = 0; i < ownedWeapons.Length; i++)
                    if (ownedWeapons[i] != null) ownedWeapons[i].enabled = local;
            }
            if (ownedTelemetry != null)
            {
                for (int i = 0; i < ownedTelemetry.Length; i++)
                    if (ownedTelemetry[i] != null) ownedTelemetry[i].enabled = local;
            }
        }
    }
}
