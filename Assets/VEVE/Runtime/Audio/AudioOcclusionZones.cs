using UnityEngine;
using UnityEngine.Audio;

namespace VEVE.Audio
{
    /// <summary>
    /// Defines reverb zones and audio environment presets with support for
    /// interior/exterior transitions and automatic reverb blending.
    /// </summary>
    [System.Serializable]
    public class AudioEnvironmentPreset
    {
        [Header("Reverb")]
        [SerializeField] private AudioReverbPreset reverbPreset = AudioReverbPreset.User;
        [SerializeField, Range(-80f, 20f)] private float reverbLevel = 0f;
        [SerializeField, Range(0.1f, 20f)] private float reverbDecayTime = 1f;
        [SerializeField, Range(0.1f, 2f)] private float reverbDecayHFRatio = 0.5f;
        [SerializeField, Range(-100f, 20f)] private float reflectionsLevel = 0f;
        [SerializeField, Range(0f, 0.3f)] private float reflectionsDelay = 0.02f;
        [SerializeField, Range(-100f, 20f)] private float reverbLevelLate = 0f;
        [SerializeField, Range(0f, 0.1f)] private float reverbLateDelay = 0.04f;
        [SerializeField, Range(0f, 20000f)] private float hfReference = 5000f;
        [SerializeField, Range(20f, 20000f)] private float lfReference = 250f;

        [Header("Environment")]
        [SerializeField] private float wetLevel = 0.5f;
        [SerializeField] private float dryLevel = 0.5f;
        [SerializeField, Range(0f, 100f)] private float roomSize = 10f;
        [SerializeField, Range(0f, 100f)] private float roomHF = 50f;
        [SerializeField] private float roomLF = 50f;
        [SerializeField, Range(0.1f, 20f)] private float decayTime = 1f;
        [SerializeField, Range(0f, 100f)] private float density = 50f;
        [SerializeField, Range(0f, 100f)] private float diffusion = 50f;

        public AudioReverbPreset ReverbPreset { get { return reverbPreset; } set { reverbPreset = value; } }
        public float ReverbLevel { get { return reverbLevel; } set { reverbLevel = value; } }
        public float ReverbDecayTime { get { return reverbDecayTime; } set { reverbDecayTime = value; } }
        public float ReverbDecayHFRatio { get { return reverbDecayHFRatio; } set { reverbDecayHFRatio = value; } }
        public float ReflectionsLevel { get { return reflectionsLevel; } set { reflectionsLevel = value; } }
        public float ReflectionsDelay { get { return reflectionsDelay; } set { reflectionsDelay = value; } }
        public float ReverbLevelLate { get { return reverbLevelLate; } set { reverbLevelLate = value; } }
        public float ReverbLateDelay { get { return reverbLateDelay; } set { reverbLateDelay = value; } }
        public float HFReference { get { return hfReference; } set { hfReference = value; } }
        public float LFReference { get { return lfReference; } set { lfReference = value; } }
        public float WetLevel { get { return wetLevel; } set { wetLevel = value; } }
        public float DryLevel { get { return dryLevel; } set { dryLevel = value; } }
        public float RoomSize { get { return roomSize; } set { roomSize = value; } }
        public float RoomHF { get { return roomHF; } set { roomHF = value; } }
        public float RoomLF { get { return roomLF; } set { roomLF = value; } }
        public float DecayTime { get { return decayTime; } set { decayTime = value; } }
        public float Density { get { return density; } set { density = value; } }
        public float Diffusion { get { return diffusion; } set { diffusion = value; } }

        public void Apply(AudioReverbZone zone)
        {
            if (zone == null) return;
            zone.reverbPreset = AudioReverbPreset.User;
            zone.room = Mathf.RoundToInt(roomSize * 80f - 80f);
            zone.roomHF = Mathf.RoundToInt(roomHF * 100f - 100f);
            zone.roomLF = Mathf.RoundToInt(roomLF * 100f - 100f);
            zone.decayTime = decayTime;
            zone.decayHFRatio = reverbDecayHFRatio;
            zone.reverbDelay = reverbLateDelay;
            zone.diffusion = diffusion;
            zone.density = density;
        }

        public void ApplyToMixer(AudioMixer mixer, string exposedReverbParam)
        {
            if (mixer == null) return;
            mixer.SetFloat(exposedReverbParam, Mathf.Lerp(-80f, 0f, wetLevel));
        }
    }

    /// <summary>
    /// Reverb zone trigger that automatically blends between interior and exterior
    /// audio environments based on player position.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AudioOcclusionZones : MonoBehaviour
    {
        [System.Serializable]
        public class EnvironmentTransition
        {
            [SerializeField] private AudioEnvironmentPreset interior;
            [SerializeField] private AudioEnvironmentPreset exterior;
            [SerializeField] private float blendDistance = 2f;
            [SerializeField] private AudioMixerSnapshot interiorSnapshot;
            [SerializeField] private AudioMixerSnapshot exteriorSnapshot;

            public AudioEnvironmentPreset Interior => interior;
            public AudioEnvironmentPreset Exterior => exterior;
            public float BlendDistance => blendDistance;
            public AudioMixerSnapshot InteriorSnapshot => interiorSnapshot;
            public AudioMixerSnapshot ExteriorSnapshot => exteriorSnapshot;
        }

        [Header("Zones")]
        [SerializeField] private AudioEnvironmentPreset defaultPreset;
        [SerializeField] private EnvironmentTransition[] transitions;
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private Transform listener;
        [SerializeField] private string reverbMixerParameter = "ReverbLevel";

        [Header("Blending")]
        [SerializeField] private float blendSpeed = 2f;

        private AudioEnvironmentPreset currentPreset;
        private AudioEnvironmentPreset targetPreset;
        private float blendProgress = 1f;
        private bool isInterior;

        private void Start()
        {
            if (defaultPreset != null)
            {
                currentPreset = defaultPreset;
                targetPreset = defaultPreset;
            }
        }

        private void Update()
        {
            if (listener == null) return;

            if (blendProgress < 1f)
            {
                blendProgress = Mathf.MoveTowards(blendProgress, 1f, blendSpeed * Time.unscaledDeltaTime);
                ApplyBlendedPreset();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.transform != listener) return;
            EvaluateZoneTransitions();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform != listener) return;
            EvaluateZoneTransitions();
        }

        private void EvaluateZoneTransitions()
        {
            foreach (var transition in transitions)
            {
                bool inside = IsInsideZone(transition);
                if (inside != isInterior)
                {
                    isInterior = inside;
                    targetPreset = isInterior ? transition.Interior : transition.Exterior;
                    blendProgress = 0f;

                    if (isInterior && transition.InteriorSnapshot != null)
                    {
                        transition.InteriorSnapshot.TransitionTo(blendProgress);
                    }
                    if (!isInterior && transition.ExteriorSnapshot != null)
                    {
                        transition.ExteriorSnapshot.TransitionTo(1f - blendProgress);
                    }
                }
            }
        }

        private bool IsInsideZone(EnvironmentTransition transition)
        {
            return Physics.OverlapSphere(listener.position, transition.BlendDistance).Length > 0;
        }

        private void ApplyBlendedPreset()
        {
            float t = Mathf.SmoothStep(0f, 1f, blendProgress);
            var blended = BlendPreset(currentPreset, targetPreset, t);
            blended.ApplyToMixer(mixer, reverbMixerParameter);
        }

        private AudioEnvironmentPreset BlendPreset(AudioEnvironmentPreset from, AudioEnvironmentPreset to, float t)
        {
            var blended = new AudioEnvironmentPreset();
            blended.ReverbLevel = Mathf.Lerp(from.ReverbLevel, to.ReverbLevel, t);
            blended.ReverbDecayTime = Mathf.Lerp(from.ReverbDecayTime, to.ReverbDecayTime, t);
            blended.ReverbDecayHFRatio = Mathf.Lerp(from.ReverbDecayHFRatio, to.ReverbDecayHFRatio, t);
            blended.ReflectionsLevel = Mathf.Lerp(from.ReflectionsLevel, to.ReflectionsLevel, t);
            blended.ReflectionsDelay = Mathf.Lerp(from.ReflectionsDelay, to.ReflectionsDelay, t);
            blended.WetLevel = Mathf.Lerp(from.WetLevel, to.WetLevel, t);
            blended.DryLevel = Mathf.Lerp(from.DryLevel, to.DryLevel, t);
            blended.RoomSize = Mathf.Lerp(from.RoomSize, to.RoomSize, t);
            blended.RoomHF = Mathf.Lerp(from.RoomHF, to.RoomHF, t);
            blended.DecayTime = Mathf.Lerp(from.DecayTime, to.DecayTime, t);
            blended.Density = Mathf.Lerp(from.Density, to.Density, t);
            blended.Diffusion = Mathf.Lerp(from.Diffusion, to.Diffusion, t);
            return blended;
        }

        private void ApplyEnvironmentalDampening()
        {
            if (mixer == null || listener == null) return;
            float height = listener.position.y;
            mixer.SetFloat("HeightDampening", Mathf.Clamp01(height / 100f));
        }

        public AudioEnvironmentPreset GetCurrentPreset()
        {
            return currentPreset;
        }

        public AudioEnvironmentPreset GetTargetPreset()
        {
            return targetPreset;
        }
    }
}
