using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Audio
{
    public enum SoundCategory { Footstep, Gunshot, Impact, Explosion, Voice, Radio, Environmental, UI }

    [System.Serializable]
    public class SoundLayer
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float weight = 1f;
        [SerializeField, Range(0f, 2f)] private float volumeScale = 1f;
        [SerializeField, Range(0.5f, 2f)] private float pitchMin = 0.9f;
        [SerializeField, Range(0.5f, 2f)] private float pitchMax = 1.1f;
        [SerializeField, Range(0f, 1f)] private float randomDelay = 0f;

        public AudioClip Clip => clip;
        public float Weight { get { return weight; } }
        public float VolumeScale { get { return volumeScale; } }
        public float PitchMin { get { return pitchMin; } }
        public float PitchMax { get { return pitchMax; } }
        public float RandomDelay { get { return randomDelay; } }

        public float GetRandomPitch()
        {
            return Random.Range(pitchMin, pitchMax);
        }
    }

    [CreateAssetMenu(menuName = "VEVE/Audio/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        [SerializeField] private SoundCategory category;
        [SerializeField] private List<SoundLayer> layers = new();
        [SerializeField] private bool randomizePitch = true;
        [SerializeField] private float volumeMin = 0.8f;
        [SerializeField] private float volumeMax = 1.0f;
        [SerializeField] private float spatialBlend = 1.0f;
        [SerializeField] private float minDistance = 1.0f;
        [SerializeField] private float maxDistance = 100.0f;
        [SerializeField] private float rolloff = 1.0f;

        public SoundCategory Category => category;
        public IReadOnlyList<SoundLayer> Layers => layers;
        public bool RandomizePitch => randomizePitch;
        public float VolumeMin => volumeMin;
        public float VolumeMax => volumeMax;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float Rolloff => rolloff;

        public AudioClip GetRandomClip()
        {
            if (layers == null || layers.Count == 0) return null;
            float totalWeight = 0f;
            foreach (SoundLayer layer in layers)
            {
                totalWeight += Mathf.Max(0.01f, layer.Weight);
            }
            float r = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                cumulative += Mathf.Max(0.01f, layers[i].Weight);
                if (r <= cumulative) return layers[i].Clip;
            }
            return layers[layers.Count - 1].Clip;
        }

        public SoundLayer GetRandomLayer()
        {
            if (layers == null || layers.Count == 0) return null;
            float totalWeight = 0f;
            foreach (SoundLayer layer in layers)
            {
                totalWeight += Mathf.Max(0.01f, layer.Weight);
            }
            float r = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                cumulative += Mathf.Max(0.01f, layers[i].Weight);
                if (r <= cumulative) return layers[i];
            }
            return layers[layers.Count - 1];
        }

        public List<AudioClip> GetAllClipsForLayer(int layerIndex)
        {
            var clips = new List<AudioClip>();
            if (layers == null || layerIndex < 0 || layerIndex >= layers.Count) return clips;
            clips.Add(layers[layerIndex].Clip);
            return clips;
        }
    }
}
