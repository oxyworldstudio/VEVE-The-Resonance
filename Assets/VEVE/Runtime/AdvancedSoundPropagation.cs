using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public sealed class AdvancedSoundPropagation : MonoBehaviour
    {
        [SerializeField] private RealismConfig realismConfig;

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
    }
}
