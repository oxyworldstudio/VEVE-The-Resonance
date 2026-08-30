using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Mission
{
    /// <summary>
    /// Types of triggers that can activate a mission event.
    /// </summary>
    public enum TriggerType { Proximity, Timer, KillCount, ItemPickup, DialogueChoice, ObjectiveComplete, SceneEnter, Custom }

    /// <summary>
    /// Configuration for a proximity-based trigger.
    /// </summary>
    [Serializable]
    public sealed class ProximityTrigger
    {
        /// <summary>
        /// Center position of the proximity zone.
        /// </summary>
        public Vector3 center;

        /// <summary>
        /// Radius of the proximity zone.
        /// </summary>
        public float radius;

        /// <summary>
        /// Tag filter for valid trigger targets. Empty means any object.
        /// </summary>
        public string targetTag;
    }

    /// <summary>
    /// Configuration for a timer-based trigger.
    /// </summary>
    [Serializable]
    public sealed class TimerTrigger
    {
        /// <summary>
        /// Delay in seconds before the trigger activates.
        /// </summary>
        public float delay;

        /// <summary>
        /// Indicates whether the timer starts immediately on mission start.
        /// </summary>
        public bool startOnMissionStart;
    }

    /// <summary>
    /// Configuration for a kill count-based trigger.
    /// </summary>
    [Serializable]
    public sealed class KillCountTrigger
    {
        /// <summary>
        /// ID of the enemy type or faction to count.
        /// </summary>
        public string targetEnemyId;

        /// <summary>
        /// Number of kills required to activate the trigger.
        /// </summary>
        public int requiredKills;
    }

    /// <summary>
    /// Configuration for an item-based trigger.
    /// </summary>
    [Serializable]
    public sealed class ItemTrigger
    {
        /// <summary>
        /// ID of the item that must be in inventory.
        /// </summary>
        public string itemId;

        /// <summary>
        /// Indicates whether the item must be picked up after the trigger is armed.
        /// </summary>
        public bool requirePickup;

        /// <summary>
        /// Indicates whether the item must be equipped.
        /// </summary>
        public bool requireEquipped;
    }

    /// <summary>
    /// Configuration for a dialogue choice trigger.
    /// </summary>
    [Serializable]
    public sealed class DialogueTrigger
    {
        /// <summary>
        /// ID of the dialogue node whose choice activates the trigger.
        /// </summary>
        public string dialogueNodeId;

        /// <summary>
        /// Index of the choice within the dialogue node.
        /// </summary>
        public int choiceIndex;
    }

    /// <summary>
    /// Types of consequence actions that can be executed.
    /// </summary>
    public enum ConsequenceType { TriggerEvent, CompleteObjective, FailObjective, SpawnEntity, PlayDialogue, ShowBriefing, AddItem, RemoveItem, ModifyStat, EndMission, EnableTrigger, DisableTrigger }

    /// <summary>
    /// A single consequence action to execute.
    /// </summary>
    [Serializable]
    public sealed class ConsequenceAction
    {
        /// <summary>
        /// Type of action to perform.
        /// </summary>
        public ConsequenceType actionType;

        /// <summary>
        /// Target ID for the action, if applicable.
        /// </summary>
        public string targetId;

        /// <summary>
        /// Numeric value parameter for the action.
        /// </summary>
        public float floatValue;

        /// <summary>
        /// Integer value parameter for the action.
        /// </summary>
        public int intValue;

        /// <summary>
        /// String value parameter for the action.
        /// </summary>
        public string stringValue;

        /// <summary>
        /// Boolean value parameter for the action.
        /// </summary>
        public bool boolValue;
    }

    /// <summary>
    /// A trigger-based mission event with consequences.
    /// </summary>
    [Serializable]
    public sealed class MissionEvent
    {
        /// <summary>
        /// Unique identifier for the event.
        /// </summary>
        public string eventId;

        /// <summary>
        /// Display name for debugging and editor tools.
        /// </summary>
        public string displayName;

        /// <summary>
        /// Indicates whether the event is currently enabled.
        /// </summary>
        public bool isEnabled;

        /// <summary>
        /// Indicates whether the event has already fired.
        /// </summary>
        public bool hasFired;

        /// <summary>
        /// Type of trigger that activates this event.
        /// </summary>
        public TriggerType triggerType;

        /// <summary>
        /// Proximity trigger configuration.
        /// </summary>
        public ProximityTrigger proximityTrigger;

        /// <summary>
        /// Timer trigger configuration.
        /// </summary>
        public TimerTrigger timerTrigger;

        /// <summary>
        /// Kill count trigger configuration.
        /// </summary>
        public KillCountTrigger killCountTrigger;

        /// <summary>
        /// Item trigger configuration.
        /// </summary>
        public ItemTrigger itemTrigger;

        /// <summary>
        /// Dialogue trigger configuration.
        /// </summary>
        public DialogueTrigger dialogueTrigger;

        /// <summary>
        /// ID of the objective that activates this event.
        /// </summary>
        public string objectiveId;

        /// <summary>
        /// Actions to execute when the event is triggered.
        /// </summary>
        public List<ConsequenceAction> consequences;

        /// <summary>
        /// Delay in seconds before consequences execute after triggering.
        /// </summary>
        public float consequenceDelay;

        /// <summary>
        /// Indicates whether the event can only fire once.
        /// </summary>
        public bool onlyOnce;

        /// <summary>
        /// Evaluates whether the proximity trigger condition is met.
        /// </summary>
        /// <param name="position">The position to test against the trigger zone.</param>
        /// <returns>True if the position is within the proximity radius; otherwise false.</returns>
        public bool EvaluateProximity(Vector3 position)
        {
            if (triggerType != TriggerType.Proximity || proximityTrigger == null)
            {
                return false;
            }
            return Vector3.Distance(position, proximityTrigger.center) <= proximityTrigger.radius;
        }

        /// <summary>
        /// Evaluates whether the timer trigger condition is met based on elapsed time.
        /// </summary>
        /// <param name="elapsedTime">Time elapsed since the mission started, in seconds.</param>
        /// <returns>True if the elapsed time exceeds the trigger delay; otherwise false.</returns>
        public bool EvaluateTimer(float elapsedTime)
        {
            if (triggerType != TriggerType.Timer || timerTrigger == null)
            {
                return false;
            }
            return elapsedTime >= timerTrigger.delay;
        }

        /// <summary>
        /// Evaluates whether the kill count trigger condition is met.
        /// </summary>
        /// <param name="currentKills">Current number of kills recorded.</param>
        /// <returns>True if the kill count meets or exceeds the required amount; otherwise false.</returns>
        public bool EvaluateKillCount(int currentKills)
        {
            if (triggerType != TriggerType.KillCount || killCountTrigger == null)
            {
                return false;
            }
            return currentKills >= killCountTrigger.requiredKills;
        }

        /// <summary>
        /// Evaluates whether the item trigger condition is met.
        /// </summary>
        /// <param name="inventory">The player inventory to check.</param>
        /// <returns>True if the item condition is satisfied; otherwise false.</returns>
        public bool EvaluateItemTrigger(IInventory inventory)
        {
            if (triggerType != TriggerType.ItemPickup || itemTrigger == null || inventory == null)
            {
                return false;
            }
            return inventory.HasItem(itemTrigger.itemId);
        }

        /// <summary>
        /// Marks the event as fired and disables it if configured to fire only once.
        /// </summary>
        public void Fire()
        {
            hasFired = true;
            if (onlyOnce)
            {
                isEnabled = false;
            }
        }
    }

    /// <summary>
    /// Minimal inventory interface for trigger evaluation.
    /// </summary>
    public interface IInventory
    {
        /// <summary>
        /// Checks whether the inventory contains the specified item.
        /// </summary>
        bool HasItem(string itemId);
    }
}
