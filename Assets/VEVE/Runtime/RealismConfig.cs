using UnityEngine;
using System;

namespace VEVE.Realism
{
    [CreateAssetMenu(menuName = "VEVE/Realism/Configuration")]
    public sealed class RealismConfig : ScriptableObject
    {
        [Header("Ballistics & Physics")]
        [SerializeField] private float standardGravity = 9.80665f;
        [SerializeField] private float airDensitySeaLevel = 1.225f;
        [SerializeField] private float temperatureSeaLevelCelsius = 15f;
        [SerializeField] private float pressureSeaLevelPa = 101325f;
        [SerializeField] private float coriolisCoefficient = 0.000072921f;
        [SerializeField] private bool enableCoriolisEffect = true;
        [SerializeField] private bool enableSpinDrift = true;
        [SerializeField] private bool enableTemperatureGradient = true;
        [SerializeField] private float maximumSimulationRange = 5000f;

        [Header("Material Physics")]
        [SerializeField] private float steelDensity = 7850f;
        [SerializeField] private float leadDensity = 11340f;
        [SerializeField] private float copperDensity = 8960f;
        [SerializeField] private float concreteDensity = 2400f;
        [SerializeField] private float woodDensity = 600f;
        [SerializeField] private float ballisticLimitTolerance = 0.02f;

        [Header("Rendering Fidelity")]
        [SerializeField] private bool forceUltraQuality = true;
        [SerializeField] private int renderScale = 100;
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool enableVSync = true;
        [SerializeField] private float lodBias = 2.5f;
        [SerializeField] private float shadowDistance = 200f;
        [SerializeField] private int shadowCascades = 4;
        [SerializeField] private float shadowCascadeSplit = 0.1f;
        [SerializeField] private bool enableHDR = true;
        [SerializeField] private bool enableAntiAliasing = true;
        [SerializeField] private int antiAliasingSamples = 8;
        [SerializeField] private float textureStreamingBudget = 2048f;

        [Header("Audio Fidelity")]
        [SerializeField] private int audioSampleRate = 48000;
        [SerializeField] private int audioSpeakerMode = 7;
        [SerializeField] private float dopplerFactor = 1f;
        [SerializeField] private float spatialBlend = 1f;
        [SerializeField] private float maxDistance = 1000f;
        [SerializeField] private float rolloffFactor = 1f;
        [SerializeField] private bool enableReverb = true;
        [SerializeField] private float reverbDecayTime = 3f;

        [Header("Environment")]
        [SerializeField] private float lapseRate = 0.0065f;
        [SerializeField] private float windShearExponent = 0.143f;
        [SerializeField] private float precipitationVisibilityRange = 800f;
        [SerializeField] private float fogVisibilityRange = 1500f;

        [Header("Physiology")]
        [SerializeField] private float bloodVolumeLiters = 5f;
        [SerializeField] private float restingHeartRateBPM = 65f;
        [SerializeField] private float maxHeartRateBPM = 220f;
        [SerializeField] private float restingRespirationRate = 15f;
        [SerializeField] private float maxRespirationRate = 60f;
        [SerializeField] private float bloodLossLethalThreshold = 2.0f;
        [SerializeField] private float unconsciousnessThreshold = 0.3f;

        [Header("Simulation")]
        [SerializeField] private float fixedDeltaTime = 0.008333f;
        [SerializeField] private float maximumDeltaTime = 0.1f;
        [SerializeField] private int physicsSolverIterations = 20;
        [SerializeField] private int physicsSolverVelocityIterations = 10;
        [SerializeField] private bool enableSubstepping = true;
        [SerializeField] private float substeppingMaxDeltaTime = 0.016666f;

        public float StandardGravity => standardGravity;
        public float AirDensitySeaLevel => airDensitySeaLevel;
        public float TemperatureSeaLevelCelsius => temperatureSeaLevelCelsius;
        public float PressureSeaLevelPa => pressureSeaLevelPa;
        public float CoriolisCoefficient => coriolisCoefficient;
        public bool EnableCoriolisEffect => enableCoriolisEffect;
        public bool EnableSpinDrift => enableSpinDrift;
        public bool EnableTemperatureGradient => enableTemperatureGradient;
        public float MaximumSimulationRange => maximumSimulationRange;
        public float SteelDensity => steelDensity;
        public float LeadDensity => leadDensity;
        public float CopperDensity => copperDensity;
        public float ConcreteDensity => concreteDensity;
        public float WoodDensity => woodDensity;
        public float BallisticLimitTolerance => ballisticLimitTolerance;
        public bool ForceUltraQuality => forceUltraQuality;
        public int RenderScale => renderScale;
        public int TargetFrameRate => targetFrameRate;
        public bool EnableVSync => enableVSync;
        public float LODBias => lodBias;
        public float ShadowDistance => shadowDistance;
        public int ShadowCascades => shadowCascades;
        public float ShadowCascadeSplit => shadowCascadeSplit;
        public bool EnableHDR => enableHDR;
        public bool EnableAntiAliasing => enableAntiAliasing;
        public int AntiAliasingSamples => antiAliasingSamples;
        public float TextureStreamingBudget => textureStreamingBudget;
        public int AudioSampleRate => audioSampleRate;
        public int AudioSpeakerMode => audioSpeakerMode;
        public float DopplerFactor => dopplerFactor;
        public float SpatialBlend => spatialBlend;
        public float MaxDistance => maxDistance;
        public float RolloffFactor => rolloffFactor;
        public bool EnableReverb => enableReverb;
        public float ReverbDecayTime => reverbDecayTime;
        public float LapseRate => lapseRate;
        public float WindShearExponent => windShearExponent;
        public float PrecipitationVisibilityRange => precipitationVisibilityRange;
        public float FogVisibilityRange => fogVisibilityRange;
        public float BloodVolumeLiters => bloodVolumeLiters;
        public float RestingHeartRateBPM => restingHeartRateBPM;
        public float MaxHeartRateBPM => maxHeartRateBPM;
        public float RestingRespirationRate => restingRespirationRate;
        public float MaxRespirationRate => maxRespirationRate;
        public float BloodLossLethalThreshold => bloodLossLethalThreshold;
        public float UnconsciousnessThreshold => unconsciousnessThreshold;
        public float FixedDeltaTime => fixedDeltaTime;
        public float MaximumDeltaTime => maximumDeltaTime;
        public int PhysicsSolverIterations => physicsSolverIterations;
        public int PhysicsSolverVelocityIterations => physicsSolverVelocityIterations;
        public bool EnableSubstepping => enableSubstepping;
        public float SubsteppingMaxDeltaTime => substeppingMaxDeltaTime;

        public static float CalculateAirDensity(float altitude, float temperatureCelsius)
        {
            float temperatureSeaLevel = 15f;
            float pressureSeaLevelPa = 101325f;
            float standardGravity = 9.80665f;
            float lapseRate = 0.0065f;
            float gasConstant = 287.05f;

            float temperatureKelvin = temperatureCelsius + 273.15f;
            float pressure = pressureSeaLevelPa * Mathf.Pow(1f - (lapseRate * altitude) / temperatureSeaLevel, (standardGravity * 0.0289644f) / (gasConstant * lapseRate));
            float saturationVaporPressure = 610.94f * Mathf.Exp((17.625f * temperatureCelsius) / (temperatureCelsius + 243.04f));
            float vaporPressure = 0.5f * saturationVaporPressure;
            return (pressure - vaporPressure) / (gasConstant * temperatureKelvin);
        }

        public static float CalculateWindSpeed(float altitude, float surfaceWindSpeed)
        {
            if (surfaceWindSpeed <= 0f) return 0f;
            return surfaceWindSpeed * Mathf.Pow(altitude / 1.5f, 0.143f);
        }
    }
}
