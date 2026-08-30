using System;
using System.Collections.Generic;
using NUnit.Framework;
using VEVE.UI.Personalization;

/// <summary>
/// Pure EditMode coverage for the personalization seam defaults, the gear coverage table
/// invariant (raw per-zone fractions sum <= 1) and the local finish catalogue.
/// </summary>
public sealed class PersSeamsTests
{
    [Test]
    public void DefaultSourcesReturnHarmlessPlaceholders()
    {
        var roster = new DefaultOperatorRosterSource();
        Assert.That(roster.GetOperators(), Is.Not.Null.And.Empty);
        Assert.That(roster.GetTraits(default), Is.Not.Null.And.Empty);

        var gear = new DefaultGearRosterSource();
        Assert.That(gear.GetSlots().Length, Is.EqualTo(GearSlotKey.DefaultSlots.Length));
        Assert.That(gear.GetItems(GearSlotKey.Helmet), Is.Not.Null.And.Empty);
        Assert.That(gear.GetCoveragePercent(GearSlotKey.Helmet), Is.EqualTo(0f));

        var presenter = new DefaultGearLoadoutPresenter();
        Assert.That(presenter.TotalMassKg, Is.EqualTo(0f));
        Assert.That(presenter.MassCapacityKg, Is.GreaterThan(0f));
        Assert.That(presenter.ThermalLoad01, Is.EqualTo(0f));
        Assert.That(presenter.GetCoveragePercent(HitZone.Head), Is.EqualTo(-1f),
            "presenter must signal 'not computed' with -1 so the panel uses the local table");

        var zero = new DefaultZeroingProvider();
        Assert.That(zero.ZeroRangeMeters, Is.EqualTo(DefaultZeroingProvider.FallbackZeroMeters));
        Assert.That(zero.MilPerClick, Is.GreaterThan(0f));
        Assert.That(zero.MoaPerClick, Is.GreaterThan(0f));
    }

    [Test]
    public void GearSlotKeyNormalizesAndComparesCaseInsensitively()
    {
        GearSlotKey a = "helmet";
        GearSlotKey b = " HELMET ";
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        Assert.That(a.ToString(), Is.EqualTo("HELMET"));
    }

    [Test]
    public void CoverageTablePerZoneRawSumsStayBelowOne()
    {
        foreach (HitZone zone in GearCoverageTable.Zones)
        {
            float sum = 0f;
            foreach (GearSlotKey slot in GearSlotKey.DefaultSlots)
            {
                sum += GearCoverageTable.BaseCoverage(slot, zone);
            }
            Assert.That(sum, Is.LessThanOrEqualTo(1f + 1e-6f),
                "slot table fractions for zone " + zone + " must never exceed full coverage");
        }
    }

    [Test]
    public void CoverageTableUnknownSlotOrZoneIsZero()
    {
        Assert.That(GearCoverageTable.BaseCoverage(new GearSlotKey("PARACORD"), HitZone.Head),
            Is.EqualTo(0f));
        Assert.That(GearCoverageTable.BaseCoverage(GearSlotKey.Gloves, HitZone.Legs),
            Is.EqualTo(0f));
    }

    [Test]
    public void AggregateCoverageScalesWithProtectionAndClampsHundred()
    {
        var half = new List<KeyValuePair<GearSlotKey, float>>
        {
            new KeyValuePair<GearSlotKey, float>(GearSlotKey.Helmet, 0.5f),
        };
        // 0.70 head fraction * 0.5 protection = 35 %.
        Assert.That(
            GearCoverageTable.AggregateZoneCoveragePercent(HitZone.Head, half),
            Is.EqualTo(35f).Within(0.01f));

        var oversaturated = new List<KeyValuePair<GearSlotKey, float>>
        {
            new KeyValuePair<GearSlotKey, float>(GearSlotKey.Helmet, 5f), // clamped to 1
            new KeyValuePair<GearSlotKey, float>(GearSlotKey.FaceShield, 5f),
            new KeyValuePair<GearSlotKey, float>(GearSlotKey.EarPro, 5f),
        };
        Assert.That(
            GearCoverageTable.AggregateZoneCoveragePercent(HitZone.Head, oversaturated),
            Is.EqualTo(100f).Within(0.01f));
        Assert.That(
            GearCoverageTable.AggregateZoneCoveragePercent(HitZone.Head, null),
            Is.EqualTo(0f));
    }

    [Test]
    public void FinishesCatalogRowsHaveUniqueIdsAndValidHex()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FinishDefinition def in FinishesCatalog.All)
        {
            Assert.That(string.IsNullOrEmpty(def.id), Is.False);
            Assert.That(seen.Add(def.id), Is.True, "duplicate finish id " + def.id);
            Assert.That(def.colorHex, Is.Not.Null.And.Length.EqualTo(6));
            foreach (char c in def.colorHex)
                Assert.That(Uri.IsHexDigit(c), Is.True, def.id + " hex digit");
            Assert.That(string.IsNullOrEmpty(def.irSignatureTag), Is.False);
        }
        Assert.That(FinishesCatalog.All.Length, Is.GreaterThanOrEqualTo(8));
    }

    [Test]
    public void FinishesCatalogTryGetFindsFdeAndRejectsUnknown()
    {
        Assert.That(FinishesCatalog.TryGet("fde", out FinishDefinition fde), Is.True);
        Assert.That(fde.colorHex, Is.EqualTo("B88A5F"));
        Assert.That(FinishesCatalog.TryGet("nope", out _), Is.False);
    }
}
