using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Squad leader agent with command authority, task assignment, and coordination capabilities.
    /// </summary>
    public class SquadLeaderAgent : AutonomousAgent
    {
        [Header("Command")]
        [SerializeField] protected float commandRange = 100f;
        [SerializeField] protected float formationSpacing = 5f;
        [SerializeField] protected LayerMask friendlyMask;
        [SerializeField] protected float taskAssignmentInterval = 2f;

        /// <summary>
        /// Serializable container for squad orders.
        /// </summary>
        [System.Serializable]
        public struct SquadOrder
        {
            public OrderType type;
            public Vector3 targetPosition;
            public GameObject targetEntity;
            public float priority;
            public float issueTime;
        }

        /// <summary>
        /// Types of orders a squad leader can issue.
        /// </summary>
        public enum OrderType
        {
            Move,
            Attack,
            Defend,
            Flank,
            Regroup,
            Suppress,
            Hold
        }

        /// <summary>
        /// Gets or sets the list of assigned squad members.
        /// </summary>
        public List<InfantryAgent> SquadMembers { get; protected set; } = new List<InfantryAgent>();

        /// <summary>
        /// Gets or sets the current issued order.
        /// </summary>
        public SquadOrder CurrentOrder { get; protected set; }

        /// <summary>
        /// Gets or sets whether the squad is in formation.
        /// </summary>
        public bool InFormation { get; protected set; } = true;

        /// <summary>
        /// Gets or sets the command authority level.
        /// </summary>
        public float CommandAuthority { get; protected set; } = 1f;

        private float assignmentTimer;
        private Vector3 formationCenter;

        protected override void Awake()
        {
            base.Awake();
            if (selfTransform == null) selfTransform = transform;
            DiscoverSquadMembers();
        }

        protected override void Update()
        {
            base.Update();
            assignmentTimer += Time.deltaTime;
            if (assignmentTimer >= taskAssignmentInterval)
            {
                assignmentTimer = 0f;
                AssignTasks();
            }
            UpdateFormation();
        }

        protected override void DecisionCycle()
        {
            base.DecisionCycle();

            if (LastPerception.target != null && LastPerception.visibilityScore > 0.4f)
            {
                CurrentOrder = new SquadOrder
                {
                    type = OrderType.Attack,
                    targetPosition = LastPerception.target.transform.position,
                    targetEntity = LastPerception.target,
                    priority = 1f,
                    issueTime = Time.time
                };
                InFormation = false;
            }
            else if (CurrentOrder.type == OrderType.Attack && Time.time - CurrentOrder.issueTime > 10f)
            {
                CurrentOrder = new SquadOrder
                {
                    type = OrderType.Regroup,
                    targetPosition = selfTransform.position,
                    priority = 0.5f,
                    issueTime = Time.time
                };
                InFormation = true;
            }
        }

        protected override void ActionCycle()
        {
            if (CurrentState == AgentState.Combat && CurrentOrder.targetEntity != null)
            {
                RotateTowards(CurrentOrder.targetPosition);
            }
            else if (InFormation && formationCenter != Vector3.zero)
            {
                MoveTowards(formationCenter, 5f * 0.8f);
                RotateTowards(formationCenter);
            }
        }

        /// <summary>
        /// Discovers nearby friendly units to add to the squad.
        /// </summary>
        public virtual void DiscoverSquadMembers()
        {
            SquadMembers.Clear();
            Collider[] hits = Physics.OverlapSphere(selfTransform.position, commandRange, friendlyMask);
            foreach (Collider hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (hit.TryGetComponent(out InfantryAgent infantry))
                {
                    if (!SquadMembers.Contains(infantry))
                    {
                        SquadMembers.Add(infantry);
                    }
                }
            }
        }

        /// <summary>
        /// Assigns tactical tasks to squad members based on current order.
        /// </summary>
        public virtual void AssignTasks()
        {
            if (SquadMembers.Count == 0) return;

            for (int i = 0; i < SquadMembers.Count; i++)
            {
                if (SquadMembers[i] == null) continue;
                Vector3 assignedPosition = CalculateFormationPosition(i);
                SquadMembers[i].LastDecision = new AutonomousAgent.DecisionResult
                {
                    nextState = SquadMembers[i].CurrentState,
                    targetPosition = assignedPosition,
                    priorityTarget = CurrentOrder.targetEntity,
                    urgency = CurrentOrder.priority
                };
            }
        }

        /// <summary>
        /// Calculates formation position for a squad member index.
        /// </summary>
        /// <param name="index">Member index in the squad.</param>
        /// <returns>World position for formation.</returns>
        public virtual Vector3 CalculateFormationPosition(int index)
        {
            float angle = (index / (float)SquadMembers.Count) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * formationSpacing;
            return formationCenter + offset;
        }

        /// <summary>
        /// Issues a new order to the squad.
        /// </summary>
        /// <param name="order">The order to issue.</param>
        public virtual void IssueOrder(SquadOrder order)
        {
            CurrentOrder = order;
            InFormation = order.type == OrderType.Defend || order.type == OrderType.Hold;
            if (InFormation)
            {
                formationCenter = order.targetPosition;
            }
            AssignTasks();
        }

        private void UpdateFormation()
        {
            if (!InFormation) return;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var member in SquadMembers)
            {
                if (member != null)
                {
                    sum += member.transform.position;
                    count++;
                }
            }
            if (count > 0)
            {
                formationCenter = sum / count;
            }
        }

        private void MoveTowards(Vector3 target, float speed)
        {
            Vector3 direction = (target - selfTransform.position).normalized;
            direction.y = 0f;
            selfTransform.position += direction * speed * Time.deltaTime;
        }

        private void RotateTowards(Vector3 target)
        {
            Vector3 direction = (target - selfTransform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                selfTransform.rotation = Quaternion.RotateTowards(selfTransform.rotation, targetRotation, 180f * Time.deltaTime);
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, commandRange);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(formationCenter, 2f);
        }
    }
}
