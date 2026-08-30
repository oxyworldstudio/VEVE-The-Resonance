using System;
using System.Collections.Generic;

namespace VEVE.Procedural
{
    /// <summary>
    /// Supported procedural biomes. Each biome is a pure data row in the BiomeTable.
    /// </summary>
    public enum BiomeId { MediterraneanTown, EasternEuropeanIndustrial, DesertCheckpoint, SubarcticCompound, TemperateForestVillage }

    /// <summary>
    /// Building silhouette and fabric parameters that condition the generator for a biome.
    /// </summary>
    [System.Serializable]
    public struct BuildingStyleParams
    {
        /// <summary>
        /// Typical number of above-ground storeys for regional buildings.
        /// </summary>
        public int typicalStoreys;

        /// <summary>
        /// Nominal roof pitch in degrees (near zero for flat-roof regions).
        /// </summary>
        public float roofPitchDegrees;

        /// <summary>
        /// Facade window density from 0 (fortified) to 1 (glazed).
        /// </summary>
        public float windowDensity;

        /// <summary>
        /// Frequency of interior courtyards and setbacks from 0 to 1.
        /// </summary>
        public float courtyardFrequency;

        /// <summary>
        /// Facade variation between neighboring structures from 0 (monolithic) to 1 (eclectic).
        /// </summary>
        public float facadeVariation;

        /// <summary>
        /// Whether usable flat roofs (rooftop traversal) are regionally common.
        /// </summary>
        public bool flatRoofsCommon;
    }

    /// <summary>
    /// Complete biome data row: material mix, prop palette keys, ground texture keys,
    /// vegetation density, building style, and lighting bias. Pure serializable struct.
    /// </summary>
    [System.Serializable]
    public struct BiomeProfile
    {
        /// <summary>
        /// Biome identity.
        /// </summary>
        public BiomeId id;

        /// <summary>
        /// Lowercase region token shared with EnvironmentContextProfile semantic keys.
        /// </summary>
        public string regionToken;

        /// <summary>
        /// Semantic material keys (mat.*) for surface selection and wear layering.
        /// </summary>
        public string[] materialMixKeys;

        /// <summary>
        /// Semantic prop palette keys (prop.*) consumed by PropScatterSystem.
        /// </summary>
        public string[] propPaletteKeys;

        /// <summary>
        /// Semantic ground/decal texture keys (tex.*) for terrain painting.
        /// </summary>
        public string[] groundTextureKeys;

        /// <summary>
        /// Ambient sound bank key (sfx.ambient.*) for the audio layer.
        /// </summary>
        public string ambientSoundKey;

        /// <summary>
        /// Default lighting mood key (light.*) for the lighting controller.
        /// </summary>
        public string lightingKey;

        /// <summary>
        /// Foliage coverage bias from 0 to 1 used by vegetation scatter.
        /// </summary>
        public float vegetationDensity;

        /// <summary>
        /// Typical conflict intensity for scenarios sited in this biome.
        /// </summary>
        public float conflictIntensityBias;

        /// <summary>
        /// Regional building fabric parameters.
        /// </summary>
        public BuildingStyleParams buildingStyle;
    }

    /// <summary>
    /// Read-only biome data table for the currently authored set of five theaters.
    /// </summary>
    public static class BiomeTable
    {
        private static readonly BiomeProfile[] Profiles = BuildProfiles();

        /// <summary>
        /// All biome rows in declaration order.
        /// </summary>
        public static IReadOnlyList<BiomeProfile> All
        {
            get { return Profiles; }
        }

        /// <summary>
        /// Looks up a biome by identifier.
        /// </summary>
        /// <param name="id">Biome to fetch.</param>
        /// <returns>The matching biome row.</returns>
        public static BiomeProfile Get(BiomeId id)
        {
            foreach (var profile in Profiles)
            {
                if (profile.id == id) return profile;
            }
            return Profiles[0];
        }

        /// <summary>
        /// Maps a narrative semantic region onto its biome row.
        /// </summary>
        /// <param name="region">Region declared by an EnvironmentContextProfile.</param>
        /// <param name="profile">Resolved biome when found.</param>
        /// <returns>False for Unclassified regions with no dedicated biome.</returns>
        public static bool TryGetForRegion(SemanticRegion region, out BiomeProfile profile)
        {
            switch (region)
            {
                case SemanticRegion.MediterraneanTown:
                    profile = Get(BiomeId.MediterraneanTown);
                    return true;
                case SemanticRegion.EasternEuropeanIndustrial:
                    profile = Get(BiomeId.EasternEuropeanIndustrial);
                    return true;
                case SemanticRegion.DesertCheckpoint:
                    profile = Get(BiomeId.DesertCheckpoint);
                    return true;
                case SemanticRegion.SubarcticCompound:
                    profile = Get(BiomeId.SubarcticCompound);
                    return true;
                case SemanticRegion.TemperateForestVillage:
                    profile = Get(BiomeId.TemperateForestVillage);
                    return true;
                default:
                    profile = default;
                    return false;
            }
        }

        /// <summary>
        /// Deterministically samples a biome row from a seed, letting context weighting bias the pick.
        /// </summary>
        /// <param name="seed">Strictly positive seed (e.g. from DeriveSeed).</param>
        /// <returns>Selected biome row.</returns>
        public static BiomeProfile Sample(int seed)
        {
            unchecked
            {
                int index = (int)(((uint)seed * 2654435761u) >> 27) % Profiles.Length;
                return Profiles[index < 0 ? index + Profiles.Length : index];
            }
        }

        private static BiomeProfile[] BuildProfiles()
        {
            return new[]
            {
                new BiomeProfile
                {
                    id = BiomeId.MediterraneanTown,
                    regionToken = "mediterraneantown",
                    materialMixKeys = new[] { "mat.stone.limestone", "mat.plaster.ochre", "mat.tile.terracotta", "mat.wood.olive", "mat.asphalt.worn" },
                    propPaletteKeys = new[] { "prop.crate.wood", "prop.barrel.olive", "prop.foliage.cypress", "prop.foliage.bush", "prop.vehicle.sedan", "prop.furniture.pallet", "prop.sandbag.emplacement", "prop.debris.rubble" },
                    groundTextureKeys = new[] { "tex.cobblestone", "tex.plaster.warm", "tex.dirt.warm", "tex.asphalt.cracked" },
                    ambientSoundKey = "sfx.ambient.town_warm",
                    lightingKey = "light.warm.sun_baked",
                    vegetationDensity = 0.55f,
                    conflictIntensityBias = 0.45f,
                    buildingStyle = new BuildingStyleParams
                    {
                        typicalStoreys = 2,
                        roofPitchDegrees = 18f,
                        windowDensity = 0.6f,
                        courtyardFrequency = 0.5f,
                        facadeVariation = 0.7f,
                        flatRoofsCommon = false
                    }
                },
                new BiomeProfile
                {
                    id = BiomeId.EasternEuropeanIndustrial,
                    regionToken = "easterneuropeanindustrial",
                    materialMixKeys = new[] { "mat.concrete.panel", "mat.steel.rusted", "mat.brick.red", "mat.glass.factory", "mat.gravel.industrial" },
                    propPaletteKeys = new[] { "prop.crate.ammo", "prop.barrel.rusty", "prop.vehicle.truck", "prop.debris.rubble", "prop.sandbag.emplacement", "prop.foliage.bush" },
                    groundTextureKeys = new[] { "tex.concrete.stained", "tex.gravel.industrial", "tex.asphalt.oiled", "tex.rust.metal" },
                    ambientSoundKey = "sfx.ambient.industrial_hum",
                    lightingKey = "light.cold.overcast_industrial",
                    vegetationDensity = 0.2f,
                    conflictIntensityBias = 0.6f,
                    buildingStyle = new BuildingStyleParams
                    {
                        typicalStoreys = 3,
                        roofPitchDegrees = 6f,
                        windowDensity = 0.45f,
                        courtyardFrequency = 0.2f,
                        facadeVariation = 0.25f,
                        flatRoofsCommon = true
                    }
                },
                new BiomeProfile
                {
                    id = BiomeId.DesertCheckpoint,
                    regionToken = "desertcheckpoint",
                    materialMixKeys = new[] { "mat.concrete.sandbuff", "mat.hesco.filled", "mat.canvas.tan", "mat.corrugated.iron", "mat.sand.compacted" },
                    propPaletteKeys = new[] { "prop.sandbag.emplacement", "prop.barrel.rusty", "prop.crate.ammo", "prop.vehicle.tech", "prop.vehicle.sedan", "prop.debris.rubble" },
                    groundTextureKeys = new[] { "tex.sand.dune", "tex.dirt.compacted", "tex.concrete.dusty", "tex.gravel.limestone" },
                    ambientSoundKey = "sfx.ambient.desert_wind",
                    lightingKey = "light.hot.glare",
                    vegetationDensity = 0.05f,
                    conflictIntensityBias = 0.7f,
                    buildingStyle = new BuildingStyleParams
                    {
                        typicalStoreys = 1,
                        roofPitchDegrees = 2f,
                        windowDensity = 0.3f,
                        courtyardFrequency = 0.6f,
                        facadeVariation = 0.4f,
                        flatRoofsCommon = true
                    }
                },
                new BiomeProfile
                {
                    id = BiomeId.SubarcticCompound,
                    regionToken = "subarcticcompound",
                    materialMixKeys = new[] { "mat.timber.treated", "mat.steel.corrugated", "mat.panel.insulated", "mat.concrete.frost", "mat.ice.sheet" },
                    propPaletteKeys = new[] { "prop.crate.wood", "prop.barrel.fuel", "prop.vehicle.truck", "prop.foliage.pine", "prop.debris.rubble", "prop.sandbag.emplacement" },
                    groundTextureKeys = new[] { "tex.snow.packed", "tex.ice.sheet", "tex.concrete.frost", "tex.gravel.frozen" },
                    ambientSoundKey = "sfx.ambient.cold_wind",
                    lightingKey = "light.low-angle.cold_day",
                    vegetationDensity = 0.3f,
                    conflictIntensityBias = 0.4f,
                    buildingStyle = new BuildingStyleParams
                    {
                        typicalStoreys = 2,
                        roofPitchDegrees = 28f,
                        windowDensity = 0.35f,
                        courtyardFrequency = 0.25f,
                        facadeVariation = 0.3f,
                        flatRoofsCommon = false
                    }
                },
                new BiomeProfile
                {
                    id = BiomeId.TemperateForestVillage,
                    regionToken = "temperateforestvillage",
                    materialMixKeys = new[] { "mat.timber.oak", "mat.plaster.white", "mat.stone.field", "mat.shingle.weathered", "mat.glass.cottage" },
                    propPaletteKeys = new[] { "prop.foliage.oak", "prop.foliage.bush", "prop.crate.wood", "prop.barrel.rusty", "prop.furniture.pallet", "prop.vehicle.sedan", "prop.debris.rubble" },
                    groundTextureKeys = new[] { "tex.grass.patched", "tex.mud.trail", "tex.stone.flag", "tex.needlelitter" },
                    ambientSoundKey = "sfx.ambient.forest_village",
                    lightingKey = "light.soft.overcast_green",
                    vegetationDensity = 0.8f,
                    conflictIntensityBias = 0.35f,
                    buildingStyle = new BuildingStyleParams
                    {
                        typicalStoreys = 2,
                        roofPitchDegrees = 32f,
                        windowDensity = 0.5f,
                        courtyardFrequency = 0.35f,
                        facadeVariation = 0.5f,
                        flatRoofsCommon = false
                    }
                }
            };
        }
    }
}
