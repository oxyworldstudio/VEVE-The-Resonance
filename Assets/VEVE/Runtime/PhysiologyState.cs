using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE
{
    /// <summary>
    /// Represents a tracked wound with detailed tissue damage and progression data.
    /// </summary>
    [Serializable]
    public struct Wound
    {
        public string woundId;
        public HitZone zone;
        public InjuryType injuryType;
        public float severity;
        public float tissueDamage;
        public float bleedingRate;
        public float painLevel;
        public bool isTreated;
        public float treatmentProgress;
        public float timeSinceInjury;
        public float timeSinceTreatment;
        public bool isFracture;
        public float fractureDisplacement;
        public float infectionRisk;
    }

    /// <summary>
    /// Represents a fracture with detailed bone damage data.
    /// </summary>
    [Serializable]
    public struct FractureData
    {
        public BoneType bone;
        public float displacement;
        public float fragmentation;
        public bool isCompound;
        public bool isStabilized;
        public float healingProgress;
    }

    /// <summary>
    /// Enumeration of bone types for fracture simulation.
    /// </summary>
    public enum BoneType
    {
        Skull,
        CervicalSpine,
        ClavicleLeft,
        ClavicleRight,
        HumerusLeft,
        HumerusRight,
        RadiusLeft,
        RadiusRight,
        UlnaLeft,
        UlnaRight,
        Pelvis,
        FemurLeft,
        FemurRight,
        TibiaLeft,
        TibiaRight,
        FibulaLeft,
        FibulaRight,
        RibsLeft,
        RibsRight,
        Spine,
        ScapulaLeft,
        ScapulaRight
    }

    /// <summary>
    /// Medical state machine representing the character's current medical condition.
    /// </summary>
    public enum MedicalState
    {
        Healthy,
        Wounded,
        CriticallyInjured,
        Unconscious,
        Deceased,
        InTreatment,
        Recovering
    }

    /// <summary>
    /// Tracks the progression of a wound over time with realistic tissue damage modeling.
    /// </summary>
    [Serializable]
    public class WoundProgression
    {
        public Wound wound;
        public float tissueDegradationRate;
        public float infectionTimer;
        public float necrosisRisk;
        public bool hasNecrosis;
        public bool isArterial;
        public bool isVenous;
        public float bloodLossAccumulated;
    }

    /// <summary>
    /// Enhanced physiology state with detailed injury tracking, wound progression, and medical state machine.
    /// </summary>
    [Serializable]
    public struct PhysiologyState
    {
        [Range(0f, 100f)] public float bleeding;
        [Range(0f, 100f)] public float pain;
        [Range(0f, 100f)] public float stress;
        [Range(0f, 100f)] public float hydration;
        [Range(0f, 100f)] public float consciousness;
        [Range(0f, 100f)] public float fracture;
        [Range(0f, 100f)] public float fatigue;
        [Range(0f, 100f)] public float infection;
        [Min(30f)] public float heartRate;
        [Range(0f, 100f)] public float respiration;
        [Range(0f, 100f)] public float bloodPressureSystolic;
        [Range(0f, 100f)] public float bloodPressureDiastolic;
        public float bloodLossVolume;
        public float cardiacOutput;
        public float bloodOxygenSaturation;
        public MedicalState medicalState;
        public List<Wound> activeWounds;
        public List<FractureData> activeFractures;

        public static PhysiologyState Stable => new PhysiologyState
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
    }
}
