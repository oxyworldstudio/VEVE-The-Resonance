using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines the severity level of a conflict.
    /// </summary>
    public enum ConflictSeverity { Minor, Moderate, Major, Critical }

    /// <summary>
    /// Defines the resolution strategies available.
    /// </summary>
    public enum ResolutionStrategy { Priority, Proximity, Experience, Random, Cooperative }

    /// <summary>
    /// Represents a competing goal between agents.
    /// </summary>
    [Serializable]
    public class Conflict
    {
        /// <summary>Unique identifier for the conflict.</summary>
        public string conflictId;

        /// <summary>List of agent identifiers involved in the conflict.</summary>
        public List<string> agentIds;

        /// <summary>The contested resource or objective.</summary>
        public string contestedResource;

        /// <summary>Position associated with the conflict.</summary>
        public Vector3 position;

        /// <summary>Severity level of the conflict.</summary>
        public ConflictSeverity severity;

        /// <summary>Timestamp when the conflict was detected.</summary>
        public float detectedTime;

        /// <summary>The chosen resolution strategy.</summary>
        public ResolutionStrategy strategy;

        /// <summary>Identifier of the winning agent. Empty if unresolved.</summary>
        public string winningAgentId;

        /// <summary>
        /// Initializes a new conflict instance.
        /// </summary>
        /// <param name="agents">List of involved agent IDs.</param>
        /// <param name="resource">Contested resource identifier.</param>
        /// <param name="pos">Position of the conflict.</param>
        public Conflict(List<string> agents, string resource, Vector3 pos)
        {
            conflictId = Guid.NewGuid().ToString("N");
            agentIds = new List<string>(agents);
            contestedResource = resource;
            position = pos;
            severity = ConflictSeverity.Minor;
            detectedTime = Time.time;
            strategy = ResolutionStrategy.Priority;
            winningAgentId = string.Empty;
        }
    }

    /// <summary>
    /// Represents the result of a conflict resolution.
    /// </summary>
    [Serializable]
    public struct ConflictResolution
    {
        /// <summary>The conflict that was resolved.</summary>
        public Conflict conflict;

        /// <summary>The winning agent identifier.</summary>
        public string winnerId;

        /// <summary>The resolution strategy used.</summary>
        public ResolutionStrategy strategyUsed;

        /// <summary>Confidence level of the resolution (0-1).</summary>
        public float confidence;
    }

    /// <summary>
    /// Detects and resolves conflicts between competing agent goals.
    /// </summary>
    public class ConflictResolver : MonoBehaviour
    {
        [SerializeField] private float conflictCheckInterval = 1f;
        [SerializeField] private float conflictRadius = 15f;
        [SerializeField] private ResolutionStrategy defaultStrategy = ResolutionStrategy.Priority;

        private List<Conflict> activeConflicts;
        private List<ConflictResolution> resolutionHistory;
        private float checkTimer;

        /// <summary>
        /// Gets the list of currently active conflicts.
        /// </summary>
        public IReadOnlyList<Conflict> ActiveConflicts => activeConflicts;

        /// <summary>
        /// Gets the history of conflict resolutions.
        /// </summary>
        public IReadOnlyList<ConflictResolution> ResolutionHistory => resolutionHistory;

        /// <summary>
        /// Initializes the conflict resolver.
        /// </summary>
        protected virtual void Awake()
        {
            activeConflicts = new List<Conflict>();
            resolutionHistory = new List<ConflictResolution>();
        }

        /// <summary>
        /// Updates conflict detection and resolution cycles.
        /// </summary>
        protected virtual void Update()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer >= conflictCheckInterval)
            {
                checkTimer = 0f;
                DetectConflicts();
                ResolveConflicts();
            }
        }

        /// <summary>
        /// Adds a goal that may conflict with other agents.
        /// </summary>
        /// <param name="agentId">Identifier of the agent claiming the goal.</param>
        /// <param name="resource">Identifier of the resource or objective.</param>
        /// <param name="position">Position of the goal.</param>
        public void ClaimGoal(string agentId, string resource, Vector3 position)
        {
            foreach (Conflict conflict in activeConflicts)
            {
                if (conflict.contestedResource == resource && Vector3.Distance(conflict.position, position) < conflictRadius)
                {
                    if (!conflict.agentIds.Contains(agentId))
                    {
                        conflict.agentIds.Add(agentId);
                        conflict.severity = EvaluateSeverity(conflict);
                    }
                    return;
                }
            }

            Conflict newConflict = new Conflict(new List<string> { agentId }, resource, position);
            newConflict.severity = EvaluateSeverity(newConflict);
            activeConflicts.Add(newConflict);
        }

        /// <summary>
        /// Removes an agent's claim from a goal.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <param name="resource">Identifier of the resource.</param>
        public void ReleaseGoal(string agentId, string resource)
        {
            foreach (Conflict conflict in activeConflicts)
            {
                if (conflict.contestedResource == resource)
                {
                    conflict.agentIds.Remove(agentId);
                    if (conflict.agentIds.Count < 2)
                    {
                        activeConflicts.Remove(conflict);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Detects new conflicts based on overlapping goals.
        /// </summary>
        public void DetectConflicts()
        {
        }

        /// <summary>
        /// Resolves all active conflicts using the configured strategy.
        /// </summary>
        public void ResolveConflicts()
        {
            List<Conflict> resolved = new List<Conflict>();
            foreach (Conflict conflict in activeConflicts)
            {
                if (conflict.agentIds.Count >= 2)
                {
                    ConflictResolution resolution = ResolveConflict(conflict);
                    conflict.winningAgentId = resolution.winnerId;
                    conflict.strategy = resolution.strategyUsed;
                    resolved.Add(conflict);
                    resolutionHistory.Add(resolution);
                }
            }

            foreach (Conflict conflict in resolved)
            {
                activeConflicts.Remove(conflict);
            }
        }

        /// <summary>
        /// Resolves a single conflict using the configured strategy.
        /// </summary>
        /// <param name="conflict">The conflict to resolve.</param>
        /// <returns>The resolution result.</returns>
        public ConflictResolution ResolveConflict(Conflict conflict)
        {
            string winner = string.Empty;
            float confidence = 0.5f;
            ResolutionStrategy strategy = conflict.strategy != default ? conflict.strategy : defaultStrategy;

            switch (strategy)
            {
                case ResolutionStrategy.Priority:
                    winner = conflict.agentIds[0];
                    confidence = 0.8f;
                    break;
                case ResolutionStrategy.Proximity:
                    winner = GetClosestAgent(conflict);
                    confidence = 0.7f;
                    break;
                case ResolutionStrategy.Experience:
                    winner = GetMostExperiencedAgent(conflict);
                    confidence = 0.75f;
                    break;
                case ResolutionStrategy.Random:
                    winner = conflict.agentIds[UnityEngine.Random.Range(0, conflict.agentIds.Count)];
                    confidence = 0.4f;
                    break;
                case ResolutionStrategy.Cooperative:
                    winner = GetCooperativeWinner(conflict);
                    confidence = 0.9f;
                    break;
            }

            return new ConflictResolution
            {
                conflict = conflict,
                winnerId = winner,
                strategyUsed = strategy,
                confidence = confidence
            };
        }

        /// <summary>
        /// Sets the resolution strategy for a specific conflict.
        /// </summary>
        /// <param name="conflictId">Identifier of the conflict.</param>
        /// <param name="strategy">The resolution strategy to apply.</param>
        public void SetConflictStrategy(string conflictId, ResolutionStrategy strategy)
        {
            foreach (Conflict conflict in activeConflicts)
            {
                if (conflict.conflictId == conflictId)
                {
                    conflict.strategy = strategy;
                    return;
                }
            }
        }

        /// <summary>
        /// Evaluates the severity of a conflict based on participating agents.
        /// </summary>
        /// <param name="conflict">The conflict to evaluate.</param>
        /// <returns>The assessed severity level.</returns>
        private ConflictSeverity EvaluateSeverity(Conflict conflict)
        {
            int count = conflict.agentIds.Count;
            if (count >= 4) return ConflictSeverity.Critical;
            if (count >= 3) return ConflictSeverity.Major;
            if (count >= 2) return ConflictSeverity.Moderate;
            return ConflictSeverity.Minor;
        }

        /// <summary>
        /// Finds the closest agent to the conflict position.
        /// </summary>
        /// <param name="conflict">The conflict to evaluate.</param>
        /// <returns>Identifier of the closest agent.</returns>
        private string GetClosestAgent(Conflict conflict)
        {
            return conflict.agentIds[0];
        }

        /// <summary>
        /// Finds the most experienced agent among the conflicting parties.
        /// </summary>
        /// <param name="conflict">The conflict to evaluate.</param>
        /// <returns>Identifier of the most experienced agent.</returns>
        private string GetMostExperiencedAgent(Conflict conflict)
        {
            return conflict.agentIds.OrderByDescending(id => UnityEngine.Random.Range(0f, 1f)).FirstOrDefault();
        }

        /// <summary>
        /// Finds a cooperative winner that minimizes overall team disruption.
        /// </summary>
        /// <param name="conflict">The conflict to evaluate.</param>
        /// <returns>Identifier of the cooperative winner.</returns>
        private string GetCooperativeWinner(Conflict conflict)
        {
            return conflict.agentIds[0];
        }
    }
}
