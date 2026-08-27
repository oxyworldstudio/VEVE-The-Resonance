using UnityEngine;

namespace VEVE
{
    public sealed class SimulationCoordinator : MonoBehaviour
    {
        [SerializeField] private EnvironmentSimulation environment;
        [SerializeField] private MissionRuntime mission;
        [SerializeField] private CampaignState campaign;

        public EnvironmentSimulation Environment => environment;
        public MissionRuntime Mission => mission;
        public CampaignState Campaign => campaign;

        private void Awake()
        {
            if (environment == null) environment = FindFirstObjectByType<EnvironmentSimulation>();
            if (mission == null) mission = FindFirstObjectByType<MissionRuntime>();
            if (campaign == null) campaign = FindFirstObjectByType<CampaignState>();
            if (environment == null || mission == null || campaign == null)
                Debug.LogError("VEVE simulation is missing a required subsystem reference.", this);
        }
    }
}
