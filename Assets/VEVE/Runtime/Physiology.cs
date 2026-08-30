using System;
using System.Collections.Generic;
using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    /// <summary>
    /// Real-time physiology simulation with blood pressure, cardiac output, respiratory rate, and medical state machine.
    /// </summary>
    public sealed class Physiology : MonoBehaviour
    {
        [Header("Physiology Parameters")]
        [SerializeField] private PhysiologyState state = new PhysiologyState
        {
            hydration = 100f,
            consciousness = 100f,
            heartRate = 65f,
            respiration = 15f,
            bloodPressureSystolic = 120f,
            bloodPressureDiastolic = 80f,
            cardiacOutput = 5f,
            bloodOxygenSaturation = 98f,
            bloodLossVolume = 0f,
            fatigue = 0f,
            infection = 0f,
            medicalState = MedicalState.Healthy,
            activeWounds = new List<Wound>(),
            activeFractures = new List<FractureData>()
        };

        [SerializeField] private VEVE.Realism.RealismConfig realismConfig;
        [SerializeField] private float woundProgressionRate = 0.02f;
        [SerializeField] private float infectionAccumulationRate = 0.01f;
        [SerializeField] private float necrosisThreshold = 80f;

        public PhysiologyState State => state;
        public RealismConfig RealismConfig => realismConfig;

        public float MovementFactor
        {
            get
            {
                if (realismConfig == null) return Mathf.Clamp01(1f - state.pain * 0.004f - state.bleeding * 0.003f - state.fracture * 0.004f - state.fatigue * 0.005f);
                float bloodLossRatio = state.bloodLossVolume / realismConfig.BloodVolumeLiters;
                float heartRateFactor = Mathf.Clamp01(1f - (state.heartRate - realismConfig.RestingHeartRateBPM) / (realismConfig.MaxHeartRateBPM - realismConfig.RestingHeartRateBPM));
                float respirationFactor = Mathf.Clamp01(1f - state.respiration / realismConfig.MaxRespirationRate);
                return Mathf.Clamp01(1f - state.pain * 0.004f - bloodLossRatio * 0.5f - state.fracture * 0.004f - state.fatigue * 0.005f + heartRateFactor * 0.1f + respirationFactor * 0.1f);
            }
        }

        public float AimStabilityFactor
        {
            get
            {
                if (realismConfig == null) return Mathf.Clamp01(1f - state.pain * 0.005f - state.stress * 0.002f - state.respiration * 0.002f);
                float bloodLossRatio = state.bloodLossVolume / realismConfig.BloodVolumeLiters;
                float consciousnessFactor = Mathf.Clamp01(state.consciousness / 100f);
                return Mathf.Clamp01(1f - state.pain * 0.005f - bloodLossRatio * 0.7f - state.stress * 0.002f - state.respiration * 0.002f + consciousnessFactor * 0.2f - state.fatigue * 0.003f);
            }
        }

        public float StaminaFactor
        {
            get
            {
                float fatiguePenalty = state.fatigue * 0.008f;
                float heartRatePenalty = Mathf.Clamp01((state.heartRate - 65f) / 100f) * 0.2f;
                return Mathf.Clamp01(1f - fatiguePenalty - heartRatePenalty);
            }
        }

        public float ConsciousnessProbability
        {
            get
            {
                if (realismConfig == null) return Mathf.Clamp01(state.consciousness / 100f);
                float bloodLossFactor = Mathf.Clamp01(state.bloodLossVolume / realismConfig.BloodLossLethalThreshold);
                float bloodOxygenFactor = Mathf.Clamp01(state.bloodOxygenSaturation / 100f);
                float painFactor = Mathf.Clamp01(state.pain / 100f);
                float heartRateFactor = Mathf.Clamp01((state.heartRate - 30f) / 190f);
                return Mathf.Clamp01(1f - bloodLossFactor * 0.8f - painFactor * 0.1f - heartRateFactor * 0.1f + bloodOxygenFactor * 0.3f);
            }
        }

        public void ApplyWound(float bleeding, float pain, float bloodLossRate = 0.1f, HitZone zone = HitZone.UpperTorso, InjuryType injuryType = InjuryType.DeepLaceration)
        {
            state.bleeding = Mathf.Clamp(state.bleeding + Mathf.Max(0f, bleeding), 0f, 100f);
            state.pain = Mathf.Clamp(state.pain + Mathf.Max(0f, pain), 0f, 100f);
            state.bloodLossVolume += bloodLossRate * Time.deltaTime;
            float bloodLossRatio = realismConfig != null ? state.bloodLossVolume / realismConfig.BloodVolumeLiters : state.bloodLossVolume / 5f;
            state.consciousness = Mathf.Clamp(state.consciousness - bloodLossRatio * 50f - pain * 0.1f, 0f, 100f);
            state.heartRate = Mathf.Clamp(state.heartRate + pain * 0.25f + bloodLossRatio * 100f, 30f, 220f);
            state.respiration = Mathf.Clamp(state.respiration + pain * 0.15f + bloodLossRatio * 80f, 8f, 60f);
            state.bloodPressureSystolic = Mathf.Clamp(state.bloodPressureSystolic - bloodLossRatio * 40f, 60f, 180f);
            state.bloodPressureDiastolic = Mathf.Clamp(state.bloodPressureDiastolic - bloodLossRatio * 20f, 40f, 120f);
            state.cardiacOutput = Mathf.Clamp(state.cardiacOutput + bloodLossRatio * 2f, 2f, 15f);
            state.bloodOxygenSaturation = Mathf.Clamp(state.bloodOxygenSaturation - bloodLossRatio * 5f, 70f, 100f);
            UpdateMedicalState();
            TrackWound(bleeding, pain, bloodLossRate, zone, injuryType);
        }

        public void ApplyFracture(float severity, BoneType bone = BoneType.FemurLeft, bool isCompound = false, float displacement = 0f)
        {
            state.fracture = Mathf.Clamp(state.fracture + Mathf.Max(0f, severity), 0f, 100f);
            state.pain = Mathf.Clamp(state.pain + severity * 0.5f, 0f, 100f);
            state.heartRate = Mathf.Clamp(state.heartRate + severity * 0.3f, 30f, 220f);
            UpdateMedicalState();
            TrackFracture(severity, bone, isCompound, displacement);
        }

        public void Treat(float bleedingReduction, float painReduction)
        {
            state.bleeding = Mathf.Max(0f, state.bleeding - Mathf.Max(0f, bleedingReduction));
            state.pain = Mathf.Max(0f, state.pain - Mathf.Max(0f, painReduction));
            if (state.bleeding < 5f && state.pain < 20f && state.medicalState == MedicalState.InTreatment)
            {
                state.medicalState = MedicalState.Recovering;
            }
        }

        public void ApplyConsciousnessRecovery(float amount)
        {
            state.consciousness = Mathf.Clamp(state.consciousness + Mathf.Max(0f, amount), 0f, 100f);
            if (state.consciousness > 70f && state.medicalState == MedicalState.Unconscious)
            {
                state.medicalState = MedicalState.Wounded;
            }
        }

        public void ApplyFatigue(float amount)
        {
            state.fatigue = Mathf.Clamp(state.fatigue + Mathf.Max(0f, amount), 0f, 100f);
        }

        public void ApplyHydrationLoss(float amount)
        {
            state.hydration = Mathf.Max(0f, state.hydration - Mathf.Max(0f, amount));
            if (state.hydration < 20f)
            {
                state.heartRate = Mathf.Clamp(state.heartRate + Time.deltaTime * 2f, 30f, 220f);
                state.respiration = Mathf.Clamp(state.respiration + Time.deltaTime * 0.5f, 8f, 60f);
                state.pain = Mathf.Clamp(state.pain + Time.deltaTime * 0.1f, 0f, 100f);
            }
        }

        public void Recover(float recoveryRate)
        {
            state.pain = Mathf.Max(0f, state.pain - Time.deltaTime * recoveryRate * 0.1f);
            state.fatigue = Mathf.Max(0f, state.fatigue - Time.deltaTime * recoveryRate * 0.05f);
            state.hydration = Mathf.Min(100f, state.hydration + Time.deltaTime * recoveryRate * 0.02f);
            float targetHeartRate = 65f + state.stress * 0.5f;
            state.heartRate = Mathf.MoveTowards(state.heartRate, targetHeartRate, Time.deltaTime * recoveryRate * 2f);
            float targetRespiration = 15f + state.stress * 0.2f;
            state.respiration = Mathf.MoveTowards(state.respiration, targetRespiration, Time.deltaTime * recoveryRate * 0.8f);
            state.bloodPressureSystolic = Mathf.MoveTowards(state.bloodPressureSystolic, 120f, Time.deltaTime * recoveryRate);
            state.bloodPressureDiastolic = Mathf.MoveTowards(state.bloodPressureDiastolic, 80f, Time.deltaTime * recoveryRate);
            state.cardiacOutput = Mathf.MoveTowards(state.cardiacOutput, 5f, Time.deltaTime * recoveryRate);
            state.bloodOxygenSaturation = Mathf.MoveTowards(state.bloodOxygenSaturation, 98f, Time.deltaTime * recoveryRate);
            state.bloodLossVolume = Mathf.Max(0f, state.bloodLossVolume - Time.deltaTime * recoveryRate * 0.01f);
            state.consciousness = Mathf.Min(100f, state.consciousness + Time.deltaTime * recoveryRate * 0.05f);
            for (int i = 0; i < state.activeWounds.Count; i++)
            {
                var wound = state.activeWounds[i];
                wound.timeSinceTreatment += Time.deltaTime;
                if (wound.isTreated)
                {
                    wound.bleedingRate = Mathf.Max(0f, wound.bleedingRate - Time.deltaTime * recoveryRate * 0.1f);
                    wound.treatmentProgress = Mathf.Clamp01(wound.treatmentProgress + Time.deltaTime * recoveryRate * 0.05f);
                }
                wound.timeSinceInjury += Time.deltaTime;
                wound.infectionRisk += Time.deltaTime * infectionAccumulationRate;
                if (wound.timeSinceInjury > 300f && wound.infectionRisk > necrosisThreshold)
                {
                    wound.painLevel += 20f;
                    state.pain = Mathf.Clamp(state.pain + 20f, 0f, 100f);
                }
                state.activeWounds[i] = wound;
            }
            if (state.bleeding < 1f && state.pain < 10f && state.medicalState == MedicalState.Recovering)
            {
                state.medicalState = MedicalState.Healthy;
            }
            UpdateMedicalState();
        }

        public void UpdateBloodPressure(float systolic, float diastolic)
        {
            state.bloodPressureSystolic = Mathf.Clamp(systolic, 60f, 200f);
            state.bloodPressureDiastolic = Mathf.Clamp(diastolic, 40f, 130f);
        }

        public void UpdateCardiacOutput(float output)
        {
            state.cardiacOutput = Mathf.Clamp(output, 2f, 20f);
        }

        public void UpdateRespiratoryRate(float rate)
        {
            state.respiration = Mathf.Clamp(rate, 6f, 60f);
        }

        public void ApplyStress(float amount)
        {
            state.stress = Mathf.Clamp(state.stress + Mathf.Max(0f, amount), 0f, 100f);
            state.heartRate = Mathf.Clamp(state.heartRate + amount * 0.3f, 30f, 220f);
            state.respiration = Mathf.Clamp(state.respiration + amount * 0.1f, 8f, 60f);
        }

        public void SetMedicalState(MedicalState newState)
        {
            state.medicalState = newState;
        }

        private void TrackWound(float bleeding, float pain, float bloodLossRate, HitZone zone, InjuryType injuryType)
        {
            Wound wound = new Wound
            {
                woundId = Guid.NewGuid().ToString(),
                zone = zone,
                injuryType = injuryType,
                severity = bleeding + pain,
                tissueDamage = bleeding * 0.5f + pain * 0.3f,
                bleedingRate = bloodLossRate,
                painLevel = pain,
                isTreated = false,
                treatmentProgress = 0f,
                timeSinceInjury = 0f,
                timeSinceTreatment = 0f,
                isFracture = false,
                fractureDisplacement = 0f,
                infectionRisk = 0f
            };
            state.activeWounds.Add(wound);
            if (state.activeWounds.Count > 20) state.activeWounds.RemoveAt(0);
        }

        private void TrackFracture(float severity, BoneType bone, bool isCompound, float displacement)
        {
            FractureData fracture = new FractureData
            {
                bone = bone,
                displacement = displacement,
                fragmentation = severity * 0.3f,
                isCompound = isCompound,
                isStabilized = false,
                healingProgress = 0f
            };
            state.activeFractures.Add(fracture);
            if (state.activeFractures.Count > 15) state.activeFractures.RemoveAt(0);
        }

        private void UpdateMedicalState()
        {
            float totalInjury = state.pain + state.bleeding + state.fracture;
            if (state.bloodLossVolume > (realismConfig != null ? realismConfig.BloodLossLethalThreshold : 2f))
            {
                state.medicalState = MedicalState.Deceased;
                state.consciousness = 0f;
            }
            else if (state.consciousness < 10f)
            {
                state.medicalState = MedicalState.Unconscious;
            }
            else if (totalInjury > 50f)
            {
                state.medicalState = MedicalState.CriticallyInjured;
            }
            else if (totalInjury > 10f)
            {
                state.medicalState = MedicalState.Wounded;
            }
            else if (state.medicalState == MedicalState.InTreatment)
            {
                state.medicalState = MedicalState.Recovering;
            }
        }

        private void Update()
        {
            float bloodLossRatio = realismConfig != null ? state.bloodLossVolume / realismConfig.BloodVolumeLiters : state.bloodLossVolume / 5f;
            state.bleeding = Mathf.Max(0f, state.bleeding - Time.deltaTime * 0.05f * (1f - bloodLossRatio));
            state.pain = Mathf.Max(0f, state.pain - Time.deltaTime * 0.08f);
            state.hydration = Mathf.Max(0f, state.hydration - Time.deltaTime * 0.002f);
            float targetHeartRate = 65f + state.stress * 0.5f + bloodLossRatio * 80f + state.fatigue * 0.3f;
            state.heartRate = Mathf.MoveTowards(state.heartRate, targetHeartRate, Time.deltaTime * 2f);
            float targetRespiration = 15f + state.stress * 0.2f + bloodLossRatio * 60f + state.fatigue * 0.2f;
            state.respiration = Mathf.MoveTowards(state.respiration, targetRespiration, Time.deltaTime * 0.8f);
            if (state.respiration > 20f) state.stress = Mathf.Clamp(state.stress + Time.deltaTime * 0.1f, 0f, 100f);
            float targetBP = 120f - bloodLossRatio * 50f;
            state.bloodPressureSystolic = Mathf.MoveTowards(state.bloodPressureSystolic, targetBP, Time.deltaTime * 1f);
            state.bloodPressureDiastolic = Mathf.MoveTowards(state.bloodPressureDiastolic, targetBP * 0.67f, Time.deltaTime * 1f);
            state.cardiacOutput = Mathf.MoveTowards(state.cardiacOutput, 5f + bloodLossRatio * 3f, Time.deltaTime * 0.5f);
            state.bloodOxygenSaturation = Mathf.MoveTowards(state.bloodOxygenSaturation, 98f - bloodLossRatio * 8f, Time.deltaTime * 0.3f);
        }
    }
}
