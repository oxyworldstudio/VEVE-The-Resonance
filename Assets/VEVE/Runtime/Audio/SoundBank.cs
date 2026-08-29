using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Audio
{
    public enum SoundCategory { Footstep, Gunshot, Impact, Explosion, Voice, Radio, Environmental, UI }

    [CreateAssetMenu(menuName = "VEVE/Audio/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        [SerializeField] private SoundCategory category;
        [SerializeField] private List<AudioClip> clips = new();
        [SerializeField] private List<float> weights = new();
        [SerializeField] private bool randomizePitch = true;
        [SerializeField] private float pitchMin = 0.9f;
        [SerializeField] private float pitchMax = 1.1f;
        [SerializeField] private float volumeMin = 0.8f;
        [SerializeField] private float volumeMax = 1.0f;
        [SerializeField] private float spatialBlend = 1.0f;
        [SerializeField] private float minDistance = 1.0f;
        [SerializeField] private float maxDistance = 100.0f;
        [SerializeField] private float rolloff = 1.0f;

        public SoundCategory Category => category;
        public IReadOnlyList<AudioClip> Clips => clips;
        public IReadOnlyList<float> Weights => weights;
        public bool RandomizePitch => randomizePitch;
        public float PitchMin => pitchMin;
        public float PitchMax => pitchMax;
        public float VolumeMin => volumeMin;
        public float VolumeMax => volumeMax;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float Rolloff => rolloff;

        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Count == 0) return null;
            float totalWeight = 0f;
            foreach (float w in weights) totalWeight += Mathf.Max(0.01f, w);
            float r = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += Mathf.Max(0.01f, weights[i]);
                if (r <= cumulative) return clips[i];
            }
            return clips[clips.Count - 1];
        }
    }
}
