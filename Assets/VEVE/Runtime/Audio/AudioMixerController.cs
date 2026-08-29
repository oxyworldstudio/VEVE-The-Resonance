using UnityEngine;
using UnityEngine.Audio;

namespace VEVE.Audio
{
    public sealed class AudioMixerController : MonoBehaviour
    {
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup voiceGroup;
        [SerializeField] private AudioPresetDefinition defaultPreset;

        private AudioPresetDefinition currentPreset;

        private void Start()
        {
            if (defaultPreset != null)
            {
                ApplyPreset(defaultPreset);
            }
        }

        public void ApplyPreset(AudioPresetDefinition preset)
        {
            if (preset == null) return;
            currentPreset = preset;

            if (masterGroup != null)
            {
                masterGroup.audioMixer.SetFloat("MasterVolume", DecibelToLinear(preset.MasterVolume));
            }
            if (sfxGroup != null)
            {
                sfxGroup.audioMixer.SetFloat("SFXVolume", DecibelToLinear(preset.SFXVolume));
            }
            if (musicGroup != null)
            {
                musicGroup.audioMixer.SetFloat("MusicVolume", DecibelToLinear(preset.MusicVolume));
            }
            if (voiceGroup != null)
            {
                voiceGroup.audioMixer.SetFloat("VoiceVolume", DecibelToLinear(preset.VoiceVolume));
            }
        }

        private float DecibelToLinear(float db)
        {
            return Mathf.Clamp01(Mathf.Pow(10f, db / 20f));
        }
    }
}
