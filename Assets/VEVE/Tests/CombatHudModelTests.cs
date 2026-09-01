using NUnit.Framework;
using UnityEngine;
using VEVE.UI;

public sealed class CombatHudModelTests
{
    private static CombatHudState FullState()
    {
        return new CombatHudState
        {
            modeLabel = "IN SESSION",
            grenadeCount = 3,
            squadAlive = 4,
            squadTotal = 4,
            posture01 = 0.5f,
            health01 = 0.75f,
            missionRankLabel = "OPERATOR"
        };
    }

    [Test]
    public void FormatIsDeterministicForIdenticalStates()
    {
        string a = CombatHudPresenter.Format(FullState());
        string b = CombatHudPresenter.Format(FullState());
        Assert.AreEqual(a, b);
        StringAssert.Contains("MODE IN SESSION", a);
        StringAssert.Contains("FRAG x3", a);
        StringAssert.Contains("SQUAD 4/4", a);
        StringAssert.Contains("POSTURE MEDIUM", a);
        StringAssert.Contains("HEALTH 75%", a);
        StringAssert.Contains("RANK OPERATOR", a);
    }

    [Test]
    public void EmptyStateCollapsesToNoSquad()
    {
        var empty = FullState();
        empty.squadTotal = 0;
        Assert.AreEqual("NO SQUAD", CombatHudPresenter.Format(empty));

        var defaultState = default(CombatHudState);
        Assert.AreEqual("NO SQUAD", CombatHudPresenter.Format(defaultState), "default struct has no squad");
    }

    [Test]
    public void SquadColor01IsMonotonicInAliveCount()
    {
        Assert.AreEqual(0f, CombatHudRules.SquadColor01(0, 0), "degenerate total floors at zero");
        Assert.AreEqual(0f, CombatHudRules.SquadColor01(0, 4));
        Assert.AreEqual(0.25f, CombatHudRules.SquadColor01(1, 4), 1e-6f);
        Assert.AreEqual(0.5f, CombatHudRules.SquadColor01(2, 4), 1e-6f);
        Assert.AreEqual(1f, CombatHudRules.SquadColor01(4, 4));
        Assert.AreEqual(1f, CombatHudRules.SquadColor01(9, 4), "alive clamped to total");

        float prev = -1f;
        for (int alive = 0; alive <= 6; alive++)
        {
            float v = CombatHudRules.SquadColor01(alive, 6);
            Assert.GreaterOrEqual(v, prev, "more alive never reads lower");
            prev = v;
        }
    }

    [Test]
    public void PostureBandsAreOrderedLowMediumHigh()
    {
        Assert.AreEqual("LOW", CombatHudRules.PostureLabel(0f));
        Assert.AreEqual("MEDIUM", CombatHudRules.PostureLabel(0.5f));
        Assert.AreEqual("HIGH", CombatHudRules.PostureLabel(1f));
        Assert.AreEqual("HIGH", CombatHudRules.PostureLabel(5f), "values above one stay HIGH");
        Assert.AreEqual("LOW", CombatHudRules.PostureLabel(-2f), "values below zero stay LOW");

        string[] order = { "LOW", "MEDIUM", "HIGH" };
        int prevRank = -1;
        for (int i = 0; i <= 10; i++)
        {
            string label = CombatHudRules.PostureLabel(i / 10f);
            int rank = System.Array.IndexOf(order, label);
            Assert.GreaterOrEqual(rank, prevRank, "bands ascend with posture01");
            prevRank = rank;
        }
    }

    [Test]
    public void GrenadeLineIsPresentForNonNegativeCountsAndAbsentOtherwise()
    {
        Assert.IsTrue(CombatHudRules.ShouldShowGrenade(0));
        Assert.IsTrue(CombatHudRules.ShouldShowGrenade(5));
        Assert.IsFalse(CombatHudRules.ShouldShowGrenade(-1), "kept for future gating");

        var withGrenades = FullState();
        withGrenades.grenadeCount = -3;
        StringAssert.DoesNotContain("FRAG", CombatHudPresenter.Format(withGrenades));

        var emptyStock = FullState();
        emptyStock.grenadeCount = 0;
        StringAssert.Contains("FRAG x0", CombatHudPresenter.Format(emptyStock), "zero is never negative");
    }

    [Test]
    public void PlaceholderPlaceholdersForMissingLabelsAndUnknownHealth()
    {
        var s = FullState();
        s.modeLabel = null;
        s.missionRankLabel = null;
        s.health01 = CombatHudState.UnknownHealth01;
        string text = CombatHudPresenter.Format(s);
        StringAssert.Contains("MODE --", text);
        StringAssert.Contains("RANK --", text);
        StringAssert.Contains("HEALTH --", text);
    }

    [Test]
    public void NoSessionPlaceholderMatchesPresenterEmptyState()
    {
        // The panel with no loop/session formats the default state: exactly NO SQUAD.
        Assert.AreEqual("NO SQUAD", CombatHudPresenter.Format(default(CombatHudState)));
    }

    [Test]
    public void SquadAliveIsClampedIntoTotal()
    {
        var s = FullState();
        s.squadAlive = 9;
        s.squadTotal = 4;
        StringAssert.Contains("SQUAD 4/4", CombatHudPresenter.Format(s));
    }
}
