using System;
using UnityEngine;
using VEVE.Gear;
using VEVE.WeaponCustomPro;

namespace VEVE.Operators
{
    /// <summary>
    /// Scene-graph binding of an <see cref="OperatorProfile"/> to the player GameObject: folds the
    /// trait <see cref="ChannelVector"/>, the worn <see cref="GearLoadout"/> aggregates, the
    /// <see cref="MobilityPenaltyModel"/> mass penalties, and (optionally) a mounted
    /// <see cref="ScopeProfile"/>'s handling multipliers into a handful of cached feel scalars that
    /// <c>LookController</c>, <c>PlayerController</c>, and <c>Weapon</c> consume every frame.
    /// <para>
    /// All derived scalars are recomputed in <see cref="OnEnable"/> only (darkness is sampled cheaply
    /// once from <c>EnvironmentSimulation.SunElevation</c>); consumers treat a missing instance as
    /// neutral 1, so adding or removing this component never breaks existing behaviour.
    /// Composition convention: a higher returned multiplier always means better feel (steadier,
    /// faster), and every public static helper is pure and unit-testable without a scene.
    /// </para>
    /// </summary>
    public sealed class OperatorInstance : MonoBehaviour
    {
        [Header("Operator Binding")]
        [SerializeField] private OperatorProfile activeOperator;
        [SerializeField] private WeaponInstanceIdentity identity;
        [SerializeField] private string primaryWeaponFamily = string.Empty;

        [Header("Optics (optional; null = neutral handling)")]
        [SerializeField] private ScopeProfile opticProfile;
        [SerializeField] private float opticZoom = 1f;
        [SerializeField] private float railKitOverhangMm;

        /// <summary>Reference body mass (kg) used to normalize worn mass into the load ratio.</summary>
        public const float ReferenceBodyMassKg = 80f;

        /// <summary>Hard floor of any composed feel multiplier; guards against degenerate stacks.</summary>
        public const float MinMultiplier = 0.25f;

        /// <summary>Hard ceiling of any composed feel multiplier.</summary>
        public const float MaxMultiplier = 2.5f;

        private ChannelVector channels = ChannelVector.Neutral();
        private float aimStabilityMultiplier = 1f;
        private float swayRecoveryMultiplier = 1f;
        private float moveSpeedMultiplier = 1f;
        private float sprintSpeedMultiplier = 1f;
        private float noiseMultiplier = 1f;
        private float reloadSpeedMultiplier = 1f;

        /// <summary>Profile whose traits feed the channel vector (may be null = neutral operator).</summary>
        public OperatorProfile ActiveOperator => activeOperator;

        /// <summary>Serialized per-weapon identity used by the firing solution (may be null).</summary>
        public WeaponInstanceIdentity Identity => identity;

        /// <summary>Free-form primary weapon family label for roster/UI flavor hooks.</summary>
        public string PrimaryWeaponFamily => primaryWeaponFamily;

        /// <summary>Aggregated trait channels currently in effect (never null; neutral without profile).</summary>
        public ChannelVector Channels => channels;

        /// <summary>
        /// Aim quality factor (trait aimStability × gear aim aggregate × optic weight/balance/mag
        /// penalties). Above 1 = steadier than baseline; consumers attenuate sway amplitude by it.
        /// </summary>
        public float AimStabilityMultiplier => aimStabilityMultiplier;

        /// <summary>
        /// Sway settle-rate factor (trait swayRecovery × optic weight penalty, against the
        /// <see cref="MobilityPenaltyModel.AimRecoveryMultiplier"/> gear resistance). Higher = faster decay.
        /// </summary>
        public float SwayRecoveryMultiplier => swayRecoveryMultiplier;

        /// <summary>Walk-speed factor: trait moveSpeed × loadout mobility × <see cref="MobilityPenaltyModel.WalkSpeedMultiplier"/>.</summary>
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        /// <summary>Sprint-speed factor: trait moveSpeed × loadout mobility × <see cref="MobilityPenaltyModel.SprintSpeedMultiplier"/>.</summary>
        public float SprintSpeedMultiplier => sprintSpeedMultiplier;

        /// <summary>Emitted-noise multiplier (trait noiseLoudness). Lower is quieter; exposed for later audio integration.</summary>
        public float NoiseMultiplier => noiseMultiplier;

        /// <summary>Weapon manipulation speed factor (trait reloadSpeed channel).</summary>
        public float ReloadSpeedMultiplier => reloadSpeedMultiplier;

        private void OnEnable()
        {
            Recompute();
        }

        /// <summary>
        /// Re-samples environment darkness and gear, then rebuilds every cached feel scalar.
        /// Safe to call whenever the profile, loadout, or optic binding changes at runtime.
        /// </summary>
        public void Recompute()
        {
            float darkness = SampleDarkness(ResolveSunElevation());
            channels = AggregateTraits(activeOperator, darkness);

            GearLoadout loadout = ResolveLoadout();
            float loadRatio = loadout != null ? loadout.TotalMassKg / ReferenceBodyMassKg : 0f;
            float thermal = loadout != null ? loadout.TotalHeatLoad : 0f;
            float upperBodyBulk = 0f;
            float limbCoverage = 0f;
            if (loadout != null)
            {
                ComputeCoverageProxies(loadout, out upperBodyBulk, out limbCoverage);
            }

            // Aim: trait stability × gear aim aggregate × optic handling penalties.
            float gearAim = loadout != null ? loadout.AggregateAimMultiplier : 1f;
            float opticStability = OpticStabilityPenalty(opticProfile, opticZoom, railKitOverhangMm);
            aimStabilityMultiplier = Combine(channels.Get(TraitChannel.AimStability), gearAim, opticStability);

            // Sway recovery: trait rate × optic weight × inverse gear recovery resistance.
            float gearRecoveryAssist = Combine(1f, 1f / MobilityPenaltyModel.AimRecoveryMultiplier(loadRatio, upperBodyBulk), 1f);
            float opticWeightSway = OpticClamp((float)ScopeOpticsModel.WeightSwayPenaltyMultiplier(opticProfile));
            swayRecoveryMultiplier = Combine(channels.Get(TraitChannel.SwayRecovery), gearRecoveryAssist, opticWeightSway);

            // Locomotion: trait moveSpeed × gear mobility aggregate × mass/heat/limb penalty model.
            float gearMobility = loadout != null ? loadout.AggregateMobilityMultiplier : 1f;
            moveSpeedMultiplier = Combine(channels.Get(TraitChannel.MoveSpeed), gearMobility,
                MobilityPenaltyModel.WalkSpeedMultiplier(loadRatio, thermal, limbCoverage));
            sprintSpeedMultiplier = Combine(channels.Get(TraitChannel.MoveSpeed), gearMobility,
                MobilityPenaltyModel.SprintSpeedMultiplier(loadRatio, thermal, limbCoverage));

            noiseMultiplier = ClampPositive(channels.Get(TraitChannel.NoiseLoudness));
            reloadSpeedMultiplier = Combine(channels.Get(TraitChannel.ReloadSpeed), 1f, 1f);
        }

        /// <summary>
        /// Pure composition of one trait channel with its gear and optic penalty factors:
        /// <c>clamp(traitChannel × gearPenalty × opticPenalty, MinMultiplier, MaxMultiplier)</c>.
        /// Each factor is multiplicative-commutative (order of composition does not matter);
        /// a factor that is NaN, infinite, or non-positive is treated as neutral 1 instead of
        /// poisoning the product. The 1/x form of "penalty ≥1" models (like
        /// <see cref="MobilityPenaltyModel.AimRecoveryMultiplier"/>'s inverse) passes as
        /// <paramref name="gearPenalty"/> directly.
        /// </summary>
        /// <param name="traitChannel">Trait channel aggregate from <see cref="ChannelVector.Get"/>.</param>
        /// <param name="gearPenalty">Gear contribution (≤1 penalty or an already-inverted assist).</param>
        /// <param name="opticPenalty">Optic handling multiplier from <see cref="ScopeOpticsModel"/>.</param>
        /// <returns>Composed multiplier inside [<see cref="MinMultiplier"/>, <see cref="MaxMultiplier"/>].</returns>
        public static float Combine(float traitChannel, float gearPenalty, float opticPenalty)
        {
            float product = ClampPositive(traitChannel) * ClampPositive(gearPenalty) * ClampPositive(opticPenalty);
            return ClampPositive(product);
        }

        /// <summary>
        /// Pure civil-twilight darkness curve used for night-conditioned traits, matching the
        /// <c>skyDarkness</c> formula of EnvironmentSimulation/SkyboxController:
        /// <c>1 − clamp01((elevation + 6) / 6)</c>.
        /// </summary>
        /// <param name="sunElevationDeg">Solar elevation in degrees (negative = below horizon).</param>
        /// <returns>Darkness in [0, 1]; 0 at or above +6°, 1 at or below −6°.</returns>
        public static float SampleDarkness(float sunElevationDeg)
        {
            return 1f - Mathf.Clamp01((sunElevationDeg + 6f) / 6f);
        }

        /// <summary>
        /// Neutral-safe trait fold: a null profile (or null trait set) returns the all-ones
        /// <see cref="ChannelVector"/>; otherwise the profile's traits aggregate under darkness.
        /// </summary>
        /// <param name="profile">Active operator profile (may be null).</param>
        /// <param name="darkness">Darkness condition passed to <see cref="TraitSet.Aggregate(float)"/>.</param>
        /// <returns>Aggregated (already clamped) channel vector; never null.</returns>
        public static ChannelVector AggregateTraits(OperatorProfile profile, float darkness)
        {
            if (profile == null || profile.traits == null)
            {
                return ChannelVector.Neutral();
            }
            return profile.traits.Aggregate(darkness);
        }

        /// <summary>
        /// Combined optic handling penalty for aim stability: weight × balance torque × magnification
        /// agility multipliers. A null profile (irons, or a rail kit carrying nothing) is neutral 1.
        /// </summary>
        /// <param name="profile">Mounted optic data (may be null).</param>
        /// <param name="zoom">Effective magnification currently dialed.</param>
        /// <param name="railKitOverhangMm">Fore-aft rail/adapter overhang feeding the torque model.</param>
        /// <returns>Penalty multiplier in [0.5³ clamped range, 1]; 1 = no optic penalty.</returns>
        public static float OpticStabilityPenalty(ScopeProfile profile, float zoom, float railKitOverhangMm)
        {
            if (profile == null) return 1f;
            double weight = ScopeOpticsModel.WeightSwayPenaltyMultiplier(profile);
            double balance = ScopeOpticsModel.BalanceTorquePenaltyMultiplier(profile, railKitOverhangMm);
            double magnification = ScopeOpticsModel.MagnificationAgilityMultiplier(profile, zoom);
            return OpticClamp((float)(weight * balance * magnification));
        }

        /// <summary>Sanitizes a scope-model multiplier (finite, inside [MinMultiplier, 1] by construction).</summary>
        /// <param name="value">Raw optic multiplier.</param>
        /// <returns>Finite clamp into [<see cref="MinMultiplier"/>, <see cref="MaxMultiplier"/>].</returns>
        public static float OpticClamp(float value)
        {
            return ClampPositive(value);
        }

        private static float ClampPositive(float value)
        {
            if (!ChannelVector.IsFinitePositive(value))
            {
                return 1f;
            }
            if (value < MinMultiplier) return MinMultiplier;
            if (value > MaxMultiplier) return MaxMultiplier;
            return value;
        }

        private float ResolveSunElevation()
        {
            EnvironmentSimulation environment = FindFirstObjectByType<EnvironmentSimulation>();
            return environment != null ? environment.SunElevation : 6f;
        }

        private GearLoadout ResolveLoadout()
        {
            DamageableGearAdapter adapter = GetComponentInParent<DamageableGearAdapter>();
            return adapter != null ? adapter.Loadout : null;
        }

        private static void ComputeCoverageProxies(GearLoadout loadout, out float upperBodyBulk, out float limbCoverage)
        {
            var coverage = new float[GearItem.ZoneCount];
            loadout.ComputeCoverage(coverage);
            float upper = 0f;
            float limbs = 0f;
            for (int z = 0; z < GearItem.ZoneCount; z++)
            {
                float value = Mathf.Clamp01(coverage[z]);
                if (z <= (int)HitZone.LowerTorso) upper += value;          // head, neck, torso
                else if (z >= (int)HitZone.UpperArmLeft && z <= (int)HitZone.CalfRight) limbs += value; // arms + legs
            }
            upperBodyBulk = upper / ((int)HitZone.LowerTorso + 1);
            limbCoverage = limbs / ((int)HitZone.CalfRight - (int)HitZone.UpperArmLeft + 1);
        }
    }
}
