using UnityEngine;
using VEVE;

namespace VEVE.VFX
{
    /// <summary>
    /// Configuration for weapon caliber-specific muzzle flash effects.
    /// </summary>
    [System.Serializable]
    public struct MuzzleFlashConfig
    {
        [Header("Visual")]
        public ParticleSystem mainFlash;
        public Light muzzleLight;
        public Color lightColor;
        public float lightIntensity;
        public float lightDuration;

        [Header("Smoke")]
        public ParticleSystem smokeParticles;
        public float smokeDuration;

        [Header("Calibration")]
        public float energyScale;
    }

    /// <summary>
    /// Dynamic muzzle flash with light emission, particle burst, and smoke.
    /// Supports different weapon calibers.
    /// </summary>
    public sealed class MuzzleFlashSystem : MonoBehaviour
    {
        [Header("Pistol Caliber (9mm, .45)")]
        [SerializeField] private MuzzleFlashConfig pistolConfig;

        [Header("Rifle Caliber (5.56, 7.62)")]
        [SerializeField] private MuzzleFlashConfig rifleConfig;

        [Header("Magnum Caliber (.338, .50)")]
        [SerializeField] private MuzzleFlashConfig magnumConfig;

        [Header("Shotgun")]
        [SerializeField] private MuzzleFlashConfig shotgunConfig;

        [Header("Behavior")]
        [SerializeField, Range(0f, 1f)] private float intensityVariation = 0.2f;

        private Light cachedLight;
        private float lightTimer;
        private float currentLightIntensity;

        private void Update()
        {
            if (lightTimer > 0f)
            {
                lightTimer -= Time.deltaTime;
                if (cachedLight != null)
                {
                    currentLightIntensity = Mathf.Lerp(currentLightIntensity, 0f, Time.deltaTime / 0.03f);
                    cachedLight.intensity = currentLightIntensity;
                }
            }
        }

        /// <summary>
        /// Fires the appropriate muzzle flash based on weapon caliber.
        /// </summary>
        /// <param name="caliber">Weapon caliber category.</param>
        /// <param name="muzzlePoint">World-space muzzle position.</param>
        /// <param name="direction">Muzzle forward direction.</param>
        /// <param name="energy">Muzzle energy for intensity scaling.</param>
        public void FireMuzzle(WeaponCaliber caliber, Vector3 muzzlePoint, Vector3 direction, float energy)
        {
            MuzzleFlashConfig config = caliber switch
            {
                WeaponCaliber.Pistol => pistolConfig,
                WeaponCaliber.Rifle => rifleConfig,
                WeaponCaliber.Magnum => magnumConfig,
                WeaponCaliber.Shotgun => shotgunConfig,
                _ => rifleConfig
            };

            float intensity = Mathf.Clamp01(energy / 1500f) * config.energyScale;
            intensity = Mathf.Clamp(intensity + Random.Range(-intensityVariation, intensityVariation), 0.1f, 1.5f);

            PlayMainFlash(muzzlePoint, direction, config, intensity);
            PlaySmoke(muzzlePoint, direction, config, intensity);
            ActivateLight(muzzlePoint, config, intensity);
        }

        private void PlayMainFlash(Vector3 point, Vector3 direction, MuzzleFlashConfig config, float intensity)
        {
            if (config.mainFlash == null) return;

            ParticleSystem flash = Instantiate(config.mainFlash, point, Quaternion.LookRotation(direction));
            var main = flash.main;
            main.startSizeMultiplier = intensity;
            flash.Play();

            Destroy(flash.gameObject, config.lightDuration + 0.05f);
        }

        private void PlaySmoke(Vector3 point, Vector3 direction, MuzzleFlashConfig config, float intensity)
        {
            if (config.smokeParticles == null) return;

            ParticleSystem smoke = Instantiate(config.smokeParticles, point, Quaternion.LookRotation(direction));
            var main = smoke.main;
            main.startSizeMultiplier = intensity * 0.8f;
            smoke.Play();

            Destroy(smoke.gameObject, config.smokeDuration);
        }

        private void ActivateLight(Vector3 point, MuzzleFlashConfig config, float intensity)
        {
            if (config.muzzleLight == null) return;

            cachedLight = Instantiate(config.muzzleLight, point, Quaternion.identity);
            currentLightIntensity = config.lightIntensity * intensity;
            cachedLight.intensity = currentLightIntensity;
            lightTimer = config.lightDuration;

            Destroy(cachedLight.gameObject, config.lightDuration + 0.1f);
        }
    }

    /// <summary>
    /// Weapon caliber categories for muzzle flash calibration.
    /// </summary>
    public enum WeaponCaliber
    {
        Pistol,
        Rifle,
        Magnum,
        Shotgun
    }
}
