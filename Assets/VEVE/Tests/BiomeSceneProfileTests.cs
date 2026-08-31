using NUnit.Framework;
using UnityEngine;
using VEVE;
using VEVE.Content;

public sealed class BiomeSceneProfileTests
{
    [Test]
    public void FiveBiomesWithPhysicalRangesAndOrder()
    {
        Assert.AreEqual(5, BiomeSceneProfiles.All.Count);
        Assert.IsTrue(BiomeSceneProfiles.TryGet("DESERT_CHECKPOINT", out var desert));
        Assert.IsTrue(BiomeSceneProfiles.TryGet("SUBARCTIC_COMPOUND", out var subarctic));
        Assert.Greater(desert.temperatureC, subarctic.temperatureC, "desert hotter than arctic");
        Assert.Less(subarctic.humidity01 * 1f, 0.99f);

        foreach (var p in BiomeSceneProfiles.All)
        {
            Assert.That(p.fogDensityBias, Is.InRange(0f, 1f));
            Assert.That(p.alertPostureBase01, Is.GreaterThan(0f));
            Assert.IsNotEmpty(p.lightingKey);
            Assert.IsNotEmpty(p.biomeKey);
            Assert.GreaterOrEqual(p.propPalette.Length, 2, p.biomeKey + " needs readable props");
        }
    }

    [Test]
    public void UnknownOrEmptyKeyIsSafe()
    {
        Assert.IsTrue(BiomeSceneProfiles.TryGet("", out var def));
        Assert.IsNotEmpty(def.biomeKey);
        Assert.IsFalse(BiomeSceneProfiles.TryGet("MARS_BASE", out _));
    }

    [Test]
    public void ApplyToEnvironmentIsNoThrowNullSafe()
    {
        Assert.DoesNotThrow(() => BiomeSceneProfiles.ApplyTo(null, BiomeSceneProfiles.All[1]));
        Assert.DoesNotThrow(() => BiomeSceneProfiles.ApplyDefault(null));

        var go = new GameObject("env");
        try
        {
            var sim = go.AddComponent<EnvironmentSimulation>();
            BiomeSceneProfiles.TryGet("INDUSTRIAL_EAST", out var east);
            BiomeSceneProfiles.ApplyTo(sim, east);
            Assert.AreEqual(12f, sim.Temperature, 1e-4f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}

