using NUnit.Framework;
using UnityEngine;
using VEVE;
using VEVE.Realism;
using VEVE.RealisticPhysics;

/// <summary>
/// EditMode physics validation for the Terminal Velocity Falling System, grounding probe and
/// mass model. All assertions exercise static/pure math paths only â€” no scene objects â€” so the
/// suite runs headless. Verifies terminal-velocity monotonicity in mass, landing-energy
/// monotonicity in fall height, and correct consumption of the signed downward gravity read
/// from <see cref="RealismConfig"/> against the CODATA standard magnitude.
/// </summary>
public sealed class FallingSystemPhysicsTests
{
    private const float Gravity = TerminalVelocityFallingSystem.StandardGravityMagnitude;
    private const float AirDensity = 1.225f;
    private const float Drag = 1.0f;
    private const float Area = 0.7f;

    [Test]
    public void StandardGravityMatchesCodataMagnitude()
    {
        Assert.AreEqual(9.80665f, Gravity, 0.0001f);
        Assert.Greater(Gravity, 0f);
    }

    [Test]
    public void SignedGravityFieldIsDownwardNegative()
    {
        Assert.AreEqual(-9.80665f, TerminalVelocityFallingSystem.ToSignedDownward(9.80665f), 0.0001f);
        Assert.AreEqual(-9.80665f, TerminalVelocityFallingSystem.ToSignedDownward(-9.80665f), 0.0001f);
        Assert.AreEqual(0f, TerminalVelocityFallingSystem.ToSignedDownward(0f), 0.0001f);
    }

    [Test]
    public void ConfiguredRealismGravityIsConsumedAsNegative()
    {
        var config = ScriptableObject.CreateInstance<RealismConfig>();
        try
        {
            Assert.AreEqual(9.80665f, config.StandardGravity, 0.0001f);
            float signed = TerminalVelocityFallingSystem.ToSignedDownward(config.StandardGravity);
            Assert.Less(signed, 0f);
            Assert.AreEqual(-config.StandardGravity, signed, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void TerminalVelocityMatchesAnalyticFormula()
    {
        // v_t = sqrt(2mg / (rho Cd A))
        float expected = Mathf.Sqrt(2f * 80f * Gravity / (AirDensity * Drag * Area));
        Assert.AreEqual(expected, TerminalVelocityFallingSystem.ComputeTerminalVelocity(80f, Gravity, AirDensity, Drag, Area), 0.001f);
    }

    [Test]
    public void TerminalVelocityMonotonicInMass()
    {
        float previous = 0f;
        for (float mass = 40f; mass <= 200f; mass += 10f)
        {
            float vt = TerminalVelocityFallingSystem.ComputeTerminalVelocity(mass, Gravity, AirDensity, Drag, Area);
            Assert.Greater(vt, previous, "Terminal velocity must grow monotonically with mass.");
            previous = vt;
        }
    }

    [Test]
    public void TerminalVelocityScalesWithSqrtOfMass()
    {
        float vt1 = TerminalVelocityFallingSystem.ComputeTerminalVelocity(80f, Gravity, AirDensity, Drag, Area);
        float vt4 = TerminalVelocityFallingSystem.ComputeTerminalVelocity(320f, Gravity, AirDensity, Drag, Area);
        Assert.AreEqual(2f, vt4 / vt1, 0.01f);
    }

    [Test]
    public void LandingEnergyIsInsensitiveToGravitySign()
    {
        float withPositive = TerminalVelocityFallingSystem.ComputeLandingEnergy(20f, 80f, Gravity, AirDensity, Drag, Area);
        float withSignedDownward = TerminalVelocityFallingSystem.ComputeLandingEnergy(20f, 80f, -Gravity, AirDensity, Drag, Area);
        Assert.AreEqual(withPositive, withSignedDownward, 0.001f);
    }

    [Test]
    public void LandingEnergyMonotonicInFallHeight()
    {
        float previous = -1f;
        for (float height = 1f; height <= 30f; height += 1f)
        {
            float energy = TerminalVelocityFallingSystem.ComputeLandingEnergy(height, 80f, Gravity, AirDensity, Drag, Area);
            Assert.GreaterOrEqual(energy, previous, "Landing energy must not decrease with fall height.");
            previous = energy;
        }
        Assert.Greater(TerminalVelocityFallingSystem.ComputeLandingEnergy(30f, 80f, Gravity, AirDensity, Drag, Area),
                        TerminalVelocityFallingSystem.ComputeLandingEnergy(1f, 80f, Gravity, AirDensity, Drag, Area));
    }

    [Test]
    public void LandingEnergyBelowTerminalFollowsHalfMvSquared()
    {
        float height = 5f;
        float expected = 80f * Gravity * height;
        float actual = TerminalVelocityFallingSystem.ComputeLandingEnergy(height, 80f, Gravity, AirDensity, Drag, Area);
        Assert.AreEqual(expected, actual, 0.5f);
    }

    [Test]
    public void LandingEnergySaturatesAtTerminalVelocity()
    {
        float terminal = TerminalVelocityFallingSystem.ComputeTerminalVelocity(80f, Gravity, AirDensity, Drag, Area);
        float expectedSaturation = 0.5f * 80f * terminal * terminal;
        float huge = TerminalVelocityFallingSystem.ComputeLandingEnergy(5000f, 80f, Gravity, AirDensity, Drag, Area);
        float evenHuger = TerminalVelocityFallingSystem.ComputeLandingEnergy(9000f, 80f, Gravity, AirDensity, Drag, Area);
        Assert.AreEqual(expectedSaturation, huge, 1f);
        Assert.AreEqual(huge, evenHuger, 1f);
    }

    [Test]
    public void ZeroOrNegativeFallHeightYieldsZeroEnergyAndDamage()
    {
        Assert.AreEqual(0f, TerminalVelocityFallingSystem.ComputeLandingEnergy(0f, 80f, Gravity, AirDensity, Drag, Area), 0.001f);
        Assert.AreEqual(0f, TerminalVelocityFallingSystem.ComputeLandingEnergy(-3f, 80f, Gravity, AirDensity, Drag, Area), 0.001f);
        Assert.AreEqual(0f, TerminalVelocityFallingSystem.ComputeLandingDamage(100f, 18000f, 100f), 0.001f);
    }

    [Test]
    public void LandingDamageMonotonicInImpactEnergy()
    {
        float tolerance = 18000f;
        float previous = -1f;
        for (float energy = tolerance; energy <= tolerance * 5f; energy += 2000f)
        {
            float damage = TerminalVelocityFallingSystem.ComputeLandingDamage(energy, tolerance, 100f);
            Assert.GreaterOrEqual(damage, previous);
            previous = damage;
        }
        Assert.Greater(TerminalVelocityFallingSystem.ComputeLandingDamage(tolerance * 5f, tolerance, 100f),
                        TerminalVelocityFallingSystem.ComputeLandingDamage(tolerance * 1.5f, tolerance, 100f));
    }

    [Test]
    public void CrouchMitigationHalvesEffectiveFallHeight()
    {
        Assert.AreEqual(5f, TerminalVelocityFallingSystem.ApplyCrouchMitigation(10f, true), 0.0001f);
        Assert.AreEqual(10f, TerminalVelocityFallingSystem.ApplyCrouchMitigation(10f, false), 0.0001f);
        Assert.AreEqual(0f, TerminalVelocityFallingSystem.ApplyCrouchMitigation(-5f, true), 0.0001f);
    }

    [Test]
    public void CrouchMitigationReducesLandingEnergy()
    {
        float raw = TerminalVelocityFallingSystem.ComputeLandingEnergy(10f, 80f, Gravity, AirDensity, Drag, Area);
        float mitigated = TerminalVelocityFallingSystem.ComputeLandingEnergy(
            TerminalVelocityFallingSystem.ApplyCrouchMitigation(10f, true), 80f, Gravity, AirDensity, Drag, Area);
        Assert.Less(mitigated, raw);
    }

    [Test]
    public void LegBucklingThresholdSplitsAtConstantSpeed()
    {
        float threshold = TerminalVelocityFallingSystem.LegBucklingImpactSpeed;
        Assert.IsFalse(TerminalVelocityFallingSystem.ExceedsLegBucklingThreshold(threshold - 1f));
        Assert.IsTrue(TerminalVelocityFallingSystem.ExceedsLegBucklingThreshold(threshold));
        Assert.IsTrue(TerminalVelocityFallingSystem.ExceedsLegBucklingThreshold(threshold + 5f));
        Assert.IsTrue(TerminalVelocityFallingSystem.ExceedsLegBucklingThreshold(-threshold), "Sign of impact velocity must not matter.");
    }

    [Test]
    public void AirtimeFollowsParabolicFreeFallSolution()
    {
        // t = sqrt(2h/g) for a drop from rest
        float height = 19.6133f;
        float expected = Mathf.Sqrt(2f * height / Gravity);
        Assert.AreEqual(expected, TerminalVelocityFallingSystem.ComputeAirtime(height, Gravity), 0.01f);
        Assert.AreEqual(0f, TerminalVelocityFallingSystem.ComputeAirtime(0f, Gravity), 0.0001f);
    }

    [Test]
    public void AirtimeMonotonicInFallHeight()
    {
        float previous = -1f;
        for (float height = 2f; height <= 40f; height += 2f)
        {
            float airtime = TerminalVelocityFallingSystem.ComputeAirtime(height, -Gravity);
            Assert.Greater(airtime, previous, "Airtime must grow monotonically with fall height.");
            previous = airtime;
        }
    }

    [Test]
    public void CoMOffsetAndStanceHeightsMatchLoadModel()
    {
        Assert.AreEqual(Vector3.zero, CharacterMassModel.ComputeCoMOffset(0f));
        Vector3 offset = CharacterMassModel.ComputeCoMOffset(1f);
        Assert.Greater(offset.y, 0f, "Loaded pack raises the center of mass.");
        Assert.Less(offset.z, 0f, "Loaded pack shifts the center of mass rearward.");

        float standing = CharacterMassModel.ComputeCoMHeight(0.95f, 0f, OperatorPosture.Standing);
        float crouched = CharacterMassModel.ComputeCoMHeight(0.95f, 0f, OperatorPosture.Crouched);
        float prone = CharacterMassModel.ComputeCoMHeight(0.95f, 0f, OperatorPosture.Prone);
        Assert.Greater(standing, crouched);
        Assert.Greater(crouched, prone);
    }

    [Test]
    public void InertiaScalesMonotonicallyWithTotalMass()
    {
        float unloaded = CharacterMassModel.ComputeInertia(12f, 80f, 80f, 0f);
        float loaded = CharacterMassModel.ComputeInertia(12f, 110f, 80f, 0.6f);
        Assert.Greater(loaded, unloaded);
        Assert.AreEqual(12f, unloaded, 0.0001f);
    }

    [Test]
    public void SurfaceDatabaseHasRealisticDensities()
    {
        Assert.AreEqual(2400f, PhysicsMaterialDatabase.GetDensity(SurfaceMaterial.Concrete), 0.001f);
        Assert.AreEqual(7850f, PhysicsMaterialDatabase.GetDensity(SurfaceMaterial.Metal), 0.001f);
        Assert.AreEqual(600f, PhysicsMaterialDatabase.GetDensity(SurfaceMaterial.Wood), 0.001f);
        Assert.Greater(PhysicsMaterialDatabase.GetDensity(SurfaceMaterial.Ice), 800f);
        Assert.Less(PhysicsMaterialDatabase.GetDensity(SurfaceMaterial.Fabric), 1000f);
    }

    [Test]
    public void SurfaceFrictionOrderingIsPhysicallySane()
    {
        Assert.Less(PhysicsMaterialDatabase.GetStaticFriction(SurfaceMaterial.Ice),
                     PhysicsMaterialDatabase.GetStaticFriction(SurfaceMaterial.Concrete));
        Assert.Less(PhysicsMaterialDatabase.GetRestitution(SurfaceMaterial.Fabric),
                     PhysicsMaterialDatabase.GetRestitution(SurfaceMaterial.Glass));
    }

    [Test]
    public void SurfaceClassificationMapsRendererNames()
    {
        Assert.AreEqual(SurfaceMaterial.Concrete, PhysicsMaterialDatabase.ClassifyByName("floor_concrete_01"));
        Assert.AreEqual(SurfaceMaterial.Metal, PhysicsMaterialDatabase.ClassifyByName("STEEL_Beam"));
        Assert.AreEqual(SurfaceMaterial.Wood, PhysicsMaterialDatabase.ClassifyByName("Wood Crate (Instance)"));
        Assert.AreEqual(SurfaceMaterial.Ice, PhysicsMaterialDatabase.ClassifyByName("Glacier_Ice_Sheet"));
        Assert.AreEqual(SurfaceMaterial.Dirt, PhysicsMaterialDatabase.ClassifyByName("unrecognised_prop", SurfaceMaterial.Dirt));
    }

    [Test]
    public void ComputeStanceHeightMatchesGroundProbeModel()
    {
        float standing = GroundContactProbe.ComputeStanceHeight(OperatorPosture.Standing, 1.75f);
        float crouched = GroundContactProbe.ComputeStanceHeight(OperatorPosture.Crouched, 1.75f);
        float prone = GroundContactProbe.ComputeStanceHeight(OperatorPosture.Prone, 1.75f);
        Assert.AreEqual(1.75f, standing, 0.0001f);
        Assert.Less(crouched, standing);
        Assert.Less(prone, crouched);
    }
}
