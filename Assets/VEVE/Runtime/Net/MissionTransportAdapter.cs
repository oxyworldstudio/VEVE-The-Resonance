using Unity.Netcode;
using UnityEngine;

namespace VEVE.Net
{
    /// <summary>
    /// C4b transport: moves journal-ordered NetCommands between host and clients with
    /// one ServerRpc (client intent) + one ClientRpc (authoritative fan-out). Host is
    /// the only writer of journal sequences; clients own mirrors, never state. With no
    /// NetworkManager present the adapter degrades gracefully to offline direct journal +
    /// mirror application (the single-player campaign loop keeps working unchanged).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MissionTransportAdapter : NetworkBehaviour
    {
        private MissionCommandJournal _journal;
        private NetMissionMirror _mirror;

        public MissionCommandJournal Journal => _journal;
        public NetMissionMirror Mirror => _mirror;

        /// <summary>Wire the CampaignLoopController session and mirror (called by the bootstrapper).</summary>
        public void Attach(MissionCommandJournal journal, NetMissionMirror mirror)
        {
            _journal = journal ?? new MissionCommandJournal();
            _mirror = mirror ?? new NetMissionMirror();
        }

        private void Start()
        {
            if (_journal == null) Attach(new MissionCommandJournal(), new NetMissionMirror());
            VEVE.Content.CampaignLoopController loop = FindFirstObjectByType<VEVE.Content.CampaignLoopController>();
            if (loop != null) loop.CommandSink = Submit;
        }

        public override void OnNetworkDespawn()
        {
            VEVE.Content.CampaignLoopController loop = FindFirstObjectByType<VEVE.Content.CampaignLoopController>();
            if (loop != null) loop.CommandSink = null;
            base.OnNetworkDespawn();
        }

        /// <summary>Authoritative-or-client entry point; route per role.</summary>
        public void Submit(NetCommand c)
        {
            if (_journal == null) _journal = new MissionCommandJournal();
            if (_mirror == null) _mirror = new NetMissionMirror();

            NetworkManager nm = NetworkManager.Singleton;
            bool live = nm != null && (nm.IsServer || nm.IsClient);
            if (!live)
            {
                // No live session: journal + mirror directly (single-player stays authoritative-identical).
                c.seq = MissionNetMap.AppendToJournal(_journal, c);
                _mirror.Apply(c);
                RelayPresentation(c);
                return;
            }

            if (IsHost)
            {
                c.seq = MissionNetMap.AppendToJournal(_journal, c);
                _mirror.Apply(c);
                RelayPresentation(c);
                BroadcastCommandClientRpc(c);
                return;
            }

            EnqueueServerRpc(c);
        }

        [ServerRpc(RequireOwnership = false)]
        private void EnqueueServerRpc(NetCommand c, ServerRpcParams p = default)
        {
            if (!IsServer) return;
            c.senderId = (ushort)(p.Receive.SenderClientId % 65534ul + 2ul); // tag origin per client

            if (_journal == null) _journal = new MissionCommandJournal();
            if (_mirror == null) _mirror = new NetMissionMirror();
            c.seq = MissionNetMap.AppendToJournal(_journal, c);
            _mirror.Apply(c);
            RelayPresentation(c);
            BroadcastCommandClientRpc(c);
        }

        // The host sees its own ClientRpc too; NetMissionMirror's ordered-applied
        // guard makes the duplicate Apply a no-op, so delivery stays simple and reliable.
        [ClientRpc]
        private void BroadcastCommandClientRpc(NetCommand c)
        {
            if (_mirror == null) _mirror = new NetMissionMirror();
            _mirror.Apply(c);
            if (!IsHost) RelayPresentation(c); // host already presented locally
        }

        private void RelayPresentation(NetCommand c)
        {
            if (!MissionNetMap.IsRelayOnly(c)) return;
            VEVE.EventBus.PublishGlobal(MissionNetMap.ToBark(c));
        }

        private static bool IsListening(NetworkManager nm)
        {
            return nm != null && (nm.IsServer || nm.IsClient);
        }
    }
}
