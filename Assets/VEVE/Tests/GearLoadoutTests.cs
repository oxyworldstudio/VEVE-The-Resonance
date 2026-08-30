using NUnit.Framework;
using UnityEngine;
using VEVE;
using VEVE.Gear;

public sealed class GearLoadoutTests
{
    [Test]
    public void CoverageAggregatesAsMaxPerZone()
    {
        var loadout = new GearLoadout();
        Assert.That(GearCatalog.TryFind("fast_mt", out GearItem helmet), Is.True);
        Assert.That(loadout.TryEquip(GearSlotType.BallisticHelmet, helmet, out _), Is.True);
        GearItem plate = Create(GearSlotType.PlateCarrier, ProtectionLevel.NIJ_III, 6.6f, 2f);
        plate.SetCoverage(HitZone.Neck, 0.8f);
        plate.SetCoverage(HitZone.UpperTorso, 0.55f);
        Assert.That(loadout.TryEquip(GearSlotType.PlateCarrier, plate, out _), Is.True);
        Assert.That(helmet.CoverageFor(HitZone.Neck), Is.GreaterThan(0f));
        Assert.That(loadout.CoverageFor(HitZone.Neck), Is.EqualTo(0.8f).Within(0.0001f), "max rule: best layer counts, no stacking");
        Assert.That(loadout.CoverageFor(HitZone.UpperTorso), Is.EqualTo(0.55f).Within(0.0001f));
        Assert.That(loadout.CoverageFor(HitZone.ThighLeft), Is.EqualTo(0f));

        GearMitigationResult result = default;
        Assert.That(DamageableGearAdapter.TryMitigate(null, 500f, 400f, HitZone.Neck, 12f, ref result), Is.False);
    }

    [Test]
    public void MassAndHeatAccumulate()
    {
        var loadout = new GearLoadout();
        Assert.That(GearCatalog.TryFind("fast_mt", out GearItem helmet), Is.True);
        Assert.That(GearCatalog.TryFind("comtac_vi", out GearItem hearing), Is.True);
        Assert.That(loadout.TryEquip(GearSlotType.BallisticHelmet, helmet, out _), Is.True);
        Assert.That(loadout.TryEquip(GearSlotType.EarProtection, hearing, out _), Is.True);
        Assert.That(loadout.TotalMassKg, Is.EqualTo(helmet.massKg + hearing.massKg).Within(0.0001f));
        Assert.That(loadout.TotalHeatLoad, Is.GreaterThan(0f));
        Assert.That(loadout.TotalVolumeLitres, Is.GreaterThan(0f));
        Assert.That(loadout.EquippedCount, Is.EqualTo(2));
        Assert.That(loadout.HasCommsIntegration, Is.True);
    }

    [Test]
    public void SoftArmorConflictsWithPlateCarrier()
    {
        var loadout = new GearLoadout();
        Assert.That(GearCatalog.TryFind("cf_plate", out GearItem carrier), Is.True);
        Assert.That(loadout.TryEquip(GearSlotType.PlateCarrier, carrier, out _), Is.True);
        Assert.That(GearCatalog.TryFind("iiia_soft", out GearItem soft), Is.True);
        Assert.That(loadout.TryEquip(GearSlotType.SoftArmor, soft, out string failure), Is.False);
        Assert.That(failure, Does.Contain("conflicts"));
        Assert.That(GearSlots.ConflictsWith(GearSlotType.SoftArmor, GearSlotType.PlateCarrier), Is.True);
        Assert.That(GearSlots.ConflictsWith(GearSlotType.Backpack, GearSlotType.PlateCarrier), Is.False);
    }

    [Test]
    public void VolumeAndMassBudgetsRejectEquip()
    {
        var loadout = new GearLoadout();
        GearItem heavy = Create(GearSlotType.PlateCarrier, ProtectionLevel.NIJ_IV, 60f, 5f);
        Assert.That(loadout.TryEquip(GearSlotType.PlateCarrier, heavy, out _, maxMassKg: 25f), Is.False);
        GearItem bulky = Create(GearSlotType.Backpack, ProtectionLevel.Unrated, 2f, 90f);
        Assert.That(loadout.TryEquip(GearSlotType.Backpack, bulky, out _, maxVolumeLitres: 45f), Is.False);
        GearItem wrongSlot = Create(GearSlotType.Gloves, ProtectionLevel.Unrated, 0.4f, 1f);
        Assert.That(loadout.TryEquip(GearSlotType.Backpack, wrongSlot, out _), Is.False);
        Assert.That(loadout.EquippedCount, Is.EqualTo(0), "failed equips must leave loadout untouched");
    }

    [Test]
    public void DamageMultiplierStaysInUnitRange()
    {
        var loadout = new GearLoadout();
        Assert.That(GearCatalog.TryFind("esapi", out GearItem plate), Is.True);
        Assert.That(GearCatalog.TryFind("sleeves", out GearItem sleeves), Is.True);
        Assert.That(GearCatalog.TryFind("gloves_armorx", out GearItem gloves), Is.True);
        loadout.TryEquip(GearSlotType.PlateCarrier, plate, out _);
        loadout.TryEquip(GearSlotType.ArmsSleeves, sleeves, out _);
        loadout.TryEquip(GearSlotType.Gloves, gloves, out _);
        foreach (HitZone zone in System.Enum.GetValues(typeof(HitZone)))
        {
            foreach (float energy in new[] { 0f, 500f, 1500f, 3500f, 12000f })
            {
                foreach (float angle in new[] { 0f, 25f, 80f })
                {
                    float mult = loadout.DamageMultiplierFor(energy, zone, angle);
                    Assert.That(mult, Is.InRange(0f, 1f), $"zone {zone} energy {energy} angle {angle}");
                }
            }
        }
    }

    [Test]
    public void ArmorCutsDamageMultiplierAgainstSubThreatRounds()
    {
        var loadout = new GearLoadout();
        GearItem plate = Create(GearSlotType.PlateCarrier, ProtectionLevel.NIJ_III, 6.6f, 2f);
        plate.SetCoverage(HitZone.UpperTorso, 0.6f);
        Assert.That(loadout.TryEquip(GearSlotType.PlateCarrier, plate, out _), Is.True);
        float armored = loadout.DamageMultiplierFor(1000f, HitZone.UpperTorso, 0f);
        Assert.That(armored, Is.EqualTo(0.4f).Within(0.0001f), "1 - coverage*stopFraction with full stop");
        Assert.That(loadout.DamageMultiplierFor(1000f, HitZone.ThighLeft, 0f), Is.EqualTo(1f));
        Assert.That(loadout.DamageMultiplierFor(3350f * 3f, HitZone.UpperTorso, 0f), Is.GreaterThan(armored), "overmatch leaks more");
    }

    [Test]
    public void MitigationRegistersStrikesAndDeratesLaterHits()
    {
        var loadout = new GearLoadout();
        GearItem plate = Create(GearSlotType.PlateCarrier, ProtectionLevel.NIJ_IV, 6.6f, 2f);
        plate.SetCoverage(HitZone.UpperTorso, 0.9f);
        loadout.TryEquip(GearSlotType.PlateCarrier, plate, out _);
        GearProtectionStandard.TryGetLevel(ProtectionLevel.NIJ_IV, out ProtectionLevelData data);
        float energy = data.stopEnergyJoules * (0.5f + 0.5f * data.multiHitRetention);
        GearMitigationResult first = loadout.ApplyHitMitigation(energy, 850f, HitZone.UpperTorso, 12f);
        Assert.That(first.stopped, Is.True);
        Assert.That(first.traumaEnergyJoules, Is.LessThan(energy));
        Assert.That(loadout.PanelStrikeCount(GearSlotType.PlateCarrier), Is.EqualTo(1));
        GearMitigationResult second = loadout.ApplyHitMitigation(energy, 850f, HitZone.UpperTorso, 12f);
        Assert.That(second.stopped, Is.False);
        Assert.That(second.traumaEnergyJoules, Is.EqualTo(energy));
    }

    [Test]
    public void CatalogCoversEverySlotAndPlausibleMasses()
    {
        var seenSlots = new System.Collections.Generic.HashSet<GearSlotType>();
        foreach (GearItem item in GearCatalog.All())
        {
            seenSlots.Add(item.slot);
            Assert.That(item.massKg, Is.GreaterThan(0f).And.LessThan(60f), item.id);
            Assert.That(item.volumeLitres, Is.GreaterThan(0f), item.id);
            Assert.That(item.coveragePerZone.Length, Is.EqualTo(GearItem.ZoneCount));
        }
        Assert.That(GearCatalog.All().Count, Is.GreaterThanOrEqualTo(18));
        foreach (GearSlotType slot in System.Enum.GetValues(typeof(GearSlotType)))
            Assert.That(seenSlots, Contains.Item(slot), $"catalog missing entries for {slot}");
        Assert.That(GearSlots.SlotCount, Is.EqualTo(System.Enum.GetValues(typeof(GearSlotType)).Length));
    }

    [Test]
    public void MobilityModelClampsAndIsMonotonic()
    {
        Assert.That(MobilityPenaltyModel.WalkSpeedMultiplier(0f, 0f, 0f), Is.EqualTo(1f));
        Assert.That(MobilityPenaltyModel.SprintSpeedMultiplier(5f, 500f, 5f), Is.EqualTo(0.5f).Within(0.001f), "inputs clamp at worst case");
        float previous = float.MaxValue;
        for (float load = 0f; load <= 1f; load += 0.1f)
        {
            float walk = MobilityPenaltyModel.WalkSpeedMultiplier(load, 20f, 0.5f);
            Assert.That(walk, Is.LessThanOrEqualTo(previous + 0.0001f));
            Assert.That(walk, Is.InRange(0.65f, 1f));
            float sprint = MobilityPenaltyModel.SprintSpeedMultiplier(load, 20f, 0.5f);
            Assert.That(sprint, Is.LessThanOrEqualTo(walk + 0.0001f), "sprint punished harder than walking");
            previous = walk;
        }
        previous = float.MinValue;
        for (float load = 0f; load <= 1f; load += 0.1f)
        {
            float drain = MobilityPenaltyModel.StaminaDrainMultiplier(load, 100f);
            Assert.That(drain, Is.GreaterThanOrEqualTo(previous - 0.0001f));
            Assert.That(drain, Is.InRange(1f, MobilityPenaltyModel.MaxStaminaMultiplier));
            float sway = MobilityPenaltyModel.SwayMultiplier(load, 1f, 60f);
            Assert.That(sway, Is.InRange(1f, MobilityPenaltyModel.MaxSwayMultiplier));
            previous = drain;
        }
        Assert.That(MobilityPenaltyModel.HeatGainMultiplier(1f, 1f), Is.EqualTo(2.1f).Within(0.001f));
        Assert.That(MobilityPenaltyModel.AimRecoveryMultiplier(1f, 1f), Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void AdapterBehaviourAddsWithoutWiring()
    {
        GameObject owner = new GameObject("GearAdapterTest");
        try
        {
            DamageableGearAdapter adapter = owner.AddComponent<DamageableGearAdapter>();
            Assert.That(adapter.Loadout, Is.Null);
            GearMitigationResult result = default;
            Assert.That(adapter.MitigateHit(1000f, 400f, HitZone.UpperTorso, 12f, ref result), Is.False);
            adapter.Loadout = new GearLoadout();
            Assert.That(adapter.MitigateHit(1000f, 400f, HitZone.UpperTorso, 12f, ref result), Is.True);
            Assert.That(result.damageScale, Is.EqualTo(1f), "empty loadout leaks everything");
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    private static GearItem Create(GearSlotType slot, ProtectionLevel level, float mass, float volume)
    {
        return new GearItem
        {
            id = "test_" + slot + "_" + level,
            displayName = "Test " + slot,
            slot = slot,
            category = GearCategory.Accessories,
            protectionLevel = level,
            massKg = mass,
            volumeLitres = volume
        };
    }
}
