using UnityEngine;

namespace VEVE
{
    public static class SoundPropagation
    {
        public static float HeardLoudness(float sourceLoudness, float distance, float absorption)
        {
            if (sourceLoudness <= 0f || distance < 0f) return 0f;
            float distanceLoss = 1f / (1f + distance * distance * 0.02f);
            return Mathf.Max(0f, sourceLoudness * distanceLoss * Mathf.Clamp01(1f - absorption));
        }
    }
}
