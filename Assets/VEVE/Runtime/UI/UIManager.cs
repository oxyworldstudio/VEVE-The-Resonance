using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace VEVE.UI
{
    [System.Serializable]
    public enum ColorblindMode { None, Protanopia, Deuteranopia, Tritanopia, Achromatopsia }

    [System.Serializable]
    public struct AccessibilitySettingsData
    {
        public ColorblindMode colorblindMode;
        public float textScale;
        public float uiScale;
        public bool enableAudioVisualizer;
        public float subtitleSize;
        public Color subtitleBackgroundColor;
        public Color subtitleTextColor;
        public bool showDamageDirection;
        public float damageDirectionOpacity;
    }

    public enum UIState { MainMenu, Playing, Paused, Settings, Inventory, Map, Dead, Loadout, Progression }

    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private GameObject loadoutPanel;
        [SerializeField] private GameObject progressionPanel;
        [SerializeField] private AccessibilitySettings accessibilitySettings;

        [Header("Navigation")]
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private StandaloneInputModule inputModule;
        [SerializeField] private float navigationCooldown = 0.2f;

        public UIState CurrentState => currentState;
        public AccessibilitySettings AccessibilitySettings => accessibilitySettings;

        private UIState currentState;
        private float navigationTimer;
        private AccessibilitySettingsData savedAccessibilityData;

        private const string SettingsKey = "VEVE_AccessibilitySettings";

        private void Start()
        {
            LoadSettings();
            ApplyAccessibilitySettings();

            if (eventSystem == null)
                eventSystem = EventSystem.current;
            if (eventSystem != null && inputModule == null)
                inputModule = eventSystem.GetComponent<StandaloneInputModule>();

            SetState(UIState.MainMenu);
        }

        private void Update()
        {
            HandleNavigationInput();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentState == UIState.Playing)
                    SetState(UIState.Paused);
                else if (currentState == UIState.Paused)
                    SetState(UIState.Playing);
                else if (currentState == UIState.Loadout || currentState == UIState.Progression)
                    SetState(UIState.MainMenu);
            }
        }

        public void SetState(UIState newState)
        {
            currentState = newState;
            UpdatePanels();
        }

        private void UpdatePanels()
        {
            mainMenuPanel.SetActive(currentState == UIState.MainMenu);
            hudPanel.SetActive(currentState == UIState.Playing);
            pauseMenuPanel.SetActive(currentState == UIState.Paused);
            settingsPanel.SetActive(currentState == UIState.Settings);
            inventoryPanel.SetActive(currentState == UIState.Inventory);
            mapPanel.SetActive(currentState == UIState.Map);
            deathPanel.SetActive(currentState == UIState.Dead);
            loadoutPanel.SetActive(currentState == UIState.Loadout);
            progressionPanel.SetActive(currentState == UIState.Progression);

            Time.timeScale = currentState == UIState.Paused || currentState == UIState.Settings ? 0f : 1f;
            Cursor.lockState = currentState == UIState.Playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = currentState != UIState.Playing;
        }

        public void ResumeGame()
        {
            SetState(UIState.Playing);
        }

        public void OpenSettings()
        {
            SetState(UIState.Settings);
        }

        public void CloseSettings()
        {
            SetState(currentState == UIState.Paused ? UIState.Paused : UIState.Playing);
        }

        public void OpenInventory()
        {
            if (currentState == UIState.Playing)
                SetState(UIState.Inventory);
        }

        public void CloseInventory()
        {
            SetState(UIState.Playing);
        }

        public void OpenMap()
        {
            if (currentState == UIState.Playing)
                SetState(UIState.Map);
        }

        public void CloseMap()
        {
            SetState(UIState.Playing);
        }

        public void QuitToMenu()
        {
            SetState(UIState.MainMenu);
        }

        public void OnPlayerDeath()
        {
            SetState(UIState.Dead);
        }

        public void OpenLoadout()
        {
            if (currentState == UIState.MainMenu)
                SetState(UIState.Loadout);
        }

        public void CloseLoadout()
        {
            SetState(UIState.MainMenu);
        }

        public void OpenProgression()
        {
            if (currentState == UIState.MainMenu)
                SetState(UIState.Progression);
        }

        public void CloseProgression()
        {
            SetState(UIState.MainMenu);
        }

        public void SaveSettings()
        {
            if (accessibilitySettings != null)
            {
                savedAccessibilityData = accessibilitySettings.GetSettingsData();
                string json = JsonUtility.ToJson(savedAccessibilityData);
                PlayerPrefs.SetString(SettingsKey, json);
                PlayerPrefs.Save();
            }
        }

        public void LoadSettings()
        {
            if (accessibilitySettings != null && PlayerPrefs.HasKey(SettingsKey))
            {
                string json = PlayerPrefs.GetString(SettingsKey);
                AccessibilitySettingsData data = JsonUtility.FromJson<AccessibilitySettingsData>(json);
                accessibilitySettings.ApplySettingsData(data);
            }
        }

        public void ApplyAccessibilitySettings()
        {
            if (accessibilitySettings != null)
            {
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in canvases)
                {
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    if (scaler != null)
                    {
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(1920, 1080);
                        scaler.matchWidthOrHeight = 0.5f;
                    }
                }
            }
        }

        private void HandleNavigationInput()
        {
            if (navigationTimer > 0)
            {
                navigationTimer -= Time.unscaledDeltaTime;
                return;
            }

            if (currentState == UIState.Playing || eventSystem == null)
                return;

            float vertical = Input.GetAxis("Vertical");
            float horizontal = Input.GetAxis("Horizontal");
            bool submit = Input.GetButtonDown("Submit");
            bool cancel = Input.GetButtonDown("Cancel");

            if (Mathf.Abs(vertical) > 0.5f || Mathf.Abs(horizontal) > 0.5f || submit || cancel)
            {
                if (inputModule != null)
                {
                    StandaloneInputModule sim = inputModule;
                    if (vertical > 0.5f)
                        eventSystem.SetSelectedGameObject(null);
                }
                navigationTimer = navigationCooldown;
            }
        }
    }
}
