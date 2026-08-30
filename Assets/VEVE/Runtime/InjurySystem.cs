using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE
{
    /// <summary>
    /// Detailed injury model with hit zones, tissue damage, fracture simulation, and medical treatment options.
    /// </summary>
    [Serializable]
    public struct TissueDamageProfile
    {
        public float skinIntegrity;
        public float muscleDamage;
        public float vascularDamage;
        public float nerveDamage;
        public float organDamage;
        public float boneDamage;
    }

    /// <summary>
    /// Represents a medical treatment option with effectiveness and timing data.
    /// </summary>
    public enum TreatmentType
    {
        None,
        Tourniquet,
        Hemostatic,
        PressureDressing,
        Surgical,
        PainManagement,
        Antibiotic,
        FractureSplint,
        ChestSeal,
        IVFluids,
        BloodTransfusion
    }

    /// <summary>
    /// Detailed injury model with hit zones, tissue damage, fracture simulation, and medical treatment options.
    /// </summary>
    public sealed class InjurySystem : MonoBehaviour
    {
        [Header("Injury Configuration")]
        [SerializeField] private float tissueDamageMultiplier = 1f;
        [SerializeField] private float fractureBoneLossThreshold = 0.6f;
        [SerializeField] private float organDamageThreshold = 0.7f;
        [SerializeField] private float nerveDamageRecoveryRate = 0.01f;
        [SerializeField] private float infectionAccumulationRate = 0.01f;

        private Physiology physiology;
        private List<WoundProgression> woundProgressions = new List<WoundProgression>();

        public event Action<HitZone, InjuryType, float> OnInjuryApplied;
        public event Action<TreatmentType, float> OnTreatmentApplied;
        public event Action<HitZone> OnFractureStabilized;
        public event Action<HitZone> OnTourniquetApplied;

        private void Awake()
        {
            physiology = GetComponent<Physiology>();
        }

        /// <summary>
        /// Applies a ballistic wound to a specific hit zone with realistic tissue damage modeling.
        /// </summary>
        public void ApplyBallisticWound(HitZone zone, float energy, float penetrationDepth, float bulletDiameter, out TissueDamageProfile damageProfile)
        {
            float severity = CalculateBallisticSeverity(energy, penetrationDepth, bulletDiameter);
            float tissueDamage = severity * tissueDamageMultiplier;
            damageProfile = new TissueDamageProfile
            {
                skinIntegrity = Mathf.Clamp01(1f - tissueDamage * 0.3f),
                muscleDamage = Mathf.Clamp01(tissueDamage * 0.5f),
                vascularDamage = Mathf.Clamp01(severity * 0.4f),
                nerveDamage = Mathf.Clamp01(severity * 0.15f),
                organDamage = 0f,
                boneDamage = 0f
            };
            if (IsBoneZone(zone))
            {
                damageProfile.boneDamage = Mathf.Clamp01(severity * 0.6f);
                if (damageProfile.boneDamage > fractureBoneLossThreshold)
                {
                    BoneType bone = GetBoneType(zone);
                    bool isCompound = UnityEngine.Random.value < 0.3f;
                    float displacement = UnityEngine.Random.Range(0.1f, 0.5f) * damageProfile.boneDamage;
                    physiology.ApplyFracture(damageProfile.boneDamage * 50f, bone, isCompound, displacement);
                    OnFractureStabilized?.Invoke(zone);
                }
            }
            if (IsOrganZone(zone) && severity > organDamageThreshold)
            {
                damageProfile.organDamage = Mathf.Clamp01(severity * 0.8f);
                physiology.ApplyWound(severity * 30f, severity * 40f, severity * 0.3f, zone, InjuryType.GunshotWound);
            }
            else
            {
                float bleeding = damageProfile.vascularDamage * 25f;
                float pain = tissueDamage * 15f;
                physiology.ApplyWound(bleeding, pain, bleeding * 0.1f, zone, InjuryType.GunshotWound);
            }
            CreateWoundProgression(zone, severity, damageProfile);
            OnInjuryApplied?.Invoke(zone, InjuryType.GunshotWound, severity);
        }

        /// <summary>
        /// Applies blunt trauma to a specific hit zone.
        /// </summary>
        public void ApplyBluntTrauma(HitZone zone, float impactForce, float contactArea, out TissueDamageProfile damageProfile)
        {
            float severity = CalculateBluntTraumaSeverity(impactForce, contactArea);
            float tissueDamage = severity * tissueDamageMultiplier;
            damageProfile = new TissueDamageProfile
            {
                skinIntegrity = Mathf.Clamp01(1f - tissueDamage * 0.2f),
                muscleDamage = Mathf.Clamp01(tissueDamage * 0.4f),
                vascularDamage = Mathf.Clamp01(severity * 0.2f),
                nerveDamage = Mathf.Clamp01(severity * 0.1f),
                organDamage = 0f,
                boneDamage = 0f
            };
            if (IsBoneZone(zone) && severity > 0.5f)
            {
                damageProfile.boneDamage = Mathf.Clamp01(severity * 0.5f);
                if (damageProfile.boneDamage > fractureBoneLossThreshold)
                {
                    BoneType bone = GetBoneType(zone);
                    float displacement = UnityEngine.Random.Range(0.05f, 0.3f) * damageProfile.boneDamage;
                    physiology.ApplyFracture(damageProfile.boneDamage * 40f, bone, false, displacement);
                }
            }
            float bleeding = damageProfile.vascularDamage * 10f;
            float pain = tissueDamage * 20f;
            physiology.ApplyWound(bleeding, pain, bleeding * 0.05f, zone, InjuryType.BluntTrauma);
            CreateWoundProgression(zone, severity, damageProfile);
            OnInjuryApplied?.Invoke(zone, InjuryType.BluntTrauma, severity);
        }

        /// <summary>
        /// Applies a laceration wound.
        /// </summary>
        public void ApplyLaceration(HitZone zone, float cutDepth, float cutLength, out TissueDamageProfile damageProfile)
        {
            float severity = CalculateLacerationSeverity(cutDepth, cutLength);
            damageProfile = new TissueDamageProfile
            {
                skinIntegrity = Mathf.Clamp01(1f - severity * 0.5f),
                muscleDamage = Mathf.Clamp01(severity * 0.3f),
                vascularDamage = Mathf.Clamp01(severity * 0.5f),
                nerveDamage = Mathf.Clamp01(severity * 0.1f),
                organDamage = 0f,
                boneDamage = 0f
            };
            float bleeding = damageProfile.vascularDamage * 20f;
            float pain = severity * 10f;
            physiology.ApplyWound(bleeding, pain, bleeding * 0.08f, zone, InjuryType.DeepLaceration);
            CreateWoundProgression(zone, severity, damageProfile);
            OnInjuryApplied?.Invoke(zone, InjuryType.DeepLaceration, severity);
        }

        /// <summary>
        /// Applies a fracture directly to a bone.
        /// </summary>
        public void ApplyFractureInjury(BoneType bone, float force, bool isCompound, out FractureData fractureData)
        {
            float severity = CalculateFractureSeverity(force, bone);
            float displacement = UnityEngine.Random.Range(0.1f, 0.6f) * severity;
            physiology.ApplyFracture(severity * 50f, bone, isCompound, displacement);
            fractureData = new FractureData
            {
                bone = bone,
                displacement = displacement,
                fragmentation = severity * 0.4f,
                isCompound = isCompound,
                isStabilized = false,
                healingProgress = 0f
            };
            OnInjuryApplied?.Invoke(HitZone.ThighLeft, InjuryType.Fracture, severity);
        }

        /// <summary>
        /// Applies a burn injury.
        /// </summary>
        public void ApplyBurn(HitZone zone, float temperature, float exposureTime, float surfaceArea, out TissueDamageProfile damageProfile)
        {
            float severity = CalculateBurnSeverity(temperature, exposureTime, surfaceArea);
            damageProfile = new TissueDamageProfile
            {
                skinIntegrity = Mathf.Clamp01(1f - severity * 0.8f),
                muscleDamage = Mathf.Clamp01(severity * 0.6f),
                vascularDamage = Mathf.Clamp01(severity * 0.3f),
                nerveDamage = Mathf.Clamp01(severity * 0.7f),
                organDamage = 0f,
                boneDamage = 0f
            };
            float pain = severity * 25f;
            float bleeding = damageProfile.vascularDamage * 5f;
            physiology.ApplyWound(bleeding, pain, bleeding * 0.02f, zone, InjuryType.Burn);
            CreateWoundProgression(zone, severity, damageProfile);
            OnInjuryApplied?.Invoke(zone, InjuryType.Burn, severity);
        }

        /// <summary>
        /// Applies shock to the character.
        /// </summary>
        public void ApplyShock(float bloodLoss, float pain, float stress)
        {
            float shockLevel = Mathf.Clamp01((bloodLoss * 0.4f + pain * 0.35f + stress * 0.25f) / 100f);
            if (shockLevel > 0.6f)
            {
                physiology.ApplyWound(shockLevel * 10f, shockLevel * 20f, 0f, HitZone.UpperTorso, InjuryType.Shock);
            }
        }

        /// <summary>
        /// Applies a tourniquet to stop bleeding from a limb wound.
        /// </summary>
        public void ApplyTourniquet(HitZone zone)
        {
            for (int i = 0; i < physiology.State.activeWounds.Count; i++)
            {
                Wound wound = physiology.State.activeWounds[i];
                if (IsLimbZone(wound.zone) && wound.zone == zone && wound.bleedingRate > 0f)
                {
                    wound.bleedingRate *= 0.1f;
                    wound.isTreated = true;
                    wound.treatmentProgress = Mathf.Clamp01(wound.treatmentProgress + 0.5f);
                    physiology.State.activeWounds[i] = wound;
                    OnTourniquetApplied?.Invoke(zone);
                    break;
                }
            }
            OnTreatmentApplied?.Invoke(TreatmentType.Tourniquet, 0.5f);
        }

        /// <summary>
        /// Applies hemostatic gauze to a wound.
        /// </summary>
        public void ApplyHemostatic(HitZone zone)
        {
            for (int i = 0; i < physiology.State.activeWounds.Count; i++)
            {
                Wound wound = physiology.State.activeWounds[i];
                if (wound.zone == zone)
                {
                    wound.bleedingRate *= 0.3f;
                    wound.isTreated = true;
                    wound.treatmentProgress = Mathf.Clamp01(wound.treatmentProgress + 0.7f);
                    physiology.State.activeWounds[i] = wound;
                    break;
                }
            }
            OnTreatmentApplied?.Invoke(TreatmentType.Hemostatic, 0.7f);
        }

        /// <summary>
        /// Applies a pressure dressing to a wound.
        /// </summary>
        public void ApplyPressureDressing(HitZone zone)
        {
            for (int i = 0; i < physiology.State.activeWounds.Count; i++)
            {
                Wound wound = physiology.State.activeWounds[i];
                if (wound.zone == zone)
                {
                    wound.bleedingRate *= 0.5f;
                    wound.painLevel *= 0.8f;
                    wound.isTreated = true;
                    wound.treatmentProgress = Mathf.Clamp01(wound.treatmentProgress + 0.6f);
                    physiology.State.activeWounds[i] = wound;
                    break;
                }
            }
            OnTreatmentApplied?.Invoke(TreatmentType.PressureDressing, 0.6f);
        }

        /// <summary>
        /// Stabilizes a fracture.
        /// </summary>
        public void StabilizeFracture(BoneType bone)
        {
            for (int i = 0; i < physiology.State.activeFractures.Count; i++)
            {
                FractureData fracture = physiology.State.activeFractures[i];
                if (fracture.bone == bone && !fracture.isStabilized)
                {
                    fracture.isStabilized = true;
                    fracture.healingProgress = Mathf.Clamp01(fracture.healingProgress + 0.5f);
                    physiology.State.activeFractures[i] = fracture;
                    physiology.ApplyFracture(-20f, bone, false, fracture.displacement * 0.5f);
                    break;
                }
            }
            OnTreatmentApplied?.Invoke(TreatmentType.FractureSplint, 0.5f);
        }

        /// <summary>
        /// Applies surgical treatment to a wound.
        /// </summary>
        public void ApplySurgicalTreatment(HitZone zone)
        {
            for (int i = 0; i < physiology.State.activeWounds.Count; i++)
            {
                Wound wound = physiology.State.activeWounds[i];
                if (wound.zone == zone)
                {
                    wound.bleedingRate = 0f;
                    wound.painLevel *= 0.3f;
                    wound.isTreated = true;
                    wound.treatmentProgress = 1f;
                    wound.tissueDamage *= 0.5f;
                    physiology.State.activeWounds[i] = wound;
                    break;
                }
            }
            physiology.Treat(20f, 15f);
            OnTreatmentApplied?.Invoke(TreatmentType.Surgical, 1f);
        }

        public void ApplyChestSeal()
        {
            for (int i = 0; i < physiology.State.activeWounds.Count; i++)
            {
                Wound wound = physiology.State.activeWounds[i];
                if (wound.zone == HitZone.UpperTorso || wound.zone == HitZone.LowerTorso)
                {
                    wound.bleedingRate *= 0.4f;
                    wound.isTreated = true;
                    wound.treatmentProgress = Mathf.Clamp01(wound.treatmentProgress + 0.4f);
                    physiology.State.activeWounds[i] = wound;
                    break;
                }
            }
            OnTreatmentApplied?.Invoke(TreatmentType.ChestSeal, 0.4f);
        }

        public void ApplyIVFluids(float volumeLiters)
        {
            if (physiology == null) return;
            physiology.UpdateBloodPressure(
                physiology.State.bloodPressureSystolic + volumeLiters * 10f,
                physiology.State.bloodPressureDiastolic + volumeLiters * 5f);
            physiology.UpdateCardiacOutput(physiology.State.cardiacOutput + volumeLiters * 0.5f);
            physiology.ApplyConsciousnessRecovery(5f);
            OnTreatmentApplied?.Invoke(TreatmentType.IVFluids, 0.6f);
        }

        public void ApplyBloodTransfusion(float volumeLiters)
        {
            if (physiology == null) return;
            physiology.UpdateBloodPressure(
                physiology.State.bloodPressureSystolic + volumeLiters * 15f,
                physiology.State.bloodPressureDiastolic + volumeLiters * 8f);
            physiology.UpdateCardiacOutput(physiology.State.cardiacOutput + volumeLiters * 0.8f);
            physiology.UpdateRespiratoryRate(physiology.State.respiration + volumeLiters * 2f);
            physiology.ApplyConsciousnessRecovery(10f);
            OnTreatmentApplied?.Invoke(TreatmentType.BloodTransfusion, 0.9f);
        }

        public float GetTreatmentEffectiveness(TreatmentType treatment, HitZone zone)
        {
            float severity = GetZoneSeverity(zone);
            return treatment switch
            {
                TreatmentType.Tourniquet => IsLimbZone(zone) ? Mathf.Clamp01(0.9f - severity * 0.3f) : 0f,
                TreatmentType.Hemostatic => Mathf.Clamp01(0.7f - severity * 0.2f),
                TreatmentType.PressureDressing => Mathf.Clamp01(0.6f - severity * 0.15f),
                TreatmentType.Surgical => Mathf.Clamp01(1f - severity * 0.1f),
                TreatmentType.FractureSplint => HasFractureInZone(zone) ? Mathf.Clamp01(0.8f - severity * 0.2f) : 0f,
                TreatmentType.ChestSeal => zone == HitZone.UpperTorso || zone == HitZone.LowerTorso ? 0.7f : 0f,
                TreatmentType.IVFluids => Mathf.Clamp01(0.5f + (1f - severity) * 0.4f),
                TreatmentType.BloodTransfusion => Mathf.Clamp01(0.8f + (1f - severity) * 0.2f),
                _ => 0f
            };
        }

        public float CalculateTreatmentTime(TreatmentType treatment, float skillLevel)
        {
            float baseTime = treatment switch
            {
                TreatmentType.Tourniquet => 12f,
                TreatmentType.Hemostatic => 30f,
                TreatmentType.PressureDressing => 45f,
                TreatmentType.Surgical => 120f,
                TreatmentType.PainManagement => 15f,
                TreatmentType.Antibiotic => 10f,
                TreatmentType.FractureSplint => 90f,
                TreatmentType.ChestSeal => 20f,
                TreatmentType.IVFluids => 60f,
                TreatmentType.BloodTransfusion => 180f,
                _ => 30f
            };
            float skillMultiplier = 1f - (skillLevel * 0.4f);
            return baseTime * skillMultiplier;
        }

        public List<Wound> GetActiveWounds() => physiology.State.activeWounds;
        public List<FractureData> GetActiveFractures() => physiology.State.activeFractures;
        public int GetActiveWoundCount() => physiology.State.activeWounds.Count;
        public int GetActiveFractureCount() => physiology.State.activeFractures.Count;

        private float CalculateBallisticSeverity(float energy, float penetrationDepth, float bulletDiameter)
        {
            float energyFactor = Mathf.Clamp01(energy / 500f);
            float depthFactor = Mathf.Clamp01(penetrationDepth / 0.3f);
            float diameterFactor = Mathf.Clamp01(bulletDiameter / 0.01f);
            return Mathf.Clamp01(energyFactor * 0.5f + depthFactor * 0.3f + diameterFactor * 0.2f);
        }

        private float CalculateBluntTraumaSeverity(float impactForce, float contactArea)
        {
            float forceFactor = Mathf.Clamp01(impactForce / 500f);
            float areaFactor = Mathf.Clamp01(1f - contactArea / 0.1f);
            return Mathf.Clamp01(forceFactor * 0.7f + areaFactor * 0.3f);
        }

        private float CalculateLacerationSeverity(float cutDepth, float cutLength)
        {
            float depthFactor = Mathf.Clamp01(cutDepth / 0.05f);
            float lengthFactor = Mathf.Clamp01(cutLength / 0.2f);
            return Mathf.Clamp01(depthFactor * 0.6f + lengthFactor * 0.4f);
        }

        private float CalculateFractureSeverity(float force, BoneType bone)
        {
            float boneStrength = bone switch
            {
                BoneType.FemurLeft or BoneType.FemurRight => 0.3f,
                BoneType.HumerusLeft or BoneType.HumerusRight => 0.4f,
                BoneType.TibiaLeft or BoneType.TibiaRight => 0.35f,
                BoneType.Skull => 0.8f,
                _ => 0.5f
            };
            return Mathf.Clamp01((force / 1000f) * (1f - boneStrength));
        }

        private float CalculateBurnSeverity(float temperature, float exposureTime, float surfaceArea)
        {
            float tempFactor = Mathf.Clamp01(temperature / 1000f);
            float timeFactor = Mathf.Clamp01(exposureTime / 10f);
            float areaFactor = Mathf.Clamp01(surfaceArea / 0.5f);
            return Mathf.Clamp01(tempFactor * 0.5f + timeFactor * 0.3f + areaFactor * 0.2f);
        }

        private void CreateWoundProgression(HitZone zone, float severity, TissueDamageProfile profile)
        {
            WoundProgression progression = new WoundProgression
            {
                wound = new Wound
                {
                    woundId = Guid.NewGuid().ToString(),
                    zone = zone,
                    injuryType = InjuryType.GunshotWound,
                    severity = severity,
                    tissueDamage = severity,
                    bleedingRate = profile.vascularDamage * 0.2f,
                    painLevel = severity * 15f,
                    isTreated = false,
                    treatmentProgress = 0f,
                    timeSinceInjury = 0f,
                    timeSinceTreatment = 0f,
                    isFracture = false,
                    fractureDisplacement = 0f,
                    infectionRisk = 0f
                },
                tissueDegradationRate = severity * 0.02f,
                infectionTimer = 0f,
                necrosisRisk = severity * 0.1f,
                hasNecrosis = false,
                isArterial = profile.vascularDamage > 0.7f,
                isVenous = profile.vascularDamage > 0.3f && profile.vascularDamage <= 0.7f,
                bloodLossAccumulated = 0f
            };
            woundProgressions.Add(progression);
            if (woundProgressions.Count > 20) woundProgressions.RemoveAt(0);
        }

        private bool IsBoneZone(HitZone zone)
        {
            return zone switch
            {
                HitZone.Head => true,
                HitZone.UpperTorso => true,
                HitZone.LowerTorso => true,
                HitZone.UpperArmLeft or HitZone.UpperArmRight => true,
                HitZone.ForearmLeft or HitZone.ForearmRight => true,
                HitZone.HandLeft or HitZone.HandRight => true,
                HitZone.ThighLeft or HitZone.ThighRight => true,
                HitZone.CalfLeft or HitZone.CalfRight => true,
                HitZone.FootLeft or HitZone.FootRight => true,
                _ => false
            };
        }

        private bool IsOrganZone(HitZone zone)
        {
            return zone == HitZone.UpperTorso || zone == HitZone.LowerTorso || zone == HitZone.Head;
        }

        private bool IsLimbZone(HitZone zone)
        {
            return zone is >= HitZone.UpperArmLeft and <= HitZone.FootRight;
        }

        public BoneType GetBoneType(HitZone zone)
        {
            return zone switch
            {
                HitZone.Head => BoneType.Skull,
                HitZone.UpperTorso => BoneType.RibsLeft,
                HitZone.LowerTorso => BoneType.Pelvis,
                HitZone.UpperArmLeft => BoneType.HumerusLeft,
                HitZone.UpperArmRight => BoneType.HumerusRight,
                HitZone.ForearmLeft => BoneType.RadiusLeft,
                HitZone.ForearmRight => BoneType.RadiusRight,
                HitZone.HandLeft or HitZone.HandRight => BoneType.RadiusRight,
                HitZone.ThighLeft => BoneType.FemurLeft,
                HitZone.ThighRight => BoneType.FemurRight,
                HitZone.CalfLeft => BoneType.TibiaLeft,
                HitZone.CalfRight => BoneType.TibiaRight,
                HitZone.FootLeft or HitZone.FootRight => BoneType.TibiaRight,
                _ => BoneType.FemurLeft
            };
        }

        public float GetZoneSeverity(HitZone zone)
        {
            float totalSeverity = 0f;
            foreach (var wound in physiology.State.activeWounds)
            {
                if (wound.zone == zone) totalSeverity += wound.severity;
            }
            return Mathf.Clamp01(totalSeverity / 10f);
        }

        private bool HasFractureInZone(HitZone zone)
        {
            BoneType bone = GetBoneType(zone);
            foreach (var fracture in physiology.State.activeFractures)
            {
                if (fracture.bone == bone) return true;
            }
            return false;
        }

        private void Update()
        {
            foreach (var progression in woundProgressions)
            {
                progression.wound.timeSinceInjury += Time.deltaTime;
                progression.wound.timeSinceTreatment += Time.deltaTime;
                progression.infectionTimer += Time.deltaTime;
                progression.bloodLossAccumulated += progression.wound.bleedingRate * Time.deltaTime;
                if (!progression.wound.isTreated && progression.infectionTimer > 600f)
                {
                    progression.wound.infectionRisk += Time.deltaTime * infectionAccumulationRate;
                }
                if (progression.hasNecrosis)
                {
                    progression.tissueDegradationRate *= 1.01f;
                }
            }
        }
    }
}
