using NUnit.Framework;
using UnityEngine;
using VEVE.UI;

public sealed class ScopeReticleOverlayTests
{
    [Test]
    public void PixelsPerMoaMatchesGeometry()
    {
        float ppm = ScopeReticleOverlay.PixelsPerMoa(1920f, 6f);
        Assert.That(ppm, Is.EqualTo(1920f / 360f).Within(0.001f), "6 deg picture: 5.33 px/MOA");
    }

    [Test]
    public void NarrowerFovYieldsMorePixelsPerMoa()
    {
        Assert.Greater(ScopeReticleOverlay.PixelsPerMoa(1920f, 2f),
                       ScopeReticleOverlay.PixelsPerMoa(1920f, 6f));
    }

    [Test]
    public void InvalidFovFallsBackToDefaultRatio()
    {
        float def = ScopeReticleOverlay.DefaultCanvasWidthPx / (ScopeReticleOverlay.DefaultFieldOfViewDegrees * 60f);
        Assert.AreEqual(def, ScopeReticleOverlay.PixelsPerMoa(0f, 6f), 0.01f);
        Assert.AreEqual(def, ScopeReticleOverlay.PixelsPerMoa(1920f, 0f), 0.01f);
        Assert.AreEqual(def, ScopeReticleOverlay.PixelsPerMoa(1920f, 200f), 0.01f);
        Assert.AreEqual(def, ScopeReticleOverlay.PixelsPerMoa(float.NaN, 4f), 0.01f);
    }

    [Test]
    public void MarkerOffsetSignPlacesTargetBelowCenterWhenHoldingHigh()
    {
        // hold high (+MOA) means the round hits above the point of aim for the observed
        // distance => the shooter must place the target BELOW center: marker Y negative.
        Assert.Less(ScopeReticleOverlay.MarkerOffsetY(+4.9f, 5.33f), 0f);
        Assert.Greater(ScopeReticleOverlay.MarkerOffsetY(-1.3f, 5.33f), 0f);
        Assert.AreEqual(0f, ScopeReticleOverlay.MarkerOffsetY(0f, 5.33f), 0.001f);
    }

    [Test]
    public void MarkerOffsetClampsToMax()
    {
        Assert.AreEqual(-ScopeReticleOverlay.MaxMarkerOffsetPx,
            ScopeReticleOverlay.MarkerOffsetY(999f, 5.33f), 0.001f, "hold high clamps the marker below center");
        Assert.AreEqual(ScopeReticleOverlay.MaxMarkerOffsetPx,
            ScopeReticleOverlay.MarkerOffsetY(-999f, 5.33f), 0.001f, "hold low clamps the marker above center");
    }

    [Test]
    public void MarkerOffsetIsNanNullSafe()
    {
        Assert.AreEqual(0f, ScopeReticleOverlay.MarkerOffsetY(float.NaN, 5.33f), 0.001f);
        Assert.AreEqual(0f, ScopeReticleOverlay.MarkerOffsetY(2f, float.NaN), 0.001f);
        Assert.LessOrEqual(Mathf.Abs(ScopeReticleOverlay.MarkerOffsetY(2f, 0f)),
            ScopeReticleOverlay.MaxMarkerOffsetPx);
    }

    [Test]
    public void HoldLabelFormattingIsInvariantAndSigned()
    {
        string s = ScopeReticleOverlay.HoldLabel(new ScopeTelemetryEvent { holdoverMoa = 4.87f, distanceMeters = 118f });
        Assert.That(s, Does.Contain("+4.9"), "trailing one-decimal with sign: " + s);
        Assert.That(s, Does.Contain("MOA"));
        Assert.That(s, Does.Contain("118 m"));

        string neg = ScopeReticleOverlay.HoldLabel(new ScopeTelemetryEvent { holdoverMoa = -1.26f, distanceMeters = 62f });
        Assert.That(neg, Does.Contain("-1.3"), "negative keeps explicit minus: " + neg);

        Assert.AreEqual(string.Empty, ScopeReticleOverlay.HoldLabel(null));
        Assert.That(ScopeReticleOverlay.HoldLabel(new ScopeTelemetryEvent { holdoverMoa = float.NaN, distanceMeters = -4f }),
            Does.Contain("0.0 MOA @ 0 m"), "NaN input renders sanitized zero");
    }
}
