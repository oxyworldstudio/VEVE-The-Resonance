using UnityEngine;

namespace VEVE
{
    [CreateAssetMenu(menuName = "VEVE/Simulation/Material Definition")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        [Min(0.01f)] public float density = 1f;
        [Min(0f)] public float ballisticResistance = 1f;
        [Min(0f)] public float acousticAbsorption = 0.2f;
        [Range(0f, 1f)] public float lightTransmission;

        public float RemainingEnergy(float incomingEnergy, float thickness)
        {
            return Mathf.Max(0f, incomingEnergy - ballisticResistance * density * Mathf.Max(0f, thickness));
        }
    }
}
