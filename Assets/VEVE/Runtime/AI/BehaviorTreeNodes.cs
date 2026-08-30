using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.AI
{
    /// <summary>
    /// Executes child nodes in sequence. Fails on the first child that fails.
    /// </summary>
    public sealed class SequenceNode : CompositeNode
    {
        private int currentIndex;

        /// <summary>
        /// Ticks all children in order until one fails or all succeed.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The node execution status.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            for (currentIndex = 0; currentIndex < Children.Count; currentIndex++)
            {
                NodeStatus status = Children[currentIndex].Tick(blackboard);
                if (status != NodeStatus.Success) return status;
            }
            return NodeStatus.Success;
        }

        /// <summary>
        /// Aborts the currently running child node.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        public override void Abort(Blackboard blackboard)
        {
            if (currentIndex >= 0 && currentIndex < Children.Count)
                Children[currentIndex].Abort(blackboard);
        }
    }

    /// <summary>
    /// Executes child nodes until one succeeds. Returns failure if all children fail.
    /// </summary>
    public sealed class SelectorNode : CompositeNode
    {
        private int currentIndex;

        /// <summary>
        /// Ticks children until one succeeds or all fail.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The node execution status.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            for (currentIndex = 0; currentIndex < Children.Count; currentIndex++)
            {
                NodeStatus status = Children[currentIndex].Tick(blackboard);
                if (status == NodeStatus.Success) return NodeStatus.Success;
                if (status == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }
            }
            return NodeStatus.Failure;
        }

        /// <summary>
        /// Aborts the currently running child node.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        public override void Abort(Blackboard blackboard)
        {
            if (currentIndex >= 0 && currentIndex < Children.Count)
                Children[currentIndex].Abort(blackboard);
        }
    }

    /// <summary>
    /// Executes all child nodes simultaneously based on a completion policy.
    /// </summary>
    public sealed class ParallelNode : CompositeNode
    {
        /// <summary>
        /// Determines when the parallel node completes.
        /// </summary>
        public enum Policy { FirstFailure, AllSuccess }

        /// <summary>
        /// Gets or sets the execution policy.
        /// </summary>
        public Policy ExecutionPolicy { get; set; } = Policy.FirstFailure;

        /// <summary>
        /// Gets or sets the number of successes required when using FirstFailure policy.
        /// </summary>
        public int SuccessThreshold { get; set; } = 1;

        /// <summary>
        /// Gets or sets the number of failures required when using FirstFailure policy.
        /// </summary>
        public int FailureThreshold { get; set; } = 1;

        /// <summary>
        /// Ticks all children simultaneously.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The node execution status.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            int successCount = 0;
            int failureCount = 0;

            foreach (Node child in Children)
            {
                NodeStatus status = child.Tick(blackboard);
                if (status == NodeStatus.Success) successCount++;
                else if (status == NodeStatus.Failure) failureCount++;
            }

            if (ExecutionPolicy == Policy.FirstFailure && failureCount >= FailureThreshold)
                return NodeStatus.Failure;

            if (ExecutionPolicy == Policy.AllSuccess && successCount >= Children.Count)
                return NodeStatus.Success;

            if (ExecutionPolicy == Policy.AllSuccess && failureCount >= FailureThreshold)
                return NodeStatus.Failure;

            return NodeStatus.Running;
        }
    }

    /// <summary>
    /// Inverts the result of its child node. Success becomes Failure and vice versa.
    /// </summary>
    public sealed class InverterNode : DecoratorNode
    {
        /// <summary>
        /// Ticks the child and inverts the result.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The inverted node execution status.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (Child == null) return NodeStatus.Failure;
            NodeStatus status = Child.Tick(blackboard);
            return status == NodeStatus.Success ? NodeStatus.Failure : status == NodeStatus.Failure ? NodeStatus.Success : NodeStatus.Running;
        }

        /// <summary>
        /// Aborts the child node.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        public override void Abort(Blackboard blackboard) => Child?.Abort(blackboard);
    }

    /// <summary>
    /// Repeats its child node a specified number of times or indefinitely.
    /// </summary>
    public sealed class RepeaterNode : DecoratorNode
    {
        /// <summary>
        /// Gets or sets the number of times to repeat. -1 for infinite.
        /// </summary>
        public int RepeatCount { get; set; } = -1;

        /// <summary>
        /// Gets or sets the delay between repetitions.
        /// </summary>
        public float Delay { get; set; } = 0f;
        private int repeatCounter;
        private float delayTimer;

        /// <summary>
        /// Ticks the child and repeats based on the configured count.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The node execution status.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (Child == null) return NodeStatus.Failure;

            if (Delay > 0f)
            {
                delayTimer += Time.deltaTime;
                if (delayTimer < Delay) return NodeStatus.Running;
                delayTimer = 0f;
            }

            NodeStatus status = Child.Tick(blackboard);
            if (status == NodeStatus.Running) return NodeStatus.Running;

            if (RepeatCount < 0 || repeatCounter < RepeatCount - 1)
            {
                repeatCounter++;
                return NodeStatus.Running;
            }

            return status;
        }

        /// <summary>
        /// Aborts the child and resets the repeat counter.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        public override void Abort(Blackboard blackboard)
        {
            repeatCounter = 0;
            delayTimer = 0f;
            Child?.Abort(blackboard);
        }
    }

    /// <summary>
    /// Checks if an enemy is visible within view distance and angle.
    /// </summary>
    public sealed class CanSeeEnemyNode : ConditionNode
    {
        public float viewDistance = 30f;
        public float viewAngle = 90f;
        [SerializeField] private LayerMask targetLayer;

        /// <summary>
        /// Performs a visibility check for enemies.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success if an enemy is seen; otherwise Failure.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("selfTransform")) return NodeStatus.Failure;

            Transform self = blackboard.Get<Transform>("selfTransform");
            Collider[] hits = Physics.OverlapSphere(self.position, viewDistance, targetLayer);
            foreach (Collider hit in hits)
            {
                Vector3 dir = (hit.transform.position - self.position).normalized;
                if (Vector3.Angle(self.forward, dir) < viewAngle * 0.5f)
                {
                    if (Physics.Raycast(self.position + Vector3.up * 1.6f, dir, out RaycastHit rayHit, viewDistance))
                    {
                        if (((1 << rayHit.transform.gameObject.layer) & targetLayer) != 0)
                        {
                            blackboard.Set("lastKnownEnemyPosition", rayHit.transform.position);
                            return NodeStatus.Success;
                        }
                    }
                }
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Checks if there is a clear line of sight to the target position.
    /// </summary>
    public sealed class HasLineOfSightNode : ConditionNode
    {
        public float maxDistance = 50f;

        /// <summary>
        /// Performs a raycast to verify line of sight to the target.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success if line of sight is clear; otherwise Failure.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("selfTransform")) return NodeStatus.Failure;
            if (!blackboard.Has("targetPosition")) return NodeStatus.Failure;

            Transform self = blackboard.Get<Transform>("selfTransform");
            Vector3 targetPos = blackboard.Get<Vector3>("targetPosition");
            Vector3 dir = (targetPos - self.position).normalized;

            if (Physics.Raycast(self.position + Vector3.up * 1.6f, dir, out RaycastHit hit, maxDistance))
            {
                if (Vector3.Distance(hit.point, targetPos) < 2f)
                {
                    return NodeStatus.Success;
                }
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Checks if the health is at or below a threshold.
    /// </summary>
    public sealed class IsLowHealthNode : ConditionNode
    {
        public float threshold = 30f;

        /// <summary>
        /// Evaluates whether health is below the threshold.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success if health is low; otherwise Failure.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("health")) return NodeStatus.Failure;
            return blackboard.Get<float>("health") <= threshold ? NodeStatus.Success : NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Checks if ammo count is at or above a threshold.
    /// </summary>
    public sealed class HasAmmoNode : ConditionNode
    {
        public float threshold = 5f;

        /// <summary>
        /// Evaluates whether ammo is above the threshold.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success if ammo is sufficient; otherwise Failure.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("ammo")) return NodeStatus.Failure;
            return blackboard.Get<int>("ammo") >= threshold ? NodeStatus.Success : NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Moves the agent toward the destination on the blackboard.
    /// </summary>
    public sealed class MoveToNode : ActionNode
    {
        public float acceptRadius = 1f;
        public float speed = 4f;
        [SerializeField] private bool useNavMesh = true;

        /// <summary>
        /// Moves the agent toward the destination.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success when destination is reached; otherwise Running.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("selfTransform")) return NodeStatus.Failure;
            if (!blackboard.Has("destination")) return NodeStatus.Failure;

            Transform self = blackboard.Get<Transform>("selfTransform");
            Vector3 destination = blackboard.Get<Vector3>("destination");

            float distance = Vector3.Distance(self.position, destination);
            if (distance <= acceptRadius) return NodeStatus.Success;

            Vector3 direction = (destination - self.position).normalized;
            direction.y = 0f;
            self.position += direction * speed * Time.deltaTime;
            self.forward = direction;

            return NodeStatus.Running;
        }
    }

    /// <summary>
    /// Attacks the target position with configurable fire rate and accuracy.
    /// </summary>
    public sealed class AttackNode : ActionNode
    {
        public float fireRate = 0.1f;
        public float accuracy = 0.8f;
        [SerializeField] private LayerMask targetLayer;
        private float nextFire;

        /// <summary>
        /// Performs a raycast attack toward the target position.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success on hit; Running otherwise.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("selfTransform")) return NodeStatus.Failure;
            if (!blackboard.Has("targetPosition")) return NodeStatus.Failure;

            Transform self = blackboard.Get<Transform>("selfTransform");
            Vector3 targetPos = blackboard.Get<Vector3>("targetPosition");
            Vector3 dir = (targetPos - self.position).normalized;

            self.forward = Vector3.Lerp(self.forward, dir, Time.deltaTime * 8f);

            if (Time.time >= nextFire)
            {
                nextFire = Time.time + fireRate;
                if (Physics.Raycast(self.position + Vector3.up * 1.6f, dir, out RaycastHit hit, 200f))
                {
                    if (((1 << hit.transform.gameObject.layer) & targetLayer) != 0)
                    {
                        if (UnityEngine.Random.value < accuracy)
                        {
                            blackboard.Set("lastHitPosition", hit.point);
                            return NodeStatus.Success;
                        }
                    }
                }
            }
            return NodeStatus.Running;
        }
    }

    /// <summary>
    /// Searches for nearby cover and sets the destination to the nearest cover point.
    /// </summary>
    public sealed class TakeCoverNode : ActionNode
    {
        public float searchRadius = 15f;
        [SerializeField] private LayerMask coverLayer;

        /// <summary>
        /// Searches for cover within the specified radius.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success if cover is found; otherwise Failure.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("selfTransform")) return NodeStatus.Failure;

            Transform self = blackboard.Get<Transform>("selfTransform");
            Collider[] coverSpots = Physics.OverlapSphere(self.position, searchRadius, coverLayer);
            if (coverSpots.Length == 0)
            {
                blackboard.Set("destination", self.position);
                return NodeStatus.Failure;
            }

            System.Array.Sort(coverSpots, (a, b) =>
                Vector3.Distance(self.position, a.transform.position)
                .CompareTo(Vector3.Distance(self.position, b.transform.position)));

            blackboard.Set("destination", coverSpots[0].transform.position);
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// Reloads the weapon after a configured duration.
    /// </summary>
    public sealed class ReloadNode : ActionNode
    {
        [SerializeField] private float reloadDuration = 2.5f;
        private float reloadTimer;

        /// <summary>
        /// Waits for the reload duration and then restores ammo.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success when reloaded; Running while reloading.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("ammo")) return NodeStatus.Failure;
            if (blackboard.Get<int>("ammo") > 0) return NodeStatus.Failure;

            reloadTimer += Time.deltaTime;
            if (reloadTimer >= reloadDuration)
            {
                blackboard.Set("ammo", 30);
                reloadTimer = 0f;
                return NodeStatus.Success;
            }
            return NodeStatus.Running;
        }

        /// <summary>
        /// Aborts the reload and resets the timer.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        public override void Abort(Blackboard blackboard) => reloadTimer = 0f;
    }

    /// <summary>
    /// Moves the agent to investigate the last known enemy position.
    /// </summary>
    public sealed class InvestigateNode : ActionNode
    {
        public float investigateDuration = 3f;
        private float investigateTimer;

        /// <summary>
        /// Sets the destination to the last known enemy position.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success after investigating; Running while investigating.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("lastKnownEnemyPosition")) return NodeStatus.Failure;

            Vector3 investigatePos = blackboard.Get<Vector3>("lastKnownEnemyPosition");
            blackboard.Set("destination", investigatePos);

            investigateTimer += Time.deltaTime;
            if (investigateTimer >= investigateDuration)
            {
                investigateTimer = 0f;
                return NodeStatus.Success;
            }
            return NodeStatus.Running;
        }

        /// <summary>
        /// Aborts investigation and resets the timer.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        public override void Abort(Blackboard blackboard) => investigateTimer = 0f;
    }

    /// <summary>
    /// Calculates a retreat position away from the threat.
    /// </summary>
    public sealed class RetreatNode : ActionNode
    {
        public float retreatDistance = 20f;

        /// <summary>
        /// Calculates a retreat destination away from the threat.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Success with retreat destination set.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("selfTransform")) return NodeStatus.Failure;
            if (!blackboard.Has("threatPosition")) return NodeStatus.Failure;

            Transform self = blackboard.Get<Transform>("selfTransform");
            Vector3 threatPos = blackboard.Get<Vector3>("threatPosition");
            Vector3 retreatDir = (self.position - threatPos).normalized;
            Vector3 retreatPos = self.position + retreatDir * retreatDistance;

            blackboard.Set("destination", retreatPos);
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// Patrols in a radius around the current position.
    /// </summary>
    public sealed class PatrolNode : ActionNode
    {
        public float patrolRadius = 15f;
        [SerializeField] private float waitTime = 2f;
        private Vector3 patrolCenter;
        private float waitTimer;

        /// <summary>
        /// Sets a random patrol destination within the radius.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>Always returns Running.</returns>
        public override NodeStatus Tick(Blackboard blackboard)
        {
            if (!blackboard.Has("selfTransform")) return NodeStatus.Failure;

            Transform self = blackboard.Get<Transform>("selfTransform");

            if (patrolCenter == Vector3.zero)
                patrolCenter = self.position;

            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-patrolRadius, patrolRadius),
                0f,
                UnityEngine.Random.Range(-patrolRadius, patrolRadius));
            Vector3 patrolTarget = patrolCenter + randomOffset;

            blackboard.Set("destination", patrolTarget);

            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTime)
            {
                waitTimer = 0f;
                patrolCenter = self.position;
            }

            return NodeStatus.Running;
        }
    }
}
