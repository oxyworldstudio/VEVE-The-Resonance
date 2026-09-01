using VEVE.Realism;

namespace VEVE.Graphics
{
    /// <summary>
    /// Pure, allocation-free rules deciding which tactical post effects are
    /// allowed per quality tier (<see cref="QualityLevel"/>). No Unity Object
    /// state and no rendering calls, so the rules are fully EditMode-testable.
    /// Authored tier ladder: Low keeps vignette + film grain only; Medium adds
    /// chromatic aberration and tonemapping; High/Ultra enable everything.
    /// </summary>
    public static class PostQualityRules
    {
        /// <summary>Effects gateable by quality tier.</summary>
        public enum PostEffect { Vignette, FilmGrain, ChromaticAberration, Tonemapping, ColorGrading, LensDirt }

        /// <summary>
        /// True when the tier may run the effect. Pure and exhaustive:
        /// unknown effects default to disabled (fail-closed).
        /// </summary>
        public static bool Allows(QualityLevel level, PostEffect effect)
        {
            switch (effect)
            {
                case PostEffect.Vignette:
                case PostEffect.FilmGrain:
                    return true;
                case PostEffect.ChromaticAberration:
                case PostEffect.Tonemapping:
                    return level >= QualityLevel.Medium;
                case PostEffect.ColorGrading:
                case PostEffect.LensDirt:
                    return level >= QualityLevel.High;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Pure pipeline-family gate: built-in CGPROGRAM fullscreen effects do
        /// not execute under scripted render pipelines (OnRenderImage never
        /// fires there), so the controller must self-disable. "Universal" and
        /// "HDRP" disable; "Built-in" and "CustomSRP" keep the controller.
        /// </summary>
        /// <param name="pipelineFamily">Family string as produced by PipelineCompat.ActivePipelineFamily().</param>
        public static bool ControllerDisabledForFamily(string pipelineFamily)
        {
            return pipelineFamily == "Universal" || pipelineFamily == "HDRP";
        }
    }
}
