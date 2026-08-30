using NUnit.Framework;
using VEVE.UI.Personalization;

/// <summary>
/// Locks the documented zero-table formulas:
///   holdover(d | zero z) = g*d^2/(2v^2) - g*z^2/(2v^2)   (vacuum, via VEVE.Ballistics)
///   1 MRAD = 0.001 m per metre; 1 MOA = 0.0002908882 m per metre; clicks = angle/click.
/// </summary>
public sealed class PersZeroingMathTests
{
    private const double G = 9.80665;

    [Test]
    public void HoldoverMatchesDocumentedClosedForm()
    {
        float v = 880f;   // AK-74M muzzle velocity
        float z = 100f;
        float d = 400f;
        double expected = G * (d * (double)d - z * (double)z) / (2.0 * v * v);
        Assert.That(ZeroingMath.HoldoverMetres(v, d, z),
            Is.EqualTo((float)expected).Within(1e-3f));
    }

    [Test]
    public void HoldoverVanishesAtZeroPlaneAndSignsBelowIt()
    {
        Assert.That(ZeroingMath.HoldoverMetres(800f, 250f, 250f), Is.EqualTo(0f).Within(1e-5f));
        Assert.That(ZeroingMath.HoldoverMetres(800f, 100f, 250f), Is.LessThan(0f),
            "inside the zero distance the vacuum table reads negative (hold low)");
    }

    [Test]
    public void AngleConversionsUseSubtensionConstants()
    {
        Assert.That(ZeroingMath.MetresToMil(1f, 1000f), Is.EqualTo(1f).Within(1e-4f),
            "1 m at 1000 m = 1 MRAD");
        Assert.That(ZeroingMath.MetresToMoa(0.0002908882f * 100f, 100f), Is.EqualTo(1f).Within(1e-3f),
            "1 MOA at 100 m ~ 2.9089 cm");
        Assert.That(ZeroingMath.MetresToMil(5f, 0f), Is.EqualTo(0f), "non-positive range guard");
        Assert.That(ZeroingMath.Clicks(1.5f, 0.1f), Is.EqualTo(15f).Within(1e-4f));
        Assert.That(ZeroingMath.Clicks(1.5f, 0f), Is.EqualTo(0f), "per-click guard");
    }

    [Test]
    public void VacuumDropAgreesWithBallisticsApi()
    {
        float direct = VEVE.Ballistics.GravityDrop(790f, 300f);
        Assert.That(ZeroingMath.VacuumDropMetres(790f, 300f), Is.EqualTo(direct));
    }

    [Test]
    public void ZeroCrossingsBracketTheSightedInDistance()
    {
        // 880 m/s, 100 m zero, 4.5 cm bore offset: second crossing sits at/near the zero,
        // near crossing well inside it (the classic two-zero pattern).
        Assert.That(ZeroingMath.TryZeroCrossings(880f, 100f,
            ZeroingMath.DefaultSightHeightMeters, out float near, out float far), Is.True);
        Assert.That(near, Is.GreaterThan(0f));
        Assert.That(near, Is.LessThan(far));
        Assert.That(far, Is.InRange(95f, 105f));
    }

    [Test]
    public void ZeroCrossingsRejectImpossibleInputs()
    {
        Assert.That(ZeroingMath.TryZeroCrossings(0f, 100f, 0.045f, out _, out _), Is.False);
        Assert.That(ZeroingMath.TryZeroCrossings(900f, -5f, 0.045f, out _, out _), Is.False);
        Assert.That(ZeroingMath.TryZeroCrossings(900f, 100f, -1f, out _, out _), Is.False);
    }

    [Test]
    public void TableStepBoundsCoverAtLeastOneRowForPistols()
    {
        // Documented rule: rows run from 100 m to floor(max(effectiveRange,100)/100)*100,
        // so a 50 m-effective pistol still yields exactly one 100 m row.
        float effective = 50f;
        float maxRange = UnityEngine.Mathf.Max(ZeroingPanel.StepMeters,
            UnityEngine.Mathf.FloorToInt(UnityEngine.Mathf.Max(effective, ZeroingPanel.StepMeters)
                / ZeroingPanel.StepMeters) * ZeroingPanel.StepMeters);
        int rows = 0;
        for (float d = ZeroingPanel.StepMeters; d <= maxRange + 0.01f; d += ZeroingPanel.StepMeters)
            rows++;
        Assert.That(rows, Is.EqualTo(1));

        float dm = 800f;
        maxRange = UnityEngine.Mathf.Max(ZeroingPanel.StepMeters,
            UnityEngine.Mathf.FloorToInt(UnityEngine.Mathf.Max(dm, ZeroingPanel.StepMeters)
                / ZeroingPanel.StepMeters) * ZeroingPanel.StepMeters);
        Assert.That(maxRange, Is.EqualTo(800f));
    }
}
