using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class AiAcousticsTests
{
    [Test]
    public void BearingErrorShrinksWithLoudnessAndGrowsWithDistance()
    {
        Assert.Less(AiAcoustics.BearingErrorDegrees(30f, 20f),
                    AiAcoustics.BearingErrorDegrees(1.5f, 20f));
        Assert.Less(AiAcoustics.BearingErrorDegrees(20f, 5f),
                    AiAcoustics.BearingErrorDegrees(20f, 45f));
        Assert.GreaterOrEqual(AiAcoustics.BearingErrorDegrees(1f, 80f), 2f);
        Assert.LessOrEqual(AiAcoustics.BearingErrorDegrees(30f, 1f), 55f);
        Assert.AreEqual(60f, AiAcoustics.BearingErrorDegrees(0f, 10f));
    }

    [Test]
    public void RangeConfidenceMonotonicInBothInputs()
    {
        Assert.Greater(AiAcoustics.RangeConfidence(30f, 10f), AiAcoustics.RangeConfidence(2f, 10f));
        Assert.Greater(AiAcoustics.RangeConfidence(20f, 10f), AiAcoustics.RangeConfidence(20f, 90f));
        Assert.That(AiAcoustics.RangeConfidence(100f, 0f), Is.InRange(0f, 1f));
        Assert.AreEqual(0f, AiAcoustics.RangeConfidence(0f, 30f));
    }

    [Test]
    public void NoiseEstimateNeverKnowsTruePositionAndIsDeterministic()
    {
        var listener = new Vector3(0f, 0f, 0f);
        var real = new Vector3(35f, 0f, 12f);

        Vector3 a = AiAcoustics.EstimateNoisePosition(listener, real, 12f, 919u);
        Vector3 b = AiAcoustics.EstimateNoisePosition(listener, real, 12f, 919u);
        Assert.AreEqual(a.x, b.x, 1e-6f);
        Assert.AreEqual(a.y, b.y, 1e-6f);
        Assert.Greater(Vector3.Distance(a, real), 0.5f, "hearing cannot pinpoint gunfire");
        Assert.Less(Vector3.Distance(a, listener), Vector3.Distance(real, listener) * 1.2f,
            "ranged under-estimate, never wildly past");
        Assert.Greater(Vector3.Distance(a, listener), 5f, "still reports a meaningful bearing");

        Assert.AreEqual(listener, AiAcoustics.EstimateNoisePosition(listener, listener + new Vector3(0.01f, 0f, 0f), 30f, 1u));
    }

    [Test]
    public void ScopeGlintRuleIsW3Shaped()
    {
        Assert.AreEqual(0f, AiAcoustics.ScopeGlintBonus(1f, 52.5f), "red dot never glints");
        Assert.AreEqual(0f, AiAcoustics.ScopeGlintBonus(9f, 10f), "sun too low");
        Assert.AreEqual(0f, AiAcoustics.ScopeGlintBonus(9f, 80f), "sun overhead past window");
        float low = AiAcoustics.ScopeGlintBonus(6f, 52.5f);
        float high = AiAcoustics.ScopeGlintBonus(25f, 52.5f);
        Assert.Greater(high, low);
        Assert.LessOrEqual(high, AiAcoustics.GlintBonusCeiling);
        Assert.Greater(low, 0f);
    }

    [Test]
    public void CalloutSeedIsStableAndPositionSensitive()
    {
        uint s1 = AiAcoustics.CalloutSeed(42, new Vector3(10f, 0f, 5f));
        uint s1b = AiAcoustics.CalloutSeed(42, new Vector3(10f, 0f, 5f));
        uint s2 = AiAcoustics.CalloutSeed(42, new Vector3(10f, 0f, 50f));
        Assert.AreEqual(s1, s1b);
        Assert.AreNotEqual(s1, s2);
        Assert.Greater(s1, 0u);
    }
}
