using NUnit.Framework;
using VEVE.Catalog;
using VEVE.Net;

public sealed class PredictionReconcilerTests
{
    [Test]
    public void OptimisticLocalHitReverifiesThroughAuthoritativeMiss()
    {
        var ring = new ShotReplayWindow(16);
        var ledger = new FamilyXpLedger();
        var rec = new PredictionReconciler();
        const ulong owner = 4;

        ledger.Grant(owner, "m4a1", 100);
        ring.Mark(new ShotPrediction { tick = 300, owner = owner, localHit = true });

        var result = rec.Reconcile(ring, owner, 304, false, ledger, "m4a1", FamilyXpLedger.XpPerHitOnTarget);
        Assert.AreEqual(PredictionResult.Reverted, result);
        Assert.AreEqual(100 - FamilyXpLedger.XpPerHitOnTarget, ledger.Xp(owner, "m4a1"), 1e-6, "exact credit revoked");
        Assert.AreEqual(1, rec.RevertCount);
        Assert.AreEqual(0, rec.ConfirmedCount);

        // second revert on exhausted ledger: no negative, still counted
        rec.Reconcile(ring, owner, 304, false, ledger, "m4a1", 900);
        Assert.GreaterOrEqual(ledger.Xp(owner, "m4a1"), 0d);
    }

    [Test]
    public void MatchedPredictionsConfirmNotRevoke()
    {
        var ring = new ShotReplayWindow(8);
        var ledger = new FamilyXpLedger();
        ring.Mark(new ShotPrediction { tick = 50, owner = 9, localHit = false });
        var rec = new PredictionReconciler();
        Assert.AreEqual(PredictionResult.Confirmed,
            rec.Reconcile(ring, 9, 52, false, ledger, "ak74m", 6));
        Assert.AreEqual(0d, ledger.Xp(9, "ak74m"), "confirmation never mints xp");
        Assert.AreEqual(0, rec.RevertCount);
    }

    [Test]
    public void ForeignAndLateAreIgnoredRevolutions()
    {
        var ring = new ShotReplayWindow(8);
        ring.Mark(new ShotPrediction { tick = 200, owner = 5, localHit = true });
        var rec = new PredictionReconciler();
        Assert.AreEqual(PredictionResult.Foreign, rec.Reconcile(ring, 0, 20, true, null, "x", 6));
        Assert.AreEqual(PredictionResult.Foreign, rec.Reconcile(ring, LagCompRules.OfflineOwner, 20, true, null, "x", 6));
        Assert.AreEqual(PredictionResult.LateOutsideWindow,
            rec.Reconcile(ring, 5, 5000, false, null, "x", 6));
        StringAssert.Contains("late=1", rec.Telemetry);
    }

    [Test]
    public void RevokeIsFlooredAtZeroAndNeverCrosses()
    {
        var l = new FamilyXpLedger();
        l.Grant(22, "scar-l", 1000);
        for (int i = 0; i < 400; i++) l.Revoke(22, "scar-l", 10);
        Assert.GreaterOrEqual(l.Xp(22, "scar-l"), 0d);
        Assert.AreEqual(0d, l.Xp(22, "scar-l"), 1e-6, "floor hit: repeated revoke converges at zero");

        l.Grant(23, "p90", 50);
        l.Revoke(0, "p90", 50); // offline owner: unrevocable
        Assert.AreEqual(50d, l.Xp(23, "p90"));
    }
}
