using UnityEngine;
using System;

namespace VEVE
{
    /// <summary>
    /// Represents a volumetric fog volume with density, height, and noise-based variation
    /// for realistic atmospheric effects.
    /// </summary>
    public sealed class FogVolume : MonoBehaviour
    {
        [Header("Fog Volume Configuration")]
        [SerializeField] private Vector3 volumeSize = new Vector3(100f, 50f, 100f);
        [SerializeField] private float density = 0.5f;
        [SerializeField] private float targetDensity = 0.5f;
        [SerializeField] private float densityTransitionSpeed = 0.1f;

        [Header("Height Configuration")]
        [SerializeField] private float heightFalloff = 1.0f;
        [SerializeField] private float baseHeight = 0f;
        [SerializeField] private float ceilingHeight = 50f;

        [Header("Noise Configuration")]
        [SerializeField] private float noiseScale = 0.02f;
        [SerializeField] private float noiseSpeed = 0.5f;
        [SerializeField] private float noiseAmplitude = 0.5f;
        [SerializeField] private int noiseOctaves = 3;
        [SerializeField] private float noiseLacunarity = 2.0f;
        [SerializeField] private float noisePersistence = 0.5f;

        [Header("Color")]
        [SerializeField] private Color fogColor = new Color(0.6f, 0.6f, 0.65f);
        [SerializeField] private Color scatteringColor = new Color(0.8f, 0.8f, 0.85f);
        [SerializeField] private float absorption = 0.5f;

        [Header("Animation")]
        [SerializeField] private Vector3 driftSpeed = new Vector3(1f, 0f, 0.5f);
        [SerializeField] private float turbulence = 0.3f;

        [Header("Rendering")]
        [SerializeField] private bool useVolumetricRendering = true;
        [SerializeField] private int rayMarchSteps = 32;
        [SerializeField] private float maxRenderDistance = 500f;

        [Header("Weather Influence")]
        [SerializeField] private float weatherDensityMultiplier = 1f;
        [SerializeField] private float rainDensityBoost = 0.3f;
        [SerializeField] private float snowDensityBoost = 0.2f;

        private Vector3 currentNoiseOffset;
        private float currentDensity;
        private int volumeIndex = -1;
        private static int nextVolumeIndex = 0;

        /// <summary>
        /// Gets or sets the fog density.
        /// </summary>
        public float Density
        {
            get => density;
            set
            {
                targetDensity = Mathf.Clamp01(value);
                density = targetDensity;
            }
        }

        /// <summary>
        /// Gets or sets the fog color.
        /// </summary>
        public Color FogColor
        {
            get => fogColor;
            set => fogColor = value;
        }

        /// <summary>
        /// Gets or sets the volume size.
        /// </summary>
        public Vector3 VolumeSize
        {
            get => volumeSize;
            set => volumeSize = value;
        }

        /// <summary>
        /// Gets the current animated density.
        /// </summary>
        public float CurrentDensity => currentDensity;

        /// <summary>
        /// Gets the center position of the fog volume.
        /// </summary>
        public Vector3 Center => transform.position;

        private void Awake()
        {
            volumeIndex = nextVolumeIndex++;
            currentDensity = density;
        }

        private void Update()
        {
            UpdateDensity();
            UpdateNoiseOffset();
            UpdateShaderParameters();
        }

        /// <summary>
        /// Updates the fog density with smooth transitions.
        /// </summary>
        private void UpdateDensity()
        {
            currentDensity = Mathf.Lerp(currentDensity, targetDensity * weatherDensityMultiplier, Time.deltaTime * densityTransitionSpeed);
        }

        /// <summary>
        /// Updates the noise offset for animation.
        /// </summary>
        private void UpdateNoiseOffset()
        {
            currentNoiseOffset += driftSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Updates shader parameters for volumetric rendering.
        /// </summary>
        private void UpdateShaderParameters()
        {
            if (!useVolumetricRendering) return;

            Shader.SetGlobalVector($"_FogVolumeCenter{volumeIndex}", transform.position);
            Shader.SetGlobalVector($"_FogVolumeSize{volumeIndex}", volumeSize);
            Shader.SetGlobalFloat($"_FogVolumeDensity{volumeIndex}", currentDensity);
            Shader.SetGlobalColor($"_FogVolumeColor{volumeIndex}", fogColor);
            Shader.SetGlobalFloat($"_FogVolumeHeightFalloff{volumeIndex}", heightFalloff);
            Shader.SetGlobalVector($"_FogVolumeNoiseOffset{volumeIndex}", currentNoiseOffset);
            Shader.SetGlobalFloat($"_FogVolumeNoiseScale{volumeIndex}", noiseScale);
            Shader.SetGlobalFloat($"_FogVolumeNoiseAmplitude{volumeIndex}", noiseAmplitude);
        }

        /// <summary>
        /// Samples the fog density at a given world position.
        /// </summary>
        /// <param name="worldPosition">World space position to sample.</param>
        /// <returns>Fog density at the position (0-1).</returns>
        public float SampleDensity(Vector3 worldPosition)
        {
            Vector3 localPoint = worldPosition - transform.position;
            Vector3 normalizedPoint = new Vector3(
                localPoint.x / (volumeSize.x * 0.5f),
                localPoint.y / (volumeSize.y * 0.5f),
                localPoint.z / (volumeSize.z * 0.5f)
            );

            float normalizedLength = normalizedPoint.magnitude;
            if (normalizedLength > 1f) return 0f;

            float densityFalloff = 1f - normalizedLength;
            densityFalloff = densityFalloff * densityFalloff * (3f - 2f * densityFalloff);

            float heightFactor = CalculateHeightFactor(localPoint.y);
            float noise = CalculateNoise(worldPosition);

            return currentDensity * densityFalloff * heightFactor * noise;
        }

        /// <summary>
        /// Calculates the height-based density factor.
        /// </summary>
        private float CalculateHeightFactor(float localY)
        {
            float normalizedHeight = (localY + volumeSize.y * 0.5f) / volumeSize.y;
            normalizedHeight = Mathf.Clamp01(normalizedHeight);

            float heightFactor = Mathf.Pow(1f - normalizedHeight, heightFalloff);
            return Mathf.Clamp01(heightFactor);
        }

        /// <summary>
        /// Calculates multi-octave noise for fog variation.
        /// </summary>
        private float CalculateNoise(Vector3 worldPosition)
        {
            float totalNoise = 0f;
            float amplitude = 1f;
            float frequency = noiseScale;
            float maxAmplitude = 0f;

            Vector3 animatedPosition = worldPosition + currentNoiseOffset;

            for (int i = 0; i < noiseOctaves; i++)
            {
                float x = animatedPosition.x * frequency;
                float y = animatedPosition.y * frequency;
                float z = animatedPosition.z * frequency;

                float noise = Mathf.PerlinNoise(x, y) * 0.5f +
                              Mathf.PerlinNoise(y, z) * 0.3f +
                              Mathf.PerlinNoise(z, x) * 0.2f;

                totalNoise += noise * amplitude;
                maxAmplitude += amplitude;
                amplitude *= noisePersistence;
                frequency *= noiseLacunarity;
            }

            totalNoise /= maxAmplitude;
            return 1f + (totalNoise - 0.5f) * noiseAmplitude;
        }

        /// <summary>
        /// Sets the weather influence on fog density.
        /// </summary>
        /// <param name="weather">Current weather state.</param>
        public void SetWeatherInfluence(WeatherState weather)
        {
            weatherDensityMultiplier = weather switch
            {
                WeatherState.Fog => 2.0f,
                WeatherState.Rain => 1.0f + rainDensityBoost,
                WeatherState.Snow => 1.0f + snowDensityBoost,
                WeatherState.Thunderstorm => 1.5f,
                WeatherState.Overcast => 1.2f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Sets the noise parameters for fog animation.
        /// </summary>
        /// <param name="scale">Noise scale.</param>
        /// <param name="speed">Animation speed.</param>
        /// <param name="amplitude">Noise amplitude.</param>
        public void SetNoiseParameters(float scale, float speed, float amplitude)
        {
            noiseScale = scale;
            noiseSpeed = speed;
            noiseAmplitude = amplitude;
        }

        /// <summary>
        /// Sets the height falloff parameters.
        /// </summary>
        /// <param name="falloff">Height falloff exponent.</param>
        /// <param name="baseHeight">Base height of the fog.</param>
        /// <param name="ceiling">Ceiling height of the fog.</param>
        public void SetHeightParameters(float falloff, float baseHeight, float ceiling)
        {
            heightFalloff = falloff;
            this.baseHeight = baseHeight;
            ceilingHeight = ceiling;
        }

        /// <summary>
        /// Checks if a point is inside the fog volume.
        /// </summary>
        /// <param name="worldPosition">World space position.</param>
        /// <returns>True if inside the volume.</returns>
        public bool IsInsideVolume(Vector3 worldPosition)
        {
            Vector3 localPoint = worldPosition - transform.position;
            return Mathf.Abs(localPoint.x) <= volumeSize.x * 0.5f &&
                   Mathf.Abs(localPoint.y) <= volumeSize.y * 0.5f &&
                   Mathf.Abs(localPoint.z) <= volumeSize.z * 0.5f;
        }

        /// <summary>
        /// Calculates the visibility reduction at a point.
        /// </summary>
        /// <param name="worldPosition">World space position.</param>
        /// <returns>Visibility factor (0 = no visibility, 1 = full visibility).</returns>
        public float GetVisibility(Vector3 worldPosition)
        {
            float fogDensity = SampleDensity(worldPosition);
            return Mathf.Exp(-fogDensity * absorption);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0.3f);
            Gizmos.DrawCube(transform.position, volumeSize);
            Gizmos.color = fogColor;
            Gizmos.DrawWireCube(transform.position, volumeSize);
        }
    }
}
