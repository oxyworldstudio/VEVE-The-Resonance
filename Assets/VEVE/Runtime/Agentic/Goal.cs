using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Priority levels for agent goals.
    /// </summary>
    public enum GoalPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }

    /// <summary>
    /// Comparison operators for goal conditions.
    /// </summary>
    public enum ComparisonType { Equals, NotEquals, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual, Exists }

    /// <summary>
    /// Serializable condition for goal evaluation against a blackboard.
    /// </summary>
    [Serializable]
    public class Condition
    {
        public string key;
        public ComparisonType comparison;
        public float floatValue;
        public bool boolValue;
        public string stringValue;

        /// <summary>
        /// Evaluates this condition against the provided blackboard.
        /// </summary>
        public bool Evaluate(Blackboard blackboard)
        {
            if (blackboard == null) return false;
            if (!blackboard.Has(key)) return false;

            if (comparison == ComparisonType.Exists) return true;

            object value = blackboard.Get<object>(key);
            if (value == null) return false;

            switch (value)
            {
                case float f:
                    return CompareNumeric(f, floatValue);
                case int i:
                    return CompareNumeric(i, floatValue);
                case double d:
                    return CompareNumeric((float)d, floatValue);
                case bool b:
                    return CompareBool(b);
                case string s:
                    return CompareString(s);
                default:
                    return false;
            }
        }

        private bool CompareNumeric(float actual, float expected)
        {
            return comparison switch
            {
                ComparisonType.Equals => Mathf.Approximately(actual, expected),
                ComparisonType.NotEquals => !Mathf.Approximately(actual, expected),
                ComparisonType.GreaterThan => actual > expected,
                ComparisonType.LessThan => actual < expected,
                ComparisonType.GreaterOrEqual => actual >= expected,
                ComparisonType.LessOrEqual => actual <= expected,
                _ => false
            };
        }

        private bool CompareBool(bool actual)
        {
            return comparison switch
            {
                ComparisonType.Equals => actual == boolValue,
                ComparisonType.NotEquals => actual != boolValue,
                _ => false
            };
        }

        private bool CompareString(string actual)
        {
            return comparison switch
            {
                ComparisonType.Equals => actual == stringValue,
                ComparisonType.NotEquals => actual != stringValue,
                _ => false
            };
        }
    }

    /// <summary>
    /// Serializable goal definition for autonomous agents.
    /// </summary>
    [Serializable]
    public class Goal
    {
        public string id;
        public string name;
        public GoalPriority priority;
        public List<Condition> conditions;
        public List<string> completionCriteria;
        [NonSerialized] public float utilityScore;

        /// <summary>
        /// Evaluates whether this goal's conditions are met by the current blackboard state.
        /// </summary>
        public bool IsValid(Blackboard blackboard)
        {
            if (conditions == null || conditions.Count == 0) return true;
            foreach (var condition in conditions)
            {
                if (!condition.Evaluate(blackboard)) return false;
            }
            return true;
        }

        /// <summary>
        /// Checks whether this goal has been completed based on blackboard state.
        /// </summary>
        public bool IsCompleted(Blackboard blackboard)
        {
            if (completionCriteria == null || completionCriteria.Count == 0) return false;
            foreach (var criterion in completionCriteria)
            {
                if (blackboard.Has(criterion)) return true;
            }
            return false;
        }
    }
}
