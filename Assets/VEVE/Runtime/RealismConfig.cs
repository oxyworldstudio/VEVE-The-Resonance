using UnityEngine;
using System;

namespace VEVE.Realism
{
    public enum QualityLevel { Low, Medium, High, Ultra }

    [CreateAssetMenu(menuName = "VEVE/Realism/Configuration")]
    public sealed class RealismConfig : ScriptableObject
    {
        [Header("Quality Presets")]
        [SerializeField] private QualityLevel activePreset = QualityLevel.High;
        [SerializeField] private bool forceUltraQuality = true;
        [SerializeField] private float renderScale = 100f;
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

        [Header("Audio Preset")]
        [SerializeField] private int audioSampleRate = 48000;
        [SerializeField] private int audioSpeakerMode = 7;
        [SerializeField] private float dopplerFactor = 1f;
        [SerializeField] private float spatialBlend = 1f;
        [SerializeField] private float maxDistance = 1000f;
        [SerializeField] private float rolloffFactor = 1f;
        [SerializeField] private bool enableReverb = true;
        [SerializeField] private float reverbDecayTime = 3f;

        [Header("Gameplay Preset")]
        [SerializeField] private bool enableCoriolisEffect = true;
        [SerializeField] private bool enableSpinDrift = true;
        [SerializeField] private bool enableTemperatureGradient = true;
        [SerializeField] private float maximumSimulationRange = 5000f;
        [SerializeField] private bool enableSubstepping = true;
        [SerializeField] private float substeppingMaxDeltaTime = 0.016666f;
        [SerializeField] private int physicsSolverIterations = 20;
        [SerializeField] private int physicsSolverVelocityIterations = 10;

        [Header("Ballistics & Physics")]
        [SerializeField] private float standardGravity = 9.80665f;
        [SerializeField] private float airDensitySeaLevel = 1.225f;
        [SerializeField] private float temperatureSeaLevelCelsius = 15f;
        [SerializeField] private float pressureSeaLevelPa = 101325f;
        [SerializeField] private float coriolisCoefficient = 0.000072921f;
        [SerializeField] private bool enableCoriolisEffectBase = true;
        [SerializeField] private bool enableSpinDriftBase = true;
        [SerializeField] private bool enableTemperatureGradientBase = true;
        [SerializeField] private float maximumSimulationRangeBase = 5000f;

        [Header("Material Physics")]
        [SerializeField] private float steelDensity = 7850f;
        [SerializeField] private float leadDensity = 11340f;
        [SerializeField] private float copperDensity = 8960f;
        [SerializeField] private float concreteDensity = 2400f;
        [SerializeField] private float woodDensity = 600f;
        [SerializeField] private float ballisticLimitTolerance = 0.02f;

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
        [SerializeField] private int physicsSolverIterationsBase = 20;
        [SerializeField] private int physicsSolverVelocityIterationsBase = 10;
        [SerializeField] private bool enableSubsteppingBase = true;
        [SerializeField] private float substeppingMaxDeltaTimeBase = 0.016666f;

        public QualityLevel ActivePreset
        {
            get => activePreset;
            set
            {
                activePreset = value;
                ApplyPreset();
            }
        }
        public bool ForceUltraQuality => forceUltraQuality;
        public float RenderScale => renderScale;
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
        public bool EnableCoriolisEffect => enableCoriolisEffect;
        public bool EnableSpinDrift => enableSpinDrift;
        public bool EnableTemperatureGradient => enableTemperatureGradient;
        public float MaximumSimulationRange => maximumSimulationRange;
        public bool EnableSubstepping => enableSubstepping;
        public float SubsteppingMaxDeltaTime => substeppingMaxDeltaTime;
        public int PhysicsSolverIterations => physicsSolverIterations;
        public int PhysicsSolverVelocityIterations => physicsSolverVelocityIterations;
        public float StandardGravity => standardGravity;
        public float AirDensitySeaLevel => airDensitySeaLevel;
        public float TemperatureSeaLevelCelsius => temperatureSeaLevelCelsius;
        public float PressureSeaLevelPa => pressureSeaLevelPa;
        public float CoriolisCoefficient => coriolisCoefficient;
        public float SteelDensity => steelDensity;
        public float LeadDensity => leadDensity;
        public float CopperDensity => copperDensity;
        public float ConcreteDensity => concreteDensity;
        public float WoodDensity => woodDensity;
        public float BallisticLimitTolerance => ballisticLimitTolerance;
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

        private void Reset()
        {
            ApplyPreset();
        }

        public void ApplyPreset()
        {
            switch (activePreset)
            {
                case QualityLevel.Low:
                    renderScale = 50f;
                    targetFrameRate = 60;
                    enableVSync = false;
                    lodBias = 1f;
                    shadowDistance = 100f;
                    shadowCascades = 0;
                    shadowCascadeSplit = 0.33f;
                    enableHDR = false;
                    enableAntiAliasing = false;
                    antiAliasingSamples = 0;
                    textureStreamingBudget = 512f;
                    audioSampleRate = 44100;
                    audioSpeakerMode = 2;
                    dopplerFactor = 0.5f;
                    spatialBlend = 1f;
                    maxDistance = 500f;
                    rolloffFactor = 1f;
                    enableReverb = false;
                    reverbDecayTime = 1.5f;
                    enableCoriolisEffect = false;
                    enableSpinDrift = false;
                    enableTemperatureGradient = false;
                    maximumSimulationRange = 1000f;
                    enableSubstepping = false;
                    substeppingMaxDeltaTime = 0.033333f;
                    physicsSolverIterations = 10;
                    physicsSolverVelocityIterations = 5;
                    break;
                case QualityLevel.Medium:
                    renderScale = 75f;
                    targetFrameRate = 60;
                    enableVSync = true;
                    lodBias = 1.5f;
                    shadowDistance = 150f;
                    shadowCascades = 2;
                    shadowCascadeSplit = 0.2f;
                    enableHDR = true;
                    enableAntiAliasing = true;
                    antiAliasingSamples = 4;
                    textureStreamingBudget = 1024f;
                    audioSampleRate = 44100;
                    audioSpeakerMode = 5;
                    dopplerFactor = 0.75f;
                    spatialBlend = 1f;
                    maxDistance = 750f;
                    rolloffFactor = 1f;
                    enableReverb = true;
                    reverbDecayTime = 2f;
                    enableCoriolisEffect = true;
                    enableSpinDrift = false;
                    enableTemperatureGradient = true;
                    maximumSimulationRange = 2500f;
                    enableSubstepping = true;
                    substeppingMaxDeltaTime = 0.016666f;
                    physicsSolverIterations = 14;
                    physicsSolverVelocityIterations = 7;
                    break;
                case QualityLevel.High:
                    renderScale = 100f;
                    targetFrameRate = 60;
                    enableVSync = true;
                    lodBias = 2f;
                    shadowDistance = 200f;
                    shadowCascades = 4;
                    shadowCascadeSplit = 0.1f;
                    enableHDR = true;
                    enableAntiAliasing = true;
                    antiAliasingSamples = 8;
                    textureStreamingBudget = 2048f;
                    audioSampleRate = 48000;
                    audioSpeakerMode = 7;
                    dopplerFactor = 1f;
                    spatialBlend = 1f;
                    maxDistance = 1000f;
                    rolloffFactor = 1f;
                    enableReverb = true;
                    reverbDecayTime = 3f;
                    enableCoriolisEffect = true;
                    enableSpinDrift = true;
                    enableTemperatureGradient = true;
                    maximumSimulationRange = 5000f;
                    enableSubstepping = true;
                    substeppingMaxDeltaTime = 0.016666f;
                    physicsSolverIterations = 20;
                    physicsSolverVelocityIterations = 10;
                    break;
                case QualityLevel.Ultra:
                    renderScale = 100f;
                    targetFrameRate = 60;
                    enableVSync = true;
                    lodBias = 2.5f;
                    shadowDistance = 500f;
                    shadowCascades = 4;
                    shadowCascadeSplit = 0.1f;
                    enableHDR = true;
                    enableAntiAliasing = true;
                    antiAliasingSamples = 8;
                    textureStreamingBudget = 4096f;
                    audioSampleRate = 48000;
                    audioSpeakerMode = 7;
                    dopplerFactor = 1f;
                    spatialBlend = 1f;
                    maxDistance = 2000f;
                    rolloffFactor = 1f;
                    enableReverb = true;
                    reverbDecayTime = 4f;
                    enableCoriolisEffect = true;
                    enableSpinDrift = true;
                    enableTemperatureGradient = true;
                    maximumSimulationRange = 10000f;
                    enableSubstepping = true;
                    substeppingMaxDeltaTime = 0.008333f;
                    physicsSolverIterations = 30;
                    physicsSolverVelocityIterations = 15;
                    break;
            }
        }

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
