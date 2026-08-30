using System;
using NUnit.Framework;
using VEVE.Catalog;
using VEVE.WeaponCustomPro;

/// <summary>
/// Range-card / battle-zero / turret statics. Every assertion encodes the physical sign
/// convention: +1 = "place the reticle ABOVE the target centre". A rifle zeroed at Z drops
/// below the line of sight past Z (aim high grows monotonically) and rises above it between
/// the two sight-line crossings (aim low, i.e. negative holdover).
/// </summary>
public sealed class WcpZeroingTests
{
    private static WeaponSpec M4 => IconicWeaponCatalog.Get("m4a1");
    private static WeaponSpec Svd => IconicWeaponCatalog.Get("svd-dragunov");
    private static WeaponSpec Glock => IconicWeaponCatalog.Get("glock-17");

    [Test]
    public void TryComputeCardResolvesCatalogWeaponIds()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("m4a1", 100.0, 38.0, out RangeCard card));
        Assert.AreEqual("m4a1", card.weaponId);
        Assert.AreEqual(card.entries.Length, ZeroingSystem.DefaultCardDistancesM.Length);
        for (int i = 1; i < card.entries.Length; i++)
            Assert.Greater(card.entries[i].distanceM, card.entries[i - 1].distanceM);
        Assert.IsFalse(ZeroingSystem.TryComputeCard("no-such-gun", 100.0, 38.0, out _));
    }

    [Test]
    public void HoldoverIsZeroAtTheZeroRange()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("m4a1", 100.0, 38.0, out RangeCard card));
        Assert.AreEqual(0.0, ZeroingSystem.ComputeHoldoverMoa(card, 100.0), 1e-6);
        Assert.IsTrue(ZeroingSystem.TryComputeCard("svd-dragunov", 300.0, 40.0, out RangeCard svd));
        Assert.AreEqual(0.0, ZeroingSystem.ComputeHoldoverMoa(svd, 300.0), 1e-6);
        Assert.IsTrue(ZeroingSystem.TryComputeCard("glock-17", 25.0, 15.0, out RangeCard glock));
        Assert.AreEqual(0.0, ZeroingSystem.ComputeHoldoverMoa(glock, 25.0), 1e-6);
    }

    [Test]
    public void HoldoverGrowsMonotonicallyBeyondTheZero()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("m4a1", 100.0, 38.0, out RangeCard card));
        double prev = double.NegativeInfinity;
        foreach (double d in new[] { 150, 200, 300, 500, 800, 1200 })
        {
            double hold = ZeroingSystem.ComputeHoldoverMoa(card, d);
            Assert.Greater(hold, 0.0, $"+{d}m must require aim-up");
            Assert.Greater(hold, prev, d + "m");
            prev = hold;
        }
    }

    [Test]
    public void NearerThanTheFirstCrossingMeansAimUpAndBetweenCrossingsMeansAimDown()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("m4a1", 100.0, 38.0, out RangeCard card));
        Assert.Greater(card.firstSightLineCrossingM, 1.0);
        Assert.Less(card.firstSightLineCrossingM, card.zeroRangeM);

        double near = ZeroingSystem.ComputeHoldoverMoa(card, card.firstSightLineCrossingM * 0.4);
        Assert.Greater(near, 0.0, "inside the near crossing the sight line is under the trajectory: aim up");

        double mid = ZeroingSystem.ComputeHoldoverMoa(
            card, 0.5 * (card.firstSightLineCrossingM + card.zeroRangeM));
        Assert.Less(mid, 0.0, "between the crossings the trajectory crowns the sight line: aim down");

        Assert.GreaterOrEqual(card.maxRiseAboveSightM, 0.0);
    }

    [Test]
    public void RangeCardPoiSignMatchesHoldoverConvention()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("svd-dragunov", 300.0, 40.0, out RangeCard card));
        foreach (RangeCardEntry e in card.entries)
        {
            double poi = e.pointOfImpactAboveSightM;
            double hold = e.holdoverMoa;
            if (Math.Abs(poi) < 1e-4 && Math.Abs(hold) < 1e-4) continue; // the zero row is ~0/0 by design
            Assert.AreEqual(Math.Sign(-poi), Math.Sign(hold), (int)e.distanceM,
                "hold-up must oppose impact deviation from the sight line");
        }
    }

    [Test]
    public void PointBlankEnvelopeGrowsWithZeroRange()
    {
        double pbr50 = ZeroingSystem.SolvePointBlankRange(M4, 50.0, 38.0, ZeroingSystem.TargetMaxDropM);
        double pbr150 = ZeroingSystem.SolvePointBlankRange(M4, 150.0, 38.0, ZeroingSystem.TargetMaxDropM);
        Assert.Greater(pbr50, 50.0);
        Assert.Greater(pbr150, pbr50);
        Assert.LessOrEqual(pbr150, 2000.0);

        // The 38 cm drop budget must give a looser (longer) envelope than a 10 cm one.
        double pbrTight = ZeroingSystem.SolvePointBlankRange(M4, 150.0, 38.0, 0.10);
        Assert.Greater(pbr150, pbrTight);
    }

    [Test]
    public void BattleZeroIsDeterministicAndCoversDesiredEnvelope()
    {
        double z1 = ZeroingSystem.ComputeBattleZero(M4, 38.0, 250.0);
        double z2 = ZeroingSystem.ComputeBattleZero(M4, 38.0, 250.0);
        Assert.AreEqual(z1, z2, "same inputs must give the same battle zero");
        Assert.GreaterOrEqual(z1, 10.0);
        Assert.LessOrEqual(z1, 300.0);
        double pbr = ZeroingSystem.SolvePointBlankRange(M4, z1, 38.0, ZeroingSystem.TargetMaxDropM);
        Assert.GreaterOrEqual(pbr, 244.0, "battle zero must honour the requested point-blank range");

        // Pistol-class weapon: a 25 m zero solves cleanly, no NaN.
        double zp = ZeroingSystem.ComputeBattleZero(Glock, 15.0, 60.0, 200.0);
        Assert.Greater(zp, 0.0);
        Assert.Less(zp, 200.0);
    }

    [Test]
    public void CarryBarrelAdjustmentMovesVelocityInTheRightDirection()
    {
        double shortBarrel = ZeroingSystem.AdjustMuzzleVelocityForBarrel(M4, 234.0);   // 10" CQB carry
        double longBarrel = ZeroingSystem.AdjustMuzzleVelocityForBarrel(M4, 508.0);     // 20" recce
        Assert.Less(shortBarrel, M4.muzzleVelocity);
        Assert.Greater(longBarrel, M4.muzzleVelocity);
        Assert.Greater(shortBarrel, M4.muzzleVelocity * 0.6 - 1.0);
        Assert.LessOrEqual(longBarrel, M4.muzzleVelocity * 1.25 + 1.0);

        Assert.IsTrue(ZeroingSystem.TryComputeCard("m4a1", 100.0, 38.0, out RangeCard full, barrelLengthMm: 508.0));
        Assert.IsTrue(ZeroingSystem.TryComputeCard("m4a1", 100.0, 38.0, out RangeCard cqb, barrelLengthMm: 234.0));
        Assert.Greater(full.muzzleVelocityMs, cqb.muzzleVelocityMs);
        // slower carry -> must need MORE holdover at a fixed long distance
        Assert.Greater(ZeroingSystem.ComputeHoldoverMoa(cqb, 500.0),
                       ZeroingSystem.ComputeHoldoverMoa(full, 500.0));
    }

    [Test]
    public void AdjustClicksClampsToTurretTravel()
    {
        Assert.AreEqual(15, ZeroingSystem.AdjustClicks(0.25, 10, 5));
        Assert.AreEqual(-8, ZeroingSystem.AdjustClicks(0.25, -3, -5));
        Assert.AreEqual(ZeroingSystem.MaxTurretClicksPerDirection, ZeroingSystem.AdjustClicks(0.25, 0, 10000));
        Assert.AreEqual(-ZeroingSystem.MaxTurretClicksPerDirection, ZeroingSystem.AdjustClicks(0.25, 0, -10000));
        Assert.AreEqual(ZeroingSystem.MaxTurretClicksPerDirection,
            ZeroingSystem.AdjustClicks(0.25, ZeroingSystem.MaxTurretClicksPerDirection, 3));
    }

    [Test]
    public void ClickAccumulatorWrapsAroundTheDialRing()
    {
        Assert.AreEqual(0, ZeroingSystem.WrapClickIndex(100));
        Assert.AreEqual(50, ZeroingSystem.WrapClickIndex(150));
        Assert.AreEqual(99, ZeroingSystem.WrapClickIndex(-1));
        Assert.AreEqual(90, ZeroingSystem.WrapClickIndex(-10));
        Assert.AreEqual(9, ZeroingSystem.WrapClickIndex(1009));
        Assert.AreEqual(0, ZeroingSystem.WrapClickIndex(5, 0));
    }

    [Test]
    public void MoaToClicksRoundsAwayFromZero()
    {
        Assert.AreEqual(6, ZeroingSystem.MoaToClicks(0.25, 1.6));
        Assert.AreEqual(3, ZeroingSystem.MoaToClicks(0.1, 0.25));
        Assert.AreEqual(-6, ZeroingSystem.MoaToClicks(0.25, -1.6));
        Assert.AreEqual(0, ZeroingSystem.MoaToClicks(0.0, 5.0));
    }

    [Test]
    public void SvdHeavyCaseTrajectoryStaysFiniteToCardMax()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("svd-dragunov", 300.0, 40.0, out RangeCard card));
        RangeCardEntry last = card.entries[card.entries.Length - 1];
        Assert.AreEqual(1200f, last.distanceM, 1e-3);
        Assert.That(last.dropMeters, Is.Positive & Is.LessThan(200f));
        Assert.That(last.timeOfFlightS, Is.Positive & Is.LessThan(5f));
        Assert.That(last.holdoverMoa, Is.Positive);
        Assert.That(last.retainedVelocityMs, Is.Positive);
    }

    [Test]
    public void SightHeightMovesTheNearCrossing()
    {
        double lowCross, highCross, lowCrown, highCrown;
        ZeroingSystem.SolveSightLineCrossings(Svd, 300.0, 20.0, out lowCross, out lowCrown);
        ZeroingSystem.SolveSightLineCrossings(Svd, 300.0, 90.0, out highCross, out highCrown);
        Assert.Less(lowCross, highCross, "more sight height pushes the near cross further out");
        Assert.Less(highCrown, lowCrown, "a higher sight line eats into the trajectory crown above it");
    }
}
