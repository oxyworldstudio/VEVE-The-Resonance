using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public sealed class SimulationDiagnostics : MonoBehaviour
    {
        [Header("Diagnostics Settings")]
        [SerializeField] private bool logOnStart = true;
        [SerializeField] private bool enablePerformanceMetrics = true;
        [SerializeField] private bool enableHealthChecks = true;
        [SerializeField] private bool enableDebugVisualization = true;
        [SerializeField] private float metricsUpdateInterval = 1f;

        private float _metricsTimer;
        private int _frameCount;
        private float _frameTimeAccumulator;
        private float _lastFrameTime;

        public float CurrentFPS { get; private set; }
        public float AverageFrameTimeMs { get; private set; }
        public float PeakFrameTimeMs { get; private set; }
        public bool AllSubsystemsHealthy { get; private set; } = true;

        private void Start()
        {
            if (!logOnStart) return;
            ValidateReference<EnvironmentSimulation>("EnvironmentSimulation");
            ValidateReference<MissionRuntime>("MissionRuntime");
            ValidateReference<CampaignState>("CampaignState");
            ValidateReference<PhysicalInventory>("PhysicalInventory");
            ValidateReference<MovementSimulation>("MovementSimulation");
            ValidateReference<PhysicsRealism>("PhysicsRealism");
            ValidateReference<RenderingRealism>("RenderingRealism");
            ValidateReference<QualityPreset>("QualityPreset");
        }

        private void Update()
        {
            if (!enablePerformanceMetrics) return;

            _frameCount++;
            _frameTimeAccumulator += Time.unscaledDeltaTime;
            _metricsTimer += Time.unscaledDeltaTime;

            if (_frameTimeAccumulator > metricsUpdateInterval || _metricsTimer >= metricsUpdateInterval)
            {
                UpdatePerformanceMetrics();
                _metricsTimer = 0f;
            }

            if (enableHealthChecks && Time.frameCount % 120 == 0)
                RunHealthChecks();

            if (enableDebugVisualization && _frameCount % 60 == 0)
                DrawDebugOverlay();
        }

        private void UpdatePerformanceMetrics()
        {
            float avgFrameTime = _frameTimeAccumulator / _frameCount;
            CurrentFPS = 1f / avgFrameTime;
            AverageFrameTimeMs = avgFrameTime * 1000f;
            PeakFrameTimeMs = Mathf.Max(PeakFrameTimeMs, Time.unscaledDeltaTime * 1000f);

            if (CurrentFPS < 30f)
                Debug.LogWarning($"VEVE diagnostic: FPS dropped to {CurrentFPS:F1} (avg {AverageFrameTimeMs:F2}ms, peak {PeakFrameTimeMs:F2}ms)");

            _frameCount = 0;
            _frameTimeAccumulator = 0f;
            PeakFrameTimeMs = 0f;
        }

        private void RunHealthChecks()
        {
            AllSubsystemsHealthy = true;
            var coordinator = SimulationCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogError("VEVE diagnostic: SimulationCoordinator missing.");
                AllSubsystemsHealthy = false;
                return;
            }

            ValidateReference<EnvironmentSimulation>("EnvironmentSimulation");
            ValidateReference<MissionRuntime>("MissionRuntime");
            ValidateReference<CampaignState>("CampaignState");
            ValidateReference<PhysicsRealism>("PhysicsRealism");
            ValidateReference<RenderingRealism>("RenderingRealism");

            var rb = FindFirstObjectByType<Rigidbody>();
            if (rb != null && rb.interpolation != RigidbodyInterpolation.Interpolate)
                Debug.LogWarning("VEVE diagnostic: Rigidbody interpolation is disabled on " + rb.name);
        }

        private void DrawDebugOverlay()
        {
            Debug.Log($"VEVE diagnostic: FPS={CurrentFPS:F1} | AvgFrame={AverageFrameTimeMs:F2}ms | Healthy={AllSubsystemsHealthy}");
        }

        private void ValidateReference<T>(string name) where T : Component
        {
            if (FindFirstObjectByType<T>() == null)
            {
                Debug.LogError("VEVE diagnostic: missing " + name + " component.", this);
                AllSubsystemsHealthy = false;
            }
        }
    }
}
