using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines signal quality levels for distance-based communication.
    /// </summary>
    public enum SignalQuality
    {
        Clear,
        Degraded,
        Weak,
        Unintelligible
    }

    /// <summary>
    /// Handles distance-based communication with signal degradation.
    /// </summary>
    public class CommunicationRange : MonoBehaviour
    {
        [SerializeField] private float maxRange = 50f;
        [SerializeField] private float optimalRange = 15f;
        [SerializeField] private float degradedRange = 35f;
        [SerializeField] private float environmentObstructionFactor = 0.3f;
        [SerializeField] private float messageLossThreshold = 0.7f;

        private float _obstructionLevel;

        /// <summary>
        /// Gets the maximum communication range.
        /// </summary>
        public float MaxRange => maxRange;

        /// <summary>
        /// Gets the optimal communication range with no degradation.
        /// </summary>
        public float OptimalRange => optimalRange;

        /// <summary>
        /// Gets the current environmental obstruction level.
        /// </summary>
        public float ObstructionLevel => _obstructionLevel;

        /// <summary>
        /// Initializes the communication range system.
        /// </summary>
        private void Awake()
        {
            _obstructionLevel = 0f;
        }

        /// <summary>
        /// Sets the environmental obstruction level affecting communication.
        /// </summary>
        /// <param name="obstruction">Obstruction level from 0 (clear) to 1 (fully obstructed).</param>
        public void SetObstructionLevel(float obstruction)
        {
            _obstructionLevel = Mathf.Clamp01(obstruction);
        }

        /// <summary>
        /// Calculates the signal quality between two positions.
        /// </summary>
        /// <param name="senderPosition">Position of the sender.</param>
        /// <param name="receiverPosition">Position of the receiver.</param>
        /// <returns>The calculated signal quality.</returns>
        public SignalQuality CalculateSignalQuality(Vector3 senderPosition, Vector3 receiverPosition)
        {
            float distance = Vector3.Distance(senderPosition, receiverPosition);

            if (distance > maxRange)
                return SignalQuality.Unintelligible;

            float effectiveRange = maxRange * (1f - _obstructionLevel * environmentObstructionFactor);

            if (distance > effectiveRange)
                return SignalQuality.Unintelligible;

            if (distance <= optimalRange)
                return SignalQuality.Clear;

            if (distance <= degradedRange)
            {
                float t = (distance - optimalRange) / (degradedRange - optimalRange);
                return t < 0.5f ? SignalQuality.Clear : SignalQuality.Degraded;
            }

            float outerT = (distance - degradedRange) / (effectiveRange - degradedRange);
            return outerT < 0.5f ? SignalQuality.Degraded : SignalQuality.Weak;
        }

        /// <summary>
        /// Calculates the transmission success probability based on signal quality.
        /// </summary>
        /// <param name="quality">The signal quality level.</param>
        /// <returns>Probability of successful transmission (0-1).</returns>
        public float GetTransmissionSuccessProbability(SignalQuality quality)
        {
            switch (quality)
            {
                case SignalQuality.Clear:
                    return 1f;
                case SignalQuality.Degraded:
                    return 0.8f;
                case SignalQuality.Weak:
                    return 0.5f;
                case SignalQuality.Unintelligible:
                    return 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Determines if a message can be transmitted between two positions.
        /// </summary>
        /// <param name="senderPosition">Position of the sender.</param>
        /// <param name="receiverPosition">Position of the receiver.</param>
        /// <returns>True if transmission is possible.</returns>
        public bool CanCommunicate(Vector3 senderPosition, Vector3 receiverPosition)
        {
            SignalQuality quality = CalculateSignalQuality(senderPosition, receiverPosition);
            return quality != SignalQuality.Unintelligible;
        }

        /// <summary>
        /// Attempts to transmit a message, applying degradation effects.
        /// </summary>
        /// <param name="senderPosition">Position of the sender.</param>
        /// <param name="receiverPosition">Position of the receiver.</param>
        /// <param name="messageContent">The message content to transmit.</param>
        /// <param name="receivedContent">The received content after potential degradation.</param>
        /// <returns>True if the message was successfully transmitted.</returns>
        public bool TryTransmit(Vector3 senderPosition, Vector3 receiverPosition, string messageContent, out string receivedContent)
        {
            receivedContent = messageContent;
            SignalQuality quality = CalculateSignalQuality(senderPosition, receiverPosition);

            if (quality == SignalQuality.Unintelligible)
            {
                receivedContent = string.Empty;
                return false;
            }

            float probability = GetTransmissionSuccessProbability(quality);
            if (Random.value > probability)
            {
                receivedContent = string.Empty;
                return false;
            }

            if (quality == SignalQuality.Weak)
            {
                receivedContent = ApplyDegradation(messageContent, 0.5f);
            }
            else if (quality == SignalQuality.Degraded)
            {
                receivedContent = ApplyDegradation(messageContent, 0.2f);
            }

            return true;
        }

        /// <summary>
        /// Calculates the effective range considering environmental factors.
        /// </summary>
        /// <returns>The effective maximum range.</returns>
        public float GetEffectiveRange()
        {
            return maxRange * (1f - _obstructionLevel * environmentObstructionFactor);
        }

        private string ApplyDegradation(string content, float degradationFactor)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            if (degradationFactor >= 0.8f)
                return "...";

            int visibleLength = Mathf.Max(1, Mathf.RoundToInt(content.Length * (1f - degradationFactor)));
            return content.Substring(0, visibleLength) + "...";
        }
    }
}
