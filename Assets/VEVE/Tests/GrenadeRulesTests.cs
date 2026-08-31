using NUnit.Framework;
using UnityEngine;
using VEVE.Combat;
using VEVE.Gear;
using VEVE;

public sealed class GrenadeRulesTests
{
    [Test]
    public void BlastFallsOffQuadraticallyToZeroAtRadius()
    {
        Assert.AreEqual(GrenadeRules.DefaultBlastEnergyJ,
            GrenadeRules.BlastEnergyAtDistance(0f, 12f, GrenadeRules.DefaultBlastEnergyJ), 1e-3f);
        Assert.AreEqual(GrenadeRules.DefaultBlastEnergyJ * 0.25f,
            GrenadeRules.BlastEnergyAtDistance(6f, 12f, GrenadeRules.DefaultBlastEnergyJ), 1e-2f);
        Assert.AreEqual(0f, GrenadeRules.BlastEnergyAtDistance(12f, 12f, 400f), 1e-5f);
        Assert.AreEqual(0f, GrenadeRules.BlastEnergyAtDistance(25f, 12f, 400f), "beyond radius silent");
        Assert.Greater(GrenadeRules.BlastEnergyAtDistance(3f, 12f, 400f),
                       GrenadeRules.BlastEnergyAtDistance(9f, 12f, 400f));
    }

    [Test]
    public void FuseSanitizeKeepsUsableWindow()
    {
        Assert.AreEqual(1f, GrenadeRules.FuseClamp(1f), "legit short fuse preserved");
        Assert.AreEqual(GrenadeRules.DefaultFuseSeconds, GrenadeRules.FuseClamp(4.5f), 1e-4f);
        Assert.AreEqual(8f, GrenadeRules.FuseClamp(99f), "long fuses clipped");
        Assert.AreEqual(GrenadeRules.DefaultFuseSeconds, GrenadeRules.FuseClamp(-1f));
    }

    [Test]
    public void PlateDeflectsNearBlastAndBareBodyTakesEverything()
    {
        Assert.IsTrue(GearCatalog.TryFind("esapi", out var plate), "plate id must exist in catalog");
        var loadout = new GearLoadout();
        Assert.IsTrue(loadout.TryEquip(GearSlotType.PlateCarrier, plate, out _, 40f, 55f));

        var mitigate = default(GearMitigationResult);
        bool consulted = GrenadeRules.ApplyBlastMitigation(loadout, 2f, 8f, 230f, VEVE.HitZone.UpperTorso, 2f, ref mitigate);
        Assert.IsTrue(consulted, "armor consulted");
        Assert.GreaterOrEqual(plate.coveragePerZone[3], 0.5f, "plate covers a torso zone");

        var bare = default(GearMitigationResult);
        Assert.IsTrue(GrenadeRules.ApplyBlastMitigation(null, 2f, 8f, 230f, VEVE.HitZone.UpperTorso, 2f, ref bare) == false);
        Assert.AreEqual(0f, bare.damageScale);

        // far blast: energy 0 -> nothing consulted, no damage from caller
        var zero = default(GearMitigationResult);
        Assert.IsFalse(GrenadeRules.ApplyBlastMitigation(loadout, 8f, 8f, 230f, VEVE.HitZone.UpperTorso, 0f, ref zero));
    }

    [Test]
    public void ProjectileConfigureSanitizesExtremeInputs()
    {
        var go = new GameObject("grenade-test");
        try
        {
            var proj = go.AddComponent<GrenadeProjectile>();
            proj.Configure(Vector3.forward * 500f, 0f, -40f, 7, -9f);
            Assert.IsTrue(proj.Live, "configured live; fuse sanitized not expired");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
