using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VEVE.Agents
{
    /// <summary>
    /// First-class, zero-dependency heuristic HTN-lite planner (C#). This is
    /// the authoritative cognition path: it always satisfies a decision
    /// request within the same frame, with the Python sidecar used only as an
    /// optional async refinement producer.
    /// </summary>
    public sealed class LocalHeuristicCognition : ICognitionService
    {
        private static int nextPlanId;

        public string Name => "local-heuristic";
        public bool IsAvailable => true;

        public Task<BehaviorPlan> PlanAsync(AgentCognitionInput input, CancellationToken token = default)
        {
            return Task.FromResult(Plan(input));
        }

        public BehaviorPlan Plan(AgentCognitionInput input)
        {
            var steps = new List<BehaviorStep>(4);
            steps.Add(ChoosePrimaryStep(in input));

            if (input.lod != AgentLODTier.Statistical)
                InsertSupportingSteps(in input, steps);

            return new BehaviorPlan
            {
                planId = Interlocked.Increment(ref nextPlanId),
                agentInstanceId = input.agentInstanceId,
                steps = steps.ToArray(),
                generatedAt = Time.unscaledTime,
                ttl = GetPlanTtl(input.lod),
                producer = Name
            };
        }

        private static BehaviorStep ChoosePrimaryStep(in AgentCognitionInput input)
        {
            if (input.healthRatio <= 0f)
                return new BehaviorStep { op = BehaviorOp.Idle, value = 0f, position = input.position };

            if (input.healthRatio < 0.25f)
                return new BehaviorStep { op = BehaviorOp.Retreat, position = input.position, value = 0.9f };

            if (input.roundsRemaining == 0)
                return new BehaviorStep { op = BehaviorOp.Reload, value = 0.5f, position = input.position };

            if (input.target != null && input.targetVisibility > 0.3f)
                return new BehaviorStep
                {
                    op = BehaviorOp.FireAt,
                    targetInstanceID = input.targetInstanceID,
                    position = input.targetPosition,
                    value = input.targetVisibility
                };

            if (input.targetInstanceID != 0 || input.distanceToTarget > 0f)
                return new BehaviorStep { op = BehaviorOp.Investigate, position = input.targetPosition, value = 0.4f };

            return new BehaviorStep { op = BehaviorOp.Idle, position = input.position, value = 0f };
        }

        private static void InsertSupportingSteps(in AgentCognitionInput input, List<BehaviorStep> steps)
        {
            var primary = steps[0];
            switch (primary.op)
            {
                case BehaviorOp.FireAt:
                    if (input.distanceToTarget > 50f && input.teamId != -1)
                    {
                        Vector3 direction = (input.targetPosition - input.position).normalized;
                        Vector3 flankOffset = Vector3.Cross(direction, Vector3.up) * 10f;
                        steps.Add(new BehaviorStep
                        {
                            op = BehaviorOp.Flank,
                            targetInstanceID = input.targetInstanceID,
                            position = input.targetPosition + flankOffset,
                            value = primary.value
                        });
                    }
                    break;
                case BehaviorOp.Investigate:
                    steps.Add(new BehaviorStep { op = BehaviorOp.TakeCover, position = input.position, value = 0.3f });
                    break;
                case BehaviorOp.Retreat:
                    steps.Add(new BehaviorStep
                    {
                        op = BehaviorOp.Callout,
                        targetInstanceID = input.targetInstanceID,
                        position = input.position,
                        value = 1f
                    });
                    break;
            }
        }

        private static float GetPlanTtl(AgentLODTier lod)
        {
            switch (lod)
            {
                case AgentLODTier.Full: return 1.0f;
                case AgentLODTier.Standard: return 2.0f;
                case AgentLODTier.Simplified: return 4.0f;
                default: return 10.0f;
            }
        }
    }
}
