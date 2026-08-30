using UnityEngine;
using System;
using System.Collections.Generic;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines the current state of a task in the allocation system.
    /// </summary>
    public enum TaskState { Open, Assigned, InProgress, Completed, Failed }

    /// <summary>
    /// Defines the priority level of a task.
    /// </summary>
    public enum TaskPriority { Low, Normal, High, Critical }

    /// <summary>
    /// Represents a task that can be allocated to agents via auction.
    /// </summary>
    [Serializable]
    public class Task
    {
        /// <summary>Unique identifier for the task.</summary>
        public string taskId;

        /// <summary>Short description of the task.</summary>
        public string description;

        /// <summary>Priority level of the task.</summary>
        public TaskPriority priority;

        /// <summary>Target position for the task, if applicable.</summary>
        public Vector3 targetPosition;

        /// <summary>Current state of the task.</summary>
        public TaskState state;

        /// <summary>Identifier of the assigned agent. Empty if unassigned.</summary>
        public string assignedAgentId;

        /// <summary>Bid value representing agent interest in this task.</summary>
        public float currentBid;

        /// <summary>Time limit for task completion in seconds. 0 for no limit.</summary>
        public float timeLimit;

        /// <summary>
        /// Initializes a new task with default values.
        /// </summary>
        public Task()
        {
            taskId = Guid.NewGuid().ToString("N");
            description = string.Empty;
            priority = TaskPriority.Normal;
            targetPosition = Vector3.zero;
            state = TaskState.Open;
            assignedAgentId = string.Empty;
            currentBid = 0f;
            timeLimit = 0f;
        }
    }

    /// <summary>
    /// Represents a bid submitted by an agent for a task.
    /// </summary>
    [Serializable]
    public class TaskBid
    {
        /// <summary>Identifier of the bidding agent.</summary>
        public string agentId;

        /// <summary>Identifier of the task being bid on.</summary>
        public string taskId;

        /// <summary>Bid value representing agent capability or interest.</summary>
        public float bidValue;

        /// <summary>Estimated completion time in seconds.</summary>
        public float estimatedDuration;

        /// <summary>Timestamp when the bid was submitted.</summary>
        public float timestamp;

        /// <summary>
        /// Initializes a new task bid.
        /// </summary>
        /// <param name="agent">The agent ID.</param>
        /// <param name="task">The task ID.</param>
        /// <param name="value">The bid value.</param>
        /// <param name="duration">Estimated duration.</param>
        public TaskBid(string agent, string task, float value, float duration)
        {
            agentId = agent;
            taskId = task;
            bidValue = value;
            estimatedDuration = duration;
            timestamp = Time.time;
        }
    }

    /// <summary>
    /// Distributed task allocation system using auction-based assignment.
    /// </summary>
    public class TaskAllocator : MonoBehaviour
    {
        [SerializeField] private float auctionDuration = 2f;
        [SerializeField] private float reallocationThreshold = 0.3f;
        [SerializeField] private int maxRetries = 3;

        private Dictionary<string, Task> tasks;
        private Dictionary<string, List<TaskBid>> bidHistory;
        private float auctionTimer;
        private bool auctionInProgress;

        /// <summary>
        /// Gets the list of all registered tasks.
        /// </summary>
        public IReadOnlyDictionary<string, Task> Tasks => tasks;

        /// <summary>
        /// Gets the list of all task bids submitted during auctions.
        /// </summary>
        public IReadOnlyDictionary<string, List<TaskBid>> BidHistory => bidHistory;

        /// <summary>
        /// Initializes the task allocator.
        /// </summary>
        protected virtual void Awake()
        {
            tasks = new Dictionary<string, Task>();
            bidHistory = new Dictionary<string, List<TaskBid>>();
            auctionInProgress = false;
        }

        /// <summary>
        /// Registers a new task for allocation.
        /// </summary>
        /// <param name="task">The task to register.</param>
        public void RegisterTask(Task task)
        {
            if (task == null || string.IsNullOrEmpty(task.taskId)) return;
            tasks[task.taskId] = task;
        }

        /// <summary>
        /// Removes a task from the allocator.
        /// </summary>
        /// <param name="taskId">Identifier of the task to remove.</param>
        public void UnregisterTask(string taskId)
        {
            if (tasks.ContainsKey(taskId))
            {
                tasks.Remove(taskId);
                if (bidHistory.ContainsKey(taskId))
                {
                    bidHistory.Remove(taskId);
                }
            }
        }

        /// <summary>
        /// Updates the auction cycle and assigns tasks.
        /// </summary>
        protected virtual void Update()
        {
            if (auctionInProgress)
            {
                auctionTimer -= Time.deltaTime;
                if (auctionTimer <= 0f)
                {
                    ResolveAuctions();
                }
            }
        }

        /// <summary>
        /// Starts an auction for all open tasks.
        /// </summary>
        public void StartAuction()
        {
            if (auctionInProgress) return;
            auctionInProgress = true;
            auctionTimer = auctionDuration;
        }

        /// <summary>
        /// Submits a bid for a specific task.
        /// </summary>
        /// <param name="bid">The task bid to submit.</param>
        public void SubmitBid(TaskBid bid)
        {
            if (bid == null || string.IsNullOrEmpty(bid.taskId)) return;
            if (!tasks.ContainsKey(bid.taskId)) return;
            if (tasks[bid.taskId].state != TaskState.Open) return;

            if (!bidHistory.ContainsKey(bid.taskId))
            {
                bidHistory[bid.taskId] = new List<TaskBid>();
            }
            bidHistory[bid.taskId].Add(bid);
        }

        /// <summary>
        /// Resolves all pending auctions and assigns tasks to winning bidders.
        /// </summary>
        public void ResolveAuctions()
        {
            List<string> completedTaskIds = new List<string>();
            foreach (var kvp in tasks)
            {
                Task task = kvp.Value;
                if (task.state != TaskState.Open) continue;
                if (!bidHistory.ContainsKey(task.taskId)) continue;

                List<TaskBid> bids = bidHistory[task.taskId];
                if (bids.Count == 0) continue;

                TaskBid winningBid = DetermineWinner(bids);
                AssignTask(task.taskId, winningBid.agentId);
                completedTaskIds.Add(task.taskId);
            }

            foreach (string taskId in completedTaskIds)
            {
                if (bidHistory.ContainsKey(taskId))
                {
                    bidHistory[taskId].Clear();
                }
            }

            auctionInProgress = false;
        }

        /// <summary>
        /// Reallocates a task if the assigned agent is unable to complete it.
        /// </summary>
        /// <param name="taskId">Identifier of the task to reallocate.</param>
        /// <returns>True if reallocation was successful; otherwise false.</returns>
        public bool ReallocateTask(string taskId)
        {
            if (!tasks.ContainsKey(taskId)) return false;
            Task task = tasks[taskId];
            if (task.state != TaskState.Assigned && task.state != TaskState.InProgress) return false;

            task.assignedAgentId = string.Empty;
            task.state = TaskState.Open;
            tasks[taskId] = task;
            StartAuction();
            return true;
        }

        /// <summary>
        /// Completes a task and updates its state.
        /// </summary>
        /// <param name="taskId">Identifier of the task to complete.</param>
        public void CompleteTask(string taskId)
        {
            if (tasks.ContainsKey(taskId))
            {
                Task task = tasks[taskId];
                task.state = TaskState.Completed;
                tasks[taskId] = task;
            }
        }

        /// <summary>
        /// Marks a task as failed.
        /// </summary>
        /// <param name="taskId">Identifier of the failed task.</param>
        public void FailTask(string taskId)
        {
            if (tasks.ContainsKey(taskId))
            {
                Task task = tasks[taskId];
                task.state = TaskState.Failed;
                tasks[taskId] = task;
            }
        }

        /// <summary>
        /// Determines the winning bid for a task.
        /// </summary>
        /// <param name="bids">List of bids to evaluate.</param>
        /// <returns>The winning bid.</returns>
        private TaskBid DetermineWinner(List<TaskBid> bids)
        {
            TaskBid winner = bids[0];
            float bestScore = CalculateBidScore(winner);
            foreach (TaskBid bid in bids)
            {
                float score = CalculateBidScore(bid);
                if (score > bestScore)
                {
                    bestScore = score;
                    winner = bid;
                }
            }
            return winner;
        }

        /// <summary>
        /// Calculates a composite score for a bid.
        /// </summary>
        /// <param name="bid">The bid to evaluate.</param>
        /// <returns>The calculated score.</returns>
        private float CalculateBidScore(TaskBid bid)
        {
            return bid.bidValue * 0.6f + (1f - Mathf.Clamp01(bid.estimatedDuration / 30f)) * 0.4f;
        }

        /// <summary>
        /// Assigns a task to an agent.
        /// </summary>
        /// <param name="taskId">Identifier of the task.</param>
        /// <param name="agentId">Identifier of the agent.</param>
        private void AssignTask(string taskId, string agentId)
        {
            if (!tasks.ContainsKey(taskId)) return;
            Task task = tasks[taskId];
            task.assignedAgentId = agentId;
            task.state = TaskState.Assigned;
            task.currentBid = 0f;
            tasks[taskId] = task;
        }
    }
}
