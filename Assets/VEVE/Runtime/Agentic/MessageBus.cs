using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Delegate for message handler callbacks.
    /// </summary>
    /// <param name="message">The message being received.</param>
    public delegate void MessageHandler(AgentMessage message);

    /// <summary>
    /// Central message routing system supporting broadcast, multicast, and direct messaging.
    /// </summary>
    public sealed class MessageBus
    {
        private static MessageBus _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, List<MessageHandler>> _directSubscribers = new Dictionary<string, List<MessageHandler>>();
        private readonly Dictionary<MessageType, List<MessageHandler>> _typedSubscribers = new Dictionary<MessageType, List<MessageHandler>>();
        private readonly List<MessageHandler> _broadcastHandlers = new List<MessageHandler>();
        private readonly List<AgentMessage> _messageQueue = new List<AgentMessage>();
        private readonly HashSet<string> _registeredAgents = new HashSet<string>();
        private bool _processing;

        /// <summary>
        /// Gets the singleton instance of the MessageBus.
        /// </summary>
        public static MessageBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new MessageBus();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Registers an agent to receive messages.
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent.</param>
        public void RegisterAgent(string agentId)
        {
            if (string.IsNullOrEmpty(agentId)) return;
            lock (_lock)
            {
                _registeredAgents.Add(agentId);
                if (!_directSubscribers.ContainsKey(agentId))
                    _directSubscribers[agentId] = new List<MessageHandler>();
            }
        }

        /// <summary>
        /// Unregisters an agent from receiving messages.
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent.</param>
        public void UnregisterAgent(string agentId)
        {
            lock (_lock)
            {
                _registeredAgents.Remove(agentId);
                _directSubscribers.Remove(agentId);
            }
        }

        /// <summary>
        /// Subscribes a handler to direct messages for a specific agent.
        /// </summary>
        /// <param name="agentId">The agent identifier to receive messages for.</param>
        /// <param name="handler">The handler callback.</param>
        public void SubscribeDirect(string agentId, MessageHandler handler)
        {
            lock (_lock)
            {
                if (!_directSubscribers.ContainsKey(agentId))
                    _directSubscribers[agentId] = new List<MessageHandler>();
                if (!_directSubscribers[agentId].Contains(handler))
                    _directSubscribers[agentId].Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribes a handler from direct messages for a specific agent.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="handler">The handler callback to remove.</param>
        public void UnsubscribeDirect(string agentId, MessageHandler handler)
        {
            lock (_lock)
            {
                if (_directSubscribers.ContainsKey(agentId))
                    _directSubscribers[agentId].Remove(handler);
            }
        }

        /// <summary>
        /// Subscribes a handler to all messages of a specific type.
        /// </summary>
        /// <param name="messageType">The message type to subscribe to.</param>
        /// <param name="handler">The handler callback.</param>
        public void SubscribeTyped(MessageType messageType, MessageHandler handler)
        {
            lock (_lock)
            {
                if (!_typedSubscribers.ContainsKey(messageType))
                    _typedSubscribers[messageType] = new List<MessageHandler>();
                if (!_typedSubscribers[messageType].Contains(handler))
                    _typedSubscribers[messageType].Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribes a handler from a specific message type.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <param name="handler">The handler callback to remove.</param>
        public void UnsubscribeTyped(MessageType messageType, MessageHandler handler)
        {
            lock (_lock)
            {
                if (_typedSubscribers.ContainsKey(messageType))
                    _typedSubscribers[messageType].Remove(handler);
            }
        }

        /// <summary>
        /// Subscribes a handler to all broadcast messages.
        /// </summary>
        /// <param name="handler">The handler callback.</param>
        public void SubscribeBroadcast(MessageHandler handler)
        {
            lock (_lock)
            {
                if (!_broadcastHandlers.Contains(handler))
                    _broadcastHandlers.Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribes a handler from broadcast messages.
        /// </summary>
        /// <param name="handler">The handler callback to remove.</param>
        public void UnsubscribeBroadcast(MessageHandler handler)
        {
            lock (_lock)
            {
                _broadcastHandlers.Remove(handler);
            }
        }

        /// <summary>
        /// Sends a direct message to a specific agent.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendDirect(AgentMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.receiverId)) return;
            lock (_messageQueue)
            {
                _messageQueue.Add(message);
            }
        }

        /// <summary>
        /// Broadcasts a message to all registered agents.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        public void Broadcast(AgentMessage message)
        {
            if (message == null) return;
            message.receiverId = string.Empty;
            lock (_messageQueue)
            {
                _messageQueue.Add(message);
            }
        }

        /// <summary>
        /// Sends a message to multiple specified recipients.
        /// </summary>
        /// <param name="message">The base message to send.</param>
        /// <param name="recipientIds">The list of recipient identifiers.</param>
        public void Multicast(AgentMessage message, IEnumerable<string> recipientIds)
        {
            if (message == null || recipientIds == null) return;
            lock (_messageQueue)
            {
                foreach (var recipientId in recipientIds)
                {
                    AgentMessage copy = new AgentMessage
                    {
                        messageId = Guid.NewGuid().ToString("N"),
                        senderId = message.senderId,
                        receiverId = recipientId,
                        type = message.type,
                        priority = message.priority,
                        timestamp = message.timestamp,
                        payload = message.payload
                    };
                    _messageQueue.Add(copy);
                }
            }
        }

        /// <summary>
        /// Processes all queued messages and dispatches them to subscribers.
        /// </summary>
        public void ProcessQueue()
        {
            if (_processing) return;
            _processing = true;

            List<AgentMessage> messages;
            lock (_messageQueue)
            {
                if (_messageQueue.Count == 0)
                {
                    _processing = false;
                    return;
                }
                messages = new List<AgentMessage>(_messageQueue);
                _messageQueue.Clear();
            }

            messages.Sort((a, b) => b.priority.CompareTo(a.priority));

            foreach (var message in messages)
            {
                Dispatch(message);
            }

            _processing = false;
        }

        /// <summary>
        /// Gets all registered agent identifiers.
        /// </summary>
        /// <returns>A list of registered agent IDs.</returns>
        public List<string> GetRegisteredAgents()
        {
            lock (_lock)
            {
                return new List<string>(_registeredAgents);
            }
        }

        /// <summary>
        /// Clears all subscribers and queued messages.
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                lock (_messageQueue)
                {
                    _directSubscribers.Clear();
                    _typedSubscribers.Clear();
                    _broadcastHandlers.Clear();
                    _messageQueue.Clear();
                    _registeredAgents.Clear();
                }
            }
            _processing = false;
        }

        private void Dispatch(AgentMessage message)
        {
            if (!string.IsNullOrEmpty(message.receiverId))
            {
                if (_directSubscribers.ContainsKey(message.receiverId))
                {
                    var handlers = new List<MessageHandler>(_directSubscribers[message.receiverId]);
                    foreach (var handler in handlers)
                    {
                        try { handler(message); }
                        catch (Exception ex) { Debug.LogError($"MessageBus direct handler error: {ex}"); }
                    }
                }
            }

            if (_typedSubscribers.ContainsKey(message.type))
            {
                var handlers = new List<MessageHandler>(_typedSubscribers[message.type]);
                foreach (var handler in handlers)
                {
                    try { handler(message); }
                    catch (Exception ex) { Debug.LogError($"MessageBus typed handler error: {ex}"); }
                }
            }

            if (string.IsNullOrEmpty(message.receiverId))
            {
                var broadcastCopy = new List<MessageHandler>(_broadcastHandlers);
                foreach (var handler in broadcastCopy)
                {
                    try { handler(message); }
                    catch (Exception ex) { Debug.LogError($"MessageBus broadcast handler error: {ex}"); }
                }
            }
        }
    }
}
