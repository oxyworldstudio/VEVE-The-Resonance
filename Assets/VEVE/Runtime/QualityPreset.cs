using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE
{
    public sealed class QualityPreset : MonoBehaviour
    {
        [Header("Runtime Quality Settings")]
        [SerializeField] private VEVE.Realism.QualityLevel currentLevel = VEVE.Realism.QualityLevel.High;
        [SerializeField] private RealismConfig realismConfig;
        [SerializeField] private bool enableAdaptiveQuality = true;
        [SerializeField] private float adaptationSmoothing = 0.1f;

        public VEVE.Realism.QualityLevel CurrentLevel
        {
            get => currentLevel;
            set
            {
                currentLevel = value;
                ApplyRuntimeSettings();
                EventBus.PublishGlobal(new QualityPresetChangedEvent(value));
            }
        }

        public RealismConfig RealismConfig => realismConfig;

        private void Start()
        {
            ApplyRuntimeSettings();
        }

        private void Update()
        {
            if (enableAdaptiveQuality)
                AdaptQuality();
        }

        public void ApplyRuntimeSettings()
        {
            if (realismConfig == null) return;

            QualitySettings.lodBias = realismConfig.LODBias;
            QualitySettings.shadowDistance = realismConfig.ShadowDistance;
            QualitySettings.shadowCascades = realismConfig.ShadowCascades;
            QualitySettings.antiAliasing = realismConfig.EnableAntiAliasing ? realismConfig.AntiAliasingSamples : 0;
            QualitySettings.vSyncCount = realismConfig.EnableVSync ? 1 : 0;
            Application.targetFrameRate = realismConfig.TargetFrameRate;
            Physics.defaultSolverIterations = realismConfig.PhysicsSolverIterations;
            Physics.defaultSolverVelocityIterations = realismConfig.PhysicsSolverVelocityIterations;
            Time.fixedDeltaTime = realismConfig.FixedDeltaTime;
            Time.maximumDeltaTime = realismConfig.MaximumDeltaTime;
        }

        private void AdaptQuality()
        {
            var diagnostics = FindFirstObjectByType<SimulationDiagnostics>();
            if (diagnostics == null) return;

            float fps = diagnostics.CurrentFPS;
            if (fps < 30f && currentLevel > VEVE.Realism.QualityLevel.Low)
            {
                CurrentLevel = currentLevel - 1;
                Debug.Log($"VEVE adaptive quality: downgraded to {currentLevel} due to low FPS ({fps:F1})");
            }
            else if (fps > 55f && currentLevel < VEVE.Realism.QualityLevel.Ultra)
            {
                CurrentLevel = currentLevel + 1;
                Debug.Log($"VEVE adaptive quality: upgraded to {currentLevel} due to high FPS ({fps:F1})");
            }
        }
    }
}
