using UnityEngine;

namespace VEVE.Audio
{
    public enum AudioPreset { Interior, Exterior, Underwater, Helmet, Radio, Cave, Mountain, Forest, Urban, Tunnel, OpenField, Industrial }

    [CreateAssetMenu(menuName = "VEVE/Audio/Audio Preset")]
    public sealed class AudioPresetDefinition : ScriptableObject
    {
        [SerializeField] private AudioPreset preset;
        [SerializeField] private float masterVolume = 1f;
        [SerializeField] private float sfxVolume = 1f;
        [SerializeField] private float musicVolume = 0.8f;
        [SerializeField] private float voiceVolume = 1f;
        [SerializeField] private float reverbAmount = 0.5f;
        [SerializeField] private float lowPassCutoff = 22000f;
        [SerializeField] private float highPassCutoff = 20f;
        [SerializeField] private float compressionThreshold = -20f;
        [SerializeField] private float compressionRatio = 4f;
        [SerializeField] private float dopplerIntensity = 1f;
        [SerializeField] private float spatialBlend = 1f;
        [SerializeField] private float maxDistance = 1000f;
        [SerializeField] private float rolloffFactor = 1f;

        public AudioPreset Preset => preset;
        public float MasterVolume => masterVolume;
        public float SFXVolume => sfxVolume;
        public float MusicVolume => musicVolume;
        public float VoiceVolume => voiceVolume;
        public float ReverbAmount => reverbAmount;
        public float LowPassCutoff => lowPassCutoff;
        public float HighPassCutoff => highPassCutoff;
        public float CompressionThreshold => compressionThreshold;
        public float CompressionRatio => compressionRatio;
        public float DopplerIntensity => dopplerIntensity;
        public float SpatialBlend => spatialBlend;
        public float MaxDistance => maxDistance;
        public float RolloffFactor => rolloffFactor;
    }
}
