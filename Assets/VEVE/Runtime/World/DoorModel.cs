namespace VEVE.World
{
    using UnityEngine;

    /// <summary>Discrete states of an interactable door.</summary>
    public enum DoorState { Locked, Closed, Open, Breached }

    /// <summary>
    /// Pure, deterministic door interaction math so it is fully unit-testable:
    /// lock levels 0-3 (unlocked/jimple/padlock/heavy deadbolt); integrity gate for breaching.
    /// </summary>
    public static class DoorModel
    {
        public const float BaseKickDamage = 12f;
        public const float KickDamagePerLockLevel = 6f;
        public const float KickNoiseLoudness = 45f;
        public const float PickSecondsBase = 2.4f;
        public const float PickSecondsPerLockLevel = 1.6f;
        public const float BreachJoulesPerKg = 520f;
        public const float MinChargeKg = 0.05f;
        public const float IntegrityBreakThreshold = 0f;
        public const float ForceOpenIntegrity = 30f;

        public static float KickDamage(int lockLevel)
        {
            int clamped = lockLevel > 0 ? lockLevel : 0;
            float damage = BaseKickDamage + KickDamagePerLockLevel * clamped;
            return damage > 60f ? 60f : damage;
        }

        public static float PickSeconds(int lockLevel, bool hasLockpickKit)
        {
            int clamped = lockLevel > 0 ? lockLevel : 0;
            float baseTime = PickSecondsBase + PickSecondsPerLockLevel * clamped;
            return hasLockpickKit ? baseTime * 0.6f : baseTime;
        }

        public static float BreachDamage(float chargeKg)
        {
            return BreachJoulesPerKg * (chargeKg > MinChargeKg ? chargeKg : MinChargeKg);
        }

        /// <summary>True when a door can be brute-forced open below this remaining integrity.</summary>
        public static bool CanForceOpen(float integrity)
        {
            return integrity <= ForceOpenIntegrity;
        }

        /// <summary>
        /// Resolve the state after one kick. Unlatched (or integrity destroyed) doors breach;
        /// already-open or breached doors are untouched.
        /// </summary>
        public static DoorState ResolveKick(DoorState state, float integrityAfterKick, bool unlocked)
        {
            if (state == DoorState.Breached || state == DoorState.Open) return state;
            if (unlocked || integrityAfterKick <= IntegrityBreakThreshold) return DoorState.Breached;
            return state;
        }
    }
}

