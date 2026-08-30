using NUnit.Framework;
using UnityEngine;
using VEVE.Agents;

public sealed class AgentLODTests
{
    [Test]
    public void ComputeLODDistanceBandsMapToTiers()
    {
        var origin = Vector3.zero;
        Assert.AreEqual(AgentLODTier.Full, AgentLOD.ComputeLOD(new Vector3(5f, 0f, 0f), origin));
        Assert.AreEqual(AgentLODTier.Standard, AgentLOD.ComputeLOD(new Vector3(30f, 0f, 0f), origin));
        Assert.AreEqual(AgentLODTier.Simplified, AgentLOD.ComputeLOD(new Vector3(70f, 0f, 0f), origin));
        Assert.AreEqual(AgentLODTier.Statistical, AgentLOD.ComputeLOD(new Vector3(500f, 0f, 0f), origin));
    }

    [Test]
    public void TickIntervalsIncreaseMonotonicallyWithDistance()
    {
        Assert.AreEqual(1, AgentLOD.GetTickInterval(AgentLODTier.Full));
        Assert.LessOrEqual(AgentLOD.GetTickInterval(AgentLODTier.Full),
            AgentLOD.GetTickInterval(AgentLODTier.Standard));
        Assert.Less(AgentLOD.GetTickInterval(AgentLODTier.Standard),
            AgentLOD.GetTickInterval(AgentLODTier.Simplified));
        Assert.Less(AgentLOD.GetTickInterval(AgentLODTier.Simplified),
            AgentLOD.GetTickInterval(AgentLODTier.Statistical));
    }

    [Test]
    public void FullTierAlwaysTicks()
    {
        Assert.IsTrue(AgentLOD.ShouldTick(AgentLODTier.Full, 0));
        Assert.IsTrue(AgentLOD.ShouldTick(AgentLODTier.Full, 7));
    }

    [Test]
    public void StaggerIsDeterministicAndNonNegative()
    {
        int a = AgentLOD.GetStaggerFor(-42);
        int b = AgentLOD.GetStaggerFor(-42);
        Assert.AreEqual(a, b);
        Assert.GreaterOrEqual(a, 0);
    }
}
