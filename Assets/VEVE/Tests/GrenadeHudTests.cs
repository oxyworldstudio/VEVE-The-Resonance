using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using VEVE.Combat;
using VEVE.UI;

public sealed class GrenadeHudTests
{
    [Test]
    public void PresenterFormatsAndPulses()
    {
        Assert.AreEqual("FRAG x3", GrenadeHudPresenter.Format(3));
        Assert.AreEqual("FRAG x0", GrenadeHudPresenter.Format(0));
        Assert.AreEqual("FRAG x0", GrenadeHudPresenter.Format(-2), "negative clamps to zero in display");
        Assert.IsFalse(GrenadeHudPresenter.ShouldPulse(3));
        Assert.IsTrue(GrenadeHudPresenter.ShouldPulse(0));
    }

    [Test]
    public void LabelColorSwitchesOnEmpty()
    {
        Color normal = new Color(0.9f, 0.9f, 0.85f, 1f);
        Color empty = new Color(0.78f, 0.19f, 0.15f, 1f);
        Assert.AreEqual(normal, GrenadeHudPresenter.LabelColor(3, normal, empty));
        Assert.AreEqual(empty, GrenadeHudPresenter.LabelColor(0, normal, empty));
    }

    [Test]
    public void InventoryRulesBlockAtZeroAndRestockCaps()
    {
        Assert.IsFalse(GrenadeInventoryRules.CanThrow(0));
        Assert.IsTrue(GrenadeInventoryRules.CanThrow(1));
        Assert.AreEqual(2, GrenadeInventoryRules.AfterThrow(3));
        Assert.AreEqual(0, GrenadeInventoryRules.AfterThrow(0), "never negative");
        Assert.AreEqual(3, GrenadeInventoryRules.Restock(0, 3), "full restock from empty");
        Assert.AreEqual(3, GrenadeInventoryRules.Restock(2, 3), "capped at allowance");
        Assert.AreEqual(5, GrenadeInventoryRules.Restock(5, 3), "never reduces an over-cap count");
    }

    [Test]
    public void ThrowBlockedReasonIsPresentOnlyWhenEmpty()
    {
        Assert.IsEmpty(GrenadeInventoryRules.ThrowBlockedReason(2));
        Assert.AreEqual("out of grenades", GrenadeInventoryRules.ThrowBlockedReason(0));
    }

    [Test]
    public void UsableCountRangeGuards()
    {
        Assert.IsFalse(GrenadeInventoryRules.IsUsableCount(-1));
        Assert.IsTrue(GrenadeInventoryRules.IsUsableCount(0));
        Assert.IsTrue(GrenadeInventoryRules.IsUsableCount(3));
        Assert.IsFalse(GrenadeInventoryRules.IsUsableCount(100));
    }
}
