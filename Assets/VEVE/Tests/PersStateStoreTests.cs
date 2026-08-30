using System;
using System.Collections.Generic;
using NUnit.Framework;
using VEVE.UI.Personalization;

/// <summary>
/// Round trip, dictionary helper and migration coverage for UserLoadoutSelection /
/// PersonalizationStateStore. All IO goes through an injected in-memory provider, so the
/// persistentDataPath is never touched.
/// </summary>
public sealed class PersStateStoreTests
{
    private sealed class FakeProvider : ILoadoutFileProvider
    {
        public readonly Dictionary<string, string> Files =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public int WriteCount;

        public bool Exists(string path) => Files.ContainsKey(path);
        public string ReadAllText(string path) => Files[path];
        public void WriteAllText(string path, string text)
        {
            Files[path] = text;
            WriteCount++;
        }
    }

    [Test]
    public void JsonRoundTripPreservesAllFields()
    {
        var sel = new UserLoadoutSelection
        {
            operatorId = "op-wraith",
            weaponId = "hk416",
            finishId = "fde",
        };
        sel.SetAttachment("OPTIC", "optic_holo");
        sel.SetAttachment("MUZZLE", "muzzle_comp");
        sel.SetGear("PLATE_CARRIER", "plate_sapi_h");
        sel.SetGear("BOOTS", "boots_tan");

        string json = sel.ToJson();
        Assert.That(json, Does.Contain("op-wraith"));

        UserLoadoutSelection loaded = UserLoadoutSelection.FromJson(json);
        Assert.That(loaded.version, Is.EqualTo(UserLoadoutSelection.CurrentVersion));
        Assert.That(loaded.weaponId, Is.EqualTo("hk416"));
        Assert.That(loaded.finishId, Is.EqualTo("fde"));
        Assert.That(loaded.TryGetAttachment("optic", out string optic), Is.True);
        Assert.That(optic, Is.EqualTo("optic_holo"));
        Assert.That(loaded.TryGetGear("boots", out string boots), Is.True);
        Assert.That(boots, Is.EqualTo("boots_tan"));
        Assert.That(loaded.attachedSlots.Count, Is.EqualTo(2));
    }

    [Test]
    public void SetPairOverwritesAndEmptyValueClears()
    {
        var sel = new UserLoadoutSelection();
        sel.SetAttachment("GRIP", "grip_ergonomic");
        sel.SetAttachment("grip", "grip_angle"); // case-insensitive replace
        Assert.That(sel.attachedSlots.Count, Is.EqualTo(1));
        Assert.That(sel.TryGetAttachment("GRIP", out string v), Is.True);
        Assert.That(v, Is.EqualTo("grip_angle"));

        sel.SetAttachment("GRIP", "");
        Assert.That(sel.attachedSlots.Count, Is.EqualTo(0));
        Assert.That(sel.TryGetAttachment("GRIP", out _), Is.False);
        // Null keys are ignored defensively.
        Assert.That(() => sel.SetGear(null, "x"), Throws.Nothing);
    }

    [Test]
    public void FromJsonToleratesGarbageAndEmpties()
    {
        Assert.That(UserLoadoutSelection.FromJson(null), Is.Not.Null);
        Assert.That(UserLoadoutSelection.FromJson(string.Empty).version,
            Is.EqualTo(UserLoadoutSelection.CurrentVersion));
        UserLoadoutSelection garbage = UserLoadoutSelection.FromJson("{not json");
        Assert.That(garbage.weaponId ?? string.Empty, Is.EqualTo(string.Empty),
            "malformed payload falls back to a fresh selection");
    }

    [Test]
    public void MigrateIsGuardedNoOpAndBumpsStaleVersions()
    {
        var sel = new UserLoadoutSelection { weaponId = "m4a1" };
        Assert.That(sel.Migrate(), Is.False, "current-version selection must be a no-op");

        // Simulate a legacy v0 payload written by hand.
        string legacy = "{\"version\":0,\"operatorId\":\"op-legacy\"}";
        UserLoadoutSelection old = UserLoadoutSelection.FromJson(legacy);
        Assert.That(old.version, Is.EqualTo(0));
        Assert.That(old.Migrate(), Is.True);
        Assert.That(old.version, Is.EqualTo(UserLoadoutSelection.CurrentVersion));
        Assert.That(old.operatorId, Is.EqualTo("op-legacy"));
        Assert.That(old.attachedSlots, Is.Not.Null);
        Assert.That(old.gearSlots, Is.Not.Null);
    }

    [Test]
    public void StoreSavesThenLoadsThroughInjectedProvider()
    {
        var provider = new FakeProvider();
        const string path = "mem://loadout.json";

        var store = new PersonalizationStateStore(provider, path);
        store.Selection.weaponId = "scar-h";
        store.Selection.SetGear("HELMET", "ops_core");
        Assert.That(store.Save(), Is.True);
        Assert.That(provider.WriteCount, Is.EqualTo(1));
        Assert.That(provider.Files[path], Does.Contain("scar-h"));

        var reopened = new PersonalizationStateStore(provider, path);
        Assert.That(reopened.Load(), Is.True);
        Assert.That(reopened.Selection.weaponId, Is.EqualTo("scar-h"));
        Assert.That(reopened.Selection.TryGetGear("helmet", out string helmet), Is.True);
        Assert.That(helmet, Is.EqualTo("ops_core"));
        Assert.That(reopened.LastError, Is.Null);
    }

    [Test]
    public void StoreLoadOnMissingFileReturnsFalseWithoutThrowing()
    {
        var store = new PersonalizationStateStore(new FakeProvider(), "mem://absent.json");
        Assert.That(store.Load(), Is.False);
        Assert.That(store.LastError, Is.Null);
    }

    [Test]
    public void StoreConstructorPerformsNoFileAccess()
    {
        // The provider is never queried during construction — only at Save/Load time.
        var store = new PersonalizationStateStore(new FaultyProvider());
        Assert.That(store.Selection, Is.Not.Null);
    }

    private sealed class FaultyProvider : ILoadoutFileProvider
    {
        public bool Exists(string path) => throw new NotSupportedException();
        public string ReadAllText(string path) => throw new NotSupportedException();
        public void WriteAllText(string path, string text) => throw new NotSupportedException();
    }
}
