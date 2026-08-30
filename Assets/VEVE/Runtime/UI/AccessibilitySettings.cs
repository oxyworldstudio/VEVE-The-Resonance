using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace VEVE.UI
{
    public class AccessibilitySettings : MonoBehaviour
    {
        [Header("Colorblind Settings")]
        [SerializeField] private ColorblindMode colorblindMode = ColorblindMode.None;
        [SerializeField] private Material colorblindMaterial;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Image[] friendlyIndicatorImages;
        [SerializeField] private Image[] hostileIndicatorImages;

        [Header("Text & UI Scaling")]
        [SerializeField] private float textScale = 1.0f;
        [SerializeField] private float uiScale = 1.0f;
        [SerializeField] private Text[] scalableTextElements;
        [SerializeField] private RectTransform[] scalableUIElements;

        [Header("Audio Visualizer")]
        [SerializeField] private bool enableAudioVisualizer = false;
        [SerializeField] private RectTransform audioVisualizerContainer;
        [SerializeField] private Image audioVisualizerBarPrefab;
        [SerializeField] private int audioVisualizerBarCount = 16;
        [SerializeField] private AudioSource targetAudioSource;

        [Header("Subtitle Settings")]
        [SerializeField] private float subtitleSize = 24f;
        [SerializeField] private Color subtitleBackgroundColor = new Color(0f, 0f, 0f, 0.8f);
        [SerializeField] private Color subtitleTextColor = Color.white;
        [SerializeField] private Text subtitleDisplayText;
        [SerializeField] private float subtitleDuration = 4.0f;

        [Header("Damage Direction")]
        [SerializeField] private bool showDamageDirection = true;
        [SerializeField] private float damageDirectionOpacity = 1.0f;
        [SerializeField] private Image damageDirectionTemplate;
        [SerializeField] private int damageDirectionSegments = 8;

        private List<Image> audioVisualizerBars = new List<Image>();
        private float subtitleTimer;

        public ColorblindMode ColorblindMode
        {
            get => colorblindMode;
            set
            {
                colorblindMode = value;
                ApplyColorblindMode();
            }
        }

        public float TextScale
        {
            get => textScale;
            set
            {
                textScale = Mathf.Clamp(value, 0.5f, 3.0f);
                ApplyTextScaling();
            }
        }

        public float UIScale
        {
            get => uiScale;
            set
            {
                uiScale = Mathf.Clamp(value, 0.5f, 2.0f);
                ApplyUIScaling();
            }
        }

        public bool EnableAudioVisualizer
        {
            get => enableAudioVisualizer;
            set
            {
                enableAudioVisualizer = value;
                if (audioVisualizerContainer != null)
                    audioVisualizerContainer.gameObject.SetActive(value);
            }
        }

        public float SubtitleSize
        {
            get => subtitleSize;
            set
            {
                subtitleSize = Mathf.Clamp(value, 12f, 72f);
                if (subtitleDisplayText != null)
                    subtitleDisplayText.fontSize = (int)subtitleSize;
            }
        }

        public Color SubtitleBackgroundColor
        {
            get => subtitleBackgroundColor;
            set
            {
                subtitleBackgroundColor = value;
                if (subtitleDisplayText != null)
                    subtitleDisplayText.color = subtitleTextColor;
            }
        }

        public Color SubtitleTextColor
        {
            get => subtitleTextColor;
            set
            {
                subtitleTextColor = value;
                if (subtitleDisplayText != null)
                    subtitleDisplayText.color = value;
            }
        }

        public bool ShowDamageDirection
        {
            get => showDamageDirection;
            set
            {
                showDamageDirection = value;
                if (damageDirectionTemplate != null)
                    damageDirectionTemplate.gameObject.SetActive(value);
            }
        }

        public float DamageDirectionOpacity
        {
            get => damageDirectionOpacity;
            set
            {
                damageDirectionOpacity = Mathf.Clamp01(value);
                if (damageDirectionTemplate != null)
                    damageDirectionTemplate.color = new Color(1f, 0f, 0f, damageDirectionOpacity);
            }
        }

        private void Start()
        {
            ApplyColorblindMode();
            ApplyTextScaling();
            ApplyUIScaling();
            InitializeAudioVisualizer();
        }

        private void Update()
        {
            if (enableAudioVisualizer && targetAudioSource != null && audioVisualizerBars.Count > 0)
            {
                float[] spectrum = new float[audioVisualizerBars.Count];
                targetAudioSource.GetSpectrumData(spectrum, 0, FFTWindow.Blackman);
                for (int i = 0; i < audioVisualizerBars.Count; i++)
                {
                    if (audioVisualizerBars[i] != null)
                    {
                        float height = Mathf.Clamp01(spectrum[i] * 10f);
                        audioVisualizerBars[i].rectTransform.localScale = new Vector3(1f, height, 1f);
                    }
                }
            }

            if (subtitleTimer > 0)
            {
                subtitleTimer -= Time.deltaTime;
                if (subtitleTimer <= 0 && subtitleDisplayText != null)
                    subtitleDisplayText.text = "";
            }
        }

        public void ShowSubtitle(string text)
        {
            if (subtitleDisplayText != null)
            {
                subtitleDisplayText.text = text;
                subtitleDisplayText.fontSize = (int)subtitleSize;
                subtitleDisplayText.color = subtitleTextColor;
                subtitleTimer = subtitleDuration;
            }
        }

        public void SetColorblindMode(ColorblindMode mode)
        {
            colorblindMode = mode;
            ApplyColorblindMode();
        }

        private void ApplyColorblindMode()
        {
            if (mainCamera != null && colorblindMaterial != null)
            {
                mainCamera.SetReplacementShader(colorblindMaterial.shader, "");
            }

            Color friendlyColor = Color.green;
            Color hostileColor = Color.red;
            Color neutralColor = Color.blue;

            switch (colorblindMode)
            {
                case ColorblindMode.Protanopia:
                    friendlyColor = new Color(0.627f, 0.537f, 0.0f);
                    hostileColor = new Color(0.835f, 0.369f, 0.0f);
                    neutralColor = new Color(0.0f, 0.467f, 0.439f);
                    break;
                case ColorblindMode.Deuteranopia:
                    friendlyColor = new Color(0.627f, 0.616f, 0.0f);
                    hostileColor = new Color(0.867f, 0.627f, 0.0f);
                    neutralColor = new Color(0.0f, 0.467f, 0.439f);
                    break;
                case ColorblindMode.Tritanopia:
                    friendlyColor = new Color(0.788f, 0.302f, 0.0f);
                    hostileColor = new Color(0.0f, 0.467f, 0.439f);
                    neutralColor = new Color(0.627f, 0.325f, 0.196f);
                    break;
                case ColorblindMode.Achromatopsia:
                    friendlyColor = Color.gray;
                    hostileColor = Color.gray;
                    neutralColor = Color.gray;
                    break;
            }

            if (friendlyIndicatorImages != null)
            {
                foreach (Image img in friendlyIndicatorImages)
                    if (img != null) img.color = friendlyColor;
            }
            if (hostileIndicatorImages != null)
            {
                foreach (Image img in hostileIndicatorImages)
                    if (img != null) img.color = hostileColor;
            }
        }

        private void ApplyTextScaling()
        {
            if (scalableTextElements != null)
            {
                foreach (Text text in scalableTextElements)
                {
                    if (text != null)
                        text.fontSize = (int)(text.fontSize * textScale);
                }
            }
        }

        private void ApplyUIScaling()
        {
            if (scalableUIElements != null)
            {
                foreach (RectTransform rect in scalableUIElements)
                {
                    if (rect != null)
                        rect.localScale = new Vector3(uiScale, uiScale, 1f);
                }
            }
        }

        private void InitializeAudioVisualizer()
        {
            if (audioVisualizerContainer == null || audioVisualizerBarPrefab == null)
                return;

            if (!enableAudioVisualizer)
                audioVisualizerContainer.gameObject.SetActive(false);

            for (int i = 0; i < audioVisualizerBarCount; i++)
            {
                GameObject barObj = Instantiate(audioVisualizerBarPrefab.gameObject, audioVisualizerContainer);
                Image barImg = barObj.GetComponent<Image>();
                if (barImg != null)
                {
                    barImg.color = Color.Lerp(Color.blue, Color.red, (float)i / audioVisualizerBarCount);
                    audioVisualizerBars.Add(barImg);
                }
            }
        }

        public AccessibilitySettingsData GetSettingsData()
        {
            return new AccessibilitySettingsData
            {
                colorblindMode = colorblindMode,
                textScale = textScale,
                uiScale = uiScale,
                enableAudioVisualizer = enableAudioVisualizer,
                subtitleSize = subtitleSize,
                subtitleBackgroundColor = subtitleBackgroundColor,
                subtitleTextColor = subtitleTextColor,
                showDamageDirection = showDamageDirection,
                damageDirectionOpacity = damageDirectionOpacity
            };
        }

        public void ApplySettingsData(AccessibilitySettingsData data)
        {
            colorblindMode = data.colorblindMode;
            textScale = data.textScale;
            uiScale = data.uiScale;
            enableAudioVisualizer = data.enableAudioVisualizer;
            subtitleSize = data.subtitleSize;
            subtitleBackgroundColor = data.subtitleBackgroundColor;
            subtitleTextColor = data.subtitleTextColor;
            showDamageDirection = data.showDamageDirection;
            damageDirectionOpacity = data.damageDirectionOpacity;

            ApplyColorblindMode();
            ApplyTextScaling();
            ApplyUIScaling();
            if (subtitleDisplayText != null)
            {
                subtitleDisplayText.fontSize = (int)subtitleSize;
                subtitleDisplayText.color = subtitleTextColor;
            }
            if (damageDirectionTemplate != null)
            {
                damageDirectionTemplate.gameObject.SetActive(showDamageDirection);
                damageDirectionTemplate.color = new Color(1f, 0f, 0f, damageDirectionOpacity);
            }
        }
    }
}
