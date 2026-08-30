using NUnit.Framework;
using VEVE;
using VEVE.Gear;

public sealed class GearProtectionTests
{
    [Test]
    public void NormalHitStopsRoundThatObliqueHitMayNot()
    {
        bool normalStopped = GearProtectionStandard.TryStopAmmunition(ProtectionLevel.NIJ_III, 854f, 3300f, 8f, 0, out float traumaNormal, out float backface);
        bool obliqueStopped = GearProtectionStandard.TryStopAmmunition(ProtectionLevel.NIJ_III, 854f, 3300f, 58f, 0, out float traumaOblique, out _);
        Assert.That(backface, Is.GreaterThan(0f));
        Assert.That(traumaNormal, Is.LessThanOrEqualTo(traumaOblique + 0.001f));
        Assert.That(obliqueStopped, Is.False, "58-degree strike must exceed the derated III ceiling");
        Assert.That(traumaOblique, Is.EqualTo(3300f), "non-stopped hits transmit their full energy as trauma");
        Assert.That(normalStopped, Is.True, "near-normal strike inside ceiling must stop");
    }

    [Test]
    public void ObliquityFactorIsMonotonicDecreasing()
    {
        float previous = float.MaxValue;
        for (float angle = 0f; angle <= 60f; angle += 3f)
        {
            float factor = GearProtectionStandard.ObliquityDefenseFactor(angle);
            Assert.That(factor, Is.LessThanOrEqualTo(previous + 0.0001f));
            Assert.That(factor, Is.GreaterThanOrEqualTo(0.55f).And.LessThanOrEqualTo(1f));
            previous = factor;
        }
        Assert.That(GearProtectionStandard.ObliquityDefenseFactor(0f), Is.EqualTo(1f));
        Assert.That(GearProtectionStandard.ObliquityDefenseFactor(90f), Is.EqualTo(GearProtectionStandard.ObliquityDefenseFactor(60f)).Within(0.0001f));
    }

    [Test]
    public void AngleMonotonicityStoppingIsNested()
    {
        ProtectionLevel level = ProtectionLevel.NIJ_IIIA;
        GearProtectionStandard.TryGetLevel(level, out ProtectionLevelData data);
        float normalCeiling = data.stopEnergyJoules / GearProtectionStandard.ObliquityDefenseFactor(12f);
        float obliqueCeiling = data.stopEnergyJoules * GearProtectionStandard.ObliquityDefenseFactor(45f) / GearProtectionStandard.ObliquityDefenseFactor(12f);
        float energy = (normalCeiling + obliqueCeiling) * 0.5f;
        Assert.That(GearProtectionStandard.TryStopAmmunition(level, 0f, energy, 0f, 0, out _, out _), Is.True, "normal incidence stops energy between the ceilings");
        Assert.That(GearProtectionStandard.TryStopAmmunition(level, 0f, energy, 12f, 0, out _, out _), Is.True);
        Assert.That(GearProtectionStandard.TryStopAmmunition(level, 0f, energy, 45f, 0, out _, out _), Is.False, "same energy fails at 45 degrees");
        Assert.That(GearProtectionStandard.TryStopAmmunition(level, 0f, energy * 4f, 0f, 0, out _, out _), Is.False, "if normal fails nothing oblique stops");
    }

    [Test]
    public void TraumaEnergyStaysWithinLevelBudgetForStoppedHits()
    {
        foreach (ProtectionLevelData data in GearProtectionStandard.Levels)
        {
            if (data.level == ProtectionLevel.Unrated) continue;
            Assert.That(GearProtectionStandard.TryStopAmmunition(data.level, 0f, data.stopEnergyJoules * 0.99f, 12f, 0, out float trauma, out float backface), Is.True);
            Assert.That(trauma, Is.InRange(0f, data.traumaEnergyLimitJoules));
            if (data.maxBackfaceMm > 0f)
            {
                Assert.That(backface, Is.GreaterThan(0f), $"{data.label}: rated rows transmit measurable BFD");
                Assert.That(backface, Is.LessThanOrEqualTo(data.maxBackfaceMm));
            }
        }
    }

    [Test]
    public void MultiHitSecondStrikeCeilingIsLower()
    {
        GearProtectionStandard.TryGetLevel(ProtectionLevel.NIJ_IV, out ProtectionLevelData data);
        float energy = data.stopEnergyJoules * (data.multiHitRetention + 1f) * 0.5f;
        Assert.That(GearProtectionStandard.TryStopAmmunition(ProtectionLevel.NIJ_IV, 0f, energy, 12f, 0, out _, out _), Is.True);
        Assert.That(GearProtectionStandard.TryStopAmmunition(ProtectionLevel.NIJ_IV, 0f, energy, 12f, 1, out _, out _), Is.False);
    }

    [Test]
    public void RatedThreatsAreStoppedAtReferenceAngle()
    {
        foreach (ProtectionLevelData data in GearProtectionStandard.Levels)
        {
            if (data.ratedThreats == null) continue;
            for (int i = 0; i < data.ratedThreats.Length; i++)
            {
                ThreatAmmunition threat = data.ratedThreats[i];
                Assert.That(
                    GearProtectionStandard.TryStopAmmunition(data.level, threat.velocityMps, threat.EnergyJoules, GearProtectionStandard.ReferenceAngleDeg, 0, out _, out _),
                    Is.True,
                    $"{data.label} must stop certified {threat.designation} at proof angle");
            }
        }
    }

    [Test]
    public void HigherLevelsStopAtLeastWeakRoundEnergies()
    {
        Assert.That(GearProtectionStandard.TryStopAmmunition(ProtectionLevel.NIJ_II, 0f, 900f, 12f, 0, out _, out _), Is.True);
        Assert.That(GearProtectionStandard.TryStopAmmunition(ProtectionLevel.NIJ_IV, 0f, 900f, 12f, 0, out _, out _), Is.True);
        Assert.That(GearProtectionStandard.TryStopAmmunition(ProtectionLevel.Unrated, 0f, 1f, 0f, 0, out _, out _), Is.False);
    }
}
