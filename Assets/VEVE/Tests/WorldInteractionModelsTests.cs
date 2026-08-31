using NUnit.Framework;
using VEVE.World;
using VEVE.Combat;

public sealed class WorldInteractionModelsTests
{
    [Test]
    public void KickDamageGrowsWithLockAndCaps()
    {
        Assert.Greater(DoorModel.KickDamage(2), DoorModel.KickDamage(0));
        Assert.LessOrEqual(DoorModel.KickDamage(99), 60f);
        Assert.AreEqual(DoorModel.KickDamage(0), DoorModel.BaseKickDamage);
    }

    [Test]
    public void PickTimeMonotonicAndKitFaster()
    {
        Assert.Greater(DoorModel.PickSeconds(2, false), DoorModel.PickSeconds(1, false));
        Assert.Less(DoorModel.PickSeconds(2, true), DoorModel.PickSeconds(2, false));
    }

    [Test]
    public void KickResolvesBreachedOnlyWhenUnlatchedOrDestroyed()
    {
        Assert.AreEqual(DoorState.Locked, DoorModel.ResolveKick(DoorState.Locked, 88f, false), "hard kick on solid deadbolt: still locked");
        Assert.AreEqual(DoorState.Breached, DoorModel.ResolveKick(DoorState.Locked, 0f, false), "integrity gone: breached");
        Assert.AreEqual(DoorState.Breached, DoorModel.ResolveKick(DoorState.Closed, 100f, true), "unlatched closed door gives to kick");
        Assert.AreEqual(DoorState.Open, DoorModel.ResolveKick(DoorState.Open, 0f, true), "open cannot be breached");
    }

    [Test]
    public void BreachDamageScalesAndFloors()
    {
        Assert.Greater(DoorModel.BreachDamage(0.5f), DoorModel.BreachDamage(0.1f));
        Assert.AreEqual(DoorModel.BreachDamage(0f), DoorModel.BreachDamage(DoorModel.MinChargeKg));
    }

    [Test]
    public void FullReloadTransfersFromReserveOnce()
    {
        int reserve = 90;
        int transferred = AmmunitionModel.TransferForReload(4, 30, reserve, out int newReserve);
        Assert.AreEqual(26, transferred);
        Assert.AreEqual(64, newReserve);

        Assert.AreEqual(0, AmmunitionModel.TransferForReload(30, 30, newReserve, out int unchanged));
        Assert.AreEqual(newReserve, unchanged);

        Assert.AreEqual(0, AmmunitionModel.TransferForReload(10, 30, 0, out int zero));
        Assert.AreEqual(0, zero);
    }

    [Test]
    public void TacticalSwapSpendsWholeMagazineFromRoundReserve()
    {
        // Reserve is counted in rounds: a 30-rounder swap consumes 30 reserve rounds.
        AmmunitionModel.TacticalTransfer(12, 30, 90, out int rounds, out int reserve);
        Assert.AreEqual(30, rounds);
        Assert.AreEqual(60, reserve);

        AmmunitionModel.TacticalTransfer(30, 30, 5, out int sameRounds, out int sameReserve);
        Assert.AreEqual(30, sameRounds);
        Assert.AreEqual(5, sameReserve, "full magazine is a no-op");

        // Under-strength reserve cannot fund a swap (no free ammo ever).
        AmmunitionModel.TacticalTransfer(10, 30, 20, out int r2, out int res2);
        Assert.AreEqual(10, r2);
        Assert.AreEqual(20, res2);
    }

    [Test]
    public void DryReloadSlowerThanTactical()
    {
        Assert.Greater(AmmunitionModel.DryReloadSeconds(2.6f, 1f), AmmunitionModel.FullReloadSeconds(2.6f, 1f));
        Assert.Less(AmmunitionModel.TacticalReloadSeconds(2.6f, 1f), AmmunitionModel.FullReloadSeconds(2.6f, 1f));
        Assert.Less(AmmunitionModel.FullReloadSeconds(2.6f, 1.4f), AmmunitionModel.FullReloadSeconds(2.6f, 1f), "faster hands finish quicker");
    }

    [Test]
    public void StartReserveIsThreeMags()
    {
        Assert.AreEqual(90, AmmunitionModel.StartReserve(30));
        Assert.AreEqual(0, AmmunitionModel.StartReserve(0));
    }
}
