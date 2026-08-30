using UnityEngine;
using System;

namespace VEVE
{
    /// <summary>
    /// Controls the dynamic skybox with sun, moon, stars, atmospheric scattering, and horizon rendering.
    /// </summary>
    public sealed class SkyboxController : MonoBehaviour
    {
        [Header("Skybox Material")]
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private Material customSkyboxMaterial;

        [Header("Sun Configuration")]
        [SerializeField] private Light sunLight;
        [SerializeField] private float sunSize = 0.04f;
        [SerializeField] private float sunSizeConvergence = 5f;
        [SerializeField] private Color sunColor = Color.white;
        [SerializeField] private float sunAtmosphereTint = 0.5f;

        [Header("Moon Configuration")]
        [SerializeField] private Light moonLight;
        [SerializeField] private float moonSize = 0.02f;
        [SerializeField] private Color moonColor = new Color(0.8f, 0.85f, 1f);
        [SerializeField] private Texture2D moonAlbedo;
        [SerializeField] private Texture2D moonNormalMap;

        [Header("Star Field")]
        [SerializeField] private Texture2D starTexture;
        [SerializeField] private float starIntensity = 1f;
        [SerializeField] private float starTwinkleSpeed = 1f;
        [SerializeField] private float starTwinkleAmount = 0.3f;
        [SerializeField] private Color starTint = Color.white;

        [Header("Atmospheric Scattering")]
        [SerializeField] private float rayleighScattering = 1.0f;
        [SerializeField] private float mieScattering = 1.0f;
        [SerializeField] private float mieDirectionalG = 0.8f;
        [SerializeField] private float turbidity = 2.0f;
        [SerializeField] private Color groundColor = new Color(0.34f, 0.34f, 0.34f);
        [SerializeField] private float exposure = 1.5f;

        [Header("Horizon")]
        [SerializeField] private float horizonBlendStart = 0.0f;
        [SerializeField] private float horizonBlendEnd = 0.3f;
        [SerializeField] private float horizonBlendPower = 1.0f;
        [SerializeField] private Color horizonColor = new Color(0.6f, 0.7f, 0.8f);

        [Header("Zenith")]
        [SerializeField] private float zenithBlendStart = 0.0f;
        [SerializeField] private float zenithBlendEnd = 0.15f;
        [SerializeField] private float zenithBlendPower = 0.5f;
        [SerializeField] private Color zenithColor = new Color(0.1f, 0.2f, 0.5f);

        [Header("Animation")]
        [SerializeField] private float rotationSpeed = 0f;
        [SerializeField] private bool rotateSkybox = false;

        private float currentRotation;
        private float timeOfDay;
        private Vector3 sunDirection;
        private Vector3 moonDirection;
        private float sunElevation;

        /// <summary>
        /// Gets or sets the sun direction for skybox calculations.
        /// </summary>
        public Vector3 SunDirection
        {
            get => sunDirection;
            set
            {
                sunDirection = value;
                sunElevation = Mathf.Asin(Mathf.Clamp(value.y, -1f, 1f)) * Mathf.Rad2Deg;
                UpdateSkyboxParameters();
            }
        }

        /// <summary>
        /// Gets or sets the moon direction for skybox calculations.
        /// </summary>
        public Vector3 MoonDirection
        {
            get => moonDirection;
            set
            {
                moonDirection = value;
                UpdateMoonParameters();
            }
        }

        /// <summary>
        /// Gets or sets the star field intensity.
        /// </summary>
        public float StarIntensity
        {
            get => starIntensity;
            set => starIntensity = value;
        }

        /// <summary>
        /// Gets or sets the atmospheric exposure.
        /// </summary>
        public float Exposure
        {
            get => exposure;
            set => exposure = value;
        }

        private void Start()
        {
            InitializeSkybox();
        }

        private void Update()
        {
            if (rotateSkybox)
            {
                currentRotation += rotationSpeed * Time.deltaTime;
                if (skyboxMaterial != null)
                {
                    skyboxMaterial.SetFloat("_Rotation", currentRotation);
                }
            }

            UpdateStarTwinkle();
            UpdateAtmosphericScattering();
        }

        /// <summary>
        /// Initializes the skybox material and shader parameters.
        /// </summary>
        private void InitializeSkybox()
        {
            if (customSkyboxMaterial != null)
            {
                skyboxMaterial = customSkyboxMaterial;
                RenderSettings.skybox = skyboxMaterial;
            }

            if (skyboxMaterial == null) return;

            skyboxMaterial.SetFloat("_SunSize", sunSize);
            skyboxMaterial.SetFloat("_SunSizeConvergence", sunSizeConvergence);
            skyboxMaterial.SetColor("_SunColor", sunColor);
            skyboxMaterial.SetFloat("_SunAtmosphereTint", sunAtmosphereTint);

            skyboxMaterial.SetFloat("_MoonSize", moonSize);
            skyboxMaterial.SetColor("_MoonColor", moonColor);
            if (moonAlbedo != null) skyboxMaterial.SetTexture("_MoonAlbedo", moonAlbedo);
            if (moonNormalMap != null) skyboxMaterial.SetTexture("_MoonNormalMap", moonNormalMap);

            skyboxMaterial.SetColor("_GroundColor", groundColor);
            skyboxMaterial.SetFloat("_Exposure", exposure);

            skyboxMaterial.SetColor("_HorizonColor", horizonColor);
            skyboxMaterial.SetFloat("_HorizonBlendStart", horizonBlendStart);
            skyboxMaterial.SetFloat("_HorizonBlendEnd", horizonBlendEnd);
            skyboxMaterial.SetFloat("_HorizonBlendPower", horizonBlendPower);

            skyboxMaterial.SetColor("_ZenithColor", zenithColor);
            skyboxMaterial.SetFloat("_ZenithBlendStart", zenithBlendStart);
            skyboxMaterial.SetFloat("_ZenithBlendEnd", zenithBlendEnd);
            skyboxMaterial.SetFloat("_ZenithBlendPower", zenithBlendPower);

            if (starTexture != null) skyboxMaterial.SetTexture("_StarTexture", starTexture);
            skyboxMaterial.SetColor("_StarTint", starTint);
        }

        /// <summary>
        /// Updates skybox parameters based on sun position.
        /// </summary>
        private void UpdateSkyboxParameters()
        {
            if (skyboxMaterial == null) return;

            skyboxMaterial.SetVector("_SunDirection", sunDirection);

            float sunHeight = Mathf.Clamp(sunDirection.y, -1f, 1f);
            skyboxMaterial.SetFloat("_SunHeight", sunHeight);

            UpdateSkyColors();
        }

        /// <summary>
        /// Updates sky colors based on sun elevation.
        /// </summary>
        private void UpdateSkyColors()
        {
            if (skyboxMaterial == null) return;

            Color currentHorizon = horizonColor;
            Color currentZenith = zenithColor;

            if (sunElevation > 10f)
            {
                currentHorizon = Color.Lerp(horizonColor, new Color(0.7f, 0.8f, 0.9f), Mathf.Clamp01((sunElevation - 10f) / 30f));
                currentZenith = Color.Lerp(zenithColor, new Color(0.2f, 0.4f, 0.8f), Mathf.Clamp01((sunElevation - 10f) / 30f));
            }
            else if (sunElevation > 0f)
            {
                float t = sunElevation / 10f;
                currentHorizon = Color.Lerp(new Color(1f, 0.5f, 0.2f), horizonColor, t);
                currentZenith = Color.Lerp(new Color(0.1f, 0.1f, 0.3f), zenithColor, t);
            }
            else if (sunElevation > -6f)
            {
                float t = (sunElevation + 6f) / 6f;
                currentHorizon = Color.Lerp(new Color(0.1f, 0.05f, 0.1f), new Color(1f, 0.5f, 0.2f), t);
                currentZenith = Color.Lerp(new Color(0.02f, 0.02f, 0.05f), new Color(0.1f, 0.1f, 0.3f), t);
            }
            else
            {
                currentHorizon = new Color(0.02f, 0.02f, 0.05f);
                currentZenith = new Color(0.01f, 0.01f, 0.02f);
            }

            skyboxMaterial.SetColor("_HorizonColor", currentHorizon);
            skyboxMaterial.SetColor("_ZenithColor", currentZenith);
        }

        /// <summary>
        /// Updates moon parameters in the skybox.
        /// </summary>
        private void UpdateMoonParameters()
        {
            if (skyboxMaterial == null) return;

            skyboxMaterial.SetVector("_MoonDirection", moonDirection);
            skyboxMaterial.SetFloat("_MoonHeight", Mathf.Clamp(moonDirection.y, -1f, 1f));
        }

        /// <summary>
        /// Updates star twinkle animation.
        /// </summary>
        private void UpdateStarTwinkle()
        {
            if (skyboxMaterial == null) return;

            float twinkle = Mathf.Sin(Time.time * starTwinkleSpeed) * starTwinkleAmount;
            float currentStarIntensity = starIntensity * (1f + twinkle);
            skyboxMaterial.SetFloat("_StarIntensity", currentStarIntensity);

            float skyDarkness = 1f - Mathf.Clamp01((sunElevation + 6f) / 6f);
            skyboxMaterial.SetFloat("_SkyDarkness", skyDarkness);
        }

        /// <summary>
        /// Updates atmospheric scattering parameters.
        /// </summary>
        private void UpdateAtmosphericScattering()
        {
            if (skyboxMaterial == null) return;

            float sunFactor = Mathf.Clamp01(Mathf.Sin(sunElevation * Mathf.Deg2Rad));
            float rayleigh = rayleighScattering * (1f + turbidity * 0.1f);
            float mie = mieScattering * turbidity;

            skyboxMaterial.SetFloat("_RayleighScattering", rayleigh);
            skyboxMaterial.SetFloat("_MieScattering", mie);
            skyboxMaterial.SetFloat("_MieDirectionalG", mieDirectionalG);
            skyboxMaterial.SetFloat("_Turbidity", turbidity);
            skyboxMaterial.SetFloat("_SunFactor", sunFactor);
        }

        /// <summary>
        /// Sets the skybox material.
        /// </summary>
        /// <param name="material">The new skybox material.</param>
        public void SetSkyboxMaterial(Material material)
        {
            skyboxMaterial = material;
            RenderSettings.skybox = material;
            InitializeSkybox();
        }

        /// <summary>
        /// Sets the sun direction and updates the skybox.
        /// </summary>
        /// <param name="direction">Sun direction in world space.</param>
        public void SetSunDirection(Vector3 direction)
        {
            SunDirection = direction.normalized;
        }

        /// <summary>
        /// Sets the moon direction and updates the skybox.
        /// </summary>
        /// <param name="direction">Moon direction in world space.</param>
        public void SetMoonDirection(Vector3 direction)
        {
            MoonDirection = direction.normalized;
        }

        /// <summary>
        /// Sets the atmospheric exposure.
        /// </summary>
        /// <param name="newExposure">Exposure value.</param>
        public void SetExposure(float newExposure)
        {
            exposure = newExposure;
            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetFloat("_Exposure", exposure);
            }
        }

        /// <summary>
        /// Sets the turbidity for atmospheric scattering.
        /// </summary>
        /// <param name="newTurbidity">Turbidity value.</param>
        public void SetTurbidity(float newTurbidity)
        {
            turbidity = newTurbidity;
        }

        /// <summary>
        /// Sets the star field texture.
        /// </summary>
        /// <param name="texture">Star texture.</param>
        public void SetStarTexture(Texture2D texture)
        {
            starTexture = texture;
            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetTexture("_StarTexture", starTexture);
            }
        }
    }
}
