using NUnit.Framework;
using UnityEngine;
using VEVE.Content;
using VEVE.Tactics;

public sealed class MissionSessionTests
{
    private static MissionSession Session(CampaignDifficulty d = CampaignDifficulty.Regular)
    {
        Assert.IsTrue(MissionContentCatalog.TryGet("desert_wells", out MissionTemplate t));
        var s = new MissionSession(t, d);
        s.Deploy();
        return s;
    }

    [Test]
    public void ParScalesMonotonicallyByDifficultyTrack()
    {
        float regular = new MissionSession(GetTemplate(), CampaignDifficulty.Regular).ParSeconds;
        float hardened = new MissionSession(GetTemplate(), CampaignDifficulty.Hardened).ParSeconds;
        float elite = new MissionSession(GetTemplate(), CampaignDifficulty.Elite).ParSeconds;
        Assert.Greater(regular, hardened);
        Assert.Greater(hardened, elite);
        Assert.Greater(elite, 0f);
    }

    [Test]
    public void ShotsIgnoredBeforeDeploy()
    {
        Assert.IsTrue(MissionContentCatalog.TryGet("desert_wells", out MissionTemplate t));
        var s = new MissionSession(t, CampaignDifficulty.Regular);
        s.RecordShot(true, false);
        Assert.AreEqual(0, s.ShotsFired);
    }

    [Test]
    public void AccuracyDrivesScoreMonotonic()
    {
        var miss = Session();
        for (int i = 0; i < 12; i++) miss.RecordShot(false, false);
        var hit = Session();
        hit.RecordShot(true, false);
        for (int i = 1; i < 12; i++) hit.RecordShot(true, false);

        Assert.Greater(hit.Complete(300f, true).total, miss.Complete(300f, true).total);
    }

    [Test]
    public void FailureZeroesIntelStealthAndCapsRank()
    {
        var clean = Session();
        clean.SetSquadTotal(2);
        clean.ReportIntelObject();
        clean.ReportContactHeld();
        clean.RecordShot(true, false);
        var win = clean.Complete(300f, true);

        var fail = Session();
        fail.SetSquadTotal(2);
        fail.ReportIntelObject();
        fail.ReportContactHeld();
        fail.RecordShot(true, false);
        var lose = fail.Complete(300f, false);

        Assert.Greater(win.total, lose.total);
        Assert.AreEqual(0, lose.intelBonus);
        Assert.AreEqual(0, lose.stealthBonus);
        Assert.AreEqual(MissionPhase.Debrief, fail.Phase);
    }

    [Test]
    public void DifficultyXPUpliftAppliedClamped()
    {
        Assert.IsTrue(MissionContentCatalog.TryGet("subarctic_radio", out MissionTemplate t));
        var regular = new MissionSession(t, CampaignDifficulty.Regular);
        regular.Deploy();
        regular.RecordShot(true, false);
        var r = regular.Complete(200f, true);

        var elite = new MissionSession(t, CampaignDifficulty.Elite);
        elite.Deploy();
        elite.RecordShot(true, false);
        var e = elite.Complete(200f, true);

        Assert.Greater(e.experienceReward, r.experienceReward);
        Assert.That(e.experienceReward, Is.LessThanOrEqualTo(4500));
    }

    [Test]
    public void EscalationPostureIsWithinContract()
    {
        var s = Session();
        s.RecordShot(true, true); // hit with civilian harm on the same round
        s.ReportIntelObject();
        s.SetElapsedForEscalation(420f);
        s.Complete(420f, true);
        PostureDelta d = s.EscalateToNextMission();

        Assert.That(d.patrolDensity01, Is.InRange(0.05f, 1f));
        Assert.That(d.reactionTimeMult, Is.InRange(0.3f, 3f));
        Assert.That(d.armorLikelihood, Is.InRange(0f, 1f));
    }

    private static MissionTemplate GetTemplate()
    {
        Assert.IsTrue(MissionContentCatalog.TryGet("desert_wells", out MissionTemplate t));
        return t;
    }
}
