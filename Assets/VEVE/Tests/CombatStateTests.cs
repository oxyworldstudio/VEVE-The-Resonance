using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class CombatStateTests
{
    [Test]
    public void CoverStopsInsufficientEnergy()
    {
        GameObject coverObject = new GameObject("CoverTest");
        try
        {
            CoverVolume cover = coverObject.AddComponent<CoverVolume>();
            Assert.IsTrue(cover.Stops(0.01f, out float remaining));
            Assert.AreEqual(0f, remaining);
        }
        finally
        {
            Object.DestroyImmediate(coverObject);
        }
    }

    [Test]
    public void CoverVolumeExposesMaterialAndThickness()
    {
        GameObject coverObject = new GameObject("CoverTest");
        try
        {
            CoverVolume cover = coverObject.AddComponent<CoverVolume>();
            Assert.AreEqual(SurfaceMaterial.Wood, cover.Material);
            Assert.AreEqual(0.3f, cover.Thickness, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(coverObject);
        }
    }
}
