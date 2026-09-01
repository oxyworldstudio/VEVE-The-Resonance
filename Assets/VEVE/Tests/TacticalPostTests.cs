using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VEVE.Realism;

namespace VEVE.Graphics.Tests
{
    public sealed class TacticalPostTests
    {
        private readonly List<Object> tracked = new List<Object>();
        private GameObject holder;

        [TearDown]
        public void TearDown()
        {
            TacticalPostController.ForceMissingShaderForTests = false;
            if (holder != null) { Object.DestroyImmediate(holder); holder = null; }
            foreach (var o in tracked) if (o != null) Object.DestroyImmediate(o);
            tracked.Clear();
        }

        private PostProcessProfile NewProfile()
        {
            var p = ScriptableObject.CreateInstance<PostProcessProfile>();
            tracked.Add(p);
            return p;
        }

        private static void SetFloat(PostProcessProfile p, string field, float value)
        {
            var so = new SerializedObject(p);
            var prop = so.FindProperty(field);
            Assert.IsNotNull(prop, "profile field '{0}' missing", field);
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(PostProcessProfile p, string field, bool value)
        {
            var so = new SerializedObject(p);
            var prop = so.FindProperty(field);
            Assert.IsNotNull(prop, "profile field '{0}' missing", field);
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(PostProcessProfile p, string field, int value)
        {
            var so = new SerializedObject(p);
            var prop = so.FindProperty(field);
            Assert.IsNotNull(prop, "profile field '{0}' missing", field);
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- mapper clamps ----

        [Test]
        public void ExposureClampsToDocumentedRange()
        {
            var profile = NewProfile();
            SetFloat(profile, "colorGradingExposure", 99f);
            Assert.AreEqual(PostParameterMapper.ExposureMax, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).Exposure);
            SetFloat(profile, "colorGradingExposure", -5f);
            Assert.AreEqual(PostParameterMapper.ExposureMin, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).Exposure);
            Assert.AreEqual(3f, PostParameterMapper.ExposureMax);
            Assert.AreEqual(0.2f, PostParameterMapper.ExposureMin);
        }

        [Test]
        public void GrainAndVignetteClampToUnitRange()
        {
            var profile = NewProfile();
            SetFloat(profile, "filmGrainIntensity", 7f);
            SetBool(profile, "applyRealismOverrides", false);
            Assert.AreEqual(1f, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.Low).GrainIntensity);

            SetFloat(profile, "vignetteIntensity", -2f);
            SetFloat(profile, "vignetteSmoothness", 9f);
            var hi = PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High);
            Assert.AreEqual(0f, hi.VignetteIntensity);
            Assert.AreEqual(PostParameterMapper.VignetteSmoothnessMax, hi.VignetteSmoothness);

            SetFloat(profile, "vignetteSmoothness", -1f);
            Assert.AreEqual(PostParameterMapper.VignetteSmoothnessMin, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).VignetteSmoothness);
        }

        [Test]
        public void ContrastAndSaturationClamp()
        {
            var profile = NewProfile();
            SetFloat(profile, "colorGradingContrast", 0.05f);
            Assert.AreEqual(PostParameterMapper.ContrastMin, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).Contrast);
            SetFloat(profile, "colorGradingContrast", 5f);
            Assert.AreEqual(PostParameterMapper.ContrastMax, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).Contrast);
            SetFloat(profile, "colorGradingSaturation", 42f);
            Assert.AreEqual(PostParameterMapper.SaturationMax, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).Saturation);
            SetFloat(profile, "colorGradingSaturation", -1f);
            Assert.AreEqual(0f, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).Saturation);
        }

        // ---- quality tier effect sets ----

        [Test]
        public void LowTierOmitsChromaticAberrationAndTonemapping()
        {
            var profile = NewProfile();
            var low = PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.Low);
            Assert.AreEqual(0f, low.ChromaticAberration, "Low must have no CA");
            Assert.AreEqual(0f, low.TonemapSwitch);
            Assert.AreEqual(1f, low.Exposure, "color grading stays gated out on Low");
            Assert.AreEqual(1f, low.Contrast);
            Assert.AreEqual(1f, low.Saturation);
            Assert.Greater(low.VignetteIntensity, 0f);
            Assert.Greater(low.GrainIntensity, 0f);
            Assert.IsTrue(low.AnyEffectActive);
        }

        [Test]
        public void MediumTierAddsChromaticAberrationAndTonemapping()
        {
            var med = PostParameterMapper.Map(NewProfile(), VEVE.Realism.QualityLevel.Medium);
            Assert.AreEqual(0.5f, med.ChromaticAberration);
            Assert.AreEqual(1f, med.TonemapSwitch, "default ACES -> switch 1");
            Assert.AreEqual(1f, med.Exposure, "grading still gated on Medium");
            Assert.AreEqual(1f, med.Contrast);
        }

        [Test]
        public void HighAndUltraUnlocksColorGrading()
        {
            var profile = NewProfile();
            SetFloat(profile, "colorGradingContrast", 1.4f);
            Assert.AreEqual(1.4f, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High).Contrast);
            SetFloat(profile, "colorGradingSaturation", 1.5f);
            Assert.AreEqual(1.5f, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.Ultra).Saturation);
        }

        [Test]
        public void DisabledFeatureFlagsSuppressEffects()
        {
            var profile = NewProfile();
            SetBool(profile, "vignetteEnabled", false);
            SetBool(profile, "filmGrainEnabled", false);
            SetBool(profile, "chromaticAberrationEnabled", false);
            SetBool(profile, "colorGradingEnabled", false);
            SetBool(profile, "bloomEnabled", false);
            SetEnum(profile, "tonemapping", 1);
            var p = PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.Ultra);
            Assert.AreEqual(0f, p.VignetteIntensity);
            Assert.AreEqual(0f, p.GrainIntensity);
            Assert.AreEqual(0f, p.ChromaticAberration);
            Assert.AreEqual(0f, p.TonemapSwitch, "Neutral mode maps to no tonemapping");
            Assert.AreEqual(0f, p.LensDirtStrength, "bloom off suppresses lens dirt");
            Assert.AreEqual(1f, p.Exposure);
            Assert.AreEqual(1f, p.Contrast);
            Assert.AreEqual(1f, p.Saturation);
            Assert.IsFalse(p.AnyEffectActive);
        }

        [Test]
        public void NullProfileMapsToNeutralParams()
        {
            var p = PostParameterMapper.Map(null, VEVE.Realism.QualityLevel.Low);
            Assert.AreEqual(1f, p.Exposure);
            Assert.AreEqual(1f, p.Contrast);
            Assert.AreEqual(1f, p.Saturation);
            Assert.AreEqual(0f, p.VignetteIntensity);
            Assert.AreEqual(0f, p.GrainIntensity);
            Assert.AreEqual(0f, p.ChromaticAberration);
            Assert.AreEqual(0f, p.TonemapSwitch);
            Assert.IsFalse(p.AnyEffectActive);
        }

        [Test]
        public void RealismOverridesSteerGrainIntensity()
        {
            var profile = NewProfile();
            SetFloat(profile, "realismFilmGrainIntensity", 0.7f);
            Assert.AreEqual(0.7f, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.Low).GrainIntensity, 1e-4f);
            SetBool(profile, "applyRealismOverrides", false);
            SetFloat(profile, "filmGrainIntensity", 0.9f);
            Assert.AreEqual(0.9f, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.Low).GrainIntensity, 1e-4f);
        }

        [Test]
        public void TonemapSwitchFollowsProfileMode()
        {
            Assert.AreEqual(1f, PostParameterMapper.TonemapSwitchFor(TonemappingMode.ACES));
            Assert.AreEqual(2f, PostParameterMapper.TonemapSwitchFor(TonemappingMode.Reinhard));
            Assert.AreEqual(0f, PostParameterMapper.TonemapSwitchFor(TonemappingMode.Neutral));
            Assert.AreEqual(0f, PostParameterMapper.TonemapSwitchFor(TonemappingMode.HDR));

            var profile = NewProfile();
            SetEnum(profile, "tonemapping", 3);
            Assert.AreEqual(2f, PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.Medium).TonemapSwitch);
        }

        // ---- pure quality rules ----

        [Test]
        public void QualityRuleLadderIsPureAndFailsClosed()
        {
            Assert.IsTrue(PostQualityRules.Allows(VEVE.Realism.QualityLevel.Low, PostQualityRules.PostEffect.Vignette));
            Assert.IsTrue(PostQualityRules.Allows(VEVE.Realism.QualityLevel.Low, PostQualityRules.PostEffect.FilmGrain));
            Assert.IsFalse(PostQualityRules.Allows(VEVE.Realism.QualityLevel.Low, PostQualityRules.PostEffect.ChromaticAberration), "Low has no CA");
            Assert.IsFalse(PostQualityRules.Allows(VEVE.Realism.QualityLevel.Low, PostQualityRules.PostEffect.Tonemapping));
            Assert.IsFalse(PostQualityRules.Allows(VEVE.Realism.QualityLevel.Medium, PostQualityRules.PostEffect.ColorGrading));
            Assert.IsTrue(PostQualityRules.Allows(VEVE.Realism.QualityLevel.Medium, PostQualityRules.PostEffect.ChromaticAberration));
            Assert.IsTrue(PostQualityRules.Allows(VEVE.Realism.QualityLevel.Ultra, PostQualityRules.PostEffect.LensDirt));
            Assert.IsFalse(PostQualityRules.Allows(VEVE.Realism.QualityLevel.High, (PostQualityRules.PostEffect)999), "unknown effect fails closed");
        }

        [Test]
        public void PipelineFamilyRuleDisablesScriptedPipelinesOnly()
        {
            Assert.IsTrue(PostQualityRules.ControllerDisabledForFamily("Universal"));
            Assert.IsTrue(PostQualityRules.ControllerDisabledForFamily("HDRP"));
            Assert.IsFalse(PostQualityRules.ControllerDisabledForFamily("Built-in"));
            Assert.IsFalse(PostQualityRules.ControllerDisabledForFamily("CustomSRP"));
            Assert.IsFalse(PostQualityRules.ControllerDisabledForFamily("FutureSRP"));

            string live = PipelineCompat.ActivePipelineFamily();
            Assert.AreEqual(PipelineCompat.IsUniversal || PipelineCompat.IsHdrp,
                PostQualityRules.ControllerDisabledForFamily(live));
        }

        // ---- shader + controller lifecycle ----

        [Test]
        public void ShaderResolvesWithDocumentedProperties()
        {
            Shader shader = Shader.Find(TacticalPostController.ShaderName);
            if (shader == null)
            {
                Assert.Ignore("Shader.Find returned null in this headless batchmode session; shader presence assertion skipped.");
                return;
            }
            Assert.AreEqual("VEVE/TacticalPost", TacticalPostController.ShaderName);
            var m = new Material(shader);
            tracked.Add(m);
            Assert.IsTrue(m.HasProperty("_MainTex"));
            Assert.IsTrue(m.HasProperty("_VignetteIntensity"));
            Assert.IsTrue(m.HasProperty("_VignetteSmoothness"));
            Assert.IsTrue(m.HasProperty("_GrainIntensity"));
            Assert.IsTrue(m.HasProperty("_ChromaticAberration"));
            Assert.IsTrue(m.HasProperty("_Exposure"));
            Assert.IsTrue(m.HasProperty("_Contrast"));
            Assert.IsTrue(m.HasProperty("_Saturation"));
            Assert.IsTrue(m.HasProperty("_TonemapSwitch"));
            Assert.IsTrue(m.HasProperty("_LensDirtStrength"));
        }

        [Test]
        public void MissingShaderSelfDisablesWithoutThrowing()
        {
            TacticalPostController.ForceMissingShaderForTests = true;
            holder = new GameObject("TacticalPostMissingShader");
            holder.SetActive(false);
            holder.AddComponent<Camera>();
            var controller = holder.AddComponent<TacticalPostController>();
            holder.SetActive(true);
            LogAssert.Expect(LogType.Warning, new Regex("\\[TacticalPost\\]"));
            Assert.DoesNotThrow(controller.RevalidateBinding);
            Assert.IsFalse(controller.Enabled, "controller must self-disable when the shader is missing");
            Assert.IsFalse(controller.enabled);
            Assert.IsNull(controller.EffectMaterial);
            Assert.IsFalse(controller.IsOperational);
        }

        [Test]
        public void ControllerBindsMaterialAndWritesMappedParamsOnBuiltIn()
        {
            Shader shader = Shader.Find(TacticalPostController.ShaderName);
            if (shader == null)
            {
                Assert.Ignore("headless batchmode: shader not resolvable, controller binding test skipped.");
                return;
            }
            if (PipelineCompat.CustomPipelineActive)
            {
                Assert.Ignore("a scripted pipeline is active in the test realm; built-in binding test skipped.");
                return;
            }

            holder = new GameObject("TacticalPostOperational");
            holder.SetActive(false);
            holder.AddComponent<Camera>();
            var controller = holder.AddComponent<TacticalPostController>();
            holder.SetActive(true);
            Assert.DoesNotThrow(controller.RevalidateBinding);
            Assert.IsTrue(controller.IsOperational, "controller must hold a live material on the built-in pipeline");

            var profile = NewProfile();
            controller.SetProfile(profile);
            Assert.AreEqual(profile, controller.CurrentProfile);

            var prms = PostParameterMapper.Map(profile, VEVE.Realism.QualityLevel.High);
            controller.ApplyToMaterial(controller.EffectMaterial, prms);
            Assert.AreEqual(prms.Exposure, controller.EffectMaterial.GetFloat("_Exposure"));
            Assert.AreEqual(prms.Contrast, controller.EffectMaterial.GetFloat("_Contrast"));
            Assert.AreEqual(prms.Saturation, controller.EffectMaterial.GetFloat("_Saturation"));
            Assert.AreEqual(prms.VignetteIntensity, controller.EffectMaterial.GetFloat("_VignetteIntensity"));
            Assert.AreEqual(prms.VignetteSmoothness, controller.EffectMaterial.GetFloat("_VignetteSmoothness"));
            Assert.AreEqual(prms.GrainIntensity, controller.EffectMaterial.GetFloat("_GrainIntensity"));
            Assert.AreEqual(prms.ChromaticAberration, controller.EffectMaterial.GetFloat("_ChromaticAberration"));
            Assert.AreEqual(prms.TonemapSwitch, controller.EffectMaterial.GetFloat("_TonemapSwitch"));
        }

        [Test]
        public void SetProfileToleratesNullRoundTrip()
        {
            var controller = new GameObject("TacticalPostSetProfile").AddComponent<TacticalPostController>();
            tracked.Add(controller.gameObject);
            controller.SetProfile(null);
            Assert.IsNull(controller.CurrentProfile);
            var profile = NewProfile();
            controller.SetProfile(profile);
            Assert.AreEqual(profile, controller.CurrentProfile);
        }
    }
}
