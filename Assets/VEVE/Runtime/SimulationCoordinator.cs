using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public enum SimulatorState { Uninitialized, Initializing, Running, Paused, Stopping, Stopped }

    public sealed class SimulationCoordinator : MonoBehaviour
    {
        [Header("Subsystems")]
        [SerializeField] private EnvironmentSimulation environment;
        [SerializeField] private MissionRuntime mission;
        [SerializeField] private CampaignState campaign;
        [SerializeField] private PhysicsRealism physicsRealism;
        [SerializeField] private RenderingRealism renderingRealism;

        [Header("Configuration")]
        [SerializeField] private RealismConfig realismConfig;
        [SerializeField] private QualityPreset qualityPreset;

        public EnvironmentSimulation Environment => environment;
        public MissionRuntime Mission => mission;
        public CampaignState Campaign => campaign;
        public PhysicsRealism PhysicsRealism => physicsRealism;
        public RenderingRealism RenderingRealism => renderingRealism;
        public RealismConfig RealismConfig => realismConfig;
        public QualityPreset QualityPreset => qualityPreset;
        public SimulatorState CurrentState { get; private set; } = SimulatorState.Uninitialized;

        public static SimulationCoordinator Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSubsystems();
        }

        private void InitializeSubsystems()
        {
            CurrentState = SimulatorState.Initializing;
            if (environment == null) environment = FindFirstObjectByType<EnvironmentSimulation>();
            if (mission == null) mission = FindFirstObjectByType<MissionRuntime>();
            if (campaign == null) campaign = FindFirstObjectByType<CampaignState>();
            if (physicsRealism == null) physicsRealism = FindFirstObjectByType<PhysicsRealism>();
            if (renderingRealism == null) renderingRealism = FindFirstObjectByType<RenderingRealism>();

            if (environment == null || mission == null || campaign == null || physicsRealism == null || renderingRealism == null)
                Debug.LogError("VEVE simulation is missing a required subsystem reference.", this);

            ApplyRealismConfig();
            ApplyQualityPreset();
            CurrentState = SimulatorState.Running;
        }

        private void ApplyRealismConfig()
        {
            if (realismConfig == null) return;

            Physics.gravity = new Vector3(0f, -realismConfig.StandardGravity, 0f);
            Physics.defaultSolverIterations = realismConfig.PhysicsSolverIterations;
            Physics.defaultSolverVelocityIterations = realismConfig.PhysicsSolverVelocityIterations;
            Time.fixedDeltaTime = realismConfig.FixedDeltaTime;
            Time.maximumDeltaTime = realismConfig.MaximumDeltaTime;

            QualitySettings.lodBias = realismConfig.LODBias;
            QualitySettings.shadowDistance = realismConfig.ShadowDistance;
            QualitySettings.shadowCascades = realismConfig.ShadowCascades;
            QualitySettings.antiAliasing = realismConfig.EnableAntiAliasing ? 8 : 0;
            QualitySettings.vSyncCount = realismConfig.EnableVSync ? 1 : 0;
            Application.targetFrameRate = realismConfig.TargetFrameRate;
        }

        private void ApplyQualityPreset()
        {
            if (qualityPreset == null) return;
            qualityPreset.ApplyRuntimeSettings();
            EventBus.PublishGlobal(new QualityPresetChangedEvent(qualityPreset.CurrentLevel));
        }

        public void SetSimulationState(SimulatorState state)
        {
            CurrentState = state;
            EventBus.PublishGlobal(new SimulationStateChangedEvent(state));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
