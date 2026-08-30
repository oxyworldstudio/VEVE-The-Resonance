using UnityEngine;
using VEVE.Realism;

namespace VEVE.Audio
{
    /// <summary>
    /// Runtime-generated audio for footsteps, weapon handling, and environmental sounds
    /// based on material and movement parameters.
    /// </summary>
    public sealed class ProceduralAudio : MonoBehaviour
    {
        [System.Serializable]
        public class FootstepConfig
        {
            [Header("Material")]
            [SerializeField] private SurfaceMaterial surfaceMaterial = SurfaceMaterial.Concrete;
            [SerializeField] private float baseVolume = 1f;
            [SerializeField] private float lowFrequencyGain = 0.5f;
            [SerializeField] private float highFrequencyGain = 0.3f;
            [SerializeField] private float attackTime = 0.01f;
            [SerializeField] private float decayTime = 0.1f;

            public SurfaceMaterial SurfaceMaterial => surfaceMaterial;
            public float BaseVolume => baseVolume;
            public float LowFrequencyGain => lowFrequencyGain;
            public float HighFrequencyGain => highFrequencyGain;
            public float AttackTime => attackTime;
            public float DecayTime => decayTime;
        }

        [System.Serializable]
        public class WeaponHandlingConfig
        {
            [Header("Movement")]
            [SerializeField] private float baseVolume = 0.6f;
            [SerializeField] private float speedVolumeMultiplier = 0.3f;
            [SerializeField] private float pitchMin = 0.8f;
            [SerializeField] private float pitchMax = 1.2f;

            public float BaseVolume => baseVolume;
            public float SpeedVolumeMultiplier => speedVolumeMultiplier;
            public float PitchMin => pitchMin;
            public float PitchMax => pitchMax;
        }

        [System.Serializable]
        public class EnvironmentalConfig
        {
            [Header("Wind")]
            [SerializeField] private float windBaseVolume = 0.2f;
            [SerializeField] private float windPitchMin = 0.5f;
            [SerializeField] private float windPitchMax = 1.5f;
            [SerializeField] private float gustIntensity = 0.3f;

            [Header("Rain")]
            [SerializeField] private float rainBaseVolume = 0.3f;
            [SerializeField] private float rainPitch = 1f;

            public float WindBaseVolume => windBaseVolume;
            public float WindPitchMin => windPitchMin;
            public float WindPitchMax => windPitchMax;
            public float GustIntensity => gustIntensity;
            public float RainBaseVolume => rainBaseVolume;
            public float RainPitch => rainPitch;
        }

        [Header("References")]
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioSource weaponSource;
        [SerializeField] private AudioSource environmentalSource;

        [Header("Configuration")]
        [SerializeField] private FootstepConfig footstepConfig = new FootstepConfig();
        [SerializeField] private WeaponHandlingConfig weaponConfig = new WeaponHandlingConfig();
        [SerializeField] private EnvironmentalConfig environmentalConfig = new EnvironmentalConfig();

        private float footstepTimer;
        private float lastFootstepVolume;
        private float windNoisePhase;

        private void Update()
        {
            UpdateWindNoise();
        }

        public void PlayFootstep(float movementSpeed, float crouchMultiplier = 0.5f)
        {
            if (footstepSource == null) return;

            float speedFactor = Mathf.Clamp01(movementSpeed * crouchMultiplier);
            float volume = footstepConfig.BaseVolume * speedFactor;
            float pitch = 0.8f + speedFactor * 0.4f;

            footstepSource.volume = Mathf.Lerp(lastFootstepVolume, volume, 0.5f);
            footstepSource.pitch = pitch;
            footstepSource.Play();
            lastFootstepVolume = volume;
        }

        public void PlayWeaponHandling(float movementSpeed)
        {
            if (weaponSource == null) return;

            float volume = weaponConfig.BaseVolume + movementSpeed * weaponConfig.SpeedVolumeMultiplier;
            float pitch = Random.Range(weaponConfig.PitchMin, weaponConfig.PitchMax);

            weaponSource.volume = Mathf.Clamp01(volume);
            weaponSource.pitch = pitch;
            weaponSource.Play();
        }

        public void UpdateWindNoise()
        {
            if (environmentalSource == null) return;

            windNoisePhase += Time.deltaTime;
            float gust = Mathf.PerlinNoise(windNoisePhase * 0.5f, 0f) * environmentalConfig.GustIntensity;
            float volume = environmentalConfig.WindBaseVolume + gust;
            float pitch = Mathf.Lerp(environmentalConfig.WindPitchMin, environmentalConfig.WindPitchMax, gust);

            environmentalSource.volume = Mathf.Clamp01(volume);
            environmentalSource.pitch = pitch;
        }

        public void PlayRainSound()
        {
            if (environmentalSource == null) return;

            environmentalSource.volume = environmentalConfig.RainBaseVolume;
            environmentalSource.pitch = environmentalConfig.RainPitch;
            environmentalSource.Play();
        }

        public FootstepConfig GetFootstepConfig()
        {
            return footstepConfig;
        }

        public WeaponHandlingConfig GetWeaponConfig()
        {
            return weaponConfig;
        }

        public EnvironmentalConfig GetEnvironmentalConfig()
        {
            return environmentalConfig;
        }
    }
}
