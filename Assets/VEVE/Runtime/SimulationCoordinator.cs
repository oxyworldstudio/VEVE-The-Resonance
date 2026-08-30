using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public sealed class SimulationCoordinator : MonoBehaviour
    {
        [SerializeField] private EnvironmentSimulation environment;
        [SerializeField] private MissionRuntime mission;
        [SerializeField] private CampaignState campaign;
        [SerializeField] private RealismConfig realismConfig;

        public EnvironmentSimulation Environment => environment;
        public MissionRuntime Mission => mission;
        public CampaignState Campaign => campaign;
        public RealismConfig RealismConfig => realismConfig;

        private void Awake()
        {
            if (environment == null) environment = FindFirstObjectByType<EnvironmentSimulation>();
            if (mission == null) mission = FindFirstObjectByType<MissionRuntime>();
            if (campaign == null) campaign = FindFirstObjectByType<CampaignState>();
            if (environment == null || mission == null || campaign == null)
                Debug.LogError("VEVE simulation is missing a required subsystem reference.", this);

            ApplyRealismConfig();
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
    }
}
