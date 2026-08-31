using NUnit.Framework;
using VEVE.Procedural;

public sealed class TerrainHeightRulesTests
{
    [Test]
    public void BiomeOrderingAmplitudeMonotonic()
    {
        Assert.Less(TerrainHeightRules.RoughnessFor("MEDIUM_TOWN"),
                    TerrainHeightRules.RoughnessFor("SUBARCTIC_COMPOUND"), "town flatter than arctic");
        Assert.Less(TerrainHeightRules.RoughnessFor("INDUSTRIAL_EAST"),
                    TerrainHeightRules.RoughnessFor("FOREST_VILLAGE"));
        Assert.AreEqual(TerrainHeightRules.RoughnessFor("UNKNOWN_BIOME"), 4, "unknown -> generic roll");
        Assert.Greater(TerrainHeightRules.AmplitudeMetres("SUBARCTIC_COMPOUND"),
                       TerrainHeightRules.AmplitudeMetres("MEDIUM_TOWN"));
    }

    [Test]
    public void DeterministicAndAmplitudeBounded()
    {
        float amp = TerrainHeightRules.AmplitudeMetres("DESERT_CHECKPOINT");
        int first = TerrainHeightRules.HeightMeters(11, 19, 4242, "DESERT_CHECKPOINT").ToString() ==
                    TerrainHeightRules.HeightMeters(11, 19, 4242, "DESERT_CHECKPOINT").ToString() ? 1 : 0;
        Assert.AreEqual(1, first, "same inputs byte-equal");
        for (int s = 0; s < 240; s += 13)
        {
            float h = TerrainHeightRules.HeightMeters(s, s * 3 + 5, 7, "FOREST_VILLAGE");
            Assert.GreaterOrEqual(h, -6f);
            Assert.LessOrEqual(h, 6f);
        }
        Assert.GreaterOrEqual(amp, 0f);
    }

    [Test]
    public void SlopeCurveMonotonicAndFloored()
    {
        Assert.AreEqual(1f, TerrainHeightRules.SlopeFactor(0f), 1e-6f);
        Assert.Less(TerrainHeightRules.SlopeFactor(1f), TerrainHeightRules.SlopeFactor(0.2f));
        Assert.Less(TerrainHeightRules.SlopeFactor(-2f), TerrainHeightRules.SlopeFactor(-1f), "absolute");
        Assert.AreEqual(0.5f, TerrainHeightRules.SlopeFactor(1f), 1e-5f);
        Assert.AreEqual(0.1f, TerrainHeightRules.SlopeFactor(10000f), 1e-4f, "never goes near vertical collapse");
        Assert.AreEqual(1f, TerrainHeightRules.SlopeFactor(float.NaN), "NaN treated as flat-safe");
        Assert.AreEqual(1f, TerrainHeightRules.SlopeFactor(float.PositiveInfinity), "Infinity -> 1, not crash");
    }
}
