using NUnit.Framework;
using UnityEngine;
using VEVE.Net;

public sealed class NetworkGameFlowInfraTests
{
    [Test]
    public void EnsureInfraWiresTransportIntoNetworkConfig()
    {
        // W-BUG-001 regression: the lobby was static because UnityTransport was
        // added to the GameObject but never assigned to NetworkConfig.NetworkTransport,
        // making StartHost fail silently. The infra must wire it explicitly.
        // (NGO NetworkManager.OnEnable calls DontDestroyOnLoad even in editor, so the
        // test GO stays INACTIVE: no lifecycle events fire during wiring.)
        var nmGo = new GameObject("nm-under-test");
        nmGo.SetActive(false);
        var flowGo = new GameObject("flow-test");
        try
        {
            var nm = nmGo.AddComponent<Unity.Netcode.NetworkManager>();
            var flow = flowGo.AddComponent<NetworkGameFlow>();
            flow.EnsureInfra("0.0.0.0", 7777, nm);

            var transport = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            Assert.IsNotNull(transport, "UnityTransport present");
            Assert.AreSame(transport, nm.NetworkConfig.NetworkTransport,
                "transport MUST be referenced by NetworkConfig (regression)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(nmGo);
            UnityEngine.Object.DestroyImmediate(flowGo);
        }
    }

    [Test]
    public void EnsureInfraIsIdempotentAndPlayerPrefabBoundWhenAvailable()
    {
        var nmGo = new GameObject("nm-under-test");
        nmGo.SetActive(false);
        try
        {
            var nm = nmGo.AddComponent<Unity.Netcode.NetworkManager>();
            var flow = new GameObject("flow-test2").AddComponent<NetworkGameFlow>();
            flow.EnsureInfra("0.0.0.0", 7777, nm);
            var firstTransport = nm.NetworkConfig.NetworkTransport;
            flow.EnsureInfra("0.0.0.0", 7777, nm); // second call must not duplicate

            Assert.AreEqual(1, nmGo.GetComponents<Unity.Netcode.Transports.UTP.UnityTransport>().Length,
                "no duplicate transports on re-infra");
            Assert.AreSame(firstTransport, nm.NetworkConfig.NetworkTransport);

            var pawn = UnityEngine.Resources.Load<GameObject>("Generated/RemotePlayer");
            if (pawn != null)
                Assert.AreSame(pawn, nm.NetworkConfig.PlayerPrefab, "pawn prefab bound from Resources");
            // when the asset is absent the flow degrades gracefully (no crash, no assert)
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(nmGo);
        }
    }
}
