using NUnit.Framework;
using UnityEngine;
using VEVE.Comms;
using VEVE.Content;
using VEVE.Net;
using VEVE.Operators;

public sealed class MissionNetMapTests
{
    [Test]
    public void TemplateLookupByCatalogOrder()
    {
        MissionTemplate[] all = MissionContentCatalog.All;
        Assert.AreEqual(0, MissionNetMap.IndexOfTemplate(all[0].id));
        int desert = MissionNetMap.IndexOfTemplate("DESERT_WELLS");
        Assert.GreaterOrEqual(desert, 0);
        Assert.AreEqual(desert, MissionNetMap.IndexOfTemplate("desert_wells"), "case-insensitive");
        Assert.AreEqual(-1, MissionNetMap.IndexOfTemplate("atlantis"));
        Assert.AreEqual(-1, MissionNetMap.IndexOfTemplate(null));
    }

    [Test]
    public void CommandFactoryAssignsHostSenderAndZeroSeqUntilJournal()
    {
        NetCommand c = MissionNetMap.Command(NetCommandType.ShotFired, 1, 1);
        Assert.AreEqual((ushort)MissionNetMap.HostSender, c.senderId);
        Assert.AreEqual(0u, c.seq);
        Assert.AreEqual(NetCommandType.ShotFired, c.type);
        Assert.AreEqual(1, c.i0);
        Assert.AreEqual(1, c.i1);

        var journal = new MissionCommandJournal();
        uint seq = MissionNetMap.AppendToJournal(journal, c);
        Assert.AreEqual(1u, seq);
        Assert.AreEqual(1u, journal.LastSequence);

        NetCommand second = MissionNetMap.Command(NetCommandType.IntelObject);
        Assert.AreEqual(2u, MissionNetMap.AppendToJournal(journal, second));
        Assert.AreEqual(2, journal.Count);
    }

    [Test]
    public void BarkRoundTripPreservesSemanticsAndWorld()
    {
        var world = new Vector3(12.5f, 0f, -4f);
        NetCommand c = MissionNetMap.BarkCommand(OperatorSpecialty.Medic, VoiceEvent.ManDown,
            VoiceStressTier.Panic, reporterId: 77, world, gameClock: 812d);

        Assert.AreEqual(NetCommandType.RadioBark, c.type);
        Assert.IsTrue(MissionNetMap.IsRelayOnly(c));

        RadioBarkEvent bark = MissionNetMap.ToBark(c);
        Assert.AreEqual((int)OperatorSpecialty.Medic, (int)bark.specialty);
        Assert.AreEqual(VoiceEvent.ManDown, bark.voiceEvent);
        Assert.AreEqual(VoiceStressTier.Panic, bark.tier);
        Assert.AreEqual(world.x, bark.worldPosition.x, 1e-5f);
        Assert.AreEqual(world.z, bark.worldPosition.z, 1e-5f);
        Assert.AreNotEqual(0d, bark.gameClock);
    }

    [Test]
    public void BarkToBarkFieldsAreBoundedAndNeverNull()
    {
        NetCommand bad = MissionNetMap.Command(NetCommandType.RadioBark, 9999, -5, 42f, 5f,
            new Vector3(1f, 2f, 3f));
        RadioBarkEvent b = MissionNetMap.ToBark(bad);
        Assert.IsNotNull(b);
        Assert.NotNull(b.text);
        Assert.GreaterOrEqual(b.pitchMultiplier, 1f);
        Assert.GreaterOrEqual(b.speechRateMultiplier, 1f);
        Assert.LessOrEqual(b.pitchMultiplier, 1.6f);
        Assert.AreEqual((int)b.gameClock, bad.frame);
    }

    [Test]
    public void RelayOnlyClassificationForBark()
    {
        Assert.IsTrue(MissionNetMap.IsRelayOnly(new NetCommand { type = NetCommandType.RadioBark }));
        Assert.IsFalse(MissionNetMap.IsRelayOnly(new NetCommand { type = NetCommandType.ShotFired }));
    }

    [Test]
    public void WorldPackRoundTrips()
    {
        Vector3 v = new Vector3(3.75f, -1.5f, 42f);
        Vector3 back = MissionNetMap.Unpack(MissionNetMap.Pack(v));
        Assert.AreEqual(v, back);
    }

    [Test]
    public void ClockToFrameClamps()
    {
        Assert.AreEqual(0, MissionNetMap.SafeClockToFrame(-5d));
        Assert.AreEqual(0, MissionNetMap.SafeClockToFrame(double.NaN));
        Assert.AreEqual(int.MaxValue, MissionNetMap.SafeClockToFrame(double.MaxValue));
        Assert.AreEqual(41, MissionNetMap.SafeClockToFrame(41.9));
    }
}
