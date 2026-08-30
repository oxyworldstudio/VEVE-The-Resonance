using NUnit.Framework;
using UnityEngine;
using VEVE.Agents;

public sealed class LocalHeuristicCognitionTests
{
    private static AgentCognitionInput BaseInput()
    {
        return new AgentCognitionInput
        {
            agentInstanceId = 1,
            position = Vector3.zero,
            forward = Vector3.forward,
            targetPosition = Vector3.zero,
            target = null,
            targetInstanceID = 0,
            targetVisibility = 0f,
            distanceToTarget = 0f,
            healthRatio = 1f,
            roundsRemaining = 30,
            teamId = -1,
            lod = AgentLODTier.Full
        };
    }

    [Test]
    public void VisibleEnemyProducesFireAtPrimaryStep()
    {
        var input = BaseInput();
        input.target = new GameObject("enemy").gameObject;
        input.targetInstanceID = input.target.GetInstanceID();
        input.targetVisibility = 0.8f;
        input.targetPosition = new Vector3(0f, 0f, 10f);
        input.distanceToTarget = 10f;

        var plan = new LocalHeuristicCognition().Plan(input);

        Assert.AreEqual(BehaviorOp.FireAt, plan.steps[0].op);
        Assert.AreEqual(input.targetInstanceID, plan.steps[0].targetInstanceID);
        Assert.GreaterOrEqual(plan.ttl, 0.5f);
        Object.DestroyImmediate(input.target);
    }

    [Test]
    public void EmptyMagazineProducesReload()
    {
        var input = BaseInput();
        input.roundsRemaining = 0;

        var plan = new LocalHeuristicCognition().Plan(input);

        Assert.AreEqual(BehaviorOp.Reload, plan.steps[0].op);
    }

    [Test]
    public void CriticalHealthProducesRetreat()
    {
        var input = BaseInput();
        input.healthRatio = 0.1f;
        input.target = new GameObject("enemy").gameObject;
        input.targetInstanceID = input.target.GetInstanceID();
        input.targetVisibility = 0.9f;

        var plan = new LocalHeuristicCognition().Plan(input);

        Assert.AreEqual(BehaviorOp.Retreat, plan.steps[0].op);
        Object.DestroyImmediate(input.target);
    }

    [Test]
    public void CriticalHealthOutrankedOnlyByDeadState()
    {
        var input = BaseInput();
        input.healthRatio = 0f;

        var plan = new LocalHeuristicCognition().Plan(input);

        Assert.AreEqual(BehaviorOp.Idle, plan.steps[0].op);
    }

    [Test]
    public void NoTargetProducesIdle()
    {
        var plan = new LocalHeuristicCognition().Plan(BaseInput());

        Assert.AreEqual(BehaviorOp.Idle, plan.steps[0].op);
    }

    [Test]
    public void DistantVisibleTargetWithTeamAddsFlankSupportingStep()
    {
        var input = BaseInput();
        input.target = new GameObject("enemy").gameObject;
        input.targetInstanceID = input.target.GetInstanceID();
        input.targetVisibility = 0.8f;
        input.targetPosition = new Vector3(60f, 0f, 0f);
        input.distanceToTarget = 60f;
        input.teamId = 0;

        var plan = new LocalHeuristicCognition().Plan(input);

        Assert.GreaterOrEqual(plan.steps.Length, 2);
        Assert.AreEqual(BehaviorOp.Flank, plan.steps[1].op);
        Object.DestroyImmediate(input.target);
    }

    [Test]
    public void StatisticalTierProducesSingleStepLightweightPlan()
    {
        var input = BaseInput();
        input.lod = AgentLODTier.Statistical;
        input.target = new GameObject("enemy").gameObject;
        input.targetInstanceID = input.target.GetInstanceID();
        input.targetVisibility = 0.8f;
        input.targetPosition = new Vector3(60f, 0f, 0f);
        input.distanceToTarget = 60f;
        input.teamId = 0;

        var plan = new LocalHeuristicCognition().Plan(input);

        Assert.AreEqual(1, plan.steps.Length);
        Object.DestroyImmediate(input.target);
    }

    [Test]
    public void PlansHavePositiveTTLAndUniqueIds()
    {
        var planner = new LocalHeuristicCognition();
        var a = planner.Plan(BaseInput());
        var b = planner.Plan(BaseInput());

        Assert.AreNotEqual(a.planId, b.planId);
        Assert.Greater(a.ttl, 0f);
    }
}
