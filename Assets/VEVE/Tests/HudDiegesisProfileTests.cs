using NUnit.Framework;
using VEVE;
using VEVE.UI;

public sealed class HudDiegesisProfileTests
{
    [Test]
    public void TestModeEnablesEverything()
    {
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Test, AdvancedHUDLayout.Features.KillFeed), Is.True);
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Test, AdvancedHUDLayout.Features.Ammo), Is.True);
    }

    [Test]
    public void RealisticKeepsOnlyCompassAndVitals()
    {
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Realistic, AdvancedHUDLayout.Features.Compass), Is.True);
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Realistic, AdvancedHUDLayout.Features.Vitals), Is.True);
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Realistic, AdvancedHUDLayout.Features.Ammo), Is.False);
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Realistic, AdvancedHUDLayout.Features.KillFeed), Is.False);
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Realistic, AdvancedHUDLayout.Features.Squad), Is.False);
    }

    [Test]
    public void ModesAreMonotonicTestSupersetRealistic()
    {
        foreach (string f in new[]
        {
            AdvancedHUDLayout.Features.Compass, AdvancedHUDLayout.Features.Objectives,
            AdvancedHUDLayout.Features.Squad, AdvancedHUDLayout.Features.Ammo,
            AdvancedHUDLayout.Features.Vitals, AdvancedHUDLayout.Features.Damage,
            AdvancedHUDLayout.Features.KillFeed, AdvancedHUDLayout.Features.Vignette,
            AdvancedHUDLayout.Features.Stamina
        })
        {
            if (HudDiegesisProfile.IsFeatureEnabled(DeathMode.Realistic, f))
                Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Test, f), Is.True,
                    f + " enabled in Realistic but not Test");
        }
    }

    [Test]
    public void UnknownOrEmptyFeaturesAreDisabled()
    {
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Test, null), Is.False);
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Test, "radar"), Is.False);
    }

    [Test]
    public void ApplyIsCaseInsensitiveAndNullSafe()
    {
        Assert.DoesNotThrow(() => HudDiegesisProfile.Apply(null, DeathMode.Realistic));
        Assert.That(HudDiegesisProfile.IsFeatureEnabled(DeathMode.Realistic, "COMPASS"), Is.True);
    }
}
