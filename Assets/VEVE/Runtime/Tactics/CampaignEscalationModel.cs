using System;
using System.Globalization;

namespace VEVE.Tactics
{
    /// <summary>
    /// Output posture for the next mission in the campaign graph, per GAMEPLAY_MECHANICS_SPEC.md
    /// §3.4: this mission's outcome mutates enemy alert so "failure raises posture and spills into
    /// the next insertion". Curve semantics are documented on <see cref="CampaignEscalationModel"/>.
    /// All three fields are pre-clamped to their documented envelopes by <c>Compute</c>.
    /// </summary>
    [Serializable]
    public struct PostureDelta
    {
        /// <summary>Enemy patrol density for the next mission, normalized 0.05..1 consumed by the layout/patrol director.</summary>
        public float patrolDensity01;

        /// <summary>Multiplier on the enemy reaction-time baseline for the next mission. &gt; 1 = slower to react (we did well), &lt; 1 = faster (we were compromised). Clamped 0.3..3.0.</summary>
        public float reactionTimeMult;

        /// <summary>Likelihood the next insertion faces an armored/technical response, 0..1.</summary>
        public float armorLikelihood;

        /// <summary>Readable form for brief screens and logs.</summary>
        /// <returns>One-line summary.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "patrol {0:0.###} reaction x{1:0.###} armor {2:0.###}", patrolDensity01, reactionTimeMult, armorLikelihood);
        }
    }

    /// <summary>
    /// Sanitized mission-outcome feed consumed by <see cref="CampaignEscalationModel.Compute"/>.
    /// Every field has a clamping rule: percentages clamp to 0..100, counters and seconds treat
    /// negative/NaN as zero (no negative time budgets), alert levels clamp to 0..4.
    /// </summary>
    [Serializable]
    public struct MissionOutcomeInput
    {
        /// <summary>Percent of the inserting squad killed or MIA, 0..100.</summary>
        public float squadLossesPct;

        /// <summary>Intel points captured this mission (from <see cref="EngagementReporter.TotalIntelValue"/> and objective events), ≥ 0.</summary>
        public float intelCaptured;

        /// <summary>Elapsed mission time in seconds (longer exposure = enemy had time to reorganize), ≥ 0.</summary>
        public float missionTimeSeconds;

        /// <summary>Enemy alert level observed during insertion (0 = oblivious … 4 = waiting in ambush), 0..4.</summary>
        public int alertLevelDuringInsert;

        /// <summary>Civilian collateral events caused this mission (each one burns cover), ≥ 0.</summary>
        public int collateralEvents;

        /// <summary>Debug rendering.</summary>
        /// <returns>One-line summary of the raw outcome.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "loss {0:0.#}% intel {1:0.#} t {2:0}s alert {3} collat {4}", squadLossesPct, intelCaptured, missionTimeSeconds, alertLevelDuringInsert, collateralEvents);
        }
    }

    /// <summary>
    /// Pure, deterministic mission → next-mission escalation model (B4 encounter director, spec §3.4).
    ///
    /// <para>Documented curves — all monotonic non-decreasing in each listed input, closed forms so
    /// they can be tuned and unit-tested in one place:</para>
    /// <list type="bullet">
    /// <item><description>patrolDensity01 = clamp(0.05, 1, 0.10 + 0.35·loss + 0.07·insertAlert +
    /// 0.08·collateral + min(0.25, 0.04·√intel)). Rising losses/alert/collateral AND rising captured
    /// intel all raise next-mission patrol density (compromise logic: the more we take from them the
    /// harder they sweep — order must never invert per the escalation invariant), so the intel term
    /// is additive and order-preserving. A clean ghost mission (zero everything) still leaves 0.10
    /// ambient patrols.</description></item>
    /// <item><description>reactionTimeMult = clamp(0.30, 3.00, 1.50 + 0.50·min(1, missionTime/1800) −
    /// 1.00·loss − 0.15·insertAlert − 0.02·min(12, intelCaptured) − 0.05·collateral). A slow, clean
    /// mission leaves the network confused (up to ×2.0); a bloody compromise pushes toward the ×0.30
    /// floor (reaction three times faster than baseline).</description></item>
    /// <item><description>armorLikelihood = clamp(0, 1, 0.05 + 0.45·loss + 0.10·insertAlert +
    /// 0.10·min(1, missionTime/2400) + 0.12·min(3, collateral)). Armor is the enemy's deliberate
    /// response: it grows with sustained pressure, never with a quick clean raid.</description></item>
    /// </list>
    /// <see cref="ApplyToNextMission"/> adds a deterministic ±10 % fingerprint jitter derived from
    /// FNV-1a over the profile key + seed, so identical (key, seed, outcome) triples always yield
    /// an identical <see cref="PostureDelta"/> and save/reload never reshuffles the campaign.
    /// </summary>
    public static class CampaignEscalationModel
    {
        /// <summary>Floor for patrol density — the biome is never fully evacuated.</summary>
        public const float PatrolDensityMin = 0.05f;

        /// <summary>Ceiling for patrol density.</summary>
        public const float PatrolDensityMax = 1f;

        /// <summary>Reaction multiplier floor (enemy responds 3.3× faster than baseline).</summary>
        public const float ReactionTimeMultMin = 0.3f;

        /// <summary>Reaction multiplier ceiling (enemy responds 1.5× slower — near-paralysis after a ghost op).</summary>
        public const float ReactionTimeMultMax = 3f;

        /// <summary>Armor likelihood floor.</summary>
        public const float ArmorLikelihoodMin = 0f;

        /// <summary>Armor likelihood ceiling.</summary>
        public const float ArmorLikelihoodMax = 1f;

        /// <summary>Maximum alert level representable during insertion.</summary>
        public const int MaxAlertLevel = 4;

        /// <summary>±10 % fingerprint jitter band applied to patrol density and armor likelihood.</summary>
        public const float JitterBand = 0.1f;

        /// <summary>FNV-1a 32-bit offset basis.</summary>
        public const uint FnvOffsetBasis = 2166136261u;

        /// <summary>FNV-1a 32-bit prime.</summary>
        public const uint FnvPrime = 16777619u;

        /// <summary>
        /// Evaluates the raw escalation curves (no key jitter). See the class docs for formulae
        /// and clamps; the result is always inside the documented envelopes.
        /// </summary>
        /// <param name="outcome">Mission outcome feed (fields clamp; NaN-safe).</param>
        /// <returns>Next-mission enemy posture.</returns>
        public static PostureDelta Compute(MissionOutcomeInput outcome)
        {
            float loss = Clamp01Pct(outcome.squadLossesPct) / 100f;
            float intel = ClampNonNegative(outcome.intelCaptured);
            float time = ClampNonNegative(outcome.missionTimeSeconds);
            int alert = outcome.alertLevelDuringInsert < 0 ? 0 : (outcome.alertLevelDuringInsert > MaxAlertLevel ? MaxAlertLevel : outcome.alertLevelDuringInsert);
            int collateral = outcome.collateralEvents < 0 ? 0 : outcome.collateralEvents;

            float patrol = 0.10f
                + 0.35f * loss
                + 0.07f * alert
                + 0.08f * (collateral > 6 ? 6 : collateral)
                + Math.Min(0.25f, 0.04f * (float)Math.Sqrt(intel));
            patrol = ClampRange(patrol, PatrolDensityMin, PatrolDensityMax);

            float reaction = 1.50f
                + 0.50f * Math.Min(1f, time / 1800f)
                - 1.00f * loss
                - 0.15f * alert
                - 0.02f * Math.Min(12f, intel)
                - 0.05f * (collateral > 8 ? 8 : collateral);
            reaction = ClampRange(reaction, ReactionTimeMultMin, ReactionTimeMultMax);

            float armor = 0.05f
                + 0.45f * loss
                + 0.10f * alert
                + 0.10f * Math.Min(1f, time / 2400f)
                + 0.12f * Math.Min(3f, collateral);
            armor = ClampRange(armor, ArmorLikelihoodMin, ArmorLikelihoodMax);

            return new PostureDelta { patrolDensity01 = patrol, reactionTimeMult = reaction, armorLikelihood = armor };
        }

        /// <summary>
        /// Deterministically scales the next-mission posture by a per-profile fingerprint:
        /// FNV-1a(profileKey ⊕ seed) → jitter factor in [0.9, 1.1] applied to patrol density and
        /// armor likelihood (reaction multiplier is left exact — it is a timing curve, not a spawn
        /// count). Same (profileKey, seed, outcome) always produces the same result, and clamps
        /// keep the outputs inside their envelopes. The raw curves' monotonicity survives the
        /// multiplicative jitter because the factor is positive and identical across compared
        /// outcomes for a fixed key+seed.
        /// </summary>
        /// <param name="profileKey">Biome/region campaign key, e.g. "biome.arid_ridge".</param>
        /// <param name="seed">Campaign seed; mixes into the hash so replays branch per save only.</param>
        /// <param name="outcome">This mission's outcome.</param>
        /// <returns>Jittered, clamped next-mission posture.</returns>
        public static PostureDelta ApplyToNextMission(string profileKey, int seed, MissionOutcomeInput outcome)
        {
            PostureDelta base_ = Compute(outcome);
            float j = JitterFactor(profileKey, seed);
            PostureDelta result = base_;
            result.patrolDensity01 = ClampRange(base_.patrolDensity01 * j, PatrolDensityMin, PatrolDensityMax);
            result.armorLikelihood = ClampRange(base_.armorLikelihood * j, ArmorLikelihoodMin, ArmorLikelihoodMax);
            return result;
        }

        /// <summary>
        /// Overload without outcome jitter (pure escalation of the outcome alone; same result as
        /// <see cref="Compute"/>). Kept for callers that store their own fingerprint.
        /// </summary>
        /// <param name="profileKey">Biome/region campaign key (hash-mixed with seed).</param>
        /// <param name="seed">Campaign seed.</param>
        /// <param name="outcome">This mission's outcome.</param>
        /// <returns>Undisturbed raw posture.</returns>
        public static PostureDelta ApplyToNextMission(string profileKey, int seed, MissionOutcomeInput outcome, bool applyJitter)
        {
            return applyJitter ? ApplyToNextMission(profileKey, seed, outcome) : Compute(outcome);
        }

        /// <summary>
        /// Standard FNV-1a 32-bit over the UTF-16 char stream of <paramref name="text"/>, then the
        /// four seed bytes little-endian folded in with the same round function. Public because
        /// the campaign save game re-hashes profile keys at load and tests assert determinism.
        /// </summary>
        /// <param name="text">Key string; null treated as empty.</param>
        /// <param name="seed">Integer seed mixed as four extra FNV rounds.</param>
        /// <returns>32-bit hash; deterministic across platforms (char codes, not locale collation).</returns>
        public static uint Fnv1a(string text, int seed)
        {
            uint hash = FnvOffsetBasis;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    ushort c = text[i];
                    hash = (hash ^ (uint)(c & 0xFF)) * FnvPrime;
                    hash = (hash ^ (uint)(c >> 8)) * FnvPrime;
                }
            }
            hash = (hash ^ (uint)(seed & 0xFF)) * FnvPrime;
            hash = (hash ^ (uint)((seed >> 8) & 0xFF)) * FnvPrime;
            hash = (hash ^ (uint)((seed >> 16) & 0xFF)) * FnvPrime;
            hash = (hash ^ (uint)((seed >> 24) & 0xFF)) * FnvPrime;
            return hash;
        }

        /// <summary>
        /// Unit-fraction fingerprint in [0, 1) from the hash; exposed for tests/debug HUDs.
        /// </summary>
        /// <param name="profileKey">Biome/region campaign key.</param>
        /// <param name="seed">Campaign seed.</param>
        /// <returns>Normalized [0,1) hash fraction.</returns>
        public static float Fingerprint01(string profileKey, int seed)
        {
            uint h = Fnv1a(profileKey, seed);
            return (h & 0x00FFFFFFu) / 16777216f;
        }

        private static float JitterFactor(string profileKey, int seed)
        {
            if (string.IsNullOrEmpty(profileKey)) return 1f;
            // [0.9, 1.1] symmetric band.
            return 1f - JitterBand + Fingerprint01(profileKey, seed) * (2f * JitterBand);
        }

        private static float Clamp01Pct(float v)
        {
            if (float.IsNaN(v) || v < 0f) return 0f;
            return v > 100f ? 100f : v;
        }

        private static float ClampNonNegative(float v)
        {
            return float.IsNaN(v) || v < 0f ? 0f : v;
        }

        private static float ClampRange(float v, float min, float max)
        {
            if (float.IsNaN(v)) return min;
            return v < min ? min : (v > max ? max : v);
        }
    }
}
