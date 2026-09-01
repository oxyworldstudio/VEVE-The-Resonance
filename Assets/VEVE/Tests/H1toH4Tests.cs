using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using VEVE.AI;
using VEVE.Catalog;
using VEVE.Combat;
using VEVE.Content;
using VEVE.Net;
using VEVE.Scoring;
using VEVE.UI;

public sealed class H1toH4Tests
{
    // ------------------------------------------------------------------ H1

    [Test]
    public void SpreadShrinksWithSkillAndStaysNonNegative()
    {
        float novice = WeaponHandlingRules.SpreadDegrees(2f, 0);
        float master = WeaponHandlingRules.SpreadDegrees(2f, 100);
        Assert.Greater(novice, master);
        Assert.GreaterOrEqual(master, 0.4f - 1e-5f, "2deg * 0.2 retention (float ulp safe)");
        Assert.AreEqual(0f, WeaponHandlingRules.SpreadDegrees(0f, 50));
        Assert.AreEqual(0f, WeaponHandlingRules.SpreadDegrees(float.NaN, 50), "NaN sanitized");
    }

    [Test]
    public void RecoilMultiplierMonotonicWithFloor()
    {
        Assert.AreEqual(1f, WeaponHandlingRules.RecoilMultiplier(0), 1e-5f);
        Assert.Less(WeaponHandlingRules.RecoilMultiplier(100), WeaponHandlingRules.RecoilMultiplier(50));
        Assert.GreaterOrEqual(WeaponHandlingRules.RecoilMultiplier(100), 0.2f, "floor");
    }

    // ------------------------------------------------------------------ H2

    [Test]
    public void ThrowBandAndArcHeuristic()
    {
        Assert.IsFalse(AiThrowRules.ShouldThrow(true, 2f, 12f, false), "too close");
        Assert.IsFalse(AiThrowRules.ShouldThrow(true, 25f, 12f, false), "too far");
        Assert.IsFalse(AiThrowRules.ShouldThrow(false, 8f, 12f, true), "not engaged");
        Assert.IsFalse(AiThrowRules.ShouldThrow(true, 8f, 12f, false), "cooldown running");
        Assert.IsTrue(AiThrowRules.ShouldThrow(true, 8f, 12f, true));

        Vector3 v = AiThrowRules.ThrowVelocity(Vector3.zero, new Vector3(10f, 0f, 0f));
        Assert.Greater(v.x, 0f, "lobbed toward target");
        Assert.That(v.y, Is.InRange(AiThrowRules.MinUpMps, AiThrowRules.MaxUpMps));
        Assert.AreEqual(0f, AiThrowRules.ThrowVelocity(Vector3.zero, Vector3.zero).magnitude, 1e-6f, "no target: no throw");
    }

    // ------------------------------------------------------------------ H3

    [Test]
    public void LedgerIsolationPersistsThroughSaveLoad()
    {
        var original = new FamilyXpLedger();
        original.Grant(11, "m4a1", 500);
        original.Grant(22, "scar-h", 120);
        original.Grant(22, "scar-h", 240); // both under per-event cap: ledger accumulates

        var memory = new Dictionary<string, string>();
        var store = new H3MemoryStore(memory);
        ProgressionPersistence.Save(original, store);
        Assert.IsTrue(memory.ContainsKey(ProgressionPersistence.LedgerKey));

        var restored = new FamilyXpLedger();
        ProgressionPersistence.Load(restored, store);
        Assert.AreEqual(360d, restored.Xp(22, "scar-h"), 1e-6, "120 + 240 accumulated");
        Assert.AreEqual(original.Export(), restored.Export(), "round-trip equality");
    }

    private sealed class H3MemoryStore : VEVE.Content.IKeyValueStore
    {
        private readonly Dictionary<string, string> map;
        public H3MemoryStore(Dictionary<string, string> backing) { map = backing; }
        public string Get(string key) => map.TryGetValue(key ?? string.Empty, out string v) ? v : string.Empty;
        public void Set(string key, string value) => map[key ?? string.Empty] = value ?? string.Empty;
    }

    // ------------------------------------------------------------------ H4

    [Test]
    public void ReconcilerTelemetryEndsInDebriefData()
    {
        var rec = new VEVE.Net.PredictionReconciler();
        rec.Reconcile(new VEVE.Net.ShotReplayWindow(), 9, 50, true, null, "m4a1", 6);
        var data = new DebriefData
        {
            headline = "Op",
            score = new MissionScoreBreakdown { rank = MissionRank.Operator },
            reconcileTelemetry = rec.Telemetry
        };
        string body = DebriefModel.Format(data);
        StringAssert.Contains("confirmed", body);
    }
}


