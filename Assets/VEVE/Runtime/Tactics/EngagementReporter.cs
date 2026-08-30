using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace VEVE.Tactics
{
    /// <summary>
    /// Range bands used for engagement bookkeeping, aligned with the distances that drive
    /// Ballistics-driven hit probability and audio signature in the spec (GAMEPLAY_MECHANICS_SPEC §2).
    /// </summary>
    public enum DistanceBand
    {
        /// <summary>0–50 m: door-to-street fight; maximum intel and maximum stress.</summary>
        Close = 0,

        /// <summary>50–200 m: typical assault range.</summary>
        Medium = 1,

        /// <summary>200–400 m: observed engagement; precision matters.</summary>
        Long = 2,

        /// <summary>400 m+: over-watch / sniper territory; thin data, low stress.</summary>
        Far = 3
    }

    /// <summary>
    /// How the enemy element resolved the contact. Drives both the stress feedback to morale and
    /// the intel the crew extracts from the fight.
    /// </summary>
    public enum ContactOutcome
    {
        /// <summary>Enemy element was neutralized (all engaged targets killed).</summary>
        Killed = 0,

        /// <summary>Enemy element broke and displaced under fire.</summary>
        Fled = 1,

        /// <summary>Enemy held position / the crew was forced to disengage.</summary>
        Held = 2,

        /// <summary>Ambiguous contact: no confirmed effect.</summary>
        Inconclusive = 3
    }

    /// <summary>
    /// Raw numbers for one completed contact, handed to <see cref="EngagementReporter"/> by the
    /// integrator who observes the shooting. Deliberately a dumb data shape: the reporter never
    /// touches Ballistics components, it consumes per-shot tallies that the gunnery layer already
    /// produces (rounds spent vs. rounds on target, distances from impact rays). This keeps the
    /// tactics layer compilable without any weapon types and testable as pure logic.
    /// </summary>
    [Serializable]
    public struct ContactReportInput
    {
        /// <summary>Mean engagement distance in metres (from the ballistic solution log); &lt; 0 clamps to 0.</summary>
        public float distanceM;

        /// <summary>Weapon family id, e.g. "rifle_assault", "mg_generic"; free-form key for intel dedup.</summary>
        public string weaponFamilyId;

        /// <summary>Number of rounds fired during the contact (budget consumed).</summary>
        public int roundsConsumed;

        /// <summary>Rounds classed as hits/misses, from terminal ballistics events. Clamped to roundsConsumed.</summary>
        public int roundsOnTarget;

        /// <summary>Distinct enemy combatants observed engaging this contact (≥ 0).</summary>
        public int targetsEngaged;

        /// <summary>Confirmed kills among <see cref="targetsEngaged"/>.</summary>
        public int targetsKilled;

        /// <summary>Targets seen breaking contact and displacing.</summary>
        public int targetsFledCount;

        /// <summary>True when this contact was the successful suppressive base of fire that let a maneuver element move (good-initiative channel).</summary>
        public bool baseOfFireSuccess;

        /// <summary>Duration of the fire fight in seconds; negative or NaN treated as 0 (no negative time budgets).</summary>
        public float contactDurationSeconds;

        /// <summary>Debug rendering for logs.</summary>
        /// <returns>One-line description of the raw input.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} @ {1:0}m, {2}/{3} on target, {4} engaged", weaponFamilyId ?? "?", distanceM, roundsOnTarget, roundsConsumed, targetsEngaged);
        }
    }

    /// <summary>
    /// The sanitized, rated record of a single contact produced by <see cref="EngagementReporter.CloseContact"/>.
    /// </summary>
    [Serializable]
    public sealed class ContactRecord
    {
        /// <summary>Range classification of the engagement.</summary>
        public DistanceBand band;

        /// <summary>Weapon family id (may be empty when the shooter did not identify).</summary>
        public string weaponFamilyId;

        /// <summary>Rounds actually accounted for (clamped ≥ 0).</summary>
        public int roundsConsumed;

        /// <summary>Confirmed hits, clamped into [0, roundsConsumed].</summary>
        public int roundsOnTarget;

        /// <summary>hits / rounds; 0 when no rounds fired.</summary>
        public float hitRatio;

        /// <summary>Suppression effectiveness rating in [0, 1]: how convincingly this contact pinned or broke the enemy.</summary>
        public float suppressionEffectiveness;

        /// <summary>Resolution of the contact.</summary>
        public ContactOutcome outcome;

        /// <summary>Contact duration in seconds, clamped ≥ 0.</summary>
        public float contactDurationSeconds;

        /// <summary>Confirmed enemy kills credited to this contact.</summary>
        public int targetsKilled;

        /// <summary>Enemy targets observed breaking and displacing.</summary>
        public int targetsFled;

        /// <summary>Morale stress delta this record implies when fed back into <see cref="SquadMorale"/> (see <see cref="EngagementReporter.ComputeStressDelta"/>).</summary>
        public float stressDelta;

        /// <summary>Intel points this record grants the crew (see <see cref="EngagementReporter.ComputeIntelValue"/>).</summary>
        public float intelValue;

        /// <summary>Debrief line.</summary>
        /// <returns>Readable record summary.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} eff {3:0.##} stress {4:0.#} intel {5:0.#}", band, weaponFamilyId ?? "?", outcome, suppressionEffectiveness, stressDelta, intelValue);
        }
    }

    /// <summary>
    /// Accumulates per-contact records for the mission debrief and converts shooting results into
    /// the two feedback channels the encounter director needs: stress deltas that feed squad
    /// morale, and intel value that feeds campaign scoring. Pure logic, instance state only.
    ///
    /// Intel model (documentation tie-in — GAMEPLAY_MECHANICS_SPEC.md §3.4 campaign/world state
    /// and §9 mission scoring: "each contact is information", intel points are the spendable
    /// currency): closer contact = better observation, first identification of a weapon family =
    /// bonus, enemy fleeing is richer than enemy dying (routes, rally points, composition), a
    /// successful suppression tells us where the enemy's command weight sits.
    /// </summary>
    [Serializable]
    public sealed class EngagementReporter
    {
        /// <summary>Close-band upper bound in metres (exclusive).</summary>
        public const float CloseBandMaxM = 50f;

        /// <summary>Medium-band upper bound in metres (exclusive).</summary>
        public const float MediumBandMaxM = 200f;

        /// <summary>Long-band upper bound in metres (exclusive).</summary>
        public const float LongBandMaxM = 400f;

        /// <summary>Base stress delta when the enemy element was destroyed.</summary>
        public const float StressOnKill = 6f;

        /// <summary>Base stress delta when the enemy element broke.</summary>
        public const float StressOnFlee = 3f;

        /// <summary>Base stress delta when the enemy held and the crew gained nothing.</summary>
        public const float StressOnHold = -18f;

        /// <summary>Base stress delta for an inconclusive contact.</summary>
        public const float StressOnInconclusive = -6f;

        /// <summary>Extra stress penalty (subtracted) proportional to the miss ratio on a held contact.</summary>
        public const float HeldContactMissWeight = 8f;

        /// <summary>Stress penalty when a supposed base-of-fire attempt did not achieve suppression.</summary>
        public const float FailedBaseOfFirePenalty = 4f;

        /// <summary>Documented stress envelope: deltas clamp to [-25, +10].</summary>
        public const float StressMin = -25f;

        /// <summary>Documented stress envelope ceiling.</summary>
        public const float StressMax = 10f;

        /// <summary>Flat award for first-time identification of a weapon family (intel dedup).</summary>
        public const float NewFamilyIntel = 2f;

        /// <summary>Intel award per confirmed kill.</summary>
        public const float IntelPerKill = 1f;

        /// <summary>Intel award per target seen fleeing (displacement direction, rally behavior).</summary>
        public const float IntelPerFleeingTarget = 1.5f;

        [SerializeField] private List<ContactRecord> _records = new List<ContactRecord>();
        [SerializeField] private List<string> _knownFamilies = new List<string>();
        [SerializeField] private int _totalRoundsConsumed;
        [SerializeField] private int _totalRoundsOnTarget;

        /// <summary>All closed contact records, oldest first. Treat as read-only.</summary>
        public IReadOnlyList<ContactRecord> Records => _records.AsReadOnly();

        /// <summary>Rounds budget spent across every recorded contact.</summary>
        public int TotalRoundsConsumed => _totalRoundsConsumed;

        /// <summary>Confirmed hits across every recorded contact.</summary>
        public int TotalRoundsOnTarget => _totalRoundsOnTarget;

        /// <summary>Aggregate hits/rounds; 0 when nothing was fired.</summary>
        public float AggregateAccuracy
        {
            get
            {
                if (_totalRoundsConsumed <= 0) return 0f;
                return (float)_totalRoundsOnTarget / _totalRoundsConsumed;
            }
        }

        /// <summary>Sum of <see cref="ContactRecord.intelValue"/> over every contact.</summary>
        public float TotalIntelValue { get; private set; }

        /// <summary>Sum of <see cref="ContactRecord.stressDelta"/> over every contact; what the director feeds back on the contact report.</summary>
        public float TotalStressDelta { get; private set; }

        /// <summary>Number of contacts closed.</summary>
        public int ContactCount => _records.Count;

        /// <summary>
        /// Validates, rates, and stores one contact. All negative or NaN inputs clamp
        /// (no negative round budgets, no negative durations); hits never exceed rounds fired.
        /// </summary>
        /// <param name="input">Raw tallies from the gunnery/observation layer.</param>
        /// <returns>The stored record.</returns>
        public ContactRecord CloseContact(in ContactReportInput input)
        {
            float distance = input.distanceM;
            if (float.IsNaN(distance) || distance < 0f) distance = 0f;
            int rounds = input.roundsConsumed < 0 ? 0 : input.roundsConsumed;
            int hits = input.roundsOnTarget < 0 ? 0 : input.roundsOnTarget;
            if (hits > rounds) hits = rounds;
            int engaged = input.targetsEngaged < 0 ? 0 : input.targetsEngaged;
            int killed = input.targetsKilled < 0 ? 0 : input.targetsKilled;
            if (killed > engaged) killed = engaged;
            int fled = input.targetsFledCount < 0 ? 0 : input.targetsFledCount;
            if (fled > engaged - killed) fled = engaged - killed;
            float duration = input.contactDurationSeconds;
            if (float.IsNaN(duration) || duration < 0f) duration = 0f;

            DistanceBand band = ClassifyBand(distance);
            float hitRatio = rounds > 0 ? (float)hits / rounds : 0f;
            ContactOutcome outcome = ClassifyOutcome(engaged, killed, fled);
            float suppression = ComputeSuppressionEffectiveness(hitRatio, band, input.baseOfFireSuccess, outcome);

            var record = new ContactRecord
            {
                band = band,
                weaponFamilyId = input.weaponFamilyId ?? string.Empty,
                roundsConsumed = rounds,
                roundsOnTarget = hits,
                hitRatio = hitRatio,
                suppressionEffectiveness = suppression,
                outcome = outcome,
                contactDurationSeconds = duration,
                targetsKilled = killed,
                targetsFled = fled
            };
            record.stressDelta = ComputeStressDelta(in record);
            record.intelValue = ComputeIntelValue(in record, _knownFamilies);

            _records.Add(record);
            _totalRoundsConsumed += rounds;
            _totalRoundsOnTarget += hits;
            TotalStressDelta += record.stressDelta;
            TotalIntelValue += record.intelValue;
            return record;
        }

        /// <summary>Resets all accumulation (new mission / new unit of analysis). Records are discarded.</summary>
        public void Reset()
        {
            _records.Clear();
            _knownFamilies.Clear();
            _totalRoundsConsumed = 0;
            _totalRoundsOnTarget = 0;
            TotalIntelValue = 0f;
            TotalStressDelta = 0f;
        }

        /// <summary>
        /// Morale stress delta implied by a contact: destroying the enemy reassures (+6), breaking
        /// them reassures less (+3), an inconclusive firefight eats confidence (−6), and an enemy
        /// that HELD despite everything is the worst outcome (−18 − up to −8 miss-weight, −4 more
        /// if this was a failed base-of-fire attempt), clamped to [−25, +10].
        /// </summary>
        /// <param name="record">A closed contact record.</param>
        /// <returns>Delta to apply to squad morale (negative = stress).</returns>
        public static float ComputeStressDelta(in ContactRecord record)
        {
            float delta;
            switch (record.outcome)
            {
                case ContactOutcome.Killed: delta = StressOnKill; break;
                case ContactOutcome.Fled: delta = StressOnFlee; break;
                case ContactOutcome.Held: delta = StressOnHold - (1f - record.hitRatio) * HeldContactMissWeight; break;
                default: delta = StressOnInconclusive; break;
            }
            if (record.outcome != ContactOutcome.Killed && record.outcome != ContactOutcome.Fled && record.hitRatio < 0.15f)
            {
                // a base-of-fire / priority-effect attempt that did not break the enemy stings further
                delta -= FailedBaseOfFirePenalty;
            }
            if (delta < StressMin) delta = StressMin;
            if (delta > StressMax) delta = StressMax;
            return delta;
        }

        /// <summary>
        /// Overload taking the raw record by reference for integrator convenience.
        /// </summary>
        /// <param name="record">Closed record instance.</param>
        /// <returns>Morale stress delta.</returns>
        public static float ComputeStressDelta(ContactRecord record)
        {
            return ComputeStressDelta(in record);
        }

        /// <summary>
        /// Intel points earned by a contact (§3.4/§9 tie-in): proximity bonus (Close 3 / Medium 2 /
        /// Long 1 / Far 0.5), +1 per confirmed kill, +1.5 per target observed fleeing, +2 flat the
        /// first time a given weapon family id is identified (deduped across the mission).
        /// </summary>
        /// <param name="record">Closed record instance.</param>
        /// <param name="knownFamilies">Mutable list of already-identified families (pass the reporter's own list).</param>
        /// <returns>Intel point contribution of this contact.</returns>
        public static float ComputeIntelValue(in ContactRecord record, List<string> knownFamilies)
        {
            float intel = BandProximityWeight(record.band);
            if (record.outcome == ContactOutcome.Killed) intel += IntelPerKill * record.targetsKilled;
            if (record.outcome == ContactOutcome.Fled) intel += IntelPerFleeingTarget * record.targetsFled;
            if (!string.IsNullOrEmpty(record.weaponFamilyId))
            {
                bool known = knownFamilies != null && knownFamilies.Contains(record.weaponFamilyId);
                if (!known)
                {
                    intel += NewFamilyIntel;
                    if (knownFamilies != null) knownFamilies.Add(record.weaponFamilyId);
                }
            }
            return intel < 0f ? 0f : intel;
        }

        /// <summary>Convenience overload: intel value without family dedup accounting.</summary>
        /// <param name="report">Closed record instance.</param>
        /// <returns>Intel point contribution.</returns>
        public static float ComputeIntelValue(ContactRecord report)
        {
            return ComputeIntelValue(in report, null);
        }

        /// <summary>Classifies a distance into the four doctrine bands.</summary>
        /// <param name="distanceM">Engagement distance in metres.</param>
        /// <returns>Band containing the distance (negatives treated as Close).</returns>
        public static DistanceBand ClassifyBand(float distanceM)
        {
            if (distanceM < CloseBandMaxM) return DistanceBand.Close;
            if (distanceM < MediumBandMaxM) return DistanceBand.Medium;
            if (distanceM < LongBandMaxM) return DistanceBand.Long;
            return DistanceBand.Far;
        }

        private static ContactOutcome ClassifyOutcome(int engaged, int killed, int fled)
        {
            if (engaged <= 0) return ContactOutcome.Inconclusive;
            if (killed >= engaged) return ContactOutcome.Killed;
            if (killed + fled >= engaged) return fled > 0 ? ContactOutcome.Fled : ContactOutcome.Killed;
            return ContactOutcome.Held;
        }

        private static float ComputeSuppressionEffectiveness(float hitRatio, DistanceBand band, bool baseOfFireSuccess, ContactOutcome outcome)
        {
            float proximity = BandProximityWeight(band);
            float outcomeBoost = (outcome == ContactOutcome.Fled || outcome == ContactOutcome.Killed) ? 1f : (outcome == ContactOutcome.Inconclusive ? 0f : 0.3f);
            float raw = (hitRatio + (baseOfFireSuccess ? 0.25f : 0f)) * proximity + outcomeBoost * 0.2f;
            if (raw < 0f) return 0f;
            return raw > 1f ? 1f : raw;
        }

        private static float BandProximityWeight(DistanceBand band)
        {
            switch (band)
            {
                case DistanceBand.Close: return 3f;
                case DistanceBand.Medium: return 2f;
                case DistanceBand.Long: return 1f;
                default: return 0.5f;
            }
        }
    }
}
