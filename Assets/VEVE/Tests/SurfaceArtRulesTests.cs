using NUnit.Framework;
using UnityEngine;
using VEVE.Graphics;

public sealed class SurfaceArtRulesTests
{
    [Test]
    public void WetGlossAlwaysIncreasesAndCaps()
    {
        Assert.Greater(SurfaceArtRules.GlossAfterWeather(0.2f, 1f), 0.2f);
        Assert.Greater(SurfaceArtRules.GlossAfterWeather(0.8f, 1f), 0.8f);
        Assert.LessOrEqual(SurfaceArtRules.GlossAfterWeather(1f, 1f), 1f);
        Assert.AreEqual(0.4f, SurfaceArtRules.GlossAfterWeather(0.4f, 0f), 1e-4f);
    }

    [Test]
    public void WetTintDarkensAndDustWarms()
    {
        var c = new Color(0.6f, 0.6f, 0.6f);
        Assert.Less(SurfaceArtRules.TintAfterWeather(c, 1f, 0f).grayscale, c.grayscale + 1e-5f);
        var dusty = SurfaceArtRules.TintAfterWeather(c, 0f, 1f);
        Assert.Greater(dusty.r, dusty.b, "dust is warm");
        Assert.AreNotEqual(dusty, c);
    }

    [Test]
    public void SunWarmthPeaksAtGoldenAngle()
    {
        Assert.Greater(SurfaceArtRules.SunWarmth(8f), SurfaceArtRules.SunWarmth(75f));
        Assert.GreaterOrEqual(SurfaceArtRules.SunWarmth(0f), 0f);
        Assert.LessOrEqual(SurfaceArtRules.SunWarmth(90f), 1f);
    }

    [Test]
    public void KnownKeysResolveAndFallbackIsConcrete()
    {
        Assert.IsTrue(SurfaceArtRules.TryPalette("Concrete", out var p));
        Assert.IsNotEmpty(p.baseColor.ToString());
        Assert.AreEqual("Metal", SurfaceArtRules.ResolveKey("BrushedSteel"));
        Assert.AreEqual("Concrete", SurfaceArtRules.ResolveKey(null));
    }
}
