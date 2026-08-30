using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VEVE.Catalog;

/// <summary>
/// Data-consistency guarantees for the iconic weapon catalog. Every check is run across ALL
/// catalog entries so any regression fails the suite loudly. The catalog is intentionally plain
/// data (no ScriptableObject), so this runs as a normal editor test with no asset load.
/// </summary>
public sealed class BallisticConsistencyTests
{
    // Spec's own tolerance; mirrors IconicWeaponCatalog.EnergyTolerance.
    private const double EnergyTolerance = 0.08;
    private const double MinPlausibleMass = 0.001;
    private const double MaxPlausibleMass = 0.060;
    private const double MinVelocity = 100;
    private const double MaxVelocity = 1200;

    private static IEnumerable<WeaponSpec> Specs => IconicWeaponCatalog.All;

    [Test]
    public void CatalogExpectsAtLeastFourteenWeapons()
    {
        Assert.GreaterOrEqual(IconicWeaponCatalog.Count, 14,
            "Iconic arsenal must contain at least 14 weapons");
    }

    [Test]
    public void WeaponIdsAreUniqueAndNonEmpty()
    {
        var ids = new List<string>();
        foreach (WeaponSpec s in Specs)
            ids.Add(s.id);

        Assert.IsFalse(ids.Any(string.IsNullOrWhiteSpace), "Some weapon has an empty id");
        Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "Duplicate weapon ids detected");
    }

    [Test]
    public void ListedMuzzleEnergyMatchesHalfMvSquared()
    {
        var bad = new List<string>();
        foreach (WeaponSpec s in Specs)
        {
            if (s.muzzleEnergy <= 0)
            {
                bad.Add($"{s.id}: muzzleEnergy not positive ({s.muzzleEnergy})");
                continue;
            }
            double ke = 0.5 * s.bulletMass * s.muzzleVelocity * s.muzzleVelocity;
            double err = Math.Abs(ke - s.muzzleEnergy) / s.muzzleEnergy;
            if (err > EnergyTolerance)
                bad.Add($"{s.id}: 0.5*m*v^2={ke:F1} J vs listed={s.muzzleEnergy} J (error {err:P1} > 8%)");
        }
        Assert.IsEmpty(bad, "Muzzle-energy invariant violated:\n" + string.Join("\n", bad));
    }

    [Test]
    public void DerivedEnergyAgreesWithPublishedMuzzleEnergy()
    {
        var bad = new List<string>();
        foreach (WeaponSpec s in Specs)
        {
            if (s.publishedMuzzleEnergy <= 0) { bad.Add($"{s.id}: missing publishedMuzzleEnergy"); continue; }
            double ke = 0.5 * s.bulletMass * s.muzzleVelocity * s.muzzleVelocity;
            double err = Math.Abs(ke - s.publishedMuzzleEnergy) / s.publishedMuzzleEnergy;
            if (err > EnergyTolerance)
                bad.Add($"{s.id}: derived={ke:F1} J vs published={s.publishedMuzzleEnergy} J (error {err:P1})");
        }
        Assert.IsEmpty(bad, "Published-energy cross-check failed:\n" + string.Join("\n", bad));
    }

    [Test]
    public void BallisticCoefficientIsPositive()
    {
        var bad = Specs.Where(s => s.ballisticCoefficient <= 0f).Select(s => s.id).ToList();
        Assert.IsEmpty(bad, "Non-positive BC: " + string.Join(", ", bad));
    }

    [Test]
    public void BulletMassIsPlausible()
    {
        var bad = new List<string>();
        foreach (WeaponSpec s in Specs)
        {
            if (s.bulletMass < MinPlausibleMass || s.bulletMass > MaxPlausibleMass)
                bad.Add($"{s.id}: bulletMass {s.bulletMass} kg outside [{MinPlausibleMass}..{MaxPlausibleMass}]");
        }
        Assert.IsEmpty(bad, "Implausible bullet mass:\n" + string.Join("\n", bad));
    }

    [Test]
    public void MuzzleVelocityIsInRange()
    {
        var bad = new List<string>();
        foreach (WeaponSpec s in Specs)
        {
            if (s.muzzleVelocity < MinVelocity || s.muzzleVelocity > MaxVelocity)
                bad.Add($"{s.id}: muzzleVelocity {s.muzzleVelocity} m/s outside [{MinVelocity}..{MaxVelocity}]");
        }
        Assert.IsEmpty(bad, "Out-of-range muzzle velocity:\n" + string.Join("\n", bad));
    }

    [Test]
    public void RangesArePositiveAndOrdered()
    {
        var bad = new List<string>();
        foreach (WeaponSpec s in Specs)
        {
            if (s.effectiveRange <= 0f || s.maximumRange <= 0f || s.maximumRange < s.effectiveRange)
                bad.Add($"{s.id}: bad range ordering (effective {s.effectiveRange}, max {s.maximumRange})");
        }
        Assert.IsEmpty(bad, "Range ordering invariant violated:\n" + string.Join("\n", bad));
    }

    [Test]
    public void MagazineCapacityIsPositive()
    {
        var bad = Specs.Where(s => s.magazineCapacity <= 0).Select(s => s.id).ToList();
        Assert.IsEmpty(bad, "Non-positive magazine capacity: " + string.Join(", ", bad));
    }

    [Test]
    public void FireIntervalIsPositive()
    {
        var bad = Specs.Where(s => s.fireInterval <= 0f).Select(s => s.id).ToList();
        Assert.IsEmpty(bad, "Non-positive fire interval: " + string.Join(", ", bad));
    }

    [Test]
    public void SharedValidatorReportsNoViolations()
    {
        List<string> problems = WeaponSpecValidator.ValidateAll(Specs);
        Assert.IsEmpty(problems, "Validator found issues:\n" + string.Join("\n", problems));
    }

    [Test]
    public void DatabaseLookupsRoundTripEveryEntry()
    {
        foreach (WeaponSpec s in Specs)
        {
            Assert.IsTrue(IconicWeaponCatalog.TryGet(s.id, out WeaponSpec found), $"DB miss: {s.id}");
            Assert.AreEqual(s.displayName, found.displayName, $"DB data mismatch: {s.id}");
        }
    }

    [Test]
    public void DatabaseSearchByRoleAndCaliber()
    {
        Assert.IsNotEmpty(IconicWeaponCatalog.ByRole(WeaponRole.AssaultRifle), "Expected AK-74M/AK-103 etc.");
        Assert.IsNotEmpty(IconicWeaponCatalog.ByRole(WeaponRole.Pistol), "Expected Glock/M1911");
        Assert.IsNotEmpty(IconicWeaponDatabase("5.56"), "Expected multiple 5.56 platforms");
        Assert.IsNotEmpty(IconicWeaponDatabase("7.62x51mm"), "Expected 7.62x51 platforms");
    }

    private static List<WeaponSpec> IconicWeaponDatabase(string caliber) =>
        IconicWeaponCatalog.Database.SearchByCaliber(caliber).ToList();
}
