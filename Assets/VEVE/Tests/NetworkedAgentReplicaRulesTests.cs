using NUnit.Framework;
using VEVE.Net;

public sealed class NetworkedAgentReplicaRulesTests
{
    [Test]
    public void OfflineSimulatesBrain()
    {
        Assert.IsTrue(NetAgentReplicaRules.ShouldSimulate(false, false),
            "no session -> single-player AI must run (never disabled by absent NetworkManager)");
    }

    [Test]
    public void HostServerSimulate()
    {
        Assert.IsTrue(NetAgentReplicaRules.ShouldSimulate(true, true));
    }

    [Test]
    public void RemoteClientNeverSimulates()
    {
        Assert.IsFalse(NetAgentReplicaRules.ShouldSimulate(true, false),
            "dual AI simulation is structurally forbidden");
    }

    [Test]
    public void TransformReplicationMatrix()
    {
        Assert.IsFalse(NetAgentReplicaRules.ShouldReplicateTransform(false, true, true), "offline: nothing to replicate");
        Assert.IsTrue(NetAgentReplicaRules.ShouldReplicateTransform(true, true, true));
        Assert.IsFalse(NetAgentReplicaRules.ShouldReplicateTransform(true, false, true), "pre-spawn objects must not be driven");
        Assert.IsFalse(NetAgentReplicaRules.ShouldReplicateTransform(true, true, false), "authority is not a consumer");
    }
}
