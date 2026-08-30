using System;
using UnityEngine;
using VEVE.Procedural;

namespace VEVE.Graphics
{
    /// <summary>
    /// Pure-data mapping from biome to ground-splat texture array slices, designed to be uploaded to a shader
    /// as plain <c>float[]</c>/<c>int[]</c> uniform arrays. Contains no <see cref="UnityEngine.Object"/>
    /// references inside the index arrays (texture arrays themselves are referenced as single objects only,
    /// which is legal for Shader.SetTexture), so an instance can be serialized, batched and copied into
    /// constant buffers without handle marshalling per layer.
    /// <para>
    /// Layout convention: the ground shader samples a Texture2DArray where slice index
    /// <c>SetIndex * SlicesPerSet + slice</c>; each set packs Albedo(0), Normal(1), Height(2) and
    /// Mask-Roughness(3) of one surface from <see cref="ProceduralSurfaceTextureFactory"/>.
    /// Layer 0 of every biome is forced to the biome's dominant surface so single-texture fallbacks stay correct.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class TextureArrayMaterialAtlas
    {
        /// <summary>Texture array slices per surface set (albedo, normal, height, mask).</summary>
        public const int SlicesPerSet = 4;

        /// <summary>Hard cap of splat layers per biome, matching typical 8-layer terrain-style blending.</summary>
        public const int MaxLayersPerBiome = 8;

        /// <summary>
        /// One biome row: which texture-array sets participate in ground blending and their shader-side weights.
        /// All fields are value types / float arrays so the row is directly copyable to GPU uniform arrays.
        /// </summary>
        [Serializable]
        public sealed class BiomeGroundSet
        {
            [Tooltip("Biome this row belongs to.")]
            public BiomeId biome;

            [Tooltip("Texture-array set indices (each spans SlicesPerSet consecutive slices), ordered dominant -> sparse.")]
            public float[] layerSetIndices = new float[MaxLayersPerBiome];

            [Tooltip("Splat weight per layer; the shader normalizes across the row. Zero-weight layers must keep a valid index.")]
            public float[] layerWeights = new float[MaxLayersPerBiome];

            [Tooltip("UV tiling per layer (texture repeats per world meter) so adjacent layers never alias at the same scale.")]
            public float[] layerTiling = new float[MaxLayersPerBiome];

            [Tooltip("Number of valid entries in the per-layer arrays (1..MaxLayersPerBiome).")]
            [Range(1, MaxLayersPerBiome)]
            public int layerCount = 1;

            /// <summary>
            /// Creates a row pre-filled with sensible defaults; use <see cref="Configure"/> to overwrite.
            /// </summary>
            public BiomeGroundSet()
            {
                for (int i = 0; i < MaxLayersPerBiome; i++)
                {
                    layerSetIndices[i] = 0f;
                    layerWeights[i] = i == 0 ? 1f : 0f;
                    layerTiling[i] = 1f / (1f + i);
                }
            }

            /// <summary>
            /// Validates and clamps this row so it is always safe to stream into a shader array.
            /// </summary>
            /// <returns>True if the row required any correction.</returns>
            public bool Sanitize()
            {
                bool corrected = false;
                if (layerCount < 1 || layerCount > MaxLayersPerBiome)
                {
                    layerCount = Mathf.Clamp(layerCount, 1, MaxLayersPerBiome);
                    corrected = true;
                }

                float sum = 0f;
                for (int i = 0; i < layerCount; i++)
                {
                    if (layerWeights[i] < 0f)
                    {
                        layerWeights[i] = 0f;
                        corrected = true;
                    }

                    sum += layerWeights[i];
                }

                if (sum <= 0f)
                {
                    layerWeights[0] = 1f;
                    corrected = true;
                }

                return corrected;
            }
        }

        [Tooltip("All biome rows; lookup by index is O(1) and order-stable for deterministic shader uploads.")]
        public BiomeGroundSet[] rows = CreateDefaultRows();

        /// <summary>Number of biome rows in the atlas.</summary>
        public int Count => rows != null ? rows.Length : 0;

        /// <summary>
        /// Gets the row for a biome, or null when the biome has no entry.
        /// </summary>
        /// <param name="biome">Biome to look up.</param>
        /// <returns>The matching row or null.</returns>
        public BiomeGroundSet GetRow(BiomeId biome)
        {
            if (rows == null)
            {
                return null;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].biome == biome)
                {
                    return rows[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the absolute texture-array slice of (biome layer, surface map).
        /// </summary>
        /// <param name="biome">Biome to query.</param>
        /// <param name="layer">Splat layer index within the biome row.</param>
        /// <param name="map">0=albedo, 1=normal, 2=height, 3=mask.</param>
        /// <returns>Slice index into the ground Texture2DArray, or -1 when the lookup fails.</returns>
        public int GetSliceIndex(BiomeId biome, int layer, ProceduralSurfaceMap map)
        {
            BiomeGroundSet row = GetRow(biome);
            if (row == null || layer < 0 || layer >= row.layerCount)
            {
                return -1;
            }

            int set = Mathf.RoundToInt(row.layerSetIndices[layer]);
            return set * SlicesPerSet + (int)map;
        }

        /// <summary>
        /// Flattens all rows into stride-<see cref="MaxLayersPerBiome"/> float arrays suitable for
        /// <c>Material.SetFloatArray</c> / <c>Shader.SetGlobalFloatArray</c>. Unused stride slots are zero.
        /// </summary>
        /// <param name="outIndices">Set indices per biome per layer.</param>
        /// <param name="outWeights">Weights per biome per layer.</param>
        /// <param name="outTiling">Tiling per biome per layer.</param>
        public void GetFlatArrays(out float[] outIndices, out float[] outWeights, out float[] outTiling)
        {
            int count = Mathf.Max(1, Count);
            outIndices = new float[count * MaxLayersPerBiome];
            outWeights = new float[count * MaxLayersPerBiome];
            outTiling = new float[count * MaxLayersPerBiome];
            if (rows == null)
            {
                return;
            }

            for (int b = 0; b < rows.Length; b++)
            {
                BiomeGroundSet row = rows[b];
                if (row == null)
                {
                    continue;
                }

                int offset = b * MaxLayersPerBiome;
                int max = Mathf.Min(row.layerCount, MaxLayersPerBiome);
                for (int i = 0; i < max; i++)
                {
                    outIndices[offset + i] = row.layerSetIndices != null && i < row.layerSetIndices.Length ? row.layerSetIndices[i] : 0f;
                    outWeights[offset + i] = row.layerWeights != null && i < row.layerWeights.Length ? row.layerWeights[i] : 0f;
                    outTiling[offset + i] = row.layerTiling != null && i < row.layerTiling.Length ? row.layerTiling[i] : 1f;
                }
            }
        }

        /// <summary>
        /// Ensures every row is valid for GPU upload; called by consumers before streaming to shaders.
        /// </summary>
        /// <returns>True if any row was corrected.</returns>
        public bool SanitizeAll()
        {
            if (rows == null)
            {
                rows = CreateDefaultRows();
                return true;
            }

            bool corrected = false;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null)
                {
                    rows[i] = new BiomeGroundSet();
                    corrected = true;
                    continue;
                }

                corrected |= rows[i].Sanitize();
            }

            return corrected;
        }

        /// <summary>
        /// Builds the default atlas: one row per <see cref="BiomeId"/> with realistic layer choices
        /// (set indices reference procedural surface kinds: 0=Concrete, 1=Wood, 2=Metal, 3=Brick, 4=Fabric, 5=Dirt, 6=Glass, 7=Ice).
        /// </summary>
        /// <returns>A fully populated, sanitized row set.</returns>
        public static BiomeGroundSet[] CreateDefaultRows()
        {
            BiomeGroundSet[] defaultRows = new BiomeGroundSet[Enum.GetValues(typeof(BiomeId)).Length];
            for (int i = 0; i < defaultRows.Length; i++)
            {
                defaultRows[i] = new BiomeGroundSet
                {
                    biome = (BiomeId)i
                };

                switch ((BiomeId)i)
                {
                    case BiomeId.MediterraneanTown:
                        Configure(defaultRows[i], new float[] { 0f, 3f, 5f }, new float[] { 0.55f, 0.30f, 0.15f });
                        break;
                    case BiomeId.EasternEuropeanIndustrial:
                        Configure(defaultRows[i], new float[] { 0f, 2f, 5f }, new float[] { 0.50f, 0.25f, 0.25f });
                        break;
                    case BiomeId.DesertCheckpoint:
                        Configure(defaultRows[i], new float[] { 5f, 0f, 4f }, new float[] { 0.70f, 0.20f, 0.10f });
                        break;
                    case BiomeId.SubarcticCompound:
                        Configure(defaultRows[i], new float[] { 0f, 5f, 7f }, new float[] { 0.40f, 0.35f, 0.25f });
                        break;
                    case BiomeId.TemperateForestVillage:
                        Configure(defaultRows[i], new float[] { 5f, 4f, 1f }, new float[] { 0.55f, 0.30f, 0.15f });
                        break;
                }

                defaultRows[i].Sanitize();
            }

            return defaultRows;
        }

        private static void Configure(BiomeGroundSet row, float[] setIndices, float[] weights)
        {
            row.layerCount = Mathf.Min(MaxLayersPerBiome, setIndices.Length);
            for (int i = 0; i < row.layerCount; i++)
            {
                row.layerSetIndices[i] = setIndices[i];
                row.layerWeights[i] = weights[i];
                row.layerTiling[i] = 1f / (1f + 2f * i);
            }
        }
    }

    /// <summary>
    /// Slice ordering inside one texture-array surface set; matches
    /// <see cref="CachedTextureBundle"/> generation order in <see cref="ProceduralSurfaceTextureFactory"/>.
    /// </summary>
    public enum ProceduralSurfaceMap
    {
        /// <summary>sRGB albedo slice.</summary>
        Albedo = 0,

        /// <summary>Linear tangent-space normal slice.</summary>
        Normal = 1,

        /// <summary>Linear height slice.</summary>
        Height = 2,

        /// <summary>Linear packed roughness/AO/metallic mask slice.</summary>
        Mask = 3
    }
}
