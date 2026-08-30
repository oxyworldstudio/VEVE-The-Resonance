using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Support specialist agent with role-specific behaviors such as medical aid or suppression fire.
    /// </summary>
    public class SupportAgent : InfantryAgent
    {
        [Header("Support Role")]
        [SerializeField] protected SupportRole role = SupportRole.Medic;
        [SerializeField] protected float supportRange = 25f;
        [SerializeField] protected float healAmount = 25f;
        [SerializeField] protected float healRate = 10f;
        [SerializeField] protected int medkits = 3;
        [SerializeField] protected float suppressionAreaRadius = 15f;
        [SerializeField] protected float suppressionDuration = 3f;

        /// <summary>
        /// Support role types.
        /// </summary>
        public enum SupportRole
        {
            Medic,
            MachineGunner,
            Grenadier,
            Engineer
        }

        /// <summary>
        /// Gets or sets the current support role.
        /// </summary>
        public SupportRole CurrentRole { get => role; set => role = value; }

        /// <summary>
        /// Gets or sets remaining medkit count.
        /// </summary>
        public int MedkitsRemaining { get => medkits; protected set => medkits = value; }

        private float healTimer;
        private float suppressTimer;
        private GameObject currentHealTarget;

        protected override void DecisionCycle()
        {
            base.DecisionCycle();

            DecisionResult decision = LastDecision;
            if (role == SupportRole.Medic && decision.priorityTarget != null)
            {
                if (TryGetComponent(out HealthComponent health) && health.HealthPercentage < 0.5f)
                {
                    decision.nextState = AgentState.Supporting;
                    decision.targetPosition = selfTransform.position;
                    currentHealTarget = gameObject;
                }
                else
                {
                    Collider[] nearby = Physics.OverlapSphere(selfTransform.position, supportRange);
                    foreach (Collider hit in nearby)
                    {
                        if (hit.TryGetComponent(out HealthComponent targetHealth) && targetHealth.HealthPercentage < 0.5f)
                        {
                            decision.nextState = AgentState.Supporting;
                            decision.targetPosition = hit.transform.position;
                            currentHealTarget = hit.gameObject;
                            break;
                        }
                    }
                }
            }
            else if (role == SupportRole.MachineGunner && decision.priorityTarget != null)
            {
                decision.nextState = AgentState.Combat;
            }
            LastDecision = decision;
        }

        protected override void ActionCycle()
        {
            switch (CurrentState)
            {
                case AgentState.Supporting:
                    SupportBehavior();
                    break;
                case AgentState.Combat:
                    CombatBehavior();
                    break;
                default:
                    base.ActionCycle();
                    break;
            }
        }

        private void SupportBehavior()
        {
            if (currentHealTarget == null)
            {
                CurrentState = AgentState.Idle;
                return;
            }

            Vector3 direction = (currentHealTarget.transform.position - selfTransform.position).normalized;
            direction.y = 0f;

            if (Vector3.Distance(selfTransform.position, currentHealTarget.transform.position) > 3f)
            {
                selfTransform.position += direction * (moveSpeed * 0.5f) * Time.deltaTime;
            }

            RotateTowards(currentHealTarget.transform.position);

            healTimer += Time.deltaTime;
            if (healTimer >= 1f / healRate && medkits > 0)
            {
                if (currentHealTarget.TryGetComponent(out HealthComponent health))
                {
                    health.Heal(healAmount);
                    medkits--;
                }
                healTimer = 0f;
            }

            if (medkits <= 0 || (currentHealTarget.TryGetComponent(out HealthComponent h) && h.HealthPercentage >= 1f))
            {
                CurrentState = AgentState.Idle;
                currentHealTarget = null;
            }
        }

        protected override void CombatBehavior()
        {
            if (role == SupportRole.MachineGunner)
            {
                MachineGunBehavior();
            }
            else
            {
                base.CombatBehavior();
            }
        }

        private void MachineGunBehavior()
        {
            if (LastDecision.priorityTarget == null) return;

            Transform target = LastDecision.priorityTarget.transform;
            float distance = Vector3.Distance(selfTransform.position, target.position);

            if (distance > optimalRange)
            {
                MoveTowards(target.position, moveSpeed * 0.7f);
            }
            else if (distance < optimalRange * 0.8f)
            {
                Vector3 away = (selfTransform.position - target.position).normalized;
                MoveTowards(selfTransform.position + away, strafeSpeed * 0.8f);
            }

            selfTransform.LookAt(new Vector3(target.position.x, selfTransform.position.y, target.position.z));

            fireTimer += Time.deltaTime;
            if (fireTimer >= fireRate && CurrentAmmo > 0 && !IsReloading)
            {
                FireWeapon(target);
                fireTimer = 0f;
                ApplySuppression(target.position);
            }
        }

        private void ApplySuppression(Vector3 center)
        {
            Collider[] suppressed = Physics.OverlapSphere(center, suppressionAreaRadius);
            foreach (Collider hit in suppressed)
            {
                if (hit.gameObject == gameObject) continue;
                if (hit.TryGetComponent(out SupportAgent agent))
                {
                    suppressTimer = suppressionDuration;
                }
            }
        }

        /// <summary>
        /// Rotates towards target with support-specific accuracy.
        /// </summary>
        /// <param name="target">Target position to face.</param>
        protected override void RotateTowards(Vector3 target)
        {
            base.RotateTowards(target);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, supportRange);
            if (role == SupportRole.MachineGunner)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, suppressionAreaRadius);
            }
        }
    }
}
