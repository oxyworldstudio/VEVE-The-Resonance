using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Serializable plan step with preconditions and effects.
    /// </summary>
    [Serializable]
    public class PlanStep
    {
        public string actionName;
        public List<string> preconditions;
        public List<string> effects;
        public float cost;
    }

    /// <summary>
    /// Action sequence plan for achieving goals.
    /// </summary>
    [Serializable]
    public class Plan
    {
        public string name;
        public List<PlanStep> steps;
        public float estimatedTotalCost;
        public bool isValid;

        public Plan()
        {
            steps = new List<PlanStep>();
            estimatedTotalCost = 0f;
            isValid = true;
        }

        /// <summary>
        /// Evaluates whether all preconditions in the plan are satisfied.
        /// </summary>
        public bool IsValid(Blackboard blackboard)
        {
            if (steps == null) return false;
            foreach (var step in steps)
            {
                if (step.preconditions == null) continue;
                foreach (var precondition in step.preconditions)
                {
                    if (!blackboard.Has(precondition)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Calculates the total estimated cost of the plan.
        /// </summary>
        public float GetEstimatedCost()
        {
            if (steps == null) return 0f;
            float total = 0f;
            foreach (var step in steps)
            {
                total += step.cost;
            }
            estimatedTotalCost = total;
            return total;
        }

        /// <summary>
        /// Applies the effects of a completed plan step to the blackboard.
        /// </summary>
        public void ApplyStepEffects(Blackboard blackboard, int stepIndex)
        {
            if (steps == null || blackboard == null) return;
            if (stepIndex < 0 || stepIndex >= steps.Count) return;
            foreach (var effect in steps[stepIndex].effects)
            {
                blackboard.Set(effect, true);
            }
        }
    }
}
