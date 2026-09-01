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
