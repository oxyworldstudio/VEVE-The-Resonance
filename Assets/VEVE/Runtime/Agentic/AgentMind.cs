using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Core decision-making component for autonomous agents.
    /// </summary>
    public class AgentMind : MonoBehaviour
    {
        [SerializeField] private List<Goal> goals = new();
        [SerializeField] private Blackboard blackboard = new();
        [SerializeField] private float replanInterval = 2f;
        [SerializeField] private bool debugMode;

        private Plan currentPlan;
        private int currentStepIndex;
        private float lastReplanTime;

        private void Awake()
        {
            if (blackboard == null) blackboard = new Blackboard();
            if (goals == null) goals = new List<Goal>();
        }

        private void Update()
        {
            if (Time.time - lastReplanTime > replanInterval)
            {
                lastReplanTime = Time.time;
                Replan();
            }
            ExecutePlan();
        }

        /// <summary>
        /// Gets the currently active plan.
        /// </summary>
        public Plan CurrentPlan => currentPlan;

        /// <summary>
        /// Adds a goal to the agent's goal set.
        /// </summary>
        public void AddGoal(Goal goal)
        {
            if (goal == null) return;
            if (goals == null) goals = new List<Goal>();
            goals.Add(goal);
        }

        /// <summary>
        /// Removes a goal by identifier.
        /// </summary>
        public void RemoveGoal(string goalId)
        {
            if (string.IsNullOrEmpty(goalId) || goals == null) return;
            goals.RemoveAll(g => g.id == goalId);
        }

        /// <summary>
        /// Sets a value on the agent's blackboard.
        /// </summary>
        public void SetBlackboardValue<T>(string key, T value)
        {
            blackboard?.Set(key, value);
        }

        /// <summary>
        /// Attempts to retrieve a value from the agent's blackboard.
        /// </summary>
        public bool TryGetBlackboardValue<T>(string key, out T value)
        {
            if (blackboard != null) return blackboard.TryGet(key, out value);
            value = default;
            return false;
        }

        /// <summary>
        /// Forces the agent to replan immediately.
        /// </summary>
        public void Replan()
        {
            var bestGoal = UtilitySystem.SelectBestGoal(goals, blackboard);
            if (bestGoal != null)
            {
                currentPlan = GeneratePlan(bestGoal);
                currentStepIndex = 0;
            }
            else
            {
                currentPlan = null;
                currentStepIndex = 0;
            }
        }

        private Plan GeneratePlan(Goal goal)
        {
            var plan = new Plan { name = goal.name + "_Plan" };
            plan.steps.Add(new PlanStep { actionName = "Start", cost = 0.1f, preconditions = new List<string>(), effects = new List<string>() });
            plan.isValid = plan.IsValid(blackboard);
            plan.estimatedTotalCost = plan.GetEstimatedCost();
            return plan;
        }

        private void ExecutePlan()
        {
            if (currentPlan == null || currentPlan.steps == null) return;
            if (currentStepIndex >= currentPlan.steps.Count)
            {
                if (debugMode) Debug.Log("[AgentMind] Plan completed.");
                currentPlan = null;
                return;
            }

            var step = currentPlan.steps[currentStepIndex];
            if (step == null) return;

            if (debugMode)
            {
                Debug.Log($"[AgentMind] Executing step: {step.actionName} on {gameObject.name}");
            }

            currentPlan.ApplyStepEffects(blackboard, currentStepIndex);
            currentStepIndex++;
        }
    }
}
