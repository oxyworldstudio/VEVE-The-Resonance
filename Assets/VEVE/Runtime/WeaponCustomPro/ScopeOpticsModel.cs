using System;
using UnityEngine;

namespace VEVE.WeaponCustomPro
{
    /// <summary>
    /// Pure static optics evaluation for <see cref="ScopeProfile"/> data. Nothing here touches
    /// GameObjects: every function maps optic parameters to derived quantities (picture field,
    /// exit pupil, cheek-weld fit, handling multipliers, parallax error) so the weapon state
    /// machine and the rail-mount system can integrate the returned multipliers later.
    /// All multipliers are monotonically clamped and documented.
    /// </summary>
    public static class ScopeOpticsModel
    {
        /// <summary>Human eye practical entrance pupil ceiling, mm (dark-adapted upper bound).</summary>
        public const float MaxUsefulEyePupilMm = 7.0f;
        /// <summary>Lower clamp of every returned handling multiplier.</summary>
        public const float MinMultiplier = 0.5f;

        /// <summary>Clamps x into [lo, hi] without relying on framework version specifics.</summary>
        public static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);

        /// <summary>
        /// Picture width (metres at 100 m) for a sight at a given zoom. Delegates to the
        /// profile's published end-points; invalid inputs degrade to zero field rather than NaN.
        /// </summary>
        public static double PictureFovLinearMeters(ScopeProfile profile, double zoom)
        {
            if (profile == null) return 0.0;
            return Math.Max(0.0, profile.PictureFovLinearMetersAt100m(zoom));
        }

        /// <summary>True angular picture, degrees, at a given zoom.</summary>
        public static double PictureFovDegrees(ScopeProfile profile, double zoom)
        {
            if (profile == null) return 0.0;
            return Math.Max(0.0, profile.PictureFovDegrees(zoom));
        }

        /// <summary>
        /// Exit-pupil diameter, mm: objective aperture divided by magnification. A red dot's
        /// "exit pupil" is simply its window aperture at 1x. Values above the human eye pupil
        /// are clipped (the surplus vignettes) so callers get a usable clear-spot size.
        /// </summary>
        public static double ExitPupilMm(ScopeProfile profile, double zoom)
        {
            if (profile == null || profile.objectiveDiameterMm <= 0f) return 0.0;
            double z = profile.ClampedZoom(zoom);
            if (z <= 0.0) return 0.0;
            double raw = profile.objectiveDiameterMm / z;
            return Clamp(raw, 0.0, MaxUsefulEyePupilMm);
        }

        /// <summary>
        /// Effective eye-box clear diameter, mm: the exit pupil as constrained by whichever
        /// aperture is smaller (objective at 1x, or the ocular for heavy-magnification glass).
        /// </summary>
        public static double EyeBoxClearDiameterMm(ScopeProfile profile, double zoom)
        {
            if (profile == null) return 0.0;
            double exit = ExitPupilMm(profile, zoom);
            double ocularCap = profile.tubeDiameterMm > 0f ? profile.tubeDiameterMm * 0.5 : double.MaxValue;
            return Clamp(Math.Min(exit, ocularCap), 0.0, MaxUsefulEyePupilMm);
        }

        /// <summary>
        /// Cheek-weld fit multiplier in [0, 1]. Compares the shooting eye's natural weld height
        /// (stock comb, measured from the bore axis) against the optic centreline height.
        /// A long eye relief tolerates more vertical/rail mismatch, so the acceptance window
        /// widens with <c>profile.eyeReliefMm</c> (zero eye relief = "unlimited" red dots get
        /// a generous tolerance band). Strictly monotonic non-increasing in |mismatch|;
        /// returns 1 on a perfect weld, 0 when the mismatch exceeds the tolerance.
        /// </summary>
        public static double CheekWeldFitMultiplier(ScopeProfile profile, double combHeightAboveBoreMm)
        {
            if (profile == null) return 0.0;
            double mismatch = Math.Abs(combHeightAboveBoreMm - profile.boreToOpticCenterlineMm);
            // eyeReliefMm == 0 is the catalogue encoding for "unlimited" (red dots / holographics):
            // those give the widest head-position window, so they get the full allowance.
            double relief = profile.eyeReliefMm > 0f ? profile.eyeReliefMm : 180.0;
            double toleranceMm = 12.0 + 0.45 * relief;
            double fit = 1.0 - mismatch / Math.Max(1e-3, toleranceMm);
            return Clamp(fit, 0.0, 1.0);
        }

        /// <summary>
        /// Sight-mass penalty multiplier in [0.5, 1]. Feed-forward candidate for ADS
        /// movement-swing damping: heavier glass off the bore axis slows the weapon.
        /// m = 1 / (1 + 0.9 * kg) — monotone decreasing in weight, clamped at the floor.
        /// </summary>
        public static double WeightSwayPenaltyMultiplier(ScopeProfile profile)
        {
            if (profile == null) return 1.0;
            double kg = Math.Max(0.0, profile.weightGrams) * 0.001;
            return Clamp(1.0 / (1.0 + 0.9 * kg), MinMultiplier, 1.0);
        }

        /// <summary>
        /// Balance (fore-aft torque) penalty multiplier in [0.5, 1]: mass times cantilever
        /// distance from the supporting hand, τ ≈ weight * (length/2 + railKitOverhang).
        /// Returned multiplier = 1 / (1 + k*τ); monotone decreasing in both weight and overhang.
        /// </summary>
        public static double BalanceTorquePenaltyMultiplier(ScopeProfile profile, double railKitOverhangMm = 0.0)
        {
            if (profile == null) return 1.0;
            double kg = Math.Max(0.0, profile.weightGrams) * 0.001;
            double armM = Math.Max(0.0, profile.lengthMm * 0.5 + railKitOverhangMm) * 0.001;
            double torque = kg * armM;
            return Clamp(1.0 / (1.0 + 3.0 * torque), MinMultiplier, 1.0);
        }

        /// <summary>
        /// Zoom penalty multiplier in [0.5, 1] applied to target re-acquisition / sway tolerance:
        /// every stop past 1x narrows the picture and magnifies tremor
        /// (monotone decreasing in the effective magnification, clamped).
        /// </summary>
        public static double MagnificationAgilityMultiplier(ScopeProfile profile, double zoom)
        {
            if (profile == null) return 1.0;
            double z = profile.ClampedZoom(zoom);
            return Clamp(1.0 / (1.0 + 0.18 * (z - 1.0)), MinMultiplier, 1.0);
        }

        /// <summary>
        /// First-order parallax aim error, linear millimetres at the target plane, when the
        /// sight was focused/dialled for <c>rangeZeroed</c> but the target sits at
        /// <c>rangeActual</c>.
        /// <para>
        /// Small-angle thin-lens approximation: imaging a target at r puts the image plane at
        /// s'(r) = f*r/(r-f); focusing for Z while the target is at A displaces the image by
        /// δ = |s'(A) - s'(Z)|. The emergent ray bundle subtends the aperture half-angle
        /// α = Φ/(2 f) at the objective, so the reticle-vs-image mismatch angle is
        /// θ ≈ α * δ / f' with f' ≈ f, and the linear error at the target is A * θ.
        /// For Z, A ≫ f this collapses to error ≈ Φ*A*|1/Z - 1/A|/2, i.e. it saturates near
        /// the objective diameter scaled by the range ratio, which matches manufacturer
        /// parallax claims (tens of MOA at close range for a heavy objective, <1 MOA at
        /// infinity for a modern 56 mm dialled at 100 m). Returns 0 on degenerate input.
        /// </para>
        /// </summary>
        public static double ParallaxErrorMm(double rangeZeroedM, double rangeActualM, double objectivePhiMm, double focalLenMm)
        {
            if (rangeZeroedM <= 0.0 || rangeActualM <= 0.0 || objectivePhiMm <= 0.0 || focalLenMm <= 0.0) return 0.0;
            double f = focalLenMm * 0.001;
            double z = rangeZeroedM;
            double a = rangeActualM;
            if (z <= f * 1.05 || a <= f * 1.05)
            {
                // Inside the focal length the thin-lens model is meaningless; keep it bounded.
                z = Math.Max(z, f * 1.1);
                a = Math.Max(a, f * 1.1);
            }
            double imageAtZ = f * z / (z - f);
            double imageAtA = f * a / (a - f);
            double delta = Math.Abs(imageAtA - imageAtZ);
            double alpha = (objectivePhiMm * 0.001) / (2.0 * f);
            double theta = alpha * delta / imageAtZ;
            return a * theta * 1000.0;
        }
    }
}
