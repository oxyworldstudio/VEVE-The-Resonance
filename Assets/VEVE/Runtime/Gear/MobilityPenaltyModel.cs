using System;

namespace VEVE.Gear
{
    /// <summary>
    /// Pure monotonic mapping from load ratio and ballistic coverage/heat to locomotion,
    /// weapon-handling and endurance penalties. All inputs are normalized ratios; all outputs
    /// are multipliers clamped into documented ranges. 1.0 always means "no penalty".
    /// </summary>
    public static class MobilityPenaltyModel
    {
        /// <summary>Load ratio at which the heaviest walking penalty applies (gear mass / body mass reference).</summary>
        public const float FullLoadRatio = 1f;

        /// <summary>Reference thermal load index at which heat penalties saturate.</summary>
        public const float FullThermalIndex = 90f;

        /// <summary>Upper bound for the sway multiplier.</summary>
        public const float MaxSwayMultiplier = 2.5f;

        /// <summary>Upper bound for the stamina drain multiplier.</summary>
        public const float MaxStaminaMultiplier = 2.5f;

        /// <summary>
        /// Walking speed multiplier in [0.65, 1]: quadratic in load ratio, linear in thermal index;
        /// extra step penalty when limb coverage restricts articulation.
        /// </summary>
        /// <param name="loadRatio">Gear+cargo mass ratio 0..1.</param>
        /// <param name="thermalIndex">Aggregate heat load 0..~120 (clamped internally).</param>
        /// <param name="limbCoverage">Average limb-zone coverage 0..1.</param>
        /// <returns>Walk speed multiplier.</returns>
        public static float WalkSpeedMultiplier(float loadRatio, float thermalIndex, float limbCoverage)
        {
            float load = Math.Clamp(loadRatio, 0f, FullLoadRatio);
            float heat = Math.Clamp(thermalIndex / FullThermalIndex, 0f, 1.35f);
            float limbs = Math.Clamp(limbCoverage, 0f, 1f);
            float factor = 1f - 0.22f * load * load - 0.08f * heat - 0.06f * limbs;
            return Math.Clamp(factor, 0.65f, 1f);
        }

        /// <summary>
        /// Sprint speed multiplier in [0.5, 1]: load effect is stronger and more linear than walking
        /// (metabolic demand grows superlinearly with carried mass at high cadence).
        /// </summary>
        /// <param name="loadRatio">Gear+cargo mass ratio 0..1.</param>
        /// <param name="thermalIndex">Aggregate heat load.</param>
        /// <param name="limbCoverage">Average limb-zone coverage 0..1.</param>
        /// <returns>Sprint speed multiplier.</returns>
        public static float SprintSpeedMultiplier(float loadRatio, float thermalIndex, float limbCoverage)
        {
            float load = Math.Clamp(loadRatio, 0f, FullLoadRatio);
            float heat = Math.Clamp(thermalIndex / FullThermalIndex, 0f, 1.35f);
            float limbs = Math.Clamp(limbCoverage, 0f, 1f);
            float factor = 1f - 0.32f * load - 0.12f * heat * load - 0.1f * limbs;
            return Math.Clamp(factor, 0.5f, 1f);
        }

        /// <summary>
        /// Weapon sway multiplier in [1, <see cref="MaxSwayMultiplier"/>]: driven by bulk of torso/head
        /// coverage (helmet mass, plate carrier rigidity) plus load; higher is worse.
        /// </summary>
        /// <param name="loadRatio">Gear+cargo mass ratio 0..1.</param>
        /// <param name="upperBodyBulk">Combined head+torso coverage proxy 0..1.</param>
        /// <param name="thermalIndex">Aggregate heat load.</param>
        /// <returns>Sway multiplier (1 = bare).</returns>
        public static float SwayMultiplier(float loadRatio, float upperBodyBulk, float thermalIndex)
        {
            float load = Math.Clamp(loadRatio, 0f, FullLoadRatio);
            float bulk = Math.Clamp(upperBodyBulk, 0f, 1f);
            float heat = Math.Clamp(thermalIndex / FullThermalIndex, 0f, 1.35f);
            float factor = 1f + 0.5f * bulk + 0.6f * load * load + 0.25f * heat * bulk;
            return Math.Clamp(factor, 1f, MaxSwayMultiplier);
        }

        /// <summary>
        /// Aim recovery penalty multiplier in [1, 2]: torso coverage inhibits shoulder/press positions.
        /// </summary>
        /// <param name="loadRatio">Gear+cargo mass ratio 0..1.</param>
        /// <param name="upperBodyBulk">Combined head+torso coverage proxy 0..1.</param>
        /// <returns>Aim recovery multiplier (1 = bare).</returns>
        public static float AimRecoveryMultiplier(float loadRatio, float upperBodyBulk)
        {
            float load = Math.Clamp(loadRatio, 0f, FullLoadRatio);
            float bulk = Math.Clamp(upperBodyBulk, 0f, 1f);
            float factor = 1f + 0.7f * bulk + 0.4f * load;
            return Math.Clamp(factor, 1f, 2f);
        }

        /// <summary>
        /// Stamina drain multiplier in [1, <see cref="MaxStaminaMultiplier"/>]: mass-proportional cost of
        /// work plus a thermal term (higher core-temperature drift while overloaded).
        /// </summary>
        /// <param name="loadRatio">Gear+cargo mass ratio 0..1.</param>
        /// <param name="thermalIndex">Aggregate heat load.</param>
        /// <returns>Stamina drain multiplier (1 = bare).</returns>
        public static float StaminaDrainMultiplier(float loadRatio, float thermalIndex)
        {
            float load = Math.Clamp(loadRatio, 0f, FullLoadRatio);
            float heat = Math.Clamp(thermalIndex / FullThermalIndex, 0f, 1.35f);
            float factor = 1f + 0.9f * load + 0.35f * heat;
            return Math.Clamp(factor, 1f, MaxStaminaMultiplier);
        }

        /// <summary>
        /// Steady-state heat accumulation multiplier in [1, 2.4]: plate coverage and layering trap
        /// evaporative cooling; feeds future thermoregulation integration.
        /// </summary>
        /// <param name="upperBodyBulk">Combined head+torso coverage proxy 0..1.</param>
        /// <param name="loadRatio">Gear+cargo mass ratio 0..1.</param>
        /// <returns>Heat gain multiplier (1 = bare).</returns>
        public static float HeatGainMultiplier(float upperBodyBulk, float loadRatio)
        {
            float bulk = Math.Clamp(upperBodyBulk, 0f, 1f);
            float load = Math.Clamp(loadRatio, 0f, FullLoadRatio);
            float factor = 1f + 0.9f * bulk + 0.2f * load;
            return Math.Clamp(factor, 1f, 2.4f);
        }
    }
}
