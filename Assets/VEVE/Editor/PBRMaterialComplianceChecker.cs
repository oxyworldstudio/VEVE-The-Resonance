using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VEVE.Editor
{
    /// <summary>
    /// Static editor audit that flags PBR compliance violations on project materials and their imported textures.
    /// Checks: physically impossible base albedo on metallic surfaces, sRGB flag mismatch on normal/metallic/mask
    /// textures, missing normal maps on Lit (opaque/cutout Standard) materials, and non-power-of-two textures.
    /// Emits plain EditorLog warnings only (no requests, no modal dialogs), plus one summary line.
    /// Depends exclusively on <c>UnityEditor</c> and <c>UnityEngine</c>.
    /// </summary>
    public static class PBRMaterialComplianceChecker
    {
        /// <summary>Standard shader names treated as Lit surfaces by the audit.</summary>
        private static readonly HashSet<string> LitShaderNames = new HashSet<string>
        {
            "Standard",
            "Standard (Specular setup)"
        };

        private const float MetalAlbedoCeiling = 0.95f;
        private const float MetalMetallicFloor = 0.5f;

        /// <summary>
        /// Menu entry: scans the primary materials root (Assets/VEVE/Materials, falling back to all of
        /// Assets/ when that folder does not exist) and logs all findings to the Editor console.
        /// </summary>
        [MenuItem("VEVE/Graphics/Audit PBR Material Compliance")]
        public static void AuditMaterialsFolder()
        {
            int warnings = AuditMaterials(DefaultScanFolders());
            Debug.Log($"[PBRCompliance] Audit finished: {warnings} warning(s).");
        }

        /// <summary>
        /// Menu entry: audits every material asset in the project.
        /// </summary>
        [MenuItem("VEVE/Graphics/Audit PBR Compliance (Whole Project)")]
        public static void AuditEntireProject()
        {
            int warnings = AuditMaterials(new[] { "Assets" });
            Debug.Log($"[PBRCompliance] Whole-project audit finished: {warnings} warning(s).");
        }

        /// <summary>
        /// Runs the full audit over every material found under the given folders.
        /// </summary>
        /// <param name="folders">Search folders for <c>AssetDatabase.FindAssets</c>.</param>
        /// <returns>Total number of warnings emitted.</returns>
        public static int AuditMaterials(string[] folders)
        {
            if (folders == null || folders.Length == 0)
            {
                Debug.LogWarning("[PBRCompliance] No scan folders provided; nothing to audit.");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", folders);
            int warnings = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                warnings += AuditMaterial(material, path);
            }

            return warnings;
        }

        /// <summary>
        /// Audits a single material: albedo/metal legality, normal map presence and texture import correctness.
        /// </summary>
        /// <param name="material">Material to check; ignored when null.</param>
        /// <param name="path">Asset path used in the emitted warnings.</param>
        /// <returns>Number of warnings emitted for this material.</returns>
        public static int AuditMaterial(Material material, string path)
        {
            int warnings = 0;
            if (material == null)
            {
                return warnings;
            }

            bool isLit = material.shader != null && LitShaderNames.Contains(material.shader.name);
            float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            bool hasMetallicMap = material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") != null;

            if (isLit && metallic >= MetalMetallicFloor)
            {
                Color baseColor = material.HasProperty("_Color") ? material.color : Color.white;
                if (baseColor.r > MetalAlbedoCeiling && baseColor.g > MetalAlbedoCeiling && baseColor.b > MetalAlbedoCeiling)
                {
                    warnings++;
                    Debug.LogWarning($"[PBRCompliance] '{path}': metallic surface ({metallic:F2}) with base color {ColorSummary(baseColor)} exceeds the {MetalAlbedoCeiling:F2} albedo ceiling. Real conductor F0 values are <= 0.08-1.0 only as masked specular colors; desaturate/darken the base tint.");
                }
            }

            Texture mainMap = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Texture bumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
            if (isLit && bumpMap == null)
            {
                warnings++;
                Debug.LogWarning($"[PBRCompliance] '{path}': Lit material without a normal map (_BumpMap). Assign one or switch to a non-bumped shader; micro-detail relief will be lost.");
            }

            if (mainMap != null)
            {
                warnings += CheckTexture(mainMap, path, TextureRole.Albedo);
            }

            if (bumpMap != null)
            {
                warnings += CheckTexture(bumpMap, path, TextureRole.Normal);
            }

            if (hasMetallicMap)
            {
                warnings += CheckTexture(material.GetTexture("_MetallicGlossMap"), path, TextureRole.MetallicMask);
            }

            Texture detailMask = material.HasProperty("_DetailMask") ? material.GetTexture("_DetailMask") : null;
            if (detailMask != null)
            {
                warnings += CheckTexture(detailMask, path, TextureRole.MetallicMask);
            }

            Texture detailNormal = material.HasProperty("_DetailNormalMap") ? material.GetTexture("_DetailNormalMap") : null;
            if (detailNormal != null)
            {
                warnings += CheckTexture(detailNormal, path, TextureRole.Normal);
            }

            Texture occlusion = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
            if (occlusion != null)
            {
                warnings += CheckTexture(occlusion, path, TextureRole.MetallicMask);
            }

            return warnings;
        }

        /// <summary>
        /// Audits a single texture's import settings for its intended PBR role.
        /// </summary>
        /// <param name="texture">Texture asset to inspect.</param>
        /// <param name="usingMaterialPath">Material path included in the warning for traceability.</param>
        /// <param name="role">How the texture is consumed (drives the sRGB/type expectation).</param>
        /// <returns>Number of warnings emitted for this texture.</returns>
        public static int CheckTexture(Texture texture, string usingMaterialPath, TextureRole role)
        {
            int warnings = 0;
            if (texture == null)
            {
                return warnings;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                return warnings;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (!IsPowerOfTwo(texture.width) || !IsPowerOfTwo(texture.height))
            {
                warnings++;
                Debug.LogWarning($"[PBRCompliance] '{assetPath}' is non-power-of-two ({texture.width}x{texture.height}) and used by '{usingMaterialPath}'. Re-author to a POT size for mip/BC compression efficiency.");
            }

            if (importer == null)
            {
                return warnings;
            }

            switch (role)
            {
                case TextureRole.Normal:
                    if (importer.textureType != TextureImporterType.NormalMap)
                    {
                        warnings++;
                        Debug.LogWarning($"[PBRCompliance] '{assetPath}' is bound as a normal map by '{usingMaterialPath}' but importer type is '{importer.textureType}'. Set it to NormalMap (tangent space).");
                    }

                    if (importer.sRGBTexture)
                    {
                        warnings++;
                        Debug.LogWarning($"[PBRCompliance] '{assetPath}' is a normal map with sRGB enabled on '{usingMaterialPath}'. Normal maps must be linear.");
                    }

                    break;

                case TextureRole.MetallicMask:
                    if (importer.sRGBTexture)
                    {
                        warnings++;
                        Debug.LogWarning($"[PBRCompliance] '{assetPath}' is a metallic/mask texture with sRGB enabled on '{usingMaterialPath}'. Data/mask textures must be linear.");
                    }

                    break;

                case TextureRole.Albedo:
                    if (importer.textureType == TextureImporterType.NormalMap)
                    {
                        warnings++;
                        Debug.LogWarning($"[PBRCompliance] '{assetPath}' is bound as base color on '{usingMaterialPath}' but is imported as a NormalMap. Wire it to _BumpMap instead.");
                    }

                    if (!importer.sRGBTexture && importer.textureType == TextureImporterType.Default)
                    {
                        warnings++;
                        Debug.LogWarning($"[PBRCompliance] '{assetPath}' is an albedo map imported linear on '{usingMaterialPath}'. Albedo must be sRGB.");
                    }

                    break;
            }

            return warnings;
        }

        /// <summary>
        /// Resolves the audit scope: the dedicated VEVE materials root when present, otherwise all assets.
        /// </summary>
        /// <returns>Folder filter array for AssetDatabase.FindAssets.</returns>
        public static string[] DefaultScanFolders()
        {
            return AssetDatabase.IsValidFolder("Assets/VEVE/Materials")
                ? new[] { "Assets/VEVE/Materials" }
                : new[] { "Assets" };
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static string ColorSummary(Color color)
        {
            return $"RGBA({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";
        }
    }

    /// <summary>
    /// PBR usage role expected of a texture bound to a material slot.
    /// </summary>
    public enum TextureRole
    {
        /// <summary>Base color / albedo: sRGB, default importer type.</summary>
        Albedo,

        /// <summary>Tangent-space normal: linear, NormalMap importer type.</summary>
        Normal,

        /// <summary>Metallic/roughness/AO/detail mask: linear data texture.</summary>
        MetallicMask
    }
}
