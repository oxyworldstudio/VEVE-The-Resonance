using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Mission
{
    /// <summary>
    /// Defines the type classification of a mission objective.
    /// </summary>
    public enum ObjectiveType { Primary, Secondary, Hidden }

    /// <summary>
    /// Represents the current lifecycle state of an objective.
    /// </summary>
    public enum ObjectiveStatus { Inactive, Active, Completed, Failed, Hidden }

    /// <summary>
    /// Types of objective markers displayed in the world and on the map.
    /// </summary>
    public enum ObjectiveMarkerType { Waypoint, Area, Target, Extraction, Hostile, Intel }

    /// <summary>
    /// A single condition required to complete an objective.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveCondition
    {
        /// <summary>
        /// Unique identifier for the condition.
        /// </summary>
        public string conditionId;

        /// <summary>
        /// Human-readable description of the condition.
        /// </summary>
        public string description;

        /// <summary>
        /// The type of condition being evaluated.
        /// </summary>
        public ConditionType type;

        /// <summary>
        /// Target entity or location ID associated with the condition.
        /// </summary>
        public string targetId;

        /// <summary>
        /// The value required to satisfy this condition.
        /// </summary>
        public float requiredValue;

        /// <summary>
        /// The current progress toward satisfying this condition.
        /// </summary>
        public float currentValue;

        /// <summary>
        /// Indicates whether the condition has been satisfied.
        /// </summary>
        public bool IsSatisfied => currentValue >= requiredValue;
    }

    /// <summary>
    /// Categories of objective conditions.
    /// </summary>
    public enum ConditionType { ReachLocation, EliminateTarget, CollectItem, SurviveTime, CompleteAction, Custom }

    /// <summary>
    /// Reward granted upon objective completion.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveReward
    {
        /// <summary>
        /// Unique identifier for the reward.
        /// </summary>
        public string rewardId;

        /// <summary>
        /// Type of reward being granted.
        /// </summary>
        public RewardType type;

        /// <summary>
        /// Numeric amount or quantity of the reward.
        /// </summary>
        public int amount;

        /// <summary>
        /// ID of the item or asset being rewarded.
        /// </summary>
        public string itemId;

        /// <summary>
        /// Display name for the reward in the UI.
        /// </summary>
        public string displayName;
    }

    /// <summary>
    /// Categories of objective rewards.
    /// </summary>
    public enum RewardType { Currency, Experience, Item, Unlock, Score }

    /// <summary>
    /// A waypoint associated with an objective marker.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveWaypoint
    {
        /// <summary>
        /// Unique identifier for the waypoint.
        /// </summary>
        public string waypointId;

        /// <summary>
        /// World position of the waypoint.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Display name of the waypoint.
        /// </summary>
        public string displayName;

        /// <summary>
        /// Sequence index for ordered waypoints.
        /// </summary>
        public int sequenceIndex;

        /// <summary>
        /// Indicates whether the waypoint is currently active.
        /// </summary>
        public bool isActive;

        /// <summary>
        /// Indicates whether the waypoint has been reached.
        /// </summary>
        public bool isReached;
    }

    /// <summary>
    /// World-space marker configuration for an objective.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveMarker
    {
        /// <summary>
        /// Unique identifier for the marker.
        /// </summary>
        public string markerId;

        /// <summary>
        /// Classification of the marker.
        /// </summary>
        public ObjectiveMarkerType markerType;

        /// <summary>
        /// World position of the marker.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Radius of the area marker, if applicable.
        /// </summary>
        public float areaRadius;

        /// <summary>
        /// Indicates whether the marker is visible on the minimap.
        /// </summary>
        public bool visibleOnMinimap;

        /// <summary>
        /// Indicates whether the marker is visible on the world map.
        /// </summary>
        public bool visibleOnWorldMap;

        /// <summary>
        /// Waypoints associated with this marker.
        /// </summary>
        public List<ObjectiveWaypoint> waypoints;
    }

    /// <summary>
    /// A mission objective with conditions, rewards, markers, and completion tracking.
    /// </summary>
    [Serializable]
    public sealed class MissionObjective
    {
        /// <summary>
        /// Unique identifier for the objective.
        /// </summary>
        public string objectiveId;

        /// <summary>
        /// Title displayed in the mission HUD.
        /// </summary>
        public string title;

        /// <summary>
        /// Detailed description of the objective.
        /// </summary>
        public string description;

        /// <summary>
        /// Classification of the objective.
        /// </summary>
        public ObjectiveType objectiveType;

        /// <summary>
        /// Current lifecycle status of the objective.
        /// </summary>
        public ObjectiveStatus status;

        /// <summary>
        /// List of conditions that must be satisfied to complete the objective.
        /// </summary>
        public List<ObjectiveCondition> conditions;

        /// <summary>
        /// Rewards granted upon successful completion.
        /// </summary>
        public List<ObjectiveReward> rewards;

        /// <summary>
        /// Indicates whether the objective is optional.
        /// </summary>
        public bool isOptional;

        /// <summary>
        /// Indicates whether failure to complete this objective results in mission failure.
        /// </summary>
        public bool failOnFailure;

        /// <summary>
        /// Maximum time allowed to complete the objective, in seconds. Zero means no limit.
        /// </summary>
        public float timeoutDuration;

        /// <summary>
        /// World-space marker for this objective.
        /// </summary>
        public ObjectiveMarker marker;

        /// <summary>
        /// Overall completion progress from 0 to 1.
        /// </summary>
        public float completionProgress;

        /// <summary>
        /// Indicates whether the objective timer has expired.
        /// </summary>
        public bool IsTimedOut => timeoutDuration > 0f && Time.time > activationTime + timeoutDuration;

        /// <summary>
        /// Elapsed time since the objective was activated.
        /// </summary>
        public float elapsedTime => Time.time - activationTime;

        private float activationTime;

        /// <summary>
        /// Activates the objective and starts tracking progress.
        /// </summary>
        public void Activate()
        {
            status = ObjectiveStatus.Active;
            activationTime = Time.time;
            completionProgress = 0f;
            if (marker != null && marker.waypoints != null)
            {
                foreach (var waypoint in marker.waypoints)
                {
                    waypoint.isActive = false;
                    waypoint.isReached = false;
                }
            }
            foreach (ObjectiveCondition condition in conditions)
            {
                condition.currentValue = 0f;
            }
        }

        /// <summary>
        /// Updates progress toward a specific condition.
        /// </summary>
        /// <param name="conditionId">The ID of the condition to update.</param>
        /// <param name="amount">The amount to add to the current value.</param>
        public void UpdateConditionProgress(string conditionId, float amount)
        {
            foreach (ObjectiveCondition condition in conditions)
            {
                if (condition.conditionId == conditionId)
                {
                    condition.currentValue = Mathf.Min(condition.requiredValue, condition.currentValue + amount);
                    UpdateCompletionProgress();
                    break;
                }
            }
        }

        /// <summary>
        /// Sets the current value of a specific condition directly.
        /// </summary>
        /// <param name="conditionId">The ID of the condition to set.</param>
        /// <param name="value">The new current value.</param>
        public void SetConditionValue(string conditionId, float value)
        {
            foreach (ObjectiveCondition condition in conditions)
            {
                if (condition.conditionId == conditionId)
                {
                    condition.currentValue = Mathf.Clamp(value, 0f, condition.requiredValue);
                    UpdateCompletionProgress();
                    break;
                }
            }
        }

        /// <summary>
        /// Evaluates whether all conditions have been satisfied.
        /// </summary>
        /// <returns>True if all conditions are satisfied; otherwise false.</returns>
        public bool EvaluateCompletion()
        {
            foreach (ObjectiveCondition condition in conditions)
            {
                if (!condition.IsSatisfied)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Marks the objective as completed.
        /// </summary>
        public void Complete()
        {
            if (status == ObjectiveStatus.Active)
            {
                status = ObjectiveStatus.Completed;
                completionProgress = 1f;
            }
        }

        /// <summary>
        /// Marks the objective as failed.
        /// </summary>
        public void Fail()
        {
            if (status == ObjectiveStatus.Active)
            {
                status = ObjectiveStatus.Failed;
                completionProgress = 0f;
            }
        }

        /// <summary>
        /// Updates the overall completion progress based on condition states.
        /// </summary>
        private void UpdateCompletionProgress()
        {
            if (conditions == null || conditions.Count == 0)
            {
                completionProgress = 0f;
                return;
            }

            float total = 0f;
            foreach (ObjectiveCondition condition in conditions)
            {
                total += Mathf.Clamp01(condition.currentValue / Mathf.Max(condition.requiredValue, 0.01f));
            }
            completionProgress = total / conditions.Count;

            if (completionProgress >= 1f && status == ObjectiveStatus.Active)
            {
                Complete();
            }
        }
    }
}
