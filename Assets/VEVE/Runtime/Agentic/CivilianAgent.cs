using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Civilian agent with flee, hide behaviors and panic propagation mechanics.
    /// </summary>
    public class CivilianAgent : AutonomousAgent
    {
        [Header("Panic System")]
        [SerializeField] protected float panicThreshold = 0.6f;
        [SerializeField] protected float panicDecayRate = 0.1f;
        [SerializeField] protected float panicInfluenceRadius = 15f;
        [SerializeField] protected float maxPanic = 1f;

        [Header("Flee Behavior")]
        [SerializeField] protected float fleeSpeed = 6f;
        [SerializeField] protected float fleeDistance = 30f;
        [SerializeField] protected LayerMask obstacleMask;

        [Header("Hide Behavior")]
        [SerializeField] protected float hideSearchRadius = 25f;
        [SerializeField] protected LayerMask hideMask;

        /// <summary>
        /// Gets or sets the current panic level.
        /// </summary>
        public float PanicLevel { get; protected set; }

        /// <summary>
        /// Gets whether the civilian is currently panicking.
        /// </summary>
        public bool IsPanicked => PanicLevel >= panicThreshold;

        /// <summary>
        /// Gets or sets whether the civilian is hidden.
        /// </summary>
        public bool IsHidden { get; protected set; }

        /// <summary>
        /// Gets or sets the current flee target.
        /// </summary>
        public Vector3 FleeTarget { get; protected set; }

        /// <summary>
        /// Gets or sets the current hide position.
        /// </summary>
        public Vector3 HidePosition { get; protected set; }

        private List<CivilianAgent> nearbyCivilians = new List<CivilianAgent>();
        private float panicTimer;
        private float stateTimer;
        private bool hasFleeTarget;

        protected override void Awake()
        {
            base.Awake();
            if (selfTransform == null) selfTransform = transform;
            PanicLevel = 0f;
        }

        protected override void Update()
        {
            base.Update();
            panicTimer += Time.deltaTime;
            if (panicTimer >= 0.5f)
            {
                panicTimer = 0f;
                UpdatePanicState();
                PropagatePanic();
            }
            DecayPanic();
        }

        protected override void DecisionCycle()
        {
            base.DecisionCycle();

            if (LastPerception.target != null && LastPerception.visibilityScore > 0.2f)
            {
                IncreasePanic(0.4f);
            }

            DecisionResult decision = LastDecision;
            if (IsPanicked && CurrentState != AgentState.Fleeing && CurrentState != AgentState.Hiding)
            {
                decision.nextState = AgentState.Fleeing;
                FleeTarget = CalculateFleeTarget();
                hasFleeTarget = true;
            }
            else if (!IsPanicked && (CurrentState == AgentState.Fleeing || CurrentState == AgentState.Hiding))
            {
                decision.nextState = AgentState.Idle;
                IsHidden = false;
                hasFleeTarget = false;
            }
            LastDecision = decision;
        }

        protected override void ActionCycle()
        {
            switch (CurrentState)
            {
                case AgentState.Idle:
                    IdleBehavior();
                    break;
                case AgentState.Fleeing:
                    FleeBehavior();
                    break;
                case AgentState.Hiding:
                    HideBehavior();
                    break;
                default:
                    base.ActionCycle();
                    break;
            }
        }

        private void IdleBehavior()
        {
            if (IsPanicked)
            {
                CurrentState = AgentState.Fleeing;
                FleeTarget = CalculateFleeTarget();
                hasFleeTarget = true;
            }
        }

        private void FleeBehavior()
        {
            if (!hasFleeTarget || Vector3.Distance(selfTransform.position, FleeTarget) < 2f)
            {
                FleeTarget = CalculateFleeTarget();
            }

            Vector3 direction = (FleeTarget - selfTransform.position).normalized;
            direction.y = 0f;
            selfTransform.position += direction * fleeSpeed * Time.deltaTime;

            selfTransform.rotation = Quaternion.LookRotation(direction);

            stateTimer += Time.deltaTime;
            if (stateTimer >= 5f && !IsPanicked)
            {
                CurrentState = AgentState.Hiding;
                HidePosition = FindHidePosition();
                stateTimer = 0f;
            }
            else if (stateTimer >= 8f)
            {
                CurrentState = AgentState.Hiding;
                HidePosition = FindHidePosition();
                stateTimer = 0f;
            }
        }

        private void HideBehavior()
        {
            if (IsPanicked)
            {
                CurrentState = AgentState.Fleeing;
                FleeTarget = CalculateFleeTarget();
                hasFleeTarget = true;
                return;
            }

            if (Vector3.Distance(selfTransform.position, HidePosition) > 1f)
            {
                Vector3 direction = (HidePosition - selfTransform.position).normalized;
                direction.y = 0f;
                selfTransform.position += direction * (fleeSpeed * 0.5f) * Time.deltaTime;
                selfTransform.rotation = Quaternion.LookRotation(direction);
            }
            else
            {
                IsHidden = true;
            }
        }

        private Vector3 CalculateFleeTarget()
        {
            Vector3 threatDirection = Vector3.zero;
            if (LastPerception.target != null)
            {
                threatDirection = (selfTransform.position - LastPerception.target.transform.position).normalized;
            }

            Vector3 fleeDirection = threatDirection != Vector3.zero ? threatDirection : Random.insideUnitSphere.normalized;
            fleeDirection.y = 0f;
            Vector3 target = selfTransform.position + fleeDirection * fleeDistance;
            target.y = selfTransform.position.y;
            return target;
        }

        private Vector3 FindHidePosition()
        {
            Collider[] hits = Physics.OverlapSphere(selfTransform.position, hideSearchRadius, hideMask);
            Vector3 bestPosition = selfTransform.position;
            float bestScore = Mathf.NegativeInfinity;

            foreach (Collider hit in hits)
            {
                Vector3 toObject = (hit.transform.position - selfTransform.position).normalized;
                Vector3 hidePos = selfTransform.position - toObject * 3f + Vector3.up * 0.5f;
                float score = Vector3.Distance(selfTransform.position, hidePos);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPosition = hidePos;
                }
            }

            if (bestScore <= 0f)
            {
                bestPosition = selfTransform.position + Random.insideUnitSphere * 10f;
                bestPosition.y = selfTransform.position.y;
            }

            return bestPosition;
        }

        /// <summary>
        /// Increases the civilian's panic level.
        /// </summary>
        /// <param name="amount">Amount of panic to add.</param>
        public virtual void IncreasePanic(float amount)
        {
            PanicLevel = Mathf.Clamp(PanicLevel + amount, 0f, maxPanic);
        }

        /// <summary>
        /// Decreases panic over time.
        /// </summary>
        protected virtual void DecayPanic()
        {
            if (PanicLevel > 0f)
            {
                PanicLevel = Mathf.Clamp(PanicLevel - panicDecayRate * Time.deltaTime, 0f, maxPanic);
            }
        }

        /// <summary>
        /// Updates the panic state based on current conditions.
        /// </summary>
        protected virtual void UpdatePanicState()
        {
            if (LastPerception.target != null && LastPerception.visibilityScore > 0.3f)
            {
                IncreasePanic(0.2f);
            }
        }
        protected virtual void PropagatePanic()
        {
            Collider[] hits = Physics.OverlapSphere(selfTransform.position, panicInfluenceRadius);
            foreach (Collider hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (hit.TryGetComponent(out CivilianAgent civilian))
                {
                    if (IsPanicked && !civilian.IsPanicked)
                    {
                        civilian.IncreasePanic(PanicLevel * 0.3f);
                    }
                }
            }
        }

        /// <summary>
        /// Discovers nearby civilians for panic propagation.
        /// </summary>
        public virtual void DiscoverNearbyCivilians()
        {
            nearbyCivilians.Clear();
            Collider[] hits = Physics.OverlapSphere(selfTransform.position, panicInfluenceRadius);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out CivilianAgent civilian) && civilian != this)
                {
                    nearbyCivilians.Add(civilian);
                }
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, panicInfluenceRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hideSearchRadius);
            if (IsHidden)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(HidePosition, 0.5f);
            }
        }
    }
}
