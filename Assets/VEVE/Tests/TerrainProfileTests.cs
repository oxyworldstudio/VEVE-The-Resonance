using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class TerrainProfileTests
{
    [Test]
    public void TerrainProfileHasPositiveMovementAndNoiseFactors()
    {
        TerrainProfile profile = ScriptableObject.CreateInstance<TerrainProfile>();
        try
        {
            Assert.Greater(profile.speedFactor, 0f);
            Assert.GreaterOrEqual(profile.noiseFactor, 0f);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }
}
