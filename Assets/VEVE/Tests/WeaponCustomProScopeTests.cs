using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VEVE.WeaponCustomPro;

/// <summary>
/// Optics data-table and pure statics checks for the Weapon Customization Pro tranche:
/// scope catalogue integrity, field-of-view monotonicity, exit pupil, cheek weld fit,
/// handling multipliers and the documented parallax approximation. No scenes, no components.
/// </summary>
public sealed class WcpScopeOpticsTests
{
    private const double Eps = 1e-9;

    [Test]
    public void ScopeCatalogIsUniqueAndWellPopulated()
    {
        Assert.GreaterOrEqual(ScopeCatalog.Count, 12, "Expected ~12 real-world optics");
        var ids = ScopeCatalog.All.Select(p => p.id).ToList();
        Assert.IsFalse(ids.Any(string.IsNullOrWhiteSpace));
        Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Test]
    public void ScopeCatalogLookupsRoundTrip()
    {
        foreach (ScopeProfile p in ScopeCatalog.All)
        {
            Assert.IsTrue(ScopeCatalog.TryGet(p.id, out ScopeProfile got));
            Assert.AreSame(p, got);
            Assert.Greater(p.objectiveDiameterMm, 0f, p.id);
            Assert.Greater(p.weightGrams, 0f, p.id);
        }
        Assert.IsFalse(ScopeCatalog.TryGet("does-not-exist", out _));
    }

    [Test]
    public void HigherMagnificationNeverWidensThePicture()
    {
        foreach (ScopeProfile p in ScopeCatalog.All)
        {
            Assert.GreaterOrEqual(
                p.PictureFovLinearMetersAt100m(p.magnificationMin),
                p.PictureFovLinearMetersAt100m(p.magnificationMax) - Eps,
                p.id);
        }
    }

    [Test]
    public void ZoomLpvoFieldIsMonotoneInMagnification()
    {
        Assert.IsTrue(ScopeCatalog.TryGet("sb-pm2-5-25x56", out ScopeProfile pm));
        double a = ScopeOpticsModel.PictureFovLinearMeters(pm, 5);
        double b = ScopeOpticsModel.PictureFovLinearMeters(pm, 10);
        double c = ScopeOpticsModel.PictureFovLinearMeters(pm, 15);
        double d = ScopeOpticsModel.PictureFovLinearMeters(pm, 25);
        Assert.Greater(a, b);
        Assert.Greater(b, c);
        Assert.Greater(c, d);
        Assert.Greater(ScopeOpticsModel.PictureFovDegrees(pm, 25), 0.0);
    }

    [Test]
    public void ExitPupilIsObjectiveOverMagnificationAndClamped()
    {
        Assert.IsTrue(ScopeCatalog.TryGet("sb-pm2-5-25x56", out ScopeProfile pm));
        Assert.AreEqual(56.0 / 10.0, ScopeOpticsModel.ExitPupilMm(pm, 10), 1e-6);
        Assert.AreEqual(56.0 / 25.0, ScopeOpticsModel.ExitPupilMm(pm, 25), 1e-6);
        Assert.LessOrEqual(ScopeOpticsModel.ExitPupilMm(pm, 1), ScopeOpticsModel.MaxUsefulEyePupilMm);
    }

    [Test]
    public void DocumentedFieldStopFovFormulaShrinksWithFocalLength()
    {
        double wide = ScopeProfile.FovLinearMetersPerKilometer(120.0);
        double tight = ScopeProfile.FovLinearMetersPerKilometer(480.0);
        Assert.Greater(wide, tight);
        Assert.Greater(wide, 0.0);
        Assert.AreEqual(0.0, ScopeProfile.FovLinearMetersPerKilometer(0.0));
    }

    [Test]
    public void CheekWeldFitIsPerfectOnMatchAndDecaysMonotonically()
    {
        Assert.IsTrue(ScopeCatalog.TryGet("trijicon-ta31", out ScopeProfile ta31));
        double comb = ta31.boreToOpticCenterlineMm;
        Assert.AreEqual(1.0, ScopeOpticsModel.CheekWeldFitMultiplier(ta31, comb), 1e-9);
        Assert.Greater(ScopeOpticsModel.CheekWeldFitMultiplier(ta31, comb + 5),
                       ScopeOpticsModel.CheekWeldFitMultiplier(ta31, comb + 15));
        Assert.Greater(ScopeOpticsModel.CheekWeldFitMultiplier(ta31, comb - 5),
                       ScopeOpticsModel.CheekWeldFitMultiplier(ta31, comb - 15));
        Assert.AreEqual(0.0, ScopeOpticsModel.CheekWeldFitMultiplier(ta31, comb + 500.0), 1e-9);
    }

    [Test]
    public void RedDotsTolerateMoreCheekMismatchThanScopes()
    {
        Assert.IsTrue(ScopeCatalog.TryGet("aimpoint-pro", out ScopeProfile pro));
        Assert.IsTrue(ScopeCatalog.TryGet("nightforce-atacr-7-35x56", out ScopeProfile atacr));
        Assert.Greater(
            ScopeOpticsModel.CheekWeldFitMultiplier(pro, pro.boreToOpticCenterlineMm + 10),
            ScopeOpticsModel.CheekWeldFitMultiplier(atacr, atacr.boreToOpticCenterlineMm + 10));
    }

    [Test]
    public void WeightAndBalanceMultipliersAreClampedAndMonotonic()
    {
        Assert.IsTrue(ScopeCatalog.TryGet("trijicon-mro", out ScopeProfile mro));
        Assert.IsTrue(ScopeCatalog.TryGet("nightforce-atacr-7-35x56", out ScopeProfile atacr));
        double light = ScopeOpticsModel.WeightSwayPenaltyMultiplier(mro);
        double heavy = ScopeOpticsModel.WeightSwayPenaltyMultiplier(atacr);
        Assert.Greater(light, heavy);
        Assert.GreaterOrEqual(light, ScopeOpticsModel.MinMultiplier);
        Assert.LessOrEqual(light, 1.0);
        Assert.Greater(ScopeOpticsModel.BalanceTorquePenaltyMultiplier(atacr, 0.0),
                       ScopeOpticsModel.BalanceTorquePenaltyMultiplier(atacr, 120.0));
    }

    [Test]
    public void MagnificationAgilityScalesDownWithZoom()
    {
        Assert.IsTrue(ScopeCatalog.TryGet("vortex-razor-gen2e-1-10x24", out ScopeProfile razor));
        Assert.Greater(ScopeOpticsModel.MagnificationAgilityMultiplier(razor, 1),
                       ScopeOpticsModel.MagnificationAgilityMultiplier(razor, 5));
        Assert.Greater(ScopeOpticsModel.MagnificationAgilityMultiplier(razor, 5),
                       ScopeOpticsModel.MagnificationAgilityMultiplier(razor, 10));
    }

    [Test]
    public void ParallaxErrorIsZeroOnRangeAndBoundedPlausible()
    {
        Assert.AreEqual(0.0, ScopeOpticsModel.ParallaxErrorMm(100, 100, 56, 200), Eps);
        Assert.AreEqual(0.0, ScopeOpticsModel.ParallaxErrorMm(100, 100, 0, 200));
        Assert.AreEqual(0.0, ScopeOpticsModel.ParallaxErrorMm(0, 300, 56, 200));
        double at200 = ScopeOpticsModel.ParallaxErrorMm(100, 200, 40, 200);
        double at300 = ScopeOpticsModel.ParallaxErrorMm(100, 300, 40, 200);
        Assert.Greater(at200, 0.0);
        Assert.Greater(at300, at200);
        // Engineering sanity: ~tens of millimetres at 200 m for a 40 mm scope dialled at 100 m
        // (sub-decad: well under 2 MOA ~ 5.8 mm at 200 m is too tight, hundreds of cm is wrong).
        Assert.Greater(at200, 1.0);
        Assert.Less(at200, 300.0);
        Assert.Greater(ScopeOpticsModel.ParallaxErrorMm(100, 200, 56, 200), at200,
            "Bigger objective must parallax at least as much");
    }

    [Test]
    public void ACOGAndPmSpectDataMatchPublishedClasses()
    {
        Assert.IsTrue(ScopeCatalog.TryGet("trijicon-ta31", out ScopeProfile ta31));
        Assert.AreEqual(4f, ta31.magnificationMin);
        Assert.AreEqual(4f, ta31.magnificationMax);
        Assert.AreEqual(32f, ta31.objectiveDiameterMm);
        Assert.IsTrue(ScopeCatalog.TryGet("sb-pm2-5-25x56", out ScopeProfile pm));
        Assert.AreEqual(ReticleFocalPlane.FirstFocalPlane, pm.focalPlane);
        Assert.AreEqual(ReticleSubtensionUnit.Mrad, pm.reticleUnit);
        Assert.Greater(pm.parallaxCorrectionMinRangeM, 0f);
    }
}
