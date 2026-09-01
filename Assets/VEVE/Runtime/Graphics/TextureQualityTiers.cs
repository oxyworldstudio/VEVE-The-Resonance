using System;
using UnityEngine;
using QualityLevel = VEVE.Realism.QualityLevel;

namespace VEVE.Graphics
{
    /// <summary>
    /// Texture quality tiers (roadmap F/G1): resolution per QualityLevel with
    /// power-of-two normalization, mip budget and aniso levels. Pure statics.
    /// </summary>
    public static class TextureQualityRules
    {
        public const int MinSidePixels = 256;
        public const int MaxSidePixels = 4096;

        /// <summary>Side pixels for a quality level (Low 512 / Medium 1024 / High 2048 / Ultra 4096).</summary>
        public static int ResolutionFor(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return 512;
                case QualityLevel.Medium: return 1024;
                case QualityLevel.High: return 2048;
                case QualityLevel.Ultra: return 4096;
                default: return 1024;
            }
        }

        /// <summary>Normalized to [MinSidePixels, MaxSidePixels] and rounded UP to power of two.</summary>
        public static int NormalizeSide(int requestedSide)
        {
            if (requestedSide < MinSidePixels) return MinSidePixels;
            if (requestedSide > MaxSidePixels) return MaxSidePixels;
            int p = MinSidePixels;
            while (p < requestedSide) p <<= 1;
            return p;
        }

        /// <summary>Full mip chain for a square texture (1 + floor(log2(side))).</summary>
        public static int MipCountFor(int sidePixels)
        {
            if (sidePixels <= 0) return 1;
            int mips = 0;
            while (sidePixels > 0) { mips++; sidePixels >>= 1; }
            return mips;
        }

        /// <summary>Anisotropic filtering level per quality (0/2/4/8 â€” never above hardware-typical 16).</summary>
        public static int AnisoFor(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return 0;
                case QualityLevel.Medium: return 2;
                case QualityLevel.High: return 4;
                case QualityLevel.Ultra: return 8;
                default: return 2;
            }
        }

        /// <summary>Seconds budget per 1024Â² texture generation, scaled by area (keeps Tick chunking honest).</summary>
        public static float GenerationBudgetSeconds(int sidePixels, float baseBudgetMs)
        {
            float area = (float)NormalizeSide(sidePixels) * NormalizeSide(sidePixels);
            float scaled = Mathf.Max(0.5f, baseBudgetMs) / 1000f * (area / (1024f * 1024f));
            return scaled < 0.25f ? 0.25f : (scaled > 4f ? 4f : scaled);
        }
    }
}
