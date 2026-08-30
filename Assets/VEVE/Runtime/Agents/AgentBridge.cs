using System;
using System.Collections.Generic;
using System.Threading;
using TTask = System.Threading.Tasks.Task;
using UnityEngine;
using VEVE.Agentic;

namespace VEVE.Agents
{
    /// <summary>
    /// Frame-batched bridge between the C# hot path and asynchronous cognition
    /// services. The first decision for any agent is always satisfied
    /// synchronously by the local planner, so Python latency or absence can
    /// never stall a game frame. Deep plans arriving from the sidecar are
    /// applied in LateUpdate via a lock-protected pending queue.
    /// </summary>
    public sealed class AgentBridge : MonoBehaviour
    {
        public static AgentBridge Instance { get; private set; }

        [SerializeField] private float replanInterval = 1.0f;
        [SerializeField] private float pythonTimeoutSeconds = 2.5f;
        [SerializeField] private bool enablePythonClient = false;

        private PythonAgentCognitionClient python;
        private LocalHeuristicCognition local;
        private readonly Dictionary<int, PlanEntry> cache = new Dictionary<int, PlanEntry>();
        private readonly Dictionary<int, float> lastAsyncRequest = new Dictionary<int, float>();
        private readonly Queue<PendingPlan> pendingPlans = new Queue<PendingPlan>();

        /// <summary>
        /// Global kill-switch for the bridge decision path (used by tests and
        /// debug tooling to fall back to the legacy in-agent heuristic).
        /// </summary>
        public static bool UseBridgeDecisionPath = true;

        private sealed class PlanEntry
        {
            public BehaviorPlan plan;
            public bool inFlight;
        }

        private struct PendingPlan
        {
            public int agentId;
            public BehaviorPlan plan;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            local = new LocalHeuristicCognition();
            python = new PythonAgentCognitionClient { Enabled = enablePythonClient };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam != null) AgentViewContext.Position = cam.transform.position;
        }

        private void LateUpdate()
        {
            while (true)
            {
                PendingPlan pending;
                lock (pendingPlans)
                {
                    if (pendingPlans.Count == 0) break;
                    pending = pendingPlans.Dequeue();
                }
                if (cache.TryGetValue(pending.agentId, out PlanEntry entry))
                    entry.plan = pending.plan;
            }
        }

        /// <summary>
        /// Produces a decision for the agent this frame, guaranteed never to
        /// block. Requests an asynchronous deep replan in the background when
        /// the cached plan is stale and the primary service is available.
        /// </summary>
        public bool TryGetDecision(AutonomousAgent agent, out AutonomousAgent.DecisionResult result)
        {
            result = default;
            if (agent == null) return false;

            int id = agent.GetInstanceID();
            AgentCognitionInput snapshot = BuildInput(agent);

            if (!cache.TryGetValue(id, out PlanEntry entry))
            {
                entry = new PlanEntry();
                cache[id] = entry;
            }

            if (entry.plan == null || entry.plan.IsExpired)
            {
                entry.plan = local.Plan(snapshot);
                RequestAsyncReplanIfAvailable(id, entry, snapshot);
            }
            else
            {
                RequestAsyncReplanIfAvailable(id, entry, snapshot);
            }

            result = ToDecision(snapshot, entry.plan);
            return true;
        }

        private AgentCognitionInput BuildInput(AutonomousAgent agent)
        {
            AutonomousAgent.PerceptionResult perception = agent.LastPerception;
            HealthComponent health = agent.GetComponent<HealthComponent>();
            Weapon weapon = agent.GetComponent<Weapon>();

            float distance = float.IsPositiveInfinity(perception.distance) ? 0f : perception.distance;

            return new AgentCognitionInput
            {
                agentInstanceId = agent.GetInstanceID(),
                position = agent.transform.position,
                forward = agent.transform.forward,
                targetPosition = perception.target != null ? perception.target.transform.position : Vector3.zero,
                target = perception.target,
                targetInstanceID = perception.target != null ? perception.target.GetInstanceID() : 0,
                targetVisibility = perception.visibilityScore,
                distanceToTarget = distance,
                healthRatio = health != null ? health.HealthPercentage : 1f,
                roundsRemaining = weapon != null ? weapon.RoundsRemaining : -1,
                teamId = agent.TeamId,
                lod = agent.CurrentLOD
            };
        }

        private void RequestAsyncReplanIfAvailable(int id, PlanEntry entry, AgentCognitionInput snapshot)
        {
            if (!python.IsAvailable || entry.inFlight) return;

            float now = Time.unscaledTime;
            if (lastAsyncRequest.TryGetValue(id, out float last) && now - last < replanInterval) return;

            lastAsyncRequest[id] = now;
            entry.inFlight = true;
            _ = QueryPythonAsync(id, entry, snapshot);
        }

        private async TTask QueryPythonAsync(int id, PlanEntry entry, AgentCognitionInput snapshot)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(pythonTimeoutSeconds));
            try
            {
                BehaviorPlan plan = await python.PlanAsync(snapshot, cts.Token);
                if (plan != null && !plan.IsExpired)
                {
                    lock (pendingPlans)
                    {
                        pendingPlans.Enqueue(new PendingPlan { agentId = id, plan = plan });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[AgentBridge] Python sidecar plan failed ({e.GetType().Name}); local planner remains authoritative.");
            }
            finally
            {
                entry.inFlight = false;
            }
        }

        private static AutonomousAgent.DecisionResult ToDecision(AgentCognitionInput snapshot, BehaviorPlan plan)
        {
            var result = new AutonomousAgent.DecisionResult
            {
                nextState = AutonomousAgent.AgentState.Idle,
                targetPosition = snapshot.position,
                priorityTarget = null,
                urgency = 0f
            };

            if (plan?.steps == null || plan.steps.Length == 0) return result;

            BehaviorStep step = plan.steps[0];
            switch (step.op)
            {
                case BehaviorOp.FireAt:
                case BehaviorOp.Suppress:
                case BehaviorOp.Flank:
                case BehaviorOp.TakeCover:
                    result.nextState = AutonomousAgent.AgentState.Combat;
                    break;
                case BehaviorOp.MoveTo:
                    result.nextState = AutonomousAgent.AgentState.Patrolling;
                    break;
                case BehaviorOp.Investigate:
                    result.nextState = AutonomousAgent.AgentState.Investigating;
                    break;
                case BehaviorOp.Retreat:
                    result.nextState = AutonomousAgent.AgentState.Fleeing;
                    break;
                case BehaviorOp.HealAlly:
                case BehaviorOp.ReviveCasualty:
                case BehaviorOp.Callout:
                    result.nextState = AutonomousAgent.AgentState.Supporting;
                    break;
                case BehaviorOp.Reload:
                    result.nextState = AutonomousAgent.AgentState.Idle;
                    break;
                default:
                    result.nextState = AutonomousAgent.AgentState.Idle;
                    break;
            }

            if (step.position != Vector3.zero)
                result.targetPosition = step.position;

            if (step.targetInstanceID != 0 &&
                snapshot.target != null &&
                step.targetInstanceID == snapshot.targetInstanceID)
            {
                result.priorityTarget = snapshot.target;
            }

            result.urgency = step.value;
            return result;
        }
    }
}
