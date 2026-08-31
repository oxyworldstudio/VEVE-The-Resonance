using NUnit.Framework;
using System.Collections.Generic;
using VEVE.Content;
using VEVE.UI;
using VEVE.WeaponCustomPro;

public sealed class MissionContentTests
{
    [Test]
    public void CatalogHasUniqueIdsAndThreePerRegion()
    {
        MissionTemplate[] all = MissionContentCatalog.All;
        Assert.AreEqual(15, all.Length, "W8: 5 biomes x 3 authored ops");
        var ids = new HashSet<string>();
        var perRegion = new Dictionary<string, int>();
        foreach (MissionTemplate t in all)
        {
            Assert.That(ids.Add(t.id), Is.True, "duplicate template id " + t.id);
            perRegion.TryGetValue(t.regionKey, out int c);
            perRegion[t.regionKey] = c + 1;
        }
        foreach (string r in MissionContentCatalog.Regions)
            Assert.That(perRegion[r], Is.EqualTo(3), r + " should have exactly three ops");
    }

    [Test]
    public void TemplateFieldsAreSane()
    {
        foreach (MissionTemplate t in MissionContentCatalog.All)
        {
            Assert.That(t.parSeconds, Is.InRange(480, 1200), t.id);
            Assert.That((int)t.enemySquadPairs, Is.InRange(1, 6), t.id);
            Assert.That(t.alertBias, Is.InRange(0f, 1f), t.id);
            Assert.That(t.objectiveSummary, Is.Not.Null.And.Length.GreaterThan(0), t.id);
        }
    }

    [Test]
    public void SchedulerIsDeterministicAndCyclesThroughPool()
    {
        MissionTemplate a = MissionScheduler.Draft("DESERT_CHECKPOINT", 3);
        MissionTemplate b = MissionScheduler.Draft("DESERT_CHECKPOINT", 3);
        Assert.AreEqual(a.id, b.id, "same inputs must draft the same operation");

        var seen = new HashSet<string>();
        for (int i = 0; i < 40; i++) seen.Add(MissionScheduler.Draft("DESERT_CHECKPOINT", i).id);
        Assert.That(seen.Count, Is.GreaterThanOrEqualTo(2), "pool must visibly cycle");
    }

    [Test]
    public void UnknownRegionFallsBackToWholePool()
    {
        MissionTemplate t = MissionScheduler.Draft("ATLANTIS", 0);
        Assert.That(t.id, Is.Not.Empty);
    }

    [Test]
    public void TryGetAndLookupRoundTrip()
    {
        Assert.IsTrue(MissionContentCatalog.TryGet("desert_wells", out MissionTemplate t));
        Assert.AreEqual("DESERT_WELLS", t.id);
        Assert.IsFalse(MissionContentCatalog.TryGet("nope", out _));
    }

    [Test]
    public void DifficultyTrackIsMonotonic()
    {
        CampaignDifficulty[] ladder = { CampaignDifficulty.Regular, CampaignDifficulty.Hardened, CampaignDifficulty.Elite };
        for (int i = 0; i < 2; i++)
        {
            Assert.Less(CampaignDifficultyProfile.AiSkillFloor(ladder[i]), CampaignDifficultyProfile.AiSkillFloor(ladder[i + 1]));
            Assert.Greater(CampaignDifficultyProfile.ReactionTimeMultiplier(ladder[i]), CampaignDifficultyProfile.ReactionTimeMultiplier(ladder[i + 1]));
            Assert.Less(CampaignDifficultyProfile.PatrolDensity(ladder[i]), CampaignDifficultyProfile.PatrolDensity(ladder[i + 1]));
            Assert.Greater(CampaignDifficultyProfile.ParSecondsFactor(ladder[i]), CampaignDifficultyProfile.ParSecondsFactor(ladder[i + 1]));
            Assert.Less(CampaignDifficultyProfile.ExperienceMultiplier(ladder[i]), CampaignDifficultyProfile.ExperienceMultiplier(ladder[i + 1]));
        }
    }

    [Test]
    public void ScopeTelemetryHintMatchesRangeCardMath()
    {
        Assert.IsTrue(ZeroingSystem.TryComputeCard("ak74m", 100f, 40f, out RangeCard card), "range card resolves");

        float near = ScopeTelemetryBridge.HintMoa(card, 0.0, 12.3f);
        Assert.That(near, Is.GreaterThan(0f).And.LessThan(8f), "hold high inside first crossing (~+4.9)");

        float crown = ScopeTelemetryBridge.HintMoa(card, 0.0, 62.3f);
        Assert.That(crown, Is.LessThan(0f).And.GreaterThan(-3f), "hold under in the crown band (~-1.3)");

        float atZero = ScopeTelemetryBridge.HintMoa(card, 0.0, 100f);
        Assert.That(atZero, Is.EqualTo(0f).Within(0.3f), "zeroed at zero range");

        float noCard = ScopeTelemetryBridge.HintMoa(null, 1.25, 250f);
        Assert.That(noCard, Is.EqualTo(1.25f), "no card: turret offset only");
    }

    [Test]
    public void ZeroDistanceYieldsTurretOnly()
    {
        Assert.That(ScopeTelemetryBridge.HintMoa(null, -0.5, 0f), Is.EqualTo(-0.5f));
    }
}
