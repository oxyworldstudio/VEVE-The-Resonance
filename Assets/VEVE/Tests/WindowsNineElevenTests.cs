using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VEVE.Gear;
using VEVE.Net;
using VEVE.UI;

public sealed class WindowsNineElevenTests
{
    // --------------------------------------------------------------- W9 roster

    private sealed class FakePing : IClientPingSource
    {
        public int PingMs(ulong clientId) => (int)(clientId * 3 % 91);
    }

    [Test]
    public void RosterSkipsSentinelsAndSortsAsc()
    {
        var rows = LobbyRosterModel.Build(new List<ulong> { 11, 3, 0, LagCompRules.OfflineOwner, 7, 7 }, new FakePing());
        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual(3ul, rows[0].owner);
        Assert.AreEqual(11ul, rows[2].owner);
        Assert.AreEqual(9, rows[0].pingMs);
        StringAssert.Contains("CLIENT 3", LobbyRosterModel.Format(rows));
    }

    [Test]
    public void RosterNullOrEmptyIsSafe()
    {
        Assert.AreEqual(0, LobbyRosterModel.Build(null, null).Count);
        Assert.AreEqual(LobbyRosterModel.EmptyLabel, LobbyRosterModel.Format(null));
        Assert.AreEqual(LobbyRosterModel.EmptyLabel, LobbyRosterModel.Format(new List<LobbyRosterRow>()));
        Assert.AreEqual(0,
            LobbyRosterModel.Build(new List<ulong> { 0, LagCompRules.OfflineOwner }, null).Count,
            "sentinels cannot produce a row through Build");
    }

    // ---------------------------------------------------------------- W10 starter

    [Test]
    public void StarterLoadoutEquipsRequiredAndIsIdempotent()
    {
        var target = new GearLoadout();
        Assert.IsTrue(StarterLoadoutRules.TryBuild(target, out string f1), f1);

        GearItem helmet = null, torso = null;
        foreach (var cand in new[] { GearSlotType.BallisticHelmet, GearSlotType.SoftArmor })
        {
            if (target.Get(cand) != null) { if (cand == GearSlotType.BallisticHelmet) helmet = target.Get(cand); else torso = target.Get(cand); }
        }
        Assert.NotNull(helmet, "helmet slot filled from catalog");
        Assert.NotNull(torso, "soft armor filled");

        float massFirst = target.TotalMassKg;
        Assert.IsTrue(StarterLoadoutRules.TryBuild(target, out string f2), f2);
        Assert.AreEqual(massFirst, target.TotalMassKg, 1e-4f, "second pass must not add a second helmet");
        Assert.IsFalse(StarterLoadoutRules.TryBuild(null, out string f3));
        Assert.IsNotEmpty(f3);
    }

    [Test]
    public void GearAdapterStarterIsIdempotentAndKeepsExisting()
    {
        var go = new GameObject("gear");
        try
        {
            var adapter = go.AddComponent<DamageableGearAdapter>();
            Assert.IsTrue(adapter.EnsureStarterGear());
            GearLoadout first = adapter.Loadout;
            Assert.IsNotNull(first);
            Assert.IsTrue(adapter.EnsureStarterGear());
            Assert.AreSame(first, adapter.Loadout, "pre-existing loadout never overwritten");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ---------------------------------------------------------------- W11 interp

    [Test]
    public void RenderTickAndAlphaBehave()
    {
        Assert.AreEqual(0, InterpolationRules.RenderTick(5, 500));
        Assert.AreEqual(8, InterpolationRules.RenderTick(10, 2), "ping lag subtracts");
        Assert.AreEqual(0f, InterpolationRules.Alpha(10, 20, 10), 1e-5f);
        Assert.AreEqual(0.5f, InterpolationRules.Alpha(10, 20, 15), 1e-5f);
        Assert.AreEqual(1f, InterpolationRules.Alpha(10, 20, 99), 1e-5f);
        Assert.AreEqual(1f, InterpolationRules.Alpha(20, 5, 5), "degenerate span -> last");
    }

    [Test]
    public void BufferRejectsOutOfOrderAndLerps()
    {
        var b = new InterpolationBuffer(8);
        Assert.IsFalse(b.SampleAt(0, out _, out _));
        b.Push(new NetSample { tick = 10, position = Vector3.zero, yawDeg = 0f });
        Assert.IsTrue(b.IsEmpty == false);
        b.Push(new NetSample { tick = 9, position = Vector3.right * 99f }); // rejected
        b.Push(new NetSample { tick = 20, position = Vector3.right, yawDeg = 10f });
        Assert.AreEqual(2, b.Count);

        Assert.IsTrue(b.SampleAt(15, out Vector3 pos, out float yaw));
        Assert.AreEqual(0.5f, pos.x, 1e-4f);
        Assert.AreEqual(5f, yaw, 1e-4f);

        Assert.IsTrue(b.SampleAt(30, out pos, out yaw), "ahead holds newest, no extrapolation");
        Assert.AreEqual(1f, pos.x, 1e-4f);
        Assert.AreEqual(10f, yaw, 1e-4f);
        Assert.IsFalse(b.SampleAt(3, out _, out _), "before window rejects as stale");
    }

    [Test]
    public void BufferRingWrapsAroundCapacity()
    {
        var ring = new InterpolationBuffer(8);
        for (int t = 0; t < 4; t++) ring.Push(new NetSample { tick = t, position = new Vector3(t, 0f, 0f) });
        Assert.GreaterOrEqual(ring.Count, 1);
        Assert.Less(ring.OldestTick, 4);
        Assert.AreEqual(3, ring.NewestTick);
    }
}
