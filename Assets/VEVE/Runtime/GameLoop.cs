using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using VEVE;
using VEVE.Realism;

namespace VEVE
{
    public enum GameState { Menu, Playing, Paused, Dead, Loading }

    public sealed class GameLoop : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private string menuScene = "MainMenu";
        [SerializeField] private string gameScene = "Gameplay";
        [SerializeField] private string loadingScene = "Loading";

        [Header("Loading Settings")]
        [SerializeField] private float minimumLoadingTime = 1.5f;
        [SerializeField] private bool showLoadingScreen = true;

        [Header("Game State")]
        [SerializeField] private GameState initialState = GameState.Menu;

        public GameState CurrentState { get; private set; } = GameState.Menu;
        public bool IsPaused => CurrentState == GameState.Paused;
        public bool IsPlaying => CurrentState == GameState.Playing;
        public bool IsDead => CurrentState == GameState.Dead;
        public bool IsLoading => CurrentState == GameState.Loading;

        public static GameLoop Instance { get; private set; }

        private AsyncOperation _loadOperation;
        private float _loadStartTime;

        public event Action<GameState> OnStateChanged;
        public event Action<float> OnLoadingProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetState(initialState);
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    PauseGame();
            }
            else if (CurrentState == GameState.Paused)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    ResumeGame();
            }
            else if (CurrentState == GameState.Dead)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    RestartMission();
            }
            else if (CurrentState == GameState.Menu)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                    StartNewGame();
            }

            if (CurrentState == GameState.Loading && _loadOperation != null)
            {
                float progress = Mathf.Clamp01(_loadOperation.progress / 0.9f);
                OnLoadingProgress?.Invoke(progress);
                if (_loadOperation.isDone && Time.time - _loadStartTime >= minimumLoadingTime)
                {
                    SetState(GameState.Playing);
                    _loadOperation = null;
                }
            }
        }

        public void SetState(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
            EventBus.PublishGlobal(new SimulationStateChangedEvent(SimulatorState.Running));

            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.Dead:
                    Time.timeScale = 1f;
                    break;
                case GameState.Loading:
                    Time.timeScale = 0f;
                    break;
                default:
                    Time.timeScale = 1f;
                    break;
            }
        }

        public void StartNewGame()
        {
            if (IsLoading) return;
            SetState(GameState.Loading);
            _loadStartTime = Time.time;

            if (showLoadingScreen && !string.IsNullOrEmpty(loadingScene))
            {
                SceneManager.LoadSceneAsync(loadingScene, LoadSceneMode.Single);
                StartCoroutine(LoadGameSceneAfterLoadingScreen());
            }
            else
            {
                LoadSceneAsync(gameScene);
            }
        }

        public void ReturnToMenu()
        {
            if (IsLoading) return;
            SetState(GameState.Loading);
            _loadStartTime = Time.time;
            LoadSceneAsync(menuScene);
        }

        public void RestartMission()
        {
            if (IsLoading) return;
            SetState(GameState.Loading);
            _loadStartTime = Time.time;
            LoadSceneAsync(gameScene);
        }

        public void PauseGame()
        {
            if (CurrentState != GameState.Playing) return;
            SetState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused) return;
            SetState(GameState.Playing);
        }

        public void KillPlayer()
        {
            if (CurrentState != GameState.Playing) return;
            SetState(GameState.Dead);
            EventBus.PublishGlobal(new PlayerDeathEvent(null));
        }

        private void LoadSceneAsync(string sceneName)
        {
            _loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            _loadOperation.allowSceneActivation = false;
        }

        private System.Collections.IEnumerator LoadGameSceneAfterLoadingScreen()
        {
            yield return new WaitForSeconds(minimumLoadingTime);
            _loadOperation = SceneManager.LoadSceneAsync(gameScene, LoadSceneMode.Single);
            _loadOperation.allowSceneActivation = true;
        }
    }
}
