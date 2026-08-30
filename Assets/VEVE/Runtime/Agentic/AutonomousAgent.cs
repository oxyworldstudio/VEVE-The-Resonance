using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Base autonomous agent MonoBehaviour implementing perception, decision, and action cycles.
    /// </summary>
    public class AutonomousAgent : MonoBehaviour
    {
        [Header("Perception")]
        [SerializeField] protected float perceptionRadius = 50f;
        [SerializeField] protected float fieldOfViewAngle = 110f;
        [SerializeField] protected LayerMask perceptionMask;
        [SerializeField] protected float updateInterval = 0.1f;

        /// <summary>
        /// Current cognitive state of the agent.
        /// </summary>
        public enum AgentState
        {
            Idle,
            Patrolling,
            Investigating,
            Combat,
            Fleeing,
            Hiding,
            Supporting,
            Dead
        }

        /// <summary>
        /// Serializable container for perception raycast hits.
        /// </summary>
        [System.Serializable]
        public struct PerceptionResult
        {
            public GameObject target;
            public float distance;
            public Vector3 direction;
            public float visibilityScore;
        }

        /// <summary>
        /// Serializable container for agent decisions.
        /// </summary>
        [System.Serializable]
        public struct DecisionResult
        {
            public AgentState nextState;
            public Vector3 targetPosition;
            public GameObject priorityTarget;
            public float urgency;
        }

        /// <summary>
        /// Gets or sets the current agent state.
        /// </summary>
        public AgentState CurrentState { get; protected set; } = AgentState.Idle;

        /// <summary>
        /// Gets the last perception result.
        /// </summary>
        public PerceptionResult LastPerception { get; protected set; }

        /// <summary>
        /// Gets or sets the last decision result.
        /// </summary>
        public DecisionResult LastDecision { get; set; }

        /// <summary>
        /// Gets or sets the team identifier for this agent.
        /// </summary>
        public int TeamId { get; set; } = -1;

        /// <summary>
        /// Reference to the agent's transform, cached for performance.
        /// </summary>
        protected Transform selfTransform;

        /// <summary>
        /// Timer for perception cycle updates.
        /// </summary>
        protected float perceptionTimer;

        protected virtual void Awake()
        {
            selfTransform = transform;
        }

        protected virtual void Update()
        {
            perceptionTimer += Time.deltaTime;
            if (perceptionTimer >= updateInterval)
            {
                perceptionTimer = 0f;
                PerceptionCycle();
                DecisionCycle();
            }
            ActionCycle();
        }

        /// <summary>
        /// Perception cycle: gathers sensory data from the environment.
        /// </summary>
        protected virtual void PerceptionCycle()
        {
            Collider[] hits = Physics.OverlapSphere(selfTransform.position, perceptionRadius, perceptionMask);
            float bestScore = 0f;
            GameObject bestTarget = null;
            float bestDistance = Mathf.Infinity;
            Vector3 bestDirection = Vector3.forward;

            foreach (Collider hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                Vector3 direction = (hit.transform.position - selfTransform.position).normalized;
                float distance = Vector3.Distance(selfTransform.position, hit.transform.position);

                if (Vector3.Angle(selfTransform.forward, direction) > fieldOfViewAngle * 0.5f) continue;

                if (Physics.Raycast(selfTransform.position, direction, out RaycastHit rayHit, distance, perceptionMask))
                {
                    if (rayHit.collider.gameObject == hit.gameObject)
                    {
                        float visibilityScore = 1f - (distance / perceptionRadius);
                        if (visibilityScore > bestScore)
                        {
                            bestScore = visibilityScore;
                            bestTarget = hit.gameObject;
                            bestDistance = distance;
                            bestDirection = direction;
                        }
                    }
                }
            }

            LastPerception = new PerceptionResult
            {
                target = bestTarget,
                distance = bestDistance,
                direction = bestDirection,
                visibilityScore = bestScore
            };
        }

        /// <summary>
        /// Decision cycle: processes perception data to choose behavior.
        /// </summary>
        protected virtual void DecisionCycle()
        {
            DecisionResult decision = new DecisionResult
            {
                nextState = CurrentState,
                targetPosition = selfTransform.position,
                priorityTarget = null,
                urgency = 0f
            };

            if (LastPerception.target != null && LastPerception.visibilityScore > 0.3f)
            {
                decision.nextState = AgentState.Combat;
                decision.priorityTarget = LastPerception.target;
                decision.targetPosition = LastPerception.target.transform.position;
                decision.urgency = LastPerception.visibilityScore;
            }

            LastDecision = decision;
            CurrentState = decision.nextState;
        }

        /// <summary>
        /// Action cycle: executes the chosen behavior.
        /// </summary>
        protected virtual void ActionCycle()
        {
        }

        /// <summary>
        /// Coordination update: handles team communication and coordination tasks.
        /// </summary>
        public virtual void CoordinationUpdate() { }

        /// <summary>
        /// Triggers a perception cycle update.
        /// </summary>
        public virtual void PerceptionUpdate() => PerceptionCycle();

        /// <summary>
        /// Triggers a decision cycle update.
        /// </summary>
        public virtual void DecisionUpdate() => DecisionCycle();

        /// <summary>
        /// Triggers an action cycle update.
        /// </summary>
        public virtual void ActionUpdate() => ActionCycle();

        /// <summary>
        /// Draws gizmos for debugging perception.
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, perceptionRadius);

            Vector3 leftRay = Quaternion.Euler(0f, -fieldOfViewAngle * 0.5f, 0f) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0f, fieldOfViewAngle * 0.5f, 0f) * transform.forward;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + leftRay * perceptionRadius);
            Gizmos.DrawLine(transform.position, transform.position + rightRay * perceptionRadius);
        }
    }
}
