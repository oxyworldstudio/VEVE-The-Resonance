using UnityEngine;

namespace VEVE
{
    /// <summary>
    /// Pure sensory doctrine helpers: a shooter hearing gunfire on a net with
    /// materials does NOT know the source position - triangulation confidence
    /// depends on level, distance and exposure; and a live scope flashes under
    /// low-to-mid sun. All functions are deterministic and unit-testable.
    /// </summary>
    public static class AiAcoustics
    {
        /// <summary>Bearing uncertainty in degrees: louder and closer ⇒ tighter cone.</summary>
        public static float BearingErrorDegrees(float heardLoudness, float distanceMeters)
        {
            if (!(heardLoudness > 0f) || !(distanceMeters > 0f)) return 60f;
            float levelErr = Mathf.Lerp(16f, 2f, Mathf.InverseLerp(1f, 30f, heardLoudness));
            return Mathf.Clamp(levelErr + distanceMeters * 0.04f, 2f, 55f);
        }

        /// <summary>0..1 how much of the true range the listener can judge.</summary>
        public static float RangeConfidence(float heardLoudness, float distanceMeters)
        {
            if (!(heardLoudness > 0f)) return 0f;
            return Mathf.Clamp01(0.3f + heardLoudness * 0.018f - distanceMeters * 0.0012f);
        }

        /// <summary>
        /// Noise-localization estimate biased like a human: bearing error cone that grows
        /// with distance, and a range UNDER-estimate (shockwaves feel closer than they are).
        /// Deterministic on (listener, source, loudness, seed) - replays stay stable.
        /// </summary>
        public static Vector3 EstimateNoisePosition(Vector3 listener, Vector3 realSource,
            float heardLoudness, uint seed)
        {
            Vector3 to = realSource - listener;
            float dist = to.magnitude;
            if (dist < 0.05f) return listener;

            uint h = Mix(seed, listener, realSource, heardLoudness);
            float err = BearingErrorDegrees(heardLoudness, dist);
            float jitter = (h & 0xFFFFu) / 65535f * 2f - 1f;
            float angle = err * jitter;

            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * to.normalized;
            float rc = RangeConfidence(heardLoudness, dist);
            // range underestimates toward the listener: 0.55..1.05 of the true slant range
            float guess = dist * Mathf.Lerp(0.55f, 1.05f, rc);
            return listener + dir * guess;
        }

        /// <summary>
        /// W3 rule: a mounted magnification optic reflects sunlight when the sun sits
        /// 30-75° up; the flash trades exactly on magnification. Red-dot/holo (M<4.5)
        /// or off-window elevation contributes nothing.
        /// </summary>
        public const float GlintMinMagnification = 4.5f;
        public const float GlintMinSunElevationDeg = 30f;
        public const float GlintMaxSunElevationDeg = 75f;
        public const float GlintBonusCeiling = 0.22f;

        public static float ScopeGlintBonus(float scopeMagnificationMax, float sunElevationDeg)
        {
            if (scopeMagnificationMax < GlintMinMagnification) return 0f;
            if (sunElevationDeg < GlintMinSunElevationDeg || sunElevationDeg > GlintMaxSunElevationDeg) return 0f;
            float angleWindow = 1f - Mathf.Abs(sunElevationDeg - 52.5f) / 22.5f;
            float raw = 0.05f + (scopeMagnificationMax - GlintMinMagnification) * 0.015f;
            return Mathf.Clamp(raw * Mathf.Clamp01(angleWindow), 0f, GlintBonusCeiling);
        }

        /// <summary>Stable call-out hash: (reportId, targetPos) -> jitter seed.</summary>
        public static uint CalloutSeed(int reporterId, Vector3 pos)
        {
            return Mix((uint)reporterId, pos, Vector3.zero, 0f);
        }

        private static uint Mix(uint baseSeed, Vector3 a, Vector3 b, float l)
        {
            unchecked
            {
                uint h = 2166136261u ^ baseSeed;
                h = (h ^ (uint)a.x.GetHashCode()) * 16777619u;
                h = (h ^ (uint)a.y.GetHashCode()) * 16777619u;
                h = (h ^ (uint)a.z.GetHashCode()) * 16777619u;
                h = (h ^ (uint)b.x.GetHashCode()) * 16777619u;
                h = (h ^ (uint)b.y.GetHashCode()) * 16777619u;
                h = (h ^ (uint)b.z.GetHashCode()) * 16777619u;
                h = (h ^ (uint)l.GetHashCode()) * 16777619u;
                h ^= h >> 15;
                return h | 1u;
            }
        }
    }
}
