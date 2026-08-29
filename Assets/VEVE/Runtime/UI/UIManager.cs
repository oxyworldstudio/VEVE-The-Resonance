using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VEVE.UI
{
    public enum UIState { MainMenu, Playing, Paused, Settings, Inventory, Map, Dead }

    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject deathPanel;

        private UIState currentState;

        private void Start()
        {
            SetState(UIState.MainMenu);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentState == UIState.Playing)
                    SetState(UIState.Paused);
                else if (currentState == UIState.Paused)
                    SetState(UIState.Playing);
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

            Time.timeScale = currentState == UIState.Paused ? 0f : 1f;
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
    }
}
