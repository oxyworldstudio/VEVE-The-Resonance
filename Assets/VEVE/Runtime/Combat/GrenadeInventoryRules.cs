using UnityEngine;

namespace VEVE.Combat
{
    /// <summary>
    /// Grenade accounting rules (W15): throws are finite, decrement only on a
    /// successful throw, restock caps at the mission allowance. Pure statics.
    /// </summary>
    public static class GrenadeInventoryRules
    {
        public const int MaxPerMission = 3;

        public static bool CanThrow(int count) => count > 0;

        public static int AfterThrow(int count) => Mathf.Max(0, count - 1);

        /// <summary>Restock adds one full allowance, capped at the mission max; never reduces.</summary>
        public static int Restock(int current, int max)
        {
            int c = Mathf.Max(0, current);
            int cap = Mathf.Max(0, max);
            int filled = Mathf.Min(c + MaxPerMission, cap);
            return Mathf.Max(c, filled);
        }

        public static string ThrowBlockedReason(int count)
        {
            return count <= 0 ? "out of grenades" : string.Empty;
        }

        public static bool IsUsableCount(int count)
        {
            return count >= 0 && count <= 99;
        }
    }
}
