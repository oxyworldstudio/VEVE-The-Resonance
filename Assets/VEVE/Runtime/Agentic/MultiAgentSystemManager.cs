using UnityEngine;
using VEVE.Agentic;
using System.Collections.Generic;

namespace VEVE.Agentic
{
    /// <summary>
    /// Central orchestrator for all autonomous agents in the simulation.
    /// Manages agent registration, updates, coordination, and lifecycle.
    /// </summary>
    public sealed class MultiAgentSystemManager : MonoBehaviour
    {
        [SerializeField] private int maxAgents = 64;
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private bool enableCoordination = true;
        [SerializeField] private bool enableCommunication = true;

        public static MultiAgentSystemManager Instance { get; private set; }

        private readonly List<AutonomousAgent> registeredAgents = new List<AutonomousAgent>();
        private readonly Dictionary<int, AgentTeam> teams = new Dictionary<int, AgentTeam>();
        private float updateTimer;

        public int RegisteredAgentCount => registeredAgents.Count;
        public int ActiveTeamCount => teams.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateAllAgents();
            }

            if (enableCommunication)
            {
                EventBus.ProcessQueue();
            }
        }

        public void RegisterAgent(AutonomousAgent agent)
        {
            if (agent == null || registeredAgents.Contains(agent)) return;
            if (registeredAgents.Count >= maxAgents)
            {
                UnityEngine.Debug.LogWarning("VEVE Multi-Agent System: maximum agent limit reached.");
                return;
            }
            registeredAgents.Add(agent);
        }

        public void UnregisterAgent(AutonomousAgent agent)
        {
            if (agent == null) return;
            registeredAgents.Remove(agent);
            RemoveFromTeam(agent);
        }

        public void AssignToTeam(AutonomousAgent agent, int teamId)
        {
            if (agent == null) return;

            RemoveFromTeam(agent);

            if (!teams.TryGetValue(teamId, out AgentTeam team))
            {
                team = new AgentTeam { teamId = teamId };
                teams[teamId] = team;
            }

            team.AddMember(agent);
            agent.TeamId = teamId;
        }

        public void RemoveFromTeam(AutonomousAgent agent)
        {
            if (agent == null) return;

            foreach (var kvp in teams)
            {
                if (kvp.Value.Contains(agent))
                {
                    kvp.Value.RemoveMember(agent);
                    agent.TeamId = -1;
                    break;
                }
            }
        }

        public AgentTeam GetTeam(int teamId)
        {
            teams.TryGetValue(teamId, out AgentTeam team);
            return team;
        }

        public List<AutonomousAgent> GetAgentsInRadius(Vector3 position, float radius)
        {
            var result = new List<AutonomousAgent>();
            foreach (var agent in registeredAgents)
            {
                if (agent != null && Vector3.Distance(agent.transform.position, position) <= radius)
                {
                    result.Add(agent);
                }
            }
            return result;
        }

        public AutonomousAgent FindNearestAgent(Vector3 position, float maxDistance = 50f)
        {
            AutonomousAgent nearest = null;
            float nearestDistance = maxDistance;

            foreach (var agent in registeredAgents)
            {
                if (agent == null) continue;
                float distance = Vector3.Distance(agent.transform.position, position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = agent;
                }
            }

            return nearest;
        }

        private void UpdateAllAgents()
        {
            for (int i = registeredAgents.Count - 1; i >= 0; i--)
            {
                var agent = registeredAgents[i];
                if (agent == null)
                {
                    registeredAgents.RemoveAt(i);
                    continue;
                }

                if (enableCoordination)
                {
                    agent.CoordinationUpdate();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>
    /// Represents a team of autonomous agents with shared objectives and coordination.
    /// </summary>
    public sealed class AgentTeam
    {
        public int teamId { get; internal set; }
        public AutonomousAgent leader { get; private set; }
        public int memberCount => members.Count;

        private readonly List<AutonomousAgent> members = new List<AutonomousAgent>();
        private readonly List<Goal> sharedGoals = new List<Goal>();

        public void AddMember(AutonomousAgent agent)
        {
            if (agent == null || members.Contains(agent)) return;
            members.Add(agent);

            if (leader == null)
            {
                leader = agent;
            }
        }

        public void RemoveMember(AutonomousAgent agent)
        {
            if (agent == null) return;
            members.Remove(agent);

            if (leader == agent)
            {
                leader = members.Count > 0 ? members[0] : null;
            }
        }

        public bool Contains(AutonomousAgent agent)
        {
            return agent != null && members.Contains(agent);
        }

        public void SetLeader(AutonomousAgent agent)
        {
            if (agent != null && members.Contains(agent))
            {
                leader = agent;
            }
        }

        public void AddSharedGoal(Goal goal)
        {
            if (goal != null && !sharedGoals.Contains(goal))
            {
                sharedGoals.Add(goal);
            }
        }

        public List<AutonomousAgent> GetMembers()
        {
            return new List<AutonomousAgent>(members);
        }

        public List<Goal> GetSharedGoals()
        {
            return new List<Goal>(sharedGoals);
        }
    }
}
