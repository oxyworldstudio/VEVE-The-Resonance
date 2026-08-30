using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using VEVE.UI;

public sealed class UiFactoryTests
{
    [Test]
    public void FactoryReturnsComponentsWithAndWithoutExplicitParents()
    {
        Canvas canvas = UiFactory.CreateCanvas("UiFactoryTestCanvas", 500);
        try
        {
            RectTransform root = canvas.GetComponent<RectTransform>();
            Assert.That(root, Is.Not.Null);

            Image panel = UiFactory.CreatePanel(root, "Panel", Color.white);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.rectTransform, Is.Not.Null);
            Assert.That(panel.rectTransform.parent, Is.SameAs(root));

            Text text = UiFactory.CreateText(panel, "Text", "TARGET ACQUIRED", 16, Color.white);
            Assert.That(text, Is.Not.Null);
            Assert.That(text.font, Is.Not.Null);
            Assert.That(text.fontSize,
                Is.InRange(UiFactory.MinReadableFont, UiFactory.MaxReadableFont));

            Slider slider = UiFactory.CreateSlider(panel, "Slider",
                HudThemeLibrary.SliderTrack, HudThemeLibrary.Olive,
                new Vector2(240f, 12f), Vector2.zero, 0.5f);
            Assert.That(slider, Is.Not.Null);
            Assert.That(slider.fillRect, Is.Not.Null);
            Assert.That(slider.value, Is.EqualTo(0.5f).Within(0.001f));

            Button button = UiFactory.CreateTableButton(panel, "Button", "DEPLOY",
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.targetGraphic, Is.Not.Null);
            Assert.That(button.GetComponentInChildren<Text>(), Is.Not.Null);

            Image arc = UiFactory.CreateRadialArc(panel, "Arc", HudThemeLibrary.Amber, 0.5f);
            Assert.That(arc.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(arc.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(arc.sprite, Is.Not.Null);

            var vHost = new GameObject("VHost", typeof(RectTransform));
            ((RectTransform)vHost.transform).SetParent(root, false);
            var hHost = new GameObject("HHost", typeof(RectTransform));
            ((RectTransform)hHost.transform).SetParent(root, false);
            try
            {
                VerticalLayoutGroup vlayout = UiFactory.CreateVLayout(vHost.GetComponent<RectTransform>(), 6f,
                    new RectOffset(2, 2, 2, 2), false);
                HorizontalLayoutGroup hlayout = UiFactory.CreateHLayout(hHost.GetComponent<RectTransform>(), 6f,
                    new RectOffset(2, 2, 2, 2), false);
                GridLayoutGroup grid = UiFactory.CreateGrid(panel, new Vector2(50f, 50f),
                    new Vector2(4f, 4f), 5);
                Assert.That(vlayout.spacing, Is.EqualTo(6f));
                Assert.That(hlayout.spacing, Is.EqualTo(6f));
                Assert.That(grid.constraintCount, Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(vHost);
                Object.DestroyImmediate(hHost);
            }

            RectTransform content;
            ScrollRect scroll = UiFactory.CreateScrollRect(panel, out content);
            Assert.That(scroll, Is.Not.Null);
            Assert.That(content, Is.Not.Null);
            Assert.That(scroll.content, Is.SameAs(content));
        }
        finally
        {
            Object.DestroyImmediate(canvas.gameObject);
        }
    }

    [Test]
    public void CreatingElementsWithNullParentsThrowsNoExceptions()
    {
        List<GameObject> spawned = new List<GameObject>();
        Assert.That(() =>
        {
            spawned.Add(UiFactory.CreatePanel(null, "NullPanel", Color.white).gameObject);
            spawned.Add(UiFactory.CreateText(null, "NullText", "x", 16, Color.white).gameObject);
            spawned.Add(UiFactory.CreateImage(null, "NullImage", Color.white).gameObject);
            spawned.Add(UiFactory.CreateSlider(null, "NullSlider", Color.black, Color.white,
                new Vector2(100f, 8f), Vector2.zero).gameObject);
            spawned.Add(UiFactory.CreateTableButton(null, "NullButton", "OK",
                Color.white, Color.black).gameObject);
            spawned.Add(UiFactory.CreateRadialArc(null, "NullArc", Color.white, 1f).gameObject);
            Assert.That(UiFactory.GetSerializedFieldNames(null), Is.Not.Null.And.Empty);
        }, Throws.Nothing);

        foreach (GameObject go in spawned)
        {
            if (go != null)
                Assert.That(go.GetComponentsInParent<RectTransform>().Length, Is.GreaterThan(0));
        }
        foreach (GameObject go in spawned)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void FontSizesStayWithinAccessibilityRange()
    {
        UiFactory.ClearAccessibilityCache();
        Assert.That(UiFactory.ScaleFontSize(2), Is.EqualTo(UiFactory.MinReadableFont));
        Assert.That(UiFactory.ScaleFontSize(16), Is.EqualTo(16));
        Assert.That(UiFactory.ScaleFontSize(240), Is.EqualTo(UiFactory.MaxReadableFont));

        GameObject host = new GameObject("UiFactoryAccessHost");
        try
        {
            AccessibilitySettings settings = host.AddComponent<AccessibilitySettings>();
            settings.TextScale = 3f;
            UiFactory.ClearAccessibilityCache();
            Assert.That(UiFactory.ScaleFontSize(24), Is.EqualTo(UiFactory.MaxReadableFont));
            Assert.That(UiFactory.ScaleFontSize(36), Is.EqualTo(UiFactory.MaxReadableFont));
            Assert.That(UiFactory.ScaleFontSize(148), Is.EqualTo(UiFactory.MaxReadableFont));

            settings.TextScale = 0.5f;
            UiFactory.ClearAccessibilityCache();
            Assert.That(UiFactory.ScaleFontSize(10), Is.EqualTo(UiFactory.MinReadableFont));
            Assert.That(UiFactory.ScaleFontSize(24), Is.EqualTo(12));

            Text scaled = UiFactory.CreateText(null, "Scaled", "abc", 96, Color.white);
            try
            {
                Assert.That(scaled.fontSize,
                    Is.InRange(UiFactory.MinReadableFont, UiFactory.MaxReadableFont));
            }
            finally
            {
                Object.DestroyImmediate(scaled.gameObject);
            }
        }
        finally
        {
            UiFactory.ClearAccessibilityCache();
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ThemeColorsStayInSrgbUnitRange()
    {
        foreach (Color color in HudThemeLibrary.AllColors)
        {
            Assert.That(color.r, Is.InRange(0f, 1f), "red channel of " + color);
            Assert.That(color.g, Is.InRange(0f, 1f), "green channel of " + color);
            Assert.That(color.b, Is.InRange(0f, 1f), "blue channel of " + color);
            Assert.That(color.a, Is.InRange(0f, 1f), "alpha channel of " + color);
        }
        Assert.That(HudThemeLibrary.AllColors.Count, Is.GreaterThanOrEqualTo(10));

        Color adjusted = HudThemeLibrary.WithAlpha(HudThemeLibrary.Olive, 1.5f);
        Assert.That(adjusted.a, Is.EqualTo(1f));
        Color negative = HudThemeLibrary.WithAlpha(new Color(-1f, 2f, 0.5f, 0.5f), -0.2f);
        Assert.That(negative.r, Is.EqualTo(0f));
        Assert.That(negative.g, Is.EqualTo(1f));
    }

    [Test]
    public void HudControllerOwnershipMapDetectsLegacyBindings()
    {
        GameObject host = new GameObject("LegacyHudHost");
        try
        {
            HUDController hud = host.AddComponent<HUDController>();
            Assert.That(HudThemeLibrary.HudControllerOwns(hud, "ammoText"), Is.True);
            Assert.That(HudThemeLibrary.HudControllerOwns(hud, "killFeedText"), Is.True);
            Assert.That(HudThemeLibrary.HudControllerOwns(hud, "compassText"), Is.True);
            Assert.That(HudThemeLibrary.HudControllerOwns(hud, "damageIndicator"), Is.True);
            Assert.That(HudThemeLibrary.HudControllerOwns(hud, "staminaBar"), Is.True);
            Assert.That(HudThemeLibrary.HudControllerOwns(hud, "notARealField"), Is.False);
            Assert.That(HudThemeLibrary.HudControllerOwns(null, "ammoText"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void GeneratedSpritesAreCachedAndValid()
    {
        Sprite solid = UiFactory.GetSolidSprite();
        Sprite radial = UiFactory.GetRadialSprite();
        Sprite vignette = UiFactory.GetVignetteSprite();
        Assert.That(solid, Is.Not.Null);
        Assert.That(radial, Is.Not.Null);
        Assert.That(vignette, Is.Not.Null);
        Assert.That(UiFactory.GetSolidSprite(), Is.SameAs(solid));
        Assert.That(UiFactory.GetRadialSprite(), Is.SameAs(radial));
        Assert.That(vignette.texture.width, Is.GreaterThan(1));
        Assert.That(radial.bounds.size.x, Is.GreaterThan(0f));
    }
}
