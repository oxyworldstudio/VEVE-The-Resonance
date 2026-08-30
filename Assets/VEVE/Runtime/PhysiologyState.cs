using System;
using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    [Serializable]
    public struct PhysiologyState
    {
        [Range(0f, 100f)] public float bleeding;
        [Range(0f, 100f)] public float pain;
        [Range(0f, 100f)] public float stress;
        [Range(0f, 100f)] public float hydration;
        [Range(0f, 100f)] public float consciousness;
        [Range(0f, 100f)] public float fracture;
        [Min(30f)] public float heartRate;
        [Range(0f, 100f)] public float respiration;
        public float bloodLossVolume;

        public static PhysiologyState Stable => new PhysiologyState
        {
            hydration = 100f,
            consciousness = 100f,
            heartRate = 65f,
            respiration = 15f,
            bloodLossVolume = 0f
        };
    }

    public sealed class Physiology : MonoBehaviour
    {
        [SerializeField] private PhysiologyState state = new PhysiologyState
        {
            hydration = 100f,
            consciousness = 100f,
            heartRate = 65f,
            respiration = 15f,
            bloodLossVolume = 0f
        };

        [SerializeField] private RealismConfig realismConfig;

        public PhysiologyState State => state;

        public float MovementFactor
        {
            get
            {
                if (realismConfig == null) return Mathf.Clamp01(1f - state.pain * 0.004f - state.bleeding * 0.003f - state.fracture * 0.004f);
                float bloodLossRatio = state.bloodLossVolume / realismConfig.BloodVolumeLiters;
                float heartRateFactor = Mathf.Clamp01(1f - (state.heartRate - realismConfig.RestingHeartRateBPM) / (realismConfig.MaxHeartRateBPM - realismConfig.RestingHeartRateBPM));
                float respirationFactor = Mathf.Clamp01(1f - state.respiration / realismConfig.MaxRespirationRate);
                return Mathf.Clamp01(1f - state.pain * 0.004f - bloodLossRatio * 0.5f - state.fracture * 0.004f + heartRateFactor * 0.1f + respirationFactor * 0.1f);
            }
        }

        public float AimStabilityFactor
        {
            get
            {
                if (realismConfig == null) return Mathf.Clamp01(1f - state.pain * 0.005f - state.stress * 0.002f - state.respiration * 0.002f);
                float bloodLossRatio = state.bloodLossVolume / realismConfig.BloodVolumeLiters;
                float consciousnessFactor = Mathf.Clamp01(state.consciousness / 100f);
                return Mathf.Clamp01(1f - state.pain * 0.005f - bloodLossRatio * 0.7f - state.stress * 0.002f - state.respiration * 0.002f + consciousnessFactor * 0.2f);
            }
        }

        public void ApplyWound(float bleeding, float pain, float bloodLossRate = 0.1f)
        {
            state.bleeding = Mathf.Clamp(state.bleeding + Mathf.Max(0f, bleeding), 0f, 100f);
            state.pain = Mathf.Clamp(state.pain + Mathf.Max(0f, pain), 0f, 100f);
            state.bloodLossVolume += bloodLossRate * Time.deltaTime;
            float bloodLossRatio = realismConfig != null ? state.bloodLossVolume / realismConfig.BloodVolumeLiters : state.bloodLossVolume / 5f;
            state.consciousness = Mathf.Clamp(state.consciousness - bloodLossRatio * 50f - pain * 0.1f, 0f, 100f);
            state.heartRate = Mathf.Clamp(state.heartRate + pain * 0.25f + bloodLossRatio * 100f, 30f, 220f);
            state.respiration = Mathf.Clamp(state.respiration + pain * 0.15f + bloodLossRatio * 80f, 8f, 60f);
        }

        public void ApplyFracture(float severity)
        {
            state.fracture = Mathf.Clamp(state.fracture + Mathf.Max(0f, severity), 0f, 100f);
        }

        public void Treat(float bleedingReduction, float painReduction)
        {
            state.bleeding = Mathf.Max(0f, state.bleeding - Mathf.Max(0f, bleedingReduction));
            state.pain = Mathf.Max(0f, state.pain - Mathf.Max(0f, painReduction));
        }

        public void ApplyConsciousnessRecovery(float amount)
        {
            state.consciousness = Mathf.Clamp(state.consciousness + Mathf.Max(0f, amount), 0f, 100f);
        }

        private void Update()
        {
            float bloodLossRatio = realismConfig != null ? state.bloodLossVolume / realismConfig.BloodVolumeLiters : state.bloodLossVolume / 5f;
            state.bleeding = Mathf.Max(0f, state.bleeding - Time.deltaTime * 0.05f * (1f - bloodLossRatio));
            state.pain = Mathf.Max(0f, state.pain - Time.deltaTime * 0.08f);
            state.hydration = Mathf.Max(0f, state.hydration - Time.deltaTime * 0.002f);
            float targetHeartRate = 65f + state.stress * 0.5f + bloodLossRatio * 80f;
            state.heartRate = Mathf.MoveTowards(state.heartRate, targetHeartRate, Time.deltaTime * 2f);
            float targetRespiration = 15f + state.stress * 0.2f + bloodLossRatio * 60f;
            state.respiration = Mathf.MoveTowards(state.respiration, targetRespiration, Time.deltaTime * 0.8f);
            if (state.respiration > 20f) state.stress = Mathf.Clamp(state.stress + Time.deltaTime * 0.1f, 0f, 100f);
        }
    }
}
