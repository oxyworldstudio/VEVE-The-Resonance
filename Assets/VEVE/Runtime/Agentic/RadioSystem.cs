using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Defines the state of a radio channel.
    /// </summary>
    public enum RadioState
    {
        Off,
        Transmitting,
        Receiving,
        Jammed
    }

    /// <summary>
    /// Represents a radio frequency configuration.
    /// </summary>
    [System.Serializable]
    public class RadioFrequency
    {
        /// <summary>Frequency identifier.</summary>
        public string id;

        /// <summary>Frequency value in MHz.</summary>
        public float frequencyMHz;

        /// <summary>Encryption key for secure communication.</summary>
        public string encryptionKey;

        /// <summary>True if this frequency is encrypted.</summary>
        public bool isEncrypted;

        /// <summary>Maximum range of this frequency.</summary>
        public float maxRange;

        /// <summary>
        /// Creates a new radio frequency.
        /// </summary>
        public RadioFrequency(string id, float frequencyMHz, float maxRange, string encryptionKey = "")
        {
            this.id = id;
            this.frequencyMHz = frequencyMHz;
            this.maxRange = maxRange;
            this.encryptionKey = encryptionKey;
            this.isEncrypted = !string.IsNullOrEmpty(encryptionKey);
        }
    }

    /// <summary>
    /// Manages radio communication with frequency, encryption, and jamming.
    /// </summary>
    public class RadioSystem : MonoBehaviour
    {
        [SerializeField] private string radioOwnerId = string.Empty;
        [SerializeField] private float maxTransmitPower = 100f;
        [SerializeField] private float jammingSusceptibility = 0.5f;
        [SerializeField] private float encryptionStrength = 0.8f;

        private readonly List<RadioFrequency> _availableFrequencies = new List<RadioFrequency>();
        private readonly List<string> _jammedFrequencies = new List<string>();
        private readonly Queue<AgentMessage> _transmissionQueue = new Queue<AgentMessage>();

        private RadioFrequency _currentFrequency;
        private RadioState _currentState;
        private float _currentJammingLevel;
        private bool _isTransmitting;

        /// <summary>
        /// Gets the current radio state.
        /// </summary>
        public RadioState CurrentState => _currentState;

        /// <summary>
        /// Gets the currently tuned frequency.
        /// </summary>
        public RadioFrequency CurrentFrequency => _currentFrequency;

        /// <summary>
        /// Gets the current jamming level affecting this radio.
        /// </summary>
        public float CurrentJammingLevel => _currentJammingLevel;

        /// <summary>
        /// Event raised when a message is received via radio.
        /// </summary>
        public System.Action<AgentMessage> OnMessageReceived { get; set; }

        /// <summary>
        /// Initializes the radio system.
        /// </summary>
        private void Awake()
        {
            _currentState = RadioState.Off;
            _currentJammingLevel = 0f;
            _isTransmitting = false;
        }

        /// <summary>
        /// Registers an available frequency for this radio.
        /// </summary>
        /// <param name="frequency">The frequency to register.</param>
        public void RegisterFrequency(RadioFrequency frequency)
        {
            if (frequency == null) return;
            if (!_availableFrequencies.Exists(f => f.id == frequency.id))
                _availableFrequencies.Add(frequency);
        }

        /// <summary>
        /// Unregisters a frequency.
        /// </summary>
        /// <param name="frequencyId">The identifier of the frequency to remove.</param>
        public void UnregisterFrequency(string frequencyId)
        {
            _availableFrequencies.RemoveAll(f => f.id == frequencyId);
            if (_currentFrequency != null && _currentFrequency.id == frequencyId)
                _currentFrequency = null;
        }

        /// <summary>
        /// Tunes the radio to a specific frequency.
        /// </summary>
        /// <param name="frequencyId">The identifier of the frequency to tune to.</param>
        /// <returns>True if tuning was successful.</returns>
        public bool TuneToFrequency(string frequencyId)
        {
            var frequency = _availableFrequencies.Find(f => f.id == frequencyId);
            if (frequency == null)
                return false;

            _currentFrequency = frequency;
            _currentState = _jammedFrequencies.Contains(frequencyId) ? RadioState.Jammed : RadioState.Receiving;
            return true;
        }

        /// <summary>
        /// Turns the radio on.
        /// </summary>
        public void PowerOn()
        {
            if (_currentFrequency != null)
                _currentState = _jammedFrequencies.Contains(_currentFrequency.id) ? RadioState.Jammed : RadioState.Receiving;
            else
                _currentState = RadioState.Receiving;
        }

        /// <summary>
        /// Turns the radio off.
        /// </summary>
        public void PowerOff()
        {
            _currentState = RadioState.Off;
            _isTransmitting = false;
        }

        /// <summary>
        /// Transmits a message over the current frequency.
        /// </summary>
        /// <param name="message">The message to transmit.</param>
        /// <returns>True if transmission was initiated successfully.</returns>
        public bool Transmit(AgentMessage message)
        {
            if (_currentState == RadioState.Off || _currentFrequency == null)
                return false;

            if (_jammedFrequencies.Contains(_currentFrequency.id))
                return false;

            _transmissionQueue.Enqueue(message);
            _currentState = RadioState.Transmitting;
            _isTransmitting = true;
            return true;
        }

        /// <summary>
        /// Applies jamming to a specific frequency.
        /// </summary>
        /// <param name="frequencyId">The frequency to jam.</param>
        /// <param name="jammingLevel">The jamming intensity (0-1).</param>
        public void ApplyJamming(string frequencyId, float jammingLevel)
        {
            float effectiveJamming = jammingLevel * jammingSusceptibility;
            if (effectiveJamming > 0.3f && !_jammedFrequencies.Contains(frequencyId))
            {
                _jammedFrequencies.Add(frequencyId);
                if (_currentFrequency != null && _currentFrequency.id == frequencyId)
                {
                    _currentState = RadioState.Jammed;
                    _isTransmitting = false;
                }
            }
            _currentJammingLevel = Mathf.Max(_currentJammingLevel, effectiveJamming);
        }

        /// <summary>
        /// Removes jamming from a specific frequency.
        /// </summary>
        /// <param name="frequencyId">The frequency to clear.</param>
        public void ClearJamming(string frequencyId)
        {
            _jammedFrequencies.Remove(frequencyId);
            if (_currentFrequency != null && _currentFrequency.id == frequencyId && _currentState == RadioState.Jammed)
                _currentState = RadioState.Receiving;

            if (_jammedFrequencies.Count == 0)
                _currentJammingLevel = 0f;
        }

        /// <summary>
        /// Receives a message from another radio on the same frequency.
        /// </summary>
        /// <param name="message">The incoming message.</param>
        /// <param name="senderPosition">Position of the sender.</param>
        /// <param name="receiverPosition">Position of the receiver.</param>
        public void ReceiveMessage(AgentMessage message, Vector3 senderPosition, Vector3 receiverPosition)
        {
            if (_currentState == RadioState.Off || _currentFrequency == null)
                return;

            if (!CanReceiveFrom(senderPosition, receiverPosition))
                return;

            if (_currentFrequency.isEncrypted)
            {
                if (!VerifyEncryption(message))
                    return;
            }

            if (_currentState == RadioState.Jammed)
                return;

            _currentState = RadioState.Receiving;
            OnMessageReceived?.Invoke(message);
        }

        /// <summary>
        /// Gets all available frequencies.
        /// </summary>
        /// <returns>A list of available frequencies.</returns>
        public List<RadioFrequency> GetAvailableFrequencies()
        {
            return new List<RadioFrequency>(_availableFrequencies);
        }

        /// <summary>
        /// Gets the list of currently jammed frequency IDs.
        /// </summary>
        /// <returns>A list of jammed frequency identifiers.</returns>
        public List<string> GetJammedFrequencies()
        {
            return new List<string>(_jammedFrequencies);
        }

        /// <summary>
        /// Sets the transmit power level.
        /// </summary>
        /// <param name="power">Power level from 0 to maxTransmitPower.</param>
        public void SetTransmitPower(float power)
        {
            maxTransmitPower = Mathf.Max(0f, power);
        }

        /// <summary>
        /// Calculates the effective transmit range based on power.
        /// </summary>
        /// <returns>The effective range in world units.</returns>
        public float GetEffectiveRange()
        {
            if (_currentFrequency == null)
                return 0f;

            float powerFactor = maxTransmitPower / 100f;
            float jammingReduction = 1f - (_currentJammingLevel * jammingSusceptibility);
            return _currentFrequency.maxRange * powerFactor * jammingReduction;
        }

        private bool CanReceiveFrom(Vector3 senderPosition, Vector3 receiverPosition)
        {
            if (_currentFrequency == null)
                return false;

            float distance = Vector3.Distance(senderPosition, receiverPosition);
            return distance <= GetEffectiveRange();
        }

        private bool VerifyEncryption(AgentMessage message)
        {
            if (message?.payload == null)
                return false;

            if (_currentFrequency == null || !_currentFrequency.isEncrypted)
                return true;

            return Random.value < encryptionStrength;
        }
    }
}
