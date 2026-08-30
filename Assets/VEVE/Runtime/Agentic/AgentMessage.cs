using System;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines the type of an agent message.
    /// </summary>
    public enum MessageType
    {
        Command,
        Report,
        Request,
        Response,
        Alert,
        Coordination,
        Acknowledgment
    }

    /// <summary>
    /// Defines the priority level of an agent message.
    /// </summary>
    public enum MessagePriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Serializable payload data for agent messages.
    /// </summary>
    [Serializable]
    public class MessagePayload
    {
        /// <summary>Text content of the message.</summary>
        public string text;

        /// <summary>Optional numeric value associated with the message.</summary>
        public float numericValue;

        /// <summary>Optional position reference.</summary>
        public Vector3 position;

        /// <summary>Optional custom data identifier.</summary>
        public string dataId;
    }

    /// <summary>
    /// Represents a message exchanged between agents in the system.
    /// </summary>
    [Serializable]
    public class AgentMessage
    {
        /// <summary>Unique identifier for this message.</summary>
        public string messageId;

        /// <summary>Identifier of the sending agent.</summary>
        public string senderId;

        /// <summary>Identifier of the intended recipient. Empty for broadcasts.</summary>
        public string receiverId;

        /// <summary>The type of message being sent.</summary>
        public MessageType type;

        /// <summary>The priority level of this message.</summary>
        public MessagePriority priority;

        /// <summary>Timestamp when the message was created.</summary>
        public float timestamp;

        /// <summary>The payload data carried by this message.</summary>
        public MessagePayload payload;

        /// <summary>
        /// Creates a new agent message with default values.
        /// </summary>
        public AgentMessage()
        {
            messageId = Guid.NewGuid().ToString("N");
            senderId = string.Empty;
            receiverId = string.Empty;
            type = MessageType.Report;
            priority = MessagePriority.Normal;
            timestamp = Time.time;
            payload = new MessagePayload();
        }

        /// <summary>
        /// Creates a new agent message with specified parameters.
        /// </summary>
        /// <param name="sender">The sending agent's identifier.</param>
        /// <param name="receiver">The recipient agent's identifier. Empty for broadcasts.</param>
        /// <param name="messageType">The type of message.</param>
        /// <param name="messagePriority">The priority level.</param>
        /// <param name="messagePayload">The payload data.</param>
        public AgentMessage(string sender, string receiver, MessageType messageType, MessagePriority messagePriority, MessagePayload messagePayload)
        {
            messageId = Guid.NewGuid().ToString("N");
            senderId = sender;
            receiverId = receiver;
            type = messageType;
            priority = messagePriority;
            timestamp = Time.time;
            payload = messagePayload;
        }

        /// <summary>
        /// Returns a string representation of the message.
        /// </summary>
        public override string ToString()
        {
            return $"[{priority}] {type} from {senderId} to {(string.IsNullOrEmpty(receiverId) ? "ALL" : receiverId)}: {payload?.text}";
        }
    }
}
