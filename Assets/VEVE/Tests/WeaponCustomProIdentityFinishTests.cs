using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VEVE.WeaponCustomPro;

/// <summary>
/// Per-identity record checks: deterministic FNV-1a serials, validation clamps, JSON
/// snapshot round-trip, monotonic zero-drift erosion model and matrix-delegated swap time.
/// </summary>
public sealed class WcpIdentityTests
{
    // Canonical published FNV-1a (32-bit) test vectors — prove the hash is standard FNV-1a.
    [Test]
    public void SerialHashMatchesFnv1aTestVectors()
    {
        Assert.AreEqual(0x811c9dc5u, WeaponInstanceIdentity.ComputeSerial(string.Empty));
        Assert.AreEqual(0xe40c292cu, WeaponInstanceIdentity.ComputeSerial("a"));
        Assert.AreEqual(0xbf9cf968u, WeaponInstanceIdentity.ComputeSerial("foobar"));
    }

    [Test]
    public void SerialIsDeterministicAndSeedSensitive()
    {
        var a = WeaponInstanceIdentity.Create("PLATE-2026-0001", "ak74m");
        var b = WeaponInstanceIdentity.Create("PLATE-2026-0001", "m4a1");
        Assert.AreEqual(a.SerialNumber, b.SerialNumber, "same seed must give the same serial");
        var c = WeaponInstanceIdentity.Create("PLATE-2026-0002", "ak74m");
        Assert.AreNotEqual(a.SerialNumber, c.SerialNumber);
        Assert.AreEqual(WeaponInstanceIdentity.ComputeSerial("PLATE-2026-0001"), a.SerialNumber);
    }

    [Test]
    public void ResealDetectsSeedTampering()
    {
        var id = WeaponInstanceIdentity.Create("MATCH-GROUP-77", "mk14-ebr");
        uint original = id.SerialNumber;
        id.seed = "FORGED-SEED";
        Assert.IsFalse(id.Reseal(), "seed change must be flagged");
        Assert.AreNotEqual(original, id.SerialNumber);
        Assert.IsTrue(id.Reseal(), "after reseal the serial must match the seed again");
    }

    [Test]
    public void SnapshotJsonRoundTripsAllPersistentFields()
    {
        var id = WeaponInstanceIdentity.Create("SNAP-TEST-9", "hk416");
        id.barrelLifeShots = 8211;
        id.fouling = 0.4f;
        id.wear = 0.15f;
        id.zeroClicksElevation = 42;
        id.zeroClicksWindage = 3;
        id.finishId = "cerakote-fde";
        id.railKitId = "arob-30mm-low";
        string json = id.IdentitySnapshotJson();
        Assert.IsNotEmpty(json);
        Assert.IsTrue(WeaponInstanceIdentity.TryFromSnapshotJson(json, out WeaponInstanceIdentity back));
        Assert.AreEqual(id.SerialNumber, back.SerialNumber);
        Assert.AreEqual(id.seed, back.seed);
        Assert.AreEqual(id.weaponId, back.weaponId);
        Assert.AreEqual(8211, back.barrelLifeShots);
        Assert.AreEqual(0.4f, back.fouling, 1e-6f);
        Assert.AreEqual(0.15f, back.wear, 1e-6f);
        Assert.AreEqual(42, back.zeroClicksElevation);
        Assert.AreEqual(3, back.zeroClicksWindage);
        Assert.AreEqual("cerakote-fde", back.finishId);
        Assert.AreEqual("arob-30mm-low", back.railKitId);
        Assert.IsFalse(WeaponInstanceIdentity.TryFromSnapshotJson(null, out _));
        Assert.IsFalse(WeaponInstanceIdentity.TryFromSnapshotJson(string.Empty, out _));
    }

    [Test]
    public void EnsureValidClampsToPhysicalRanges()
    {
        var id = new WeaponInstanceIdentity
        {
            fouling = 7.5f,
            wear = -3f,
            barrelLifeShots = -450,
            zeroClicksElevation = 999999,
        };
        id.EnsureValid();
        Assert.AreEqual(1f, id.fouling, 1e-6f);
        Assert.AreEqual(0f, id.wear, 1e-6f);
        Assert.AreEqual(0, id.barrelLifeShots);
        Assert.AreEqual(ZeroingSystem.MaxTurretClicksPerDirection, id.zeroClicksElevation);
    }

    [Test]
    public void ZeroDriftIsSmallPositiveAndMonotoneInShotsAndWeather()
    {
        Assert.AreEqual(0.0, WeaponInstanceIdentity.DriftClicksFor(0), 1e-12);
        double d100 = WeaponInstanceIdentity.DriftClicksFor(100);
        double d200 = WeaponInstanceIdentity.DriftClicksFor(200);
        Assert.Greater(d100, 0.0, "a little positive creep per 100 shots");
        Assert.Less(d100, 1.0, "must stay far below one detent per magazine");
        Assert.Greater(d200, d100);
        Assert.Greater(WeaponInstanceIdentity.DriftClicksFor(100, 1.0), d100);
        Assert.Greater(WeaponInstanceIdentity.DriftClicksFor(100, 2.0),
                       WeaponInstanceIdentity.DriftClicksFor(100, 1.0));
    }

    [Test]
    public void FiringSessionDetentsDriftAndFlagsReZero()
    {
        var id = WeaponInstanceIdentity.Create("FIELD-RIFLE-1", "m4a1");
        id.ApplyFiringAndWeather(10000, weatherSeverity: 0.4);
        Assert.AreEqual(10000, id.barrelLifeShots);
        Assert.AreEqual(1f, id.fouling, 1e-3f);
        Assert.Greater(id.wear, 0f);
        Assert.Greater(id.zeroClicksElevation, 3, "drift clicks must have detented at least 3 notches");
        Assert.IsTrue(id.ShouldReZero());

        var fresh = WeaponInstanceIdentity.Create("FIELD-RIFLE-2", "m4a1");
        Assert.IsFalse(fresh.ShouldReZero(), "a green rifle does not need a re-zero");
    }

    [Test]
    public void RailKitSwapDelegatesToTheCompatibilityMatrix()
    {
        var id = WeaponInstanceIdentity.Create("SLOT-TEST-1", "m4a1");
        float t = id.RailKitSwapSeconds();
        Assert.Greater(t, 0f, "m4a1 has a quick-detach optic rail");
        Assert.AreEqual(
            VEVE.Catalog.AttachmentCompatibilityMatrix.GetQuickDetachSwapTime(
                "m4a1", VEVE.Customization.AttachmentSlot.Optic),
            t, 1e-6f, "no duplicated timing table allowed");
    }
}

/// <summary>
/// Cosmetic finish catalogue + visual signature mapping checks: registry integrity,
/// region monotonicity and the flavour-band clamps.
/// </summary>
public sealed class WcpFinishTests
{
    [Test]
    public void FinishRegistryIsUniqueAndQueryable()
    {
        Assert.GreaterOrEqual(CosmeticFinishSystem.Count, 8);
        var ids = CosmeticFinishSystem.All.Select(f => f.id).ToList();
        Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (string key in ids)
        {
            Assert.IsTrue(CosmeticFinishSystem.TryGet(key, out CosmeticFinish f));
            Assert.AreEqual(key, f.id);
        }
        Assert.IsFalse(CosmeticFinishSystem.TryGet("chrome-plated", out _));
        Assert.Throws<KeyNotFoundException>(() => CosmeticFinishSystem.Get("chrome-plated"));
    }

    [Test]
    public void SignatureMultipliersStayInFlavourBand()
    {
        string[] biasKeys = { "snow", "desert", "jungle", "woods", "urban", "rain", "totally-unknown" };
        foreach (CosmeticFinish f in CosmeticFinishSystem.All)
        {
            foreach (string k in biasKeys)
            {
                double m = CosmeticFinishSystem.ComputeVisualSignatureMultiplier(f.id, k);
                Assert.GreaterOrEqual(m, CosmeticFinishSystem.MinSignatureMultiplier, $"{f.id}/{k}");
                Assert.LessOrEqual(m, CosmeticFinishSystem.MaxSignatureMultiplier, $"{f.id}/{k}");
            }
        }
        // Unknown finish on unknown weather is pure neutral 1.0.
        Assert.AreEqual(1.0, CosmeticFinishSystem.ComputeVisualSignatureMultiplier("no-such", "no-where"), 1e-9);
    }

    [Test]
    public void WinterWhiteMonotonicityAgainstBackgroundRegions()
    {
        double inSnow = CosmeticFinishSystem.ComputeVisualSignatureMultiplier("winter-white", "snow");
        double inWoods = CosmeticFinishSystem.ComputeVisualSignatureMultiplier("winter-white", "woods");
        double inJungle = CosmeticFinishSystem.ComputeVisualSignatureMultiplier("winter-white", "jungle");
        Assert.Less(inSnow, inWoods, "white camo must read better in snow than woods");
        Assert.Less(inSnow, inJungle);
        Assert.GreaterOrEqual(inJungle, inWoods, "jungle affinity is the worst white background");

        // Mirror case: arid tan helps in desert, not in snow.
        Assert.Less(
            CosmeticFinishSystem.ComputeVisualSignatureMultiplier("cerakote-fde", "desert"),
            CosmeticFinishSystem.ComputeVisualSignatureMultiplier("cerakote-fde", "snow"));
    }

    [Test]
    public void RealismInversionsAreEncodiedInData()
    {
        Assert.IsTrue(CosmeticFinishSystem.TryGet("milspec-black", out CosmeticFinish black));
        Assert.IsTrue(CosmeticFinishSystem.TryGet("cerakote-fde", out CosmeticFinish fde));
        // Black anodising looks invisible to the eye but is BRIGHT in near-IR.
        Assert.Greater(black.irReflectance, fde.irReflectance);
        Assert.Greater(black.irReflectance, 0.5f);
        // H-series ceramics out-wear G-series/phosphate coatings.
        Assert.Greater(fde.pencilHardness, black.pencilHardness);
        Assert.Greater(fde.scratchRevealThreshold, black.scratchRevealThreshold);
    }

    [Test]
    public void ScratchRevealIsMonotoneAndBounded()
    {
        Assert.AreEqual(0.0, CosmeticFinishSystem.ScratchRevealFactor("winter-white", 0.0), 1e-9);
        Assert.AreEqual(1.0, CosmeticFinishSystem.ScratchRevealFactor("winter-white", 1.0), 1e-9);
        Assert.IsTrue(CosmeticFinishSystem.TryGet("winter-white", out CosmeticFinish white));
        Assert.AreEqual(0.0,
            CosmeticFinishSystem.ScratchRevealFactor("winter-white", white.scratchRevealThreshold * 0.99), 1e-9,
            "below the threshold nothing bleeds through");
        double prev = -1.0;
        for (double w = 0.0; w <= 1.0001; w += 0.1)
        {
            double r = CosmeticFinishSystem.ScratchRevealFactor("milspec-black", w);
            Assert.GreaterOrEqual(r, prev);
            prev = r;
        }
    }

    [Test]
    public void BiasFoldMapsKnownWeatherOntoRegions()
    {
        Assert.AreEqual(SignatureRegion.SnowWinter, CosmeticFinishSystem.RegionForBiasKey("snow"));
        Assert.AreEqual(SignatureRegion.AridDesert, CosmeticFinishSystem.RegionForBiasKey("arid"));
        Assert.AreEqual(SignatureRegion.UrbanGrey, CosmeticFinishSystem.RegionForBiasKey("city"));
        Assert.IsNull(CosmeticFinishSystem.RegionForBiasKey("blizzard-hurricane"));
    }
}
