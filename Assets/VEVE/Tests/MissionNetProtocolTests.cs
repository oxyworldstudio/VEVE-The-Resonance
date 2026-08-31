using NUnit.Framework;
using VEVE.Content;
using VEVE.Net;
using VEVE.Scoring;

public sealed class MissionNetProtocolTests
{
    private static int TemplateIndexOf(string id)
    {
        MissionTemplate[] all = MissionContentCatalog.All;
        for (int i = 0; i < all.Length; i++)
            if (string.Equals(all[i].id, id, System.StringComparison.OrdinalIgnoreCase)) return i;
        Assert.Fail("missing template " + id);
        return -1;
    }

    [Test]
    public void JournalAssignsMonotonicSequenceAndReplaysFromAck()
    {
        var j = new MissionCommandJournal();
        uint s1 = j.Append(1, 0, NetCommandType.MissionStart, TemplateIndexOf("desert_wells"), 0, 1f);
        uint s2 = j.Append(1, 5, NetCommandType.ShotFired, 1, 0);
        uint s3 = j.Append(2, 9, NetCommandType.IntelObject);
        Assert.AreEqual(1u, s1);
        Assert.AreEqual(3u, j.LastSequence);
        int tail = 0;
        foreach (NetCommand c in j.Entries) if (c.seq > 1u) tail++;
        Assert.AreEqual(2, tail);
    }

    [Test]
    public void MirrorReplayOfJournalEqualsAuthoritativeSessionExactly()
    {
        int templateIndex = TemplateIndexOf("desert_wells");
        const float elapsed = 410f;

        // -------- HOST: authoritative session driven live
        MissionContentCatalog.TryGet("desert_wells", out MissionTemplate template);
        var host = new MissionSession(template, CampaignDifficulty.Hardened);
        host.Deploy();
        host.SetSquadTotal(2);
        host.SetAlertAtInsert(2);
        for (int i = 0; i < 8; i++) host.RecordShot(i < 5, i == 7); // 5 hits, 1 civilian round
        host.ReportIntelObject();
        host.ReportIntelObject();
        host.ReportContactHeld();
        host.ReportSquadLoss();
        host.SetElapsedForEscalation(elapsed);
        MissionScoreBreakdown hostBreak = host.Complete(elapsed, true);

        // -------- TRANSPORT: same facts as commands
        var journal = new MissionCommandJournal();
        journal.Append(1, 0, NetCommandType.MissionStart, templateIndex, 0, (float)CampaignDifficulty.Hardened);
        journal.Append(1, 30, NetCommandType.SquadTotalSet, 2);
        journal.Append(1, 40, NetCommandType.AlertSet, 2);
        for (int i = 0; i < 8; i++) journal.Append(1, 100 + i * 3, NetCommandType.ShotFired, i < 5 ? 1 : 0, i == 7 ? 1 : 0);
        journal.Append(2, 130, NetCommandType.IntelObject);
        journal.Append(2, 140, NetCommandType.IntelObject);
        journal.Append(1, 150, NetCommandType.ContactHeld);
        journal.Append(3, 160, NetCommandType.SquadMemberKia);
        journal.Append(1, 900, NetCommandType.MissionEnd, 1, 0, elapsed);

        // -------- CLIENT: mirror with 5-frame jitter, then 100% parity expected
        var link = new LoopbackLink();
        int frame = 0;
        var mirror = new NetMissionMirror();
        var batch = new NetCommand[16];
        foreach (NetCommand c in journal.Entries)
            link.Send(c, frame, 1 + (int)(c.seq % 3));
        int guard = 0;
        while ((mirror.AppliedThrough < journal.LastSequence || link.DeliveredCount == 0) && guard++ < 64)
        {
            frame += 1;
            link.Tick(frame);
            int n = link.Drain(batch, batch.Length);
            for (int i = 0; i < n; i++) mirror.Apply(batch[i]);
        }

        Assert.AreEqual(journal.LastSequence, mirror.AppliedThrough);
        Assert.IsTrue(mirror.Finished);
        MissionScoreBreakdown clientBreak = mirror.FinalBreakdown.Value;
        Assert.AreEqual(hostBreak.total, clientBreak.total);
        Assert.AreEqual(hostBreak.experienceReward, clientBreak.experienceReward);
        Assert.AreEqual(hostBreak.rank, clientBreak.rank);
        Assert.AreEqual(hostBreak.stealthBonus, clientBreak.stealthBonus, "civilian round must suppress stealth on BOTH sides");
        Assert.AreEqual(hostBreak.collateralPenalty, clientBreak.collateralPenalty);
    }

    [Test]
    public void LateJoinReplaysFromZeroThenTracksLiveTail()
    {
        var j = new MissionCommandJournal();
        int idx = TemplateIndexOf("industrial_boiler");
        j.Append(1, 0, NetCommandType.MissionStart, idx, 0, 0f);
        j.Append(1, 20, NetCommandType.ShotFired, 1, 0);
        var early = new NetCommand[4];
        int e = 0;
        for (; e < 2; e++) early[e] = j.Entries[e];
        var late = new NetMissionMirror();
        late.ApplyThrough(System.Array.AsReadOnly(early), 2);
        Assert.IsFalse(late.Finished);

        j.Append(1, 500, NetCommandType.MissionEnd, 1, 0, 300f);
        var tail = new NetCommand[4];
        tail[0] = j.Entries[2];
        late.Apply(tail[0]);
        Assert.IsTrue(late.Finished);
        Assert.Greater(late.FinalBreakdown.Value.total, 0);
    }

    [Test]
    public void RadioBarkNeverChangesAuthoritativeState()
    {
        int idx = TemplateIndexOf("desert_wells");

        var plain = new MissionCommandJournal();
        plain.Append(1, 0, NetCommandType.MissionStart, idx, 0, 0f);
        plain.Append(1, 10, NetCommandType.ShotFired, 1, 0);
        plain.Append(1, 20, NetCommandType.MissionEnd, 1, 0, 300f);
        var mirrorPlain = new NetMissionMirror();
        mirrorPlain.ApplyThrough(plain.Entries, plain.LastSequence);

        var noisy = new MissionCommandJournal();
        noisy.Append(1, 0, NetCommandType.MissionStart, idx, 0, 0f);
        noisy.Append(1, 8, NetCommandType.RadioBark, 0, 0, 12f); // chatter must not count
        noisy.Append(1, 10, NetCommandType.ShotFired, 1, 0);
        noisy.Append(1, 15, NetCommandType.RadioBark, 0, 0, 12f);
        noisy.Append(1, 20, NetCommandType.MissionEnd, 1, 0, 300f);
        var mirrorNoisy = new NetMissionMirror();
        mirrorNoisy.ApplyThrough(noisy.Entries, noisy.LastSequence);

        Assert.IsTrue(mirrorPlain.Finished && mirrorNoisy.Finished);
        Assert.AreEqual(mirrorPlain.FinalBreakdown.Value.total, mirrorNoisy.FinalBreakdown.Value.total);
        Assert.AreEqual(mirrorPlain.FinalBreakdown.Value.experienceReward,
            mirrorNoisy.FinalBreakdown.Value.experienceReward);
        // presentation relay still advances applied-through marker (acked, not re-sent)
        Assert.AreEqual(noisy.LastSequence, mirrorNoisy.AppliedThrough);
    }

    [Test]
    public void LoopbackDeliversInFrameThenSequenceOrder()
    {
        var j = new MissionCommandJournal();
        var link = new LoopbackLink();
        uint a = j.Append(1, 0, NetCommandType.MissionStart, 0, 0);
        uint b = j.Append(1, 1, NetCommandType.ShotFired, 1, 0);
        uint c = j.Append(1, 2, NetCommandType.IntelObject);
        link.Send(new NetCommand { seq = c, type = NetCommandType.IntelObject }, 2, 3);
        link.Send(new NetCommand { seq = a, type = NetCommandType.MissionStart }, 0, 0);
        link.Send(new NetCommand { seq = b, type = NetCommandType.ShotFired }, 1, 1);

        var outBuf = new NetCommand[8];
        link.Tick(1);
        Assert.AreEqual(1, link.Drain(outBuf, 8));
        Assert.AreEqual(a, outBuf[0].seq);

        link.Tick(2);
        Assert.AreEqual(1, link.Drain(outBuf, 8));
        Assert.AreEqual(b, outBuf[0].seq);

        link.Tick(5);
        Assert.AreEqual(1, link.Drain(outBuf, 8));
        Assert.AreEqual(c, outBuf[0].seq, "tied frames are delivered in sequence order");
    }
}
