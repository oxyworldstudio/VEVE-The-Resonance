using UnityEngine;
using VEVE;

namespace VEVE.Audio
{
    /// <summary>
    /// Raycast-based audio occlusion system that performs periodic line-of-sight checks
    /// from audio sources to the listener, calculates obstruction based on hit materials,
    /// and applies volume/DSP effects. Excludes SmokeVolume from occlusion.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioOcclusion : MonoBehaviour
    {
        [System.Serializable]
        public class OcclusionProfile
        {
            [Header("Occlusion")]
            [SerializeField, Range(0f, 1f)] private float occludedVolume = 0.25f;
            [SerializeField, Min(0.02f)] private float queryInterval = 0.1f;
            [SerializeField] private float lerpSpeed = 8f;
            [SerializeField] private float lowpassOccluded = 800f;
            [SerializeField] private float lowpassClear = 22000f;
            [SerializeField] private float highpassOccluded = 20f;
            [SerializeField] private float highpassClear = 20f;

            [Header("DSP Blending")]
            [SerializeField] private float reverbWetOccluded = 0.8f;
            [SerializeField] private float reverbWetClear = 0.2f;
            [SerializeField] private float distortionOccluded = 0.3f;
            [SerializeField] private float distortionClear = 0f;
            [SerializeField] private float chorusOccluded = 0.4f;
            [SerializeField] private float chorusClear = 0f;

            public float OccludedVolume { get { return occludedVolume; } }
            public float QueryInterval { get { return queryInterval; } }
            public float LerpSpeed { get { return lerpSpeed; } }
            public float LowpassOccluded { get { return lowpassOccluded; } }
            public float LowpassClear { get { return lowpassClear; } }
            public float HighpassOccluded { get { return highpassOccluded; } }
            public float HighpassClear { get { return highpassClear; } }
            public float ReverbWetOccluded { get { return reverbWetOccluded; } }
            public float ReverbWetClear { get { return reverbWetClear; } }
            public float DistortionOccluded { get { return distortionOccluded; } }
            public float DistortionClear { get { return distortionClear; } }
            public float ChorusOccluded { get { return chorusOccluded; } }
            public float ChorusClear { get { return chorusClear; } }
        }

        [Header("References")]
        [SerializeField] private Transform listener;
        [SerializeField] private AudioMixerController mixerController;
        [SerializeField] private OcclusionProfile profile;

        private AudioSource source;
        private AudioLowPassFilter lowpassFilter;
        private AudioHighPassFilter highpassFilter;
        private AudioDistortionFilter distortionFilter;
        private AudioChorusFilter chorusFilter;
        private float nextQuery;
        private float targetOcclusion;
        private bool wasOccluded;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            lowpassFilter = GetComponent<AudioLowPassFilter>();
            if (lowpassFilter == null)
            {
                lowpassFilter = gameObject.AddComponent<AudioLowPassFilter>();
            }
            highpassFilter = GetComponent<AudioHighPassFilter>();
            if (highpassFilter == null)
            {
                highpassFilter = gameObject.AddComponent<AudioHighPassFilter>();
            }
            distortionFilter = GetComponent<AudioDistortionFilter>();
            if (distortionFilter == null)
            {
                distortionFilter = gameObject.AddComponent<AudioDistortionFilter>();
            }
            chorusFilter = GetComponent<AudioChorusFilter>();
            if (chorusFilter == null)
            {
                chorusFilter = gameObject.AddComponent<AudioChorusFilter>();
            }
        }

        private void Update()
        {
            if (listener == null || source == null) return;
            if (Time.unscaledTime < nextQuery) return;
            nextQuery = Time.unscaledTime + profile.QueryInterval;

            bool blocked = Physics.Linecast(transform.position, listener.position, out RaycastHit hit) &&
                hit.transform != listener &&
                hit.transform.GetComponent<SmokeVolume>() == null;

            if (blocked != wasOccluded)
            {
                wasOccluded = blocked;
                if (blocked)
                {
                    float absorption = GetMaterialAbsorption(hit);
                    targetOcclusion = 1f - absorption;
                }
                else
                {
                    targetOcclusion = 0f;
                }
            }

            float targetVolume = blocked ? profile.OccludedVolume : 1f;
            float targetLowpass = blocked ? profile.LowpassOccluded : profile.LowpassClear;
            float targetHighpass = blocked ? profile.HighpassOccluded : profile.HighpassClear;
            float targetDistortion = blocked ? profile.DistortionOccluded : profile.DistortionClear;
            float targetChorus = blocked ? profile.ChorusOccluded : profile.ChorusClear;

            source.volume = Mathf.MoveTowards(source.volume, targetVolume, profile.LerpSpeed * Time.unscaledDeltaTime);
            lowpassFilter.cutoffFrequency = Mathf.MoveTowards(lowpassFilter.cutoffFrequency, targetLowpass, profile.LerpSpeed * Time.unscaledDeltaTime);
            highpassFilter.cutoffFrequency = Mathf.MoveTowards(highpassFilter.cutoffFrequency, targetHighpass, profile.LerpSpeed * Time.unscaledDeltaTime);
            distortionFilter.distortionLevel = Mathf.MoveTowards(distortionFilter.distortionLevel, targetDistortion, profile.LerpSpeed * Time.unscaledDeltaTime);
            chorusFilter.dryMix = Mathf.MoveTowards(chorusFilter.dryMix, 1f - targetChorus, profile.LerpSpeed * Time.unscaledDeltaTime);
            chorusFilter.wetMix1 = Mathf.MoveTowards(chorusFilter.wetMix1, targetChorus * 0.5f, profile.LerpSpeed * Time.unscaledDeltaTime);
            chorusFilter.wetMix2 = Mathf.MoveTowards(chorusFilter.wetMix2, targetChorus * 0.5f, profile.LerpSpeed * Time.unscaledDeltaTime);

            mixerController?.SetOcclusion(targetOcclusion);
            mixerController?.SetReverbWet(blocked ? profile.ReverbWetOccluded : profile.ReverbWetClear);
        }

        private float GetMaterialAbsorption(RaycastHit hit)
        {
            var materialDef = hit.transform.GetComponent<MaterialDefinition>();
            if (materialDef != null)
            {
                return materialDef.AcousticAbsorption;
            }

            var renderer = hit.transform.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                if (renderer.sharedMaterial.name.Contains("Wood")) return 0.4f;
                if (renderer.sharedMaterial.name.Contains("Concrete")) return 0.15f;
                if (renderer.sharedMaterial.name.Contains("Metal")) return 0.1f;
                if (renderer.sharedMaterial.name.Contains("Glass")) return 0.05f;
                if (renderer.sharedMaterial.name.Contains("Fabric")) return 0.7f;
                if (renderer.sharedMaterial.name.Contains("Dirt")) return 0.5f;
                if (renderer.sharedMaterial.name.Contains("Ice")) return 0.03f;
            }

            return 0.5f;
        }
    }
}
