using UnityEngine;
using VEVE.AI;

namespace VEVE.Combat
{
    /// <summary>
    /// H2: AI grenade thrower — driven by TacticalAICore decisions (offloaded to
    /// the squad's grenade budget by the designer). Uses the pure AiThrowRules
    /// band/arc heuristic and reuses GrenadeProjectile (owner = 0, PvE friendly
    /// immunity rules apply per design).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeThrowerAI : MonoBehaviour
    {
        [SerializeField] private float throwRangeM = 14f;
        [SerializeField] private float cooldownSeconds = 8f;
        [SerializeField] private Transform target;

        private float lastThrow = -Mathf.Infinity;

        public void SetTarget(Transform t) { target = t; }
        public float CooldownSeconds => cooldownSeconds;
        public float LastThrowTime => lastThrow;

        /// <summary>
        /// W-H6: engagement band ceiling (meters) used by <see cref="TryThrowAt"/> when squad
        /// logic drives the throw; distance must land in [<see cref="AiThrowRules.MinThrowRangeM"/>,
        /// <see cref="EngageRangeM"/>].
        /// </summary>
        public float EngageRangeM = 14f;

        /// <summary>W-H6: squad-facing target accessor (same field as <see cref="SetTarget"/>); null = not engaged.</summary>
        public Transform Target
        {
            get { return target; }
            set { target = value; }
        }

        /// <summary>
        /// W-H6: distance-resolving throw overload. Resolves the thrower→<paramref name="targetTransform"/>
        /// distance, validates through <see cref="AiThrowRules.ShouldThrow"/> (engaged = distance within
        /// [<see cref="AiThrowRules.MinThrowRangeM"/>, <see cref="EngageRangeM"/>; cooldownElapsed =
        /// <c>nowTime - lastThrow &gt;= cooldownSeconds</c>) and spawns the grenade with the
        /// <see cref="AiThrowRules.ThrowVelocity"/> arc. Unlike <see cref="TryThrow"/> this path carries no
        /// frame guard: it is deterministic against the explicit clock, so EditMode tests may drive it.
        /// </summary>
        /// <param name="targetTransform">Engagement target; null refuses.</param>
        /// <param name="nowTime">Unscaled game clock in seconds driving the cooldown bookkeeping.</param>
        /// <returns>True when a grenade was spawned.</returns>
        public bool TryThrowAt(Transform targetTransform, float nowTime)
        {
            if (targetTransform == null) return false;
            Vector3 from = transform.position;
            float distance = Vector3.Distance(from, targetTransform.position);
            bool engaged = distance <= EngageRangeM && distance >= AiThrowRules.MinThrowRangeM;
            bool cooldownElapsed = nowTime - lastThrow >= cooldownSeconds;
            if (!AiThrowRules.ShouldThrow(engaged, distance, EngageRangeM, cooldownElapsed)) return false;
            Vector3 vel = AiThrowRules.ThrowVelocity(from, targetTransform.position);
            if (vel.sqrMagnitude < 0.01f) return false;
            lastThrow = nowTime;
            SpawnGrenadeAt(from, vel);
            return true;
        }

        /// <summary>W-H6: shared spawn path mirroring <see cref="TryThrow"/>'s grenade construction (owner 0, PvE immunity).</summary>
        private void SpawnGrenadeAt(Vector3 fromPosition, Vector3 velocity)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AI_Grenade";
            go.transform.localScale = Vector3.one * 0.14f;
            go.transform.position = fromPosition + Vector3.up * 0.3f;
            var proj = go.AddComponent<GrenadeProjectile>();
            proj.Configure(velocity, GrenadeRules.DefaultRadiusM, GrenadeRules.DefaultBlastEnergyJ, 0, GrenadeRules.DefaultFuseSeconds);
        }

        /// <summary>Called by TacticalAICore/squad logic when it decides a grenade throw.</summary>
        public bool TryThrow(Vector3 fromPosition, float nowTime)
        {
            if (target == null) return false;
            if (nowTime - lastThrow < cooldownSeconds) return false;
            if (Time.frameCount == 0) return false; // edit-mode guard

            Vector3 vel = AiThrowRules.ThrowVelocity(fromPosition, target.position);
            if (vel.sqrMagnitude < 0.01f) return false;

            lastThrow = nowTime;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AI_Grenade";
            go.transform.localScale = Vector3.one * 0.14f;
            go.transform.position = fromPosition + Vector3.up * 0.3f;
            var proj = go.AddComponent<GrenadeProjectile>();
            proj.Configure(vel, GrenadeRules.DefaultRadiusM, GrenadeRules.DefaultBlastEnergyJ, 0, GrenadeRules.DefaultFuseSeconds);
            return true;
        }
    }
}
