using UnityEngine;

namespace VEVE.AI
{
    /// <summary>
    /// Pure throw decision + ballistic arc heuristic for AI grenade usage (H2):
    /// throws only when engaged, inside band [min,max] range, after cooldown;
    /// arc heuristic lobs the grenade so it lands near the target without a
    /// full ballistic solver.
    /// </summary>
    public static class AiThrowRules
    {
        public const float MinThrowRangeM = 4f;
        public const float MaxThrowRangeM = 16f;
        public const float ForwardMps = 12f;
        public const float MinUpMps = 2f;
        public const float MaxUpMps = 8f;

        public static bool ShouldThrow(bool engaged, float distanceM, float maxRangeM, bool cooldownElapsed)
        {
            if (!engaged) return false;
            if (!cooldownElapsed) return false;
            float d = float.IsNaN(distanceM) ? 0f : distanceM;
            float max = float.IsNaN(maxRangeM) ? MaxThrowRangeM : maxRangeM;
            return d >= MinThrowRangeM && d <= Mathf.Max(MinThrowRangeM, max);
        }

        /// <summary>Lob heuristic: flat forward + clamped upward component growing with distance.</summary>
        public static Vector3 ThrowVelocity(Vector3 fromPosition, Vector3 targetPosition)
        {
            Vector3 to = targetPosition - fromPosition;
            float dist = to.magnitude;
            if (dist < 0.05f) return Vector3.zero;
            Vector3 flat = to; flat.y = 0f;
            float flatDist = flat.magnitude;
            if (flatDist < 0.05f) return Vector3.zero;
            Vector3 flatDir = flat / flatDist;
            float up = Mathf.Clamp(dist * 0.35f, MinUpMps, MaxUpMps);
            return flatDir * ForwardMps + Vector3.up * up;
        }
    }
}
