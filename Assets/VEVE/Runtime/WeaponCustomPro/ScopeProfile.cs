using System;
using System.Collections.Generic;
using UnityEngine;
using VEVE.Catalog;

namespace VEVE.WeaponCustomPro
{
    /// <summary>
    /// Angular unit used by a reticle's graduated marks.
    /// </summary>
    public enum ReticleSubtensionUnit
    {
        /// <summary>No graduated reticle (red dot / holographic).</summary>
        None = 0,
        /// <summary>Minute of angle marks (classic US glass).</summary>
        Moe,
        /// <summary>Miliradian marks (0.1 MRAD typical on modern tactical glass).</summary>
        Mrad
    }

    /// <summary>
    /// Where the reticle sits relative to the erector group; determines whether the
    /// subtension stays true when the magnification ring is turned.
    /// </summary>
    public enum ReticleFocalPlane
    {
        /// <summary>Non-magnified emitter (reflector / holographic sight).</summary>
        RedDot,
        /// <summary>Reticle in the second focal plane: subtension valid only at one zoom stop.</summary>
        SecondFocalPlane,
        /// <summary>Reticle in the first focal plane: subtension true at every zoom.</summary>
        FirstFocalPlane
    }

    /// <summary>
    /// Plain, serializable optic record populated from manufacturer published data
    /// (Aimpoint, EOTech, Trijicon, Vortex, Schmidt &amp; Bender, Nightforce, Leupold, Kahles).
    /// Values are best-effort published specifications in metric units:
    /// dimensions mm, angles degrees, mass grams, ranges metres.
    ///
    /// The class intentionally owns NO gameplay behaviour: handling multipliers, eye-box
    /// evaluation and parallax error live in <see cref="ScopeOpticsModel"/>.
    /// </summary>
    [Serializable]
    public sealed class ScopeProfile
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public string manufacturer;
        public string reticleName;

        [Header("Magnification")]
        public float magnificationMin = 1f;
        public float magnificationMax = 1f;

        [Header("Optics geometry")]
        /// <summary>Front (objective) lens clear aperture in millimetres. 30 mm LPVOs list ~24 mm glass.</summary>
        public float objectiveDiameterMm;
        /// <summary>Main tube diameter mm; 0 for one-piece housings without a round tube (ACOG / EOTech).</summary>
        public float tubeDiameterMm;
        /// <summary>Exit-pupil distance from ocular mm. 0 encodes "unlimited" (red dots).</summary>
        public float eyeReliefMm;
        /// <summary>Published true (object-space) field of view at the lowest useful zoom, degrees.</summary>
        public float fovDegAtMinZoom;
        /// <summary>Published true field of view at maximum magnification, degrees.</summary>
        public float fovDegAtMaxZoom;
        /// <summary>
        /// Nominal reference focal length of the erector group at 1x-equivalent geometry, mm.
        /// Used only by the documented field-stop FOV model below; derived from the published
        /// 36 mm reference frame and the low-zoom FOV so computed values land on spec.
        /// </summary>
        public float referenceFocalLengthMm = 150f;

        [Header("Reticle / adjustments")]
        public ReticleSubtensionUnit reticleUnit = ReticleSubtensionUnit.None;
        /// <summary>Dot size (MOA) or mark step (MRAD) of the graduated pattern.</summary>
        public float reticleSubtension;
        /// <summary>Elevation click value in MOA-equivalents; 0 when the sight has no clicks.</summary>
        public float elevationClickMoa;
        /// <summary>Windage click value in MOA-equivalents; 0 when the sight has no clicks.</summary>
        public float windageClickMoa;
        /// <summary>Total mechanical adjustment travel in the elevation axis, MOA-equivalents.</summary>
        public float elevationTravelMoa = 160f;
        /// <summary>Reticle subtension validity across the zoom ring.</summary>
        public ReticleFocalPlane focalPlane = ReticleFocalPlane.RedDot;
        /// <summary>Illuminated (LED/fibre) reticle rather than etched only.</summary>
        public bool illuminatedReticle = true;

        [Header("Parallax")]
        /// <summary>
        /// Nearest range the focus can be dialled to, metres. 0 = fixed-focus emitter
        /// (red dots / holographics are paraxial by design from roughly 50 m out).
        /// </summary>
        public float parallaxCorrectionMinRangeM;
        /// <summary>Residual parallax the manufacturer allows, MOA, when the target sits beyond the paraxial band.</summary>
        public float nominalResidualParallaxMoa = 1f;

        [Header("Mass / mounting")]
        public float weightGrams;
        public float lengthMm;
        /// <summary>
        /// Nominal optic centreline height above the bore axis, mm, on the manufacturer's
        /// reference mount for a Picatinny platform (absolute/low co-witness guidance, not a measurement).
        /// </summary>
        public float boreToOpticCenterlineMm = 38f;
        /// <summary>Interface the sight ring / base requires from <see cref="AttachmentCompatibilityMatrix"/>.</summary>
        public RailInterface requiredRail = RailInterface.Picatinny;

        /// <summary>Sight height in metres, the unit the zeroing solver consumes.</summary>
        public float SightHeightM => boreToOpticCenterlineMm * 0.001f;

        /// <summary>True when the sight has more than one zoom stop.</summary>
        public bool IsZoom => magnificationMax > magnificationMin + 1e-4f;

        /// <summary>Clamps a requested zoom onto the sight's useful range.</summary>
        public double ClampedZoom(double zoom)
        {
            double lo = Math.Max(0.5, magnificationMin);
            double hi = Math.Max(lo, magnificationMax);
            return zoom < lo ? lo : (zoom > hi ? hi : zoom);
        }

        /// <summary>
        /// Object-space (true) field of view, degrees, at a given zoom. The linear field of a
        /// fixed-field-stop scope is nearly inversely proportional to magnification, so the
        /// published end-points are interpolated in 1/zoom, which keeps the curve monotonic.
        /// </summary>
        public double PictureFovDegrees(double zoom)
        {
            double z = ClampedZoom(zoom);
            if (magnificationMax <= magnificationMin + 1e-4f) return fovDegAtMinZoom;
            double t = (1.0 / z - 1.0 / magnificationMin) / (1.0 / magnificationMax - 1.0 / magnificationMin);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            double fovRad = DegreesToRadians(fovDegAtMinZoom + t * (fovDegAtMaxZoom - fovDegAtMinZoom));
            return RadiansToDegrees(fovRad);
        }

        /// <summary>Picture width in metres measured at the 100 m target plane.</summary>
        public double PictureFovLinearMetersAt100m(double zoom)
        {
            double halfRad = DegreesToRadians(PictureFovDegrees(zoom)) * 0.5;
            return 200.0 * Math.Tan(halfRad);
        }

        private static double DegreesToRadians(double deg) => deg * (Math.PI / 180.0);
        private static double RadiansToDegrees(double rad) => rad * (180.0 / Math.PI);

        /// <summary>
        /// Documented reference model from the design brief:
        /// FOV_m = 2*atan(36mm/(2*f_mm)) * 1000 — the linear field in metres at 1000 m for a
        /// 36 mm reference field stop (classic 24x36 frame width) seen through focal length f.
        /// The *1000 scaling converts the (small-angle) radians subtended at the stop into
        /// metres-of-field per kilometre of range.
        /// </summary>
        public static double FovLinearMetersPerKilometer(double focalLengthMm, double fieldStopMm = 36.0)
        {
            if (focalLengthMm <= 0.0 || fieldStopMm <= 0.0) return 0.0;
            return 2.0 * Math.Atan(fieldStopMm / (2.0 * focalLengthMm)) * 1000.0;
        }
    }

    /// <summary>
    /// Static registry exposing the optic data table to the customization UI, the zeroing
    /// solver and the ballistics HUD. Mirrors <see cref="IconicWeaponCatalog"/> conventions:
    /// ordinal-ignore-case lookup, no asset loading, safe for EditMode tests.
    /// </summary>
    public static class ScopeCatalog
    {
        private static readonly ScopeProfile[] profiles = BuildProfiles();
        private static readonly Dictionary<string, ScopeProfile> byId = BuildIndex(profiles);

        /// <summary>Every optic, in declaration order.</summary>
        public static IReadOnlyList<ScopeProfile> All => profiles;

        public static int Count => profiles.Length;

        public static bool TryGet(string id, out ScopeProfile profile)
        {
            profile = null;
            return id != null && byId.TryGetValue(id, out profile);
        }

        public static ScopeProfile Get(string id)
        {
            if (TryGet(id, out ScopeProfile p)) return p;
            throw new KeyNotFoundException($"No scope profile with id '{id}'.");
        }

        private static Dictionary<string, ScopeProfile> BuildIndex(ScopeProfile[] items)
        {
            var d = new Dictionary<string, ScopeProfile>(StringComparer.OrdinalIgnoreCase);
            foreach (ScopeProfile p in items) d[p.id] = p;
            return d;
        }

        private static ScopeProfile[] BuildProfiles()
        {
            return new[]
            {
                new ScopeProfile
                {
                    id = "aimpoint-micro-t2", displayName = "Aimpoint Micro T-2", manufacturer = "Aimpoint AB",
                    reticleName = "2 MOA dot (red)", magnificationMin = 1f, magnificationMax = 1f,
                    objectiveDiameterMm = 21f, tubeDiameterMm = 25f, eyeReliefMm = 0f,
                    fovDegAtMinZoom = 20f, fovDegAtMaxZoom = 20f, referenceFocalLengthMm = 360f,
                    reticleUnit = ReticleSubtensionUnit.Moe, reticleSubtension = 2f,
                    elevationClickMoa = 1f, windageClickMoa = 1f, elevationTravelMoa = 80f,
                    focalPlane = ReticleFocalPlane.RedDot, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 0f, nominalResidualParallaxMoa = 0.7f,
                    weightGrams = 75f, lengthMm = 55f, boreToOpticCenterlineMm = 38.1f,
                    requiredRail = RailInterface.Proprietary,
                },
                new ScopeProfile
                {
                    id = "eotech-exps3", displayName = "EOTech EXPS3", manufacturer = "L3 / EOTech",
                    reticleName = "68 MOA ring + 1 MOA dot (holographic)", magnificationMin = 1f, magnificationMax = 1f,
                    objectiveDiameterMm = 70f, tubeDiameterMm = 0f, eyeReliefMm = 0f,
                    fovDegAtMinZoom = 34f, fovDegAtMaxZoom = 34f, referenceFocalLengthMm = 360f,
                    reticleUnit = ReticleSubtensionUnit.Moe, reticleSubtension = 1f,
                    elevationClickMoa = 0.5f, windageClickMoa = 0.5f, elevationTravelMoa = 40f,
                    focalPlane = ReticleFocalPlane.RedDot, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 0f, nominalResidualParallaxMoa = 1f,
                    weightGrams = 315f, lengthMm = 94f, boreToOpticCenterlineMm = 57f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "trijicon-mro", displayName = "Trijicon MRO", manufacturer = "Trijicon",
                    reticleName = "2 MOA dot + 30 MOA ring (tritium/fibre)", magnificationMin = 1f, magnificationMax = 1f,
                    objectiveDiameterMm = 32f, tubeDiameterMm = 25f, eyeReliefMm = 0f,
                    fovDegAtMinZoom = 24f, fovDegAtMaxZoom = 24f, referenceFocalLengthMm = 360f,
                    reticleUnit = ReticleSubtensionUnit.Moe, reticleSubtension = 2f,
                    elevationClickMoa = 1f, windageClickMoa = 1f, elevationTravelMoa = 60f,
                    focalPlane = ReticleFocalPlane.RedDot, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 0f, nominalResidualParallaxMoa = 1f,
                    weightGrams = 25f, lengthMm = 65f, boreToOpticCenterlineMm = 40f,
                    requiredRail = RailInterface.Proprietary,
                },
                new ScopeProfile
                {
                    id = "aimpoint-pro", displayName = "Aimpoint PRO", manufacturer = "Aimpoint AB",
                    reticleName = "2 / 4 / 6 MOA dot (red)", magnificationMin = 1f, magnificationMax = 1f,
                    objectiveDiameterMm = 26f, tubeDiameterMm = 30f, eyeReliefMm = 0f,
                    fovDegAtMinZoom = 19f, fovDegAtMaxZoom = 19f, referenceFocalLengthMm = 360f,
                    reticleUnit = ReticleSubtensionUnit.Moe, reticleSubtension = 2f,
                    elevationClickMoa = 1f, windageClickMoa = 1f, elevationTravelMoa = 150f,
                    focalPlane = ReticleFocalPlane.RedDot, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 0f, nominalResidualParallaxMoa = 1f,
                    weightGrams = 150f, lengthMm = 119f, boreToOpticCenterlineMm = 38.4f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "trijicon-ta31", displayName = "Trijicon ACOG TA31 4x32 (RCO)", manufacturer = "Trijicon",
                    reticleName = "BAC crosshair, MOA hold ladder", magnificationMin = 4f, magnificationMax = 4f,
                    objectiveDiameterMm = 32f, tubeDiameterMm = 0f, eyeReliefMm = 38f,
                    fovDegAtMinZoom = 7.3f, fovDegAtMaxZoom = 7.3f, referenceFocalLengthMm = 282f,
                    reticleUnit = ReticleSubtensionUnit.Moe, reticleSubtension = 1f,
                    elevationClickMoa = 0.5f, windageClickMoa = 0.5f, elevationTravelMoa = 60f,
                    focalPlane = ReticleFocalPlane.SecondFocalPlane, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 0f, nominalResidualParallaxMoa = 1.5f,
                    weightGrams = 380f, lengthMm = 142f, boreToOpticCenterlineMm = 38f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "trijicon-ta648", displayName = "Trijicon ACOG TA648 6x48", manufacturer = "Trijicon",
                    reticleName = "Trijicon 6x48 crosshair", magnificationMin = 6f, magnificationMax = 6f,
                    objectiveDiameterMm = 48f, tubeDiameterMm = 0f, eyeReliefMm = 39f,
                    fovDegAtMinZoom = 3.9f, fovDegAtMaxZoom = 3.9f, referenceFocalLengthMm = 530f,
                    reticleUnit = ReticleSubtensionUnit.Moe, reticleSubtension = 1f,
                    elevationClickMoa = 0.5f, windageClickMoa = 0.5f, elevationTravelMoa = 60f,
                    focalPlane = ReticleFocalPlane.SecondFocalPlane, illuminatedReticle = false,
                    parallaxCorrectionMinRangeM = 0f, nominalResidualParallaxMoa = 1.5f,
                    weightGrams = 624f, lengthMm = 252f, boreToOpticCenterlineMm = 40f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "vortex-razor-gen2e-1-10x24", displayName = "Vortex Razor HD Gen II-E 1-10x24",
                    manufacturer = "Vortex Optics",
                    reticleName = "EBR-7C MRAD illuminated (FFP)", magnificationMin = 1f, magnificationMax = 10f,
                    objectiveDiameterMm = 24f, tubeDiameterMm = 30f, eyeReliefMm = 95f,
                    fovDegAtMinZoom = 19f, fovDegAtMaxZoom = 3.4f, referenceFocalLengthMm = 360f,
                    reticleUnit = ReticleSubtensionUnit.Mrad, reticleSubtension = 0.1f,
                    elevationClickMoa = 0.35f, windageClickMoa = 0.35f, elevationTravelMoa = 140f,
                    focalPlane = ReticleFocalPlane.FirstFocalPlane, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 23f, nominalResidualParallaxMoa = 0.5f,
                    weightGrams = 622f, lengthMm = 267f, boreToOpticCenterlineMm = 40f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "sb-pm2-5-25x56", displayName = "Schmidt & Bender PM II 5-25x56",
                    manufacturer = "Schmidt & Bender",
                    reticleName = "P4L MRAD illuminated (FFP)", magnificationMin = 5f, magnificationMax = 25f,
                    objectiveDiameterMm = 56f, tubeDiameterMm = 34f, eyeReliefMm = 96f,
                    fovDegAtMinZoom = 11.6f, fovDegAtMaxZoom = 2.4f, referenceFocalLengthMm = 300f,
                    reticleUnit = ReticleSubtensionUnit.Mrad, reticleSubtension = 0.1f,
                    elevationClickMoa = 0.35f, windageClickMoa = 0.35f, elevationTravelMoa = 210f,
                    focalPlane = ReticleFocalPlane.FirstFocalPlane, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 25f, nominalResidualParallaxMoa = 0.25f,
                    weightGrams = 820f, lengthMm = 385f, boreToOpticCenterlineMm = 40f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "nightforce-atacr-7-35x56", displayName = "Nightforce ATACR 7-35x56",
                    manufacturer = "Nightforce Optics",
                    reticleName = "MOAR MRAD illuminated (FFP)", magnificationMin = 7f, magnificationMax = 35f,
                    objectiveDiameterMm = 56f, tubeDiameterMm = 34f, eyeReliefMm = 89f,
                    fovDegAtMinZoom = 4.6f, fovDegAtMaxZoom = 0.95f, referenceFocalLengthMm = 420f,
                    reticleUnit = ReticleSubtensionUnit.Mrad, reticleSubtension = 0.1f,
                    elevationClickMoa = 0.35f, windageClickMoa = 0.35f, elevationTravelMoa = 175f,
                    focalPlane = ReticleFocalPlane.FirstFocalPlane, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 15f, nominalResidualParallaxMoa = 0.25f,
                    weightGrams = 1085f, lengthMm = 401f, boreToOpticCenterlineMm = 40f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "leupold-mark5hd-5-25x56", displayName = "Leupold Mark 5HD 5-25x56",
                    manufacturer = "Leupold & Stevens",
                    reticleName = "TMR MRAD illuminated (FFP)", magnificationMin = 5f, magnificationMax = 25f,
                    objectiveDiameterMm = 56f, tubeDiameterMm = 34f, eyeReliefMm = 94f,
                    fovDegAtMinZoom = 12.3f, fovDegAtMaxZoom = 2.5f, referenceFocalLengthMm = 300f,
                    reticleUnit = ReticleSubtensionUnit.Mrad, reticleSubtension = 0.1f,
                    elevationClickMoa = 0.35f, windageClickMoa = 0.35f, elevationTravelMoa = 170f,
                    focalPlane = ReticleFocalPlane.FirstFocalPlane, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 23f, nominalResidualParallaxMoa = 0.25f,
                    weightGrams = 850f, lengthMm = 366f, boreToOpticCenterlineMm = 40f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "trijicon-hurst-3-9x40", displayName = "Trijicon Hurst 3-9x40", manufacturer = "Trijicon",
                    reticleName = "Hurst duplex (etched)", magnificationMin = 3f, magnificationMax = 9f,
                    objectiveDiameterMm = 40f, tubeDiameterMm = 25.4f, eyeReliefMm = 86f,
                    fovDegAtMinZoom = 12.1f, fovDegAtMaxZoom = 4.3f, referenceFocalLengthMm = 180f,
                    reticleUnit = ReticleSubtensionUnit.Moe, reticleSubtension = 0.25f,
                    elevationClickMoa = 0.25f, windageClickMoa = 0.25f, elevationTravelMoa = 90f,
                    focalPlane = ReticleFocalPlane.SecondFocalPlane, illuminatedReticle = false,
                    parallaxCorrectionMinRangeM = 0f, nominalResidualParallaxMoa = 1f,
                    weightGrams = 390f, lengthMm = 305f, boreToOpticCenterlineMm = 42f,
                    requiredRail = RailInterface.Picatinny,
                },
                new ScopeProfile
                {
                    id = "kahles-k525i-5-25x56", displayName = "Kahles K525i 5-25x56", manufacturer = "Kahles",
                    reticleName = "K525i MRAD illuminated (FFP)", magnificationMin = 5f, magnificationMax = 25f,
                    objectiveDiameterMm = 56f, tubeDiameterMm = 34f, eyeReliefMm = 95f,
                    fovDegAtMinZoom = 11.7f, fovDegAtMaxZoom = 2.4f, referenceFocalLengthMm = 300f,
                    reticleUnit = ReticleSubtensionUnit.Mrad, reticleSubtension = 0.1f,
                    elevationClickMoa = 0.35f, windageClickMoa = 0.35f, elevationTravelMoa = 160f,
                    focalPlane = ReticleFocalPlane.FirstFocalPlane, illuminatedReticle = true,
                    parallaxCorrectionMinRangeM = 10f, nominalResidualParallaxMoa = 0.25f,
                    weightGrams = 771f, lengthMm = 378f, boreToOpticCenterlineMm = 40f,
                    requiredRail = RailInterface.Picatinny,
                },
            };
        }
    }
}
