using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using VEVE.Realism;

namespace VEVE.Graphics
{
    [CreateAssetMenu(menuName = "VEVE/Graphics/Post-Process Profile")]
    public sealed class PostProcessProfile : ScriptableObject
    {
        [Header("Motion Blur")]
        [SerializeField] private bool motionBlurEnabled = true;
        [SerializeField] private float motionBlurIntensity = 0.5f;
        [SerializeField] private float motionBlurMaxVelocity = 10f;

        [Header("Depth of Field")]
        [SerializeField] private bool dofEnabled = true;
        [SerializeField] private float dofNearFocus = 10f;
        [SerializeField] private float dofFarFocus = 50f;
        [SerializeField] private float dofNearBlur = 5f;
        [SerializeField] private float dofFarBlur = 10f;

        [Header("Bloom")]
        [SerializeField] private bool bloomEnabled = true;
        [SerializeField] private float bloomThreshold = 1.0f;
        [SerializeField] private float bloomIntensity = 0.5f;
        [SerializeField] private float bloomRadius = 4f;

        [Header("Lens Flare")]
        [SerializeField] private bool lensFlareEnabled = true;
        [SerializeField] private float lensFlareIntensity = 0.5f;

        [Header("Chromatic Aberration")]
        [SerializeField] private bool chromaticAberrationEnabled = true;
        [SerializeField] private float chromaticAberrationIntensity = 0.5f;

        [Header("Film Grain")]
        [SerializeField] private bool filmGrainEnabled = true;
        [SerializeField] private float filmGrainIntensity = 0.05f;

        [Header("Vignette")]
        [SerializeField] private bool vignetteEnabled = true;
        [SerializeField] private float vignetteIntensity = 0.3f;
        [SerializeField] private float vignetteSmoothness = 1.0f;

        [Header("Color Grading")]
        [SerializeField] private bool colorGradingEnabled = true;
        [SerializeField] private float colorGradingExposure = 1.0f;
        [SerializeField] private float colorGradingContrast = 1.0f;
        [SerializeField] private float colorGradingSaturation = 1.0f;

        [Header("Tonemapping")]
        [SerializeField] private TonemappingMode tonemapping = TonemappingMode.ACES;

        [Header("Realism Overrides")]
        [SerializeField] private bool applyRealismOverrides = true;
        [SerializeField] private float realismMotionBlurIntensity = 0.3f;
        [SerializeField] private float realismBloomIntensity = 0.4f;
        [SerializeField] private float realismFilmGrainIntensity = 0.03f;

        public bool MotionBlurEnabled => motionBlurEnabled;
        public float MotionBlurIntensity => applyRealismOverrides ? realismMotionBlurIntensity : motionBlurIntensity;
        public float MotionBlurMaxVelocity => motionBlurMaxVelocity;
        public bool DofEnabled => dofEnabled;
        public float DofNearFocus => dofNearFocus;
        public float DofFarFocus => dofFarFocus;
        public float DofNearBlur => dofNearBlur;
        public float DofFarBlur => dofFarBlur;
        public bool BloomEnabled => bloomEnabled;
        public float BloomThreshold => bloomThreshold;
        public float BloomIntensity => applyRealismOverrides ? realismBloomIntensity : bloomIntensity;
        public float BloomRadius => bloomRadius;
        public bool LensFlareEnabled => lensFlareEnabled;
        public float LensFlareIntensity => lensFlareIntensity;
        public bool ChromaticAberrationEnabled => chromaticAberrationEnabled;
        public float ChromaticAberrationIntensity => chromaticAberrationIntensity;
        public bool FilmGrainEnabled => filmGrainEnabled;
        public float FilmGrainIntensity => applyRealismOverrides ? realismFilmGrainIntensity : filmGrainIntensity;
        public bool VignetteEnabled => vignetteEnabled;
        public float VignetteIntensity => vignetteIntensity;
        public float VignetteSmoothness => vignetteSmoothness;
        public bool ColorGradingEnabled => colorGradingEnabled;
        public float ColorGradingExposure => colorGradingExposure;
        public float ColorGradingContrast => colorGradingContrast;
        public float ColorGradingSaturation => colorGradingSaturation;
        public TonemappingMode Tonemapping => tonemapping;
    }

    public enum TonemappingMode { ACES, Neutral, HDR, Reinhard }
}
