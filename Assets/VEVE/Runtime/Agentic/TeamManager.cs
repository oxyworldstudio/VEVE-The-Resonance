using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines the types of teams in the coordination system.
    /// </summary>
    public enum TeamType { Alpha, Bravo, Charlie, Delta, Support, Recon }

    /// <summary>
    /// Represents the state of a team.
    /// </summary>
    public enum TeamState { Idle, Assembling, Moving, Engaging, Defending, Regrouping }

    /// <summary>
    /// Serializable container for team member information.
    /// </summary>
    [Serializable]
    public class TeamMember
    {
        /// <summary>Identifier of the agent.</summary>
        public string agentId;

        /// <summary>Current role within the team.</summary>
        public AgentRole role;

        /// <summary>Current health value.</summary>
        public float health;

        /// <summary>True if this member is the team leader.</summary>
        public bool isLeader;

        /// <summary>Current status of the team member.</summary>
        public string status;

        /// <summary>
        /// Initializes a new team member.
        /// </summary>
        /// <param name="id">Agent identifier.</param>
        /// <param name="role">Agent role.</param>
        public TeamMember(string id, AgentRole role)
        {
            agentId = id;
            this.role = role;
            health = 100f;
            isLeader = false;
            status = "Active";
        }
    }

    /// <summary>
    /// Represents a team of agents with leadership and coordination.
    /// </summary>
    [Serializable]
    public class Team
    {
        /// <summary>Unique identifier for the team.</summary>
        public string teamId;

        /// <summary>The type of team.</summary>
        public TeamType teamType;

        /// <summary>Current state of the team.</summary>
        public TeamState state;

        /// <summary>List of members in the team.</summary>
        public List<TeamMember> members;

        /// <summary>Current team objective.</summary>
        public string objective;

        /// <summary>Assembly point position.</summary>
        public Vector3 assemblyPoint;

        /// <summary>Timestamp of the last team decision.</summary>
        public float lastDecisionTime;

        /// <summary>
        /// Initializes a new team instance.
        /// </summary>
        /// <param name="id">Unique team identifier.</param>
        /// <param name="type">Type of team.</param>
        public Team(string id, TeamType type)
        {
            teamId = id;
            teamType = type;
            state = TeamState.Idle;
            members = new List<TeamMember>();
            objective = string.Empty;
            assemblyPoint = Vector3.zero;
            lastDecisionTime = 0f;
        }
    }

    /// <summary>
    /// Team formation, team leadership, and team-level decision making.
    /// </summary>
    public class TeamManager : MonoBehaviour
    {
        [SerializeField] private float decisionInterval = 3f;
        [SerializeField] private float regroupDistance = 20f;
        [SerializeField] private float leadershipTransferHealthThreshold = 30f;

        private Dictionary<string, Team> teams;
        private Dictionary<string, string> agentTeamMap;
        private float decisionTimer;

        /// <summary>
        /// Gets all registered teams.
        /// </summary>
        public IReadOnlyDictionary<string, Team> Teams => teams;

        /// <summary>
        /// Gets the mapping of agents to their teams.
        /// </summary>
        public IReadOnlyDictionary<string, string> AgentTeamMap => agentTeamMap;

        /// <summary>
        /// Initializes the team manager.
        /// </summary>
        protected virtual void Awake()
        {
            teams = new Dictionary<string, Team>();
            agentTeamMap = new Dictionary<string, string>();
        }

        /// <summary>
        /// Updates team-level decision making and leadership.
        /// </summary>
        protected virtual void Update()
        {
            decisionTimer += Time.deltaTime;
            if (decisionTimer >= decisionInterval)
            {
                decisionTimer = 0f;
                foreach (Team team in teams.Values)
                {
                    EvaluateTeamState(team);
                }
            }
        }

        /// <summary>
        /// Creates a new team.
        /// </summary>
        /// <param name="teamId">Unique identifier for the team.</param>
        /// <param name="type">Type of team to create.</param>
        /// <returns>The created team.</returns>
        public Team CreateTeam(string teamId, TeamType type)
        {
            if (string.IsNullOrEmpty(teamId)) return null;
            Team team = new Team(teamId, type);
            teams[teamId] = team;
            return team;
        }

        /// <summary>
        /// Disbands a team and removes all members.
        /// </summary>
        /// <param name="teamId">Identifier of the team to disband.</param>
        public void DisbandTeam(string teamId)
        {
            if (!teams.ContainsKey(teamId)) return;
            Team team = teams[teamId];
            foreach (TeamMember member in team.members)
            {
                if (agentTeamMap.ContainsKey(member.agentId))
                {
                    agentTeamMap.Remove(member.agentId);
                }
            }
            teams.Remove(teamId);
        }

        /// <summary>
        /// Adds an agent to a team.
        /// </summary>
        /// <param name="teamId">Identifier of the target team.</param>
        /// <param name="agentId">Identifier of the agent to add.</param>
        /// <param name="role">Initial role for the agent in the team.</param>
        /// <returns>True if the agent was added successfully; otherwise false.</returns>
        public bool AddAgentToTeam(string teamId, string agentId, AgentRole role)
        {
            if (!teams.ContainsKey(teamId)) return false;
            Team team = teams[teamId];
            if (team.members.Count >= 8) return false;

            TeamMember member = new TeamMember(agentId, role);
            if (team.members.Count == 0)
            {
                member.isLeader = true;
            }
            team.members.Add(member);
            agentTeamMap[agentId] = teamId;
            return true;
        }

        /// <summary>
        /// Removes an agent from its current team.
        /// </summary>
        /// <param name="agentId">Identifier of the agent to remove.</param>
        /// <returns>True if the agent was removed; otherwise false.</returns>
        public bool RemoveAgentFromTeam(string agentId)
        {
            if (!agentTeamMap.ContainsKey(agentId)) return false;
            string teamId = agentTeamMap[agentId];
            if (!teams.ContainsKey(teamId)) return false;

            Team team = teams[teamId];
            team.members.RemoveAll(m => m.agentId == agentId);
            agentTeamMap.Remove(agentId);

            if (team.members.Count == 0)
            {
                teams.Remove(teamId);
            }
            else if (team.members.Count > 0 && !team.members.Any(m => m.isLeader))
            {
                team.members[0].isLeader = true;
            }

            return true;
        }

        /// <summary>
        /// Gets the team that an agent belongs to.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <returns>The team the agent belongs to, or null if not found.</returns>
        public Team GetAgentTeam(string agentId)
        {
            if (!agentTeamMap.ContainsKey(agentId)) return null;
            string teamId = agentTeamMap[agentId];
            if (!teams.ContainsKey(teamId)) return null;
            return teams[teamId];
        }

        /// <summary>
        /// Updates team state and triggers decisions.
        /// </summary>
        /// <param name="team">The team to evaluate.</param>
        public void EvaluateTeamState(Team team)
        {
            if (team == null) return;
            team.lastDecisionTime = Time.time;

            if (team.members.Count == 0)
            {
                team.state = TeamState.Idle;
                return;
            }

            float avgHealth = team.members.Average(m => m.health);
            if (avgHealth < leadershipTransferHealthThreshold && team.members.Any(m => m.isLeader))
            {
                TransferLeadership(team);
            }

            if (team.members.Any(m => m.status == "Down"))
            {
                team.state = TeamState.Regrouping;
            }
        }

        /// <summary>
        /// Transfers leadership to the next most capable member.
        /// </summary>
        /// <param name="team">The team to update.</param>
        public void TransferLeadership(Team team)
        {
            if (team == null || team.members.Count == 0) return;
            TeamMember currentLeader = team.members.FirstOrDefault(m => m.isLeader);
            if (currentLeader == null) return;

            currentLeader.isLeader = false;
            TeamMember newLeader = team.members.OrderByDescending(m => m.health).First();
            newLeader.isLeader = true;
        }

        /// <summary>
        /// Sets a team-level objective.
        /// </summary>
        /// <param name="teamId">Identifier of the team.</param>
        /// <param name="objective">The new objective.</param>
        public void SetTeamObjective(string teamId, string objective)
        {
            if (!teams.ContainsKey(teamId)) return;
            teams[teamId].objective = objective;
        }

        /// <summary>
        /// Moves a team to an assembly point.
        /// </summary>
        /// <param name="teamId">Identifier of the team.</param>
        /// <param name="position">Target assembly position.</param>
        public void MoveTeamToAssembly(string teamId, Vector3 position)
        {
            if (!teams.ContainsKey(teamId)) return;
            Team team = teams[teamId];
            team.assemblyPoint = position;
            team.state = TeamState.Assembling;
        }

        /// <summary>
        /// Gets the leader of a team.
        /// </summary>
        /// <param name="teamId">Identifier of the team.</param>
        /// <returns>The team leader member, or null if not found.</returns>
        public TeamMember GetTeamLeader(string teamId)
        {
            if (!teams.ContainsKey(teamId)) return null;
            return teams[teamId].members.FirstOrDefault(m => m.isLeader);
        }

        /// <summary>
        /// Reports damage to a team member.
        /// </summary>
        /// <param name="agentId">Identifier of the agent.</param>
        /// <param name="damage">Amount of damage taken.</param>
        public void ReportMemberDamage(string agentId, float damage)
        {
            Team team = GetAgentTeam(agentId);
            if (team == null) return;
            TeamMember member = team.members.FirstOrDefault(m => m.agentId == agentId);
            if (member == null) return;

            member.health -= damage;
            if (member.health <= 0f)
            {
                member.health = 0f;
                member.status = "Down";
            }
        }
    }
}
