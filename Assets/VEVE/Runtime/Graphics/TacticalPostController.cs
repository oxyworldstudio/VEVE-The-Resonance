using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Runtime fullscreen post-processing controller for the built-in pipeline.
    /// Blits <see cref="PostProcessProfile"/>-authored values through the
    /// "VEVE/TacticalPost" CGPROGRAM image effect. Degrades gracefully: when the
    /// shader asset is missing or a scripted render pipeline (URP/HDRP) is
    /// active, the component disables itself with a warning instead of throwing.
    /// A URP Volume bridge is the documented next step, not part of this class.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TacticalPostController : MonoBehaviour
    {
        /// <summary>Exact shader asset name resolved via Shader.Find.</summary>
        public static string ShaderName => "VEVE/TacticalPost";

        /// <summary>
        /// Test hook: when true, the shader lookup is forced to fail so the
        /// missing-shader self-disable path is deterministic in headless
        /// batchmode runs (where Shader.Find behavior may vary). Never set in
        /// production code.
        /// </summary>
        public static bool ForceMissingShaderForTests;

        [SerializeField] private PostProcessProfile profile;
        [SerializeField] private VEVE.Realism.QualityLevel qualityLevel = VEVE.Realism.QualityLevel.High;

        private Material material;
        private bool featureEnabled = true;

        /// <summary>
        /// Master feature toggle. Setting true re-runs availability checks
        /// (<see cref="RevalidateBinding"/>); a failed check flips it back to
        /// false, so this flag always reflects the last validated state.
        /// </summary>
        public bool Enabled
        {
            get => featureEnabled;
            set
            {
                featureEnabled = value;
                if (value) RevalidateBinding();
            }
        }

        /// <summary>Quality tier gating the enabled effect set via <see cref="PostQualityRules"/>.</summary>
        public VEVE.Realism.QualityLevel QualityTier
        {
            get => qualityLevel;
            set => qualityLevel = value;
        }

        /// <summary>True when the controller holds a live material and is allowed to run.</summary>
        public bool IsOperational => featureEnabled && material != null;

        /// <summary>Currently bound profile, or null when none was found or assigned.</summary>
        public PostProcessProfile CurrentProfile => profile;

        /// <summary>Resolved effect material, or null before a successful binding.</summary>
        public Material EffectMaterial => material;

        /// <summary>
        /// Rebinds the consumed profile at runtime (scene wiring, tests). Null is
        /// tolerated: rendering then uses a neutral parameter set.
        /// </summary>
        public void SetProfile(PostProcessProfile newProfile)
        {
            profile = newProfile;
        }

        /// <summary>
        /// Re-evaluates pipeline family and shader availability and (re)creates
        /// the effect material. Idempotent and null-safe: on any unsupported
        /// state the component disables itself with a warning. Call after a
        /// runtime render-pipeline switch or from tests where the Unity
        /// lifecycle did not run OnEnable.
        /// </summary>
        public void RevalidateBinding()
        {
            // Scripted pipelines never dispatch OnRenderImage; fail closed via
            // the pure family rule instead of silently rendering nothing.
            string family = PipelineCompat.ActivePipelineFamily();
            if (PostQualityRules.ControllerDisabledForFamily(family))
            {
                Debug.LogWarning($"[TacticalPost] Pipeline family '{family}' is not supported by the built-in fullscreen effect; controller disabled (URP Volume bridge is a planned next step).");
                SelfDisable();
                return;
            }

            Shader shader = ForceMissingShaderForTests ? null : Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[TacticalPost] Shader '{ShaderName}' not found; controller disabled.");
                SelfDisable();
                return;
            }

            if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnEnable()
        {
            RevalidateBinding();
        }

        private void Start()
        {
            // Null-safe auto-wiring: an unassigned profile is looked up once; a
            // missing one simply leaves the neutral pass active.
            if (profile == null) profile = FindFirstObjectByType<PostProcessProfile>();
        }

        private void OnDisable()
        {
            if (material != null)
            {
                // Edit-mode (test) teardown must not use the deferred runtime Destroy.
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
                material = null;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!IsOperational)
            {
                UnityEngine.Graphics.Blit(source, destination);
                return;
            }

            PostParams prms = PostParameterMapper.Map(profile, qualityLevel);
            if (!prms.AnyEffectActive)
            {
                UnityEngine.Graphics.Blit(source, destination);
                return;
            }

            ApplyToMaterial(material, prms);
            UnityEngine.Graphics.Blit(source, destination, material);
        }

        /// <summary>Writes mapped parameters onto the effect material; null target is ignored.</summary>
        public void ApplyToMaterial(Material target, PostParams prms)
        {
            if (target == null) return;
            target.SetFloat("_VignetteIntensity", prms.VignetteIntensity);
            target.SetFloat("_VignetteSmoothness", prms.VignetteSmoothness);
            target.SetFloat("_GrainIntensity", prms.GrainIntensity);
            target.SetFloat("_ChromaticAberration", prms.ChromaticAberration);
            target.SetFloat("_Exposure", prms.Exposure);
            target.SetFloat("_Contrast", prms.Contrast);
            target.SetFloat("_Saturation", prms.Saturation);
            target.SetFloat("_TonemapSwitch", prms.TonemapSwitch);
            target.SetFloat("_LensDirtStrength", prms.LensDirtStrength);
        }

        private void SelfDisable()
        {
            featureEnabled = false;
            enabled = false;
        }
    }
}
