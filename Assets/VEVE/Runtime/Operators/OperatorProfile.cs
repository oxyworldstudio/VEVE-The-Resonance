using System;
using System.Collections.Generic;

namespace VEVE.Operators
{
    /// <summary>
    /// Full save-safe identity of a campaign operator: stable id, callsign, origin region,
    /// specialty pair, equipped traits with progression-gated unlock, service record, voice
    /// kit binding, biometric defaults derived by pure math, and the inheritance fields the
    /// legacy system writes onto a replacement operator. Plain serializable class so
    /// JsonUtility, content pipelines, and EditMode tests can all consume it without a scene.
    /// String IDs are hashed with a process-stable FNV-1a (same construction as
    /// VEVE.Procedural.EnvironmentContextProfile.StableStringHash) so ids never depend on
    /// System.String.GetHashCode, which is randomized per runtime.
    /// </summary>
    [Serializable]
    public sealed class OperatorProfile
    {
        /// <summary>Stable cross-save identifier, "op.&lt;callsign&gt;.&lt;fnv1a&gt;", derived from the callsign only.</summary>
        public string operatorId;

        /// <summary>Display callsign, unique within a roster generation.</summary>
        public string callsign;

        /// <summary>
        /// Lineage key shared by mentors and successors; the legacy system grants a replacement
        /// its bonus only when <c>familyId</c> matches the fallen operator's family.
        /// </summary>
        public string familyId;

        /// <summary>
        /// Origin region semantic key, exactly matching the "region.&lt;token&gt;" keys produced by
        /// VEVE.Procedural.EnvironmentContextProfile.GetSemanticKeys (e.g. "region.subarcticcompound").
        /// </summary>
        public string originRegionKey;

        /// <summary>Primary specialty; drives role assignment, voice kit barks, and proficiency default.</summary>
        public OperatorSpecialty defaultSpecialty = OperatorSpecialty.Pointman;

        /// <summary>Cross-trained second specialty, only honored when <see cref="alternativeUnlocked"/> is true.</summary>
        public OperatorSpecialty alternativeSpecialty = OperatorSpecialty.Recon;

        /// <summary>Whether the cross-trained secondary has been earned through progression.</summary>
        public bool alternativeUnlocked;

        /// <summary>Equipped trait selection; aggregated by <see cref="OperatorTraits"/> into performance channels.</summary>
        public TraitSet traits = new TraitSet();

        /// <summary>Age at last medical board review, in years; feeds max heart-rate math.</summary>
        public int ageYears = 27;

        /// <summary>Completed years of service.</summary>
        public int serviceYears = 4;

        /// <summary>Total days served; accumulated by the campaign layer and consumed by legacy bonuses.</summary>
        public int serviceDays = 1460;

        /// <summary>Count of missions survived; used for epitaphs and mentorship floors.</summary>
        public int missionsSurvived;

        /// <summary>Count of confirmed eliminations; carries into the KIA record for legacy math.</summary>
        public int confirmedKills;

        /// <summary>Voice kit identifier bound at creation, e.g. "voice.kit.regional_r".</summary>
        public string voiceKitId;

        /// <summary>Resting heart rate in BPM; stays within the medical healthy band for the species.</summary>
        public float restingHeartRateBpm = 64f;

        /// <summary>Estimated maximal heart rate in BPM for this age; classic Fox table, refined by Tanaka below.</summary>
        public float maxHeartRateBpm = 190f;

        /// <summary>Experience granted at commissioning by legacy mentorship; 0 for founding members.</summary>
        public int startingXpGrant;

        /// <summary>Extra trait slots earned as inheritance; the roster UI adds these to the base allotment.</summary>
        public int bonusTraitSlots;

        /// <summary>Mentorship skill floor in [0, 1] applied to the primary proficiency skill of a successor.</summary>
        public float mentorshipSkillFloor;

        /// <summary>
        /// Healthy resting heart-rate band used by all biometric math in this namespace. The
        /// upper bound deliberately sits below RealismConfig's 65 BPM soft baseline plus the
        /// medical tachycardia threshold, and the lower bound below the bradycardia line so
        /// generated defaults always read as fit-duty personnel.
        /// </summary>
        public const float MedicalRestingMinBpm = 40f;

        /// <summary>Upper medical bound for a resting heart rate still classifiable as duty-fit.</summary>
        public const float MedicalRestingMaxBpm = 90f;

        /// <summary>Baseline resting BPM mirroring RealismConfig's physiology default of 65.</summary>
        public const float BaselineRestingBpm = 65f;

        /// <summary>
        /// Canonical origin-region keys, kept in sync with the EnvironmentContextProfile
        /// semantic region tokens ("region." + lowercase region name). New theaters must be
        /// listed here before rosters may reference them.
        /// </summary>
        public static readonly string[] RegionKeys =
        {
            "region.mediterraneantown",
            "region.easterneuropeanindustrial",
            "region.desertcheckpoint",
            "region.subarcticcompound",
            "region.temperateforestvillage"
        };

        /// <summary>
        /// Deterministic process-stable FNV-1a 32-bit hash, matching the algorithm and
        /// convention of EnvironmentContextProfile.StableStringHash so ids survive domain
        /// reloads, platform changes, and save-file comparisons between systems.
        /// </summary>
        /// <param name="value">Input; null or empty yields 0 by convention.</param>
        /// <returns>Stable signed 32-bit hash.</returns>
        public static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 16777619) ^ value[i];
                }
                return hash;
            }
        }

        /// <summary>
        /// Builds the stable operator id for a callsign: "op.&lt;lowercase callsign&gt;.&lt;8-hex fnv&gt;".
        /// Same input always yields the same string across calls, sessions, and platforms.
        /// </summary>
        /// <param name="callsignValue">Callsign text; trimmed and lowercased internally.</param>
        /// <returns>Stable id; "op.unnamed.00000000" for a blank callsign.</returns>
        public static string ComputeStableId(string callsignValue)
        {
            string normalized = (callsignValue ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0)
            {
                return "op.unnamed.00000000";
            }
            unchecked
            {
                return "op." + normalized + "." + ((uint)StableHash(normalized)).ToString("x8");
            }
        }

        /// <summary>
        /// Pure biometric math: Tanaka maximal heart rate, HRmax = 208 - 0.7 * age.
        /// Age is clamped to the plausible service envelope [16, 60] before evaluation.
        /// </summary>
        /// <param name="ageYears">Operator age.</param>
        /// <returns>Estimated HRmax in BPM, always above <see cref="MedicalRestingMinBpm"/>.</returns>
        public static float ComputeMaxHeartRate(int ageYears)
        {
            int age = ageYears < 16 ? 16 : (ageYears > 60 ? 60 : ageYears);
            // Tanaka et al. 2001: HRmax = 208 - 0.7 * age
            return 208f - (0.7f * age);
        }

        /// <summary>
        /// Pure biometric math: deterministic resting heart rate inside the medical duty band.
        /// Starts from <see cref="BaselineRestingBpm"/> (RealismConfig's physiology default),
        /// shifts by trait stress resilience (SteelLungs, ColdNerve, AdrenalineJunkie, Flatline),
        /// drifts by age, and folds in a tiny deterministic hash jitter from the operator id so
        /// identical archetypes still read as individuals. Result clamped to
        /// [<see cref="MedicalRestingMinBpm"/>, <see cref="MedicalRestingMaxBpm"/>].
        /// </summary>
        /// <param name="ageYears">Operator age.</param>
        /// <param name="traits">Equipped traits (null treated as empty).</param>
        /// <param name="operatorIdValue">Stable id used for deterministic ±2 BPM jitter.</param>
        /// <returns>Resting heart rate in BPM within the medical band.</returns>
        public static float ComputeRestingHeartRate(int ageYears, TraitSet traits, string operatorIdValue)
        {
            float rest = BaselineRestingBpm;
            int age = ageYears < 16 ? 16 : (ageYears > 60 ? 60 : ageYears);
            rest += (age - 27) * 0.35f;
            if (traits != null)
            {
                if (traits.Contains(OperatorTraitId.SteelLungs)) rest -= 4f;
                if (traits.Contains(OperatorTraitId.ColdNerve)) rest -= 2f;
                if (traits.Contains(OperatorTraitId.NightOwl)) rest -= 1f;
                if (traits.Contains(OperatorTraitId.AdrenalineJunkie)) rest += 5f;
                if (traits.Contains(OperatorTraitId.Flatline)) rest += 3f;
                if (traits.Contains(OperatorTraitId.Clumsy)) rest += 4f;
            }
            int jitter = StableHash(operatorIdValue) % 20;
            rest += jitter * 0.2f; // -3.8 to +3.8 BPM deterministic individuality
            if (rest < MedicalRestingMinBpm) rest = MedicalRestingMinBpm;
            if (rest > MedicalRestingMaxBpm) rest = MedicalRestingMaxBpm;
            return rest;
        }

        /// <summary>
        /// Whether the trait may currently be equipped given progression level: level must meet
        /// the catalog threshold or the slot must already hold the trait.
        /// </summary>
        /// <param name="traitId">Trait to probe.</param>
        /// <param name="progressionLevel">Operator's current campaign progression level (1+).</param>
        /// <returns>Whether equipping is legal.</returns>
        public bool CanEquipTrait(OperatorTraitId traitId, int progressionLevel)
        {
            if (!TraitCatalog.IsDefined(traitId))
            {
                return false;
            }
            return traits.Contains(traitId) || progressionLevel >= TraitCatalog.UnlockLevel(traitId);
        }

        /// <summary>
        /// Traits the progression level can start selecting, for the roster UI's available list.
        /// </summary>
        /// <param name="progressionLevel">Current progression level (1+).</param>
        /// <returns>Catalog traits at or below the level, not yet equipped.</returns>
        public List<OperatorTraitId> UnlockableTraits(int progressionLevel)
        {
            var output = new List<OperatorTraitId>();
            foreach (TraitDefinition definition in TraitCatalog.AllDefinitions())
            {
                if (definition.unlockLevel <= progressionLevel && !traits.Contains(definition.id))
                {
                    output.Add(definition.id);
                }
            }
            return output;
        }

        /// <summary>
        /// Normalizes blank fields, re-derives the stable id, and clamps biometrics and service
        /// counters. Idempotent; safe to call after any deserialization.
        /// </summary>
        public void Normalize()
        {
            callsign = (callsign ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(familyId))
            {
                familyId = "family." + callsign.ToLowerInvariant();
            }
            operatorId = ComputeStableId(callsign);
            if (string.IsNullOrEmpty(originRegionKey) || !IsKnownRegionKey(originRegionKey))
            {
                originRegionKey = "region.mediterraneantown";
            }
            if (string.IsNullOrEmpty(voiceKitId))
            {
                voiceKitId = "voice.kit.unassigned";
            }
            if (traits == null)
            {
                traits = new TraitSet();
            }
            if (ageYears < 16) ageYears = 16;
            if (ageYears > 60) ageYears = 60;
            if (serviceYears < 0) serviceYears = 0;
            if (serviceDays < 0) serviceDays = 0;
            if (missionsSurvived < 0) missionsSurvived = 0;
            if (confirmedKills < 0) confirmedKills = 0;
            if (startingXpGrant < 0) startingXpGrant = 0;
            if (bonusTraitSlots < 0) bonusTraitSlots = 0;
            if (mentorshipSkillFloor < 0f) mentorshipSkillFloor = 0f;
            if (mentorshipSkillFloor > SpecialtyRules.MaxSkillFloor) mentorshipSkillFloor = SpecialtyRules.MaxSkillFloor;
            maxHeartRateBpm = ComputeMaxHeartRate(ageYears);
            restingHeartRateBpm = ComputeRestingHeartRate(ageYears, traits, operatorId);
            if (restingHeartRateBpm > maxHeartRateBpm)
            {
                restingHeartRateBpm = maxHeartRateBpm - 1f;
            }
        }

        /// <summary>
        /// Collects non-fatal identity problems for authoring and save migration audits.
        /// </summary>
        /// <returns>Human-readable list; empty when the profile is clean.</returns>
        public List<string> CollectWarnings()
        {
            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(callsign))
            {
                warnings.Add("Operator has no callsign; roster generation would collide.");
            }
            if (string.IsNullOrEmpty(operatorId) || operatorId != ComputeStableId(callsign))
            {
                warnings.Add("operatorId does not match the callsign-derived stable id.");
            }
            if (!IsKnownRegionKey(originRegionKey))
            {
                warnings.Add("originRegionKey " + originRegionKey + " is not a canonical region key.");
            }
            if (traits != null)
            {
                foreach (OperatorTraitId traitId in traits.traitIds)
                {
                    if (!TraitCatalog.IsDefined(traitId))
                    {
                        warnings.Add("Equipped trait " + traitId + " is not in the catalog.");
                    }
                }
            }
            if (restingHeartRateBpm < MedicalRestingMinBpm || restingHeartRateBpm > MedicalRestingMaxBpm)
            {
                warnings.Add("Resting heart rate " + restingHeartRateBpm + " outside medical band.");
            }
            return warnings;
        }

        /// <summary>
        /// Whether a string is one of the canonical origin-region keys.
        /// </summary>
        /// <param name="key">Candidate key.</param>
        /// <returns>Membership; null is false.</returns>
        public static bool IsKnownRegionKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            foreach (string regionKey in RegionKeys)
            {
                if (string.Equals(regionKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Deep copy used by the legacy system so bonuses can be applied to a replacement
        /// without ever mutating the archived profile of the fallen.
        /// </summary>
        /// <returns>Independent profile with the same identity fields.</returns>
        public OperatorProfile Clone()
        {
            return new OperatorProfile
            {
                operatorId = operatorId,
                callsign = callsign,
                familyId = familyId,
                originRegionKey = originRegionKey,
                defaultSpecialty = defaultSpecialty,
                alternativeSpecialty = alternativeSpecialty,
                alternativeUnlocked = alternativeUnlocked,
                traits = traits == null ? new TraitSet() : traits.Clone(),
                ageYears = ageYears,
                serviceYears = serviceYears,
                serviceDays = serviceDays,
                missionsSurvived = missionsSurvived,
                confirmedKills = confirmedKills,
                voiceKitId = voiceKitId,
                restingHeartRateBpm = restingHeartRateBpm,
                maxHeartRateBpm = maxHeartRateBpm,
                startingXpGrant = startingXpGrant,
                bonusTraitSlots = bonusTraitSlots,
                mentorshipSkillFloor = mentorshipSkillFloor
            };
        }

        /// <summary>
        /// Builds one profile with derived biometric fields filled in. All roster members and
        /// successors route through here so the resting/max heart-rate math stays single-sourced.
        /// </summary>
        /// <param name="callsignValue">Callsign; trimmed, used for the stable id.</param>
        /// <param name="specialty">Primary specialty.</param>
        /// <param name="regionKey">Origin region semantic key; falls back to Mediterranean town.</param>
        /// <param name="age">Age in years, clamped to the service envelope.</param>
        /// <param name="serviceYearsValue">Completed years of service.</param>
        /// <param name="traitSelection">Traits to equip; unknown ids are skipped.</param>
        /// <returns>Normalized, warning-free profile.</returns>
        public static OperatorProfile Create(string callsignValue, OperatorSpecialty specialty, string regionKey, int age, int serviceYearsValue, params OperatorTraitId[] traitSelection)
        {
            var profile = new OperatorProfile
            {
                callsign = (callsignValue ?? string.Empty).Trim(),
                defaultSpecialty = specialty,
                originRegionKey = regionKey,
                ageYears = age,
                serviceYears = serviceYearsValue,
                serviceDays = serviceYearsValue * 365,
                voiceKitId = "voice.kit." + specialty.ToString().ToLowerInvariant()
            };
            if (traitSelection != null)
            {
                foreach (OperatorTraitId traitId in traitSelection)
                {
                    profile.traits.Add(traitId);
                }
            }
            profile.Normalize();
            return profile;
        }

        /// <summary>
        /// The twelve founding roster members of the campaign: diverse specialties (every
        /// <see cref="OperatorSpecialty"/> appears at least once), distinctive callsigns, and a
        /// spread of traits whose combinations exercise the counterweight design (Adrenaline
        /// Junkie vs Cold Nerve, Night Owl vs Flatline). Entirely authored data, so the list is
        /// identical every run with no RNG involved; ids are callsign-hash stable.
        /// </summary>
        /// <returns>Newly built list of twelve normalized profiles, in canonical order.</returns>
        public static List<OperatorProfile> CreateDefaultRoster()
        {
            var roster = new List<OperatorProfile>(SpecialtyRules.SpecialtyCount + 4)
            {
                Create("Raven", OperatorSpecialty.Recon, "region.temperateforestvillage", 31, 9,
                    OperatorTraitId.Scout, OperatorTraitId.NightOwl, OperatorTraitId.EagleEyed),
                Create("Bishop", OperatorSpecialty.Breacher, "region.mediterraneantown", 28, 6,
                    OperatorTraitId.HeavyHitter, OperatorTraitId.ColdNerve, OperatorTraitId.FastDraw),
                Create("Anvil", OperatorSpecialty.SupportGunner, "region.easterneuropeanindustrial", 35, 13,
                    OperatorTraitId.HeavyHitter, OperatorTraitId.SteelLungs, OperatorTraitId.Flatline),
                Create("Kestrel", OperatorSpecialty.Marksman, "region.subarcticcompound", 26, 4,
                    OperatorTraitId.EagleEyed, OperatorTraitId.SteadyHands, OperatorTraitId.ColdNerve),
                Create("Wraith", OperatorSpecialty.Demolitions, "region.desertcheckpoint", 30, 8,
                    OperatorTraitId.DemolitionSafe, OperatorTraitId.NightOwl, OperatorTraitId.Scout),
                Create("Torch", OperatorSpecialty.Comms, "region.temperateforestvillage", 24, 3,
                    OperatorTraitId.CommsDiscipline, OperatorTraitId.FastDraw, OperatorTraitId.EagleEyed),
                Create("Suture", OperatorSpecialty.Medic, "region.mediterraneantown", 33, 10,
                    OperatorTraitId.GentleHands, OperatorTraitId.ColdNerve, OperatorTraitId.SteelLungs),
                Create("Vanguard", OperatorSpecialty.Pointman, "region.easterneuropeanindustrial", 23, 2,
                    OperatorTraitId.AdrenalineJunkie, OperatorTraitId.Scout, OperatorTraitId.FastDraw),
                Create("Osprey", OperatorSpecialty.Recon, "region.subarcticcompound", 29, 7,
                    OperatorTraitId.EagleEyed, OperatorTraitId.Scout, OperatorTraitId.ColdNerve),
                Create("Slate", OperatorSpecialty.Breacher, "region.desertcheckpoint", 32, 11,
                    OperatorTraitId.SteadyHands, OperatorTraitId.DemolitionSafe, OperatorTraitId.Clumsy),
                Create("Ember", OperatorSpecialty.Demolitions, "region.temperateforestvillage", 25, 3,
                    OperatorTraitId.AdrenalineJunkie, OperatorTraitId.DemolitionSafe, OperatorTraitId.Flatline),
                Create("Lark", OperatorSpecialty.Medic, "region.mediterraneantown", 27, 5,
                    OperatorTraitId.GentleHands, OperatorTraitId.SteelLungs, OperatorTraitId.NightOwl)
            };

            roster[0].alternativeSpecialty = OperatorSpecialty.Marksman;
            roster[0].alternativeUnlocked = true;
            roster[3].alternativeSpecialty = OperatorSpecialty.Recon;
            roster[3].alternativeUnlocked = true;
            roster[7].alternativeSpecialty = OperatorSpecialty.Breacher;
            roster[7].alternativeUnlocked = true;
            roster[9].alternativeSpecialty = OperatorSpecialty.Pointman;
            roster[9].alternativeUnlocked = false;
            roster[10].alternativeSpecialty = OperatorSpecialty.Breacher;
            roster[10].alternativeUnlocked = false;
            roster[11].alternativeSpecialty = OperatorSpecialty.Comms;
            roster[11].alternativeUnlocked = true;

            return roster;
        }
    }
}
