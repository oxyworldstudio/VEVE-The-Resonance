using System;
using UnityEngine;
using VEVE;
using VEVE.Agentic;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    /// <summary>
    /// Realistic fall tracking for players and NPCs. Measures the airborne drop distance and
    /// airtime, computes drag-limited terminal velocity from mass, cross-section and drag
    /// coefficient, converts impact kinetic energy into landing damage against a body tolerance,
    /// flags leg-buckling impacts, applies crouch-roll mitigation (halves the effective fall
    /// height), and raises <see cref="OnGrounded"/> on landing. Gravity is read as a signed
    /// downward value from <see cref="RealismConfig"/> — never hardcoded positive.
    /// </summary>
    [RequireComponent(typeof(GroundContactProbe))]
    public sealed class TerminalVelocityFallingSystem : MonoBehaviour
    {
        [SerializeField] private float bodyMassKg = 80f;
        [SerializeField] private float crossSectionAreaM2 = 0.7f;
        [SerializeField] private float dragCoefficient = 1.0f;
        [SerializeField] private float bodyEnergyToleranceJ = 18000f;
        [SerializeField] private float maxFallDamage = 100f;
        [SerializeField] private RealismConfig realismConfig;

        /// <summary>CODATA standard gravitational acceleration magnitude (m/s²).</summary>
        public const float StandardGravityMagnitude = 9.80665f;

        /// <summary>Impact speed (m/s) at which a human tuck-and-roll is no longer sufficient; legs buckle.</summary>
        public const float LegBucklingImpactSpeed = 9.5f;

        /// <summary>Effective fall height multiplier applied when landing crouched with a roll.</summary>
        public const float CrouchMitigationFactor = 0.5f;

        private GroundContactProbe probe;
        private HealthComponent health;
        private Rigidbody rigidbody;
        private bool wasGrounded = true;
        private float fallPeakY;
        private float fallStartTime;
        private bool falling;

        /// <summary>Raised on landing: (mitigated fall height m, impact vertical speed m/s).</summary>
        public event Action<float, float> OnGrounded;

        /// <summary>Raised when an impact exceeds the leg-buckling threshold; argument is impact speed m/s.</summary>
        public event Action<float> OnLegBuckled;

        /// <summary>True while continuously airborne beyond the probe's ground tolerance.</summary>
        public bool IsFalling => falling;

        /// <summary>Vertical drop measured since the current fall began; 0 when grounded (metres).</summary>
        public float CurrentFallHeight => falling ? Mathf.Max(0f, fallPeakY - transform.position.y) : 0f;

        /// <summary>Airtime of the current fall so far, in seconds.</summary>
        public float CurrentAirtime => falling ? Mathf.Max(0f, Time.time - fallStartTime) : 0f;

        /// <summary>Total body mass used by the terminal velocity model (kit included when a mass model is present), kg.</summary>
        public float EffectiveMassKg
        {
            get
            {
                CharacterMassModel massModel = GetComponent<CharacterMassModel>();
                return massModel != null ? massModel.TotalMassKg : bodyMassKg;
            }
        }

        /// <summary>Ambient air density from the linked <see cref="RealismConfig"/> (kg/m³).</summary>
        public float AirDensity => realismConfig != null ? realismConfig.AirDensitySeaLevel : CharacterMassModel.StandardAirDensity;

        /// <summary>
        /// Signed downward gravity actually used by this system (read from <see cref="RealismConfig.StandardGravity"/>
        /// and forced non-positive; falls back to Physics.gravity when no config is linked).
        /// </summary>
        public float SignedGravity
        {
            get
            {
                if (realismConfig != null)
                {
                    return ToSignedDownward(realismConfig.StandardGravity);
                }

                float physicsY = Physics.gravity.y;
                return physicsY > 0f ? -physicsY : (physicsY == 0f ? -StandardGravityMagnitude : physicsY);
            }
        }

        /// <summary>Free-fall terminal speed for the current mass, area, drag and air density (m/s).</summary>
        public float TerminalVelocity => ComputeTerminalVelocity(EffectiveMassKg, Mathf.Abs(SignedGravity), AirDensity, Mathf.Max(0.01f, dragCoefficient), Mathf.Max(0.01f, crossSectionAreaM2));

        /// <summary>
        /// Normalises any configured gravity magnitude to a signed downward (non-positive) value.
        /// </summary>
        /// <param name="gravityMagnitudeOrSigned">Positive magnitude or already-signed gravity.</param>
        /// <returns>Signed downward gravity in m/s².</returns>
        public static float ToSignedDownward(float gravityMagnitudeOrSigned)
        {
            return gravityMagnitudeOrSigned > 0f ? -gravityMagnitudeOrSigned : gravityMagnitudeOrSigned;
        }

        /// <summary>
        /// Terminal velocity of a drag-limited body: v_t = sqrt(2·m·g / (ρ·C_d·A)).
        /// Gravity is supplied as a positive magnitude; monotonic in mass (∝ √m).
        /// </summary>
        /// <param name="massKg">Body mass, kg.</param>
        /// <param name="gravityMagnitude">Gravitational acceleration magnitude, m/s².</param>
        /// <param name="airDensity">Ambient air density, kg/m³.</param>
        /// <param name="dragCoefficient">Dimensionless drag coefficient C_d.</param>
        /// <param name="crossSectionAreaM2">Frontal cross-section area, m².</param>
        /// <returns>Terminal fall speed in m/s, or 0 for invalid inputs.</returns>
        public static float ComputeTerminalVelocity(float massKg, float gravityMagnitude, float airDensity, float dragCoefficient, float crossSectionAreaM2)
        {
            float denominator = airDensity * Mathf.Max(0.01f, dragCoefficient) * Mathf.Max(0.01f, crossSectionAreaM2);
            if (massKg <= 0f || gravityMagnitude <= 0f || denominator <= 0f) return 0f;
            return Mathf.Sqrt(2f * massKg * gravityMagnitude / denominator);
        }

        /// <summary>
        /// Parabolic airtime for a drop of <paramref name="heightM"/> starting at vertical velocity
        /// <paramref name="initialVerticalVelocity"/> (negative when falling), solved from
        /// h = -v₀·t + ½·g·t²  →  t = (-v₀ + sqrt(v₀² + 2·g·h)) / g with g as magnitude.
        /// </summary>
        /// <param name="heightM">Drop height in metres.</param>
        /// <param name="gravityMagnitude">Gravitational acceleration magnitude, m/s².</param>
        /// <param name="initialVerticalVelocity">Signed initial vertical velocity, m/s.</param>
        /// <returns>Airtime in seconds (0 for non-positive heights).</returns>
        public static float ComputeAirtime(float heightM, float gravityMagnitude, float initialVerticalVelocity = 0f)
        {
            float g = Mathf.Abs(gravityMagnitude);
            if (heightM <= 0f || g <= 0f) return 0f;
            return (-initialVerticalVelocity + Mathf.Sqrt(initialVerticalVelocity * initialVerticalVelocity + 2f * g * heightM)) / g;
        }

        /// <summary>
        /// Impact kinetic energy of a landed body, E = ½·m·v², capped at the terminal speed.
        /// </summary>
        /// <param name="massKg">Body mass, kg.</param>
        /// <param name="impactSpeedMps">Vertical impact speed magnitude, m/s.</param>
        /// <param name="terminalVelocityMps">Terminal velocity cap for the body, m/s.</param>
        /// <returns>Impact energy in joules.</returns>
        public static float ComputeImpactEnergy(float massKg, float impactSpeedMps, float terminalVelocityMps)
        {
            float cappedSpeed = terminalVelocityMps > 0f ? Mathf.Min(Mathf.Abs(impactSpeedMps), terminalVelocityMps) : Mathf.Abs(impactSpeedMps);
            return 0.5f * Mathf.Max(0f, massKg) * cappedSpeed * cappedSpeed;
        }

        /// <summary>
        /// Fall height converted to impact energy through the drag model. Below terminal velocity
        /// the impact speed follows v = sqrt(2·g·h); it is clamped to v_t, so energy is monotonic
        /// (quadratic until saturation) in fall height.
        /// </summary>
        /// <param name="fallHeightM">Effective (post-mitigation) fall height, metres.</param>
        /// <param name="massKg">Body mass, kg.</param>
        /// <param name="gravityMagnitude">Gravitational acceleration magnitude, m/s².</param>
        /// <param name="airDensity">Air density, kg/m³.</param>
        /// <param name="dragCoefficient">Drag coefficient C_d.</param>
        /// <param name="crossSectionAreaM2">Frontal area, m².</param>
        /// <returns>Landing impact energy in joules.</returns>
        public static float ComputeLandingEnergy(float fallHeightM, float massKg, float gravityMagnitude, float airDensity, float dragCoefficient, float crossSectionAreaM2)
        {
            float g = Mathf.Abs(gravityMagnitude);
            if (fallHeightM <= 0f || g <= 0f) return 0f;
            float terminal = ComputeTerminalVelocity(massKg, g, airDensity, dragCoefficient, crossSectionAreaM2);
            float impactSpeed = Mathf.Sqrt(2f * g * fallHeightM);
            return ComputeImpactEnergy(massKg, impactSpeed, terminal);
        }

        /// <summary>
        /// Landing damage: linear in the ratio of impact energy to body tolerance (damage-per-joule).
        /// E ≤ tolerance → 0 damage; E = tolerance → 1 point · <see cref="DamageScalePerTolerance"/>.
        /// </summary>
        /// <param name="impactEnergyJ">Impact kinetic energy, joules.</param>
        /// <param name="bodyToleranceJ">Energy the body absorbs injury-free, joules.</param>
        /// <param name="damageScale">Damage points applied when energy equals 100 % over tolerance.</param>
        /// <returns>Damage in [0, damageScale].</returns>
        public static float ComputeLandingDamage(float impactEnergyJ, float bodyToleranceJ, float damageScale)
        {
            if (bodyToleranceJ <= 0f) return 0f;
            float excessRatio = (impactEnergyJ - bodyToleranceJ) / bodyToleranceJ;
            return Mathf.Clamp(excessRatio * damageScale, 0f, damageScale);
        }

        /// <summary>
        /// Crouch-landing mitigation: reduces the effective fall height by
        /// <see cref="CrouchMitigationFactor"/> (0.5×) when the operator lands crouched and rolls.
        /// </summary>
        /// <param name="rawFallHeightM">Unmitigated drop height, metres.</param>
        /// <param name="crouchedOnLanding">Whether the crouch-roll mitigation applies.</param>
        /// <returns>Effective fall height used for energy computation.</returns>
        public static float ApplyCrouchMitigation(float rawFallHeightM, bool crouchedOnLanding)
        {
            return crouchedOnLanding ? Mathf.Max(0f, rawFallHeightM) * CrouchMitigationFactor : Mathf.Max(0f, rawFallHeightM);
        }

        /// <summary>
        /// True when an impact speed exceeds the leg-buckling threshold.
        /// </summary>
        /// <param name="impactSpeedMps">Vertical impact speed magnitude, m/s.</param>
        /// <returns>Whether the legs buckle at this impact speed.</returns>
        public static bool ExceedsLegBucklingThreshold(float impactSpeedMps)
        {
            return Mathf.Abs(impactSpeedMps) >= LegBucklingImpactSpeed;
        }

        private void Awake()
        {
            probe = GetComponent<GroundContactProbe>();
            health = GetComponent<HealthComponent>();
            rigidbody = GetComponent<Rigidbody>();
            wasGrounded = probe == null || probe.IsGrounded;
        }

        private void Update()
        {
            bool grounded = probe != null ? probe.IsGrounded : CheckGroundedFallback();

            if (!wasGrounded && grounded)
            {
                ResolveLanding();
            }
            else if (wasGrounded && !grounded)
            {
                BeginFall();
            }
            else if (falling && transform.position.y > fallPeakY)
            {
                fallPeakY = transform.position.y;
            }

            wasGrounded = grounded;
        }

        private void BeginFall()
        {
            falling = true;
            fallPeakY = transform.position.y;
            fallStartTime = Time.time;
        }

        private void ResolveLanding()
        {
            float rawHeight = CurrentFallHeight;
            float impactSpeed = ResolveImpactVerticalSpeed(rawHeight);
            bool mitigated = probe != null && probe.Stance == OperatorPosture.Crouched;
            float effectiveHeight = ApplyCrouchMitigation(rawHeight, mitigated);

            float energy = ComputeLandingEnergy(effectiveHeight, EffectiveMassKg, Mathf.Abs(SignedGravity), AirDensity, dragCoefficient, crossSectionAreaM2);
            float damage = ComputeLandingDamage(energy, bodyEnergyToleranceJ, maxFallDamage);

            if (damage > 0f && health != null)
            {
                health.TakeDamage(damage);
            }

            float bucklingSpeed = mitigated ? impactSpeed * Mathf.Sqrt(CrouchMitigationFactor) : impactSpeed;
            if (ExceedsLegBucklingThreshold(bucklingSpeed))
            {
                OnLegBuckled?.Invoke(bucklingSpeed);
            }

            falling = false;
            OnGrounded?.Invoke(effectiveHeight, impactSpeed);
        }

        private float ResolveImpactVerticalSpeed(float rawHeight)
        {
            if (rigidbody != null)
            {
                return Mathf.Abs(rigidbody.linearVelocity.y);
            }

            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                return Mathf.Abs(controller.velocity.y);
            }

            float g = Mathf.Abs(SignedGravity);
            float speed = rawHeight > 0f ? Mathf.Sqrt(2f * g * rawHeight) : 0f;
            float terminal = TerminalVelocity;
            return terminal > 0f ? Mathf.Min(speed, terminal) : speed;
        }

        private bool CheckGroundedFallback()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down, 0.35f, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
