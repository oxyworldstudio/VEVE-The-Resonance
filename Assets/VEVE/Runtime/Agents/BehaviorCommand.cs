using UnityEngine;
using VEVE.Agentic;

namespace VEVE.Agents
{
    /// <summary>
    /// Canonical command vocabulary between the cognition layer (local or
    /// Python) and the C# action interpreter. The game never executes code
    /// from the planner; it interprets these primitives only.
    /// </summary>
    public enum BehaviorOp : byte
    {
        Idle = 0,
        MoveTo = 1,
        TakeCover = 2,
        FireAt = 3,
        Reload = 4,
        Suppress = 5,
        Flank = 6,
        Investigate = 7,
        Retreat = 8,
        HealAlly = 9,
        ReviveCasualty = 10,
        Callout = 11
    }

    /// <summary>
    /// One executable step of a behavior plan.
    /// </summary>
    public struct BehaviorStep
    {
        public BehaviorOp op;
        public int targetInstanceID;
        public Vector3 position;
        public float value;
    }

    /// <summary>
    /// Full input snapshot provided to the cognition service. Built once per
    /// planning cycle on the main thread; never mutated afterwards.
    /// </summary>
    public struct AgentCognitionInput
    {
        public int agentInstanceId;
        public Vector3 position;
        public Vector3 forward;
        public Vector3 targetPosition;
        public GameObject target;
        public int targetInstanceID;
        public float targetVisibility;
        public float distanceToTarget;
        public float healthRatio;
        public int roundsRemaining;
        public int teamId;
        public AgentLODTier lod;
    }

    /// <summary>
    /// Immutable plan returned by a cognition service. Time-to-live based on
    /// the agent LOD tier; expired plans trigger a synchronous local refresh
    /// plus an optional async deep replan.
    /// </summary>
    public sealed class BehaviorPlan
    {
        public int planId;
        public int agentInstanceId;
        public BehaviorStep[] steps;
        public float generatedAt;
        public float ttl;
        public string producer;

        public bool IsExpired => Time.unscaledTime - generatedAt > ttl;
    }
}
