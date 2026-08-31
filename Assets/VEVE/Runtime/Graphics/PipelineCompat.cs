using System;
using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Render-pipeline compatibility layer (no package compile-deps): detects a
    /// mounted URP/HDRP pipeline by asset type name only and resolves the right
    /// lit-shader per pipeline, so authored materials survive a package install
    /// without re-authoring the scene. When no custom pipeline is assigned the
    /// classic built-in path keeps working byte-identical (WebGL safety rule).
    /// </summary>
    public static class PipelineCompat
    {
        public const string UniversalLit = "Universal Render Pipeline/Lit";
        public const string HighDefinitionLit = "HDRP/Lit";
        public const string BuiltInStandard = "Standard";

        /// <summary>True when a custom scriptable render pipeline asset is active.</summary>
        public static bool CustomPipelineActive =>
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;

        public static string ActivePipelineFamily()
        {
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (asset == null) return "Built-in";
            string t = asset.GetType().FullName ?? asset.GetType().Name;
            if (t.Contains("Universal")) return "Universal";
            if (t.Contains("HighDefinition")) return "HDRP";
            return "CustomSRP";
        }

        public static bool IsUniversal => ActivePipelineFamily() == "Universal";
        public static bool IsHdrp => ActivePipelineFamily() == "HDRP";

        /// <summary>Pure name selection (unit-testable): matches the documented fallback ladder.</summary>
        public static string ShaderNameFor(string pipelineFamily)
        {
            switch (pipelineFamily)
            {
                case "Universal": return UniversalLit;
                case "HDRP": return HighDefinitionLit;
                default: return BuiltInStandard;
            }
        }

        public static Shader ResolveLitShader()
        {
            string preferred = ShaderNameFor(ActivePipelineFamily());
            Shader s = Shader.Find(preferred);
            if (s != null && s.isSupported) return s;
            s = Shader.Find(BuiltInStandard);
            return s; // may be null in a pure-SRP project - callers must tolerate
        }

        /// <summary>Additive-safe property writes: built-in vs SRP naming for gloss/smoothness and albedo.</summary>
        public static void ApplySurface(Material m, Color albedo, float gloss01)
        {
            if (m == null) return;
            m.color = albedo;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Clamp01(gloss01));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Clamp01(gloss01));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", albedo);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            if (m.HasProperty("_GlossyReflections")) m.SetFloat("_GlossyReflections", 1f);
        }
    }
}
