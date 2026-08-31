using NUnit.Framework;
using UnityEngine;
using VEVE.Comms;
using VEVE.Operators;
using VEVE.Tactics;

public sealed class RadioNetTests
{
    [Test]
    public void TierProjectionIsMonotonic()
    {
        Assert.AreEqual(VoiceStressTier.Calm, RadioNet.TierFor(MoraleState.Confident));
        Assert.AreEqual(VoiceStressTier.Calm, RadioNet.TierFor(MoraleState.Steady));
        Assert.AreEqual(VoiceStressTier.Urgency, RadioNet.TierFor(MoraleState.Shaken));
        Assert.AreEqual(VoiceStressTier.Panic, RadioNet.TierFor(MoraleState.Pinning));
        Assert.AreEqual(VoiceStressTier.Panic, RadioNet.TierFor(MoraleState.Routed));
        Assert.Less((int)RadioNet.TierFor(MoraleState.Steady), (int)RadioNet.TierFor(MoraleState.Shaken));
        Assert.Less((int)RadioNet.TierFor(MoraleState.Shaken), (int)RadioNet.TierFor(MoraleState.Pinning));
    }

    [Test]
    public void MoraleEventMappingIsCompleteAndSensible()
    {
        Assert.AreEqual(VoiceEvent.ManDown, RadioNet.MapMoraleEvent(MoraleEvent.ComradeKia));
        Assert.AreEqual(VoiceEvent.ContactElevated, RadioNet.MapMoraleEvent(MoraleEvent.FlankSpotted));
        Assert.AreEqual(VoiceEvent.MoveUp, RadioNet.MapMoraleEvent(MoraleEvent.Reinforced));
        Assert.AreEqual(VoiceEvent.Suppressing, RadioNet.MapMoraleEvent(MoraleEvent.GoodInitiative));
        Assert.AreEqual(VoiceEvent.Regroup, RadioNet.MapMoraleEvent(MoraleEvent.Regroup));
        Assert.AreEqual(VoiceEvent.Regroup, RadioNet.MapMoraleEvent(MoraleEvent.MedicRevive));
    }

    [Test]
    public void SpeakerGapPrioritizesContacts()
    {
        Assert.Less(RadioNet.SpeakerGapSeconds(VoiceEvent.ContactFront),
                    RadioNet.SpeakerGapSeconds(VoiceEvent.ManDown));
    }

    [Test]
    public void AllowEnforcesNetDiscipline()
    {
        Assert.IsTrue(RadioNet.Allow(10.0, double.NegativeInfinity, double.NegativeInfinity, VoiceEvent.ContactFront));
        Assert.IsFalse(RadioNet.Allow(10.2, 10.0, double.NegativeInfinity, VoiceEvent.ContactFront), "same shooter cannot double-key contacts");
        Assert.IsTrue(RadioNet.Allow(12.6, 10.0, double.NegativeInfinity, VoiceEvent.ContactFront));
        Assert.IsTrue(RadioNet.Allow(26.0, 19.9, 19.0, VoiceEvent.ManDown), "non-contact bypasses the global contact gate");
        Assert.IsFalse(RadioNet.Allow(19.6, 10.0, 19.0, VoiceEvent.ContactElevated), "one contact owns the net briefly");
        Assert.IsFalse(RadioNet.Allow(double.NaN, 0.0, 0.0, VoiceEvent.ContactFront));
    }

    [Test]
    public void ComposeIsNeverEmptyAndDeliveryIsPhysical()
    {
        RadioBarkEvent calm = RadioNet.Compose(null, OperatorSpecialty.Marksman, VoiceEvent.ContactFront,
            VoiceStressTier.Calm, new Vector3(1f, 2f, 3f), 5.0);
        Assert.AreEqual("NET", calm.speakerId);
        Assert.That(calm.text, Is.Not.Empty);
        Assert.GreaterOrEqual(calm.pitchMultiplier, 1f);
        RadioBarkEvent panic = RadioNet.Compose("Raven", OperatorSpecialty.Breacher, VoiceEvent.ManDown,
            VoiceStressTier.Panic, Vector3.zero, 9.0);
        Assert.GreaterOrEqual(panic.pitchMultiplier, calm.pitchMultiplier);
        Assert.GreaterOrEqual(panic.speechRateMultiplier, calm.speechRateMultiplier);
    }

    [Test]
    public void DispatcherEmitsWithDeterministicClock()
    {
        var go = new GameObject("radio");
        try
        {
            var net = go.AddComponent<RadioDispatcher>();

            Assert.IsTrue(net.BroadcastContact("R-1", OperatorSpecialty.Pointman, false, MoraleState.Shaken, Vector3.zero, 100.0));
            Assert.NotNull(net.LastBark);
            Assert.AreEqual(VoiceEvent.ContactFront, net.LastBark.voiceEvent);
            Assert.AreEqual(VoiceStressTier.Urgency, net.LastBark.tier);

            Assert.IsFalse(net.BroadcastContact("R-1", OperatorSpecialty.Pointman, false, MoraleState.Shaken, Vector3.zero, 100.4),
                "repeat suppressed");
            Assert.IsTrue(net.BroadcastContact("R-1", OperatorSpecialty.Pointman, true, MoraleState.Confident, Vector3.zero, 103.0),
                "elevated contact is a distinct key after gap");

            Assert.IsTrue(net.BroadcastMorale("M-2", OperatorSpecialty.Medic, MoraleEvent.ComradeKia, MoraleState.Pinning, Vector3.zero, 103.1));
            Assert.AreEqual(VoiceEvent.ManDown, net.LastBark.voiceEvent);
            Assert.AreEqual(VoiceStressTier.Panic, net.LastBark.tier);

            net.ResetNet();
            Assert.IsNull(net.LastBark);
        }
        finally
        {
            VEVE.EventBus.ClearAll();
            Object.DestroyImmediate(go);
        }
    }
}
