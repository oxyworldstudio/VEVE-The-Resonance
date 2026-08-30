using System;
using NUnit.Framework;
using VEVE.Tactics;

/// <summary>
/// B4 encounter director: squad morale state machine + engagement reporter stress/intel rules.
/// Pure EditMode logic — no Unity scene objects, no Time, no statics.
/// </summary>
public sealed class TacMoraleTests
{
    [Test]
    public void MoraleClampsNeverExceedBounds()
    {
        var squad = new SquadMorale(50f, 70f);
        for (int i = 0; i < 30; i++) squad.ProcessEvent(MoraleEvent.Reinforced, i * 100.0);
        Assert.AreEqual(100f, squad.Morale, 0.001f);

        var falling = new SquadMorale(50f, 10f);
        falling.LeaderPresent = false;
        for (int i = 0; i < 200; i++) falling.Tick(1f, true, i);
        Assert.GreaterOrEqual(falling.Morale, 0f);
        Assert.LessOrEqual(falling.Morale, 100f);
        Assert.AreEqual(0f, falling.Morale, 0.001f);
    }

    [Test]
    public void KiaEscalatesInsideBurstWindowAndResetsAfter()
    {
        var squad = new SquadMorale(100f, 60f);
        Assert.AreEqual(SquadMorale.KiaImmediateDelta, squad.ProcessEvent(MoraleEvent.ComradeKia, 10.0), 0.001f);
        Assert.AreEqual(SquadMorale.KiaImmediateDelta + SquadMorale.KiaEscalationDelta, squad.ProcessEvent(MoraleEvent.ComradeKia, 45.0), 0.001f);
        SquadMorale dummy = new SquadMorale(100f, 60f);
        squad.ResetEngagementPhase();
        Assert.AreEqual(SquadMorale.KiaImmediateDelta, squad.ProcessEvent(MoraleEvent.ComradeKia, 200.0), 0.001f);
        Assert.AreEqual(1, squad.RecentKiaChain);
        Assert.AreEqual(-12f, dummy.ProcessEvent(MoraleEvent.ComradeKia, 500.0), 0.001f);

        // window expiry: last KIA at 210, gap > 60 s resets the chain
        Assert.AreEqual(SquadMorale.KiaImmediateDelta, squad.ProcessEvent(MoraleEvent.ComradeKia, 275.0), 0.001f);
    }

    [Test]
    public void EventDeltasMatchDoctrine()
    {
        var squad = new SquadMorale(50f, 50f);
        Assert.AreEqual(SquadMorale.FlankDelta, squad.ProcessEvent(MoraleEvent.FlankSpotted, 1.0), 0.001f);
        Assert.AreEqual(0f, squad.ProcessEvent(MoraleEvent.FlankSpotted, 2.0), 0.001f, "flank is one-shot per engagement phase");
        squad.ResetEngagementPhase();
        Assert.AreEqual(SquadMorale.FlankDelta, squad.ProcessEvent(MoraleEvent.FlankSpotted, 3.0), 0.001f);
        Assert.AreEqual(SquadMorale.ReinforcementDelta, squad.ProcessEvent(MoraleEvent.Reinforced, 4.0), 0.001f);
        Assert.AreEqual(SquadMorale.GoodInitiativeDelta, squad.ProcessEvent(MoraleEvent.GoodInitiative, 5.0), 0.001f);
        Assert.AreEqual(SquadMorale.MedicReviveDelta, squad.ProcessEvent(MoraleEvent.MedicRevive, 6.0), 0.001f);
        Assert.AreEqual(SquadMorale.RegroupDelta, squad.ProcessEvent(MoraleEvent.Regroup, 7.0), 0.001f);
    }

    [Test]
    public void MovementDoctrineBoundaries()
    {
        Assert.AreEqual(MovementOrder.PinnedImmobile, SquadMorale.ComputeMovement(24.99f).order);
        Assert.AreEqual(MovementOrder.HoldAndFire, SquadMorale.ComputeMovement(25f).order);
        Assert.AreEqual(MovementOrder.HoldAndFire, SquadMorale.ComputeMovement(69.99f).order);
        Assert.AreEqual(MovementOrder.Advance, SquadMorale.ComputeMovement(70f).order);
        Assert.IsFalse(SquadMorale.ComputeMovement(54.9f).fireWhileMoving);
        Assert.IsTrue(SquadMorale.ComputeMovement(55f).fireWhileMoving);
        Assert.IsTrue(SquadMorale.ComputeMovement(-10f).canReturnFire, "NaN/negative clamps, pinned men still shoot back");
        Assert.AreEqual(MovementOrder.PinnedImmobile, SquadMorale.ComputeMovement(float.NaN).order);
    }

    [Test]
    public void RoutTriggerIsStrictOnMoraleAndInclusiveOnCasualties()
    {
        Assert.IsTrue(SquadMorale.IsRoutTrigger(17.99f, 40f));
        Assert.IsFalse(SquadMorale.IsRoutTrigger(18f, 100f), "morale boundary is exclusive");
        Assert.IsFalse(SquadMorale.IsRoutTrigger(10f, 39.9f), "casualty boundary is 40 % inclusive");
        Assert.IsTrue(SquadMorale.IsRoutTrigger(10f, 40f));
        Assert.IsFalse(SquadMorale.IsRoutTrigger(50f, 40f));
    }

    [Test]
    public void RoutTriggerFleesLeavingWounded()
    {
        var squad = new SquadMorale();
        squad.ConfigureSquad(8);
        squad.SetKilledCount(3);
        squad.SetWoundedPresent(2);
        squad.RestoreMorale(19.5f); // Pinning band, just above the strict rout line
        squad.CalmBaseline = 10f;
        squad.LeaderPresent = false;
        Assert.AreEqual(62.5f, squad.CasualtiesPct, 0.01f);
        for (int i = 0; i < 20 && squad.State != MoraleState.Routed; i++) squad.Tick(0.1f, true, 40.0 + i);
        Assert.AreEqual(MoraleState.Routed, squad.State, "rout trigger fires once morale crosses below 18 with 62.5 % losses");
        Assert.AreEqual(2, squad.WoundedAbandonedCount, "flees leaving wounded hook");
    }

    [Test]
    public void StateTransitionsAreMonotonicSingleBand()
    {
        var squad = new SquadMorale();
        squad.ConfigureSquad(10);
        squad.RestoreMorale(47f); // Shaken band
        squad.SetKilledCount(2); // 20 % losses: rout trigger (needs 40 %) must NOT arm here
        squad.ProcessEvent(MoraleEvent.FlankSpotted, 1.0); // -15 -> 32: legal single Shaken->Pinning step
        Assert.AreEqual(32f, squad.Morale, 0.001f);
        Assert.AreEqual(MoraleState.Pinning, squad.State);
        Assert.AreNotEqual(MoraleState.Routed, squad.State, "single event may not skip two bands down");

        // sustained suppression from near-Confident walks the ladder one band at a time
        var walking = new SquadMorale(95f, 95f);
        walking.LeaderPresent = true;
        MoraleState previous = walking.State;
        for (int i = 0; i < 60; i++)
        {
            MoraleState next = walking.Tick(1f, true, i);
            Assert.LessOrEqual(Math.Abs((int)next - (int)previous), 1, "state jumped more than one band: " + previous + " -> " + next);
            previous = next;
            if (next == MoraleState.Routed) break;
        }
        Assert.Less(walking.Morale, 95f);
    }

    [Test]
    public void KiaBurstMaySkipDownTwoBandsInOneEvent()
    {
        var squad = new SquadMorale(100f, 40f);
        squad.ProcessEvent(MoraleEvent.ComradeKia, 0.0);
        squad.ProcessEvent(MoraleEvent.ComradeKia, 10.0);
        squad.ProcessEvent(MoraleEvent.ComradeKia, 20.0);
        squad.ProcessEvent(MoraleEvent.ComradeKia, 30.0); // chain 4: -24, burst armed
        squad.SetKilledCount(0); // keep rout trigger out of the way; we test the ladder skip only
        squad.RestoreMorale(86f); // back to Confident band with the chain still live
        squad.ProcessEvent(MoraleEvent.ComradeKia, 40.0); // chain 5: -28 -> 58 (Shaken): allowed burst skip
        Assert.AreEqual(MoraleState.Shaken, squad.State, "KIA burst is the documented multi-band exception");
    }

    [Test]
    public void RoutedNeverSelfHealsAboveShakenWithoutRegroup()
    {
        var squad = new SquadMorale(90f, 0f);
        squad.LeaderPresent = false;
        // collapse via sustained suppression so StepState drives Routed and sets the latch
        for (int i = 0; i < 400 && squad.State != MoraleState.Routed; i++) squad.Tick(0.5f, true, i);
        Assert.AreEqual(MoraleState.Routed, squad.State);

        // long recovery against a high baseline: latch must clamp the climb at Shaken
        squad.CalmBaseline = 90f;
        for (int i = 500; i < 800; i++)
        {
            MoraleState s = squad.Tick(0.5f, false, i);
            Assert.GreaterOrEqual((int)s, (int)MoraleState.Shaken, "broke the recovery ladder cap");
        }
        Assert.AreEqual(MoraleState.Shaken, squad.State);

        squad.ProcessEvent(MoraleEvent.Regroup, 900.0);
        for (int i = 1000; i < 1300; i++) squad.Tick(0.5f, false, i);
        Assert.Less((int)squad.State, (int)MoraleState.Shaken, "regroup must permit climbing above Shaken");
    }

    [Test]
    public void LeaderlessSquadDecaysToPinnedUnderSuppression()
    {
        var squad = new SquadMorale(50f, 50f);
        squad.LeaderPresent = false;
        Assert.AreEqual(SquadMorale.LeaderAuthorityFloor, squad.LeaderAuthorityFactor, 0.001f);
        Assert.GreaterOrEqual(squad.Tick(1f, true, 1), MoraleState.Shaken);
        for (int i = 2; i <= 30; i++)
        {
            squad.Tick(1f, true, i);
        }
        Assert.AreEqual(MoraleState.Pinning, squad.State);
        Assert.IsTrue(squad.IsPinned);
        Assert.Less(squad.Morale, 45f);
    }

    [Test]
    public void SuppressionDrainAndFatigueRecoveryRates()
    {
        var squad = new SquadMorale(50f, 50f);
        squad.LeaderPresent = false;
        for (int i = 1; i <= 10; i++) squad.Tick(1f, true, i);
        Assert.AreEqual(42f, squad.Morale, 0.05f, "-0.8/s under heavy suppression");

        var recovering = new SquadMorale(30f, 60f);
        recovering.LeaderRating = 1f;
        Assert.AreEqual(1f, recovering.LeaderAuthorityFactor, 0.001f);
        for (int i = 1; i <= 8; i++) recovering.Tick(1f, false, i);
        // 4 s pre-break rally at 2.0/s (full authority) then 5.0/s once the 5 s contact break trips;
        // 30 -> 38 -> +5/s capped by baseline 60 well inside the 8 s window
        Assert.Greater(recovering.Morale, 30f + 4f * 2f);
        Assert.LessOrEqual(recovering.Morale, 60f);
    }

    [Test]
    public void ZeroAndNegativeDeltaTimesAreSafe()
    {
        var squad = new SquadMorale(50f, 50f);
        squad.LeaderPresent = true;
        float before = squad.Morale;
        squad.Tick(-100f, false, 5.0);
        Assert.AreEqual(before, squad.Morale, 0.001f, "negative dt clamps to 0");
        squad.Tick(float.NaN, false, 5.0);
        Assert.AreEqual(before, squad.Morale, 0.001f);
        squad.Tick(1f, false, 5.0);
        Assert.AreEqual(before, squad.Morale, 0.001f, "baseline == morale: no drift");
    }

    [Test]
    public void ProcessEventClampsInvalidClockTimeWithoutThrowing()
    {
        var squad = new SquadMorale(90f, 70f);
        Assert.AreEqual(SquadMorale.KiaImmediateDelta, squad.ProcessEvent(MoraleEvent.ComradeKia, -5.0), 0.001f);
        // NaN clock clamps to 0 s, still inside the burst window opened above: escalates on the safe side
        Assert.AreEqual(SquadMorale.KiaImmediateDelta + SquadMorale.KiaEscalationDelta, squad.ProcessEvent(MoraleEvent.ComradeKia, double.NaN), 0.001f);
        Assert.AreEqual(2, squad.KilledCount);
        Assert.AreEqual(2, squad.RecentKiaChain);
    }

    [Test]
    public void EngagementRoundsBudgetSanitizesNegativeAndOverflowInputs()
    {
        var reporter = new EngagementReporter();
        var record = reporter.CloseContact(new ContactReportInput
        {
            distanceM = -20f,
            weaponFamilyId = null,
            roundsConsumed = -7,
            roundsOnTarget = 3,
            targetsEngaged = -1,
            targetsKilled = 5,
            targetsFledCount = 2,
            baseOfFireSuccess = false,
            contactDurationSeconds = float.NaN
        });
        Assert.AreEqual(0, record.roundsConsumed);
        Assert.AreEqual(0, record.roundsOnTarget, "hits cannot exceed a zero round budget");
        Assert.AreEqual(0f, record.contactDurationSeconds);
        Assert.AreEqual(DistanceBand.Close, record.band, "negative distance clamps to Close");
        Assert.AreEqual(ContactOutcome.Inconclusive, record.outcome);
        reporter.Reset();
        Assert.AreEqual(0, reporter.ContactCount);

        var r2 = reporter.CloseContact(new ContactReportInput { distanceM = 10, roundsConsumed = 5, roundsOnTarget = 90 });
        Assert.AreEqual(5, r2.roundsOnTarget);
        Assert.AreEqual(1f, r2.hitRatio, 0.001f);
    }

    [Test]
    public void StressDeltaFeedsMoraleWithinEnvelope()
    {
        var reporter = new EngagementReporter();
        var killed = reporter.CloseContact(new ContactReportInput { distanceM = 30, weaponFamilyId = "rifle_assault", roundsConsumed = 8, roundsOnTarget = 8, targetsEngaged = 2, targetsKilled = 2 });
        Assert.AreEqual(ContactOutcome.Killed, killed.outcome);
        Assert.Greater(killed.stressDelta, 0f);

        var held = reporter.CloseContact(new ContactReportInput { distanceM = 30, weaponFamilyId = "mg_generic", roundsConsumed = 100, roundsOnTarget = 0, targetsEngaged = 3 });
        Assert.AreEqual(ContactOutcome.Held, held.outcome);
        Assert.Less(held.stressDelta, 0f);
        Assert.GreaterOrEqual(held.stressDelta, SquadMorale.KiaImmediateDelta * 3f, "documented floor -25 stays survivable against +…");
        Assert.GreaterOrEqual(held.stressDelta, EngagementReporter.StressMin);
        Assert.LessOrEqual(killed.stressDelta, EngagementReporter.StressMax);

        // feed the stress back the way the hub does
        var squad = new SquadMorale(50f, 50f);
        squad.LeaderPresent = false;
        float before = squad.Morale;
        squad.RestoreMorale(before + held.stressDelta);
        Assert.AreEqual(before + held.stressDelta, squad.Morale, 0.001f);

        var fled = reporter.CloseContact(new ContactReportInput { distanceM = 120, weaponFamilyId = "mg_generic", roundsConsumed = 40, roundsOnTarget = 20, targetsEngaged = 4, targetsFledCount = 4 });
        Assert.AreEqual(ContactOutcome.Fled, fled.outcome);
        Assert.Greater(fled.stressDelta, 0f, "breaking the enemy reassures the squad");
        Assert.GreaterOrEqual(fled.suppressionEffectiveness, 0f);
        Assert.LessOrEqual(fled.suppressionEffectiveness, 1f);
    }

    [Test]
    public void IntelValueRewardsProximityOutcomeAndFirstFamilyId()
    {
        var reporter = new EngagementReporter();
        var closeFirst = reporter.CloseContact(new ContactReportInput { distanceM = 20, weaponFamilyId = "sprb", roundsConsumed = 10, roundsOnTarget = 10, targetsEngaged = 1, targetsKilled = 1 });
        Assert.AreEqual(3f + EngagementReporter.IntelPerKill + EngagementReporter.NewFamilyIntel, closeFirst.intelValue, 0.001f);

        var repeatFamilyFar = reporter.CloseContact(new ContactReportInput { distanceM = 500, weaponFamilyId = "sprb", roundsConsumed = 2, roundsOnTarget = 1, targetsEngaged = 1 });
        Assert.Greater(closeFirst.intelValue, repeatFamilyFar.intelValue, "closer contact yields better intel (§3.4)");
        Assert.AreEqual(closeFirst.intelValue + repeatFamilyFar.intelValue, reporter.TotalIntelValue, 0.001f);

        var fleeIntel = reporter.CloseContact(new ContactReportInput { distanceM = 20, weaponFamilyId = "technical_dshk", roundsConsumed = 30, roundsOnTarget = 15, targetsEngaged = 3, targetsFledCount = 3 });
        Assert.AreEqual(3f + 3f * EngagementReporter.IntelPerFleeingTarget + EngagementReporter.NewFamilyIntel, fleeIntel.intelValue, 0.001f, "displacement patterns are the richest intel");
    }

    [Test]
    public void DistanceBandClassificationMatchesDoctrine()
    {
        Assert.AreEqual(DistanceBand.Close, EngagementReporter.ClassifyBand(49.99f));
        Assert.AreEqual(DistanceBand.Medium, EngagementReporter.ClassifyBand(50f));
        Assert.AreEqual(DistanceBand.Medium, EngagementReporter.ClassifyBand(199.9f));
        Assert.AreEqual(DistanceBand.Long, EngagementReporter.ClassifyBand(200f));
        Assert.AreEqual(DistanceBand.Far, EngagementReporter.ClassifyBand(401f));
    }
}
