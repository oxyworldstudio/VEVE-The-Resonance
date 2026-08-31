using Unity.Netcode;
using UnityEngine;
using VEVE;

namespace VEVE.Net
{
    /// <summary>
    /// C4f v1 network pawn: per-connection player object spawned by NGO from
    /// NetworkConfig.PlayerPrefab (registered by NetworkGameFlow). Minimal by
    /// design - movement + ownership + camera handoff are the v1 contract;
    /// per-pawn weapon/gear is the next session, never a half-wired state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkedPlayerPawn : NetworkBehaviour
    {
        public static readonly System.Collections.Generic.List<NetworkedPlayerPawn> Active =
            new System.Collections.Generic.List<NetworkedPlayerPawn>(8);

        public ulong OwnerClientId { get; private set; }
        public bool IsMine => OwnerClientId == NetworkManager.Singleton?.LocalClientId;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            OwnerClientId = NetworkObject.OwnerClientId;
            if (!Active.Contains(this)) Active.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            Active.Remove(this);
            base.OnNetworkDespawn();
        }

        /// <summary>Count distinct connections with pawn ownership on the host side.</summary>
        public static int TotalConnectedPawns()
        {
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (int i = 0; i < Active.Count; i++) seen.Add(Active[i].OwnerClientId);
            return seen.Count;
        }

        public static int PawnsOnClient(ulong clientId)
        {
            int n = 0;
            for (int i = 0; i < Active.Count; i++) if (Active[i].OwnerClientId == clientId) n++;
            return n;
        }
    }
}
