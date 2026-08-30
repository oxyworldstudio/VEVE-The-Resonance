using System.Collections.Generic;
using NUnit.Framework;
using VEVE.Tactics;

/// <summary>
/// B4 encounter director: campaign escalation curves, deterministic FNV fingerprinting, and the
/// TacticalEventHub bridge (FIFO flush, test isolation, seam mapping tables). Pure EditMode.
/// </summary>
public sealed class TacEscalationTests
{
    private static MissionOutcomeInput Outcome(float lossesPct, float intel, float timeS, int insertAlert, int collateral)
    {
        return new MissionOutcomeInput
        {
            squadLossesPct = lossesPct,
            intelCaptured = intel,
            missionTimeSeconds = timeS,
            alertLevelDuringInsert = insertAlert,
            collateralEvents = collateral
        };
    }

    [Test]
    public void AllOutputsStayInsideDocumentedEnvelopes()
    {
        MissionOutcomeInput[] probes =
        {
            Outcome(0f, 0f, 0f, 0, 0),
            Outcome(100f, 500f, 90000f, 4, 99),
            Outcome(float.NaN, float.NaN, float.NaN, int.MinValue, int.MinValue),
            Outcome(-40f, -30f, -2f, -1, -5),
            Outcome(1e6f, 1e6f, 1e6f, int.MaxValue, int.MaxValue)
        };
        foreach (MissionOutcomeInput probe in probes)
        {
            PostureDelta p = CampaignEscalationModel.Compute(probe);
            Assert.GreaterOrEqual(p.patrolDensity01, CampaignEscalationModel.PatrolDensityMin);
            Assert.LessOrEqual(p.patrolDensity01, CampaignEscalationModel.PatrolDensityMax);
            Assert.GreaterOrEqual(p.reactionTimeMult, CampaignEscalationModel.ReactionTimeMultMin);
            Assert.LessOrEqual(p.reactionTimeMult, CampaignEscalationModel.ReactionTimeMultMax);
            Assert.GreaterOrEqual(p.armorLikelihood, CampaignEscalationModel.ArmorLikelihoodMin);
            Assert.LessOrEqual(p.armorLikelihood, CampaignEscalationModel.ArmorLikelihoodMax);
            Assert.IsFalse(float.IsNaN(p.patrolDensity01));
        }
    }

    [Test]
    public void NegativeInputsAreTreatedAsZeroNotReverseTime()
    {
        Assert.AreEqual(CampaignEscalationModel.Compute(Outcome(0f, 0f, 0f, 0, 0)).patrolDensity01,
                        CampaignEscalationModel.Compute(Outcome(-55f, -20f, -99f, -3, -7)).patrolDensity01, 0.0001f);
        PostureDelta neg = CampaignEscalationModel.Compute(Outcome(-55f, -20f, -99f, -3, -7));
        PostureDelta zero = CampaignEscalationModel.Compute(Outcome(0f, 0f, 0f, 0, 0));
        Assert.AreEqual(neg.patrolDensity01, zero.patrolDensity01, 0.0001f);
        Assert.AreEqual(neg.reactionTimeMult, zero.reactionTimeMult, 0.0001f);
        Assert.AreEqual(neg.armorLikelihood, zero.armorLikelihood, 0.0001f);
    }

    [Test]
    public void IntelCapturedNeverLowersNextAlert()
    {
        PostureDelta previous = CampaignEscalationModel.Compute(Outcome(10f, 0f, 600f, 1, 0));
        for (float intel = 5f; intel <= 200f; intel += 5f)
        {
            PostureDelta current = CampaignEscalationModel.Compute(Outcome(10f, intel, 600f, 1, 0));
            Assert.GreaterOrEqual(current.patrolDensity01, previous.patrolDensity01, "more intel lowered patrol density at " + intel);
            Assert.LessOrEqual(current.reactionTimeMult, previous.reactionTimeMult, "more intel slowed enemy reaction at " + intel);
            previous = current;
        }
        // identical conclusion after jitter with a FIXED fingerprint (constant positive factor)
        PostureDelta jitteredPrev = CampaignEscalationModel.ApplyToNextMission("biome.arid_ridge", 1234, Outcome(10f, 0f, 600f, 1, 0));
        PostureDelta jitteredMore = CampaignEscalationModel.ApplyToNextMission("biome.arid_ridge", 1234, Outcome(10f, 80f, 600f, 1, 0));
        Assert.GreaterOrEqual(jitteredMore.patrolDensity01, jitteredPrev.patrolDensity01);
    }

    [Test]
    public void LosingTheSquadEscalatesEveryDimensionMonotonically()
    {
        PostureDelta previous = CampaignEscalationModel.Compute(Outcome(0f, 0f, 300f, 0, 0));
        for (float loss = 10f; loss <= 100f; loss += 10f)
        {
            PostureDelta current = CampaignEscalationModel.Compute(Outcome(loss, 0f, 300f, 0, 0));
            Assert.GreaterOrEqual(current.patrolDensity01, previous.patrolDensity01);
            Assert.GreaterOrEqual(current.armorLikelihood, previous.armorLikelihood);
            Assert.LessOrEqual(current.reactionTimeMult, previous.reactionTimeMult);
            previous = current;
        }
        // collateral events add heat too
        PostureDelta clean = CampaignEscalationModel.Compute(Outcome(5f, 0f, 1200f, 0, 0));
        PostureDelta messy = CampaignEscalationModel.Compute(Outcome(5f, 0f, 1200f, 0, 3));
        Assert.Greater(messy.patrolDensity01, clean.patrolDensity01);
        Assert.Greater(messy.armorLikelihood, clean.armorLikelihood);

        // hostile insertion alert raises patrols and armor and speeds enemy reaction
        PostureDelta quietInsert = CampaignEscalationModel.Compute(Outcome(5f, 0f, 1200f, 0, 0));
        PostureDelta hotInsert = CampaignEscalationModel.Compute(Outcome(5f, 0f, 1200f, 4, 0));
        Assert.Greater(hotInsert.patrolDensity01, quietInsert.patrolDensity01);
        Assert.Greater(hotInsert.armorLikelihood, quietInsert.armorLikelihood);
        Assert.Less(hotInsert.reactionTimeMult, quietInsert.reactionTimeMult);
    }

    [Test]
    public void GhostRaidLeavesEnemyConfusedNotEscalated()
    {
        PostureDelta ghost = CampaignEscalationModel.Compute(Outcome(0f, 0f, 60f, 0, 0));
        Assert.AreEqual(0.10f, ghost.patrolDensity01, 0.0001f, "ambient patrols only");
        Assert.Greater(ghost.reactionTimeMult, 1f, "clean fast raid must leave reaction slower than baseline");
        Assert.Less(ghost.armorLikelihood, 0.2f, "no armored response to a mission that proved nothing");
    }

    [Test]
    public void Fnv1aIsDeterministicAndKeySeedSensitive()
    {
            Assert.AreEqual(CampaignEscalationModel.Fnv1a("biome.green_valley", 7), CampaignEscalationModel.Fnv1a("biome.green_valley", 7));
        Assert.AreNotEqual(CampaignEscalationModel.Fnv1a("biome.green_valley", 7), CampaignEscalationModel.Fnv1a("biome.green_valley", 8));
        Assert.AreNotEqual(CampaignEscalationModel.Fnv1a("biome.green_valley", 7), CampaignEscalationModel.Fnv1a("biome.arid_ridge", 7));
        Assert.AreEqual(CampaignEscalationModel.Fnv1a(null, 0), CampaignEscalationModel.Fnv1a(null, 0));
        for (int seed = -3; seed <= 3; seed++)
        {
            float f = CampaignEscalationModel.Fingerprint01("k" + seed, seed);
            Assert.GreaterOrEqual(f, 0f);
            Assert.Less(f, 1f);
        }
    }

    [Test]
    public void ApplyToNextMissionIsRepeatablePerKeyAndSeed()
    {
        MissionOutcomeInput outcome = Outcome(45f, 22f, 2400f, 2, 1);
        PostureDelta first = CampaignEscalationModel.ApplyToNextMission("biome.urban_ruins", 0xC0FFEE, outcome);
        PostureDelta second = CampaignEscalationModel.ApplyToNextMission("biome.urban_ruins", 0xC0FFEE, outcome);
        Assert.AreEqual(first.patrolDensity01, second.patrolDensity01, 0f);
        Assert.AreEqual(first.reactionTimeMult, second.reactionTimeMult, 0f);
        Assert.AreEqual(first.armorLikelihood, second.armorLikelihood, 0f);

        PostureDelta raw = CampaignEscalationModel.Compute(outcome);
        Assert.AreEqual(raw, CampaignEscalationModel.ApplyToNextMission("biome.urban_ruins", 1, outcome, false), "applyJitter=false must equal the raw curves");
        Assert.AreEqual(raw, CampaignEscalationModel.ApplyToNextMission(string.Empty, 1, outcome), "empty key carries no fingerprint");

        Assert.GreaterOrEqual(first.patrolDensity01, raw.patrolDensity01 * 0.9f - 0.0001f);
        Assert.LessOrEqual(first.patrolDensity01, raw.patrolDensity01 * 1.1f + 0.0001f);
        Assert.AreEqual(raw.reactionTimeMult, first.reactionTimeMult, 0.0001f, "timing curve is never jittered");
    }

    [Test]
    public void HubFlushesQueuesFifoAndStaysEmptyAfterwards()
    {
        var hub = new TacticalEventHub();
        var gotMorale = new List<MoraleEvent>();
        var gotEscalation = new List<string>();
        System.Action<MoralePulse> onMorale = pulse => gotMorale.Add(pulse.moraleEvent);
        System.Action<EscalationPulse> onEscalation = pulse => gotEscalation.Add(pulse.profileKey);
        hub.MoralePulseProduced += onMorale;
        hub.EscalationPulseProduced += onEscalation;

        Assert.IsTrue(hub.EnqueueMoraleBehavior(6, 10.0));  // Flank  -> FlankSpotted
        Assert.IsTrue(hub.EnqueueMoraleBehavior(10, 11.0)); // Revive -> MedicRevive
        Assert.IsTrue(hub.EnqueueMoraleBehavior(11, 12.0)); // Callout-> Reinforced
        Assert.IsFalse(hub.EnqueueMoraleBehavior(5, 13.0)); // Suppress = continuous channel
        Assert.IsFalse(hub.EnqueueMoraleBehavior(3, 14.0)); // FireAt has no direct morale tag
        hub.EnqueueEscalation(new EscalationPulse { profileKey = "biome.a", seed = 1, outcome = Outcome(0f, 0f, 0f, 0, 0) });
        hub.EnqueueEscalation(new EscalationPulse { profileKey = "biome.b", seed = 2, outcome = Outcome(50f, 9f, 900f, 3, 1) });

        Assert.AreEqual(3, hub.PendingMoraleCount);
        Assert.AreEqual(2, hub.PendingEscalationCount);
        Assert.AreEqual(5, hub.Flush());

        CollectionAssert.AreEqual(new[] { MoraleEvent.FlankSpotted, MoraleEvent.MedicRevive, MoraleEvent.Reinforced }, gotMorale);
        CollectionAssert.AreEqual(new[] { "biome.a", "biome.b" }, gotEscalation);
        Assert.AreEqual(0, hub.PendingMoraleCount);
        Assert.AreEqual(0, hub.PendingEscalationCount);
        Assert.AreEqual(0, hub.Flush(), "second flush delivers nothing");
        Assert.AreEqual(5, hub.FlushedCount);

        hub.MoralePulseProduced -= onMorale;
        hub.EscalationPulseProduced -= onEscalation;
        hub.Clear();
        Assert.AreEqual(0, hub.FlushedCount);
    }

    [Test]
    public void HubInstancesAreIsolatedNoStaticBleed()
    {
        var hubA = new TacticalEventHub();
        var hubB = new TacticalEventHub();
        int bDelivered = 0;
        hubB.MoralePulseProduced += pulse => bDelivered++;
        hubA.EnqueueMorale(new MoralePulse { moraleEvent = MoraleEvent.Reinforced, source = PulseSource.EngagementReport, gameTimeSeconds = 1.0 });
        Assert.AreEqual(0, hubB.Flush());
        Assert.AreEqual(0, bDelivered);
        Assert.AreEqual(1, hubA.Flush());
    }

    [Test]
    public void HubQueueOverflowDropsOldestWithCount()
    {
        var hub = new TacticalEventHub();
        int delivered = 0;
        System.Action<MoralePulse> sink = pulse => delivered++;
        hub.MoralePulseProduced += sink;
        for (int i = 0; i < TacticalEventHub.MaxQueueLength + 10; i++)
        {
            hub.EnqueueMorale(new MoralePulse { moraleEvent = MoraleEvent.GoodInitiative, gameTimeSeconds = i });
        }
        Assert.AreEqual(TacticalEventHub.MaxQueueLength, hub.PendingMoraleCount);
        Assert.AreEqual(10, hub.DroppedPulseCount);
        Assert.AreEqual(TacticalEventHub.MaxQueueLength, hub.Flush());
        hub.MoralePulseProduced -= sink;
    }

    [Test]
    public void BehaviorOpMappingMatchesAgentBridgeVocabulary()
    {
        MoraleEvent mapped;
        Assert.IsTrue(TacticalEventHub.TryMapBehaviorOpToMoraleEvent(6, out mapped));
        Assert.AreEqual(MoraleEvent.FlankSpotted, mapped);
        Assert.IsTrue(TacticalEventHub.TryMapBehaviorOpToMoraleEvent(10, out mapped));
        Assert.AreEqual(MoraleEvent.MedicRevive, mapped);
        Assert.IsTrue(TacticalEventHub.TryMapBehaviorOpToMoraleEvent(11, out mapped)); // Callout seam
        Assert.AreEqual(MoraleEvent.Reinforced, mapped);
        Assert.IsFalse(TacticalEventHub.TryMapBehaviorOpToMoraleEvent(0, out mapped));
        Assert.IsFalse(TacticalEventHub.TryMapBehaviorOpToMoraleEvent(255, out mapped));
    }

    [Test]
    public void NoiseSeamMapsLoudnessToSuppressionFlag()
    {
        Assert.IsTrue(TacticalEventHub.HeavySuppressionFromLoudness(TacticalEventHub.MuzzleReportLoudness)); // rifle report 35 pins
        Assert.IsTrue(TacticalEventHub.HeavySuppressionFromLoudness(30f));
        Assert.IsFalse(TacticalEventHub.HeavySuppressionFromLoudness(29.9f));
        Assert.IsFalse(TacticalEventHub.HeavySuppressionFromLoudness(-5f));
        Assert.IsFalse(TacticalEventHub.HeavySuppressionFromLoudness(float.NaN));
    }
}
