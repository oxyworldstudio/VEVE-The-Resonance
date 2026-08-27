using UnityEngine;

namespace VEVE
{
    public sealed class SimulationDiagnostics : MonoBehaviour
    {
        [SerializeField] private bool logOnStart = true;

        private void Start()
        {
            if (!logOnStart) return;
            ValidateReference<EnvironmentSimulation>("EnvironmentSimulation");
            ValidateReference<MissionRuntime>("MissionRuntime");
            ValidateReference<CampaignState>("CampaignState");
            ValidateReference<PhysicalInventory>("PhysicalInventory");
            ValidateReference<MovementSimulation>("MovementSimulation");
        }

        private void ValidateReference<T>(string name) where T : Component
        {
            if (FindFirstObjectByType<T>() == null)
                Debug.LogError("VEVE diagnostic: missing " + name + " component.", this);
        }
    }
}
