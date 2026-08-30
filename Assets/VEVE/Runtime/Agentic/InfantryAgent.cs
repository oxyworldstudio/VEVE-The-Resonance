using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Autonomous infantry agent with tactical movement, cover seeking, and engagement behaviors.
    /// </summary>
    public class InfantryAgent : AutonomousAgent
    {
        [Header("Tactical Movement")]
        [SerializeField] protected float moveSpeed = 5f;
        [SerializeField] protected float sprintSpeed = 8f;
        [SerializeField] protected float strafeSpeed = 3f;
        [SerializeField] protected float rotationSpeed = 180f;

        [Header("Cover Seeking")]
        [SerializeField] protected float coverSearchRadius = 20f;
        [SerializeField] protected LayerMask coverMask;
        [SerializeField] protected float coverCrouchDistance = 2f;

        [Header("Engagement")]
        [SerializeField] protected float engagementRange = 100f;
        [SerializeField] protected float optimalRange = 30f;
        [SerializeField] protected float aimAccuracy = 0.8f;
        [SerializeField] protected float fireRate = 0.2f;
        [SerializeField] protected float reloadTime = 2.5f;

        /// <summary>
        /// Serializable container for cover points.
        /// </summary>
        [System.Serializable]
        public struct CoverPoint
        {
            public Vector3 position;
            public Vector3 coverNormal;
            public float quality;
        }

        /// <summary>
        /// Gets or sets whether the infantry is currently reloading.
        /// </summary>
        public bool IsReloading { get; protected set; }

        /// <summary>
        /// Gets or sets the current ammunition count.
        /// </summary>
        public int CurrentAmmo { get; protected set; } = 30;

        /// <summary>
        /// Maximum ammunition capacity.
        /// </summary>
        public int MaxAmmo { get; protected set; } = 30;

        /// <summary>
        /// Gets or sets the current health.
        /// </summary>
        public float Health { get; protected set; } = 100f;

        /// <summary>
        /// Maximum health value.
        /// </summary>
        public float MaxHealth { get; protected set; } = 100f;

        /// <summary>
        /// Gets or sets the current cover point.
        /// </summary>
        public CoverPoint CurrentCover { get; protected set; }

        protected float fireTimer;
        private Vector3 currentMoveTarget;
        private bool hasMoveTarget;

        protected override void Awake()
        {
            base.Awake();
            if (selfTransform == null) selfTransform = transform;
        }

        protected override void DecisionCycle()
        {
            base.DecisionCycle();

            DecisionResult decision = LastDecision;
            if (CurrentState == AgentState.Combat && decision.priorityTarget != null)
            {
                float distance = Vector3.Distance(selfTransform.position, decision.priorityTarget.transform.position);

                if (distance > optimalRange * 1.5f)
                {
                    decision.nextState = AgentState.Investigating;
                    decision.targetPosition = FindTacticalPosition(decision.priorityTarget.transform.position);
                }
                else if (distance < optimalRange * 0.5f && CurrentAmmo > 0)
                {
                    decision.nextState = AgentState.Combat;
                    decision.targetPosition = selfTransform.position;
                }
                else if (CurrentAmmo <= 0 && !IsReloading)
                {
                    IsReloading = true;
                    Invoke(nameof(FinishReload), reloadTime);
                }
            }
            else if (CurrentState == AgentState.Investigating)
            {
                if (!hasMoveTarget || Vector3.Distance(selfTransform.position, currentMoveTarget) < 1f)
                {
                    decision.nextState = AgentState.Idle;
                }
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
                case AgentState.Patrolling:
                    PatrolBehavior();
                    break;
                case AgentState.Investigating:
                    InvestigateBehavior();
                    break;
                case AgentState.Combat:
                    CombatBehavior();
                    break;
                case AgentState.Fleeing:
                    FleeBehavior();
                    break;
            }
        }

        private void IdleBehavior()
        {
            if (LastDecision.priorityTarget != null)
            {
                CurrentState = AgentState.Combat;
            }
            else
            {
                RotateTowards(LastDecision.targetPosition);
            }
        }

        private void PatrolBehavior()
        {
            if (LastDecision.priorityTarget != null)
            {
                CurrentState = AgentState.Combat;
                return;
            }

            if (!hasMoveTarget || Vector3.Distance(selfTransform.position, currentMoveTarget) < 1f)
            {
                currentMoveTarget = selfTransform.position + Random.insideUnitSphere * 10f;
                currentMoveTarget.y = selfTransform.position.y;
                hasMoveTarget = true;
            }

            MoveTowards(currentMoveTarget, moveSpeed);
        }

        private void InvestigateBehavior()
        {
            if (LastDecision.priorityTarget != null)
            {
                CurrentState = AgentState.Combat;
                return;
            }

            if (hasMoveTarget)
            {
                MoveTowards(currentMoveTarget, strafeSpeed);
                RotateTowards(currentMoveTarget);
            }
        }

        protected virtual void CombatBehavior()
        {
            if (LastDecision.priorityTarget == null) return;

            Transform target = LastDecision.priorityTarget.transform;
            float distance = Vector3.Distance(selfTransform.position, target.position);

            if (distance > optimalRange * 1.2f)
            {
                MoveTowards(target.position, moveSpeed);
            }
            else if (distance < optimalRange * 0.6f)
            {
                Vector3 away = (selfTransform.position - target.position).normalized;
                MoveTowards(selfTransform.position + away, strafeSpeed);
            }

            RotateTowards(target.position);

            fireTimer += Time.deltaTime;
            if (fireTimer >= fireRate && CurrentAmmo > 0 && !IsReloading)
            {
                FireWeapon(target);
                fireTimer = 0f;
            }
        }

        private void FleeBehavior()
        {
            if (LastDecision.targetPosition != Vector3.zero)
            {
                MoveTowards(LastDecision.targetPosition, sprintSpeed);
                RotateTowards(LastDecision.targetPosition);
            }
        }

        protected void MoveTowards(Vector3 target, float speed)
        {
            Vector3 direction = (target - selfTransform.position).normalized;
            direction.y = 0f;
            selfTransform.position += direction * speed * Time.deltaTime;
        }

        protected virtual void RotateTowards(Vector3 target)
        {
            Vector3 direction = (target - selfTransform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                selfTransform.rotation = Quaternion.RotateTowards(selfTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        protected void FireWeapon(Transform target)
        {
            CurrentAmmo--;
            float accuracyOffset = (1f - aimAccuracy) * 0.5f;
            Vector3 aimPoint = target.position + Random.insideUnitSphere * accuracyOffset;
            
            if (Physics.Raycast(selfTransform.position + Vector3.up * 1.5f, (aimPoint - (selfTransform.position + Vector3.up * 1.5f)).normalized, out RaycastHit hit, engagementRange))
            {
                Debug.DrawLine(selfTransform.position + Vector3.up * 1.5f, hit.point, Color.red, 0.1f);
            }
        }

        private void FinishReload()
        {
            CurrentAmmo = MaxAmmo;
            IsReloading = false;
        }

        private Vector3 FindTacticalPosition(Vector3 targetPosition)
        {
            Vector3 direction = (selfTransform.position - targetPosition).normalized;
            Vector3 tacticalPos = selfTransform.position + direction * optimalRange;
            tacticalPos.y = selfTransform.position.y;
            return tacticalPos;
        }

        /// <summary>
        /// Applies damage to the infantry agent.
        /// </summary>
        /// <param name="damage">Amount of damage to apply.</param>
        public void TakeDamage(float damage)
        {
            Health -= damage;
            if (Health <= 0f)
            {
                Health = 0f;
                CurrentState = AgentState.Dead;
            }
            else if (Health < 30f && CurrentState != AgentState.Fleeing)
            {
                CurrentState = AgentState.Fleeing;
                DecisionResult decision = LastDecision;
                decision.targetPosition = selfTransform.position + Random.insideUnitSphere * 20f;
                LastDecision = decision;
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, optimalRange);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, coverSearchRadius);
        }
    }
}
