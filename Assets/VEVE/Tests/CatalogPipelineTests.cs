using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using VEVE.Content;

public sealed class CatalogPipelineTests
{
    [Test]
    public void CodecEscapingRoundTripsSymbols()
    {
        string tricky = "a=b|c\nd%e";
        string enc = PayloadCodec.Escape(tricky);
        Assert.AreNotEqual(tricky, enc);
        Assert.IsTrue(enc.IndexOf('\n') < 0, "raw newline must be escaped");
        Assert.AreEqual(tricky, PayloadCodec.Unescape(enc));
    }

    [Test]
    public void MissionPayloadRoundTrips()
    {
        MissionContentCatalog.TryGet("DESERT_RIDGELINE", out MissionTemplate original);
        Assert.IsNotNull(original.id);
        string encoded = MissionPayloadCodec.Encode(original);
        MissionTemplate decoded = MissionPayloadCodec.Decode(encoded);

        Assert.AreEqual(original.id, decoded.id);
        Assert.AreEqual(original.title, decoded.title);
        Assert.AreEqual(original.regionKey, decoded.regionKey);
        Assert.AreEqual(original.parSeconds, decoded.parSeconds);
        Assert.AreEqual(original.enemySquadPairs, decoded.enemySquadPairs);
        Assert.AreEqual(original.alertBias, decoded.alertBias, 1e-6d);
        Assert.AreEqual(original.intelObjectiveWeight, decoded.intelObjectiveWeight);
        CollectionAssert.AreEqual(original.objectiveSummary, decoded.objectiveSummary);
    }

    [Test]
    public void DecodeMissingPayloadIsSafe()
    {
        MissionTemplate t = MissionPayloadCodec.Decode(null);
        Assert.AreEqual(0, t.parSeconds);
        Assert.AreEqual(string.Empty, t.id);
        Assert.IsNotNull(t.objectiveSummary);
        Assert.AreEqual(0, t.objectiveSummary.Length);
    }

    [Test]
    public void SelectionRuleAssetsBeatBuiltinAndEmptyFallsBack()
    {
        MissionTemplate[] all = MissionContentCatalog.All;

        IReadOnlyList<MissionTemplate> fromNull = MissionCatalogSource.Select(null, null);
        Assert.AreEqual(all.Length, fromNull.Count);
        IReadOnlyList<MissionTemplate> fromEmpty = MissionCatalogSource.Select(new CatalogItemAsset[0], null);
        Assert.AreEqual(all.Length, fromEmpty.Count);

        CatalogItemAsset asset = ScriptableObject.CreateInstance<CatalogItemAsset>();
        var junk = ScriptableObject.CreateInstance<CatalogItemAsset>();
        try
        {
            asset.Configure(CatalogItemKind.Mission, "MINE", MissionPayloadCodec.Encode(new MissionTemplate
            {
                id = "MINE", title = "Designer Op", regionKey = "DESERT_CHECKPOINT",
                parSeconds = 300, enemySquadPairs = 1, alertBias = 0.1f, intelObjectiveWeight = 1.0,
                objectiveSummary = new[] { "Primary: custom" }
            }));
            junk.Configure(CatalogItemKind.Mission, string.Empty, "");

            IReadOnlyList<MissionTemplate> chosen = MissionCatalogSource.Select(new[] { asset }, null);
            Assert.AreEqual(1, chosen.Count);
            Assert.AreEqual("MINE", chosen[0].id);

            IReadOnlyList<MissionTemplate> mixed = MissionCatalogSource.Select(new[] { asset, junk }, null);
            Assert.AreEqual(1, mixed.Count, "junk asset ids do not pollute the pool");
        }
        finally
        {
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(junk);
        }
    }

    [Test]
    public void SchedulerRespectsDesignerPool()
    {
        var pool = new List<MissionTemplate>
        {
            new MissionTemplate { id = "DES_A", regionKey = "DESERT_CHECKPOINT", parSeconds = 100, objectiveSummary = new[] { "x" } },
            new MissionTemplate { id = "DES_B", regionKey = "DESERT_CHECKPOINT", parSeconds = 100, objectiveSummary = new[] { "y" } }
        };
        MissionTemplate a = MissionScheduler.Draft("DESERT_CHECKPOINT", 0, pool);
        MissionTemplate b = MissionScheduler.Draft("DESERT_CHECKPOINT", 1, pool);
        Assert.IsTrue(pool.Contains(a));
        Assert.AreNotEqual(a.id, b.id, "designer pool must visibly cycle too");

        MissionTemplate other = MissionScheduler.Draft("ATLANTIS", 7, pool);
        Assert.IsTrue(pool.Contains(other), "unknown region stays inside the provided pool, no builtin spill");
    }
}
