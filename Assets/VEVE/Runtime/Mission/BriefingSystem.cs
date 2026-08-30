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
    /// Types of tactical markers displayed on the briefing map.
    /// </summary>
    public enum TacticalMarkerType { Objective, EntryPoint, Extraction, SniperNest, IED, HostileGroup, Intel }

    /// <summary>
    /// A marker displayed on the tactical map during briefing.
    /// </summary>
    [Serializable]
    public sealed class TacticalMapMarker
    {
        /// <summary>
        /// Unique identifier for the marker.
        /// </summary>
        public string markerId;

        /// <summary>
        /// Classification of the marker.
        /// </summary>
        public TacticalMarkerType markerType;

        /// <summary>
        /// World position of the marker.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Display label for the marker.
        /// </summary>
        public string label;

        /// <summary>
        /// Radius of the area marker, if applicable.
        /// </summary>
        public float areaRadius;

        /// <summary>
        /// Indicates whether the marker is visible before mission start.
        /// </summary>
        public bool visiblePreMission;
    }

    /// <summary>
    /// Configuration for the tactical map display during briefing.
    /// </summary>
    [Serializable]
    public sealed class TacticalMapDisplay
    {
        /// <summary>
        /// Path or filename of the map texture asset.
        /// </summary>
        public string mapTexturePath;

        /// <summary>
        /// Scale factor for the map display.
        /// </summary>
        public float mapScale;

        /// <summary>
        /// Rotation of the map in degrees.
        /// </summary>
        public float mapRotation;

        /// <summary>
        /// Center point of the map in world coordinates.
        /// </summary>
        public Vector3 mapCenter;

        /// <summary>
        /// All tactical markers to display on the map.
        /// </summary>
        public List<TacticalMapMarker> markers;
    }

    /// <summary>
    /// Result of a loadout validation check.
    /// </summary>
    [Serializable]
    public sealed class LoadoutValidationResult
    {
        /// <summary>
        /// Indicates whether the loadout passed all validation checks.
        /// </summary>
        public bool isValid;

        /// <summary>
        /// List of validation errors that prevent mission start.
        /// </summary>
        public List<string> errors;

        /// <summary>
        /// List of validation warnings that do not prevent mission start.
        /// </summary>
        public List<string> warnings;

        /// <summary>
        /// Calculated total loadout weight.
        /// </summary>
        public float totalWeight;

        /// <summary>
        /// Indicates whether required gear is equipped.
        /// </summary>
        public bool hasRequiredGear;

        /// <summary>
        /// Adds an error message to the validation result.
        /// </summary>
        /// <param name="message">The error message to add.</param>
        public void AddError(string message)
        {
            errors.Add(message);
            isValid = false;
        }

        /// <summary>
        /// Adds a warning message to the validation result.
        /// </summary>
        /// <param name="message">The warning message to add.</param>
        public void AddWarning(string message)
        {
            warnings.Add(message);
        }
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

        /// <summary>
        /// Validates a loadout against these restrictions.
        /// </summary>
        /// <param name="weaponIds">List of equipped weapon IDs.</param>
        /// <param name="attachmentIds">List of equipped attachment IDs.</param>
        /// <param name="gearIds">List of equipped gear IDs.</param>
        /// <param name="totalWeight">Total loadout weight.</param>
        /// <returns>A validation result describing any errors or warnings.</returns>
        public LoadoutValidationResult ValidateLoadout(List<string> weaponIds, List<string> attachmentIds, List<string> gearIds, float totalWeight)
        {
            var result = new LoadoutValidationResult
            {
                isValid = true,
                errors = new List<string>(),
                warnings = new List<string>(),
                totalWeight = totalWeight,
                hasRequiredGear = true
            };

            if (allowedWeapons != null && allowedWeapons.Count > 0)
            {
                foreach (string weapon in weaponIds)
                {
                    if (!IsWeaponAllowed(weapon))
                    {
                        result.AddError($"Weapon {weapon} is not permitted for this mission.");
                    }
                }
            }

            if (allowedAttachments != null && allowedAttachments.Count > 0)
            {
                foreach (string attachment in attachmentIds)
                {
                    if (!IsAttachmentAllowed(attachment))
                    {
                        result.AddError($"Attachment {attachment} is not permitted for this mission.");
                    }
                }
            }

            if (maxWeight > 0f && totalWeight > maxWeight)
            {
                result.AddError($"Loadout weight {totalWeight:F1} exceeds maximum allowed weight {maxWeight:F1}.");
            }

            if (requiredGear != null && requiredGear.Count > 0)
            {
                foreach (string required in requiredGear)
                {
                    if (gearIds == null || !gearIds.Contains(required))
                    {
                        result.hasRequiredGear = false;
                        result.AddError($"Required gear {required} is not equipped.");
                    }
                }
            }

            if (prohibitedGear != null && prohibitedGear.Count > 0)
            {
                foreach (string gear in gearIds)
                {
                    if (IsGearProhibited(gear))
                    {
                        result.AddError($"Prohibited gear {gear} is equipped.");
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Complete mission briefing data including intel, restrictions, map, and warnings.
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
        /// Tactical map display configuration.
        /// </summary>
        public TacticalMapDisplay tacticalMap;

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
