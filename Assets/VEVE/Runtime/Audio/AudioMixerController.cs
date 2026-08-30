using UnityEngine;
using UnityEngine.Audio;

namespace VEVE.Audio
{
    /// <summary>
    /// Controls the audio mixer with real-time parameter automation for occlusion,
    /// Doppler, and environmental effects.
    /// </summary>
    public sealed class AudioMixerController : MonoBehaviour
    {
        [System.Serializable]
        public class MixerParameters
        {
            [Header("Occlusion")]
            [SerializeField] private string occlusionParameter = "OcclusionAmount";
            [SerializeField] private float occlusionSmoothTime = 0.1f;
            [SerializeField] private float occlusionMax = 0.25f;

            [Header("Doppler")]
            [SerializeField] private string dopplerParameter = "DopplerIntensity";
            [SerializeField] private float dopplerSmoothTime = 0.05f;
            [SerializeField] private float dopplerMax = 2f;

            [Header("Environment")]
            [SerializeField] private string environmentParameter = "EnvironmentDampening";
            [SerializeField] private float environmentSmoothTime = 0.2f;
            [SerializeField] private float environmentMax = 0.5f;

            [Header("Snapshot")]
            [SerializeField] private string snapshotParameter = "SnapshotBlend";
            [SerializeField] private float snapshotTransitionDuration = 1f;

            public string OcclusionParameter { get { return occlusionParameter; } }
            public float OcclusionSmoothTime { get { return occlusionSmoothTime; } }
            public float OcclusionMax { get { return occlusionMax; } }
            public string DopplerParameter { get { return dopplerParameter; } }
            public float DopplerSmoothTime { get { return dopplerSmoothTime; } }
            public float DopplerMax { get { return dopplerMax; } }
            public string EnvironmentParameter { get { return environmentParameter; } }
            public float EnvironmentSmoothTime { get { return environmentSmoothTime; } }
            public float EnvironmentMax { get { return environmentMax; } }
            public string SnapshotParameter { get { return snapshotParameter; } }
            public float SnapshotTransitionDuration { get { return snapshotTransitionDuration; } }
        }

        [System.Serializable]
        public class SnapshotProfile
        {
            [SerializeField] private AudioMixerSnapshot snapshot;
            [SerializeField] private float blendValue = 0f;

            public AudioMixerSnapshot Snapshot => snapshot;
            public float BlendValue { get { return blendValue; } set { blendValue = value; } }
        }

        [Header("Mixer Groups")]
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup voiceGroup;

        [Header("Configuration")]
        [SerializeField] private AudioPresetDefinition defaultPreset;
        [SerializeField] private MixerParameters parameters;
        [SerializeField] private SnapshotProfile[] snapshotProfiles;

        private AudioPresetDefinition currentPreset;
        private float occlusionSmoothVelocity;
        private float dopplerSmoothVelocity;
        private float environmentSmoothVelocity;
        private float snapshotSmoothVelocity;
        private float currentOcclusion;
        private float currentDoppler;
        private float currentEnvironment;
        private float currentSnapshot;

        private void Start()
        {
            if (defaultPreset != null)
            {
                ApplyPreset(defaultPreset);
            }
        }

        private void Update()
        {
            if (masterGroup == null || masterGroup.audioMixer == null) return;
            AudioMixer mixer = masterGroup.audioMixer;

            currentOcclusion = Mathf.SmoothDamp(currentOcclusion, GetTargetOcclusion(), ref occlusionSmoothVelocity, parameters.OcclusionSmoothTime);
            currentDoppler = Mathf.SmoothDamp(currentDoppler, GetTargetDoppler(), ref dopplerSmoothVelocity, parameters.DopplerSmoothTime);
            currentEnvironment = Mathf.SmoothDamp(currentEnvironment, GetTargetEnvironment(), ref environmentSmoothVelocity, parameters.EnvironmentSmoothTime);
            currentSnapshot = Mathf.SmoothDamp(currentSnapshot, GetTargetSnapshot(), ref snapshotSmoothVelocity, parameters.SnapshotTransitionDuration);

            mixer.SetFloat(parameters.OcclusionParameter, currentOcclusion * parameters.OcclusionMax);
            mixer.SetFloat(parameters.DopplerParameter, currentDoppler * parameters.DopplerMax);
            mixer.SetFloat(parameters.EnvironmentParameter, currentEnvironment * parameters.EnvironmentMax);
            mixer.SetFloat(parameters.SnapshotParameter, currentSnapshot);
        }

        public void ApplyPreset(AudioPresetDefinition preset)
        {
            if (preset == null) return;
            currentPreset = preset;

            if (masterGroup != null && masterGroup.audioMixer != null)
            {
                masterGroup.audioMixer.SetFloat("MasterVolume", DecibelToLinear(preset.MasterVolume));
            }
            if (sfxGroup != null && sfxGroup.audioMixer != null)
            {
                sfxGroup.audioMixer.SetFloat("SFXVolume", DecibelToLinear(preset.SFXVolume));
            }
            if (musicGroup != null && musicGroup.audioMixer != null)
            {
                musicGroup.audioMixer.SetFloat("MusicVolume", DecibelToLinear(preset.MusicVolume));
            }
            if (voiceGroup != null && voiceGroup.audioMixer != null)
            {
                voiceGroup.audioMixer.SetFloat("VoiceVolume", DecibelToLinear(preset.VoiceVolume));
            }
        }

        public void TransitionToSnapshot(AudioMixerSnapshot snapshot, float duration)
        {
            if (snapshot == null) return;
            snapshot.TransitionTo(duration);
        }

        public void SetOcclusion(float occlusionFactor)
        {
            currentOcclusion = occlusionFactor;
        }

        public void SetDoppler(float dopplerFactor)
        {
            currentDoppler = dopplerFactor;
        }

        public void SetEnvironment(float environmentFactor)
        {
            currentEnvironment = environmentFactor;
        }

        public void SetReverbWet(float wetFactor)
        {
            if (masterGroup != null && masterGroup.audioMixer != null)
            {
                masterGroup.audioMixer.SetFloat("ReverbWet", Mathf.Clamp01(wetFactor));
            }
        }

        public void DuckMusic(float duckAmount, float duration)
        {
            if (musicGroup != null && musicGroup.audioMixer != null)
            {
                musicGroup.audioMixer.SetFloat("MusicDuck", Mathf.Clamp01(duckAmount));
            }
        }

        public AudioPresetDefinition GetCurrentPreset()
        {
            return currentPreset;
        }

        private float GetTargetOcclusion()
        {
            return Mathf.Clamp01(currentOcclusion);
        }

        private float GetTargetDoppler()
        {
            return Mathf.Clamp01(currentDoppler);
        }

        private float GetTargetEnvironment()
        {
            return Mathf.Clamp01(currentEnvironment);
        }

        private float GetTargetSnapshot()
        {
            return Mathf.Clamp01(currentSnapshot);
        }

        private float DecibelToLinear(float db)
        {
            return Mathf.Clamp01(Mathf.Pow(10f, db / 20f));
        }
    }
}
