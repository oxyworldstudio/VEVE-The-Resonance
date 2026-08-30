using UnityEngine;
using System.Diagnostics;

namespace VEVE
{
    public sealed class PerformanceManager : MonoBehaviour
    {
        [Header("Performance Targets")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private float performanceLogInterval = 5f;

        [Header("LOD Management")]
        [SerializeField] private float lodBias = 2f;
        [SerializeField] private bool enableDynamicLOD = true;
        [SerializeField] private float lodScaleDistance = 1f;

        [Header("Adaptive Quality")]
        [SerializeField] private bool enableAdaptiveQuality = true;
        [SerializeField] private float lowFPSThreshold = 30f;
        [SerializeField] private float highFPSThreshold = 55f;
        [SerializeField] private float adaptationCooldown = 10f;

        public int TargetFrameRate { get => targetFrameRate; set => targetFrameRate = Mathf.Clamp(value, 30, 240); }
        public float CurrentFPS { get; private set; }
        public float AverageFrameTimeMs { get; private set; }
        public float MinFPS { get; private set; } = 999f;
        public float MaxFPS { get; private set; } = 0f;
        public bool IsPerformanceAcceptable { get; private set; } = true;

        private float _timer;
        private int _frameCount;
        private float _frameTimeAccumulator;
        private float _adaptationTimer;
        private Stopwatch _sw = new Stopwatch();

        private void Update()
        {
            MeasurePerformance();
            if (enableAdaptiveQuality && _adaptationTimer <= 0f)
                UpdateAdaptiveQuality();
        }

        private void MeasurePerformance()
        {
            _frameCount++;
            _frameTimeAccumulator += Time.unscaledDeltaTime;
            _timer += Time.unscaledDeltaTime;

            if (_timer >= performanceLogInterval)
            {
                AverageFrameTimeMs = (_frameTimeAccumulator / _frameCount) * 1000f;
                CurrentFPS = 1f / (AverageFrameTimeMs / 1000f);

                if (CurrentFPS > 0f)
                {
                    MinFPS = Mathf.Min(MinFPS, CurrentFPS);
                    MaxFPS = Mathf.Max(MaxFPS, CurrentFPS);
                }

                IsPerformanceAcceptable = CurrentFPS >= lowFPSThreshold;
                LogPerformanceReport();
                _frameCount = 0;
                _frameTimeAccumulator = 0f;
                _timer = 0f;
            }
        }

        private void UpdateAdaptiveQuality()
        {
            if (CurrentFPS < lowFPSThreshold)
            {
                DecreaseQuality();
                _adaptationTimer = adaptationCooldown;
            }
            else if (CurrentFPS > highFPSThreshold)
            {
                IncreaseQuality();
                _adaptationTimer = adaptationCooldown;
            }
        }

        private void DecreaseQuality()
        {
            var preset = FindFirstObjectByType<QualityPreset>();
            if (preset == null) return;

            switch (preset.CurrentLevel)
            {
                case VEVE.Realism.QualityLevel.Ultra:
                    preset.CurrentLevel = VEVE.Realism.QualityLevel.High;
                    QualitySettings.lodBias = Mathf.Max(1f, lodBias - 0.5f);
                    break;
                case VEVE.Realism.QualityLevel.High:
                    preset.CurrentLevel = VEVE.Realism.QualityLevel.Medium;
                    QualitySettings.shadowDistance = Mathf.Max(100f, QualitySettings.shadowDistance - 50f);
                    break;
                case VEVE.Realism.QualityLevel.Medium:
                    preset.CurrentLevel = VEVE.Realism.QualityLevel.Low;
                    QualitySettings.antiAliasing = 0;
                    break;
                default:
                    return;
            }
            UnityEngine.Debug.LogWarning($"VEVE adaptive quality: decreased to {preset.CurrentLevel}");
        }

        private void IncreaseQuality()
        {
            var preset = FindFirstObjectByType<QualityPreset>();
            if (preset == null) return;

            switch (preset.CurrentLevel)
            {
                case VEVE.Realism.QualityLevel.Low:
                    preset.CurrentLevel = VEVE.Realism.QualityLevel.Medium;
                    QualitySettings.antiAliasing = 4;
                    break;
                case VEVE.Realism.QualityLevel.Medium:
                    preset.CurrentLevel = VEVE.Realism.QualityLevel.High;
                    QualitySettings.shadowDistance = Mathf.Min(500f, QualitySettings.shadowDistance + 50f);
                    break;
                case VEVE.Realism.QualityLevel.High:
                    preset.CurrentLevel = VEVE.Realism.QualityLevel.Ultra;
                    QualitySettings.lodBias = Mathf.Min(4f, lodBias + 0.5f);
                    break;
                default:
                    return;
            }
            UnityEngine.Debug.Log($"VEVE adaptive quality: increased to {preset.CurrentLevel}");
        }

        private void LogPerformanceReport()
        {
            UnityEngine.Debug.Log($"VEVE Performance: FPS={CurrentFPS:F1} | Min={MinFPS:F1} | Max={MaxFPS:F1} | AvgFrame={AverageFrameTimeMs:F2}ms | Acceptable={IsPerformanceAcceptable}");
        }

        public void ForceLODUpdate()
        {
            if (enableDynamicLOD)
            {
                QualitySettings.lodBias = lodScaleDistance * lodBias;
            }
        }

        private void OnDestroy()
        {
            _sw?.Stop();
        }
    }
}
