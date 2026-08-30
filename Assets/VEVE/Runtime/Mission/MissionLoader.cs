using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Mission
{
    /// <summary>
    /// Runtime system that loads and initializes a mission from data definitions.
    /// </summary>
    public class MissionLoader : MonoBehaviour
    {
        /// <summary>
        /// Event raised when the mission has finished loading.
        /// </summary>
        public event Action OnMissionLoaded;

        /// <summary>
        /// Event raised when the mission is unloaded or ends.
        /// </summary>
        public event Action OnMissionUnloaded;

        /// <summary>
        /// Currently active mission ID.
        /// </summary>
        public string ActiveMissionId { get; private set; }

        /// <summary>
        /// Currently active mission briefing data.
        /// </summary>
        public MissionBriefing ActiveBriefing { get; private set; }

        /// <summary>
        /// All loaded mission objectives.
        /// </summary>
        public List<MissionObjective> ActiveObjectives { get; private set; }

        /// <summary>
        /// All loaded mission events.
        /// </summary>
        public List<MissionEvent> ActiveEvents { get; private set; }

        /// <summary>
        /// All loaded dialogue sequences.
        /// </summary>
        public List<DialogueSequence> ActiveDialogues { get; private set; }

        /// <summary>
        /// NPC placements loaded for the mission.
        /// </summary>
        public List<NPCSpawnData> NPCSpawns { get; private set; }

        /// <summary>
        /// Indicates whether a mission is currently loaded and active.
        /// </summary>
        public bool IsMissionLoaded => !string.IsNullOrEmpty(ActiveMissionId);

        private void Awake()
        {
            ActiveObjectives = new List<MissionObjective>();
            ActiveEvents = new List<MissionEvent>();
            ActiveDialogues = new List<DialogueSequence>();
            NPCSpawns = new List<NPCSpawnData>();
        }

        /// <summary>
        /// Loads a mission by its identifier and initializes objectives, events, dialogue, and NPCs.
        /// </summary>
        /// <param name="missionId">The unique identifier of the mission to load.</param>
        /// <param name="briefing">Mission briefing data.</param>
        /// <param name="objectives">List of mission objectives.</param>
        /// <param name="events">List of mission events.</param>
        /// <param name="dialogues">List of dialogue sequences.</param>
        /// <param name="npcSpawns">List of NPC spawn definitions.</param>
        public void LoadMission(string missionId, MissionBriefing briefing, List<MissionObjective> objectives, List<MissionEvent> events, List<DialogueSequence> dialogues, List<NPCSpawnData> npcSpawns)
        {
            UnloadMission();

            ActiveMissionId = missionId;
            ActiveBriefing = briefing;
            ActiveObjectives = objectives != null ? new List<MissionObjective>(objectives) : new List<MissionObjective>();
            ActiveEvents = events != null ? new List<MissionEvent>(events) : new List<MissionEvent>();
            ActiveDialogues = dialogues != null ? new List<DialogueSequence>(dialogues) : new List<DialogueSequence>();
            NPCSpawns = npcSpawns != null ? new List<NPCSpawnData>(npcSpawns) : new List<NPCSpawnData>();

            InitializeObjectives();
            SpawnNPCs();

            OnMissionLoaded?.Invoke();
        }

        /// <summary>
        /// Unloads the current mission and cleans up active state.
        /// </summary>
        public void UnloadMission()
        {
            if (IsMissionLoaded)
            {
                ActiveMissionId = null;
                ActiveBriefing = null;
                ActiveObjectives.Clear();
                ActiveEvents.Clear();
                ActiveDialogues.Clear();
                NPCSpawns.Clear();

                OnMissionUnloaded?.Invoke();
            }
        }

        /// <summary>
        /// Retrieves an objective by its ID.
        /// </summary>
        /// <param name="objectiveId">The ID of the objective to retrieve.</param>
        /// <returns>The matching MissionObjective, or null if not found.</returns>
        public MissionObjective GetObjective(string objectiveId)
        {
            if (ActiveObjectives == null) return null;
            foreach (var objective in ActiveObjectives)
            {
                if (objective.objectiveId == objectiveId)
                {
                    return objective;
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieves all active objectives of a specific type.
        /// </summary>
        /// <param name="type">The objective type to filter by.</param>
        /// <returns>List of matching objectives.</returns>
        public List<MissionObjective> GetObjectivesByType(ObjectiveType type)
        {
            var result = new List<MissionObjective>();
            if (ActiveObjectives == null) return result;
            foreach (var objective in ActiveObjectives)
            {
                if (objective.objectiveType == type)
                {
                    result.Add(objective);
                }
            }
            return result;
        }

        /// <summary>
        /// Retrieves an event by its ID.
        /// </summary>
        /// <param name="eventId">The ID of the event to retrieve.</param>
        /// <returns>The matching MissionEvent, or null if not found.</returns>
        public MissionEvent GetEvent(string eventId)
        {
            if (ActiveEvents == null) return null;
            foreach (var evt in ActiveEvents)
            {
                if (evt.eventId == eventId)
                {
                    return evt;
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieves a dialogue sequence by its ID.
        /// </summary>
        /// <param name="sequenceId">The ID of the dialogue sequence to retrieve.</param>
        /// <returns>The matching DialogueSequence, or null if not found.</returns>
        public DialogueSequence GetDialogue(string sequenceId)
        {
            if (ActiveDialogues == null) return null;
            foreach (var dialogue in ActiveDialogues)
            {
                if (dialogue.sequenceId == sequenceId)
                {
                    return dialogue;
                }
            }
            return null;
        }

        /// <summary>
        /// Updates all active mission events based on current game state.
        /// </summary>
        /// <param name="playerPosition">Current position of the player.</param>
        /// <param name="elapsedTime">Time elapsed since mission start.</param>
        /// <param name="inventory">Player inventory for item triggers.</param>
        /// <param name="currentKills">Current kill count.</param>
        public void UpdateEvents(Vector3 playerPosition, float elapsedTime, IInventory inventory, int currentKills)
        {
            if (ActiveEvents == null) return;

            foreach (var evt in ActiveEvents)
            {
                if (!evt.isEnabled || evt.hasFired) continue;

                bool triggered = false;

                switch (evt.triggerType)
                {
                    case TriggerType.Proximity:
                        triggered = evt.EvaluateProximity(playerPosition);
                        break;
                    case TriggerType.AreaEnter:
                        triggered = evt.EvaluateArea(playerPosition);
                        break;
                    case TriggerType.Timer:
                        triggered = evt.EvaluateTimer(elapsedTime);
                        break;
                    case TriggerType.KillCount:
                        triggered = evt.EvaluateKillCount(currentKills);
                        break;
                    case TriggerType.ItemPickup:
                        triggered = evt.EvaluateItemTrigger(inventory);
                        break;
                }

                if (triggered)
                {
                    ExecuteConsequences(evt);
                    evt.Fire();
                }
            }
        }

        /// <summary>
        /// Executes the consequence actions of a mission event.
        /// </summary>
        /// <param name="evt">The mission event whose consequences should be executed.</param>
        public void ExecuteConsequences(MissionEvent evt)
        {
            if (evt == null || evt.consequences == null) return;

            foreach (var action in evt.consequences)
            {
                ApplyAction(action);
            }
        }

        /// <summary>
        /// Applies a single consequence action.
        /// </summary>
        /// <param name="action">The consequence action to apply.</param>
        public void ApplyAction(ConsequenceAction action)
        {
            if (action == null) return;

            switch (action.actionType)
            {
                case ConsequenceType.TriggerEvent:
                    var targetEvent = GetEvent(action.targetId);
                    if (targetEvent != null && !targetEvent.hasFired)
                    {
                        ExecuteConsequences(targetEvent);
                        targetEvent.Fire();
                    }
                    break;
                case ConsequenceType.CompleteObjective:
                    var objective = GetObjective(action.targetId);
                    objective?.Complete();
                    break;
                case ConsequenceType.FailObjective:
                    var failObj = GetObjective(action.targetId);
                    failObj?.Fail();
                    break;
                case ConsequenceType.EnableTrigger:
                    var enableEvent = GetEvent(action.targetId);
                    if (enableEvent != null) enableEvent.isEnabled = true;
                    break;
                case ConsequenceType.DisableTrigger:
                    var disableEvent = GetEvent(action.targetId);
                    if (disableEvent != null) disableEvent.isEnabled = false;
                    break;
                case ConsequenceType.SpawnEntity:
                    SpawnEntity(action.targetId, action.stringValue, action.floatValue);
                    break;
            }
        }

        private void InitializeObjectives()
        {
            foreach (var objective in ActiveObjectives)
            {
                if (objective.status == ObjectiveStatus.Inactive)
                {
                    objective.status = ObjectiveStatus.Inactive;
                }
            }
        }

        private void SpawnNPCs()
        {
            if (NPCSpawns == null) return;

            foreach (var spawn in NPCSpawns)
            {
                SpawnEntity(spawn.npcId, spawn.spawnPoint, spawn.health);
            }
        }

        private void SpawnEntity(string entityId, string spawnPoint, float health)
        {
            GameObject spawnObj = GameObject.Find(spawnPoint);
            if (spawnObj == null)
            {
                spawnObj = new GameObject(entityId);
                spawnObj.transform.position = Vector3.zero;
            }
        }

        private void SpawnEntity(string entityId, Vector3 position, float health)
        {
            GameObject entity = new GameObject(entityId);
            entity.transform.position = position;
        }
    }

    /// <summary>
    /// Defines the spawn data for an NPC in a mission.
    /// </summary>
    [Serializable]
    public sealed class NPCSpawnData
    {
        /// <summary>
        /// Unique identifier of the NPC to spawn.
        /// </summary>
        public string npcId;

        /// <summary>
        /// Name of the spawn point transform or GameObject.
        /// </summary>
        public string spawnPoint;

        /// <summary>
        /// Starting health of the NPC.
        /// </summary>
        public float health;

        /// <summary>
        /// Faction or team assignment.
        /// </summary>
        public string faction;

        /// <summary>
        /// Initial behavior state.
        /// </summary>
        public string initialState;

        /// <summary>
        /// Indicates whether the NPC is essential to mission completion.
        /// </summary>
        public bool isEssential;
    }
}
