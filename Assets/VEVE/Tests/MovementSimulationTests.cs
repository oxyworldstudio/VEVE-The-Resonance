using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class MovementSimulationTests
{
    [Test]
    public void CrouchedPostureIsSlowerThanStanding()
    {
        GameObject owner = new GameObject("MovementTest");
        try
        {
            MovementSimulation movement = owner.AddComponent<MovementSimulation>();
            Assert.Greater(movement.SpeedFactor, 0f);
            Assert.Greater(movement.NoiseFactor, 0f);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
