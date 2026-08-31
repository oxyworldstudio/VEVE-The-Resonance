using NUnit.Framework;
using VEVE.Net;

public sealed class LagCompRulesTests
{
    [Test]
    public void AuthorityWindowClampsMonotonically()
    {
        Assert.AreEqual(4, LagCompRules.AuthorityWindowFrames(10, 60));
        Assert.LessOrEqual(LagCompRules.AuthorityWindowFrames(100, 100), LagCompRules.MaxWindowFrames);
        Assert.GreaterOrEqual(LagCompRules.AuthorityWindowFrames(100000, 60), LagCompRules.MinWindowFrames, "capped not runaway");
        Assert.AreEqual(LagCompRules.MinWindowFrames, LagCompRules.AuthorityWindowFrames(0, 0));
    }

    [Test]
    public void RingStoresLatestPerOwner()
    {
        var ring = new ShotReplayWindow(8);
        ring.Mark(new ShotPrediction { tick = 10, owner = 7, localHit = true });
        Assert.IsTrue(ring.TryGetLatest(7, 10, out ShotPrediction got));
        Assert.IsTrue(got.localHit);
        ring.Mark(new ShotPrediction { tick = 11, owner = 7, localHit = false });
        Assert.IsTrue(ring.TryGetLatest(7, 9, out got));
        Assert.IsFalse(got.localHit, "latest prediction wins when server frame older");
        Assert.IsFalse(ring.TryGetLatest(99, 9, out _), "foreign owner never matches");
        ring.ForgetOwner(7);
        Assert.IsFalse(ring.TryGetLatest(7, 11, out _));
    }

    [Test]
    public void RingSmallCapacityGuardAndWrap()
    {
        var ring = new ShotReplayWindow(1); // below minimum: clamps to 8, never throws
        for (int i = 0; i < 9; i++) ring.Mark(new ShotPrediction { tick = i, owner = 3, localHit = i == 8 });
        Assert.IsTrue(ring.TryGetLatest(3, 8, out var p));
        Assert.IsTrue(p.localHit);
    }

    [Test]
    public void ReconcileConfirmsAndCountsDesync()
    {
        var ring = new ShotReplayWindow(32);
        int baseConfirmed = LagCompRules.ConfirmedCount, baseDesync = LagCompRules.DesyncCount;

        ring.Mark(new ShotPrediction { tick = 500, owner = 9, localHit = true });
        LagCompRules.Reconcile(ring, 9, 502, true); // server agrees within window
        Assert.AreEqual(baseConfirmed + 1, LagCompRules.ConfirmedCount);

        int b2 = LagCompRules.DesyncCount;
        ring.Mark(new ShotPrediction { tick = 600, owner = 9, localHit = true });
        LagCompRules.Reconcile(ring, 9, 601, false); // disagree
        Assert.AreEqual(b2 + 1, LagCompRules.DesyncCount);

        int c3 = LagCompRules.ConfirmedCount, d3 = LagCompRules.DesyncCount;
        LagCompRules.Reconcile(ring, 0, 0, true); // offline/host owner sentinel ignored
        LagCompRules.Reconcile(ring, LagCompRules.OfflineOwner, 10, true);
        Assert.AreEqual(c3, LagCompRules.ConfirmedCount + LagCompRules.DesyncCount - d3, "sentinels change nothing");

        ring.Mark(new ShotPrediction { tick = 700, owner = 12, localHit = true });
        int e1 = LagCompRules.ConfirmedCount + LagCompRules.DesyncCount;
        LagCompRules.Reconcile(ring, 12, 9999, true); // far outside window: no telemetry
        Assert.AreEqual(e1, LagCompRules.ConfirmedCount + LagCompRules.DesyncCount);
    }
}
