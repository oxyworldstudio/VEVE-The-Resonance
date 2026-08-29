using UnityEngine;
using UnityEngine.Rendering;

namespace VEVE.Graphics
{
    public enum SurfaceType { Opaque, Transparent, Cutout }
    public enum BlendMode { AlbedoAlpha, Premultiply, Additive, Multiply }
    public enum LightingMode { Standard, Subsurface, Clearcoat, Anisotropic }

    [CreateAssetMenu(menuName = "VEVE/Graphics/Advanced Material Preset")]
    public sealed class AdvancedMaterialPreset : ScriptableObject
    {
        [Header("PBR Base")]
        [SerializeField] private Color albedo = Color.white;
        [SerializeField] private float metallic = 0f;
        [SerializeField] private float roughness = 0.5f;
        [SerializeField] private Texture2D normalMap;
        [SerializeField] private float normalStrength = 1f;
        [SerializeField] private Texture2D aoMap;
        [SerializeField] private float aoStrength = 1f;
        [SerializeField] private Texture2D heightMap;
        [SerializeField] private float heightScale = 0.05f;

        [Header("Advanced PBR")]
        [SerializeField] private LightingMode lightingMode = LightingMode.Standard;
        [SerializeField] private Vector3 sssDiffusion = new Vector3(1.0f, 0.2f, 0.1f);
        [SerializeField] private float sssScale = 1.0f;
        [SerializeField] private float clearcoat = 0f;
        [SerializeField] private float clearcoatRoughness = 0f;
        [SerializeField] private float anisotropy = 0f;
        [SerializeField] private float anisotropyRotation = 0f;
        [SerializeField] private float sheen = 0f;
        [SerializeField] private float sheenRoughness = 0.5f;

        [Header("Visual Effects")]
        [SerializeField] private bool enableParallax = false;
        [SerializeField] private float parallaxScale = 0.05f;
        [SerializeField] private float parallaxSteps = 8;
        [SerializeField] private bool enableTessellation = false;
        [SerializeField] private float tessellationStrength = 1f;
        [SerializeField] private float tessellationEdge = 64f;

        [Header("Gameplay Integration")]
        [SerializeField] private SurfaceMaterial ballisticMaterial;
        [SerializeField] private float acousticAbsorption = 0.2f;
        [SerializeField] private float thermalEmissivity = 0f;
        [SerializeField] private float reflectivityIR = 0f;

        public Color Albedo => albedo;
        public float Metallic => metallic;
        public float Roughness => roughness;
        public Texture2D NormalMap => normalMap;
        public float NormalStrength => normalStrength;
        public Texture2D AOMap => aoMap;
        public float AOStrength => aoStrength;
        public Texture2D HeightMap => heightMap;
        public float HeightScale => heightScale;
        public LightingMode LightingMode => lightingMode;
        public Vector3 SSSDiffusion => sssDiffusion;
        public float SSSScale => sssScale;
        public float Clearcoat => clearcoat;
        public float ClearcoatRoughness => clearcoatRoughness;
        public float Anisotropy => anisotropy;
        public float AnisotropyRotation => anisotropyRotation;
        public float Sheen => sheen;
        public float SheenRoughness => sheenRoughness;
        public bool EnableParallax => enableParallax;
        public float ParallaxScale => parallaxScale;
        public float ParallaxSteps => parallaxSteps;
        public bool EnableTessellation => enableTessellation;
        public float TessellationStrength => tessellationStrength;
        public float TessellationEdge => tessellationEdge;
        public SurfaceMaterial BallisticMaterial => ballisticMaterial;
        public float AcousticAbsorption => acousticAbsorption;
        public float ThermalEmissivity => thermalEmissivity;
        public float ReflectivityIR => reflectivityIR;
    }
}
