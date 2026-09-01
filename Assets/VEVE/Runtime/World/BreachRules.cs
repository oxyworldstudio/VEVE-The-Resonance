namespace VEVE.World
{
    using System.Globalization;
    using UnityEngine;
    using VEVE.Combat;

    /// <summary>How the composer proposes to get through a door.</summary>
    public enum BreachMethod
    {
        /// <summary>Nothing to do (door already open/breached) or no door bound.</summary>
        None = 0,

        /// <summary>Brute force with boot or ram; always applicable, always the loudest soft option.</summary>
        Kick = 1,

        /// <summary>Stealth lockpick; needs a kit and a real lock (lockLevel &gt;= 1).</summary>
        Pick = 2,

        /// <summary>Explosive charge; needs a carried charge and integrity low enough for it to finish the door.</summary>
        Charge = 3
    }

    /// <summary>
    /// Immutable decision returned by <see cref="BreachRules.Plan"/>: the chosen method, its
    /// deterministic time cost and its noise cost (same loudness scale as
    /// <see cref="DoorModel.KickNoiseLoudness"/> / TacticalSound, ready for morale coupling).
    /// Value equality is exact so determinism tests can compare whole plans.
    /// </summary>
    public struct BreachPlan : System.IEquatable<BreachPlan>
    {
        /// <summary>Proposed breach method.</summary>
        public BreachMethod method;

        /// <summary>Deterministic estimated seconds until the door gives (0 for <see cref="BreachMethod.None"/>).</summary>
        public float estimatedSeconds;

        /// <summary>Noise loudness emitted by executing this plan (see <see cref="BreachRules.NoiseLoudness"/>).</summary>
        public float noiseLoudness;

        /// <summary>Builds a plan; prefer <see cref="BreachRules.Plan"/> so invariants stay in one place.</summary>
        public BreachPlan(BreachMethod method, float estimatedSeconds, float noiseLoudness)
        {
            this.method = method;
            this.estimatedSeconds = estimatedSeconds;
            this.noiseLoudness = noiseLoudness;
        }

        /// <summary>Exact field-wise equality (pure inputs yield bit-identical plans).</summary>
        public bool Equals(BreachPlan other)
        {
            return method == other.method
                && estimatedSeconds == other.estimatedSeconds
                && noiseLoudness == other.noiseLoudness;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is BreachPlan other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)method;
                hash = hash * 31 + estimatedSeconds.GetHashCode();
                hash = hash * 31 + noiseLoudness.GetHashCode();
                return hash;
            }
        }

        /// <summary>Log rendering (invariant culture, deterministic).</summary>
        /// <returns>Readable plan description.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:0.00}s {2:0.#}dB", method, estimatedSeconds, noiseLoudness);
        }
    }

    /// <summary>
    /// Pure, deterministic breach composer (W-H7): decides <em>how</em> to open a door and what it
    /// costs in time and noise, without touching scene state. Consumes <see cref="DoorModel"/>
    /// (kick damage, pick seconds, breach joules) and <see cref="GrenadeRules.BlastEnergyAtDistance"/>
    /// (charge falloff seam) so there is exactly one authority for each physical truth.
    ///
    /// <para><b>Priority ladder (stealth-first doctrine):</b> a quiet pick beats a viable charge,
    /// and a viable charge beats the always-available, loudest kick. A charge is only planned when
    /// the carried mass actually finishes the door (integrity low enough to matter); otherwise it
    /// would spend maximum loudness for a partial breach.</para>
    ///
    /// <para><b>Documented noise table</b> (authoritative loudness values, dB-scale, fed to the
    /// TacticalSound/morale seams; Kick mirrors <see cref="DoorModel.KickNoiseLoudness"/>):</para>
    /// <list type="table">
    /// <listheader><term>Method</term><description>Loudness</description></listheader>
    /// <item><term><see cref="BreachMethod.None"/></term><description>0</description></item>
    /// <item><term><see cref="BreachMethod.Pick"/></term><description>4</description></item>
    /// <item><term><see cref="BreachMethod.Kick"/></term><description>45</description></item>
    /// <item><term><see cref="BreachMethod.Charge"/></term><description>110</description></item>
    /// </list>
    /// </summary>
    public static class BreachRules
    {
        /// <summary>Fixed real-time cost of one kick attempt (documented pacing constant).</summary>
        public const float KickSecondsPerHit = 1.2f;

        /// <summary>Fixed plant + detonate time of a breaching charge (documented pacing constant).</summary>
        public const float ChargeSeconds = 3.5f;

        /// <summary>Hard cap on the kick-iteration loop: no door ever costs more than 20 kicks of estimate.</summary>
        public const int MaxKickIterations = 20;

        /// <summary>Noise loudness of <see cref="BreachMethod.None"/>.</summary>
        public const float NoiseNone = 0f;

        /// <summary>Noise loudness of <see cref="BreachMethod.Pick"/>.</summary>
        public const float NoisePick = 4f;

        /// <summary>Noise loudness of <see cref="BreachMethod.Kick"/> (mirrors <see cref="DoorModel.KickNoiseLoudness"/>).</summary>
        public const float NoiseKick = 45f;

        /// <summary>Noise loudness of <see cref="BreachMethod.Charge"/>.</summary>
        public const float NoiseCharge = 110f;

        /// <summary>
        /// Documented loudness per method (table above). Pure switch, total over the enum.
        /// </summary>
        /// <param name="method">Breach method to price.</param>
        /// <returns>Loudness on the TacticalSound dB scale.</returns>
        public static float NoiseLoudness(BreachMethod method)
        {
            switch (method)
            {
                case BreachMethod.Pick: return NoisePick;
                case BreachMethod.Kick: return NoiseKick;
                case BreachMethod.Charge: return NoiseCharge;
                default: return NoiseNone;
            }
        }

        /// <summary>
        /// Compose the breach decision for one door snapshot. Deterministic: the same inputs always
        /// produce an exactly equal plan. Never mutates anything and never reads the scene.
        /// <list type="number">
        /// <item><description>Open/Breached doors need nothing (<see cref="BreachMethod.None"/>).</description></item>
        /// <item><description>Pick when a lockpick kit is held and the door is actually locked with
        /// lockLevel &gt;= 1 (unlatched doors and jiggle-class locks need no picking).</description></item>
        /// <item><description>Charge only when one is carried, its mass exceeds
        /// <see cref="DoorModel.MinChargeKg"/>, and <see cref="DoorModel.BreachDamage"/> meets the
        /// remaining integrity (integrity low enough for the charge to matter).</description></item>
        /// <item><description>Kick is the unconditional fallback; an unlatched
        /// (<see cref="DoorState.Closed"/>) door gives to a single kick per
        /// <see cref="DoorModel.ResolveKick"/>, a locked one falls to integrity attrition.</description></item>
        /// </list>
        /// </summary>
        /// <param name="state">Door state snapshot.</param>
        /// <param name="lockLevel">0-3 lock tier snapshot.</param>
        /// <param name="integrity">Remaining door integrity snapshot.</param>
        /// <param name="chargeKg">Mass of the carried breaching charge (kg of C4).</param>
        /// <param name="hasLockpickKit">Whether the operator carries a lockpick kit.</param>
        /// <param name="hasCharge">Whether the operator carries a charge at all.</param>
        /// <returns>The deterministic breach plan.</returns>
        public static BreachPlan Plan(DoorState state, int lockLevel, float integrity, float chargeKg, bool hasLockpickKit, bool hasCharge)
        {
            if (state == DoorState.Open || state == DoorState.Breached)
            {
                return new BreachPlan(BreachMethod.None, 0f, NoiseLoudness(BreachMethod.None));
            }

            if (hasLockpickKit && state == DoorState.Locked && lockLevel >= 1)
            {
                return new BreachPlan(BreachMethod.Pick, DoorModel.PickSeconds(lockLevel, true), NoiseLoudness(BreachMethod.Pick));
            }

            float kickDamage = DoorModel.KickDamage(lockLevel);
            bool chargeViable = hasCharge
                && chargeKg > DoorModel.MinChargeKg
                && DoorModel.BreachDamage(chargeKg) >= integrity;
            if (chargeViable)
            {
                float chargeJ = DoorModel.BreachDamage(chargeKg);
                return new BreachPlan(BreachMethod.Charge, SecondsToBreak(integrity, kickDamage, chargeJ, DoorModel.BreachJoulesPerKg), NoiseLoudness(BreachMethod.Charge));
            }

            float kickSeconds = state == DoorState.Closed
                ? KickSecondsPerHit
                : SecondsToBreak(integrity, kickDamage, 0f, 0f);
            return new BreachPlan(BreachMethod.Kick, kickSeconds, NoiseLoudness(BreachMethod.Kick));
        }

        /// <summary>
        /// Deterministic time-to-breach. With charge data supplied, the charge's energy is first
        /// attenuated through <see cref="GrenadeRules.BlastEnergyAtDistance"/> at contact distance
        /// (distance-0 falloff is lossless; the seam stays ready for distance-aware callers),
        /// converted to an equivalent C4 mass via <paramref name="blastEnergyJPerKg"/> and priced
        /// by <see cref="DoorModel.BreachDamage"/>; any integrity surviving the blast is then
        /// kicked. Without charge data the estimate is kick attrition only. The kick loop is
        /// strictly bounded at <see cref="MaxKickIterations"/> iterations: an immovable door costs
        /// exactly 20 kicks of time, never an unbounded loop.
        /// </summary>
        /// <param name="integrity">Remaining door integrity.</param>
        /// <param name="kickDamagePerHit">Integrity removed by one kick (per <see cref="DoorModel.KickDamage"/>).</param>
        /// <param name="chargeJ">Total blast energy delivered by the charge; 0 or negative disables the charge path.</param>
        /// <param name="blastEnergyJPerKg">Charge efficacy in joules per kg of C4-equivalent (per <see cref="DoorModel.BreachJoulesPerKg"/>); must be positive for the charge path.</param>
        /// <returns>Estimated seconds until the door gives (0 when already broken).</returns>
        public static float SecondsToBreak(float integrity, float kickDamagePerHit, float chargeJ, float blastEnergyJPerKg)
        {
            float remaining = integrity > 0f ? integrity : 0f;
            if (remaining <= 0f) return 0f;

            float chargeSeconds = 0f;
            if (chargeJ > 0f && blastEnergyJPerKg > 0f)
            {
                float blastAtDoor = GrenadeRules.BlastEnergyAtDistance(0f, GrenadeRules.DefaultRadiusM, chargeJ);
                float equivalentKg = blastAtDoor / blastEnergyJPerKg;
                float doorDamage = DoorModel.BreachDamage(equivalentKg);
                remaining -= doorDamage;
                if (remaining <= 0f) return ChargeSeconds;
                chargeSeconds = ChargeSeconds;
            }

            int kicks = 0;
            while (remaining > 0f && kicks < MaxKickIterations)
            {
                remaining -= kickDamagePerHit;
                kicks++;
            }
            return chargeSeconds + kicks * KickSecondsPerHit;
        }
    }
}
