using System;
using UnityEngine;

namespace VEVE
{
    /// <summary>
    /// Enumeration of physical activities for stamina consumption.
    /// </summary>
    public enum ActivityType
    {
        Sprinting,
        Jogging,
        Walking,
        Crouching,
        Aiming,
        Jumping,
        Recovery
    }

    /// <summary>
    /// Stamina state data structure.
    /// </summary>
    [Serializable]
    public struct StaminaState
    {
        public float currentStamina;
        public float maxStamina;
        public float aerobicThreshold;
        public float anaerobicThreshold;
        public float lactateLevel;
        public float oxygenDebt;
        public float fatigueLevel;
        public bool isAerobic;
        public bool isAnaerobic;
        public float recoveryRate;
        public float glycogenReserve;
    }

    /// <summary>
    /// Stamina model with aerobic/anaerobic states, fatigue accumulation, recovery rates, and physiological impact on performance.
    /// </summary>
    public sealed class StaminaSystem : MonoBehaviour
    {
        [Header("Stamina Configuration")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float aerobicThreshold = 0.6f;
        [SerializeField] private float anaerobicThreshold = 0.8f;
        [SerializeField] private float baseRecoveryRate = 8f;
        [SerializeField] private float sprintConsumptionRate = 25f;
        [SerializeField] private float jogConsumptionRate = 10f;
        [SerializeField] private float walkConsumptionRate = 3f;
        [SerializeField] private float crouchConsumptionRate = 5f;
        [SerializeField] private float aimConsumptionRate = 2f;
        [SerializeField] private float jumpConsumptionRate = 15f;
        [SerializeField] private float recoveryDelay = 2f;
        [SerializeField] private float lactateAccumulationRate = 0.5f;
        [SerializeField] private float lactateDecayRate = 0.3f;

        private StaminaState staminaState;
        private float timeSinceLastExertion;
        private Physiology physiology;
        private MovementSimulation movement;
        private PlayerController playerController;

        public event Action<float, float> OnStaminaChanged;
        public event Action<bool> OnSprintExhausted;
        public event Action<bool> OnStaminaStateChanged;

        private void Awake()
        {
            staminaState = new StaminaState
            {
                currentStamina = maxStamina,
                maxStamina = maxStamina,
                aerobicThreshold = aerobicThreshold * maxStamina,
                anaerobicThreshold = anaerobicThreshold * maxStamina,
                lactateLevel = 0f,
                oxygenDebt = 0f,
                fatigueLevel = 0f,
                isAerobic = true,
                isAnaerobic = false,
                recoveryRate = baseRecoveryRate,
                glycogenReserve = 100f
            };
            physiology = GetComponent<Physiology>();
            movement = GetComponent<MovementSimulation>();
            playerController = GetComponent<PlayerController>();
        }

        /// <summary>
        /// Consumes stamina based on activity intensity.
        /// </summary>
        public void ConsumeStamina(ActivityType activity)
        {
            if (staminaState.currentStamina <= 0f && activity != ActivityType.Recovery) return;
            float consumptionRate = activity switch
            {
                ActivityType.Sprinting => sprintConsumptionRate,
                ActivityType.Jogging => jogConsumptionRate,
                ActivityType.Walking => walkConsumptionRate,
                ActivityType.Crouching => crouchConsumptionRate,
                ActivityType.Aiming => aimConsumptionRate,
                ActivityType.Jumping => jumpConsumptionRate,
                _ => 0f
            };
            staminaState.currentStamina = Mathf.Max(0f, staminaState.currentStamina - consumptionRate * Time.deltaTime);
            timeSinceLastExertion = 0f;
            if (staminaState.currentStamina < staminaState.aerobicThreshold)
            {
                staminaState.isAerobic = true;
                staminaState.isAnaerobic = true;
                staminaState.lactateLevel += lactateAccumulationRate * Time.deltaTime;
                staminaState.oxygenDebt += consumptionRate * Time.deltaTime * 0.5f;
            }
            else if (staminaState.currentStamina < staminaState.anaerobicThreshold)
            {
                staminaState.isAerobic = true;
                staminaState.isAnaerobic = false;
                staminaState.lactateLevel = Mathf.Max(0f, staminaState.lactateLevel - lactateDecayRate * Time.deltaTime);
            }
            else
            {
                staminaState.isAerobic = false;
                staminaState.isAnaerobic = false;
                staminaState.lactateLevel = Mathf.Max(0f, staminaState.lactateLevel - lactateDecayRate * 2f * Time.deltaTime);
            }
            staminaState.glycogenReserve = Mathf.Max(0f, staminaState.glycogenReserve - consumptionRate * Time.deltaTime * 0.01f);
            OnStaminaChanged?.Invoke(staminaState.currentStamina, staminaState.maxStamina);
            if (staminaState.currentStamina <= 0f && activity == ActivityType.Sprinting)
            {
                OnSprintExhausted?.Invoke(true);
            }
        }

        /// <summary>
        /// Recovers stamina over time with physiological considerations.
        /// </summary>
        public void RecoverStamina(float deltaTime)
        {
            timeSinceLastExertion += deltaTime;
            if (timeSinceLastExertion < recoveryDelay) return;
            float recoveryMultiplier = 1f;
            if (physiology != null)
            {
                float heartRateFactor = Mathf.Clamp01(1f - (physiology.State.heartRate - 65f) / 100f);
                recoveryMultiplier *= heartRateFactor;
                recoveryMultiplier *= physiology.State.bloodOxygenSaturation / 100f;
            }
            if (staminaState.isAnaerobic)
            {
                recoveryMultiplier *= 0.5f;
                staminaState.lactateLevel = Mathf.Max(0f, staminaState.lactateLevel - lactateDecayRate * 0.5f * deltaTime);
            }
            float actualRecovery = staminaState.recoveryRate * recoveryMultiplier * deltaTime;
            staminaState.currentStamina = Mathf.Min(staminaState.maxStamina, staminaState.currentStamina + actualRecovery);
            staminaState.oxygenDebt = Mathf.Max(0f, staminaState.oxygenDebt - deltaTime * 2f);
            staminaState.fatigueLevel = Mathf.Max(0f, staminaState.fatigueLevel - deltaTime * 0.02f);
            if (staminaState.currentStamina >= staminaState.aerobicThreshold)
            {
                staminaState.isAerobic = false;
            }
            if (staminaState.currentStamina >= staminaState.anaerobicThreshold)
            {
                staminaState.isAnaerobic = false;
            }
            OnStaminaChanged?.Invoke(staminaState.currentStamina, staminaState.maxStamina);
        }

        /// <summary>
        /// Gets the current stamina percentage.
        /// </summary>
        public float StaminaPercentage => staminaState.currentStamina / staminaState.maxStamina;

        /// <summary>
        /// Gets whether the character is currently in aerobic state.
        /// </summary>
        public bool IsAerobic => staminaState.isAerobic;

        /// <summary>
        /// Gets whether the character is currently in anaerobic state.
        /// </summary>
        public bool IsAnaerobic => staminaState.isAnaerobic;

        /// <summary>
        /// Gets the current lactate level.
        /// </summary>
        public float LactateLevel => staminaState.lactateLevel;

        /// <summary>
        /// Gets the current oxygen debt.
        /// </summary>
        public float OxygenDebt => staminaState.oxygenDebt;

        /// <summary>
        /// Gets the current fatigue level.
        /// </summary>
        public float FatigueLevel => staminaState.fatigueLevel;

        /// <summary>
        /// Gets the glycogen reserve percentage.
        /// </summary>
        public float GlycogenReserve => staminaState.glycogenReserve / 100f;

        /// <summary>
        /// Checks if the character can sprint.
        /// </summary>
        public bool CanSprint => staminaState.currentStamina > sprintConsumptionRate * recoveryDelay;

        /// <summary>
        /// Checks if the character can perform a high-intensity activity.
        /// </summary>
        public bool CanPerformHighIntensity => staminaState.currentStamina > sprintConsumptionRate * 0.5f;

        /// <summary>
        /// Gets the stamina-based speed multiplier.
        /// </summary>
        public float GetStaminaSpeedMultiplier()
        {
            float percentage = StaminaPercentage;
            if (percentage > 0.6f) return 1f;
            if (percentage > 0.3f) return 0.8f;
            return 0.5f;
        }

        /// <summary>
        /// Gets the stamina-based aim stability multiplier.
        /// </summary>
        public float GetStaminaAimMultiplier()
        {
            float percentage = StaminaPercentage;
            if (percentage > 0.5f) return 1f;
            if (percentage > 0.2f) return 0.7f;
            return 0.4f;
        }

        /// <summary>
        /// Gets the physiological impact on performance.
        /// </summary>
        public float GetPhysiologicalPerformanceImpact()
        {
            float lactateImpact = Mathf.Clamp01(staminaState.lactateLevel / 10f) * 0.5f;
            float oxygenDebtImpact = Mathf.Clamp01(staminaState.oxygenDebt / 50f) * 0.3f;
            float fatigueImpact = Mathf.Clamp01(staminaState.fatigueLevel / 100f) * 0.4f;
            return Mathf.Clamp01(lactateImpact + oxygenDebtImpact + fatigueImpact);
        }

        /// <summary>
        /// Gets the current activity intensity multiplier.
        /// </summary>
        public float GetActivityIntensityMultiplier(ActivityType activity)
        {
            return activity switch
            {
                ActivityType.Sprinting => 1.5f,
                ActivityType.Jogging => 1.2f,
                ActivityType.Walking => 0.8f,
                ActivityType.Crouching => 1.0f,
                ActivityType.Aiming => 0.6f,
                ActivityType.Jumping => 1.3f,
                _ => 1f
            };
        }

        private void Update()
        {
            RecoverStamina(Time.deltaTime);
            if (physiology != null)
            {
                float lactateImpact = Mathf.Clamp01(staminaState.lactateLevel / 10f);
                float fatigueImpact = Mathf.Clamp01(staminaState.fatigueLevel / 100f);
                physiology.ApplyFatigue(lactateImpact * 2f + fatigueImpact * 1f);
            }
        }
    }
}
