using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Deterministic FNV-1a based hashing helpers for procedural sky variation seeds.
    /// Pure integer math: identical inputs produce identical outputs on every platform
    /// and every run, and <see cref="System.Random"/> is never used anywhere in the sky
    /// stack. Hash outputs are only ever consumed as variation seeds or normalized
    /// 0..1 weights, never as timing or gameplay values.
    /// </summary>
    public static class SkyHash
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// Computes a 32-bit FNV-1a style avalanche of (seed, index). Deterministic and
        /// allocation free.
        /// </summary>
        /// <param name="seed">Variation seed (any 32-bit value).</param>
        /// <param name="index">Lattice index (e.g. star id, sub-channel id).</param>
        /// <returns>A well-distributed 32-bit hash.</returns>
        public static uint Fnv1a(uint seed, uint index)
        {
            uint hash = FnvOffsetBasis ^ seed;
            hash = (hash ^ index) * FnvPrime;
            hash ^= hash >> 13;
            hash *= FnvPrime;
            hash ^= hash >> 16;
            return hash;
        }

        /// <summary>
        /// Normalized hash in [0, 1] for procedural weights (star positions, brightness).
        /// </summary>
        /// <param name="seed">Variation seed.</param>
        /// <param name="index">Lattice index.</param>
        /// <returns>Deterministic weight in the 0..1 range.</returns>
        public static float Hash01(uint seed, uint index)
        {
            return Fnv1a(seed, index) / 4294967295f;
        }
    }

    /// <summary>
    /// Pure deterministic sky color rules for the procedural sky stack. Physically
    /// plausible smooth curves only: deep blue zenith at night, pale blue at noon,
    /// warm ember horizon in a band around sunrise/sunset, sandy desaturation on
    /// dusty horizons. Every output is clamped to 0..1 and NaN-safe; monotonicity
    /// guarantees are documented per member. No state, no randomness, no editor
    /// dependencies: safe to call from play mode, edit mode and tests.
    /// </summary>
    public static class SkyPaletteRules
    {
        private static readonly Color NightZenith = new Color(0.020f, 0.034f, 0.078f);
        private static readonly Color DawnZenith = new Color(0.088f, 0.108f, 0.200f);
        private static readonly Color NoonZenith = new Color(0.320f, 0.490f, 0.760f);
        private static readonly Color NightHorizon = new Color(0.014f, 0.022f, 0.046f);
        private static readonly Color PaleDayHorizon = new Color(0.640f, 0.720f, 0.840f);
        private static readonly Color EmberHorizon = new Color(0.980f, 0.520f, 0.220f);
        private static readonly Color NightDust = new Color(0.060f, 0.052f, 0.044f);
        private static readonly Color DayDust = new Color(0.760f, 0.640f, 0.470f);
        private static readonly Color SunEmber = new Color(0.480f, 0.130f, 0.050f);
        private static readonly Color SunOrange = new Color(1.000f, 0.460f, 0.160f);
        private static readonly Color SunSoft = new Color(1.000f, 0.840f, 0.590f);
        private static readonly Color SunWhite = new Color(1.000f, 0.990f, 0.960f);

        /// <summary>
        /// Signed solar elevation proxy from the hour of day: 0 at 06:00 and 18:00,
        /// +1 at 12:00, -1 at 00:00. Monotonically increasing on [6, 12] and
        /// decreasing on [12, 18]. Input is wrapped into [0, 24); NaN yields 0.
        /// </summary>
        /// <param name="hour">Hour of day, any float (wrapped modulo 24).</param>
        /// <returns>Signed proxy in [-1, 1].</returns>
        public static float SolarElevationProxy(float hour)
        {
            if (IsBad(hour)) return 0f;
            float h = WrapHour(hour);
            return Mathf.Sin(((h - 6f) / 12f) * Mathf.PI);
        }

        /// <summary>
        /// Zenith sky color for the given hour and relative humidity. Monotonic:
        /// zenith luminance is non-decreasing in hour on [5, 12] and non-increasing
        /// on [12, 19]; higher humidity lifts luminance toward pale haze (never
        /// saturating) in proportion to daylight. Channels are clamped to 0..1.
        /// </summary>
        /// <param name="hour">Hour of day (wrapped modulo 24).</param>
        /// <param name="humidity">Relative humidity 0..1 (clamped).</param>
        /// <returns>Deterministic zenith color.</returns>
        public static Color ZenithColor(float hour, float humidity)
        {
            humidity = Sanitize01(humidity, 0.5f);
            float day = Mathf.Clamp01(SolarElevationProxy(hour));
            Color c = Color.Lerp(NightZenith, DawnZenith, Mathf.Clamp01(day / 0.30f));
            c = Color.Lerp(c, NoonZenith, Mathf.Pow(day, 1.4f));
            float luma = Luma(c);
            Color haze = new Color(luma * 1.55f + 0.06f, luma * 1.60f + 0.07f, luma * 1.62f + 0.08f);
            c = Color.Lerp(c, haze, humidity * 0.45f * (0.35f + 0.65f * day));
            return ClampColor(c);
        }

        /// <summary>
        /// Horizon sky color for the given hour, humidity and biome dust load.
        /// A warm ember band surrounds sun crossing (hour near 6 and 18) with
        /// red > blue; at noon the horizon is pale and cool (blue > red); at night
        /// it is near-black. Dust monotonically desaturates the horizon toward
        /// sandy tan scaled by daylight, so night stays dark. Channels clamped 0..1.
        /// </summary>
        /// <param name="hour">Hour of day (wrapped modulo 24).</param>
        /// <param name="humidity">Relative humidity 0..1 (clamped).</param>
        /// <param name="biomeDust01">Dust load 0..1 (clamped); 0 = clear air.</param>
        /// <returns>Deterministic horizon color.</returns>
        public static Color HorizonColor(float hour, float humidity, float biomeDust01)
        {
            humidity = Sanitize01(humidity, 0.5f);
            float dust = Sanitize01(biomeDust01, 0f);
            float p = SolarElevationProxy(hour);
            float day = Mathf.Clamp01(p);
            Color c = Color.Lerp(NightHorizon, PaleDayHorizon, Mathf.Pow(day, 0.85f));
            float warmBump = 1f - Mathf.Clamp01(Mathf.Abs(p) / 0.38f);
            if (warmBump > 0f)
            {
                float warmStrength = warmBump * (0.45f + 0.55f * Mathf.Clamp01((p + 0.38f) / 0.76f));
                c = Color.Lerp(c, EmberHorizon, warmStrength * 0.85f);
            }

            float luma = Luma(c);
            c = Color.Lerp(c, new Color(luma, luma, luma), humidity * 0.18f);
            Color tan = Color.Lerp(NightDust, DayDust, Mathf.Pow(day, 0.8f));
            c = Color.Lerp(c, tan, dust * 0.65f);
            return ClampColor(c);
        }

        /// <summary>
        /// Sun disc tint by solar elevation. Warm ember below the horizon blending
        /// through orange and soft yellow to near-white above ~30 degrees; the blue
        /// channel is non-decreasing in elevation on [-6, 60] and red > blue holds
        /// for low elevations (warm low sun). Clamped 0..1, NaN-safe.
        /// </summary>
        /// <param name="elevationDeg">Solar elevation in degrees.</param>
        /// <returns>Deterministic sun tint.</returns>
        public static Color SunTint(float elevationDeg)
        {
            if (IsBad(elevationDeg)) return ClampColor(SunWhite);
            float e = Mathf.Clamp(elevationDeg, -6f, 60f);
            if (e < 0f) return ClampColor(Color.Lerp(SunEmber, SunOrange, (e + 6f) / 6f));
            if (e < 10f) return ClampColor(Color.Lerp(SunOrange, SunSoft, e / 10f));
            return ClampColor(Color.Lerp(SunSoft, SunWhite, (e - 10f) / 50f));
        }

        /// <summary>
        /// Constant cool moonlight tint (blue >= red, blue >= green) for the moon
        /// billboard and moon-driven ambient hints.
        /// </summary>
        /// <returns>Deterministic cool tint, all channels in 0..1.</returns>
        public static Color MoonTint()
        {
            return new Color(0.820f, 0.878f, 0.970f);
        }

        /// <summary>
        /// Star visibility for the given hour and cloud cover. Zero during daylight,
        /// rising monotonically through twilight (non-decreasing in hour on
        /// [18, 24] and on [0, 5]), and monotonically non-increasing in cloud cover;
        /// fully overcast sky yields zero. Result clamped 0..1, NaN-safe.
        /// </summary>
        /// <param name="hour">Hour of day (wrapped modulo 24).</param>
        /// <param name="cloudCover01">Cloud cover 0..1 (clamped).</param>
        /// <returns>Star visibility weight 0..1.</returns>
        public static float StarVisibility(float hour, float cloudCover01)
        {
            if (IsBad(hour)) return 0f;
            float cloud = Sanitize01(cloudCover01, 0f);
            float p = SolarElevationProxy(hour);
            float vis = Mathf.Clamp01((-p - 0.05f) / 0.30f);
            vis *= Mathf.Pow(1f - cloud, 1.5f);
            return Mathf.Clamp01(vis);
        }

        /// <summary>
        /// Cloud-cover proxy for a weather state, used when no numeric cloud data is
        /// available. Deterministic and monotonic in occlusion severity (Clear is the
        /// minimum, Fog/Thunderstorm the maximum).
        /// </summary>
        /// <param name="weather">Current weather state.</param>
        /// <returns>Cloud cover estimate 0..1.</returns>
        public static float WeatherCloudProxy(VEVE.WeatherState weather)
        {
            switch (weather)
            {
                case VEVE.WeatherState.Clear: return 0.05f;
                case VEVE.WeatherState.Overcast: return 0.75f;
                case VEVE.WeatherState.Rain: return 0.90f;
                case VEVE.WeatherState.Fog: return 0.95f;
                case VEVE.WeatherState.Snow: return 0.80f;
                case VEVE.WeatherState.Thunderstorm: return 0.95f;
                default: return 0.50f;
            }
        }

        private static float WrapHour(float hour)
        {
            float h = hour % 24f;
            if (h < 0f) h += 24f;
            return h;
        }

        private static bool IsBad(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v);
        }

        private static float Sanitize01(float v, float fallback)
        {
            if (IsBad(v)) return fallback;
            return Mathf.Clamp01(v);
        }

        private static float Luma(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        private static Color ClampColor(Color c)
        {
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
        }
    }
}
