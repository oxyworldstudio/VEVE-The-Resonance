using UnityEngine;
using VEVE.Content;

namespace VEVE.Graphics
{
    /// <summary>
    /// Bridges the deterministic <see cref="SkyPaletteRules"/> palette into the built-in
    /// render settings: fog color, fog density and ambient light. Honors the biome fog
    /// bias from <see cref="BiomeSceneProfile"/> and stays within the atmosphere limits of
    /// the environment object. The <c>VEVE.EnvironmentSimulation</c> (for hour/humidity/
    /// weather) and the biome provider are looked up with FindFirstObjectByType on a 1 s
    /// cache. Everything is null-safe: with no sky, no simulation and no biome source the
    /// bridge holds a neutral noon baseline and simply does not fight other systems more
    /// than once per refresh.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AtmosphereTintBridge : MonoBehaviour
    {
        /// <summary>Seconds between cached FindFirstObjectByType lookups (1 s).</summary>
        public const float LookupIntervalSeconds = 1f;

        /// <summary>Neutral baseline hour used when no EnvironmentSimulation exists.</summary>
        public const float NeutralHour = 12f;

        /// <summary>Neutral baseline humidity used when no EnvironmentSimulation exists.</summary>
        public const float NeutralHumidity = 0.5f;

        /// <summary>Neutral baseline fog density applied by <see cref="ForceRefresh"/>.</summary>
        public const float NeutralFogDensity = 0.004f;

        private EnvironmentSimulation sim;
        private BiomeSceneProfile? biome;
        private bool biomeLookupDone;
        private float lookupAt = float.NegativeInfinity;
        private float hour = NeutralHour;
        private float humidity = NeutralHumidity;
        private float dust01;

        /// <summary>
        /// Gets or sets the deterministic biome fog bias in [0, 1] (from
        /// <see cref="BiomeSceneProfile.fogDensityBias"/>). When unset and no biome source
        /// is found in the scene the bridge falls back to the town bias (0.18).
        /// </summary>
        public float? BiomeFogBiasOverride { get; set; }

        /// <summary>Gets the last refreshed hour of day used for palette evaluation.</summary>
        public float Hour => hour;

        /// <summary>Gets the last refreshed humidity used for palette evaluation.</summary>
        public float Humidity => humidity;

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the bridge once: cached lookups (1 s), palette evaluation, and writes
        /// to RenderSettings fog/ambient within the biome fog bias.
        /// </summary>
        /// <param name="deltaTime">Frame delta (accepted for symmetry; timers use realtime).</param>
        public void Tick(float deltaTime)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lookupAt >= LookupIntervalSeconds)
            {
                sim = FindFirstObjectByType<EnvironmentSimulation>();
                if (!biomeLookupDone)
                {
                    BiomeSceneProfile profile;
                    if (TryFindBiome(out profile))
                    {
                        biome = profile;
                        dust01 = Mathf.Clamp01(profile.fogDensityBias);
                    }
                    else
                    {
                        biome = null;
                        dust01 = 0f;
                    }

                    biomeLookupDone = true;
                }

                lookupAt = now;
            }

            ReadInputs();
            ApplyAtmosphere();
        }

        /// <summary>
        /// Forces an immediate lookup + application pass. Safe in EditMode tests and
        /// never throws even with an empty scene.
        /// </summary>
        public void ForceRefresh()
        {
            lookupAt = float.NegativeInfinity;
            biomeLookupDone = false;
            Tick(0f);
        }

        /// <summary>
        /// Evaluates the pure palette for the current cached inputs without touching
        /// RenderSettings. Exposed for tests and overlays.
        /// </summary>
        /// <param name="fogColor">Receives the biome-biased fog/horizon color.</param>
        /// <param name="ambientColor">Receives the ambient light color.</param>
        /// <param name="fogDensity">Receives the biome-biased fog density.</param>
        public void EvaluatePalette(out Color fogColor, out Color ambientColor, out float fogDensity)
        {
            Color horizon = SkyPaletteRules.HorizonColor(hour, humidity, dust01);
            Color zenith = SkyPaletteRules.ZenithColor(hour, humidity);
            float bias = ResolveFogBias();
            fogColor = Color.Lerp(horizon, zenith, Mathf.Clamp01(bias) * 0.35f);
            fogDensity = Mathf.Lerp(0.0012f, 0.055f, Mathf.Clamp01(bias));
            float night = Mathf.Clamp01(1f - SkyPaletteRules.SolarElevationProxy(hour));
            ambientColor = Color.Lerp(zenith * 0.9f, horizon * 0.65f, 0.35f * night);
        }

        private void ReadInputs()
        {
            if (sim != null)
            {
                hour = sim.CurrentHour;
                if (float.IsNaN(hour) || float.IsInfinity(hour)) hour = NeutralHour;
                hour = Mathf.Repeat(hour, 24f);
                humidity = sim.Humidity;
                if (float.IsNaN(humidity) || float.IsInfinity(humidity)) humidity = NeutralHumidity;
                humidity = Mathf.Clamp01(humidity);
            }
            else
            {
                hour = NeutralHour;
                humidity = NeutralHumidity;
            }

            if (BiomeFogBiasOverride.HasValue)
            {
                dust01 = Mathf.Clamp01(BiomeFogBiasOverride.Value);
            }
            else if (biome.HasValue)
            {
                dust01 = Mathf.Clamp01(biome.Value.fogDensityBias);
            }
        }

        private void ApplyAtmosphere()
        {
            Color fogColor;
            Color ambient;
            float fogDensity;
            EvaluatePalette(out fogColor, out ambient, out fogDensity);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(
                Mathf.Clamp01(fogColor.r), Mathf.Clamp01(fogColor.g), Mathf.Clamp01(fogColor.b), 1f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = Mathf.Clamp(fogDensity, 0f, 0.2f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(
                Mathf.Clamp01(ambient.r), Mathf.Clamp01(ambient.g), Mathf.Clamp01(ambient.b), 1f);
        }

        private float ResolveFogBias()
        {
            if (BiomeFogBiasOverride.HasValue) return Mathf.Clamp01(BiomeFogBiasOverride.Value);
            if (biome.HasValue) return Mathf.Clamp01(biome.Value.fogDensityBias);
            return 0.18f;
        }

        private static bool TryFindBiome(out BiomeSceneProfile profile)
        {
            var holder = FindFirstObjectByType<BiomeProfileHolder>();
            if (holder != null)
            {
                profile = holder.Profile;
                return true;
            }

            profile = default;
            return false;
        }
    }
}
