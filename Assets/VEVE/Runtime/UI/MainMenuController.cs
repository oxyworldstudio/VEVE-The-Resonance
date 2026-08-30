using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using VEVE.Mission;

namespace VEVE.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menu Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject campaignPanel;
        [SerializeField] private GameObject loadoutPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject progressionPanel;

        [Header("Buttons")]
        [SerializeField] private Button campaignButton;
        [SerializeField] private Button loadoutButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private string firstMissionScene = "Mission_01";

        private void Start()
        {
            if (campaignButton != null)
                campaignButton.onClick.AddListener(OnCampaignSelected);
            if (loadoutButton != null)
                loadoutButton.onClick.AddListener(OnLoadoutSelected);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsSelected);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitSelected);

            ShowMainMenu();
        }

        public void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (campaignPanel != null) campaignPanel.SetActive(false);
            if (loadoutPanel != null) loadoutPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (progressionPanel != null) progressionPanel.SetActive(false);
        }

        public void OnCampaignSelected()
        {
            if (campaignPanel != null)
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
                campaignPanel.SetActive(true);
            }
            else
            {
                StartCampaign();
            }
        }

        public void OnLoadoutSelected()
        {
            if (uiManager != null)
                uiManager.OpenLoadout();
        }

        public void OnSettingsSelected()
        {
            if (uiManager != null)
                uiManager.OpenSettings();
        }

        public void OnQuitSelected()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void StartNewMission()
        {
            StartCampaign();
        }

        public void ContinueMission()
        {
            if (MissionPersistence.Load() != null)
                StartCampaign();
        }

        private void StartCampaign()
        {
            if (!string.IsNullOrEmpty(firstMissionScene))
                SceneManager.LoadScene(firstMissionScene);
        }

        public void OpenLoadoutFromMenu()
        {
            if (loadoutPanel != null)
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
                if (campaignPanel != null) campaignPanel.SetActive(false);
                loadoutPanel.SetActive(true);
            }
        }

        public void CloseLoadoutToMenu()
        {
            ShowMainMenu();
        }

        public void OpenProgressionFromMenu()
        {
            if (progressionPanel != null)
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
                if (campaignPanel != null) campaignPanel.SetActive(false);
                progressionPanel.SetActive(true);
            }
        }

        public void CloseProgressionToMenu()
        {
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (campaignButton != null)
                campaignButton.onClick.RemoveListener(OnCampaignSelected);
            if (loadoutButton != null)
                loadoutButton.onClick.RemoveListener(OnLoadoutSelected);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(OnSettingsSelected);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(OnQuitSelected);
        }
    }
}
