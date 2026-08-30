using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Serializable option for utility evaluation.
    /// </summary>
    [Serializable]
    public class UtilityOption
    {
        public string name;
        public float baseScore;
        public List<string> modifiers;
    }

    /// <summary>
    /// Utility-based scoring system for evaluating actions and goals.
    /// </summary>
    public static class UtilitySystem
    {
        /// <summary>
        /// Evaluates a goal's utility score based on its priority and blackboard state.
        /// </summary>
        public static float EvaluateGoal(Goal goal, Blackboard blackboard)
        {
            if (goal == null || blackboard == null) return 0f;
            float score = (float)goal.priority * 0.25f;
            if (goal.IsValid(blackboard)) score += 0.5f;
            if (goal.IsCompleted(blackboard)) score -= 1f;
            return Mathf.Clamp01(score);
        }

        /// <summary>
        /// Selects the highest-scoring valid goal from a list.
        /// </summary>
        public static Goal SelectBestGoal(List<Goal> goals, Blackboard blackboard)
        {
            if (goals == null || blackboard == null) return null;

            Goal best = null;
            float bestScore = -1f;
            foreach (var goal in goals)
            {
                float score = EvaluateGoal(goal, blackboard);
                goal.utilityScore = score;
                if (score > bestScore && goal.IsValid(blackboard))
                {
                    bestScore = score;
                    best = goal;
                }
            }
            return best;
        }

        /// <summary>
        /// Normalizes a value from a source range to a 0-1 range.
        /// </summary>
        public static float Normalize(float value, float min, float max)
        {
            if (Mathf.Approximately(max, min)) return 0f;
            return Mathf.Clamp01((value - min) / (max - min));
        }
    }
}
