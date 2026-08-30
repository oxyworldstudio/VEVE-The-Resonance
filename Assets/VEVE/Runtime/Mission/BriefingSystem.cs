using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Mission
{
    /// <summary>
    /// Severity level of a briefing warning.
    /// </summary>
    public enum WarningSeverity { Info, Caution, Danger }

    /// <summary>
    /// A warning displayed during mission briefing.
    /// </summary>
    [Serializable]
    public sealed class BriefingWarning
    {
        /// <summary>
        /// Warning text displayed to the player.
        /// </summary>
        public string text;

        /// <summary>
        /// Severity level affecting visual presentation.
        /// </summary>
        public WarningSeverity severity;

        /// <summary>
        /// Duration in seconds the warning remains visible.
        /// </summary>
        public float duration;
    }

    /// <summary>
    /// A piece of intelligence presented during briefing.
    /// </summary>
    [Serializable]
    public sealed class BriefingIntel
    {
        /// <summary>
        /// Header or title for the intel entry.
        /// </summary>
        public string header;

        /// <summary>
        /// Body text of the intel entry.
        /// </summary>
        public string text;

        /// <summary>
        /// Path or filename of an associated image asset.
        /// </summary>
        public string imagePath;

        /// <summary>
        /// World position of the map marker, if applicable.
        /// </summary>
        public Vector3 mapMarker;

        /// <summary>
        /// Indicates whether the intel is classified and requires clearance.
        /// </summary>
        public bool isClassified;
    }

    /// <summary>
    /// Restrictions on loadout selection for the mission.
    /// </summary>
    [Serializable]
    public sealed class LoadoutRestriction
    {
        /// <summary>
        /// Whitelist of allowed weapon IDs. Empty means all weapons allowed.
        /// </summary>
        public List<string> allowedWeapons;

        /// <summary>
        /// Whitelist of allowed attachment IDs. Empty means all attachments allowed.
        /// </summary>
        public List<string> allowedAttachments;

        /// <summary>
        /// Maximum allowed loadout weight. Zero means no limit.
        /// </summary>
        public float maxWeight;

        /// <summary>
        /// IDs of gear items that must be equipped.
        /// </summary>
        public List<string> requiredGear;

        /// <summary>
        /// IDs of gear items that are prohibited.
        /// </summary>
        public List<string> prohibitedGear;

        /// <summary>
        /// Determines whether a given weapon ID is permitted.
        /// </summary>
        /// <param name="weaponId">The weapon ID to check.</param>
        /// <returns>True if the weapon is allowed; otherwise false.</returns>
        public bool IsWeaponAllowed(string weaponId)
        {
            if (allowedWeapons == null || allowedWeapons.Count == 0)
            {
                return true;
            }
            return allowedWeapons.Contains(weaponId);
        }

        /// <summary>
        /// Determines whether a given attachment ID is permitted.
        /// </summary>
        /// <param name="attachmentId">The attachment ID to check.</param>
        /// <returns>True if the attachment is allowed; otherwise false.</returns>
        public bool IsAttachmentAllowed(string attachmentId)
        {
            if (allowedAttachments == null || allowedAttachments.Count == 0)
            {
                return true;
            }
            return allowedAttachments.Contains(attachmentId);
        }

        /// <summary>
        /// Determines whether a given gear ID is prohibited.
        /// </summary>
        /// <param name="gearId">The gear ID to check.</param>
        /// <returns>True if the gear is prohibited; otherwise false.</returns>
        public bool IsGearProhibited(string gearId)
        {
            if (prohibitedGear == null || prohibitedGear.Count == 0)
            {
                return false;
            }
            return prohibitedGear.Contains(gearId);
        }
    }

    /// <summary>
    /// Complete mission briefing data including intel, restrictions, and warnings.
    /// </summary>
    [Serializable]
    public sealed class MissionBriefing
    {
        /// <summary>
        /// Unique identifier for the mission.
        /// </summary>
        public string missionId;

        /// <summary>
        /// Codename or display name of the mission.
        /// </summary>
        public string missionName;

        /// <summary>
        /// Classification level of the mission.
        /// </summary>
        public string classification;

        /// <summary>
        /// Main briefing text describing the mission background and objectives.
        /// </summary>
        public string intelText;

        /// <summary>
        /// Detailed intelligence entries.
        /// </summary>
        public List<BriefingIntel> intelEntries;

        /// <summary>
        /// Loadout restrictions for the mission.
        /// </summary>
        public LoadoutRestriction loadoutRestrictions;

        /// <summary>
        /// Operational warnings for the operator.
        /// </summary>
        public List<BriefingWarning> warnings;

        /// <summary>
        /// Path or filename of the background image for the briefing screen.
        /// </summary>
        public string backgroundImage;

        /// <summary>
        /// Serialized map data for the mission area.
        /// </summary>
        public string mapData;

        /// <summary>
        /// Recommended approach or strategy text.
        /// </summary>
        public string recommendedApproach;

        /// <summary>
        /// Indicates whether the briefing has been viewed by the player.
        /// </summary>
        public bool hasBeenViewed;

        /// <summary>
        /// Gets all warnings of a specific severity.
        /// </summary>
        /// <param name="severity">The severity level to filter by.</param>
        /// <returns>A list of matching warnings.</returns>
        public List<BriefingWarning> GetWarnings(WarningSeverity severity)
        {
            if (warnings == null) return new List<BriefingWarning>();
            List<BriefingWarning> result = new List<BriefingWarning>();
            foreach (BriefingWarning warning in warnings)
            {
                if (warning.severity == severity)
                {
                    result.Add(warning);
                }
            }
            return result;
        }
    }
}
