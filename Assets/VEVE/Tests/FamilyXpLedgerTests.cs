using NUnit.Framework;
using VEVE.Catalog;

public sealed class FamilyXpLedgerTests
{
    private const ulong ClientA = 1, ClientB = 2, Offline = 0, MaxReserved = ulong.MaxValue;

    [Test]
    public void OwnersAreIsolatedAndOfflineNeverGrants()
    {
        var l = new FamilyXpLedger();
        l.Grant(ClientA, "m4a1", 100);
        l.Grant(ClientB, "m4a1", 25);
        l.Grant(Offline, "m4a1", 500); // single-player pipeline stays uncredited here

        Assert.AreEqual(100d, l.Xp(ClientA, "m4a1"));
        Assert.AreEqual(25d, l.Xp(ClientB, "m4a1"));
        Assert.AreEqual(0d, l.Xp(Offline, "m4a1"));
        Assert.AreEqual(2, l.Count, "offline keys were never created");

        l.Grant(ClientA, "ak74m", 7);
        Assert.AreEqual(0d, l.Xp(ClientB, "ak74m"), "family isolation between owners");
    }

    [Test]
    public void BadInputsAreRefusedAndTotalsCapped()
    {
        var l = new FamilyXpLedger();
        l.Grant(ClientA, null, 50);
        l.Grant(ClientA, "", 50);
        l.Grant(ClientA, "glock17", -3);
        l.Grant(ClientA, "glock17", double.NaN);
        Assert.AreEqual(0d, l.Xp(ClientA, "glock17"));

        for (int i = 0; i < 200; i++) l.Grant(ClientA, "hk416", 1000);
        Assert.LessOrEqual(l.Xp(ClientA, "hk416"), FamilyXpLedger.CeilingTotal);
        Assert.Greater(l.Xp(ClientA, "hk416"), FamilyXpLedger.MaxGrant, "a stream of valid events keeps accumulating to the cap");

        // reserved id remaps instead of colliding
        Assert.IsFalse(FamilyXpLedger.Key(ClientA, "x") == FamilyXpLedger.Key(MaxReserved, "x"));
    }

    [Test]
    public void SkillMonotonicWithinFamily()
    {
        var l = new FamilyXpLedger();
        Assert.LessOrEqual(l.Skill(ClientA, "scar-h"), 100);
        l.Grant(ClientA, "scar-h", 1200);
        int mid = l.Skill(ClientA, "scar-h");
        for (int i = 0; i < 60; i++) l.Grant(ClientA, "scar-h", 240);
        Assert.Greater(l.Skill(ClientA, "scar-h"), mid);
        Assert.LessOrEqual(l.Skill(ClientA, "scar-h"), 100);
    }

    [Test]
    public void ExportImportRoundTrips()
    {
        var l = new FamilyXpLedger();
        l.Grant(ClientA, "p90", 330);
        l.Grant(ClientB, "m249", 45);
        string dump = l.Export();
        var copy = new FamilyXpLedger();
        copy.Import(dump);
        Assert.AreEqual(l.Xp(ClientA, "p90"), copy.Xp(ClientA, "p90"), 1e-6);
        Assert.AreEqual(l.Xp(ClientB, "m249"), copy.Xp(ClientB, "m249"), 1e-6);
        copy.Import("xp." + ClientA + "|" + "p90=-9"); // junk ignored entirely
        Assert.AreEqual(0, copy.Count);
    }
}
