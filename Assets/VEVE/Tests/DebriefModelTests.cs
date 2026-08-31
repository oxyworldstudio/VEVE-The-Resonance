using System.Collections.Generic;
using NUnit.Framework;
using VEVE.Catalog;
using VEVE.Content;
using VEVE.Net;
using VEVE.Scoring;
using VEVE.Tactics;
using VEVE.UI;

public sealed class DebriefModelTests
{
    [Test]
    public void RankLabelsAndFormatContainTruth()
    {
        Assert.AreEqual("MISSION FAILED", DebriefModel.RankLabel(MissionRank.Failed));
        Assert.That(DebriefModel.RankLabel(MissionRank.Ghost), Does.StartWith("GHOST"));

        var data = new DebriefData
        {
            headline = "Cache Sweep",
            score = new MissionScoreBreakdown { total = 1234, experienceReward = 2500, intelPoints = 12, rank = MissionRank.Operator },
            ownerLines = new List<string> { "client 4: 660 xp | ping 80ms" },
            reconcileTelemetry = "confirmed=12 reverted=1 late=0"
        };
        string body = DebriefModel.Format(data);
        StringAssert.Contains("OPERATOR", body);
        StringAssert.Contains("1234", body);
        StringAssert.Contains("client 4", body);
        StringAssert.Contains("confirmed=12", body);
    }

    [Test]
    public void OwnerLinesFilterSentinelsAndAreNullSafe()
    {
        Assert.AreEqual(0, DebriefModel.OwnerLines(null, null, null).Count);
        Assert.AreEqual(0, DebriefModel.OwnerLines(new FamilyXpLedger(), null, null).Count);

        var ledger = new FamilyXpLedger();
        ledger.Grant(0, "m4a1", 100);
        ledger.Grant(LagCompRules.OfflineOwner, "ak74m", 100);
        ledger.Grant(77, "svd", 5);
        var owners = new List<ulong> { 0, LagCompRules.OfflineOwner, 77 };
        var lines = DebriefModel.OwnerLines(ledger, owners, id => 42);
        Assert.AreEqual(1, lines.Count, "only real clients credited");
        Assert.AreEqual("client 77: 5 xp | ping 42ms", lines[0]);

        // per-event grant is capped: one huge credit lands as exactly MaxGrant, so 5+240 = 245
        ledger.Grant(77, "svd", 995);
        Assert.AreEqual(1, DebriefModel.OwnerLines(ledger, owners, null).Count);
        StringAssert.Contains("245", DebriefModel.OwnerLines(ledger, owners, null)[0], "single-event cap honoured");
    }

    [Test]
    public void ApplyAndSnapshotRoundTripAndBiomeFloors()
    {
        Assert.IsNull(DebriefModel.DebriefSnapshot);
        var d = new DebriefData { headline = "x", score = new MissionScoreBreakdown { rank = MissionRank.Grunt } };
        DebriefModel.Apply(d);
        Assert.IsNotNull(DebriefModel.DebriefSnapshot);
        Assert.AreEqual("x", DebriefModel.DebriefSnapshot.Value.headline);

        Assert.IsTrue(BiomeSceneProfiles.TryAlertFloor("SUBARCTIC_COMPOUND", out int arctic));
        Assert.AreEqual(2, arctic);
        Assert.IsTrue(BiomeSceneProfiles.TryAlertFloor("MEDIUM_TOWN", out int med));
        Assert.LessOrEqual(med, 1);
        Assert.IsTrue(BiomeSceneProfiles.TryAlertFloor("not-a-biome", out int none) == false);
    }
}
