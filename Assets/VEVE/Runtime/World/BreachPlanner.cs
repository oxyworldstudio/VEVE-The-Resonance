namespace VEVE.World
{
    using UnityEngine;

    /// <summary>
    /// W-H7 breach composer component: binds one <see cref="DoorSystem"/> and exposes the pure
    /// <see cref="BreachRules.Plan"/> decision for that door's live snapshot. Holds no state of
    /// its own beyond the reference; plans are computed per call (cached per call, no stale
    /// caching across calls). Null-safe (unbound or destroyed door →
    /// <see cref="BreachMethod.None"/>) and edit-mode safe (no scene/physics access, pure math
    /// only), so it can run inside EditMode tests and preview tooling.
    /// </summary>
    public sealed class BreachPlanner : MonoBehaviour
    {
        [SerializeField] private DoorSystem door;

        /// <summary>The bound door, or null.</summary>
        public DoorSystem Door => door;

        /// <summary>Runtime/scene rebinding hook (keeps the serialized reference authoritative).</summary>
        /// <param name="doorSystem">Door to plan against; null disables planning.</param>
        public void SetDoor(DoorSystem doorSystem)
        {
            door = doorSystem;
        }

        /// <summary>
        /// Compose the breach plan for the bound door's own state with the operator's carried kit.
        /// Deterministic and side-effect free: safe to call every frame (each call recomputes from
        /// the door's current snapshot and reuses that single computation).
        /// </summary>
        /// <param name="hasKit">Whether the operator carries a lockpick kit.</param>
        /// <param name="hasCharge">Whether the operator carries a breaching charge.</param>
        /// <returns>The pure <see cref="BreachRules"/> plan; a <see cref="BreachMethod.None"/> plan when no door is bound.</returns>
        public BreachPlan CurrentPlan(bool hasKit, bool hasCharge)
        {
            if (door == null) return BreachRules.Plan(DoorState.Open, 0, 0f, 0f, hasKit, hasCharge);
            return BreachRules.Plan(door.State, door.LockLevel, door.Integrity, 0f, hasKit, hasCharge);
        }
    }
}
