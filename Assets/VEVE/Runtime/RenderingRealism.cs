using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public sealed class RenderingRealism : MonoBehaviour
    {
        [SerializeField] private RealismConfig realismConfig;

        private void Start()
        {
            if (realismConfig == null) return;

            QualitySettings.lodBias = realismConfig.LODBias;
            QualitySettings.shadowDistance = realismConfig.ShadowDistance;
            QualitySettings.shadowCascades = realismConfig.ShadowCascades;
            QualitySettings.antiAliasing = realismConfig.EnableAntiAliasing ? 8 : 0;
            QualitySettings.vSyncCount = realismConfig.EnableVSync ? 1 : 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.softParticles = true;
            QualitySettings.softVegetation = true;
            Application.targetFrameRate = realismConfig.TargetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        public void ApplyUltraPreset()
        {
            if (realismConfig == null || !realismConfig.ForceUltraQuality) return;

            QualitySettings.lodBias = 2.5f;
            QualitySettings.shadowDistance = 500f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.antiAliasing = 8;
            QualitySettings.vSyncCount = 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.softParticles = true;
            QualitySettings.softVegetation = true;
            Application.targetFrameRate = realismConfig.TargetFrameRate;
        }
    }
}
