using UnityEngine;
using UnityEngine.Rendering;
using System;

namespace VEVE
{
    /// <summary>
    /// Controls real-time directional light with physically correct intensity, color temperature,
    /// and shadow quality based on sun position and weather conditions.
    /// </summary>
    public sealed class LightingController : MonoBehaviour
    {
        [Header("Light References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;
        [SerializeField] private Light[] ambientLights;

        [Header("Sun Configuration")]
        [SerializeField] private float maxSunIntensity = 1.4f;
        [SerializeField] private float minSunIntensity = 0f;
        [SerializeField] private float sunIntensitySmoothing = 2f;
        [SerializeField] private AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Color Temperature")]
        [SerializeField] private float sunriseTemperature = 2000f;
        [SerializeField] private float middayTemperature = 6500f;
        [SerializeField] private float sunsetTemperature = 2500f;
        [SerializeField] private float nightTemperature = 4500f;
        [SerializeField] private bool useColorTemperature = true;

        [Header("Color Gradient")]
        [SerializeField] private Gradient sunColorByElevation;
        [SerializeField] private Gradient ambientColorByElevation;
        [SerializeField] private Gradient skyColorByElevation;

        [Header("Shadow Configuration")]
        [SerializeField] private float shadowDistance = 200f;
        [SerializeField] private LightShadowResolution shadowResolution = LightShadowResolution.VeryHigh;
        [SerializeField] private int shadowCascades = 4;
        [SerializeField] private float shadowBias = 0.05f;
        [SerializeField] private float shadowNormalBias = 0.4f;
        [SerializeField] private float shadowNearPlane = 0.2f;
        [SerializeField] private float shadowStrengthDay = 1f;
        [SerializeField] private float shadowStrengthNight = 0.4f;

        [Header("Moon Configuration")]
        [SerializeField] private float maxMoonIntensity = 0.3f;
        [SerializeField] private float moonTemperature = 4100f;
        [SerializeField] private Color moonColor = new Color(0.8f, 0.85f, 1f);

        [Header("Ambient Light")]
        [SerializeField] private float ambientIntensityDay = 0.5f;
        [SerializeField] private float ambientIntensityNight = 0.1f;
        [SerializeField] private AmbientMode ambientMode = AmbientMode.Trilight;

        [Header("Reflection")]
        [SerializeField] private ReflectionProbe reflectionProbe;
        [SerializeField] private float reflectionUpdateInterval = 1f;
        [SerializeField] private int reflectionResolution = 128;

        [Header("Weather Influence")]
        [SerializeField] private float weatherIntensityModifier = 1f;
        [SerializeField] private float weatherColorModifier = 1f;

        private float currentSunIntensity;
        private float currentMoonIntensity;
        private float currentAmbientIntensity;
        private float sunElevation;
        private Vector3 sunDirection;
        private Vector3 moonDirection;
        private float reflectionTimer;
        private Color currentSunColor;
        private Color currentAmbientColor;

        /// <summary>
        /// Gets the current sun intensity.
        /// </summary>
        public float CurrentSunIntensity => currentSunIntensity;

        /// <summary>
        /// Gets the current moon intensity.
        /// </summary>
        public float CurrentMoonIntensity => currentMoonIntensity;

        /// <summary>
        /// Gets the current sun color.
        /// </summary>
        public Color CurrentSunColor => currentSunColor;

        /// <summary>
        /// Gets the current sun direction.
        /// </summary>
        public Vector3 SunDirection => sunDirection;

        /// <summary>
        /// Gets the current sun elevation in degrees.
        /// </summary>
        public float SunElevation => sunElevation;

        /// <summary>
        /// Gets whether it is currently daytime.
        /// </summary>
        public bool IsDaytime => sunElevation > -6f;

        private void Start()
        {
            InitializeLighting();
        }

        private void Update()
        {
            UpdateSunLight();
            UpdateMoonLight();
            UpdateAmbientLight();
            UpdateShadows();
            UpdateReflectionProbe();
        }

        /// <summary>
        /// Initializes the lighting system.
        /// </summary>
        private void InitializeLighting()
        {
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = Color.grey;
            RenderSettings.ambientEquatorColor = Color.grey * 0.8f;
            RenderSettings.ambientGroundColor = Color.grey * 0.5f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            if (sunLight != null)
            {
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowResolution = shadowResolution;
                sunLight.shadowBias = shadowBias;
                sunLight.shadowNormalBias = shadowNormalBias;
            }

            if (reflectionProbe != null)
            {
                reflectionProbe.resolution = reflectionResolution;
                reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            }
        }

        /// <summary>
        /// Sets the sun direction and elevation.
        /// </summary>
        /// <param name="direction">Sun direction in world space.</param>
        /// <param name="elevation">Sun elevation in degrees.</param>
        public void SetSunPosition(Vector3 direction, float elevation)
        {
            sunDirection = direction;
            sunElevation = elevation;

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.LookRotation(-direction);
            }
        }

        /// <summary>
        /// Sets the moon direction.
        /// </summary>
        /// <param name="direction">Moon direction in world space.</param>
        public void SetMoonPosition(Vector3 direction)
        {
            moonDirection = direction;

            if (moonLight != null)
            {
                moonLight.transform.rotation = Quaternion.LookRotation(-direction);
            }
        }

        /// <summary>
        /// Sets the weather influence on lighting.
        /// </summary>
        /// <param name="intensityModifier">Intensity modifier (0-1).</param>
        /// <param name="colorModifier">Color modifier (0-1).</param>
        public void SetWeatherInfluence(float intensityModifier, float colorModifier)
        {
            weatherIntensityModifier = Mathf.Clamp01(intensityModifier);
            weatherColorModifier = Mathf.Clamp01(colorModifier);
        }

        /// <summary>
        /// Updates the sun light based on position and weather.
        /// </summary>
        private void UpdateSunLight()
        {
            if (sunLight == null) return;

            float normalizedElevation = Mathf.InverseLerp(-18f, 90f, sunElevation);
            float targetIntensity = 0f;

            if (sunIntensityCurve != null)
            {
                targetIntensity = maxSunIntensity * sunIntensityCurve.Evaluate(normalizedElevation);
            }
            else
            {
                targetIntensity = maxSunIntensity * Mathf.Clamp01(normalizedElevation);
            }

            targetIntensity *= weatherIntensityModifier;
            targetIntensity = Mathf.Clamp(targetIntensity, minSunIntensity, maxSunIntensity);

            currentSunIntensity = Mathf.Lerp(currentSunIntensity, targetIntensity, Time.deltaTime * sunIntensitySmoothing);
            sunLight.intensity = currentSunIntensity;

            UpdateSunColor();
        }

        /// <summary>
        /// Updates the sun color based on elevation.
        /// </summary>
        private void UpdateSunColor()
        {
            if (sunColorByElevation != null && sunColorByElevation.colorKeys.Length > 0)
            {
                float t = Mathf.InverseLerp(-18f, 90f, sunElevation);
                currentSunColor = sunColorByElevation.Evaluate(t);
            }
            else
            {
                if (sunElevation > 10f)
                {
                    currentSunColor = Color.white;
                }
                else if (sunElevation > 0f)
                {
                    float t = sunElevation / 10f;
                    currentSunColor = new Color(1f, Mathf.Lerp(0.5f, 1f, t), Mathf.Lerp(0.2f, 1f, t));
                }
                else if (sunElevation > -6f)
                {
                    float t = (sunElevation + 6f) / 6f;
                    currentSunColor = new Color(1f, Mathf.Lerp(0.3f, 0.5f, t), Mathf.Lerp(0.1f, 0.2f, t));
                }
                else
                {
                    currentSunColor = new Color(0.1f, 0.1f, 0.2f);
                }
            }

            currentSunColor.r *= weatherColorModifier;
            currentSunColor.g *= weatherColorModifier;
            currentSunColor.b *= weatherColorModifier;

            sunLight.color = currentSunColor;

            if (useColorTemperature)
            {
                float temp = GetColorTemperatureForElevation(sunElevation);
                sunLight.colorTemperature = temp;
                sunLight.useColorTemperature = true;
            }
        }

        /// <summary>
        /// Gets the color temperature for a given sun elevation.
        /// </summary>
        private float GetColorTemperatureForElevation(float elevation)
        {
            if (elevation > 10f) return middayTemperature;
            if (elevation > 0f) return Mathf.Lerp(sunriseTemperature, middayTemperature, elevation / 10f);
            if (elevation > -6f) return Mathf.Lerp(sunsetTemperature, sunriseTemperature, (elevation + 6f) / 6f);
            return nightTemperature;
        }

        /// <summary>
        /// Updates the moon light based on position.
        /// </summary>
        private void UpdateMoonLight()
        {
            if (moonLight == null) return;

            float moonElevation = Mathf.Asin(Mathf.Clamp(moonDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            float targetIntensity = 0f;

            if (moonElevation > 0f && sunElevation < 0f)
            {
                float moonFactor = Mathf.Clamp01(moonElevation / 30f);
                float nightFactor = Mathf.Clamp01((-sunElevation) / 6f);
                targetIntensity = maxMoonIntensity * moonFactor * nightFactor;
            }

            currentMoonIntensity = Mathf.Lerp(currentMoonIntensity, targetIntensity, Time.deltaTime * sunIntensitySmoothing);
            moonLight.intensity = currentMoonIntensity;
            moonLight.color = moonColor;

            if (useColorTemperature)
            {
                moonLight.colorTemperature = moonTemperature;
                moonLight.useColorTemperature = true;
            }

            moonLight.shadows = moonElevation > 5f && sunElevation < -6f ? LightShadows.Soft : LightShadows.None;
            moonLight.shadowStrength = shadowStrengthNight;
        }

        /// <summary>
        /// Updates ambient lighting based on time of day.
        /// </summary>
        private void UpdateAmbientLight()
        {
            float targetAmbient = IsDaytime ? ambientIntensityDay : ambientIntensityNight;
            currentAmbientIntensity = Mathf.Lerp(currentAmbientIntensity, targetAmbient, Time.deltaTime * sunIntensitySmoothing);

            if (ambientColorByElevation != null && ambientColorByElevation.colorKeys.Length > 0)
            {
                float t = Mathf.InverseLerp(-18f, 90f, sunElevation);
                currentAmbientColor = ambientColorByElevation.Evaluate(t);
            }
            else
            {
                currentAmbientColor = Color.Lerp(new Color(0.1f, 0.1f, 0.15f), Color.grey * 0.5f, Mathf.Clamp01((sunElevation + 6f) / 24f));
            }

            RenderSettings.ambientSkyColor = currentAmbientColor;
            RenderSettings.ambientEquatorColor = currentAmbientColor * 0.8f;
            RenderSettings.ambientGroundColor = currentAmbientColor * 0.5f;
            RenderSettings.ambientIntensity = currentAmbientIntensity;

            foreach (var light in ambientLights)
            {
                if (light != null)
                {
                    light.intensity = currentAmbientIntensity;
                    light.color = currentAmbientColor;
                }
            }
        }

        /// <summary>
        /// Updates shadow settings based on sun position.
        /// </summary>
        private void UpdateShadows()
        {
            if (sunLight == null) return;

            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowCascades = shadowCascades;

            if (sunElevation > 0f)
            {
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = Mathf.Lerp(shadowStrengthNight, shadowStrengthDay, Mathf.Clamp01(sunElevation / 45f));
            }
            else if (sunElevation > -6f)
            {
                float t = (sunElevation + 6f) / 6f;
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = Mathf.Lerp(shadowStrengthNight, shadowStrengthDay, t);
            }
            else
            {
                sunLight.shadows = LightShadows.None;
            }
        }

        /// <summary>
        /// Updates the reflection probe at intervals.
        /// </summary>
        private void UpdateReflectionProbe()
        {
            if (reflectionProbe == null) return;

            reflectionTimer += Time.deltaTime;
            if (reflectionTimer >= reflectionUpdateInterval)
            {
                reflectionTimer = 0f;
                reflectionProbe.RenderProbe();
            }
        }

        /// <summary>
        /// Sets the shadow distance.
        /// </summary>
        /// <param name="distance">Shadow distance in world units.</param>
        public void SetShadowDistance(float distance)
        {
            shadowDistance = distance;
            QualitySettings.shadowDistance = distance;
        }

        /// <summary>
        /// Sets the shadow resolution.
        /// </summary>
        /// <param name="resolution">Shadow resolution quality.</param>
        public void SetShadowResolution(LightShadowResolution resolution)
        {
            shadowResolution = resolution;
            if (sunLight != null)
            {
                sunLight.shadowResolution = resolution;
            }
        }

        /// <summary>
        /// Sets the maximum sun intensity.
        /// </summary>
        /// <param name="intensity">Maximum intensity value.</param>
        public void SetMaxSunIntensity(float intensity)
        {
            maxSunIntensity = intensity;
        }

        /// <summary>
        /// Sets the maximum moon intensity.
        /// </summary>
        /// <param name="intensity">Maximum intensity value.</param>
        public void SetMaxMoonIntensity(float intensity)
        {
            maxMoonIntensity = intensity;
        }
    }
}
