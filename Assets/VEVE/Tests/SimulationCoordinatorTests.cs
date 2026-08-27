using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class SimulationCoordinatorTests
{
    [Test]
    public void CoordinatorCanExistWithoutImplicitState()
    {
        GameObject owner = new GameObject("CoordinatorTest");
        try
        {
            Assert.IsNotNull(owner.AddComponent<SimulationCoordinator>());
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
