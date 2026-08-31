using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Procedural art-pass rules (no binary assets required): physical palette per
    /// authored surface kind, weather/wind-time material response (wet gloss boost,
    /// dust/albedo shifts) and sun warmth curve. Kept pure statics so every value
    /// is testable and reusable by shaders and the scene author passes.
    /// </summary>
    public static class SurfaceArtRules
    {
        public struct Palette { public Color baseColor; public float gloss; }

        private static readonly Dictionary<string, Palette> PaletteTable = BuildPalette();

        private static Dictionary<string, Palette> BuildPalette()
        {
            return new Dictionary<string, Palette>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Concrete", new Palette { baseColor = new Color(0.500f, 0.500f, 0.55f), gloss = 0.25f } },
                { "Wood", new Palette { baseColor = new Color(0.450f, 0.320f, 0.200f), gloss = 0.45f } },
                { "Metal", new Palette { baseColor = new Color(0.620f, 0.640f, 0.680f), gloss = 0.22f } },
                { "Fabric", new Palette { baseColor = new Color(0.120f, 0.160f, 0.110f), gloss = 0.08f } },
                { "Asphalt", new Palette { baseColor = new Color(0.255f, 0.26f, 0.28f), gloss = 0.18f } },
                { "Sand", new Palette { baseColor = new Color(0.765f, 0.68f, 0.52f), gloss = 0.05f } },
                { "Foliage", new Palette { baseColor = new Color(0.185f, 0.31f, 0.165f), gloss = 0.32f } },
                { "Glass", new Palette { baseColor = new Color(0.85f, 0.9f, 0.95f), gloss = 0.92f } },
            };
        }

        public static bool TryPalette(string surfaceKind, out Palette palette)
        {
            palette = default;
            return !string.IsNullOrEmpty(surfaceKind) && PaletteTable.TryGetValue(surfaceKind, out palette);
        }

        /// <summary>Wetness raises specular gloss (rain on concrete/asphalt/glass), dust lowers it.</summary>
        public static float GlossAfterWeather(float gloss01, float wetness01)
        {
            float g = Mathf.Clamp01(gloss01) + (1f - Mathf.Clamp01(gloss01)) * 0.62f * Mathf.Clamp01(wetness01);
            return Mathf.Clamp01(g);
        }

        public static Color TintAfterWeather(Color baseColor, float wetness01, float dustLoad01)
        {
            Color c = baseColor * (1f - 0.26f * Mathf.Clamp01(wetness01));
            float dust = Mathf.Clamp01(dustLoad01) * 0.22f;
            c = Color.Lerp(c, new Color(0.72f, 0.64f, 0.52f), dust);
            return c;
        }

        /// <summary>Sun warmth curve peaks with low golden angles; neutral on blue noon.</summary>
        public static float SunWarmth(float sunElevationDeg)
        {
            float e = Mathf.Clamp(sunElevationDeg, 0f, 90f);
            return Mathf.InverseLerp(70f, 0f, e);
        }

        /// <summary>Palette key nearest to a material name (tolerant authoring bridge).</summary>
        public static string ResolveKey(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return "Concrete";
            foreach (var k in PaletteTable.Keys)
                if (materialName.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return k;
            string lower = materialName.ToLowerInvariant();
            if (lower.Contains("steel")) return "Metal";
            if (lower.Contains("cloth") || lower.Contains("uniform") || lower.Contains("gear")) return "Fabric";
            return "Concrete";
        }
    }

    /// <summary>
    /// Optional runtime refresher: re-applies the weathered palette to named
    /// renderers as the environment changes. Pure-data authoring stays in the
    /// scene builder; this driver only mutates existing material properties.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceStyleDriver : MonoBehaviour
    {
        [SerializeField] private Renderer[] targets;
        [SerializeField] private float refreshSeconds = 1.5f;

        private float t;

        private void Start()
        {
            if (targets == null || targets.Length == 0)
                targets = GetComponentsInChildren<Renderer>();
            t = 0f;
            ApplyOnce();
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t >= refreshSeconds) { t = 0f; ApplyOnce(); }
        }

        public void ApplyOnce()
        {
            PipelineUtils.EnvState weather = PipelineUtils.CurrentEnv();
            if (targets == null) return;
            foreach (Renderer r in targets)
            {
                if (r == null || r.sharedMaterial == null) continue;
                string key = SurfaceArtRules.ResolveKey(r.sharedMaterial.name);
                if (!SurfaceArtRules.TryPalette(key, out var p)) continue;
                Material m = r.sharedMaterial;
                m.color = SurfaceArtRules.TintAfterWeather(p.baseColor, weather.wetness01, weather.dust01);
                float g = SurfaceArtRules.GlossAfterWeather(p.gloss, weather.wetness01);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", g);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", g);
            }
        }
    }

    /// <summary>Thin environment bridge so the driver keeps no hard dependency and stays null-safe.</summary>
    internal static class PipelineUtils
    {
        public struct EnvState { public float wetness01; public float dust01; }

        public static EnvState CurrentEnv()
        {
            var state = new EnvState();
            var sim = UnityEngine.Object.FindFirstObjectByType<EnvironmentSimulation>();
            if (sim == null) return state;
            float rain = sim.Humidity >= 0.85f ? Mathf.InverseLerp(0.85f, 1f, sim.Humidity) : 0f;
            float sun = sim.SunElevation;
            state.wetness01 = rain;
            state.dust01 = (1f - rain) * Mathf.Clamp01((sun - 20f) / 70f) * 0.45f;
            return state;
        }
    }
}
