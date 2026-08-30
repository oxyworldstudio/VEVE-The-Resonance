using UnityEngine;
using VEVE.Agentic;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.Agentic
{
    /// <summary>
    /// Coordinator Agent responsible for managing communication between all agents,
    /// synchronizing workflows, and implementing quality control.
    /// </summary>
    public sealed class CoordinatorAgent : MonoBehaviour
    {
        [SerializeField] private float coordinationInterval = 0.5f;
        [SerializeField] private float qualityCheckInterval = 5f;
        [SerializeField] private bool enableQualityControl = true;

        public static CoordinatorAgent Instance { get; private set; }

        private readonly Dictionary<System.Type, object> agentRegistry = new Dictionary<System.Type, object>();
        private readonly List<System.Action> coordinationTasks = new List<System.Action>();
        private float coordinationTimer;
        private float qualityCheckTimer;
        private int totalErrorsDetected;
        private int totalCorrectionsApplied;

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
            coordinationTimer += Time.deltaTime;
            if (coordinationTimer >= coordinationInterval)
            {
                coordinationTimer = 0f;
                RunCoordinationCycle();
            }

            if (enableQualityControl)
            {
                qualityCheckTimer += Time.deltaTime;
                if (qualityCheckTimer >= qualityCheckInterval)
                {
                    qualityCheckTimer = 0f;
                    RunQualityCheck();
                }
            }
        }

        public void RegisterAgent<T>(T agent) where T : class
        {
            var type = typeof(T);
            if (!agentRegistry.ContainsKey(type))
            {
                agentRegistry[type] = agent;
                UnityEngine.Debug.Log($"[Coordinator] Registered agent: {type.Name}");
            }
        }

        public T GetAgent<T>() where T : class
        {
            var type = typeof(T);
            if (agentRegistry.TryGetValue(type, out var agent))
            {
                return agent as T;
            }
            return null;
        }

        public void AddCoordinationTask(System.Action task)
        {
            if (task != null && !coordinationTasks.Contains(task))
            {
                coordinationTasks.Add(task);
            }
        }

        public void RemoveCoordinationTask(System.Action task)
        {
            coordinationTasks.Remove(task);
        }

        private void RunCoordinationCycle()
        {
            foreach (var task in coordinationTasks.ToList())
            {
                try
                {
                    task?.Invoke();
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[Coordinator] Error in coordination task: {ex}");
                    totalErrorsDetected++;
                }
            }

            SynchronizeAgentStates();
            BroadcastSystemState();
        }

        private void SynchronizeAgentStates()
        {
            var manager = MultiAgentSystemManager.Instance;
            if (manager == null) return;

            foreach (var agent in manager.GetAgentsInRadius(Vector3.zero, 1000f))
            {
                if (agent == null) continue;
                agent.CoordinationUpdate();
            }
        }

        private void BroadcastSystemState()
        {
            var manager = MultiAgentSystemManager.Instance;
            if (manager == null) return;

            EventBus.PublishGlobal(new SystemStateEvent
            {
                registeredAgentCount = manager.RegisteredAgentCount,
                activeTeamCount = manager.ActiveTeamCount,
                timestamp = Time.time
            });
        }

        private void RunQualityCheck()
        {
            totalErrorsDetected = 0;
            totalCorrectionsApplied = 0;

            var manager = MultiAgentSystemManager.Instance;
            if (manager != null)
            {
                foreach (var agent in manager.GetAgentsInRadius(Vector3.zero, 1000f))
                {
                    if (agent == null) continue;

                    if (float.IsNaN(agent.transform.position.x) || float.IsInfinity(agent.transform.position.x))
                    {
                        UnityEngine.Debug.LogWarning($"[Coordinator] Invalid position detected for agent: {agent.name}");
                        totalErrorsDetected++;
                        agent.transform.position = Vector3.zero;
                        totalCorrectionsApplied++;
                    }
                }
            }

            if (totalErrorsDetected > 0)
            {
                UnityEngine.Debug.Log($"[Coordinator] Quality check: {totalErrorsDetected} errors detected, {totalCorrectionsApplied} corrections applied.");
            }
        }

        public System.Tuple<int, int> GetQualityMetrics()
        {
            return System.Tuple.Create(totalErrorsDetected, totalCorrectionsApplied);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>
    /// System state event for inter-agent communication.
    /// </summary>
    public sealed class SystemStateEvent : IEvent
    {
        public int registeredAgentCount { get; set; }
        public int activeTeamCount { get; set; }
        public float timestamp { get; set; }
    }
}
