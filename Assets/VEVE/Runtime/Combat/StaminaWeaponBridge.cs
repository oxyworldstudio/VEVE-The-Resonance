using UnityEngine;

namespace VEVE.Combat
{
    /// <summary>
    /// W-H5 stamina/weapon coupling: every shot drains sprint-equivalent effort from the
    /// rig's <see cref="StaminaSystem"/>, and sustained fire on an empty tank is blocked.
    /// Bind on the same GameObject as both components (same rig). Pure rules live in
    /// <see cref="StaminaWeaponRules"/> so tests run without a live loop.
    /// </summary>
    public sealed class StaminaWeaponBridge : MonoBehaviour
    {
        [Header("Stamina/Weapon Coupling (W-H5)")]
        [Tooltip("Stamina tank drained by this rig's weapon fire. Same GameObject when left empty.")]
        [SerializeField] private StaminaSystem stamina;

        [Tooltip("Weapon whose shots pay the stamina cost. Same GameObject when left empty.")]
        [SerializeField] private Weapon weapon;

        /// <summary>Sprint-equivalent seconds of stamina paid per shot (pure, deterministic; session-configurable).</summary>
        public static float ShotCostSeconds = 0.35f;

        private Weapon boundWeapon;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Resolves missing references on the same rig; safe to call repeatedly.</summary>
        public void Bind()
        {
            if (stamina == null) stamina = GetComponent<StaminaSystem>();
            if (weapon == null) weapon = GetComponent<Weapon>();
            if (boundWeapon != weapon)
            {
                if (boundWeapon != null) boundWeapon.ShotFired -= OnShotFired;
                boundWeapon = weapon;
                if (boundWeapon != null) boundWeapon.ShotFired += OnShotFired;
            }
        }

        /// <summary>Lazily resolves the rig's stamina tank (rigs may bind before Awake runs).</summary>
        private StaminaSystem RigStamina
        {
            get
            {
                if (stamina == null) stamina = GetComponent<StaminaSystem>();
                return stamina;
            }
        }

        private void OnDestroy()
        {
            if (boundWeapon != null)
            {
                boundWeapon.ShotFired -= OnShotFired;
                boundWeapon = null;
            }
        }

        private void OnShotFired()
        {
            if (RigStamina == null) return;
            stamina.DrainSprint(ShotCostSeconds);
        }

        /// <summary>HUD-facing gate: true while <see cref="StaminaWeaponRules.CanFire"/> refuses shots on this rig.</summary>
        public bool FireBlockedByExhaustion
        {
            get
            {
                if (RigStamina == null) return false;
                return !StaminaWeaponRules.CanFire(stamina.StaminaPercentage);
            }
        }
    }

    /// <summary>
    /// Pure W-H5 fire gate: shooting stays legal down to a small stamina floor so a spent
    /// operator can still fight out of a sprint-empty state, but never on fumes.
    /// </summary>
    public static class StaminaWeaponRules
    {
        /// <summary>Minimum normalized stamina (0..1) required to pull the trigger.</summary>
        public const float FireStaminaFloor = 0.08f;

        /// <summary>True when the normalized stamina permits a shot (NaN-safe: refuse).</summary>
        public static bool CanFire(float stamina01)
        {
            if (float.IsNaN(stamina01)) return false;
            return stamina01 >= FireStaminaFloor;
        }
    }
}
