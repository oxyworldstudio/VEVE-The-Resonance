using NUnit.Framework;
using UnityEngine;
using VEVE.Scoring;

public sealed class MissionScoringTests
{
    private static MissionScoreInputs Base()
    {
        return new MissionScoreInputs
        {
            shotsFired = 40,
            shotsOnTarget = 32,
            missionSeconds = 540f,
            parSeconds = 600f,
            contactsHeld = 2,
            squadMembersTotal = 2,
            squadMembersLost = 0,
            intelObjectsRecovered = 0,
            civilianHarmEvents = 0,
            malfunctionCount = 0
        };
    }

    [Test]
    public void HitRateAccuracyScalesBonus()
    {
        var low = Base(); low.shotsOnTarget = 8;
        var high = Base(); high.shotsOnTarget = 39;
        Assert.Greater(MissionScoreCalculator.Score(high).accuracyBonus,
                       MissionScoreCalculator.Score(low).accuracyBonus);
        Assert.LessOrEqual(MissionScoreCalculator.Score(high).accuracyBonus,
                           MissionScoreCalculator.MaxAccuracyBonus);
    }

    [Test]
    public void NoDivideByZeroWithoutShots()
    {
        var i = Base();
        i.shotsFired = 0;
        i.shotsOnTarget = 0;
        Assert.DoesNotThrow(() => MissionScoreCalculator.Score(i));
        Assert.That(MissionScoreCalculator.Score(i).accuracyBonus, Is.EqualTo(0));
    }

    [Test]
    public void TotalNeverNegativeAndIsMonotonicInCollateral()
    {
        var clean = Base();
        var dirty = Base();
        dirty.civilianHarmEvents = 3;
        var b1 = MissionScoreCalculator.Score(clean);
        var b2 = MissionScoreCalculator.Score(dirty);
        Assert.Greater(b1.total, b2.total);
        Assert.GreaterOrEqual(b2.total, 0);
        Assert.Less(b2.rank, MissionRank.Ghost);
    }

    [Test]
    public void CivilianHarmDisqualifiesGhost()
    {
        var perfect = Base();
        perfect.intelObjectsRecovered = 4;
        Assert.GreaterOrEqual((int)MissionScoreCalculator.Score(perfect).rank, (int)MissionRank.Operator);
        var bad = Base();
        bad.civilianHarmEvents = 1;
        Assert.AreNotEqual(MissionRank.Ghost, MissionScoreCalculator.Score(bad).rank);
    }

    [Test]
    public void RankThresholdsOrdered()
    {
        MissionRank[] ladder =
        {
            MissionRank.Failed, MissionRank.Grunt, MissionRank.Operator, MissionRank.Ghost
        };
        for (int i = 0; i < ladder.Length - 1; i++)
            Assert.Less((int)ladder[i], (int)ladder[i + 1]);
    }

    [Test]
    public void IntelAndCrewBonusesAddPoints()
    {
        var none = Base();
        none.intelObjectsRecovered = 0;
        var many = Base();
        many.intelObjectsRecovered = 3;
        var b1 = MissionScoreCalculator.Score(none);
        var b2 = MissionScoreCalculator.Score(many);
        Assert.Greater(b2.intelBonus, b1.intelBonus);
        Assert.Greater(b2.total, b1.total);
        Assert.Greater(b2.intelPoints, b1.intelPoints);
    }

    [Test]
    public void ExperienceRewardClampedAndNonNegative()
    {
        var b = MissionScoreCalculator.Score(Base());
        Assert.That(b.experienceReward, Is.InRange(0, 4500));
        var huge = Base();
        huge.shotsOnTarget = 40;
        huge.missionSeconds = 1f;
        huge.parSeconds = 10000f;
        huge.intelObjectsRecovered = 50;
        b = MissionScoreCalculator.Score(huge);
        Assert.That(b.experienceReward, Is.LessThanOrEqualTo(4500), "XP capped at ceiling");
        Assert.That(b.total, Is.GreaterThan(0));
    }

    [Test]
    public void FinalizedBoardStopsAccumulating()
    {
        var go = new GameObject("scoreboard");
        try
        {
            var board = go.AddComponent<MissionScoreBoard>();
            board.ReportShot(true);
            var first = board.FinalizeMission(300f, 0, "t");
            Assert.That(board.Finalized, Is.True);
            board.ReportShot(false);
            var second = board.FinalizeMission(300f, 0, "t");
            Assert.AreEqual(first.total, second.total, "post-finalize reports ignored");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TimeBonusWithinPar()
    {
        var slow = Base();
        slow.missionSeconds = 999f;
        Assert.AreEqual(0, MissionScoreCalculator.Score(slow).timeBonus, "over par: no time bonus");

        var fast = Base();
        fast.missionSeconds = 30f;
        fast.parSeconds = 600f;
        Assert.Greater(MissionScoreCalculator.Score(fast).timeBonus, 0);
    }
}
