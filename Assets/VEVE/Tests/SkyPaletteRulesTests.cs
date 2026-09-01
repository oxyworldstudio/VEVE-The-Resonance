using NUnit.Framework;
using UnityEngine;
using VEVE;
using VEVE.Graphics;

/// <summary>
/// EditMode validation for the deterministic <see cref="SkyPaletteRules"/> color system:
/// channel-level monotonicity, dawn/dusk warmth, night darkness, dust desaturation, star
/// visibility windows, clamping and NaN-safety. All assertions are structural (channels,
/// inequalities) rather than exact art colors, so the palette can be tuned freely.
/// </summary>
public sealed class SkyPaletteRulesTests
{
    private const float Eps = 0.0001f;

    private static float Luma(Color c)
    {
        return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }

    [Test]
    public void ZenithIsDarkAtNightAndBrighterAtNoon()
    {
        Color night = SkyPaletteRules.ZenithColor(0f, 0.4f);
        Color noon = SkyPaletteRules.ZenithColor(12f, 0.4f);
        Assert.Greater(Luma(noon), Luma(night) + 0.10f, "noon zenith must be clearly brighter than night");
        Assert.Less(Luma(night), 0.15f, "night zenith must stay dark");
    }

    [Test]
    public void ZenithLuminanceMonotonicRiseThenFall()
    {
        float prev = -1f;
        for (float h = 5f; h <= 12f; h += 0.5f)
        {
            float l = Luma(SkyPaletteRules.ZenithColor(h, 0.4f));
            Assert.GreaterOrEqual(l, prev - Eps, "zenith rises monotonically toward noon");
            prev = l;
        }

        prev = float.MaxValue;
        for (float h = 12f; h <= 19f; h += 0.5f)
        {
            float l = Luma(SkyPaletteRules.ZenithColor(h, 0.4f));
            Assert.LessOrEqual(l, prev + Eps, "zenith falls monotonically after noon");
            prev = l;
        }
    }

    [Test]
    public void HorizonWarmAtDawnAndDuskCoolBlueAtNoon()
    {
        Color noon = SkyPaletteRules.HorizonColor(12f, 0.4f, 0f);
        Assert.Greater(noon.b, noon.r, "noon horizon must be cool (blue dominant)");
        foreach (float h in new[] { 6f, 18f })
        {
            Color warm = SkyPaletteRules.HorizonColor(h, 0.4f, 0f);
            Assert.Greater(warm.r, warm.b + 0.15f, $"horizon at hour {h} must be warm (red dominant)");
        }
    }

    [Test]
    public void HorizonEmberBandsBracketTheNoonCoolness()
    {
        // Warmth peaks at 6/18 and decays toward 9/15, so 9:00 and 15:00 must sit
        // between the extremes: cooler than ember, warmer than noon.
        Color ember = SkyPaletteRules.HorizonColor(18f, 0.4f, 0f);
        Color mid = SkyPaletteRules.HorizonColor(15f, 0.4f, 0f);
        Color noon = SkyPaletteRules.HorizonColor(12f, 0.4f, 0f);
        float emberWarmth = ember.r - ember.b;
        float midWarmth = mid.r - mid.b;
        float noonWarmth = noon.r - noon.b;
        Assert.Greater(emberWarmth, midWarmth);
        Assert.Greater(midWarmth, noonWarmth);
    }

    [Test]
    public void HorizonNightIsNearBlack()
    {
        Color night = SkyPaletteRules.HorizonColor(0f, 0.4f, 0f);
        Assert.Less(Luma(night), 0.10f, "night horizon must be near black");
    }

    [Test]
    public void DustDesaturatesDayHorizonTowardTan()
    {
        Color clean = SkyPaletteRules.HorizonColor(12f, 0.4f, 0f);
        Color dusty = SkyPaletteRules.HorizonColor(12f, 0.4f, 1f);
        float cleanSat = Mathf.Max(clean.r, Mathf.Max(clean.g, clean.b)) - Mathf.Min(clean.r, Mathf.Min(clean.g, clean.b));
        float dustySat = Mathf.Max(dusty.r, Mathf.Max(dusty.g, dusty.b)) - Mathf.Min(dusty.r, Mathf.Min(dusty.g, dusty.b));
        Assert.Less(dustySat, cleanSat, "dust must desaturate the day horizon");
        Assert.Greater(dusty.r, dusty.b, "dusty horizon must lean sandy (red > blue)");
        // Dust must not brighten the night horizon into a glow.
        Color cleanNight = SkyPaletteRules.HorizonColor(0f, 0.4f, 0f);
        Color dustyNight = SkyPaletteRules.HorizonColor(0f, 0.4f, 1f);
        Assert.Less(Luma(dustyNight), Luma(cleanNight) + 0.05f, "dust keeps the night horizon dark");
    }

    [Test]
    public void HumidityLiftsNoonZenithTowardHaze()
    {
        Color dry = SkyPaletteRules.ZenithColor(12f, 0f);
        Color humid = SkyPaletteRules.ZenithColor(12f, 1f);
        Assert.Greater(Luma(humid), Luma(dry), "humidity must lift the noon zenith toward pale haze");
        foreach (Color c in new[] { dry, humid })
        {
            Assert.LessOrEqual(Mathf.Max(c.r, Mathf.Max(c.g, c.b)), 1f);
        }
    }

    [Test]
    public void SunTintWarmAtLowElevationWhiteAtNoon()
    {
        Color low = SkyPaletteRules.SunTint(2f);
        Assert.Greater(low.r, low.b + 0.25f, "low sun must be strongly warm");
        Color noon = SkyPaletteRules.SunTint(60f);
        Assert.Greater(noon.b, 0.85f, "high sun must be near white");
        Assert.Greater(noon.b, low.b, "blue channel must rise with elevation");
    }

    [Test]
    public void SunTintBlueChannelMonotonicInElevation()
    {
        float prev = -1f;
        for (float e = -6f; e <= 60f; e += 2f)
        {
            float b = SkyPaletteRules.SunTint(e).b;
            Assert.GreaterOrEqual(b, prev - Eps, "sun tint blue channel is non-decreasing");
            prev = b;
        }
    }

    [Test]
    public void SunTintBelowHorizonIsEmberNotBlue()
    {
        Color below = SkyPaletteRules.SunTint(-4f);
        Assert.Greater(below.r, below.g, "sub-horizon sun keeps red > green");
        Assert.Greater(below.g, below.b, "sub-horizon sun keeps green > blue");
    }

    [Test]
    public void MoonTintIsCool()
    {
        Color m = SkyPaletteRules.MoonTint();
        Assert.Greater(m.b, m.r, "moonlight is blue-dominant");
        Assert.Greater(m.b, m.g, "moonlight is bluer than green");
        Assert.LessOrEqual(m.r, 1f);
        Assert.LessOrEqual(m.b, 1f);
    }

    [Test]
    public void StarVisibilityNightExceedsDay()
    {
        float day = SkyPaletteRules.StarVisibility(12f, 0f);
        float night = SkyPaletteRules.StarVisibility(0f, 0f);
        Assert.AreEqual(0f, day, Eps, "no stars in daylight");
        Assert.Greater(night, 0.9f, "clear night sky is fully star-visible");
        Assert.Greater(night, day);
    }

    [Test]
    public void StarVisibilityRisesMonotonicallyThroughEvening()
    {
        float prev = -1f;
        for (float h = 17.5f; h <= 24f; h += 0.5f)
        {
            float v = SkyPaletteRules.StarVisibility(h, 0f);
            Assert.GreaterOrEqual(v, prev - Eps, "stars fade in monotonically after sunset");
            prev = v;
        }
    }

    [Test]
    public void StarVisibilityDecreasesWithCloudCover()
    {
        float prev = float.MaxValue;
        for (float c = 0f; c <= 1.001f; c += 0.25f)
        {
            float v = SkyPaletteRules.StarVisibility(0f, c);
            Assert.LessOrEqual(v, prev + Eps, "clouds can only hide stars");
            prev = v;
        }

        Assert.AreEqual(0f, SkyPaletteRules.StarVisibility(0f, 1f), Eps, "overcast sky has no stars");
    }

    [Test]
    public void StarVisibilitySymmetricMorningAndEvening()
    {
        // The solar proxy is symmetric around noon/midnight, so equal hours before
        // sunrise and after sunset must produce identical visibility.
        Assert.AreEqual(
            SkyPaletteRules.StarVisibility(3f, 0.2f),
            SkyPaletteRules.StarVisibility(21f, 0.2f), Eps);
    }

    [Test]
    public void SolarElevationProxyMatchesDayArc()
    {
        Assert.AreEqual(-1f, SkyPaletteRules.SolarElevationProxy(0f), Eps);
        Assert.AreEqual(0f, SkyPaletteRules.SolarElevationProxy(6f), Eps);
        Assert.AreEqual(1f, SkyPaletteRules.SolarElevationProxy(12f), Eps);
        Assert.AreEqual(0f, SkyPaletteRules.SolarElevationProxy(18f), Eps);
        Assert.AreEqual(-1f, SkyPaletteRules.SolarElevationProxy(24f), Eps);
    }

    [Test]
    public void AllOutputsClampedAndNanSafe()
    {
        foreach (float h in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -100f, 100f, -0f })
        {
            foreach (float x in new[] { float.NaN, float.PositiveInfinity, -1f, 2f })
            {
                Color z = SkyPaletteRules.ZenithColor(h, x);
                Color hz = SkyPaletteRules.HorizonColor(h, x, x);
                Color st = SkyPaletteRules.SunTint(h);
                Assert.IsFalse(float.IsNaN(z.r) || float.IsNaN(z.g) || float.IsNaN(z.b));
                Assert.IsFalse(float.IsNaN(hz.r) || float.IsNaN(hz.g) || float.IsNaN(hz.b));
                Assert.IsFalse(float.IsNaN(st.r) || float.IsNaN(st.g) || float.IsNaN(st.b));
                foreach (float ch in new[] { z.r, z.g, z.b, z.a, hz.r, hz.g, hz.b, hz.a, st.r, st.g, st.b, st.a })
                {
                    Assert.GreaterOrEqual(ch, 0f);
                    Assert.LessOrEqual(ch, 1f);
                }

                float sv = SkyPaletteRules.StarVisibility(h, x);
                Assert.GreaterOrEqual(sv, 0f);
                Assert.LessOrEqual(sv, 1f);
                Assert.IsFalse(float.IsNaN(sv));
            }
        }
    }

    [Test]
    public void DeterministicAcrossRepeatedCalls()
    {
        for (int i = 0; i < 64; i++)
        {
            float h = (i * 37) % 24;
            Color a = SkyPaletteRules.ZenithColor(h, 0.3f);
            Color b = SkyPaletteRules.ZenithColor(h, 0.3f);
            Assert.AreEqual(a.r, b.r);
            Assert.AreEqual(a.g, b.g);
            Assert.AreEqual(a.b, b.b);
        }
    }

    [Test]
    public void WeatherCloudProxyIsMonotonicAndBounded()
    {
        Assert.LessOrEqual(SkyPaletteRules.WeatherCloudProxy(WeatherState.Clear),
            SkyPaletteRules.WeatherCloudProxy(WeatherState.Overcast));
        Assert.LessOrEqual(SkyPaletteRules.WeatherCloudProxy(WeatherState.Overcast),
            SkyPaletteRules.WeatherCloudProxy(WeatherState.Rain));
        Assert.GreaterOrEqual(SkyPaletteRules.WeatherCloudProxy(WeatherState.Clear), 0f);
        Assert.LessOrEqual(SkyPaletteRules.WeatherCloudProxy(WeatherState.Thunderstorm), 1f);
    }

    [Test]
    public void SkyHashIsDeterministicAndWellSpread()
    {
        Assert.AreEqual(SkyHash.Fnv1a(1234u, 56u), SkyHash.Fnv1a(1234u, 56u));
        Assert.AreNotEqual(SkyHash.Fnv1a(1234u, 56u), SkyHash.Fnv1a(1235u, 56u));
        Assert.GreaterOrEqual(SkyHash.Hash01(7u, 9u), 0f);
        Assert.LessOrEqual(SkyHash.Hash01(7u, 9u), 1f);
    }
}
