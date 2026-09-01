using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Clamped, shader-ready post parameters produced from a
    /// <see cref="PostProcessProfile"/>. Pure value struct; every field documents
    /// its validated range so the fullscreen shader can consume it verbatim.
    /// </summary>
    public struct PostParams
    {
        /// <summary>0..1: darkened-corner strength (0 disables the vignette).</summary>
        public float VignetteIntensity;
        /// <summary>0.1..2: how far the vignette reach pulls toward the center.</summary>
        public float VignetteSmoothness;
        /// <summary>0..1: animated hash grain amplitude.</summary>
        public float GrainIntensity;
        /// <summary>0..1: radial RGB channel split (1.0 = ~1.5% screen distance).</summary>
        public float ChromaticAberration;
        /// <summary>0.2..3: linear exposure multiplier.</summary>
        public float Exposure;
        /// <summary>0.5..2: contrast around the 0.5 pivot.</summary>
        public float Contrast;
        /// <summary>0..2: Rec709-luma saturation blend.</summary>
        public float Saturation;
        /// <summary>0=none, 1=ACES-approx, 2=Reinhard.</summary>
        public float TonemapSwitch;
        /// <summary>0..1: optional additive lens-dirt strength.</summary>
        public float LensDirtStrength;
        /// <summary>Master switch: false when no gated effect survives quality rules.</summary>
        public bool AnyEffectActive;
    }

    /// <summary>
    /// Pure static mapper from <see cref="PostProcessProfile"/> to
    /// <see cref="PostParams"/>, gated by <see cref="PostQualityRules"/> for the
    /// quality tier. No Unity Object lifecycle is touched (the profile may be a
    /// ScriptableObject.CreateInstance'd instance or a mock), so it is safe to
    /// call from EditMode tests.
    /// </summary>
    public static class PostParameterMapper
    {
        /// <summary>Documented clamps: exposure [0.2, 3], contrast [0.5, 2], saturation [0, 2].</summary>
        public const float ExposureMin = 0.2f, ExposureMax = 3f;
        public const float ContrastMin = 0.5f, ContrastMax = 2f;
        public const float SaturationMin = 0f, SaturationMax = 2f;
        public const float VignetteSmoothnessMin = 0.1f, VignetteSmoothnessMax = 2f;

        /// <summary>Maps all profile grading/effects through quality gates with documented clamps.</summary>
        /// <param name="profile">Authored profile; null yields a fully neutral param set.</param>
        /// <param name="qualityLevel">Quality tier deciding the enabled effect set.</param>
        public static PostParams Map(PostProcessProfile profile, VEVE.Realism.QualityLevel qualityLevel)
        {
            var p = new PostParams
            {
                VignetteIntensity = 0f,
                VignetteSmoothness = 1f,
                GrainIntensity = 0f,
                ChromaticAberration = 0f,
                Exposure = 1f,
                Contrast = 1f,
                Saturation = 1f,
                TonemapSwitch = 0f,
                LensDirtStrength = 0f,
                AnyEffectActive = false
            };
            if (profile == null) return p;

            // Vignette + grain run on every tier (Low baseline).
            if (profile.VignetteEnabled && PostQualityRules.Allows(qualityLevel, PostQualityRules.PostEffect.Vignette))
            {
                p.VignetteIntensity = Clamp01(profile.VignetteIntensity);
                p.VignetteSmoothness = Mathf.Clamp(profile.VignetteSmoothness, VignetteSmoothnessMin, VignetteSmoothnessMax);
                p.AnyEffectActive |= p.VignetteIntensity > 0f;
            }

            if (profile.FilmGrainEnabled && PostQualityRules.Allows(qualityLevel, PostQualityRules.PostEffect.FilmGrain))
            {
                p.GrainIntensity = Clamp01(profile.FilmGrainIntensity);
                p.AnyEffectActive |= p.GrainIntensity > 0f;
            }

            // Medium tier: chromatic aberration + tonemapping.
            if (profile.ChromaticAberrationEnabled && PostQualityRules.Allows(qualityLevel, PostQualityRules.PostEffect.ChromaticAberration))
            {
                p.ChromaticAberration = Clamp01(profile.ChromaticAberrationIntensity);
                p.AnyEffectActive |= p.ChromaticAberration > 0f;
            }

            if (PostQualityRules.Allows(qualityLevel, PostQualityRules.PostEffect.Tonemapping))
            {
                p.TonemapSwitch = TonemapSwitchFor(profile.Tonemapping);
                p.AnyEffectActive |= p.TonemapSwitch != 0f;
            }

            // High/Ultra tier: full color grading.
            if (profile.ColorGradingEnabled && PostQualityRules.Allows(qualityLevel, PostQualityRules.PostEffect.ColorGrading))
            {
                p.Exposure = Mathf.Clamp(profile.ColorGradingExposure, ExposureMin, ExposureMax);
                p.Contrast = Mathf.Clamp(profile.ColorGradingContrast, ContrastMin, ContrastMax);
                p.Saturation = Mathf.Clamp(profile.ColorGradingSaturation, SaturationMin, SaturationMax);
                p.AnyEffectActive |= !Mathf.Approximately(p.Exposure, 1f)
                                     || !Mathf.Approximately(p.Contrast, 1f)
                                     || !Mathf.Approximately(p.Saturation, 1f);
            }

            // Lens dirt is optional and authored only at High/Ultra; the profile
            // has no dedicated field, so strength rides on the bloom toggle.
            if (profile.BloomEnabled && PostQualityRules.Allows(qualityLevel, PostQualityRules.PostEffect.LensDirt))
            {
                p.LensDirtStrength = Clamp01(profile.LensFlareIntensity * 0.5f);
                p.AnyEffectActive |= p.LensDirtStrength > 0f;
            }

            return p;
        }

        /// <summary>Pure enum → shader switch (0 none, 1 ACES-approx, 2 Reinhard). HDR/Neutral collapse to none.</summary>
        public static float TonemapSwitchFor(TonemappingMode mode)
        {
            switch (mode)
            {
                case TonemappingMode.ACES: return 1f;
                case TonemappingMode.Reinhard: return 2f;
                default: return 0f; // Neutral and HDR have no built-in CGPROGRAM equivalent
            }
        }

        private static float Clamp01(float v) => Mathf.Clamp(v, 0f, 1f);
    }
}
