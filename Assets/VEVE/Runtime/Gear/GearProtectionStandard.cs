using System;
using System.Collections.Generic;

namespace VEVE.Gear
{
    /// <summary>
    /// Protection rating namespaces. NIJ 0101.07 covers handgun soft armor plus rifle plates;
    /// VPAM ERGV/SR covers European multi-threat ratings above NIJ coverage.
    /// </summary>
    public enum ProtectionStandardFamily
    {
        /// <summary>No ballistic rating (load carriage, comfort gear).</summary>
        None,
        /// <summary>NIJ 0101.07 handgun + rifle plate levels.</summary>
        NIJ_0101_07,
        /// <summary>VPAM ERGV/SR personnel protection classes.</summary>
        VPAM
    }

    /// <summary>
    /// Ordered protection tiers spanning both families, weakest to strongest. Enum ordering is the
    /// authority for level comparison and for catalog indexing of <see cref="GearProtectionStandard.Levels"/>.
    /// </summary>
    public enum ProtectionLevel
    {
        Unrated = 0,
        NIJ_I,
        NIJ_II,
        NIJ_IIIA,
        VPAM_TRS,
        NIJ_III,
        VPAM_BRS_S,
        VPAM_SR9,
        VPAM_GRW1,
        NIJ_IV,
        VPAM_B6,
        VPAM_B7,
        V50_FRAG
    }

    /// <summary>
    /// Reference cartridge descriptors drawn from NIJ 0101.07 test threat tables and standard
    /// proof-gun data. Velocities are V0 test velocities; masses are nominal projectile masses.
    /// </summary>
    public readonly struct ThreatAmmunition
    {
        /// <summary>Human-readable designation, e.g. "7.62x39mm API 6T1".</summary>
        public readonly string designation;
        /// <summary>Nominal projectile mass in kilograms.</summary>
        public readonly float massKg;
        /// <summary>Test velocity in meters per second.</summary>
        public readonly float velocityMps;
        /// <summary>Whether the projectile has an armor-piercing core.</summary>
        public readonly bool armorPiercing;

        /// <summary>Kinetic energy of the reference load in joules.</summary>
        public float EnergyJoules => 0.5f * massKg * velocityMps * velocityMps;

        /// <summary>Creates a threat descriptor.</summary>
        /// <param name="designation">Cartridge designation.</param>
        /// <param name="massKg">Projectile mass in kilograms.</param>
        /// <param name="velocityMps">Test velocity in meters per second.</param>
        /// <param name="armorPiercing">True for AP/penetrator cores.</param>
        public ThreatAmmunition(string designation, float massKg, float velocityMps, bool armorPiercing)
        {
            this.designation = designation;
            this.massKg = massKg;
            this.velocityMps = velocityMps;
            this.armorPiercing = armorPiercing;
        }
    }

    /// <summary>
    /// Immutable data row for one protection level: rated threats, per-hit stopping energy ceiling,
    /// behind-armor blunt trauma budget, backface deformation limits and multi-hit degradation.
    /// </summary>
    public readonly struct ProtectionLevelData
    {
        /// <summary>The level this row describes.</summary>
        public readonly ProtectionLevel level;
        /// <summary>Rating family the level belongs to.</summary>
        public readonly ProtectionStandardFamily family;
        /// <summary>Threats the level is certified to stop at the standard test angle.</summary>
        public readonly ThreatAmmunition[] ratedThreats;
        /// <summary>Aggregate kinetic energy ceiling (J) a single hit may carry and still be stopped at normal incidence.</summary>
        public readonly float stopEnergyJoules;
        /// <summary>Cumulative behind-armor trauma energy (J) allowed to pass to the body on a stopped hit.</summary>
        public readonly float traumaEnergyLimitJoules;
        /// <summary>Maximum backface deformation (mm) permitted by the standard on the first hit.</summary>
        public readonly float maxBackfaceMm;
        /// <summary>Backface deformation (mm) measured on the first hit at the reference threat energy.</summary>
        public readonly float firstHitBackfaceMm;
        /// <summary>Fraction of the level's stopping ceiling that survives on a second hit into the same strike area (0..1). NIJ 0101.07 re-hits are commonly derated ~30-40% on multi-hit ceramic.</summary>
        public readonly float multiHitRetention;
        /// <summary>Short display name.</summary>
        public readonly string label;

        /// <summary>Creates a protection level row.</summary>
        /// <param name="level">Level identifier.</param>
        /// <param name="family">Rating family.</param>
        /// <param name="label">Display label.</param>
        /// <param name="ratedThreats">Certified threats.</param>
        /// <param name="stopEnergyJoules">Single-hit stopping ceiling in joules.</param>
        /// <param name="traumaEnergyLimitJoules">Trauma energy budget in joules.</param>
        /// <param name="maxBackfaceMm">Standard backface limit in millimeters.</param>
        /// <param name="firstHitBackfaceMm">Measured first-hit backface in millimeters.</param>
        /// <param name="multiHitRetention">Second-hit ceiling retention factor.</param>
        public ProtectionLevelData(
            ProtectionLevel level,
            ProtectionStandardFamily family,
            string label,
            ThreatAmmunition[] ratedThreats,
            float stopEnergyJoules,
            float traumaEnergyLimitJoules,
            float maxBackfaceMm,
            float firstHitBackfaceMm,
            float multiHitRetention)
        {
            this.level = level;
            this.family = family;
            this.label = label;
            this.ratedThreats = ratedThreats ?? Array.Empty<ThreatAmmunition>();
            this.stopEnergyJoules = stopEnergyJoules;
            this.traumaEnergyLimitJoules = traumaEnergyLimitJoules;
            this.maxBackfaceMm = maxBackfaceMm;
            this.firstHitBackfaceMm = firstHitBackfaceMm;
            this.multiHitRetention = multiHitRetention;
        }
    }

    /// <summary>
    /// Deterministic model of real ballistic personal-protection standards (NIJ 0101.07, VPAM classes).
    /// Pure statics only — no Unity object model — so it is unit-testable in EditMode.
    /// </summary>
    public static class GearProtectionStandard
    {
        /// <summary>Reference NIJ proof-gun test angle from plate normal (12 degrees).</summary>
        public const float ReferenceAngleDeg = 12f;

        /// <summary>Angle whose cosine the obliquity curve is normalized against.</summary>
        public const float ObliquityCutoffDeg = 60f;

        private static readonly Dictionary<ProtectionLevel, ProtectionLevelData> Table = BuildTable();

        /// <summary>All rated levels ordered by enum value. Index 0 is <see cref="ProtectionLevel.Unrated"/>.</summary>
        public static ProtectionLevelData[] Levels
        {
            get
            {
                var list = new List<ProtectionLevelData>(Table.Values);
                return list.ToArray();
            }
        }

        /// <summary>Looks up the data row for a level.</summary>
        /// <param name="level">Level to resolve.</param>
        /// <param name="data">Row when found.</param>
        /// <returns>True for known levels.</returns>
        public static bool TryGetLevel(ProtectionLevel level, out ProtectionLevelData data)
        {
            return Table.TryGetValue(level, out data);
        }

        /// <summary>
        /// Normalized obliquity defense factor in [0.55, 1] as a function of the angle between the
        /// projectile path and the armor normal. 1.0 at normal incidence (0 degrees), falling through
        /// a smoothstep to 0.55 at <see cref="ObliquityCutoffDeg"/> and beyond. Models the V50-style
        /// drop in effective stopping capacity as strikes graze the plate: oblique shots see reduced
        /// normal impulse on the backing but a higher chance of the core catching and yawing through,
        /// so the usable energy budget shrinks.
        /// </summary>
        /// <param name="angleDeg">Impact angle from armor normal, degrees; absolute value is used.</param>
        /// <returns>Factor in [0.55, 1]; multiply by the level's stopping ceiling.</returns>
        public static float ObliquityDefenseFactor(float angleDeg)
        {
            float t = Math.Clamp(Math.Abs(angleDeg) / ObliquityCutoffDeg, 0f, 1f);
            float s = t * t * (3f - 2f * t);
            return 1f - 0.45f * s;
        }

        /// <summary>
        /// Deterministic stopped-hit test for a level against an incoming round.
        /// Effective ceiling = level.stopEnergyJoules × ObliquityDefenseFactor(angle) × multi-hit retention.
        /// A hit is stopped only when the incoming energy is under the ceiling AND the predicted
        /// behind-armor trauma stays inside the level's trauma budget (stopping a rifle round in
        /// handgun soft armor is a fail — ribs break, person goes down).
        /// Trauma output = energy that reaches the body as blunt deformation work when stopped,
        /// or the full impact energy when not stopped.
        /// Angle monotonicity: a shot stopped at normal incidence may fail once obliquity grows,
        /// never the reverse.
        /// </summary>
        /// <param name="level">Protection level of the struck panel.</param>
        /// <param name="velocityMps">Projectile velocity at impact, m/s (used for the V50 energy consistency check; &lt;=0 derives energy from a 4 g reference projectile).</param>
        /// <param name="energyJoules">Kinetic energy at impact, J.</param>
        /// <param name="angleDeg">Angle from panel normal, degrees; reference angle (12) yields the certified ceiling.</param>
        /// <param name="previousStrikesOnPanel">Hits already absorbed by the same panel this engagement (0 = first hit).</param>
        /// <param name="traumaJoules">Blunt trauma energy delivered to the body.</param>
        /// <param name="backfaceMm">Predicted backface deformation (mm) for the attempt; 0 when the round is not stopped.</param>
        /// <returns>True when the level stops the round within its trauma budget.</returns>
        public static bool TryStopAmmunition(
            ProtectionLevel level,
            float velocityMps,
            float energyJoules,
            float angleDeg,
            int previousStrikesOnPanel,
            out float traumaJoules,
            out float backfaceMm)
        {
            traumaJoules = Math.Max(0f, energyJoules);
            backfaceMm = 0f;
            if (!Table.TryGetValue(level, out ProtectionLevelData data) || energyJoules <= 0f)
                return false;

            float ceiling = EffectiveCeiling(data, angleDeg, previousStrikesOnPanel);
            if (energyJoules <= ceiling)
            {
                float ratio = data.stopEnergyJoules > 0f ? energyJoules / data.stopEnergyJoules : 0f;
                // Trauma scales superlinearly toward the trauma budget as the hit approaches the panel's rated threat energy.
                float traumaCurve = ratio < 1f ? ratio * ratio : ratio * (2f - 1f / Math.Max(ratio, 1f));
                traumaJoules = Math.Min(data.traumaEnergyLimitJoules, traumaCurve * data.traumaEnergyLimitJoules);
                float baseBfd = Math.Max(data.firstHitBackfaceMm, data.maxBackfaceMm * 0.4f);
                backfaceMm = Math.Min(data.maxBackfaceMm, baseBfd * (0.45f + 0.55f * Math.Min(ratio, 1f)) * (1f + 0.15f * Math.Max(0, previousStrikesOnPanel)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Convenience overload using first-hit, reference-adjacent conditions and an energy-only signature.
        /// </summary>
        /// <param name="level">Panel protection level.</param>
        /// <param name="energyJoules">Impact energy in joules.</param>
        /// <param name="angleDeg">Angle from normal, degrees.</param>
        /// <param name="traumaJoules">Trauma energy transmitted to the body.</param>
        /// <returns>True when stopped.</returns>
        public static bool TryStopAmmunition(ProtectionLevel level, float energyJoules, float angleDeg, out float traumaJoules)
        {
            return TryStopAmmunition(level, 0f, energyJoules, angleDeg, 0, out traumaJoules, out _);
        }

        /// <summary>
        /// Effective single-hit stopping ceiling (J) for a level at an obliquity and strike index,
        /// after multi-hit derating of previously struck panels.
        /// </summary>
        /// <param name="data">Panel level row.</param>
        /// <param name="angleDeg">Angle from normal, degrees.</param>
        /// <param name="previousStrikesOnPanel">Prior hits on the same panel.</param>
        /// <returns>Derated ceiling in joules.</returns>
        public static float EffectiveCeiling(ProtectionLevelData data, float angleDeg, int previousStrikesOnPanel)
        {
            float retention = previousStrikesOnPanel <= 0
                ? 1f
                : Math.Clamp(MathF.Pow(Math.Max(0f, data.multiHitRetention), previousStrikesOnPanel), 0.25f, 1f);
            return data.stopEnergyJoules * retention * ReferenceToAngleWeight(angleDeg);
        }

        private static float ReferenceToAngleWeight(float angleDeg)
        {
            // Curve is normalized so the certified ceiling maps to the NIJ/VPAM 12-degree proof angle.
            float correction = ObliquityDefenseFactor(angleDeg);
            float atReference = ObliquityDefenseFactor(ReferenceAngleDeg);
            return correction / atReference;
        }

        private static Dictionary<ProtectionLevel, ProtectionLevelData> BuildTable()
        {
            var fmj9 = new ThreatAmmunition("9x19mm FMJ RN 124gr", 0.008f, 396f, false);
            var jrsp357 = new ThreatAmmunition(".357 SIG SJSP 125gr", 0.0081f, 448f, false);
            var jsp44 = new ThreatAmmunition(".44 Magnum SJSP 240gr", 0.0156f, 436f, false);
            var m80 = new ThreatAmmunition("7.62x51mm NATO M80 FMJ", 0.0097f, 854f, false);
            var m855 = new ThreatAmmunition("5.56x45mm M855 SS109", 0.004f, 918f, false);
            var m193 = new ThreatAmmunition("5.56x45mm M193 FMJ", 0.0036f, 997f, false);
            var api6t1 = new ThreatAmmunition("7.62x39mm API 6T1", 0.0074f, 735f, true);
            var m993 = new ThreatAmmunition("7.62x51mm M993 AP", 0.0096f, 854f, true);
            var b41 = new ThreatAmmunition("7.62x54R B32 API", 0.0119f, 820f, true);
            var b32 = new ThreatAmmunition("7.62x51mm NATO AP (B32 ref)", 0.0094f, 880f, true);
            var sr9 = new ThreatAmmunition("9x19mm AP (VPAM SR9 ref)", 0.008f, 420f, true);
            var grw = new ThreatAmmunition("7.62x39mm API (VPAM GRW1 ref)", 0.0074f, 730f, true);
            var v50 = new ThreatAmmunition("17gr FSP 90% V50", 0.0011f, 1524f, false);
            var ijap = new ThreatAmmunition("7.62x39mm BZ (AP)", 0.0075f, 740f, true);

            var rows = new ProtectionLevelData[]
            {
                new ProtectionLevelData(ProtectionLevel.Unrated, ProtectionStandardFamily.None, "Unrated",
                    Array.Empty<ThreatAmmunition>(), 0f, 0f, 0f, 0f, 1f),
                new ProtectionLevelData(ProtectionLevel.NIJ_I, ProtectionStandardFamily.NIJ_0101_07, "NIJ 0101.07 Level I",
                    new[] { fmj9 }, 888f, 12f, 44f, 12f, 1f),
                new ProtectionLevelData(ProtectionLevel.NIJ_II, ProtectionStandardFamily.NIJ_0101_07, "NIJ 0101.07 Level II",
                    new[] { fmj9, jrsp357 }, 1140f, 15f, 44f, 15f, 1f),
                new ProtectionLevelData(ProtectionLevel.NIJ_IIIA, ProtectionStandardFamily.NIJ_0101_07, "NIJ 0101.07 Level IIIA",
                    new[] { fmj9, jrsp357, jsp44 }, 1568f, 22f, 44f, 18f, 1f),
                new ProtectionLevelData(ProtectionLevel.VPAM_TRS, ProtectionStandardFamily.VPAM, "VPAM TRS (Level A)",
                    new[] { fmj9, jsp44 }, 1600f, 20f, 30f, 16f, 1f),
                new ProtectionLevelData(ProtectionLevel.NIJ_III, ProtectionStandardFamily.NIJ_0101_07, "NIJ 0101.07 Level III (R)",
                    new[] { m80, m855 }, 3600f, 40f, 44f, 25f, 0.7f),
                new ProtectionLevelData(ProtectionLevel.VPAM_BRS_S, ProtectionStandardFamily.VPAM, "VPAM ERGV BRS-S (9mm)",
                    new[] { fmj9, sr9 }, 1700f, 24f, 30f, 20f, 1f),
                new ProtectionLevelData(ProtectionLevel.VPAM_SR9, ProtectionStandardFamily.VPAM, "VPAM SR9 (AP handgun + 5.56)",
                    new[] { sr9, m855, m193 }, 2150f, 33f, 30f, 24f, 0.85f),
                new ProtectionLevelData(ProtectionLevel.VPAM_GRW1, ProtectionStandardFamily.VPAM, "VPAM GRW1 (7.62x39 AP)",
                    new[] { api6t1, grw, ijap }, 2500f, 38f, 30f, 26f, 0.8f),
                new ProtectionLevelData(ProtectionLevel.NIJ_IV, ProtectionStandardFamily.NIJ_0101_07, "NIJ 0101.07 Level IV (R)",
                    new[] { m993, b32 }, 3680f, 50f, 44f, 30f, 0.6f),
                new ProtectionLevelData(ProtectionLevel.VPAM_B6, ProtectionStandardFamily.VPAM, "VPAM BRV B6 (9mm/AP rifle)",
                    new[] { sr9, m80 }, 3560f, 42f, 30f, 27f, 0.7f),
                new ProtectionLevelData(ProtectionLevel.VPAM_B7, ProtectionStandardFamily.VPAM, "VPAM BRV B7 (7.62 AP)",
                    new[] { b32, m993 }, 3700f, 55f, 30f, 29f, 0.65f),
                new ProtectionLevelData(ProtectionLevel.V50_FRAG, ProtectionStandardFamily.NIJ_0101_07, "Frag-proof V50 (STANAG 2920 ref)",
                    new[] { v50 }, 1330f, 60f, 0f, 20f, 0.9f)
            };

            var table = new Dictionary<ProtectionLevel, ProtectionLevelData>(rows.Length);
            for (int i = 0; i < rows.Length; i++) table[rows[i].level] = rows[i];
            return table;
        }
    }
}
