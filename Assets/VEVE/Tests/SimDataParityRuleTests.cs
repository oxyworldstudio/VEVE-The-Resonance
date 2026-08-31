using System;
using System.Collections.Generic;
using NUnit.Framework;
using VEVE.CodeReview;
using VEVE.CodeReview.Agents;
using VEVE.Content.SimData;

public sealed class SimDataParityRuleTests
{
    [Test]
    public void PackRoundTripsByteIdenticalAndHashBound()
    {
        var a = new SimDataPack().Add("m4a1.bc", 0.294, 1.013).Add("esapi.v50", 1800.0, 0.93d);
        byte[] bytes = a.Encode();
        var b = SimDataPack.Decode(bytes, true);
        Assert.AreEqual(2, b.Entries.Count);
        Assert.IsTrue(b.TryGet("m4a1.bc", out double[] v1));
        CollectionAssert.AreEqual(new[] { 0.294, 1.013 }, v1);
        // deterministic: same entries -> identical hash and bytes
        var c = new SimDataPack().Add("m4a1.bc", 0.294, 1.013).Add("esapi.v50", 1800.0, 0.93d);
        CollectionAssert.AreEqual(bytes, c.Encode());
        Assert.AreEqual(a.PayloadHash, c.PayloadHash);
    }

    [Test]
    public void TamperAndGarbageDecodeToEmptyAndBadMagicRejected()
    {
        var pack = new SimDataPack().Add("k", 1, 2).Add("other", 3);
        byte[] bytes = pack.Encode();
        bytes[bytes.Length - 1] ^= 0xFF; // flip a value byte
        Assert.AreEqual(0, SimDataPack.Decode(bytes, true).Entries.Count, "tamper => hash mismatch => refuse");
        Assert.AreEqual(0, SimDataPack.Decode(new byte[0], true).Entries.Count);
        Assert.AreEqual(0, SimDataPack.Decode(null, true).Entries.Count);
    }

    // ------------------------------------------------------------------ parity

    [Test]
    public void ParityByteAndTextFailWithIndex()
    {
        byte[] x = { 1, 2, 3 };
        byte[] y = { 1, 9, 3 };
        var r = ParityHarness.Compare(x, y);
        Assert.IsFalse(r.Match);
        Assert.AreEqual(1, r.FirstMismatchIndex);
        Assert.IsTrue(ParityHarness.Compare(x, (byte[])x.Clone()).Match);

        var t = ParityHarness.CompareText("rough*rough", "rough/rough");
        Assert.IsFalse(t.Match);
        Assert.AreEqual(5, t.FirstMismatchIndex);
        Assert.AreEqual("rough*rough", ParityHarness.CompareText("rough*rough", "rough*rough").Detail ?? "rough*rough");

        Assert.IsFalse(ParityHarness.Compare(null, x).Match);
        Assert.IsTrue(ParityHarness.Compare((byte[])null, (byte[])null).Match, "both null considered parity");
    }

    // ------------------------------------------------------------------ agents

    [Test]
    public void SimDataAndDeterminismAgentsFireOnlyInSimPaths()
    {
        string sim = "Assets/VEVE/Sim/Systems/BallisticsSystem.cs";
        string game = "Assets/VEVE/Runtime/Gear/GearLoadout.cs";

        var sd = new SimDataRule();
        var list = new List<ReviewIssue>();
        foreach (var i in sd.Scan(sim, new[] { "float drag = 0.31f;" })) list.Add(i);
        Assert.AreEqual(1, list.Count, "magic float flagged in Sim path");
        list.Clear();
        foreach (var i in sd.Scan(game, new[] { "float drag = 0.31f;" })) list.Add(i);
        Assert.AreEqual(0, list.Count, "presentation/runtime code is free of SimData rule");

        list.Clear();
        foreach (var i in sd.Scan(sim, new[] { "// SimData read: 0.31f", "float drag = SimData.POWDER;" })) list.Add(i);
        Assert.AreEqual(0, list.Count, "SimData-mentioned line not flagged");

        var det = new DeterminismRule();
        list.Clear();
        foreach (var i in det.Scan(sim, new[] { "DateTime now = DateTime.UtcNow;" })) list.Add(i);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(ReviewSeverity.Error, list[0].severity, "nondeterminism is a gate blocker");
        list.Clear();
        foreach (var i in det.Scan(sim, new[] { "var t = DateTime.UtcNow; // [sim-allowed] presentation only" })) list.Add(i);
        Assert.AreEqual(0, list.Count, "explicit allow comment respected");
        list.Clear();
        foreach (var i in det.Scan(game, new[] { "DateTime now = DateTime.Now;" })) list.Add(i);
        Assert.AreEqual(0, list.Count);
    }

    [Test]
    public void OrchestratorGateBlocksDeterminismButNotMagicLiteralWarnings()
    {
        var o = ReviewOrchestrator.CreateDefault();
        var issues = new List<ReviewIssue>();
        issues.AddRange(o.Run("Assets/VEVE/Sim/Systems/Foo.cs", new[] { "float drag = 0.31f;" }));
        Assert.AreEqual(1, issues.Count);
        Assert.IsFalse(ReviewOrchestrator.ShouldBlockGate(issues), "warning must not block");

        var blocking = new List<ReviewIssue>();
        blocking.AddRange(o.Run("Assets/VEVE/Sim/Systems/Foo.cs", new[] { "int r = Environment.TickCount;", }));
        Assert.AreEqual(1, blocking.Count);
        Assert.IsTrue(ReviewOrchestrator.ShouldBlockGate(blocking));
    }
}
