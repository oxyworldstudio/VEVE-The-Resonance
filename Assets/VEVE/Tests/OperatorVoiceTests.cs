using System;
using System.Collections.Generic;
using NUnit.Framework;
using VEVE.Operators;

public sealed class OperatorVoiceTests
{
    [Test]
    public void GetBark_NeverEmpty_OrSilentHold_ForEveryCombination()
    {
        for (int s = 0; s <= (int)OperatorSpecialty.Pointman; s++)
        {
            for (int e = 0; e <= (int)VoiceEvent.AreaSecure; e++)
            {
                for (int t = 0; t <= (int)VoiceStressTier.Panic; t++)
                {
                    string bark = VoiceKitLibrary.GetBark((OperatorSpecialty)s, (VoiceEvent)e, (VoiceStressTier)t);
                    Assert.IsFalse(string.IsNullOrEmpty(bark),
                        "Empty bark for " + (OperatorSpecialty)s + "/" + (VoiceEvent)e + "/" + (VoiceStressTier)t);
                    Assert.AreNotEqual("Hold transmission.", bark,
                        "Fallback chain failed to resolve for " + (OperatorSpecialty)s + "/" + (VoiceEvent)e);
                }
            }
        }
    }

    [Test]
    public void BarkKeyChain_IsOrderedSpecificToGeneric()
    {
        List<string> chain = VoiceKitLibrary.GetBarkKeyChain(OperatorSpecialty.Medic, VoiceEvent.ManDown, VoiceStressTier.Panic);
        Assert.AreEqual(4, chain.Count);
        Assert.AreEqual("bark.medic.man_down.panic", chain[0]);
        Assert.AreEqual("bark.medic.man_down.calm", chain[1]);
        Assert.AreEqual("bark.generic.man_down.panic", chain[2]);
        Assert.AreEqual("bark.generic.man_down.calm", chain[3]);
    }

    [Test]
    public void SpecialtyBark_OverridesGenericLine()
    {
        string breacher = VoiceKitLibrary.GetBark(OperatorSpecialty.Breacher, VoiceEvent.Breach, VoiceStressTier.Calm);
        string gunner = VoiceKitLibrary.GetBark(OperatorSpecialty.SupportGunner, VoiceEvent.Breach, VoiceStressTier.Calm);
        StringAssert.Contains("hinge", breacher);
        Assert.AreNotEqual(breacher, gunner, "Distinct specialties must not share the same authored bark.");
        Assert.IsTrue(VoiceKitLibrary.HasSpecialtyBark(OperatorSpecialty.Breacher, VoiceEvent.Breach));
        Assert.IsFalse(VoiceKitLibrary.HasSpecialtyBark(OperatorSpecialty.Comms, VoiceEvent.Breach),
            "Comms should fall back to the generic breach line.");
    }

    [Test]
    public void Delivery_PitchAndRateAreStrictlyMonotonicWithStress()
    {
        VoiceDelivery calm = VoiceKitLibrary.GetDelivery(VoiceStressTier.Calm);
        VoiceDelivery urgency = VoiceKitLibrary.GetDelivery(VoiceStressTier.Urgency);
        VoiceDelivery panic = VoiceKitLibrary.GetDelivery(VoiceStressTier.Panic);

        Assert.Greater(urgency.pitchMultiplier, calm.pitchMultiplier);
        Assert.Greater(panic.pitchMultiplier, urgency.pitchMultiplier);
        Assert.Greater(urgency.speechRateMultiplier, calm.speechRateMultiplier);
        Assert.Greater(panic.speechRateMultiplier, urgency.speechRateMultiplier);
        Assert.LessOrEqual(panic.pitchMultiplier, 1.2f, "Radio panic stays within plausible vocal physiology.");
        Assert.LessOrEqual(panic.speechRateMultiplier, 1.4f, "Radio panic stays within plausible cadence.");
    }

    [Test]
    public void RadioTemplates_FallbackAndNineLinePlaceholders()
    {
        Assert.AreEqual(VoiceKitLibrary.GetRadioTemplate("check_in"), VoiceKitLibrary.GetRadioTemplate("bogus_key"),
            "Unknown keys must fall back instead of nulling.");
        string nineLine = VoiceKitLibrary.GetRadioTemplate("nine_line");
        List<string> placeholders = VoiceKitLibrary.ExtractPlaceholders(nineLine);
        Assert.GreaterOrEqual(placeholders.Count, 9);
        Assert.IsTrue(placeholders.Contains("location_grid"));
        Assert.IsTrue(placeholders.Contains("patients_military"));
        Assert.IsTrue(placeholders.Contains("vehicle_type"));
        Assert.AreEqual(0, VoiceKitLibrary.ExtractPlaceholders(null).Count);
    }

    [Test]
    public void Kit_ValidateClean()
    {
        Assert.IsEmpty(VoiceKitLibrary.ValidateKit());
    }
}
