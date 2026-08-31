using NUnit.Framework;
using UnityEngine;
using VEVE.AI;

public sealed class TuningRulesTests
{
    [Test]
    public void TtaMonotonicBoundedAndHumanPlausible()
    {
        Assert.Greater(TuningRules.TimeToAcquireSeconds(0f), TuningRules.TimeToAcquireSeconds(0.5f));
        Assert.Greater(TuningRules.TimeToAcquireSeconds(0.5f), TuningRules.TimeToAcquireSeconds(1f));
        Assert.GreaterOrEqual(TuningRules.TimeToAcquireSeconds(1f), TuningRules.ReflexFloorSeconds - 0.001f);
        Assert.LessOrEqual(TuningRules.TimeToAcquireSeconds(0f), TuningRules.NoviceAcquireSeconds + 0.001f);
        // NaN input sanitizes to novice behaviour, never crashes the simulation
        Assert.GreaterOrEqual(TuningRules.TimeToAcquireSeconds(float.NaN), TuningRules.ReflexFloorSeconds);
    }

    [Test]
    public void CadencePrioritizesContacts()
    {
        Assert.Greater(TuningRules.CadenceSeconds(0), TuningRules.CadenceSeconds(1));
        Assert.Greater(TuningRules.CadenceSeconds(1), TuningRules.CadenceSeconds(2));
        Assert.Greater(TuningRules.CadenceSeconds(99), 1f);
        Assert.LessOrEqual(TuningRules.CadenceSeconds(2), 2.001f);
    }

    [Test]
    public void PostureDensityScalesWithinContract()
    {
        Assert.AreEqual(0.05f, TuningRules.PatrolDensityFromPosture(0.01f, 0f), 1e-4f, "floor");
        Assert.Greater(TuningRules.PatrolDensityFromPosture(0.5f, 1f), TuningRules.PatrolDensityFromPosture(0.5f, 0f));
        Assert.LessOrEqual(TuningRules.PatrolDensityFromPosture(9f, 1f), 1f, "hard ceiling");
        Assert.AreEqual(0.5f, TuningRules.PatrolDensityFromPosture(0.385f, 1f), 0.6f); // sanity loose
    }

    [Test]
    public void PreferredRangeGrowsWithSkillAndZoom()
    {
        float baseRange = 500f;
        Assert.Greater(TuningRules.PreferredEngagementRange(baseRange, 1f, 1f),
                       TuningRules.PreferredEngagementRange(baseRange, 0f, 1f));
        Assert.Greater(TuningRules.PreferredEngagementRange(baseRange, 0.5f, 8f),
                       TuningRules.PreferredEngagementRange(baseRange, 0.5f, 1f));
        Assert.LessOrEqual(TuningRules.PreferredEngagementRange(baseRange, 2f, 99f), baseRange * 0.92f,
            "skill+glass never claims past the authored effective range");
    }
}
