using NUnit.Framework;
using VEVE;

public sealed class BallisticsTests
{
    [Test] public void ConcreteAbsorbsMoreEnergyThanWood()
    {
        Assert.Greater(Ballistics.EnergyAfterMaterial(100f, SurfaceMaterial.Wood, 1f),
            Ballistics.EnergyAfterMaterial(100f, SurfaceMaterial.Concrete, 1f));
    }

    [Test]
    public void ThickConcreteStopsLowEnergyRound()
    {
        Assert.IsFalse(Ballistics.TryPenetrate(1f, SurfaceMaterial.Concrete, 2f, out _));
    }

    [Test]
    public void ImpactReportsEnergyLossAndPenetration()
    {
        BallisticImpact impact = Ballistics.ResolveImpact(10f, SurfaceMaterial.Wood, 1f);
        Assert.AreEqual(10f, impact.incomingEnergy);
        Assert.Less(impact.remainingEnergy, impact.incomingEnergy);
        Assert.IsTrue(impact.penetrated);
    }

    [Test]
    public void DistanceReducesProjectileEnergy()
    {
        Assert.Greater(Ballistics.EnergyAfterDistance(100f, 0f),
            Ballistics.EnergyAfterDistance(100f, 50f));
    }
}
