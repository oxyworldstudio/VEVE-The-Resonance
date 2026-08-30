using System;
using NUnit.Framework;
using UnityEngine;
using VEVE.Catalog;
using VEVE.Operators;
using VEVE.WeaponCustomPro;

/// <summary>
/// Player-feel seams for <see cref="OperatorInstance"/>: pure multiplier composition math and
/// the holdover sign convention consumed by the weapon fire loop. No scenes, no objects.
/// </summary>
public sealed class FeelOperatorInstanceTests
{
    [Test]
    public void Combine_NeutralFactors_ReturnNeutral()
    {
        Assert.AreEqual(1f, OperatorInstance.Combine(1f, 1f, 1f), 1e-6f);
    }

    [Test]
    public void Combine_ClampsIntoPositiveEnvelope()
    {
        Assert.AreEqual(OperatorInstance.MaxMultiplier, OperatorInstance.Combine(2.5f, 2.5f, 1f), 1e-4f,
            "Stacked upgrades clamp at the ceiling.");
        Assert.AreEqual(OperatorInstance.MinMultiplier, OperatorInstance.Combine(0.5f, 0.5f, 0.5f), 1e-4f,
            "Stacked penalties clamp at the floor, never to zero or negative.");
        float composed = OperatorInstance.Combine(1.12f, 0.9f, 1f);
        Assert.Greater(composed, 0f);
        Assert.LessOrEqual(composed, OperatorInstance.MaxMultiplier);
        Assert.GreaterOrEqual(composed, OperatorInstance.MinMultiplier);
    }

    [Test]
    public void Combine_RejectsNonFiniteAndNonPositiveFactors_AsNeutral()
    {
        Assert.AreEqual(1f, OperatorInstance.Combine(float.NaN, 1f, 1f), 1e-6f);
        Assert.AreEqual(1f, OperatorInstance.Combine(float.PositiveInfinity, 1f, 1f), 1e-6f);
        Assert.AreEqual(2f, OperatorInstance.Combine(-3f, 2f, 1f), 1e-4f,
            "A poison factor is replaced by neutral 1, leaving 1 * 2 * 1 = 2.");
        Assert.AreEqual(1f, OperatorInstance.Combine(0f, 1f, 1f), 1e-6f);
    }

    [Test]
    public void Combine_TraitAndGearOrderOfCompositionIsIrrelevant()
    {
        float trait = 1.12f;
        float gear = 0.88f;
        float optic = 0.95f;
        Assert.AreEqual(OperatorInstance.Combine(trait, gear, optic), OperatorInstance.Combine(gear, trait, optic), 1e-6f);
        Assert.AreEqual(OperatorInstance.Combine(trait, gear, optic), OperatorInstance.Combine(optic, gear, trait), 1e-6f);
    }

    [Test]
    public void AggregateTraits_NoProfile_IsFullyNeutral()
    {
        ChannelVector vector = OperatorInstance.AggregateTraits(null, 0.5f);
        Assert.NotNull(vector);
        for (int i = 0; i <= (int)TraitChannel.SightRange; i++)
        {
            Assert.AreEqual(1f, vector.Get((TraitChannel)i), 1e-6f, "Channel " + (TraitChannel)i);
        }
    }

    [Test]
    public void AggregateTraits_FoldsCatalogValues()
    {
        OperatorProfile profile = OperatorProfile.Create(
            "Probe", OperatorSpecialty.Pointman, "region.desertcheckpoint", 25, 2, OperatorTraitId.SteadyHands);
        ChannelVector day = OperatorInstance.AggregateTraits(profile, 0f);
        Assert.AreEqual(1.12f, day.Get(TraitChannel.AimStability), 1e-4f, "SteadyHands aim-stability fold.");
        Assert.AreEqual(1.08f, day.Get(TraitChannel.SwayRecovery), 1e-4f, "SteadyHands sway-recovery fold.");
        Assert.AreEqual(1f, day.Get(TraitChannel.NoiseLoudness), 1e-6f, "Untouched channels stay neutral.");
    }

    [Test]
    public void AggregateTraits_NightConditionedBlendAppliesAtDarkness()
    {
        OperatorProfile profile = OperatorProfile.Create(
            "Nocturnal", OperatorSpecialty.Recon, "region.subarcticcompound", 30, 8, OperatorTraitId.NightOwl);
        Assert.AreEqual(0.97f, OperatorInstance.AggregateTraits(profile, 0f).Get(TraitChannel.SightRange), 1e-4f,
            "Daylight: NightOwl pays its acuity penalty.");
        Assert.AreEqual(1.10f, OperatorInstance.AggregateTraits(profile, 1f).Get(TraitChannel.SightRange), 1e-4f,
            "Full darkness: blended to the nocturnal multiplier.");
    }

    [Test]
    public void SampleDarkness_MatchesCivilTwilightCurve()
    {
        Assert.AreEqual(0f, OperatorInstance.SampleDarkness(6f), 1e-6f, "Above civil twilight is full light.");
        Assert.AreEqual(0f, OperatorInstance.SampleDarkness(0f), 1e-6f);
        Assert.AreEqual(0.5f, OperatorInstance.SampleDarkness(-3f), 1e-6f, "Mid-twilight samples half dark.");
        Assert.AreEqual(1f, OperatorInstance.SampleDarkness(-6f), 1e-6f);
        Assert.AreEqual(1f, OperatorInstance.SampleDarkness(-45f), 1e-6f, "Saturates at night.");
        Assert.Greater(OperatorInstance.SampleDarkness(-4f), OperatorInstance.SampleDarkness(-2f),
            "Monotone decreasing in elevation.");
    }

    [Test]
    public void OpticStabilityPenalty_NullScopeIsNeutral()
    {
        Assert.AreEqual(1f, OperatorInstance.OpticStabilityPenalty(null, 1f, 0f), 1e-6f,
            "Identity/rail kit with no optic must not change aim stability.");
    }

    [Test]
    public void OpticStabilityPenalty_HeavyZoomGlassPenalizes()
    {
        var light = new ScopeProfile
        {
            id = "micro",
            weightGrams = 50f,
            lengthMm = 56f,
            magnificationMin = 1f,
            magnificationMax = 1f,
            objectiveDiameterMm = 21f,
            eyeReliefMm = 0f,
        };
        var heavy = new ScopeProfile
        {
            id = "lpvo",
            weightGrams = 660f,
            lengthMm = 265f,
            magnificationMin = 1f,
            magnificationMax = 8f,
            objectiveDiameterMm = 30f,
            eyeReliefMm = 96f,
        };
        float lightPenalty = OperatorInstance.OpticStabilityPenalty(light, 1f, 0f);
        float heavyPenalty = OperatorInstance.OpticStabilityPenalty(heavy, 8f, 12f);
        Assert.Less(heavyPenalty, lightPenalty, "Heavy zoomed glass magnifies tremor more.");
        Assert.GreaterOrEqual(heavyPenalty, OperatorInstance.MinMultiplier);
        Assert.LessOrEqual(lightPenalty, 1f);
    }

    [Test]
    public void Holdover_SignConvention_MatchesRangeCardDocs()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("ak74m", 100.0, 40.0, out RangeCard card),
            "Catalog weapon must resolve a card.");
        Assert.Greater(card.zeroRangeM, 0.0);
        Assert.Greater(card.firstSightLineCrossingM, 0.0);

        Assert.AreEqual(0.0, ZeroingSystem.ComputeHoldoverMoa(card, card.zeroRangeM), 1e-6,
            "Holdover is exactly zero at the zero distance.");

        double nearMidD = card.firstSightLineCrossingM * 0.5;
        Assert.Greater(ZeroingSystem.ComputeHoldoverMoa(card, nearMidD), 0.0,
            "Inside the near crossing the round rides under the sight line: aim above -> positive holdover.");

        double crownD = 0.5 * (card.firstSightLineCrossingM + card.zeroRangeM);
        Assert.Less(ZeroingSystem.ComputeHoldoverMoa(card, crownD), 0.0,
            "Between the crossings the trajectory crowns high: aim low vs card docs -> negative holdover.");

        Assert.Greater(ZeroingSystem.ComputeHoldoverMoa(card, card.zeroRangeM * 2.0), 0.0,
            "Beyond the zero the ball drops: hold above -> positive.");
        Assert.Greater(
            ZeroingSystem.ComputeHoldoverMoa(card, card.zeroRangeM * 3.0),
            ZeroingSystem.ComputeHoldoverMoa(card, card.zeroRangeM * 2.0),
            "Holdover grows with range past the zero.");
    }

    [Test]
    public void Holdover_ConvertMoaToRadians_UsesPiOver10800()
    {
        double moa = ZeroingSystem.ComputeHoldoverMoa(
            IconicWeaponCatalog.Get("ak74m"), 100.0, 40.0, 200.0);
        float radians = (float)(moa * Math.PI / 10800.0);
        Assert.Greater(radians, 0f);
        Assert.Less(radians, 0.01f, "A few-MOA hold must stay a tiny angle.");
        Assert.AreEqual(moa * (Math.PI / 10800.0), radians, 1e-7f);
    }
}
