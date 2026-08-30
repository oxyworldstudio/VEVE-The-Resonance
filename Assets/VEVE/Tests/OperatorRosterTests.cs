using System;
using System.Collections.Generic;
using NUnit.Framework;
using VEVE.Operators;

public sealed class OperatorRosterTests
{
    [Test]
    public void Roster_HasTwelveOperators_WithUniqueCallsignsAndIds()
    {
        List<OperatorProfile> roster = OperatorProfile.CreateDefaultRoster();
        Assert.AreEqual(12, roster.Count);

        var callsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (OperatorProfile profile in roster)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(profile.callsign), "Blank callsign in roster.");
            Assert.IsTrue(callsigns.Add(profile.callsign), "Duplicate callsign " + profile.callsign);
            Assert.IsTrue(ids.Add(profile.operatorId), "Duplicate operatorId " + profile.operatorId);
            Assert.AreEqual(OperatorProfile.ComputeStableId(profile.callsign), profile.operatorId,
                "Id must equal the callsign-derived stable id for " + profile.callsign);
        }
    }

    [Test]
    public void Roster_IsDeterministicAcrossCalls()
    {
        List<OperatorProfile> first = OperatorProfile.CreateDefaultRoster();
        List<OperatorProfile> second = OperatorProfile.CreateDefaultRoster();
        Assert.AreEqual(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.AreEqual(first[i].operatorId, second[i].operatorId, "Id drift at index " + i);
            Assert.AreEqual(first[i].callsign, second[i].callsign, "Callsign drift at index " + i);
            Assert.AreEqual(first[i].defaultSpecialty, second[i].defaultSpecialty, "Specialty drift at index " + i);
            Assert.AreEqual(first[i].restingHeartRateBpm, second[i].restingHeartRateBpm, "Biometric drift at index " + i);
        }
    }

    [Test]
    public void Roster_CoversEverySpecialty()
    {
        List<OperatorProfile> roster = OperatorProfile.CreateDefaultRoster();
        var seen = new HashSet<OperatorSpecialty>();
        foreach (OperatorProfile profile in roster)
        {
            seen.Add(profile.defaultSpecialty);
        }
        for (int i = 0; i <= (int)OperatorSpecialty.Pointman; i++)
        {
            Assert.IsTrue(seen.Contains((OperatorSpecialty)i), "No roster member with specialty " + (OperatorSpecialty)i);
        }
    }

    [Test]
    public void Roster_OriginRegionsAndTraitsAreKnownKeys()
    {
        List<OperatorProfile> roster = OperatorProfile.CreateDefaultRoster();
        foreach (OperatorProfile profile in roster)
        {
            Assert.IsTrue(OperatorProfile.IsKnownRegionKey(profile.originRegionKey),
                profile.callsign + " has an unknown region key.");
            Assert.IsFalse(profile.traits == null || profile.traits.traitIds.Count == 0,
                profile.callsign + " must ship with at least one trait.");
            foreach (OperatorTraitId traitId in profile.traits.traitIds)
            {
                Assert.IsTrue(TraitCatalog.IsDefined(traitId), profile.callsign + " carries undefined trait " + traitId);
                Assert.IsEmpty(profile.CollectWarnings(), profile.callsign + " produced warnings: " + string.Join("; ", profile.CollectWarnings()));
            }
        }
    }

    [Test]
    public void Biometrics_RestingStaysInMedicalBand_AndMaxFollowsAgeMath()
    {
        List<OperatorProfile> roster = OperatorProfile.CreateDefaultRoster();
        foreach (OperatorProfile profile in roster)
        {
            Assert.GreaterOrEqual(profile.restingHeartRateBpm, OperatorProfile.MedicalRestingMinBpm);
            Assert.LessOrEqual(profile.restingHeartRateBpm, OperatorProfile.MedicalRestingMaxBpm);
            Assert.Greater(profile.maxHeartRateBpm, profile.restingHeartRateBpm,
                profile.callsign + " HRmax must exceed resting HR.");
            float expected = OperatorProfile.ComputeMaxHeartRate(profile.ageYears);
            Assert.AreEqual(expected, profile.maxHeartRateBpm, 0.001f, "HRmax must follow HRmax = 208 - 0.7 * age.");
        }

        Assert.Greater(OperatorProfile.ComputeMaxHeartRate(20), OperatorProfile.ComputeMaxHeartRate(40),
            "HRmax decreases with age.");
        Assert.AreEqual(OperatorProfile.ComputeMaxHeartRate(40), OperatorProfile.ComputeMaxHeartRate(40));
    }

    [Test]
    public void StableId_IsCaseInsensitive_AndSensitiveToText()
    {
        Assert.AreEqual(OperatorProfile.ComputeStableId("RAVEN"), OperatorProfile.ComputeStableId("raven"));
        Assert.AreNotEqual(OperatorProfile.ComputeStableId("raven"), OperatorProfile.ComputeStableId("raven2"));
        Assert.AreEqual("op.unnamed.00000000", OperatorProfile.ComputeStableId("  "));
    }

    [Test]
    public void SpecialtyTables_ValidateClean()
    {
        Assert.IsEmpty(SpecialtyRules.ValidateTables());
        Assert.AreNotEqual(0, SpecialtyRules.PreferredAttachmentFamilies(OperatorSpecialty.Medic).Length);
        Assert.IsTrue(SpecialtyRules.ReviveSpeedMultiplier(OperatorSpecialty.Medic)
                      > SpecialtyRules.ReviveSpeedMultiplier(OperatorSpecialty.SupportGunner),
            "Medics must revive faster than gunners by doctrine.");
        Assert.IsTrue(SpecialtyRules.GrenadeUsageBias(OperatorSpecialty.Demolitions)
                      > SpecialtyRules.GrenadeUsageBias(OperatorSpecialty.Recon));
        Assert.IsTrue(SpecialtyRules.SpottingBonus(OperatorSpecialty.Recon)
                      > SpecialtyRules.SpottingBonus(OperatorSpecialty.SupportGunner),
            "Recon leads on spotting value.");
    }
}
