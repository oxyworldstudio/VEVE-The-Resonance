using UnityEngine;

namespace VEVE.AI
{
    /// <summary>
    /// Central human-factors tuning curves. Everything monotonic and
    /// unit-testable; live simulation consumers were wired in the same pass
    /// (EnemyAwareness TTA, posture-driven patrol density).
    /// </summary>
    public static class TuningRules
    {
        /// <summary>Physical startle 0.62s + decision load: skill 0..1 compresses toward ~0.62s (never under a human reflex floor).</summary>
        public const float ReflexFloorSeconds = 0.62f;
        public const float NoviceAcquireSeconds = 1.5f;

        public static float TimeToAcquireSeconds(float skill01)
        {
            if (float.IsNaN(skill01)) skill01 = 0f;
            float s = Mathf.Clamp01(skill01);
            return Mathf.Lerp(NoviceAcquireSeconds, ReflexFloorSeconds, Mathf.Pow(s, 1.35f));
        }

        /// <summary>Radio channel spacing by urgency tier (0 calm / 1 contact / 2 panic): tighter priority.</summary>
        public static float CadenceSeconds(int urgencyTier)
        {
            switch (urgencyTier)
            {
                case 1: return 4.0f;
                case 2: return 2.0f;
                default: return 8.0f;
            }
        }

        /// <summary>B4 posture patrolDensity01 (0.05..1) feeds the generated layout density: 0.5x..1.7x the authored base.</summary>
        public static float PatrolDensityFromPosture(float authoredBaseDensity, float postureDelta)
        {
            float d = Mathf.Clamp01(postureDelta);
            return Mathf.Clamp(authoredBaseDensity * (0.5f + 1.2f * d), 0.05f, 1f);
        }

        /// <summary>Distance-aware engagement range bias: optic users push out, novices close in (simple skill/zoom mix).</summary>
        public static float PreferredEngagementRange(float maxEffectiveRange, float skill01, float zoomMax)
        {
            float s = Mathf.Clamp01(skill01);
            float zoom = Mathf.Max(1f, zoomMax);
            float factor = Mathf.Clamp01(0.35f + 0.35f * s + 0.06f * Mathf.Min(zoom, 10f) * 0.35f);
            return maxEffectiveRange * factor;
        }
    }
}
