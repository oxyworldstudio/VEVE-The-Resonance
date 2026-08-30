using System;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines the types of coordination protocols available.
    /// </summary>
    public enum CoordinationType { Formation, BoundingOverwatch, CenterPeel, ContactDrill, ReactToContact }

    /// <summary>
    /// Defines the current state of a coordination protocol.
    /// </summary>
    public enum ProtocolState { Idle, Preparing, Active, Paused, Completed }

    /// <summary>
    /// Represents a formation configuration.
    /// </summary>
    [Serializable]
    public struct FormationConfig
    {
        /// <summary>The type of formation.</summary>
        public string formationName;

        /// <summary>Offset positions relative to the formation center.</summary>
        public List<Vector3> offsets;

        /// <summary>Rotation adjustment in degrees.</summary>
        public float rotationOffset;

        /// <summary>Spacing multiplier for the formation.</summary>
        public float spacingMultiplier;
    }

    /// <summary>
    /// Represents a bounding overwatch assignment.
    /// </summary>
    [Serializable]
    public struct BoundingAssignment
    {
        /// <summary>Identifier of the bounding agent.</summary>
        public string boundingAgentId;

        /// <summary>Identifier of the covering agent.</summary>
        public string coveringAgentId;

        /// <summary>Target position for the bounding agent.</summary>
        public Vector3 targetPosition;

        /// <summary>Hold position for the covering agent.</summary>
        public Vector3 coverPosition;

        /// <summary>Duration of the bounding move in seconds.</summary>
        public float moveDuration;
    }

    /// <summary>
    /// Serializable container for coordination parameters.
    /// </summary>
    [Serializable]
    public class CoordinationParams
    {
        /// <summary>The type of coordination protocol.</summary>
        public CoordinationType type;

        /// <summary>Current state of the protocol.</summary>
        public ProtocolState state;

        /// <summary>Center position of the coordination.</summary>
        public Vector3 center;

        /// <summary>List of participating agent identifiers.</summary>
        public List<string> participantIds;

        /// <summary>Formation configuration, if applicable.</summary>
        public FormationConfig formation;

        /// <summary>Bounding assignments, if applicable.</summary>
        public List<BoundingAssignment> boundingAssignments;

        /// <summary>
        /// Initializes coordination parameters with default values.
        /// </summary>
        public CoordinationParams()
        {
            type = CoordinationType.Formation;
            state = ProtocolState.Idle;
            center = Vector3.zero;
            participantIds = new List<string>();
            formation = new FormationConfig();
            boundingAssignments = new List<BoundingAssignment>();
        }
    }

    /// <summary>
    /// Protocols for agent coordination including formations and bounding overwatch.
    /// </summary>
    public class CoordinationProtocol : MonoBehaviour
    {
        [SerializeField] private float formationSpacing = 3f;
        [SerializeField] private float boundingMoveSpeed = 4f;
        [SerializeField] private float overwatchSuppressionRange = 30f;

        private CoordinationParams currentParams;
        private Dictionary<string, Vector3> agentTargetPositions;

        /// <summary>
        /// Gets the current coordination parameters.
        /// </summary>
        public CoordinationParams CurrentParams => currentParams;

        /// <summary>
        /// Initializes the coordination protocol system.
        /// </summary>
        protected virtual void Awake()
        {
            currentParams = new CoordinationParams();
            agentTargetPositions = new Dictionary<string, Vector3>();
        }

        /// <summary>
        /// Updates the coordination protocol execution.
        /// </summary>
        protected virtual void Update()
        {
            if (currentParams.state == ProtocolState.Active)
            {
                ExecuteProtocol();
            }
        }

        /// <summary>
        /// Activates a coordination protocol.
        /// </summary>
        /// <param name="parameters">The coordination parameters to activate.</param>
        public void ActivateProtocol(CoordinationParams parameters)
        {
            currentParams = parameters;
            currentParams.state = ProtocolState.Preparing;
            CalculatePositions();
            currentParams.state = ProtocolState.Active;
        }

        /// <summary>
        /// Pauses the current coordination protocol.
        /// </summary>
        public void PauseProtocol()
        {
            currentParams.state = ProtocolState.Paused;
        }

        /// <summary>
        /// Resumes a paused coordination protocol.
        /// </summary>
        public void ResumeProtocol()
        {
            if (currentParams.state == ProtocolState.Paused)
            {
                currentParams.state = ProtocolState.Active;
            }
        }

        /// <summary>
        /// Stops the current coordination protocol.
        /// </summary>
        public void StopProtocol()
        {
            currentParams.state = ProtocolState.Idle;
            agentTargetPositions.Clear();
        }

        /// <summary>
        /// Sets a specific formation for the coordinated agents.
        /// </summary>
        /// <param name="formationName">Name of the formation.</param>
        /// <param name="offsets">Relative position offsets for each agent.</param>
        public void SetFormation(string formationName, List<Vector3> offsets)
        {
            currentParams.type = CoordinationType.Formation;
            currentParams.formation.formationName = formationName;
            currentParams.formation.offsets = offsets;
            currentParams.formation.spacingMultiplier = 1f;
            CalculatePositions();
        }

        /// <summary>
        /// Starts a bounding overwatch protocol.
        /// </summary>
        /// <param name="assignments">List of bounding assignments.</param>
        public void StartBoundingOverwatch(List<BoundingAssignment> assignments)
        {
            currentParams.type = CoordinationType.BoundingOverwatch;
            currentParams.boundingAssignments = assignments;
            currentParams.state = ProtocolState.Active;
            CalculatePositions();
        }

        /// <summary>
        /// Gets the target position for a specific agent.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <returns>Target position, or Vector3.zero if not found.</returns>
        public Vector3 GetAgentTargetPosition(string agentId)
        {
            if (agentTargetPositions.ContainsKey(agentId))
            {
                return agentTargetPositions[agentId];
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Calculates formation positions for all participants.
        /// </summary>
        private void CalculatePositions()
        {
            agentTargetPositions.Clear();
            if (currentParams.participantIds == null || currentParams.participantIds.Count == 0) return;

            switch (currentParams.type)
            {
                case CoordinationType.Formation:
                    CalculateFormationPositions();
                    break;
                case CoordinationType.BoundingOverwatch:
                    CalculateBoundingPositions();
                    break;
            }
        }

        /// <summary>
        /// Calculates positions based on the current formation configuration.
        /// </summary>
        private void CalculateFormationPositions()
        {
            if (currentParams.formation.offsets == null) return;
            for (int i = 0; i < currentParams.participantIds.Count && i < currentParams.formation.offsets.Count; i++)
            {
                Vector3 offset = currentParams.formation.offsets[i] * currentParams.formation.spacingMultiplier;
                Quaternion rotation = Quaternion.Euler(0f, currentParams.formation.rotationOffset, 0f);
                agentTargetPositions[currentParams.participantIds[i]] = currentParams.center + rotation * offset;
            }
        }

        /// <summary>
        /// Calculates positions for bounding overwatch assignments.
        /// </summary>
        private void CalculateBoundingPositions()
        {
            foreach (BoundingAssignment assignment in currentParams.boundingAssignments)
            {
                if (!string.IsNullOrEmpty(assignment.boundingAgentId))
                {
                    agentTargetPositions[assignment.boundingAgentId] = assignment.targetPosition;
                }
                if (!string.IsNullOrEmpty(assignment.coveringAgentId))
                {
                    agentTargetPositions[assignment.coveringAgentId] = assignment.coverPosition;
                }
            }
        }

        /// <summary>
        /// Executes the active coordination protocol.
        /// </summary>
        private void ExecuteProtocol()
        {
            switch (currentParams.type)
            {
                case CoordinationType.BoundingOverwatch:
                    ExecuteBoundingOverwatch();
                    break;
            }
        }

        /// <summary>
        /// Executes bounding overwatch logic.
        /// </summary>
        private void ExecuteBoundingOverwatch()
        {
            if (currentParams.boundingAssignments == null || currentParams.boundingAssignments.Count == 0) return;
            for (int i = 0; i < currentParams.boundingAssignments.Count; i++)
            {
                BoundingAssignment assignment = currentParams.boundingAssignments[i];
                if (!string.IsNullOrEmpty(assignment.boundingAgentId) && agentTargetPositions.ContainsKey(assignment.boundingAgentId))
                {
                    agentTargetPositions[assignment.boundingAgentId] = Vector3.Lerp(
                        agentTargetPositions[assignment.boundingAgentId],
                        assignment.targetPosition,
                        boundingMoveSpeed * Time.deltaTime
                    );
                }
            }
        }
    }
}
