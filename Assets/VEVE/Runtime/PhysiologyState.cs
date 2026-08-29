using System;
using UnityEngine;

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

        public static PhysiologyState Stable => new PhysiologyState
        {
            hydration = 100f,
            consciousness = 100f,
            heartRate = 65f,
            respiration = 15f
        };
    }

    public sealed class Physiology : MonoBehaviour
    {
        [SerializeField] private PhysiologyState state = new PhysiologyState
        {
            hydration = 100f,
            consciousness = 100f,
            heartRate = 65f,
            respiration = 15f
        };

        public PhysiologyState State => state;
        public float MovementFactor => Mathf.Clamp01(1f - state.pain * 0.004f - state.bleeding * 0.003f - state.fracture * 0.004f);
        public float AimStabilityFactor => Mathf.Clamp01(1f - state.pain * 0.005f - state.stress * 0.002f - state.respiration * 0.002f);

        public void ApplyWound(float bleeding, float pain)
        {
            state.bleeding = Mathf.Clamp(state.bleeding + Mathf.Max(0f, bleeding), 0f, 100f);
            state.pain = Mathf.Clamp(state.pain + Mathf.Max(0f, pain), 0f, 100f);
            state.consciousness = Mathf.Clamp(state.consciousness - pain * 0.1f, 0f, 100f);
            state.heartRate = Mathf.Clamp(state.heartRate + pain * 0.25f, 30f, 220f);
            state.respiration = Mathf.Clamp(state.respiration + pain * 0.15f, 8f, 60f);
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
            state.bleeding = Mathf.Max(0f, state.bleeding - Time.deltaTime * 0.05f);
            state.pain = Mathf.Max(0f, state.pain - Time.deltaTime * 0.08f);
            state.hydration = Mathf.Max(0f, state.hydration - Time.deltaTime * 0.002f);
            state.heartRate = Mathf.MoveTowards(state.heartRate, 65f + state.stress * 0.5f, Time.deltaTime * 2f);
            state.respiration = Mathf.MoveTowards(state.respiration, 15f + state.stress * 0.2f, Time.deltaTime * 0.8f);
            if (state.respiration > 20f) state.stress = Mathf.Clamp(state.stress + Time.deltaTime * 0.1f, 0f, 100f);
        }
    }
}
