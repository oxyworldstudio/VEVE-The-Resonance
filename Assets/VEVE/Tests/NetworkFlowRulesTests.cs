using NUnit.Framework;
using UnityEngine;
using VEVE.Content;
using VEVE.Net;
using VEVE.UI;

public sealed class NetworkFlowRulesTests
{
    [Test]
    public void JoinGateHostOnlyAndCountBounded()
    {
        Assert.IsTrue(NetFlowRules.CanAcceptClient(NetSessionMode.Host, 0, true));
        Assert.IsTrue(NetFlowRules.CanAcceptClient(NetSessionMode.Host, NetFlowRules.MaxClients - 1, true));
        Assert.IsFalse(NetFlowRules.CanAcceptClient(NetSessionMode.Host, NetFlowRules.MaxClients, true),
            "session is full");
        Assert.IsFalse(NetFlowRules.CanAcceptClient(NetSessionMode.Host, -9, true), "negative count sanitizes");
        Assert.IsFalse(NetFlowRules.CanAcceptClient(NetSessionMode.Client, 0, true), "clients never accept");
        Assert.IsFalse(NetFlowRules.CanAcceptClient(NetSessionMode.Offline, 0, true), "offline session does not exist");
        Assert.IsFalse(NetFlowRules.CanAcceptClient(NetSessionMode.Host, 1, false), "no live session, no join");
    }

    [Test]
    public void AuthorityMatrixIsExclusive()
    {
        Assert.IsTrue(NetFlowRules.ShouldRunAuthoritativeLoop(NetSessionMode.Offline));
        Assert.IsTrue(NetFlowRules.ShouldRunAuthoritativeLoop(NetSessionMode.Host));
        Assert.IsFalse(NetFlowRules.ShouldRunAuthoritativeLoop(NetSessionMode.Client),
            "clients are command-only, never dual-authoritative");
    }

    [Test]
    public void OnlyOfflineCanOpenASession()
    {
        Assert.IsTrue(NetFlowRules.CanStartNewSession(NetSessionMode.Offline));
        Assert.IsFalse(NetFlowRules.CanStartNewSession(NetSessionMode.Host));
        Assert.IsFalse(NetFlowRules.CanStartNewSession(NetSessionMode.Client));
    }

    [Test]
    public void ClientLoopProducesCommandsOnly()
    {
        var go = new GameObject("loop");
        try
        {
            var loop = go.AddComponent<CampaignLoopController>();
            VEVE.Net.NetCommand captured = default;
            int hits = 0;
            loop.Authoritative = false;
            loop.CommandSink = c => { captured = c; hits++; };

            MissionTemplate t = loop.BeginNextMission();
            Assert.AreEqual(default(MissionTemplate).id, t.id, "client cannot draft");
            Assert.AreEqual(0, hits);

            VEVE.EventBus.PublishGlobal(new VEVE.Content.ShotResolvedEvent { onTarget = true, civilianHarm = false });
            VEVE.EventBus.ProcessQueue();
            loop.NotifyShot(true); // lifecycle-independent fact

            Assert.GreaterOrEqual(hits, 1, "client shots travel as commands");
            VEVE.Net.NetCommand seen = default;
            int found = 0;
            loop.CommandSink = c => { seen = c; found++; };
            loop.ReportIntelObject();
            Assert.AreEqual(1, found);
            Assert.AreEqual(VEVE.Net.NetCommandType.IntelObject, seen.type);
        }
        finally
        {
            VEVE.EventBus.ClearAll();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void DebriefFromMirrorRebuildsAuthoritativeNumbers()
    {
        var journal = new MissionCommandJournal();
        int idx = MissionNetMap.IndexOfTemplate("MEDITERRA_CACHE");
        Assert.GreaterOrEqual(idx, 0);

        var mirror = new NetMissionMirror();
        Assert.IsNull(MissionDebriefView.FromMirror(mirror, "X"), "no session yet -> nothing to show");

        NetCommand start = MissionNetMap.Command(NetCommandType.MissionStart, idx, 0, 0f);
        start.seq = MissionNetMap.AppendToJournal(journal, start);
        NetCommand shot = MissionNetMap.Command(NetCommandType.ShotFired, 1, 0);
        shot.seq = MissionNetMap.AppendToJournal(journal, shot);
        NetCommand end = MissionNetMap.Command(NetCommandType.MissionEnd, 1, 0, 220f);
        end.seq = MissionNetMap.AppendToJournal(journal, end);
        mirror.ApplyThrough(journal.Entries, journal.LastSequence);

        MissionDebriefView.Data? debrief = MissionDebriefView.FromMirror(mirror, "MEDITERRA_CACHE");
        Assert.NotNull(debrief);
        Assert.Greater(debrief.Value.total, 0);
        Assert.AreEqual("MISSION DEBRIEF - RELAYED", debrief.Value.headline);
        Assert.IsFalse(debrief.Value.fromAuthoritativePublish);
    }
}
