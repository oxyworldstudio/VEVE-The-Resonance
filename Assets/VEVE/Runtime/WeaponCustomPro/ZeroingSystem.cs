using System;
using System.Collections.Generic;
using UnityEngine;
using VEVE.Catalog;

namespace VEVE.WeaponCustomPro
{
    /// <summary>
    /// One row of a printed range card: hold and click data for a single distance.
    /// Immutable plain data so cards can be baked, compared and unit tested deterministically.
    /// </summary>
    [Serializable]
    public struct RangeCardEntry
    {
        public float distanceM;
        public float timeOfFlightS;
        public float dropMeters;
        public float retainedVelocityMs;
        public float retainedEnergyJ;
        /// <summary>Point of impact relative to the centre crosshair, metres (+ = impact high).</summary>
        public float pointOfImpactAboveSightM;
        /// <summary>Angle to hold off the zeroed centre, MOA (+ = aim above the target).</summary>
        public float holdoverMoa;
        /// <summary>Elevation turret clicks to dial from the zero for this line.</summary>
        public int holdoverClicks;

        public RangeCardEntry(float distanceM, float timeOfFlightS, float dropMeters, float retainedVelocityMs,
            float retainedEnergyJ, float pointOfImpactAboveSightM, float holdoverMoa, int holdoverClicks)
        {
            this.distanceM = distanceM;
            this.timeOfFlightS = timeOfFlightS;
            this.dropMeters = dropMeters;
            this.retainedVelocityMs = retainedVelocityMs;
            this.retainedEnergyJ = retainedEnergyJ;
            this.pointOfImpactAboveSightM = pointOfImpactAboveSightM;
            this.holdoverMoa = holdoverMoa;
            this.holdoverClicks = holdoverClicks;
        }
    }

    /// <summary>
    /// Battle-zero analysis for a catalog weapon: the solved zero angle, the sight-line
    /// crossings, the maximum trajectory crown above the line of sight and the hold table.
    /// </summary>
    public sealed class RangeCard
    {
        public string weaponId;
        public string displayName;
        public double zeroRangeM;
        public double sightHeightMm;
        public double muzzleVelocityMs;
        public double elevationAngleRad;
        public double clickValueMoa = 0.25;
        /// <summary>Ascending sight-line crossing (near-zero), metres; 0 when degenerate.</summary>
        public double firstSightLineCrossingM;
        /// <summary>Trajectory crown above the sight line between the crossings, metres.</summary>
        public double maxRiseAboveSightM;
        /// <summary>Point-blank limit distance for the configured target box.</summary>
        public double pointBlankRangeM;
        public RangeCardEntry[] entries = Array.Empty<RangeCardEntry>();

        public double ClickValueMoa => clickValueMoa;
    }

    /// <summary>
    /// Deterministic zeroing / battle-zero / range-card solver. The trajectory model reuses the
    /// shared <see cref="Ballistics"/> primitives (energy bleed-off, gravity drop) exactly like
    /// <see cref="AdvancedBallistics"/>; nothing here is scene- or component-bound.
    /// <para>
    /// Geometry: the sight line is the reference axis, parallel to the line of departure, at
    /// <c>sightHeightMm</c> above the bore origin. Zeroing elevates the bore so the descending
    /// branch of the trajectory meets the sight line at the zero range:
    /// tan(theta0) = (s0 + drop(Z)) / Z. Aim "high" therefore means adding angle above theta0.
    /// </para>
    /// </summary>
    public static class ZeroingSystem
    {
        /// <summary>Standard gravity, m/s^2 — the Ballistics default, kept explicit for tests.</summary>
        public const double StandardGravity = 9.80665;
        /// <summary>Default printed range-card distances: 100..1200 m in 100 m lines.</summary>
        public static readonly double[] DefaultCardDistancesM =
            { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200 };
        /// <summary>Deer-sized box for point-blank analysis: crown allowance 10 cm above the line, 38 cm drop budget below.</summary>
        public const double TargetMaxCrownM = 0.10;
        public const double TargetMaxDropM = 0.38;
        /// <summary>Turret accumulator clamps: mechanical travel guard, typical modern tactical drum.</summary>
        public const int MaxTurretClicksPerDirection = 150;
        /// <summary>Clicks on a graduation ring before the dial wraps to zero again.</summary>
        public const int DefaultClicksPerRevolution = 100;

        private const double VelocityFloorRatio = 0.25;

        /// <summary>
        /// Barrel-length (carry) adjustment for catalog velocity: published loads are proofed from a
        /// nominal barrel, so scale v ≈ v_nom + 0.45 m/s per added mm (≈30 m/s per 100 mm — the
        /// mid-range 5.56/7.62 powder-burn figure), clamped to [0.6, 1.25]x nominal.
        /// </summary>
        public static double AdjustMuzzleVelocityForBarrel(WeaponSpec spec, double barrelLengthMm)
        {
            if (spec.muzzleVelocity <= 0f || spec.barrelLength <= 0f) return spec.muzzleVelocity;
            double delta = (barrelLengthMm - spec.barrelLength) * 0.45;
            double ratio = Clamp((spec.muzzleVelocity + delta) / spec.muzzleVelocity, 0.6, 1.25);
            return spec.muzzleVelocity * ratio;
        }

        /// <summary>Retained velocity, m/s, from the shared linear-drag energy model with a subsonic floor.</summary>
        public static double RetainedVelocity(WeaponSpec spec, double distanceM)
        {
            double e = Ballistics.EnergyAfterDistance(spec.muzzleEnergy, (float)distanceM, spec.ballisticCoefficient);
            if (e <= 0.0) e = 0.0;
            double v = spec.bulletMass > 0.0 ? Math.Sqrt(2.0 * e / spec.bulletMass) : 0.0;
            return Math.Max(v, spec.muzzleVelocity * VelocityFloorRatio);
        }

        /// <summary>Trapezoidal time of flight between two speeds, matching AdvancedBallistics convention.</summary>
        public static double TimeOfFlight(WeaponSpec spec, double distanceM)
        {
            if (distanceM <= 0.0) return 0.0;
            double vEnd = RetainedVelocity(spec, distanceM);
            double vAvg = 0.5 * (spec.muzzleVelocity + vEnd);
            return vAvg > 0.0 ? distanceM / vAvg : double.PositiveInfinity;
        }

        /// <summary>
        /// Gravity drop, metres, through the shared <see cref="Ballistics.GravityDrop"/> primitive,
        /// fed with the average effective speed along the leg so the trapezoidal time is reproduced.
        /// </summary>
        public static double DropAt(WeaponSpec spec, double distanceM, double muzzleVelocityOverride = 0.0)
        {
            if (distanceM <= 0.0) return 0.0;
            WeaponSpec s = ApplyVelocityOverride(spec, muzzleVelocityOverride);
            double t = TimeOfFlight(s, distanceM);
            if (double.IsPositiveInfinity(t) || t <= 0.0) return 0.0;
            double vEff = distanceM / t;
            return Ballistics.GravityDrop((float)vEff, (float)distanceM, (float)StandardGravity);
        }

        /// <summary>Bore angle above the sight line that zeroes the rifle at the given range.</summary>
        public static double SolveZeroAngleRad(WeaponSpec spec, double zeroRangeM, double sightHeightMm, double muzzleVelocityOverride = 0.0)
        {
            if (zeroRangeM <= 0.0) return 0.0;
            WeaponSpec s = ApplyVelocityOverride(spec, muzzleVelocityOverride);
            double s0 = Math.Max(0.0, sightHeightMm) * 0.001;
            return Math.Atan2(s0 + DropAt(s, zeroRangeM, 0.0), zeroRangeM);
        }

        /// <summary>
        /// Point of impact relative to the sight line, metres (+ = impact high),
        /// for a rifle zeroed at <c>zeroRangeM</c>.
        /// </summary>
        public static double HeightAboveSightLine(WeaponSpec spec, double zeroRangeM, double sightHeightMm, double distanceM)
        {
            if (distanceM <= 0.0 || zeroRangeM <= 0.0) return -Math.Max(0.0, sightHeightMm) * 0.001;
            double theta = SolveZeroAngleRad(spec, zeroRangeM, sightHeightMm);
            double s0 = Math.Max(0.0, sightHeightMm) * 0.001;
            return distanceM * Math.Tan(theta) - DropAt(spec, distanceM) - s0;
        }

        /// <summary>
        /// Builds the full range card. Entries cover <c>DefaultCardDistancesM</c> unless a custom
        /// distance ladder is supplied; holdover clicks are rounded against <paramref name="clickValueMoa"/>.
        /// </summary>
        public static RangeCard ComputeCard(WeaponSpec spec, double zeroRangeM, double sightHeightMm,
            double[] distancesM = null, double clickValueMoa = 0.25, double muzzleVelocityOverride = 0.0)
        {
            WeaponSpec s = ApplyVelocityOverride(spec, muzzleVelocityOverride);
            double s0 = Math.Max(0.0, sightHeightMm) * 0.001;
            double theta0 = SolveZeroAngleRad(s, zeroRangeM, sightHeightMm, 0.0);
            double[] ladder = distancesM ?? DefaultCardDistancesM;

            var card = new RangeCard
            {
                weaponId = s.id,
                displayName = s.displayName,
                zeroRangeM = zeroRangeM,
                sightHeightMm = sightHeightMm,
                muzzleVelocityMs = s.muzzleVelocity,
                elevationAngleRad = theta0,
                clickValueMoa = clickValueMoa <= 0.0 ? 0.25 : clickValueMoa,
            };

            SolveSightLineCrossings(s, zeroRangeM, sightHeightMm, out card.firstSightLineCrossingM, out card.maxRiseAboveSightM);
            card.pointBlankRangeM = SolvePointBlankRange(s, zeroRangeM, sightHeightMm, TargetMaxDropM);

            var rows = new List<RangeCardEntry>(ladder.Length);
            foreach (double d in ladder)
            {
                if (d <= 0.0) continue;
                double drop = DropAt(s, d);
                double poi = d * Math.Tan(theta0) - drop - s0;
                double holdMoa = ComputeHoldoverMoa(s, zeroRangeM, sightHeightMm, d);
                int clicks = MoaToClicks(card.clickValueMoa, holdMoa);
                rows.Add(new RangeCardEntry((float)d, (float)TimeOfFlight(s, d), (float)drop,
                    (float)RetainedVelocity(s, d),
                    (float)(0.5 * s.bulletMass * RetainedVelocity(s, d) * RetainedVelocity(s, d)),
                    (float)poi, (float)holdMoa, clicks));
            }
            card.entries = rows.ToArray();
            return card;
        }

        /// <summary>
        /// Convenience zero from the catalog: resolves ballisticCoef/muzzleVelocity through
        /// <see cref="IconicWeaponCatalog.TryGet"/>. A positive <paramref name="barrelLengthMm"/>
        /// models a carry (short/long barrel variant).
        /// </summary>
        public static bool TryComputeCard(string weaponId, double zeroRangeM, double sightHeightMm,
            out RangeCard card, double barrelLengthMm = 0.0, double[] distancesM = null, double clickValueMoa = 0.25)
        {
            card = null;
            if (!IconicWeaponCatalog.TryGet(weaponId, out WeaponSpec spec)) return false;
            double vOverride = barrelLengthMm > 0.0 ? AdjustMuzzleVelocityForBarrel(spec, barrelLengthMm) : 0.0;
            card = ComputeCard(spec, zeroRangeM, sightHeightMm, distancesM, clickValueMoa, vOverride);
            return true;
        }

        /// <summary>
        /// Holdover relative to the zeroed centre, MOA; + means "aim above the target".
        /// Exactly 0 at the zero distance, positive inside the near crossing and beyond
        /// max-crown, negative through the crown band between the sight-line crossings.
        /// </summary>
        public static double ComputeHoldoverMoa(RangeCard card, double distanceM)
        {
            if (!IconicWeaponCatalog.TryGet(card.weaponId, out WeaponSpec spec)) return 0.0;
            double vOverride = Math.Abs(card.muzzleVelocityMs - spec.muzzleVelocity) > 1e-6 && card.muzzleVelocityMs > 0.0
                ? card.muzzleVelocityMs : 0.0;
            WeaponSpec s = ApplyVelocityOverride(spec, vOverride);
            return ComputeHoldoverMoa(s, card.zeroRangeM, card.sightHeightMm, distanceM);
        }

        /// <summary>Direct form of the holdover law for a spec without a baked card.</summary>
        public static double ComputeHoldoverMoa(WeaponSpec spec, double zeroRangeM, double sightHeightMm, double distanceM)
        {
            if (distanceM <= 0.0 || zeroRangeM <= 0.0) return 0.0;
            double theta0 = SolveZeroAngleRad(spec, zeroRangeM, sightHeightMm);
            double s0 = Math.Max(0.0, sightHeightMm) * 0.001;
            double thetaReq = Math.Atan2(s0 + DropAt(spec, distanceM), distanceM);
            // Small-angle difference -> MOA (60 arc-minutes per degree).
            return (thetaReq - theta0) * (180.0 / Math.PI) * 60.0;
        }

        /// <summary>
        /// Round trip between turrets and angles: nearest whole click to a desired hold.
        /// </summary>
        public static int MoaToClicks(double clickValueMoa, double moa)
        {
            if (clickValueMoa <= 0.0) return 0;
            return (int)Math.Round(moa / clickValueMoa, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Applies a requested click delta to the accumulated turret position, clamped to
        /// <see cref="MaxTurretClicksPerDirection"/> so a shot callout can never wind past the
        /// mechanical travel guard.
        /// </summary>
        public static int AdjustClicks(double clickValueMoa, int currentClicks, int requestedClicks)
        {
            if (clickValueMoa <= 0.0) return ClampInt(currentClicks, -MaxTurretClicksPerDirection, MaxTurretClicksPerDirection);
            long next = (long)currentClicks + requestedClicks;
            return ClampInt((int)next, -MaxTurretClicksPerDirection, MaxTurretClicksPerDirection);
        }

        /// <summary>Sight-dial position wrapped to a 0..modulus-1 ring (laser/zero-store indices).</summary>
        public static int WrapClickIndex(int clicks, int modulus = DefaultClicksPerRevolution)
        {
            if (modulus <= 0) return 0;
            int m = clicks % modulus;
            return m < 0 ? m + modulus : m;
        }

        /// <summary>
        /// Ascending sight-line crossing and crown height of the zeroed trajectory, solved by
        /// coarse 1 m scan + bisection (deterministic; h(r) is unimodal between 0 and the zero).
        /// </summary>
        public static void SolveSightLineCrossings(WeaponSpec spec, double zeroRangeM, double sightHeightMm,
            out double firstCrossingM, out double maxRiseM)
        {
            firstCrossingM = 0.0;
            maxRiseM = 0.0;
            if (spec.muzzleVelocity <= 0f || zeroRangeM <= 0.0) return;

            double prev = HeightAboveSightLine(spec, zeroRangeM, sightHeightMm, 0.5);
            double scanStep = Math.Min(1.0, Math.Max(0.5, zeroRangeM / 60.0));
            double crownD = 0.0;
            double crown = prev;
            for (double r = scanStep; r < zeroRangeM; r += scanStep)
            {
                double cur = HeightAboveSightLine(spec, zeroRangeM, sightHeightMm, r);
                if (firstCrossingM <= 0.0 && prev < 0.0 && cur >= 0.0)
                    firstCrossingM = Bisect(spec, zeroRangeM, sightHeightMm, r - scanStep, r);
                if (cur > crown) { crown = cur; crownD = r; }
                prev = cur;
            }
            if (firstCrossingM <= 0.0) firstCrossingM = zeroRangeM;
            maxRiseM = Math.Max(0.0, crown);
            if (crownD <= 0.0) maxRiseM = Math.Max(0.0, HeightAboveSightLine(spec, zeroRangeM, sightHeightMm, firstCrossingM * 1.6));
        }

        /// <summary>
        /// Last distance for which the zeroed trajectory still lands no lower than
        /// <paramref name="maxDropBelowSightM"/> under the sight line (point-blank limit).
        /// </summary>
        public static double SolvePointBlankRange(WeaponSpec spec, double zeroRangeM, double sightHeightMm,
            double maxDropBelowSightM = TargetMaxDropM)
        {
            if (spec.muzzleVelocity <= 0f || zeroRangeM <= 0.0) return 0.0;
            double limit = -Math.Abs(maxDropBelowSightM);
            double prev = HeightAboveSightLine(spec, zeroRangeM, sightHeightMm, Math.Max(5.0, zeroRangeM * 0.5));
            for (double r = Math.Max(10.0, zeroRangeM); r <= 2000.0; r += 10.0)
            {
                double cur = HeightAboveSightLine(spec, zeroRangeM, sightHeightMm, r);
                if (cur < limit)
                    return BisectDropAbove(spec, zeroRangeM, sightHeightMm, limit, r - 10.0, r);
                prev = cur;
            }
            return prev < limit ? 0.0 : 2000.0;
        }

        /// <summary>
        /// Two-sided point-blank window: the first distance at which the zeroed trajectory
        /// leaves the engagement box — crowning above <c>+maxCrownM</c> or undermining
        /// <c>-maxDropM</c>. With the default 10 cm / 38 cm deer box this is the range-card PBR.
        /// </summary>
        public static double SolvePointBlankWindow(WeaponSpec spec, double zeroRangeM, double sightHeightMm,
            double maxCrownM = TargetMaxCrownM, double maxDropM = TargetMaxDropM, double maxSearchM = 2000.0)
        {
            if (spec.muzzleVelocity <= 0f || zeroRangeM <= 0.0) return 0.0;
            double crown = Math.Abs(maxCrownM);
            double floor = -Math.Abs(maxDropM);
            for (double r = 2.0; r <= maxSearchM; r += 2.0)
            {
                double h = HeightAboveSightLine(spec, zeroRangeM, sightHeightMm, r);
                if (h > crown || h < floor) return Math.Max(0.0, r - 2.0);
            }
            return maxSearchM;
        }

        /// <summary>
        /// Battle zero: the smallest catalogue-step zero range whose two-sided point-blank
        /// window (10 cm crown / 38 cm drop) still reaches <paramref name="desiredPointBlankM"/>.
        /// The window is non-monotone in zero range (tiny zeroes crown early, huge zeroes dive),
        /// so this is a deterministic ascending scan with 0.1 m refinement; when no zero can
        /// reach the requested envelope the best window is returned instead.
        /// </summary>
        public static double ComputeBattleZero(WeaponSpec spec, double sightHeightMm, double desiredPointBlankM, double maxZeroM = 300.0)
        {
            double start = 25.0;
            double end = Math.Max(start, maxZeroM);
            double bestZ = start;
            double bestWindow = -1.0;
            double hitZ = -1.0;
            for (double z = start; z <= end + 1e-9; z += 2.5)
            {
                double w = SolvePointBlankWindow(spec, z, sightHeightMm);
                if (w > bestWindow)
                {
                    bestWindow = w;
                    bestZ = z;
                }
                if (hitZ < 0.0 && w >= desiredPointBlankM)
                {
                    hitZ = z;
                    break;
                }
            }
            if (hitZ < 0.0) return Math.Round(bestZ, 1);
            double lo = Math.Max(start, hitZ - 2.5);
            for (double z = lo; z <= hitZ + 1e-9; z += 0.1)
            {
                if (SolvePointBlankWindow(spec, z, sightHeightMm) >= desiredPointBlankM)
                    return Math.Round(z, 1);
            }
            return Math.Round(hitZ, 1);
        }

        private static double Bisect(WeaponSpec spec, double zero, double sight, double lo, double hi)
        {
            for (int i = 0; i < 40; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (HeightAboveSightLine(spec, zero, sight, mid) < 0.0) lo = mid;
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        private static double BisectDropAbove(WeaponSpec spec, double zero, double sight, double limit, double lo, double hi)
        {
            for (int i = 0; i < 30; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (HeightAboveSightLine(spec, zero, sight, mid) < limit) hi = mid;
                else lo = mid;
            }
            return 0.5 * (lo + hi);
        }

        private static WeaponSpec ApplyVelocityOverride(WeaponSpec spec, double muzzleVelocityOverride)
        {
            if (muzzleVelocityOverride <= 0.0 || Math.Abs(muzzleVelocityOverride - spec.muzzleVelocity) < 1e-6) return spec;
            spec.muzzleVelocity = (float)muzzleVelocityOverride;
            spec.muzzleEnergy = (float)(0.5 * spec.bulletMass * muzzleVelocityOverride * muzzleVelocityOverride);
            return spec;
        }

        private static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);
        private static int ClampInt(int x, int lo, int hi) => x < lo ? lo : (x > hi ? hi : x);
    }
}
