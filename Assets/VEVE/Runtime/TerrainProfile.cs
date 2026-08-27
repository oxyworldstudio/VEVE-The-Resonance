using UnityEngine;

namespace VEVE
{
    [CreateAssetMenu(menuName = "VEVE/Simulation/Terrain Profile")]
    public sealed class TerrainProfile : ScriptableObject
    {
        [Range(0.1f, 2f)] public float speedFactor = 1f;
        [Range(0f, 2f)] public float noiseFactor = 1f;
        [Range(0f, 2f)] public float staminaCost = 1f;
    }
}
