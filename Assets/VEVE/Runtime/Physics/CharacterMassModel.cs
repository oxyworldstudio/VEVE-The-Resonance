using System;
using UnityEngine;
using VEVE;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    /// <summary>
    /// Rigid-body mass model for an equipped operator. Combines base body mass with
    /// <see cref="PhysicalInventory"/> load, shifts the center of mass with load ratio,
    /// scales inertia accordingly, and projects the center of mass onto the ground plane
    /// for stance-based stability checks.
    /// </summary>
    public sealed class CharacterMassModel : MonoBehaviour
    {
        [SerializeField] private float bodyMassKg = 80f;
        [SerializeField] private float standingCoMHeightM = 0.95f;
        [SerializeField] private float baseInertiaKgM2 = 12f;
        [SerializeField] private RealismConfig realismConfig;

        private PhysicalInventory inventory;
        private GroundContactProbe probe;

        /// <summary>Base body mass without equipment, in kilograms.</summary>
        public float BodyMassKg
        {
            get => bodyMassKg;
            set => bodyMassKg = Mathf.Max(1f, value);
        }

        /// <summary>Equipment mass carried by the attached <see cref="PhysicalInventory"/>, in kilograms.</summary>
        public float EquipmentMassKg => inventory != null ? inventory.TotalMassKg : 0f;

        /// <summary>Total wet mass: body plus carried kit, in kilograms.</summary>
        public float TotalMassKg => BodyMassKg + EquipmentMassKg;

        /// <summary>
        /// Volumetric load ratio of the attached inventory in [0, 1]; drives CoM shift and inertia scaling.
        /// </summary>
        public float LoadRatio => inventory != null ? inventory.LoadRatio : 0f;

        /// <summary>Height of the center of mass above the feet anchor for the current stance, in metres.</summary>
        public float CenterOfMassHeight => ComputeCoMHeight(standingCoMHeightM, LoadRatio, CurrentStance);

        /// <summary>Local (x, y, z) offset of the shifted center of mass relative to the unloaded hip anchor.</summary>
        public Vector3 CenterOfMassOffset => ComputeCoMOffset(LoadRatio);

        /// <summary>Mass-scaled moment of inertia about the vertical axis, in kg·m².</summary>
        public float InertiaKgM2 => ComputeInertia(baseInertiaKgM2, TotalMassKg, BodyMassKg, LoadRatio);

        /// <summary>Stance reported by the sibling <see cref="GroundContactProbe"/> when present.</summary>
        public OperatorPosture CurrentStance => probe != null ? probe.Stance : OperatorPosture.Standing;

        /// <summary>
        /// World-space projection of the center of mass straight down onto the probed ground plane
        /// (or the transform's foot level when no probe is available). Used for support-polygon
        /// stability checks.
        /// </summary>
        /// <returns>Ground-plane CoM projection in world coordinates.</returns>
        public Vector3 CoMGroundProjection()
        {
            return CoMGroundProjection(CurrentStance);
        }

        /// <summary>
        /// World-space projection of the center of mass for an explicit stance.
        /// </summary>
        /// <param name="stance">Stance whose CoM height is used for the projection.</param>
        /// <returns>Ground-plane CoM projection in world coordinates.</returns>
        public Vector3 CoMGroundProjection(OperatorPosture stance)
        {
            Vector3 comWorld = transform.position
                + transform.TransformDirection(CenterOfMassOffset)
                + Vector3.up * ComputeCoMHeight(standingCoMHeightM, LoadRatio, stance);

            float groundY = probe != null && probe.IsGrounded ? probe.GroundPoint.y : transform.position.y;
            return new Vector3(comWorld.x, groundY, comWorld.z);
        }

        /// <summary>
        /// Horizontal distance between the standing-anchor column and the ground-projected CoM
        /// for an explicit stance. Positive offset shifts the CoM backwards under load.
        /// </summary>
        /// <param name="stance">Stance to evaluate.</param>
        /// <returns>Horizontal CoM excursion from the anchor column, in metres.</returns>
        public float CoMLateralExcursion(OperatorPosture stance)
        {
            Vector3 projected = CoMGroundProjection(stance);
            Vector3 anchor = new Vector3(transform.position.x, projected.y, transform.position.z);
            return Vector3.Distance(projected, anchor);
        }

        /// <summary>
        /// Static support-stability margin: distance from the projected CoM to the support polygon
        /// centre, normalised by the maximum lean radius for the stance (from
        /// <see cref="CharacterPhysics.CalculateSupportPolygonArea"/> and
        /// <see cref="CharacterPhysics.CalculateMaxLeanAngle"/>).
        /// </summary>
        /// <param name="stance">Stance to evaluate; crouch and prone widen the stability margin.</param>
        /// <param name="leftFoot">World-space left foot contact point.</param>
        /// <param name="rightFoot">World-space right foot contact point.</param>
        /// <returns>Stability margin in [0, 1]; 0 means the CoM has left the support base.</returns>
        public float StabilityMargin(OperatorPosture stance, Vector3 leftFoot, Vector3 rightFoot)
        {
            Vector3 projected = CoMGroundProjection(stance);
            Vector3 supportCentre = (leftFoot + rightFoot) * 0.5f;
            float supportHalfWidth = Vector3.Distance(leftFoot, rightFoot) * 0.5f + 0.15f;
            float comHeight = Mathf.Max(0.05f, CenterOfMassHeight);
            float horizontal = Vector3.Distance(
                new Vector3(projected.x, 0f, projected.z),
                new Vector3(supportCentre.x, 0f, supportCentre.z));
            float leanCapacity = Mathf.Tan(CharacterPhysics.CalculateMaxLeanAngle(comHeight, supportHalfWidth) * Mathf.Deg2Rad) * comHeight;
            return Mathf.Clamp01(1f - horizontal / Mathf.Max(0.01f, leanCapacity));
        }

        /// <summary>
        /// Pure stance-aware center-of-mass height model. A loaded pack raises and shifts the CoM;
        /// crouch folds it toward 0.85×, prone toward 0.35× of its standing value.
        /// </summary>
        /// <param name="standingCoMHeight">Unloaded standing CoM height above the feet, metres.</param>
        /// <param name="loadRatio">Inventory load ratio in [0, 1].</param>
        /// <param name="stance">Stance to evaluate.</param>
        /// <returns>CoM height above the feet anchor, metres.</returns>
        public static float ComputeCoMHeight(float standingCoMHeight, float loadRatio, OperatorPosture stance)
        {
            float loaded = standingCoMHeight * (1f + 0.12f * Mathf.Clamp01(loadRatio));
            switch (stance)
            {
                case OperatorPosture.Crouched: return loaded * 0.85f;
                case OperatorPosture.Prone: return loaded * 0.35f;
                default: return loaded;
            }
        }

        /// <summary>
        /// Pure CoM offset model: loaded carries shift the CoM rearward (-Z) and slightly upward as
        /// a fraction of load ratio. Returned in the character's local space.
        /// </summary>
        /// <param name="loadRatio">Inventory load ratio in [0, 1].</param>
        /// <returns>Local-space CoM offset.</returns>
        public static Vector3 ComputeCoMOffset(float loadRatio)
        {
            float ratio = Mathf.Clamp01(loadRatio);
            return new Vector3(0f, 0.06f * ratio, -0.12f * ratio);
        }

        /// <summary>
        /// Pure inertia scaling: I = I_base · (M_total / M_body) · (1 + 0.4 · loadRatio). The extra
        /// factor captures mass distributed away from the spin axis (pack, weapon).
        /// </summary>
        /// <param name="baseInertia">Unloaded base inertia, kg·m².</param>
        /// <param name="totalMassKg">Body plus kit mass, kg.</param>
        /// <param name="bodyMassKg">Body-only mass, kg.</param>
        /// <param name="loadRatio">Inventory load ratio in [0, 1].</param>
        /// <returns>Scaled moment of inertia, kg·m².</returns>
        public static float ComputeInertia(float baseInertia, float totalMassKg, float bodyMassKg, float loadRatio)
        {
            float massFactor = bodyMassKg > 0f ? Mathf.Max(0f, totalMassKg) / bodyMassKg : 1f;
            return baseInertia * massFactor * (1f + 0.4f * Mathf.Clamp01(loadRatio));
        }

        /// <summary>
        /// Returns the configured air density (kg/m³) from the linked
        /// <see cref="RealismConfig"/> at sea level, falling back to the standard 1.225.
        /// </summary>
        public float AirDensityKgPerM3 => realismConfig != null ? realismConfig.AirDensitySeaLevel : StandardAirDensity;

        /// <summary>ISA sea-level air density constant in kg/m³.</summary>
        public const float StandardAirDensity = 1.225f;

        private void Awake()
        {
            inventory = GetComponent<PhysicalInventory>();
            probe = GetComponent<GroundContactProbe>();
        }
    }
}
