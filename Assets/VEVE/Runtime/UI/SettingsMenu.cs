using UnityEngine;
using UnityEngine.UI;
using System;

namespace VEVE.UI
{
    public class SettingsMenu : MonoBehaviour
    {
        [Header("Graphics")]
        [SerializeField] private Slider resolutionScaleSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private Toggle rayTracingToggle;
        [SerializeField] private Slider shadowQualitySlider;
        [SerializeField] private Text graphicsPreviewText;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider voiceChatVolumeSlider;
        [SerializeField] private AudioSource previewAudioSource;
        [SerializeField] private Text audioPreviewText;

        [Header("Gameplay")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Slider aimSensitivitySlider;
        [SerializeField] private Toggle invertedYAxisToggle;
        [SerializeField] private Toggle invertedXAxisToggle;
        [SerializeField] private Slider fovSlider;
        [SerializeField] private Text fovPreviewText;

        [Header("Controls")]
        [SerializeField] private Dropdown controlPresetDropdown;
        [SerializeField] private Button keyBindingButton;
        [SerializeField] private Text keyBindingPreviewText;
        [SerializeField] private Slider controllerDeadzoneSlider;
        [SerializeField] private Text controllerPreviewText;

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private AccessibilitySettings accessibilitySettings;

        private float originalMouseSensitivity = 1.0f;
        private float originalFov = 75.0f;
        private float originalVolume = 1.0f;

        private void Start()
        {
            LoadSettings();
        }

        public void OnGraphicsTabSelected()
        {
            if (graphicsPreviewText != null)
                graphicsPreviewText.text = "Graphics settings applied in real-time";
        }

        public void OnAudioTabSelected()
        {
            if (audioPreviewText != null)
                audioPreviewText.text = "Audio settings applied in real-time";
        }

        public void OnGameplayTabSelected()
        {
            if (fovPreviewText != null)
                fovPreviewText.text = $"FOV: {fovSlider.value:F0}";
        }

        public void OnControlsTabSelected()
        {
            if (keyBindingPreviewText != null)
                keyBindingPreviewText.text = "Press a key to bind";
            if (controllerPreviewText != null)
                controllerPreviewText.text = $"Deadzone: {controllerDeadzoneSlider.value:F2}";
        }

        public void OnResolutionScaleChanged(float value)
        {
            if (resolutionScaleSlider != null)
            {
                float scale = Mathf.Lerp(0.5f, 2.0f, value);
                CanvasScaler[] scalers = FindObjectsOfType<CanvasScaler>();
                foreach (CanvasScaler scaler in scalers)
                {
                    scaler.scaleFactor = scale;
                }
            }
        }

        public void OnFullscreenChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
        }

        public void OnVSyncChanged(bool isEnabled)
        {
            QualitySettings.vSyncCount = isEnabled ? 1 : 0;
        }

        public void OnRayTracingChanged(bool isEnabled)
        {
            if (rayTracingToggle != null)
            {
                if (isEnabled)
                {
                    if (graphicsPreviewText != null)
                        graphicsPreviewText.text = "Ray tracing enabled - restart required for full effect";
                }
            }
        }

        public void OnShadowQualityChanged(float value)
        {
            int quality = Mathf.RoundToInt(value);
            if (quality >= 0 && quality < QualitySettings.shadowCascades)
                QualitySettings.SetQualityLevel(quality);
        }

        public void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            originalVolume = value;
            if (audioPreviewText != null)
                audioPreviewText.text = $"Master Volume: {value * 100:F0}%";
        }

        public void OnSFXVolumeChanged(float value)
        {
            if (audioPreviewText != null)
                audioPreviewText.text = $"SFX Volume: {value * 100:F0}%";
        }

        public void OnMusicVolumeChanged(float value)
        {
            if (audioPreviewText != null)
                audioPreviewText.text = $"Music Volume: {value * 100:F0}%";
        }

        public void OnVoiceChatVolumeChanged(float value)
        {
            if (audioPreviewText != null)
                audioPreviewText.text = $"Voice Chat: {value * 100:F0}%";
        }

        public void OnMouseSensitivityChanged(float value)
        {
            originalMouseSensitivity = value;
            if (keyBindingPreviewText != null)
                keyBindingPreviewText.text = $"Sensitivity: {value:F2}";
        }

        public void OnAimSensitivityChanged(float value)
        {
            if (keyBindingPreviewText != null)
                keyBindingPreviewText.text = $"Aim Sensitivity: {value:F2}";
        }

        public void OnInvertedYChanged(bool isInverted)
        {
            if (keyBindingPreviewText != null)
                keyBindingPreviewText.text = $"Y Axis: {(isInverted ? "Inverted" : "Normal")}";
        }

        public void OnInvertedXChanged(bool isInverted)
        {
            if (keyBindingPreviewText != null)
                keyBindingPreviewText.text = $"X Axis: {(isInverted ? "Inverted" : "Normal")}";
        }

        public void OnFOVChanged(float value)
        {
            originalFov = value;
            Camera.main.fieldOfView = value;
            if (fovPreviewText != null)
                fovPreviewText.text = $"FOV: {value:F0}";
        }

        public void OnControlPresetChanged(int index)
        {
            if (keyBindingPreviewText != null)
            {
                string[] presets = { "Default", "Tactical", "Precision", "Console" };
                keyBindingPreviewText.text = $"Preset: {presets[index >= 0 && index < presets.Length ? index : 0]}";
            }
        }

        public void OnKeyBindingButtonPressed()
        {
            if (keyBindingPreviewText != null)
                keyBindingPreviewText.text = "Press any key to bind...";
        }

        public void OnControllerDeadzoneChanged(float value)
        {
            if (controllerPreviewText != null)
                controllerPreviewText.text = $"Deadzone: {value:F2}";
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("VEVE_ResolutionScale", resolutionScaleSlider != null ? resolutionScaleSlider.value : 1.0f);
            PlayerPrefs.SetInt("VEVE_Fullscreen", fullscreenToggle != null && fullscreenToggle.isOn ? 1 : 0);
            PlayerPrefs.SetInt("VEVE_VSync", vsyncToggle != null && vsyncToggle.isOn ? 1 : 0);
            PlayerPrefs.SetFloat("VEVE_MasterVolume", masterVolumeSlider != null ? masterVolumeSlider.value : 1.0f);
            PlayerPrefs.SetFloat("VEVE_SFXVolume", sfxVolumeSlider != null ? sfxVolumeSlider.value : 1.0f);
            PlayerPrefs.SetFloat("VEVE_MusicVolume", musicVolumeSlider != null ? musicVolumeSlider.value : 1.0f);
            PlayerPrefs.SetFloat("VEVE_VoiceChatVolume", voiceChatVolumeSlider != null ? voiceChatVolumeSlider.value : 1.0f);
            PlayerPrefs.SetFloat("VEVE_MouseSensitivity", originalMouseSensitivity);
            PlayerPrefs.SetFloat("VEVE_AimSensitivity", aimSensitivitySlider != null ? aimSensitivitySlider.value : 1.0f);
            PlayerPrefs.SetInt("VEVE_InvertedY", invertedYAxisToggle != null && invertedYAxisToggle.isOn ? 1 : 0);
            PlayerPrefs.SetInt("VEVE_InvertedX", invertedXAxisToggle != null && invertedXAxisToggle.isOn ? 1 : 0);
            PlayerPrefs.SetFloat("VEVE_FOV", originalFov);
            PlayerPrefs.SetInt("VEVE_ControlPreset", controlPresetDropdown != null ? controlPresetDropdown.value : 0);
            PlayerPrefs.SetFloat("VEVE_ControllerDeadzone", controllerDeadzoneSlider != null ? controllerDeadzoneSlider.value : 0.1f);
            PlayerPrefs.Save();

            if (uiManager != null)
                uiManager.SaveSettings();
        }

        public void ResetToDefaults()
        {
            if (resolutionScaleSlider != null) resolutionScaleSlider.value = 1.0f;
            if (fullscreenToggle != null) fullscreenToggle.isOn = true;
            if (vsyncToggle != null) vsyncToggle.isOn = true;
            if (rayTracingToggle != null) rayTracingToggle.isOn = false;
            if (shadowQualitySlider != null) shadowQualitySlider.value = 2.0f;
            if (masterVolumeSlider != null) masterVolumeSlider.value = 1.0f;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1.0f;
            if (musicVolumeSlider != null) musicVolumeSlider.value = 0.8f;
            if (voiceChatVolumeSlider != null) voiceChatVolumeSlider.value = 1.0f;
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = 1.0f;
            if (aimSensitivitySlider != null) aimSensitivitySlider.value = 1.0f;
            if (invertedYAxisToggle != null) invertedYAxisToggle.isOn = false;
            if (invertedXAxisToggle != null) invertedXAxisToggle.isOn = false;
            if (fovSlider != null) fovSlider.value = 75.0f;
            if (controlPresetDropdown != null) controlPresetDropdown.value = 0;
            if (controllerDeadzoneSlider != null) controllerDeadzoneSlider.value = 0.1f;

            Camera.main.fieldOfView = 75.0f;
            AudioListener.volume = 1.0f;
            Screen.fullScreen = true;
            QualitySettings.vSyncCount = 1;

            SaveSettings();
        }

        public void ApplySettings()
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            if (resolutionScaleSlider != null && PlayerPrefs.HasKey("VEVE_ResolutionScale"))
                resolutionScaleSlider.value = PlayerPrefs.GetFloat("VEVE_ResolutionScale");
            if (fullscreenToggle != null && PlayerPrefs.HasKey("VEVE_Fullscreen"))
                fullscreenToggle.isOn = PlayerPrefs.GetInt("VEVE_Fullscreen") == 1;
            if (vsyncToggle != null && PlayerPrefs.HasKey("VEVE_VSync"))
                vsyncToggle.isOn = PlayerPrefs.GetInt("VEVE_VSync") == 1;
            if (masterVolumeSlider != null && PlayerPrefs.HasKey("VEVE_MasterVolume"))
            {
                masterVolumeSlider.value = PlayerPrefs.GetFloat("VEVE_MasterVolume");
                AudioListener.volume = masterVolumeSlider.value;
            }
            if (mouseSensitivitySlider != null && PlayerPrefs.HasKey("VEVE_MouseSensitivity"))
                mouseSensitivitySlider.value = PlayerPrefs.GetFloat("VEVE_MouseSensitivity");
            if (aimSensitivitySlider != null && PlayerPrefs.HasKey("VEVE_AimSensitivity"))
                aimSensitivitySlider.value = PlayerPrefs.GetFloat("VEVE_AimSensitivity");
            if (invertedYAxisToggle != null && PlayerPrefs.HasKey("VEVE_InvertedY"))
                invertedYAxisToggle.isOn = PlayerPrefs.GetInt("VEVE_InvertedY") == 1;
            if (invertedXAxisToggle != null && PlayerPrefs.HasKey("VEVE_InvertedX"))
                invertedXAxisToggle.isOn = PlayerPrefs.GetInt("VEVE_InvertedX") == 1;
            if (fovSlider != null && PlayerPrefs.HasKey("VEVE_FOV"))
            {
                fovSlider.value = PlayerPrefs.GetFloat("VEVE_FOV");
                Camera.main.fieldOfView = fovSlider.value;
            }
            if (controlPresetDropdown != null && PlayerPrefs.HasKey("VEVE_ControlPreset"))
                controlPresetDropdown.value = PlayerPrefs.GetInt("VEVE_ControlPreset");
            if (controllerDeadzoneSlider != null && PlayerPrefs.HasKey("VEVE_ControllerDeadzone"))
                controllerDeadzoneSlider.value = PlayerPrefs.GetFloat("VEVE_ControllerDeadzone");
        }
    }
}
