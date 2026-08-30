using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines the available agent roles.
    /// </summary>
    public enum AgentRole { Leader, Assault, Support, Marksman, Medic, Recon, Heavy }

    /// <summary>
    /// Represents the capability profile of an agent for a specific role.
    /// </summary>
    [Serializable]
    public struct RoleCapability
    {
        /// <summary>The role being evaluated.</summary>
        public AgentRole role;

        /// <summary>Capability score for this role (0-1).</summary>
        public float score;

        /// <summary>Experience level with this role (0-1).</summary>
        public float experience;

        /// <summary>True if the agent is currently trained for this role.</summary>
        public bool isTrained;
    }

    /// <summary>
    /// Serializable container for role assignment data.
    /// </summary>
    [Serializable]
    public class RoleAssignment
    {
        /// <summary>Identifier of the agent.</summary>
        public string agentId;

        /// <summary>Assigned role.</summary>
        public AgentRole currentRole;

        /// <summary>Previous role before reassignment.</summary>
        public AgentRole previousRole;

        /// <summary>Timestamp of the last role change.</summary>
        public float lastSwitchTime;

        /// <summary>Cooldown duration before another role switch is allowed.</summary>
        public float switchCooldown;

        /// <summary>
        /// Initializes a new role assignment.
        /// </summary>
        /// <param name="agent">The agent ID.</param>
        /// <param name="role">The initial role.</param>
        public RoleAssignment(string agent, AgentRole role)
        {
            agentId = agent;
            currentRole = role;
            previousRole = role;
            lastSwitchTime = 0f;
            switchCooldown = 10f;
        }
    }

    /// <summary>
    /// Dynamic role assignment and role-switching logic.
    /// </summary>
    public class RoleManager : MonoBehaviour
    {
        [SerializeField] private float roleUpdateInterval = 5f;
        [SerializeField] private float minimumCapabilityGap = 0.15f;

        private Dictionary<string, RoleAssignment> assignments;
        private Dictionary<string, List<RoleCapability>> capabilities;
        private float updateTimer;

        /// <summary>
        /// Gets all current role assignments.
        /// </summary>
        public IReadOnlyDictionary<string, RoleAssignment> Assignments => assignments;

        /// <summary>
        /// Gets all agent capability profiles.
        /// </summary>
        public IReadOnlyDictionary<string, List<RoleCapability>> Capabilities => capabilities;

        /// <summary>
        /// Initializes the role manager.
        /// </summary>
        protected virtual void Awake()
        {
            assignments = new Dictionary<string, RoleAssignment>();
            capabilities = new Dictionary<string, List<RoleCapability>>();
        }

        /// <summary>
        /// Updates role assignments based on current conditions.
        /// </summary>
        protected virtual void Update()
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= roleUpdateInterval)
            {
                updateTimer = 0f;
                EvaluateAndReassign();
            }
        }

        /// <summary>
        /// Registers an agent for role management.
        /// </summary>
        /// <param name="agentId">Unique identifier of the agent.</param>
        /// <param name="initialRole">Starting role for the agent.</param>
        public void RegisterAgent(string agentId, AgentRole initialRole)
        {
            if (string.IsNullOrEmpty(agentId)) return;
            assignments[agentId] = new RoleAssignment(agentId, initialRole);
            if (!capabilities.ContainsKey(agentId))
            {
                capabilities[agentId] = new List<RoleCapability>();
            }
        }

        /// <summary>
        /// Unregisters an agent from role management.
        /// </summary>
        /// <param name="agentId">Identifier of the agent to remove.</param>
        public void UnregisterAgent(string agentId)
        {
            if (assignments.ContainsKey(agentId)) assignments.Remove(agentId);
            if (capabilities.ContainsKey(agentId)) capabilities.Remove(agentId);
        }

        /// <summary>
        /// Updates the capability profile of an agent.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <param name="capability">The capability data to update.</param>
        public void UpdateCapability(string agentId, RoleCapability capability)
        {
            if (string.IsNullOrEmpty(agentId) || !capabilities.ContainsKey(agentId)) return;
            int index = capabilities[agentId].FindIndex(c => c.role == capability.role);
            if (index >= 0)
            {
                capabilities[agentId][index] = capability;
            }
            else
            {
                capabilities[agentId].Add(capability);
            }
        }

        /// <summary>
        /// Gets the current role of an agent.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <returns>The agent's current role, or Leader if not found.</returns>
        public AgentRole GetCurrentRole(string agentId)
        {
            if (assignments.ContainsKey(agentId)) return assignments[agentId].currentRole;
            return AgentRole.Leader;
        }

        /// <summary>
        /// Attempts to switch an agent to a new role.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <param name="newRole">Desired new role.</param>
        /// <returns>True if the role switch was successful; otherwise false.</returns>
        public bool TrySwitchRole(string agentId, AgentRole newRole)
        {
            if (string.IsNullOrEmpty(agentId) || !assignments.ContainsKey(agentId)) return false;
            if (!capabilities.ContainsKey(agentId)) return false;

            RoleAssignment assignment = assignments[agentId];
            if (Time.time - assignment.lastSwitchTime < assignment.switchCooldown) return false;
            if (assignment.currentRole == newRole) return false;

            float currentCap = GetCapabilityScore(agentId, assignment.currentRole);
            float newCap = GetCapabilityScore(agentId, newRole);
            if (newCap < currentCap - minimumCapabilityGap) return false;

            assignment.previousRole = assignment.currentRole;
            assignment.currentRole = newRole;
            assignment.lastSwitchTime = Time.time;
            assignments[agentId] = assignment;
            return true;
        }

        /// <summary>
        /// Evaluates all agents and automatically reassigns roles if beneficial.
        /// </summary>
        public void EvaluateAndReassign()
        {
            List<string> agentIds = new List<string>(assignments.Keys);
            foreach (string agentId in agentIds)
            {
                if (!assignments.ContainsKey(agentId) || !capabilities.ContainsKey(agentId)) continue;
                AgentRole bestRole = FindBestRole(agentId);
                if (bestRole != assignments[agentId].currentRole)
                {
                    TrySwitchRole(agentId, bestRole);
                }
            }
        }

        /// <summary>
        /// Finds the best role for an agent based on capability scores.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <returns>The best suited role for the agent.</returns>
        public AgentRole FindBestRole(string agentId)
        {
            if (!capabilities.ContainsKey(agentId)) return AgentRole.Leader;
            AgentRole best = AgentRole.Leader;
            float bestScore = 0f;
            foreach (RoleCapability cap in capabilities[agentId])
            {
                if (cap.isTrained && cap.score > bestScore)
                {
                    bestScore = cap.score;
                    best = cap.role;
                }
            }
            return best;
        }

        /// <summary>
        /// Gets the capability score for a specific agent and role.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <param name="role">The role to evaluate.</param>
        /// <returns>Capability score between 0 and 1.</returns>
        public float GetCapabilityScore(string agentId, AgentRole role)
        {
            if (!capabilities.ContainsKey(agentId)) return 0f;
            RoleCapability cap = capabilities[agentId].FirstOrDefault(c => c.role == role);
            return cap.isTrained ? cap.score : 0f;
        }
    }
}
