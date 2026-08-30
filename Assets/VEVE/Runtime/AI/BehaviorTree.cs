using UnityEngine;
using System;
using System.Collections.Generic;

namespace VEVE.AI
{
    /// <summary>
    /// Represents the execution status of a behavior tree node.
    /// </summary>
    public enum NodeStatus { Success, Failure, Running }

    /// <summary>
    /// A key-value store used to share data between behavior tree nodes.
    /// </summary>
    [Serializable]
    public sealed class Blackboard
    {
        private readonly Dictionary<string, object> data = new Dictionary<string, object>();

        /// <summary>
        /// Stores a value in the blackboard.
        /// </summary>
        /// <typeparam name="T">The type of value to store.</typeparam>
        /// <param name="key">The key to associate with the value.</param>
        /// <param name="value">The value to store.</param>
        public void Set<T>(string key, T value) => data[key] = value;

        /// <summary>
        /// Retrieves a value from the blackboard.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key associated with the value.</param>
        /// <returns>The stored value, or default if not found.</returns>
        public T Get<T>(string key)
        {
            if (data.TryGetValue(key, out object value) && value is T t) return t;
            return default;
        }

        /// <summary>
        /// Checks if the blackboard contains the specified key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists; otherwise false.</returns>
        public bool Has(string key) => data.ContainsKey(key);

        /// <summary>
        /// Clears all data from the blackboard.
        /// </summary>
        public void Clear() => data.Clear();
    }

    /// <summary>
    /// Base class for all behavior tree nodes.
    /// </summary>
    public abstract class Node
    {
        /// <summary>
        /// Gets or sets the display name of the node.
        /// </summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// Evaluates the node logic and returns the execution status.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The node execution status.</returns>
        public abstract NodeStatus Tick(Blackboard blackboard);

        /// <summary>
        /// Aborts the current node execution, if running.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        public virtual void Abort(Blackboard blackboard) { }
    }

    /// <summary>
    /// Base class for composite nodes that can have multiple children.
    /// </summary>
    public abstract class CompositeNode : Node
    {
        /// <summary>
        /// Gets or sets the child nodes of this composite.
        /// </summary>
        public List<Node> Children { get; set; } = new List<Node>();
    }

    /// <summary>
    /// Base class for decorator nodes that wrap a single child node.
    /// </summary>
    public abstract class DecoratorNode : Node
    {
        /// <summary>
        /// Gets or sets the child node decorated by this node.
        /// </summary>
        public Node Child { get; set; }
    }

    /// <summary>
    /// Base class for action nodes that perform work.
    /// </summary>
    public abstract class ActionNode : Node
    {
        /// <summary>
        /// Evaluates the action logic and returns the execution status.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The node execution status.</returns>
        public abstract override NodeStatus Tick(Blackboard blackboard);
    }

    /// <summary>
    /// Base class for condition nodes that evaluate boolean checks.
    /// </summary>
    public abstract class ConditionNode : Node
    {
        /// <summary>
        /// Evaluates the condition and returns Success or Failure.
        /// </summary>
        /// <param name="blackboard">The shared blackboard data.</param>
        /// <returns>The node execution status.</returns>
        public abstract override NodeStatus Tick(Blackboard blackboard);
    }

    /// <summary>
    /// MonoBehaviour component that executes a behavior tree every frame.
    /// </summary>
    public sealed class BehaviorTreeRunner : MonoBehaviour
    {
        [SerializeField] private Node rootNode;
        private Blackboard blackboard = new Blackboard();
        private NodeStatus lastStatus;

        private void Update()
        {
            if (rootNode != null) lastStatus = rootNode.Tick(blackboard);
        }

        private void OnDisable()
        {
            rootNode?.Abort(blackboard);
        }

        /// <summary>
        /// Gets the blackboard shared by all nodes in the tree.
        /// </summary>
        public Blackboard Blackboard => blackboard;

        /// <summary>
        /// Gets the last execution status of the root node.
        /// </summary>
        public NodeStatus LastStatus => lastStatus;
    }
}
