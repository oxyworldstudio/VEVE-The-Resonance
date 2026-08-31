using System;
using NUnit.Framework;
using UnityEngine;
using VEVE.Content;
using VEVE.Gear;
using VEVE.Catalog;
using VEVE.WeaponCustomPro;

public sealed class CatalogPayloadCodecTests
{
    [Test]
    public void WeaponPayloadRoundTripsAllDesignFields()
    {
        Assert.IsTrue(IconicWeaponCatalog.TryGet("m4a1", out WeaponSpec original) || IconicWeaponCatalog.TryGet("M4A1", out original),
            "catalog m4 expected");
        WeaponSpec decoded = WeaponPayloadCodec.Decode(WeaponPayloadCodec.Encode(original));

        Assert.AreEqual(original.id, decoded.id);
        Assert.AreEqual(original.displayName, decoded.displayName);
        Assert.AreEqual(original.muzzleVelocity, decoded.muzzleVelocity, 1e-4f);
        Assert.AreEqual(original.bulletMass, decoded.bulletMass, 1e-9f);
        Assert.AreEqual(original.ballisticCoefficient, decoded.ballisticCoefficient, 1e-6f);
        Assert.AreEqual(original.magazineCapacity, decoded.magazineCapacity);
        Assert.AreEqual(original.fireInterval, decoded.fireInterval, 1e-6f);
        Assert.AreEqual(original.muzzleEnergy, decoded.muzzleEnergy, 1e-6f);
        Assert.AreEqual(original.smoothbore, decoded.smoothbore);
        CollectionAssert.AreEqual(new[] { original.role }, new[] { decoded.role });
    }

    [Test]
    public void ScopePayloadRoundTripsAndMergeRuleWins()
    {
        ScopeProfile original = ScopeCatalog.All[2];
        ScopeProfile decoded = ScopePayloadCodec.Decode(ScopePayloadCodec.Encode(original));
        Assert.AreEqual(original.id, decoded.id);
        Assert.AreEqual(original.magnificationMax, decoded.magnificationMax, 1e-6f);
        Assert.AreEqual(original.boreToOpticCenterlineMm, decoded.boreToOpticCenterlineMm, 1e-6f);
        Assert.AreEqual(original.requiredRail, decoded.requiredRail);

        CatalogItemAsset overrideAsset = ScriptableObject.CreateInstance<CatalogItemAsset>();
        var edited = ScopePayloadCodec.Decode(ScopePayloadCodec.Encode(original));
        edited.boreToOpticCenterlineMm = 91f;
        overrideAsset.Configure(CatalogItemKind.Scope, edited.id, ScopePayloadCodec.Encode(edited));
        try
        {
            var merged = ScopeCatalogSource.Select(new[] { overrideAsset });
            Assert.GreaterOrEqual(merged.Count, ScopeCatalog.All.Count);
            ScopeProfile winner = null;
            foreach (ScopeProfile p in merged) if (p.id == original.id) winner = p;
            Assert.NotNull(winner);
            Assert.AreEqual(91f, winner.boreToOpticCenterlineMm, 1e-6f, "asset overrides builtin");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(overrideAsset);
        }
    }

    [Test]
    public void ScopeNewAssetAppendsAndBadPayloadsIgnored()
    {
        var good = ScriptableObject.CreateInstance<CatalogItemAsset>();
        good.Configure(CatalogItemKind.Scope, "CUSTOM_GLASS", ScopePayloadCodec.Encode(new ScopeProfile
        {
            id = "CUSTOM_GLASS", displayName = "Designer Glass", magnificationMin = 1, magnificationMax = 8,
            boreToOpticCenterlineMm = 45f, elevationClickMoa = 0.5f
        }));
        var junk = ScriptableObject.CreateInstance<CatalogItemAsset>();
        junk.Configure(CatalogItemKind.Scope, string.Empty, "not-a-payload");
        try
        {
            var merged = (System.Collections.Generic.IReadOnlyList<ScopeProfile>)ScopeCatalogSource.Select(new[] { good, junk });
            Assert.AreEqual(ScopeCatalog.All.Count + 1, merged.Count, "appended exactly once, junk skipped");
            Assert.IsTrue(ScopeCatalogSource.Select(null).Count == ScopeCatalog.All.Count);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(good);
            UnityEngine.Object.DestroyImmediate(junk);
        }
    }

    [Test]
    public void GearPayloadRoundTripsCoverageVector()
    {
        GearItem item = GearCatalog.All()[2];
        GearItem decoded = GearPayloadCodec.Decode(GearPayloadCodec.Encode(item));
        Assert.AreEqual(item.id, decoded.id);
        Assert.AreEqual((int)item.slot, (int)decoded.slot);
        Assert.AreEqual((int)item.protectionLevel, (int)decoded.protectionLevel);
        Assert.AreEqual(item.massKg, decoded.massKg, 1e-6f);
        Assert.AreEqual(item.volumeLitres, decoded.volumeLitres, 1e-6f);
        Assert.AreEqual(item.coveragePerZone.Length, decoded.coveragePerZone.Length);
        for (int i = 0; i < GearItem.ZoneCount; i++)
            Assert.AreEqual(item.coveragePerZone[i], decoded.coveragePerZone[i], 1e-6f, "zone " + i);
    }

    [Test]
    public void EmptyDecodeIsSafeEveryKind()
    {
        Assert.IsNotNull(WeaponPayloadCodec.Decode(null));
        Assert.IsNotNull(ScopePayloadCodec.Decode(""));
        Assert.IsNotNull(GearPayloadCodec.Decode(null));
        Assert.AreEqual(string.Empty, WeaponPayloadCodec.Decode("id=").id);
    }
}
