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
        BallisticImpact impact = Ballistics.ResolveImpact(100f, SurfaceMaterial.Wood, 1f);
        Assert.AreEqual(100f, impact.incomingEnergy);
        Assert.Less(impact.remainingEnergy, impact.incomingEnergy);
        Assert.IsTrue(impact.penetrated);
    }

    [Test]
    public void DistanceReducesProjectileEnergy()
    {
        Assert.Greater(Ballistics.EnergyAfterDistance(100f, 0f),
            Ballistics.EnergyAfterDistance(100f, 50f));
    }

    [Test]
    public void GravityDropIncreasesWithDistance()
    {
        Assert.Less(Ballistics.GravityDrop(800f, 10f), Ballistics.GravityDrop(800f, 100f));
    }

    [Test]
    public void WindDriftCalculatesLateralDeviation()
    {
        float drift = Ballistics.WindDrift(100f, 5f, 90f, 800f);
        Assert.Greater(drift, 0f);
    }

    [Test]
    public void PenetrationDepthScalesWithEnergy()
    {
        Assert.Greater(Ballistics.CalculatePenetrationDepth(500f, SurfaceMaterial.Wood),
            Ballistics.CalculatePenetrationDepth(100f, SurfaceMaterial.Wood));
    }

    [Test]
    public void MaterialResistanceValuesAreReasonable()
    {
        Assert.Greater(MaterialDefinition.GetResistance(SurfaceMaterial.Metal),
            MaterialDefinition.GetResistance(SurfaceMaterial.Wood));
    }

    [Test]
    public void AcousticAbsorptionVariesByMaterial()
    {
        Assert.Greater(MaterialDefinition.GetAbsorption(SurfaceMaterial.Fabric),
            MaterialDefinition.GetAbsorption(SurfaceMaterial.Metal));
    }
}
