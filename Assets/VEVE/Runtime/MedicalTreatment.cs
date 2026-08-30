using System;
using UnityEngine;

namespace VEVE
{
    /// <summary>
    /// Medical treatment system with realistic treatment times, effectiveness based on injury severity, and medic skill modifiers.
    /// </summary>
    public sealed class MedicalTreatment : MonoBehaviour
    {
        [Header("Medic Parameters")]
        [SerializeField] private float baseSkillLevel = 0.5f;
        [SerializeField] private float stressPenalty = 0.2f;
        [SerializeField] private float fatiguePenalty = 0.15f;
        [SerializeField] private float equipmentQualityBonus = 0.1f;

        private InjurySystem injurySystem;
        private Physiology physiology;
        private float currentTreatmentTimeRemaining;
        private TreatmentType currentTreatment;
        private HitZone currentTargetZone;
        private bool isTreating;

        public event Action<TreatmentType, HitZone, float> OnTreatmentStarted;
        public event Action<TreatmentType, HitZone, float> OnTreatmentCompleted;
        public event Action<string, float> OnTreatmentProgress;

        private void Awake()
        {
            injurySystem = GetComponent<InjurySystem>();
            physiology = GetComponent<Physiology>();
        }

        /// <summary>
        /// Starts a medical treatment on a specific hit zone.
        /// </summary>
        public bool StartTreatment(TreatmentType treatment, HitZone zone)
        {
            if (isTreating) return false;
            if (physiology == null || injurySystem == null) return false;
            float skillLevel = CalculateEffectiveSkillLevel();
            float treatmentTime = injurySystem.CalculateTreatmentTime(treatment, skillLevel);
            if (treatmentTime <= 0f) return false;
            currentTreatment = treatment;
            currentTargetZone = zone;
            currentTreatmentTimeRemaining = treatmentTime;
            isTreating = true;
            OnTreatmentStarted?.Invoke(treatment, zone, treatmentTime);
            return true;
        }

        /// <summary>
        /// Cancels the current treatment.
        /// </summary>
        public void CancelTreatment()
        {
            if (!isTreating) return;
            isTreating = false;
            currentTreatmentTimeRemaining = 0f;
            currentTreatment = TreatmentType.None;
            currentTargetZone = HitZone.UpperTorso;
        }

        /// <summary>
        /// Updates the current treatment progress.
        /// </summary>
        public void UpdateTreatment(float deltaTime)
        {
            if (!isTreating) return;
            currentTreatmentTimeRemaining -= deltaTime;
            float totalTime = injurySystem.CalculateTreatmentTime(currentTreatment, CalculateEffectiveSkillLevel());
            float progress = 1f - (currentTreatmentTimeRemaining / totalTime);
            OnTreatmentProgress?.Invoke(currentTreatment.ToString(), Mathf.Clamp01(progress));
            if (currentTreatmentTimeRemaining <= 0f)
            {
                CompleteTreatment();
            }
        }

        /// <summary>
        /// Completes the current treatment with effectiveness calculation.
        /// </summary>
        public float CompleteTreatment()
        {
            if (!isTreating) return 0f;
            float effectiveness = CalculateTreatmentEffectiveness(currentTreatment, currentTargetZone);
            ApplyTreatmentEffects(currentTreatment, currentTargetZone, effectiveness);
            isTreating = false;
            currentTreatment = TreatmentType.None;
            currentTargetZone = HitZone.UpperTorso;
            OnTreatmentCompleted?.Invoke(currentTreatment, currentTargetZone, effectiveness);
            return effectiveness;
        }

        /// <summary>
        /// Applies immediate pain management.
        /// </summary>
        public void ApplyPainManagement(float painReduction)
        {
            if (physiology == null) return;
            float skillLevel = CalculateEffectiveSkillLevel();
            float actualReduction = painReduction * (0.5f + skillLevel * 0.5f);
            physiology.Treat(0f, actualReduction);
        }

        /// <summary>
        /// Applies antibiotics to reduce infection risk.
        /// </summary>
        public void ApplyAntibiotics(float effectiveness)
        {
            if (physiology == null) return;
            physiology.Treat(0f, 2f);
            physiology.ApplyConsciousnessRecovery(5f);
        }

        /// <summary>
        /// Calculates the effectiveness of a treatment based on injury severity and medic skill.
        /// </summary>
        public float CalculateTreatmentEffectiveness(TreatmentType treatment, HitZone zone)
        {
            float skillLevel = CalculateEffectiveSkillLevel();
            float baseEffectiveness = injurySystem.GetTreatmentEffectiveness(treatment, zone);
            float severityPenalty = GetSeverityPenalty(zone);
            float equipmentBonus = equipmentQualityBonus;
            return Mathf.Clamp01(baseEffectiveness * skillLevel * (1f - severityPenalty * 0.3f) + equipmentBonus);
        }

        /// <summary>
        /// Calculates the risk of treatment failure.
        /// </summary>
        public float CalculateTreatmentFailureRisk(TreatmentType treatment, HitZone zone)
        {
            float skillLevel = CalculateEffectiveSkillLevel();
            float severity = injurySystem.GetZoneSeverity(zone);
            float baseRisk = treatment switch
            {
                TreatmentType.Tourniquet => 0.05f,
                TreatmentType.Hemostatic => 0.1f,
                TreatmentType.PressureDressing => 0.15f,
                TreatmentType.Surgical => 0.3f,
                TreatmentType.FractureSplint => 0.2f,
                TreatmentType.ChestSeal => 0.1f,
                TreatmentType.IVFluids => 0.08f,
                TreatmentType.BloodTransfusion => 0.15f,
                _ => 0.2f
            };
            float skillBonus = skillLevel * 0.4f;
            float severityBonus = severity * 0.3f;
            return Mathf.Clamp01(baseRisk - skillBonus + severityBonus);
        }

        /// <summary>
        /// Gets the current treatment progress.
        /// </summary>
        public float GetCurrentTreatmentProgress()
        {
            if (!isTreating) return 0f;
            float totalTime = injurySystem.CalculateTreatmentTime(currentTreatment, CalculateEffectiveSkillLevel());
            return 1f - (currentTreatmentTimeRemaining / totalTime);
        }

        /// <summary>
        /// Checks if a treatment is currently in progress.
        /// </summary>
        public bool IsTreating => isTreating;

        /// <summary>
        /// Gets the current treatment type.
        /// </summary>
        public TreatmentType CurrentTreatment => currentTreatment;

        /// <summary>
        /// Gets the current target zone.
        /// </summary>
        public HitZone CurrentTargetZone => currentTargetZone;

        /// <summary>
        /// Sets the medic's base skill level.
        /// </summary>
        public void SetSkillLevel(float skillLevel)
        {
            baseSkillLevel = Mathf.Clamp01(skillLevel);
        }

        private float CalculateEffectiveSkillLevel()
        {
            if (physiology == null) return baseSkillLevel;
            float stressFactor = 1f - physiology.State.stress * 0.01f;
            float fatigueFactor = 1f - physiology.State.fatigue * 0.01f;
            return Mathf.Clamp01(baseSkillLevel * stressFactor * fatigueFactor);
        }

        private float GetSeverityPenalty(HitZone zone)
        {
            float totalSeverity = 0f;
            foreach (var wound in physiology.State.activeWounds)
            {
                if (wound.zone == zone) totalSeverity += wound.severity;
            }
            return Mathf.Clamp01(totalSeverity / 20f);
        }

        private void ApplyTreatmentEffects(TreatmentType treatment, HitZone zone, float effectiveness)
        {
            switch (treatment)
            {
                case TreatmentType.Tourniquet:
                    injurySystem.ApplyTourniquet(zone);
                    physiology.Treat(15f * effectiveness, 5f * effectiveness);
                    break;
                case TreatmentType.Hemostatic:
                    injurySystem.ApplyHemostatic(zone);
                    physiology.Treat(25f * effectiveness, 10f * effectiveness);
                    break;
                case TreatmentType.PressureDressing:
                    injurySystem.ApplyPressureDressing(zone);
                    physiology.Treat(20f * effectiveness, 8f * effectiveness);
                    break;
                case TreatmentType.Surgical:
                    injurySystem.ApplySurgicalTreatment(zone);
                    physiology.Treat(30f * effectiveness, 15f * effectiveness);
                    break;
                case TreatmentType.PainManagement:
                    ApplyPainManagement(20f * effectiveness);
                    break;
                case TreatmentType.Antibiotic:
                    ApplyAntibiotics(effectiveness);
                    break;
                case TreatmentType.FractureSplint:
                    BoneType bone = injurySystem.GetBoneType(zone);
                    injurySystem.StabilizeFracture(bone);
                    physiology.ApplyFracture(-15f * effectiveness, bone);
                    break;
                case TreatmentType.ChestSeal:
                    injurySystem.ApplyChestSeal();
                    physiology.Treat(10f * effectiveness, 5f * effectiveness);
                    break;
                case TreatmentType.IVFluids:
                    injurySystem.ApplyIVFluids(0.5f * effectiveness);
                    physiology.ApplyConsciousnessRecovery(10f * effectiveness);
                    break;
                case TreatmentType.BloodTransfusion:
                    injurySystem.ApplyBloodTransfusion(0.5f * effectiveness);
                    physiology.ApplyConsciousnessRecovery(15f * effectiveness);
                    break;
            }
        }
    }
}
