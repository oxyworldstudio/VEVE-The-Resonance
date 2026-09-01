using UnityEngine;

namespace VEVE.Catalog
{
    /// <summary>
    /// Human-factors weapon handling (H1): a trained operator holds a tighter
    /// cone and manages recoil better. Monotonic in skill, NaN-safe, with a
    /// trained-operator baseline (0.75) when no ledger entry exists yet — the
    /// offline host must not be punished for an empty ledger.
    /// </summary>
    public static class WeaponHandlingRules
    {
        public const float DefaultSkill01 = 0.75f;
        public const float SpreadRetentionAtMaxSkill = 0.2f;

        /// <summary>skill 0..100 → 0..1 (skill 75 default baseline).</summary>
        public static float Skill01(int skill)
        {
            if (skill <= 0) return 0f;
            if (skill > 100) return 1f;
            return skill / 100f;
        }

        /// <summary>Cone half-angle in degrees for a base weapon spread; monotonic decreasing in skill.</summary>
        public static float SpreadDegrees(float baseDegrees, int skill)
        {
            if (baseDegrees <= 0f || float.IsNaN(baseDegrees)) return 0f;
            float s = float.IsNaN(Skill01(skill)) ? 0f : Skill01(skill);
            return baseDegrees * (1f - 0.8f * s);
        }

        /// <summary>Recoil impulse multiplier; monotonic decreasing in skill, floored at 0.2.</summary>
        public static float RecoilMultiplier(int skill)
        {
            float s = float.IsNaN(Skill01(skill)) ? 0f : Skill01(skill);
            float m = 1f - 0.8f * s;
            return m < 0.2f ? 0.2f : m;
        }
    }
}
