using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Procedural
{
    /// <summary>
    /// Geographic and cultural theater used to steer prop, material, and lighting selection.
    /// </summary>
    public enum SemanticRegion { Unclassified, MediterraneanTown, EasternEuropeanIndustrial, DesertCheckpoint, SubarcticCompound, TemperateForestVillage }

    /// <summary>
    /// Loose historical period of the conflict, affecting silhouette language and prop wear.
    /// </summary>
    public enum ConflictEra { Contemporary, NearFuture, ColdWar, PostSovietTransitional, LegacyUnrest }

    /// <summary>
    /// Expected density of civilian life in the area, gating domestic props and furniture.
    /// </summary>
    public enum CivilianPresenceLevel { None, Sparse, Moderate, Dense }

    /// <summary>
    /// Who controls the area at mission start, driving fortification and signage selection.
    /// </summary>
    public enum FactionControlState { Abandoned, Neutral, Contested, FactionAlpha, FactionBravo }

    /// <summary>
    /// Dominant weather tendency, consumed by lighting and particle systems as a semantic key.
    /// </summary>
    public enum WeatherBias { Clear, Overcast, PersistentRain, Snowpack, DustHaze, GroundFog }

    /// <summary>
    /// Narrative context description for a procedurally generated map. A serializable plain class
    /// (ScriptableObject-style asset data) exposing semantic keys for prop/lighting selection and
    /// deterministic seed derivation so two sessions with the same context produce identical layouts.
    /// String hashing uses a process-stable FNV-1a implementation instead of System.String.GetHashCode,
    /// guaranteeing reproducibility across runs and platforms.
    /// </summary>
    [System.Serializable]
    public sealed class EnvironmentContextProfile
    {
        /// <summary>
        /// Stable identifier for save-file reference (e.g. "context.medtown_op42").
        /// </summary>
        public string profileId = "context.unnamed";

        /// <summary>
        /// Human-readable theater name shown in briefings.
        /// </summary>
        public string displayName = "Unnamed Theater";

        /// <summary>
        /// Geographic/cultural region of the map.
        /// </summary>
        public SemanticRegion region = SemanticRegion.Unclassified;

        /// <summary>
        /// Historical period bucket of the conflict.
        /// </summary>
        public ConflictEra era = ConflictEra.Contemporary;

        /// <summary>
        /// Conflict intensity from 0 (peacetime) to 1 (hot zone); scales debris, fortification, and wear.
        /// </summary>
        public float conflictIntensity = 0.5f;

        /// <summary>
        /// Expected civilian presence, gating domestic props.
        /// </summary>
        public CivilianPresenceLevel civilianPresence = CivilianPresenceLevel.Moderate;

        /// <summary>
        /// Faction control state at mission start.
        /// </summary>
        public FactionControlState factionControl = FactionControlState.Contested;

        /// <summary>
        /// Dominant weather tendency.
        /// </summary>
        public WeatherBias weatherBias = WeatherBias.Clear;

        /// <summary>
        /// Weather strength from 0 to 1, consumed by particles and visibility modifiers.
        /// </summary>
        public float weatherIntensity = 0.3f;

        /// <summary>
        /// Semantic lighting mood key (e.g. "light.warm.sun_baked") for the lighting controller.
        /// </summary>
        public string lightingKey = "light.mood.natural_day";

        /// <summary>
        /// Explicit prop palette keys. Empty means fall back to the inferred regional palette.
        /// </summary>
        public string[] propPaletteKeys = Array.Empty<string>();

        /// <summary>
        /// Free-form narrative tags (e.g. "evacuation", "siege") added to semantic keys.
        /// </summary>
        public string[] narrativeTags = Array.Empty<string>();

        /// <summary>
        /// Clamped view of conflict intensity in [0,1].
        /// </summary>
        public float NormalizedConflictIntensity
        {
            get { return Mathf.Clamp01(conflictIntensity); }
        }

        /// <summary>
        /// Clamped view of weather intensity in [0,1].
        /// </summary>
        public float NormalizedWeatherIntensity
        {
            get { return Mathf.Clamp01(weatherIntensity); }
        }

        /// <summary>
        /// Indicates the area is an active militarized zone: contested control plus meaningful conflict.
        /// </summary>
        public bool IsMilitarized
        {
            get
            {
                return conflictIntensity >= 0.25f
                    && factionControl != FactionControlState.Abandoned
                    && factionControl != FactionControlState.Neutral;
            }
        }

        /// <summary>
        /// Clamps scalar ranges and replaces null/blank collections and keys.
        /// </summary>
        public void Normalize()
        {
            conflictIntensity = Mathf.Clamp01(conflictIntensity);
            weatherIntensity = Mathf.Clamp01(weatherIntensity);

            propPaletteKeys = SanitizeArray(propPaletteKeys);
            narrativeTags = SanitizeArray(narrativeTags);

            if (string.IsNullOrWhiteSpace(lightingKey))
            {
                lightingKey = "light.mood.natural_day";
            }

            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = "context.unnamed";
            }
        }

        /// <summary>
        /// Enumerates the semantic keys that define this context. Used for palette matching
        /// (PropScatterSystem) and for deterministic seed derivation (DeriveSeed).
        /// </summary>
        /// <returns>Stable enumeration of dotted lowercase keys.</returns>
        public IEnumerable<string> GetSemanticKeys()
        {
            yield return "region." + RegionToken(region);
            yield return "era." + era.ToString().ToLowerInvariant();
            yield return "conflict.band" + Quantize(conflictIntensity, 10).ToString("D2");
            yield return "civilian." + civilianPresence.ToString().ToLowerInvariant();
            yield return "control." + factionControl.ToString().ToLowerInvariant();
            yield return "weather." + weatherBias.ToString().ToLowerInvariant();
            yield return "weather-intensity." + Quantize(weatherIntensity, 10).ToString("D2");
            yield return "lighting." + lightingKey;

            if (propPaletteKeys != null)
            {
                foreach (var key in propPaletteKeys)
                {
                    if (!string.IsNullOrWhiteSpace(key)) yield return "palette." + key.Trim().ToLowerInvariant();
                }
            }

            if (narrativeTags != null)
            {
                foreach (var tag in narrativeTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag)) yield return "tag." + tag.Trim().ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Process-stable, cross-platform FNV-1a string hash. Used for seed derivation because
        /// System.String.GetHashCode is not contractually stable between runtimes.
        /// </summary>
        /// <param name="value">String to hash; null or empty yields 0.</param>
        /// <returns>32-bit stable hash.</returns>
        public static int StableStringHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 16777619) ^ value[i];
                }
                return hash;
            }
        }

        /// <summary>
        /// Derives a deterministic, strictly positive RNG seed by folding every semantic key
        /// of this context over the supplied base seed.
        /// </summary>
        /// <param name="baseSeed">Caller-controlled seed (e.g. mission seed).</param>
        /// <returns>Seed in [1, int.MaxValue-1], identical across runs for identical contexts.</returns>
        public int DeriveSeed(int baseSeed)
        {
            unchecked
            {
                uint combined = (uint)baseSeed;
                foreach (var key in GetSemanticKeys())
                {
                    combined ^= (uint)StableStringHash(key);
                    combined *= 16777619u;
                    combined ^= combined >> 7;
                }
                if (combined == 0) combined = 0x9E3779B9u;
                return (int)((combined & 0x7FFFFFFF) | 1u);
            }
        }

        /// <summary>
        /// Returns the configured prop palette, or the regional default palette when none is set.
        /// </summary>
        /// <returns>Non-null array of prop palette keys.</returns>
        public string[] GetEffectivePropPalette()
        {
            if (propPaletteKeys != null && propPaletteKeys.Length > 0)
            {
                return SanitizeArray(propPaletteKeys);
            }
            return InferredPaletteForRegion(region);
        }

        /// <summary>
        /// Maps a region to its lowercase palette/matching token.
        /// </summary>
        /// <param name="value">Region to tokenize.</param>
        /// <returns>Lowercase camel-case-flattened token (e.g. "mediterraneantown").</returns>
        public static string RegionToken(SemanticRegion value)
        {
            return value.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Returns the default prop palette keys for a region, resolved through the biome table.
        /// </summary>
        /// <param name="value">Region to infer for.</param>
        /// <returns>Non-null palette array; generic keys when the region is unclassified.</returns>
        public static string[] InferredPaletteForRegion(SemanticRegion value)
        {
            if (BiomeTable.TryGetForRegion(value, out var biome))
            {
                return (string[])biome.propPaletteKeys.Clone();
            }

            return new string[]
            {
                "prop.crate.wood",
                "prop.barrel.rusty",
                "prop.debris.rubble",
                "prop.sandbag.emplacement",
                "prop.foliage.bush"
            };
        }

        /// <summary>
        /// Builds a coherent default context for a region with region-appropriate weather bias
        /// and palette, sharing defaults with the biome table.
        /// </summary>
        /// <param name="value">Target region.</param>
        /// <returns>Filled, normalized context profile.</returns>
        public static EnvironmentContextProfile FromRegion(SemanticRegion value)
        {
            var profile = new EnvironmentContextProfile
            {
                profileId = "context." + RegionToken(value),
                displayName = value.ToString(),
                region = value
            };

            if (BiomeTable.TryGetForRegion(value, out var biome))
            {
                profile.propPaletteKeys = (string[])biome.propPaletteKeys.Clone();
                profile.lightingKey = biome.lightingKey;
                profile.conflictIntensity = biome.conflictIntensityBias;
            }

            switch (value)
            {
                case SemanticRegion.DesertCheckpoint:
                    profile.weatherBias = WeatherBias.DustHaze;
                    break;
                case SemanticRegion.SubarcticCompound:
                    profile.weatherBias = WeatherBias.Snowpack;
                    break;
                case SemanticRegion.EasternEuropeanIndustrial:
                    profile.weatherBias = WeatherBias.Overcast;
                    break;
                default:
                    profile.weatherBias = WeatherBias.Clear;
                    break;
            }

            profile.Normalize();
            return profile;
        }

        /// <summary>
        /// Collects non-fatal data-quality warnings for authoring-time tooling.
        /// </summary>
        /// <returns>List of human-readable warnings; empty when the profile is clean.</returns>
        public List<string> CollectWarnings()
        {
            var warnings = new List<string>();

            if (region == SemanticRegion.Unclassified)
            {
                warnings.Add("Region is Unclassified; procedural selection will use fallback heuristics.");
            }
            if (conflictIntensity < 0f || conflictIntensity > 1f)
            {
                warnings.Add("conflictIntensity outside [0,1]; call Normalize() before generation.");
            }
            if (weatherIntensity < 0f || weatherIntensity > 1f)
            {
                warnings.Add("weatherIntensity outside [0,1]; call Normalize() before generation.");
            }
            if (civilianPresence != CivilianPresenceLevel.None && factionControl == FactionControlState.Abandoned)
            {
                warnings.Add("Civilian presence declared for an abandoned area; verify narrative intent.");
            }

            return warnings;
        }

        private static string[] SanitizeArray(string[] input)
        {
            if (input == null) return Array.Empty<string>();

            var output = new List<string>(input.Length);
            foreach (var entry in input)
            {
                if (!string.IsNullOrWhiteSpace(entry)) output.Add(entry.Trim());
            }
            return output.ToArray();
        }

        private static int Quantize(float value, int bins)
        {
            float clamped = Mathf.Clamp01(value);
            return Mathf.Clamp(Mathf.RoundToInt(clamped * bins), 0, bins);
        }
    }
}
