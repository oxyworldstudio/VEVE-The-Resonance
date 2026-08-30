using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Operators
{
    /// <summary>
    /// The immutable service record taken at the moment an operator is declared killed in
    /// action under permadeath. Snapshot copy, so later edits to a living profile can never
    /// rewrite history on the memorial wall.
    /// </summary>
    [Serializable]
    public sealed class KilledInActionRecord
    {
        /// <summary>Stable id of the fallen operator at the time of death.</summary>
        public string operatorId;

        /// <summary>Callsign as it appears on the memorial wall.</summary>
        public string callsign;

        /// <summary>Lineage key; successors in the same family inherit the mentorship bonus.</summary>
        public string familyId;

        /// <summary>Primary specialty token, e.g. "Recon".</summary>
        public string specialty;

        /// <summary>Days served at the time of death.</summary>
        public int serviceDays;

        /// <summary>Confirmed eliminations carried by the fallen.</summary>
        public int kills;

        /// <summary>Missions completed by the fallen.</summary>
        public int missionsCompleted;

        /// <summary>Weapons-primarily cause of death, free text for the epitaph (e.g. "sniper, tree line W").</summary>
        public string causeOfDeath;

        /// <summary>Campaign date of the loss formatted "yyyy-MM-dd".</summary>
        public string deathDate;
    }

    /// <summary>
    /// Legacy inheritance values computed from a KIA record and granted to the next operator
    /// of the same family. Every field is pure math over the snapshot; nothing here mutates a
    /// profile by itself—see <see cref="OperatorLegacySystem.ApplyTo"/>.
    /// </summary>
    [Serializable]
    public sealed class LegacyBonusResult
    {
        /// <summary>Ceiling for the commissioning XP grant so no successor skips boot camp.</summary>
        public const int MaxStartingXp = 1200;

        /// <summary>Extra trait slot granted at this many days of the mentor's service.</summary>
        public const int VeteranServiceDayThreshold = 365;

        /// <summary>Starting experience granted to the successor (0..MaxStartingXp).</summary>
        public int startingXp;

        /// <summary>Additional trait slots the successor may spend at commissioning (0..2).</summary>
        public int unlockedTraitSlots;

        /// <summary>Proficiency skill floor [0, <see cref="SpecialtyRules.MaxSkillFloor"/>] for same-specialty successors.</summary>
        public float mentorshipSkillFloor;

        /// <summary>Id of the KIA record this bonus was computed from; empty for founding members.</summary>
        public string sourceRecordId = string.Empty;

        /// <summary>Whether the bonus's mentorship applies to the supplied specialty.</summary>
        /// <param name="specialty">Successor's primary specialty.</param>
        /// <returns>True when a floor applies; the floor itself still requires the successor's specialty check.</returns>
        public bool HasMentorship(OperatorSpecialty specialty)
        {
            return mentorshipSkillFloor > 0f;
        }
    }

    /// <summary>
    /// JsonUtility-friendly container persisted alongside the campaign save: the ordered list
    /// of lost operators plus memorial epitaph lines. Fields are plain serializable types only,
    /// mirroring the existing SaveData style (flat lists, string dates, no polymorphism).
    /// </summary>
    [Serializable]
    public sealed class LegacyRoster
    {
        /// <summary>Version stamp written into saves for future migration.</summary>
        public int version = 1;

        /// <summary>KIA snapshots in the order recorded.</summary>
        public List<KilledInActionRecord> records = new List<KilledInActionRecord>();

        /// <summary>Pre-formatted memorial lines for the campaign wall, newest last.</summary>
        public List<string> memorialEntries = new List<string>();
    }

    /// <summary>
    /// Record-keeping and inheritance for permadeath losses. A plain class—no MonoBehaviour,
    /// no static state—so the campaign layer owns its lifetime and the save system can
    /// round-trip its roster through plain strings via <see cref="ToSaveString"/> and
    /// <see cref="FromSaveString"/>. Bonuses are applied to an explicit replacement passed in
    /// by the caller and always return a modified <b>copy</b>, never mutating either the
    /// archived record or the caller's in-hand profile (struct-mutation-trap insurance for
    /// callers that later wrap this in a value type field).
    /// </summary>
    public sealed class OperatorLegacySystem
    {
        /// <summary>Experience granted per day of the mentor's service, before capping.</summary>
        public const int XpPerServiceDay = 12;

        /// <summary>Experience granted per mission the mentor completed, before capping.</summary>
        public const int XpPerMission = 60;

        /// <summary>Experience granted per confirmed elimination credited to the mentor, before capping.</summary>
        public const int XpPerKill = 8;

        private readonly LegacyRoster roster;

        /// <summary>
        /// Creates an empty legacy ledger.
        /// </summary>
        public OperatorLegacySystem()
        {
            roster = new LegacyRoster();
        }

        /// <summary>
        /// Read-only view of the persisted ledger for UI binding (memorial screen).
        /// </summary>
        public LegacyRoster Roster
        {
            get { return roster; }
        }

        /// <summary>
        /// Number of losses recorded in this ledger.
        /// </summary>
        public int LossCount
        {
            get { return roster.records.Count; }
        }

        /// <summary>
        /// Snapshots a fallen operator into the ledger and writes the memorial line. The
        /// incoming profile is read, never written to; call the returned record's familyId when
        /// commissioning the successor so <see cref="ComputeLegacyBonus"/> can find the mentor.
        /// </summary>
        /// <param name="fallen">The operator who died; null rejected.</param>
        /// <param name="causeOfDeath">Short weapons-primary cause text for the epitaph.</param>
        /// <param name="campaignDate">Campaign date of the loss; formatted as yyyy-MM-dd when parsable, else kept verbatim.</param>
        /// <param name="missionsCompletedOverride">Override the profile's mission tally (e.g. campaign-adjusted value); negative uses the profile value.</param>
        /// <returns>The stored KIA record.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fallen"/> is null.</exception>
        public KilledInActionRecord RecordKia(OperatorProfile fallen, string causeOfDeath, DateTime campaignDate, int missionsCompletedOverride = -1)
        {
            if (fallen == null)
            {
                throw new ArgumentException("Cannot record a KIA without the fallen operator's profile.", nameof(fallen));
            }

            var record = new KilledInActionRecord
            {
                operatorId = fallen.operatorId,
                callsign = fallen.callsign,
                familyId = string.IsNullOrEmpty(fallen.familyId) ? fallen.operatorId : fallen.familyId,
                specialty = fallen.defaultSpecialty.ToString(),
                serviceDays = fallen.serviceDays < 0 ? 0 : fallen.serviceDays,
                kills = fallen.confirmedKills < 0 ? 0 : fallen.confirmedKills,
                missionsCompleted = missionsCompletedOverride >= 0 ? missionsCompletedOverride : (fallen.missionsSurvived < 0 ? 0 : fallen.missionsSurvived),
                causeOfDeath = string.IsNullOrEmpty(causeOfDeath) ? "unspecified" : causeOfDeath.Trim(),
                deathDate = campaignDate.ToString("yyyy-MM-dd")
            };

            roster.records.Add(record);
            roster.memorialEntries.Add(BuildMemorialLine(record));
            return record;
        }

        /// <summary>
        /// Pure legacy inheritance math over a KIA record: XP grant f(serviceDays, missions,
        /// kills) bounded to [0, <see cref="LegacyBonusResult.MaxStartingXp"/>], one trait slot
        /// per veteran-length mentorship, and a same-specialty skill floor computed by
        /// <see cref="SpecialtyRules.MentorshipSkillFloor"/>.
        /// </summary>
        /// <param name="record">The archived loss; null yields a zero bonus.</param>
        /// <returns>Bonus values ready to hand to <see cref="ApplyTo"/>.</returns>
        public static LegacyBonusResult ComputeLegacyBonus(KilledInActionRecord record)
        {
            var bonus = new LegacyBonusResult();
            if (record == null)
            {
                return bonus;
            }

            int days = record.serviceDays < 0 ? 0 : record.serviceDays;
            int missions = record.missionsCompleted < 0 ? 0 : record.missionsCompleted;
            int kills = record.kills < 0 ? 0 : record.kills;

            long xp = (long)days * XpPerServiceDay + (long)missions * XpPerMission + (long)kills * XpPerKill;
            if (xp > LegacyBonusResult.MaxStartingXp) xp = LegacyBonusResult.MaxStartingXp;
            bonus.startingXp = (int)xp;
            bonus.unlockedTraitSlots = days >= LegacyBonusResult.VeteranServiceDayThreshold ? 1 : 0;
            bonus.mentorshipSkillFloor = SpecialtyRules.MentorshipSkillFloor(days, missions);
            bonus.sourceRecordId = record.operatorId;
            return bonus;
        }

        /// <summary>
        /// Convenience overload: derives a KIA snapshot from the living profile and computes
        /// the bonus in one step without touching the ledger.
        /// </summary>
        /// <param name="kiaProfile">The fallen operator's profile; null yields a zero bonus.</param>
        /// <returns>Bonus values; see <see cref="ComputeLegacyBonus(KilledInActionRecord)"/>.</returns>
        public static LegacyBonusResult ComputeLegacyBonus(OperatorProfile kiaProfile)
        {
            if (kiaProfile == null)
            {
                return new LegacyBonusResult();
            }
            return ComputeLegacyBonus(SnapshotFrom(kiaProfile));
        }

        /// <summary>
        /// Returns a modified copy of <paramref name="replacement"/> carrying the legacy grant;
        /// both inputs are left untouched. The mentorship floor only applies when the successor
        /// trains in the mentor's specialty (mentorship does not transfer across job families).
        /// </summary>
        /// <param name="replacement">Incoming operator about to be commissioned; null returns null.</param>
        /// <param name="bonus">Values from <see cref="ComputeLegacyBonus"/>; null treats as zero grant.</param>
        /// <param name="mentorSpecialty">Primary specialty of the mentor, for the job-family gate.</param>
        /// <returns>New profile instance with inheritance fields written onto the copy.</returns>
        public static OperatorProfile ApplyTo(OperatorProfile replacement, LegacyBonusResult bonus, OperatorSpecialty mentorSpecialty)
        {
            if (replacement == null)
            {
                return null;
            }
            OperatorProfile applied = replacement.Clone();
            if (bonus == null)
            {
                return applied;
            }

            applied.startingXpGrant = bonus.startingXp < 0 ? 0 : bonus.startingXp;
            applied.bonusTraitSlots = bonus.unlockedTraitSlots < 0 ? 0 : bonus.unlockedTraitSlots;
            if (applied.defaultSpecialty == mentorSpecialty)
            {
                applied.mentorshipSkillFloor = bonus.mentorshipSkillFloor;
            }
            return applied;
        }

        /// <summary>
        /// End-to-end successor commissioning: looks up the mentor by family id in the ledger,
        /// computes the bonus, and returns a modified copy of the replacement. Returns an
        /// unmodified copy when the family has no archived losses.
        /// </summary>
        /// <param name="replacement">Incoming operator; must have <c>familyId</c> set to the mentor's family to inherit.</param>
        /// <returns>New profile carrying whatever the family's last loss entitles.</returns>
        public OperatorProfile CommissionSuccessor(OperatorProfile replacement)
        {
            if (replacement == null)
            {
                return null;
            }
            OperatorProfile successor = replacement.Clone();
            for (int i = roster.records.Count - 1; i >= 0; i--)
            {
                KilledInActionRecord record = roster.records[i];
                if (string.Equals(record.familyId, successor.familyId, StringComparison.Ordinal))
                {
                    LegacyBonusResult bonus = ComputeLegacyBonus(record);
                    return ApplyTo(successor, bonus, ParseSpecialty(record.specialty));
                }
            }
            return successor;
        }

        /// <summary>
        /// Memorial line formatter; stable so tests and UI previews agree.
        /// </summary>
        /// <param name="record">Archived loss.</param>
        /// <returns>One-line epitaph: callsign, service, loss date, cause.</returns>
        public static string BuildMemorialLine(KilledInActionRecord record)
        {
            if (record == null)
            {
                return "KIA - unknown.";
            }
            return "KIA - " + record.callsign + " (" + record.specialty + "), "
                   + record.serviceDays + " days, " + record.missionsCompleted + " missions. Lost "
                   + record.deathDate + ": " + record.causeOfDeath + ".";
        }

        /// <summary>
        /// Serializes the ledger for the save system, matching the JsonUtility-first pattern
        /// used by VEVE.Mission.SaveSystem.
        /// </summary>
        /// <returns>Indented JSON string of the whole ledger.</returns>
        public string ToSaveString()
        {
            return JsonUtility.ToJson(roster, true);
        }

        /// <summary>
        /// Replaces the in-memory ledger contents from a save string produced by
        /// <see cref="ToSaveString"/>. Null/empty/invalid input resets to an empty ledger and
        /// returns false so the caller can decide whether to warn the player.
        /// </summary>
        /// <param name="json">JSON text.</param>
        /// <returns>Whether the load populated a ledger.</returns>
        public bool LoadFromString(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                roster.records.Clear();
                roster.memorialEntries.Clear();
                return false;
            }

            LegacyRoster loaded = JsonUtility.FromJson<LegacyRoster>(json);
            if (loaded == null || loaded.records == null)
            {
                roster.records.Clear();
                roster.memorialEntries.Clear();
                return false;
            }

            roster.records = loaded.records;
            roster.memorialEntries = loaded.memorialEntries ?? new List<string>();
            return true;
        }

        /// <summary>
        /// Builds a fresh ledger object from a save string without touching live state.
        /// </summary>
        /// <param name="json">JSON text; null/empty/invalid yields an empty ledger.</param>
        /// <returns>New <see cref="OperatorLegacySystem"/> seeded with the loaded records.</returns>
        public static OperatorLegacySystem FromSaveString(string json)
        {
            var system = new OperatorLegacySystem();
            system.LoadFromString(json);
            return system;
        }

        /// <summary>
        /// Looks up the most recent archived loss for a family line.
        /// </summary>
        /// <param name="familyId">Lineage key to search.</param>
        /// <returns>The newest matching record or null when the family is unbroken.</returns>
        public KilledInActionRecord FindLatestLoss(string familyId)
        {
            if (string.IsNullOrEmpty(familyId))
            {
                return null;
            }
            for (int i = roster.records.Count - 1; i >= 0; i--)
            {
                if (string.Equals(roster.records[i].familyId, familyId, StringComparison.Ordinal))
                {
                    return roster.records[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Lenient specialty token parse used when restoring archived records; unknown tokens
        /// fall back to <see cref="OperatorSpecialty.Pointman"/> rather than throwing, because a
        /// corrupt save should degrade, not crash.
        /// </summary>
        /// <param name="token">Specialty name as stored.</param>
        /// <returns>Parsed specialty.</returns>
        public static OperatorSpecialty ParseSpecialty(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return OperatorSpecialty.Pointman;
            }
            try
            {
                return (OperatorSpecialty)Enum.Parse(typeof(OperatorSpecialty), token, true);
            }
            catch (ArgumentException)
            {
                return OperatorSpecialty.Pointman;
            }
        }

        private static KilledInActionRecord SnapshotFrom(OperatorProfile profile)
        {
            return new KilledInActionRecord
            {
                operatorId = profile.operatorId,
                callsign = profile.callsign,
                familyId = string.IsNullOrEmpty(profile.familyId) ? profile.operatorId : profile.familyId,
                specialty = profile.defaultSpecialty.ToString(),
                serviceDays = profile.serviceDays < 0 ? 0 : profile.serviceDays,
                kills = profile.confirmedKills < 0 ? 0 : profile.confirmedKills,
                missionsCompleted = profile.missionsSurvived < 0 ? 0 : profile.missionsSurvived,
                causeOfDeath = "unspecified",
                deathDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };
        }
    }
}
