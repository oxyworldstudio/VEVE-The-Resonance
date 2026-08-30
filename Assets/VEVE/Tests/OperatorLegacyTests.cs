using System;
using System.Collections.Generic;
using NUnit.Framework;
using VEVE.Operators;

public sealed class OperatorLegacyTests
{
    [Test]
    public void ComputeLegacyBonus_IsNonNegativeAndClampedForExtremeService()
    {
        var record = new KilledInActionRecord
        {
            operatorId = "op.old.dead",
            callsign = "Old-timer",
            familyId = "family.old",
            specialty = "Medic",
            serviceDays = 100000,
            kills = int.MaxValue / 2,
            missionsCompleted = 9999,
            causeOfDeath = "artillery",
            deathDate = "2026-08-30"
        };

        LegacyBonusResult bonus = OperatorLegacySystem.ComputeLegacyBonus(record);
        Assert.GreaterOrEqual(bonus.startingXp, 0);
        Assert.LessOrEqual(bonus.startingXp, LegacyBonusResult.MaxStartingXp);
        Assert.Greater(bonus.startingXp, 0, "A long career must grant something.");
        Assert.LessOrEqual(bonus.mentorshipSkillFloor, SpecialtyRules.MaxSkillFloor);
        Assert.GreaterOrEqual(bonus.mentorshipSkillFloor, 0f);
    }

    [Test]
    public void ComputeLegacyBonus_GrowsWithServiceAndGatesTraitSlot()
    {
        LegacyBonusResult rookie = OperatorLegacySystem.ComputeLegacyBonus(new KilledInActionRecord
        {
            serviceDays = 10,
            missionsCompleted = 1,
            kills = 2
        });
        LegacyBonusResult veteran = OperatorLegacySystem.ComputeLegacyBonus(new KilledInActionRecord
        {
            serviceDays = 400,
            missionsCompleted = 40,
            kills = 60
        });

        Assert.Greater(rookie.startingXp, 0, "Any service is worth an inheritance.");
        Assert.LessOrEqual(rookie.startingXp, LegacyBonusResult.MaxStartingXp);
        Assert.AreEqual(0, rookie.unlockedTraitSlots, "Sub-threshold service earns no bonus slot.");
        Assert.AreEqual(1, veteran.unlockedTraitSlots);
        Assert.Greater(veteran.startingXp, rookie.startingXp);
        Assert.Greater(veteran.mentorshipSkillFloor, rookie.mentorshipSkillFloor);
    }

    [Test]
    public void ComputeLegacyBonus_NullRecord_YieldsZeroBonus()
    {
        LegacyBonusResult bonus = OperatorLegacySystem.ComputeLegacyBonus((KilledInActionRecord)null);
        Assert.AreEqual(0, bonus.startingXp);
        Assert.AreEqual(0, bonus.unlockedTraitSlots);
        Assert.AreEqual(0f, bonus.mentorshipSkillFloor);
        Assert.AreEqual(string.Empty, bonus.sourceRecordId);
    }

    [Test]
    public void ApplyTo_ReturnsModifiedCopy_AndNeverTouchesInputs()
    {
        OperatorProfile replacement = OperatorProfile.Create("Sparrow", OperatorSpecialty.Recon, "region.subarcticcompound", 21, 1);
        var bonus = new LegacyBonusResult
        {
            startingXp = 750,
            unlockedTraitSlots = 1,
            mentorshipSkillFloor = 0.42f,
            sourceRecordId = "op.recon.dead"
        };

        OperatorProfile applied = OperatorLegacySystem.ApplyTo(replacement, bonus, OperatorSpecialty.Recon);

        Assert.AreNotSame(replacement, applied, "ApplyTo must hand back a copy.");
        Assert.AreEqual(0, replacement.startingXpGrant, "The caller's in-hand profile must remain pristine.");
        Assert.AreEqual(0f, replacement.mentorshipSkillFloor);
        Assert.AreEqual(750, applied.startingXpGrant);
        Assert.AreEqual(1, applied.bonusTraitSlots);
        Assert.AreEqual(0.42f, applied.mentorshipSkillFloor, 0.0001f);

        OperatorProfile crossSpecialty = OperatorLegacySystem.ApplyTo(
            replacement, bonus, OperatorSpecialty.Medic);
        Assert.AreEqual(0f, crossSpecialty.mentorshipSkillFloor,
            "Mentorship must not skip across job families.");
        Assert.AreEqual(750, crossSpecialty.startingXpGrant,
            "XP inheritance is family-wide even when mentorship is specialty-gated.");
    }

    [Test]
    public void RecordKia_WritesLedgerAndMemorialLine()
    {
        var system = new OperatorLegacySystem();
        OperatorProfile fallen = OperatorProfile.Create("Raven", OperatorSpecialty.Recon, "region.temperateforestvillage", 31, 9);
        fallen.serviceDays = 3285;
        fallen.confirmedKills = 47;
        fallen.missionsSurvived = 58;

        KilledInActionRecord record = system.RecordKia(fallen, "sniper, tree line west", new DateTime(2026, 8, 30, 4, 20, 0));

        Assert.AreEqual(1, system.LossCount);
        Assert.AreEqual("Raven", record.callsign);
        Assert.AreEqual("2026-08-30", record.deathDate);
        Assert.AreEqual(3285, record.serviceDays);
        Assert.AreEqual(1, system.Roster.memorialEntries.Count);
        StringAssert.Contains("KIA - Raven", system.Roster.memorialEntries[0]);
        StringAssert.Contains("sniper, tree line west", system.Roster.memorialEntries[0]);
        Assert.AreEqual(0, fallen.startingXpGrant, "Recording a KIA must not mutate the living copy.");
    }

    [Test]
    public void CommissionSuccessor_InheritsFromFamilyLoss()
    {
        var system = new OperatorLegacySystem();
        OperatorProfile mentor = OperatorProfile.Create("Raven", OperatorSpecialty.Recon, "region.temperateforestvillage", 31, 9);
        mentor.serviceDays = 800;
        mentor.confirmedKills = 12;
        mentor.missionsSurvived = 21;
        system.RecordKia(mentor, "IED", new DateTime(2026, 5, 2));

        OperatorProfile successor = OperatorProfile.Create("Raven II", OperatorSpecialty.Recon, "region.temperateforestvillage", 23, 1);
        successor.familyId = mentor.familyId;

        OperatorProfile commissioned = system.CommissionSuccessor(successor);

        Assert.AreNotSame(successor, commissioned);
        Assert.Greater(commissioned.startingXpGrant, 0);
        Assert.Greater(commissioned.mentorshipSkillFloor, 0f);

        OperatorProfile orphan = OperatorProfile.Create("Hollow", OperatorSpecialty.Breacher, "region.mediterraneantown", 24, 1);
        OperatorProfile untouched = system.CommissionSuccessor(orphan);
        Assert.AreEqual(0, untouched.startingXpGrant, "Operators with no family losses commission bare.");
    }

    [Test]
    public void SaveRoundTrip_PreservesRecordsAndMemorials()
    {
        var system = new OperatorLegacySystem();
        OperatorProfile fallen = OperatorProfile.Create("Bishop", OperatorSpecialty.Breacher, "region.mediterraneantown", 28, 6);
        fallen.serviceDays = 2190;
        system.RecordKia(fallen, "ambush, stairwell", new DateTime(2026, 3, 14));
        string json = system.ToSaveString();
        Assert.False(string.IsNullOrEmpty(json));

        OperatorLegacySystem restored = OperatorLegacySystem.FromSaveString(json);
        Assert.AreEqual(1, restored.LossCount);
        Assert.AreEqual("Bishop", restored.Roster.records[0].callsign);
        Assert.AreEqual(2190, restored.Roster.records[0].serviceDays);
        Assert.AreEqual(system.Roster.memorialEntries[0], restored.Roster.memorialEntries[0]);
        Assert.IsTrue(restored.LoadFromString(null) == false);
        Assert.AreEqual(0, restored.LossCount, "Empty save text resets to an empty ledger.");
    }
}
