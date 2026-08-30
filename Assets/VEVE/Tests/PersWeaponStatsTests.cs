using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VEVE.Catalog;
using VEVE.Customization;
using VEVE.UI.Personalization;

/// <summary>
/// Unit coverage for the documented clamp/normalization math behind the weapon card stat
/// bars (recoil control, ergonomics proxy, range, damage) and the search/grouping filter.
/// Formulas are defined in <see cref="WeaponStatMath"/> — these tests lock the contract:
///   Normalize(v,min,max)      = clamp01((v-min)/(max-min)), 0 when max &lt;= min
///   RecoilControl01           = 1 - Normalize(recoilImpulse, min, max)
///   ErgonomicsProxy01         = clamp01(0.85 - 0.45*massNorm - 0.25*impulseNorm)
/// </summary>
public sealed class PersWeaponStatsTests
{
    [Test]
    public void NormalizeClampsAndHandlesDegenerateRange()
    {
        Assert.That(WeaponStatMath.Normalize(5f, 0f, 10f), Is.EqualTo(0.5f));
        Assert.That(WeaponStatMath.Normalize(-4f, 0f, 10f), Is.EqualTo(0f));
        Assert.That(WeaponStatMath.Normalize(99f, 0f, 10f), Is.EqualTo(1f));
        Assert.That(WeaponStatMath.Normalize(3f, 4f, 4f), Is.EqualTo(0f),
            "degenerate (max<=min) range must return 0, never NaN");
    }

    [Test]
    public void RecoilControlIsInverseMonotonic()
    {
        // AK-74M impulse 0.62 vs Barrett 5.0: the lighter-kicking gun reads more control.
        Assert.That(WeaponStatMath.RecoilControl01(0.62f, 0.33f, 5f),
            Is.GreaterThan(WeaponStatMath.RecoilControl01(5f, 0.33f, 5f)));
        Assert.That(WeaponStatMath.RecoilControl01(0.33f, 0.33f, 5f), Is.EqualTo(1f).Within(1e-4f));
        Assert.That(WeaponStatMath.RecoilControl01(5f, 0.33f, 5f), Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void ErgonomicsProxyStaysInRangeAndFavoursLightLowKick()
    {
        for (int i = 0; i <= 10; i++)
        {
            float m = i / 10f;
            for (int j = 0; j <= 10; j++)
            {
                float v = WeaponStatMath.ErgonomicsProxy01(m, j / 10f);
                Assert.That(v, Is.InRange(0f, 1f), "ergonomics out of range at " + m + "," + j);
            }
        }
        Assert.That(WeaponStatMath.ErgonomicsProxy01(0f, 0f), Is.EqualTo(0.85f).Within(1e-4f));
        Assert.That(WeaponStatMath.ErgonomicsProxy01(1f, 0f), Is.EqualTo(0.40f).Within(1e-4f));
        Assert.That(WeaponStatMath.ErgonomicsProxy01(0f, 1f), Is.EqualTo(0.60f).Within(1e-4f));
        // Both heavy AND high-recoil saturates at the floor instead of going negative.
        Assert.That(WeaponStatMath.ErgonomicsProxy01(1f, 1f), Is.EqualTo(0.15f).Within(1e-4f));
        // Inputs saturate to [0,1]; the proxy's physical floor for max mass AND max kick is 0.85-0.45-0.25 = 0.15.
        Assert.That(WeaponStatMath.ErgonomicsProxy01(5f, 5f), Is.EqualTo(0.15f).Within(1e-4f), "clamped input");
    }

    [Test]
    public void CatalogBoundsUseRealSpecData()
    {
        float minImpulse = WeaponStatMath.Min(IconicWeaponCatalog.All, s => s.recoilImpulse);
        float maxImpulse = WeaponStatMath.Max(IconicWeaponCatalog.All, s => s.recoilImpulse);
        Assert.That(minImpulse, Is.EqualTo(0.33f).Within(1e-3f), "MP7A1 is the softest kicker");
        Assert.That(maxImpulse, Is.EqualTo(5.0f).Within(1e-3f), "M82A1 is the hardest");
    }

    [Test]
    public void EveryCatalogWeaponProducesValidBars()
    {
        float massMin = WeaponStatMath.Min(IconicWeaponCatalog.All, s => s.weaponMass);
        float massMax = WeaponStatMath.Max(IconicWeaponCatalog.All, s => s.weaponMass);
        float impMin = WeaponStatMath.Min(IconicWeaponCatalog.All, s => s.recoilImpulse);
        float impMax = WeaponStatMath.Max(IconicWeaponCatalog.All, s => s.recoilImpulse);

        foreach (WeaponSpec spec in IconicWeaponCatalog.All)
        {
            Assert.That(WeaponStatMath.RecoilControl01(spec.recoilImpulse, impMin, impMax),
                Is.InRange(0f, 1f), spec.id);
            Assert.That(WeaponStatMath.ErgonomicsProxy01(
                WeaponStatMath.Normalize(spec.weaponMass, massMin, massMax),
                WeaponStatMath.Normalize(spec.recoilImpulse, impMin, impMax)),
                Is.InRange(0f, 1f), spec.id);
        }
    }

    [Test]
    public void SearchFilterMatchesNameCaliberAndRole()
    {
        Assert.That(WeaponCustomizationPanel.MatchesSearch(
            IconicWeaponCatalog.Get("hk416"), "5.56"), Is.True);
        Assert.That(WeaponCustomizationPanel.MatchesSearch(
            IconicWeaponCatalog.Get("svd-dragunov"), "sniper"), Is.True, "role substring");
        Assert.That(WeaponCustomizationPanel.MatchesSearch(
            IconicWeaponCatalog.Get("ak74m"), "unobtanium"), Is.False);
        Assert.That(WeaponCustomizationPanel.MatchesSearch(
            IconicWeaponCatalog.Get("ak74m"), "  "), Is.True, "whitespace = no filter");
    }

    [Test]
    public void SlotStateBridgeMapsManagerFields()
    {
        var state = new WeaponCustomizationState
        {
            weaponId = "m4a1",
            equippedOptic = "optic_holo",
            equippedMuzzle = "muzzle_comp",
        };
        Assert.That(WeaponCustomizationPanel.GetEquipped(state, AttachmentSlot.Optic),
            Is.EqualTo("optic_holo"));
        Assert.That(WeaponCustomizationPanel.GetEquipped(state, AttachmentSlot.Muzzle),
            Is.EqualTo("muzzle_comp"));
        Assert.That(WeaponCustomizationPanel.GetEquipped(state, AttachmentSlot.Stock), Is.Null);
        Assert.That(WeaponCustomizationPanel.GetEquipped(state, AttachmentSlot.Rail), Is.Null,
            "rail accessories are outside WeaponCustomizationManager");
        Assert.That(WeaponCustomizationPanel.SlotKey(AttachmentSlot.Optic), Is.EqualTo("OPTIC"));
    }

    [Test]
    public void DeltaFormattingMatchesDocumentedMultiplierFormula()
    {
        var comp = new AttachmentDefinition
        {
            attachmentId = "muzzle_comp",
            slot = AttachmentSlot.Muzzle,
            recoilModifier = 0.85f,
            rangeModifier = 1.0f,
        };
        string delta = WeaponCustomizationPanel.FormatDelta(comp);
        Assert.That(delta, Does.Contain("RCL -15%"), "(multiplier-1)*100 signed");
        Assert.That(delta, Does.Contain("RNG +0%"));
        Assert.That(delta, Does.Contain("ACC +0%"));
    }
}
