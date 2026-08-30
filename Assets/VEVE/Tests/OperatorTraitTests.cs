using System;
using System.Collections.Generic;
using NUnit.Framework;
using VEVE.Operators;

public sealed class OperatorTraitTests
{
    [Test]
    public void Catalog_HoldsFourteenTraits_AndValidatesClean()
    {
        TraitDefinition[] definitions = TraitCatalog.AllDefinitions();
        Assert.AreEqual(14, definitions.Length);
        Assert.IsEmpty(TraitCatalog.Validate());
    }

    [Test]
    public void Catalog_UnlockLevelsAreWithinBoundedRange()
    {
        foreach (TraitDefinition definition in TraitCatalog.AllDefinitions())
        {
            Assert.GreaterOrEqual(definition.unlockLevel, 1, definition.id + " unlock level below 1.");
            Assert.LessOrEqual(definition.unlockLevel, TraitCatalog.MaxTraitUnlockLevel, definition.id + " unlock level beyond cap.");
        }
        Assert.AreEqual(1, TraitCatalog.UnlockLevel(OperatorTraitId.SteadyHands), "Basic traits are available at enlistment.");
        Assert.AreEqual(8, TraitCatalog.UnlockLevel(OperatorTraitId.AdrenalineJunkie), "Risk/reward traits gate late.");
    }

    [Test]
    public void CanEquipTrait_RespectsProgressionGates()
    {
        OperatorProfile profile = OperatorProfile.Create("Probe", OperatorSpecialty.Pointman, "region.desertcheckpoint", 25, 2);
        Assert.IsFalse(profile.CanEquipTrait(OperatorTraitId.AdrenalineJunkie, 7),
            "Trait must be gated until its unlock level.");
        Assert.IsTrue(profile.CanEquipTrait(OperatorTraitId.AdrenalineJunkie, 8));
        Assert.IsTrue(profile.CanEquipTrait(OperatorTraitId.SteadyHands, 1));
        Assert.IsFalse(profile.CanEquipTrait((OperatorTraitId)99, 99), "Unknown traits are never equippable.");
        Assert.IsNotEmpty(profile.UnlockableTraits(3), "Level three must open at least one trait option.");
    }

    [Test]
    public void Aggregate_EmptySet_IsNeutralOnEveryChannel()
    {
        ChannelVector vector = TraitSet.Aggregate(null);
        for (int i = 0; i <= (int)TraitChannel.SightRange; i++)
        {
            Assert.AreEqual(1f, vector.Get((TraitChannel)i), 0.0001f);
        }
    }

    [Test]
    public void Aggregate_SteadyHands_MatchesAuthoredValues()
    {
        var set = new TraitSet();
        set.Add(OperatorTraitId.SteadyHands);
        ChannelVector vector = set.Aggregate(0f);
        Assert.AreEqual(1.12f, vector.aimStability, 0.0001f);
        Assert.AreEqual(1.08f, vector.swayRecovery, 0.0001f);
        Assert.AreEqual(1f, vector.noiseLoudness, 0.0001f);
    }

    [Test]
    public void Aggregate_KeepsEveryChannelInsideClampEnvelope()
    {
        var set = new TraitSet();
        for (int i = 0; i <= (int)OperatorTraitId.Clumsy; i++)
        {
            set.Add((OperatorTraitId)i);
        }
        ChannelVector harsh = set.Aggregate(1f);
        for (int i = 0; i <= (int)TraitChannel.SightRange; i++)
        {
            Assert.GreaterOrEqual(harsh.Get((TraitChannel)i), ChannelVector.MinAggregate);
            Assert.LessOrEqual(harsh.Get((TraitChannel)i), ChannelVector.MaxAggregate);
        }
    }

    [Test]
    public void AggregateChannel_ClampsAndDefaults()
    {
        Assert.AreEqual(ChannelVector.MaxAggregate, TraitSet.AggregateChannel(3.5f, true));
        Assert.AreEqual(ChannelVector.MinAggregate, TraitSet.AggregateChannel(0.1f, true));
        Assert.AreEqual(1f, TraitSet.AggregateChannel(0.0001f, false), "Channels with no contributions read neutral.");
    }

    [Test]
    public void NightOwlPlusSteadyHands_NightImproveAimAndSightMonotonically()
    {
        var set = new TraitSet();
        set.Add(OperatorTraitId.SteadyHands);
        set.Add(OperatorTraitId.NightOwl);

        ChannelVector day = set.Aggregate(0f);
        ChannelVector dusk = set.Aggregate(0.5f);
        ChannelVector night = set.Aggregate(1f);

        Assert.Greater(night.aimStability, dusk.aimStability);
        Assert.Greater(dusk.aimStability, day.aimStability);
        Assert.Greater(night.sightRange, dusk.sightRange);
        Assert.Greater(dusk.sightRange, day.sightRange);

        var steadyOnly = new TraitSet();
        steadyOnly.Add(OperatorTraitId.SteadyHands);
        Assert.Greater(night.aimStability, steadyOnly.Aggregate(0f).aimStability,
            "NightOwl + SteadyHands must beat SteadyHands alone in aimed shooting after dark.");
    }

    [Test]
    public void Aggregate_RejectsUnboundedContributions()
    {
        var infiniteDef = new TraitDefinition
        {
            id = OperatorTraitId.SteadyHands,
            multipliers = new[]
            {
                new TraitMultiplier { channel = TraitChannel.AimStability, multiplier = float.PositiveInfinity }
            }
        };
        Assert.Throws<ArgumentException>(() => TraitSet.Aggregate(new List<TraitDefinition> { infiniteDef }));

        var nanDef = new TraitDefinition
        {
            id = OperatorTraitId.Scout,
            multipliers = new[]
            {
                new TraitMultiplier { channel = TraitChannel.NoiseLoudness, multiplier = float.NaN }
            }
        };
        Assert.Throws<ArgumentException>(() => TraitSet.Aggregate(new List<TraitDefinition> { nanDef }));
        Assert.IsFalse(TraitSet.ValidateNoUnbounded(new List<TraitDefinition> { nanDef }));
        Assert.IsTrue(TraitSet.ValidateNoUnbounded(TraitCatalog.AllDefinitions()));
    }

    [Test]
    public void TraitSet_AddRejectsDuplicatesAndUnknownIds()
    {
        var set = new TraitSet();
        Assert.IsTrue(set.Add(OperatorTraitId.Scout));
        Assert.IsFalse(set.Add(OperatorTraitId.Scout));
        Assert.IsFalse(set.Add((OperatorTraitId)250));
        Assert.IsTrue(set.Contains(OperatorTraitId.Scout));
        Assert.IsFalse(new TraitSet().Contains(OperatorTraitId.Scout));
    }
}
