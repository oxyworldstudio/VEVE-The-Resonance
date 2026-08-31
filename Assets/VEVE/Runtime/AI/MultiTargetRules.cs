using System.Collections.Generic;
using UnityEngine;

namespace VEVE.AI
{
    /// <summary>
    /// Pure target-selection rules for multi-pawn sessions: the brain engages
    /// the nearest live pawn, and never throws when the candidate list is
    /// empty/destroyed. Fallback semantics match offline play exactly.
    /// </summary>
    public static class MultiTargetRules
    {
        public static Transform ChooseNearest(IReadOnlyList<Transform> candidates, Vector3 from, float maxSqrDistance = float.MaxValue)
        {
            if (candidates == null) return null;
            Transform best = null;
            float bestD = maxSqrDistance;
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform t = candidates[i];
                if (t == null) continue;
                float d = (t.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        public static bool ShouldPreferPawns(int pawnCount)
        {
            return pawnCount > 0;
        }
    }
}
