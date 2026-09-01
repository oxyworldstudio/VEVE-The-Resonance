using NUnit.Framework;
using UnityEngine;
using VEVE.Graphics;
using VEVE;

public sealed class TextureFidelityTests
{
    [Test]
    public void TierResolutionMappingIsExact()
    {
        Assert.AreEqual(512, TextureQualityRules.ResolutionFor(VEVE.Realism.QualityLevel.Low));
        Assert.AreEqual(1024, TextureQualityRules.ResolutionFor(VEVE.Realism.QualityLevel.Medium));
        Assert.AreEqual(2048, TextureQualityRules.ResolutionFor(VEVE.Realism.QualityLevel.High));
        Assert.AreEqual(4096, TextureQualityRules.ResolutionFor(VEVE.Realism.QualityLevel.Ultra));
    }

    [Test]
    public void NormalizeSideRoundsUpToPowerOfTwoAndClamps()
    {
        Assert.AreEqual(256, TextureQualityRules.NormalizeSide(100));
        Assert.AreEqual(1024, TextureQualityRules.NormalizeSide(513), "rounds UP to power of two");
        Assert.AreEqual(1024, TextureQualityRules.NormalizeSide(1000));
        Assert.AreEqual(4096, TextureQualityRules.NormalizeSide(9999));
        Assert.AreEqual(256, TextureQualityRules.NormalizeSide(-5), "below-range clamps to min");
    }

    [Test]
    public void MipCountForIsLog2PlusOne()
    {
        Assert.AreEqual(11, TextureQualityRules.MipCountFor(1024));
        Assert.AreEqual(12, TextureQualityRules.MipCountFor(2048), "1 + floor(log2(side))");
        Assert.AreEqual(1, TextureQualityRules.MipCountFor(1));
    }

    [Test]
    public void AnisoMapsPerLevel()
    {
        Assert.AreEqual(0, TextureQualityRules.AnisoFor(VEVE.Realism.QualityLevel.Low));
        Assert.AreEqual(2, TextureQualityRules.AnisoFor(VEVE.Realism.QualityLevel.Medium));
        Assert.AreEqual(4, TextureQualityRules.AnisoFor(VEVE.Realism.QualityLevel.High));
        Assert.AreEqual(8, TextureQualityRules.AnisoFor(VEVE.Realism.QualityLevel.Ultra));
    }

    [Test]
    public void GenerationBudgetScalesWithArea()
    {
        float small = TextureQualityRules.GenerationBudgetSeconds(512, 500f);
        float large = TextureQualityRules.GenerationBudgetSeconds(4096, 500f);
        Assert.Greater(large, small, "area scaling");
        Assert.LessOrEqual(large, 4f);
        Assert.GreaterOrEqual(small, 0.25f);
    }

    [Test]
    public void VariationIsDeterministicAndInRange()
    {
        float h1 = SurfaceVariationRules.HueShift("MEDIUM_TOWN", "Concrete");
        float h2 = SurfaceVariationRules.HueShift("MEDIUM_TOWN", "Concrete");
        Assert.AreEqual(h1, h2, 1e-9f);
        Assert.LessOrEqual(System.Math.Abs(h1), SurfaceVariationRules.MaxHueShift);
        float s = SurfaceVariationRules.SatMul("DESERT_CHECKPOINT", "Sand");
        Assert.That(s, Is.InRange(SurfaceVariationRules.MinSatMul, SurfaceVariationRules.MaxSatMul));
        float v = SurfaceVariationRules.ValMul("FOREST_VILLAGE", "Wood");
        Assert.That(v, Is.InRange(SurfaceVariationRules.MinValMul, SurfaceVariationRules.MaxValMul));
        float r = SurfaceVariationRules.RoughDelta("INDUSTRIAL_EAST", "Metal");
        Assert.LessOrEqual(System.Math.Abs(r), SurfaceVariationRules.MaxRoughDelta + 1e-6f);
    }

    [Test]
    public void VariationDiffersBetweenBiomes()
    {
        bool anyDiff = false;
        foreach (string kind in new[] { "Concrete", "Wood", "Metal", "Fabric", "Sand" })
        {
            float a = SurfaceVariationRules.ValMul("MEDIUM_TOWN", kind);
            float b = SurfaceVariationRules.ValMul("SUBARCTIC_COMPOUND", kind);
            if (System.Math.Abs(a - b) > 1e-4f) { anyDiff = true; break; }
        }
        Assert.IsTrue(anyDiff, "biome variation must differ somewhere");
    }

    [Test]
    public void ApplyVariationKeepsAlphaAndHueWraps()
    {
        var c = new Color(0.99f, 0.5f, 0.25f, 0.75f);
        Color out1 = SurfaceVariationRules.ApplyVariation(c, "MEDIUM_TOWN", "Concrete");
        Assert.AreEqual(0.75f, out1.a, 1e-5f, "alpha preserved");
        // hue wraps: shift near +0.04 past 1.0 must wrap
        Color wrap = SurfaceVariationRules.ApplyVariation(new Color(0.99f, 0.99f, 0.25f, 1f), "MEDIUM_TOWN", "Concrete");
        Assert.GreaterOrEqual(wrap.r, 0f);
        Assert.LessOrEqual(wrap.r, 1f);
    }

    [Test]
    public void FactoryTierSwitchAffectsResolution()
    {
        var go = new GameObject("tex-factory-test");
        go.SetActive(false); // no lifecycle side effects
        try
        {
            var factory = go.AddComponent<ProceduralSurfaceTextureFactory>();
            factory.SetQualityTier(512);
            Assert.AreEqual(512, factory.GetQualityTierSide());
            factory.SetQualityTier(2048);
            Assert.AreEqual(2048, factory.GetQualityTierSide());
            factory.SetVariationSeed(777);
            Assert.AreEqual(777, factory.GetVariationSeed());
            Assert.AreEqual(string.Empty, factory.GetVariationBiome(), "no biome set by default");
            factory.SetVariationBiome("MEDIUM_TOWN");
            Assert.AreEqual("MEDIUM_TOWN", factory.GetVariationBiome());
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
