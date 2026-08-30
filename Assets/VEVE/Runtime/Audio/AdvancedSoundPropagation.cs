using UnityEngine;
using VEVE.Realism;

namespace VEVE.Audio
{
    /// <summary>
    /// Enhanced sound propagation system with real-time raycast-based propagation,
    /// reflection calculation, and material-based absorption.
    /// </summary>
    public sealed class AdvancedSoundPropagation : MonoBehaviour
    {
        [System.Serializable]
        public class PropagationSettings
        {
            [Header("Raycast")]
            [SerializeField] private int maxReflections = 4;
            [SerializeField] private float rayDistance = 100f;
            [SerializeField] private LayerMask occlusionLayers = Physics.DefaultRaycastLayers;
            [SerializeField] private float reflectionEnergyThreshold = 0.01f;

            [Header("Reflection")]
            [SerializeField] private float reflectionCoefficient = 0.3f;
            [SerializeField] private float reflectionDamping = 0.85f;
            [SerializeField] private float propagationSpeed = 343f;

            [Header("Absorption")]
            [SerializeField] private float airAbsorption = 0.001f;
            [SerializeField] private float humidityAbsorption = 0.0005f;

            public int MaxReflections { get { return maxReflections; } }
            public float RayDistance { get { return rayDistance; } }
            public LayerMask OcclusionLayers { get { return occlusionLayers; } }
            public float ReflectionEnergyThreshold { get { return reflectionEnergyThreshold; } }
            public float ReflectionCoefficient { get { return reflectionCoefficient; } }
            public float ReflectionDamping { get { return reflectionDamping; } }
            public float PropagationSpeed { get { return propagationSpeed; } }
            public float AirAbsorption { get { return airAbsorption; } }
            public float HumidityAbsorption { get { return humidityAbsorption; } }
        }

        [Header("Settings")]
        [SerializeField] private PropagationSettings settings;
        [SerializeField] private RealismConfig realismConfig;
        [SerializeField] private Transform listener;

        public float CalculateHeardLoudness(float sourceLoudness, float distance, float absorption, float reflectionCoefficient = 0.3f)
        {
            if (realismConfig == null) return sourceLoudness * 0.5f;
            float distanceLoss = 1f / (1f + distance * distance * 0.02f);
            float reflectedEnergy = sourceLoudness * reflectionCoefficient * Mathf.Pow(0.5f, distance / 50f);
            float totalEnergy = (sourceLoudness * distanceLoss * Mathf.Clamp01(1f - absorption)) + reflectedEnergy;
            return Mathf.Max(0f, totalEnergy);
        }

        public float CalculateReverbDecay(float roomVolume, float surfaceAbsorption, float speedOfSound = 343f)
        {
            if (realismConfig != null && !realismConfig.EnableReverb) return 0f;
            float meanFreePath = 4f * roomVolume / (6f * Mathf.Max(1f, roomVolume));
            return (meanFreePath * surfaceAbsorption) / speedOfSound;
        }

        public float CalculateDopplerShift(float sourceVelocity, float listenerVelocity, float frequency, float speedOfSound = 343f)
        {
            float relativeVelocity = sourceVelocity - listenerVelocity;
            return frequency * (speedOfSound / (speedOfSound - relativeVelocity));
        }

        public float CalculateMaterialAbsorption(SurfaceMaterial material)
        {
            return material switch
            {
                SurfaceMaterial.Wood => 0.4f,
                SurfaceMaterial.Concrete => 0.15f,
                SurfaceMaterial.Metal => 0.1f,
                SurfaceMaterial.Glass => 0.05f,
                SurfaceMaterial.Fabric => 0.7f,
                SurfaceMaterial.Dirt => 0.5f,
                SurfaceMaterial.Ice => 0.03f,
                _ => 0.5f,
            };
        }

        public RaycastHit[] CastPropagationRays(Vector3 origin, int rayCount)
        {
            var hits = new System.Collections.Generic.List<RaycastHit>();
            for (int i = 0; i < rayCount; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                if (Physics.Raycast(origin, direction, out RaycastHit hit, settings.RayDistance, settings.OcclusionLayers))
                {
                    hits.Add(hit);
                }
            }
            return hits.ToArray();
        }

        public float CalculateReflectionEnergy(float incomingEnergy, float distance, float surfaceAbsorption)
        {
            float energyLoss = settings.ReflectionDamping * surfaceAbsorption;
            float distanceAttenuation = Mathf.Pow(0.5f, distance / settings.PropagationSpeed);
            return incomingEnergy * energyLoss * distanceAttenuation;
        }

        public float CalculatePropagationDelay(float distance)
        {
            return distance / settings.PropagationSpeed;
        }
    }
}
