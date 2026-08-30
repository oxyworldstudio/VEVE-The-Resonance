using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Represents a recognized speech command with associated action data.
    /// </summary>
    [System.Serializable]
    public class RecognizedCommand
    {
        /// <summary>The command identifier.</summary>
        public string commandId;

        /// <summary>Confidence level of the recognition (0-1).</summary>
        public float confidence;

        /// <summary>Parameters extracted from the command.</summary>
        public string parameters;

        /// <summary>Raw transcribed text.</summary>
        public string rawText;
    }

    /// <summary>
    /// Simulated speech recognition system for understanding voice commands.
    /// </summary>
    public class SpeechRecognition : MonoBehaviour
    {
        [SerializeField] private float recognitionThreshold = 0.6f;
        [SerializeField] private float ambientNoiseInterference = 0.1f;
        [SerializeField] private float stressPenalty = 0.05f;

        private readonly Dictionary<string, string> _commandPatterns = new Dictionary<string, string>();
        private readonly List<RecognizedCommand> _commandHistory = new List<RecognizedCommand>();
        private float _currentStressLevel;
        private bool _isActive;

        /// <summary>
        /// Event raised when a command is recognized.
        /// </summary>
        public System.Action<RecognizedCommand> OnCommandRecognized { get; set; }

        /// <summary>
        /// Gets the current stress level affecting recognition accuracy.
        /// </summary>
        public float CurrentStressLevel => _currentStressLevel;

        /// <summary>
        /// Gets whether the recognition system is active.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Initializes the speech recognition system.
        /// </summary>
        private void Awake()
        {
            InitializeCommandPatterns();
            _isActive = true;
            _currentStressLevel = 0f;
        }

        /// <summary>
        /// Sets the current stress level affecting recognition accuracy.
        /// </summary>
        /// <param name="stress">Stress level from 0 to 1.</param>
        public void SetStressLevel(float stress)
        {
            _currentStressLevel = Mathf.Clamp01(stress);
        }

        /// <summary>
        /// Enables or disables the recognition system.
        /// </summary>
        /// <param name="active">True to enable recognition.</param>
        public void SetActive(bool active)
        {
            _isActive = active;
        }

        /// <summary>
        /// Registers a command pattern for recognition.
        /// </summary>
        /// <param name="commandId">The command identifier.</param>
        /// <param name="pattern">The pattern or keyword to recognize.</param>
        public void RegisterCommand(string commandId, string pattern)
        {
            if (!string.IsNullOrEmpty(commandId) && !string.IsNullOrEmpty(pattern))
                _commandPatterns[commandId] = pattern.ToLowerInvariant();
        }

        /// <summary>
        /// Unregisters a command pattern.
        /// </summary>
        /// <param name="commandId">The command identifier to remove.</param>
        public void UnregisterCommand(string commandId)
        {
            _commandPatterns.Remove(commandId);
        }

        /// <summary>
        /// Processes raw input text and attempts to recognize a command.
        /// </summary>
        /// <param name="inputText">The raw input text to process.</param>
        /// <returns>The recognized command, or null if no command was recognized.</returns>
        public RecognizedCommand ProcessInput(string inputText)
        {
            if (!_isActive || string.IsNullOrEmpty(inputText))
                return null;

            string normalized = inputText.ToLowerInvariant().Trim();
            RecognizedCommand bestMatch = null;
            float bestScore = 0f;

            foreach (var kvp in _commandPatterns)
            {
                float score = CalculateMatchScore(normalized, kvp.Value);
                if (score > bestScore && score >= recognitionThreshold)
                {
                    bestScore = score;
                    bestMatch = new RecognizedCommand
                    {
                        commandId = kvp.Key,
                        confidence = score,
                        parameters = ExtractParameters(normalized, kvp.Value),
                        rawText = inputText
                    };
                }
            }

            if (bestMatch != null)
            {
                _commandHistory.Add(bestMatch);
                OnCommandRecognized?.Invoke(bestMatch);
            }

            return bestMatch;
        }

        /// <summary>
        /// Processes ambient speech for passive recognition (lower accuracy).
        /// </summary>
        /// <param name="inputText">The raw input text heard from ambient sources.</param>
        /// <returns>The recognized command, or null if confidence is too low.</returns>
        public RecognizedCommand ProcessAmbientSpeech(string inputText)
        {
            if (!_isActive || string.IsNullOrEmpty(inputText))
                return null;

            string normalized = inputText.ToLowerInvariant().Trim();
            RecognizedCommand bestMatch = null;
            float bestScore = 0f;

            foreach (var kvp in _commandPatterns)
            {
                float score = CalculateMatchScore(normalized, kvp.Value) * (1f - ambientNoiseInterference);
                if (score > bestScore && score >= recognitionThreshold * 0.8f)
                {
                    bestScore = score;
                    bestMatch = new RecognizedCommand
                    {
                        commandId = kvp.Key,
                        confidence = score,
                        parameters = ExtractParameters(normalized, kvp.Value),
                        rawText = inputText
                    };
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Gets the command recognition history.
        /// </summary>
        /// <returns>A list of previously recognized commands.</returns>
        public List<RecognizedCommand> GetCommandHistory()
        {
            return new List<RecognizedCommand>(_commandHistory);
        }

        /// <summary>
        /// Clears the command history.
        /// </summary>
        public void ClearHistory()
        {
            _commandHistory.Clear();
        }

        private void InitializeCommandPatterns()
        {
            _commandPatterns["move_to"] = "move to";
            _commandPatterns["hold_position"] = "hold position";
            _commandPatterns["open_fire"] = "open fire";
            _commandPatterns["cease_fire"] = "cease fire";
            _commandPatterns["fall_back"] = "fall back";
            _commandPatterns["regroup"] = "regroup";
            _commandPatterns["report_status"] = "report status";
            _commandPatterns["enemy_spotted"] = "enemy spotted";
            _commandPatterns["need_reinforcement"] = "need reinforcement";
            _commandPatterns["covering_fire"] = "covering fire";
        }

        private float CalculateMatchScore(string input, string pattern)
        {
            if (input.Contains(pattern))
                return 1f - (_currentStressLevel * stressPenalty);

            string[] patternWords = pattern.Split(' ');
            int matchCount = 0;

            foreach (var word in patternWords)
            {
                if (input.Contains(word))
                    matchCount++;
            }

            if (patternWords.Length == 0)
                return 0f;

            float baseScore = (float)matchCount / patternWords.Length;
            float stressReduction = _currentStressLevel * stressPenalty;
            return Mathf.Max(0f, baseScore - stressReduction);
        }

        private string ExtractParameters(string input, string pattern)
        {
            if (!input.Contains(pattern))
                return string.Empty;

            int index = input.IndexOf(pattern);
            string after = input.Substring(index + pattern.Length).Trim();
            return string.IsNullOrEmpty(after) ? string.Empty : after;
        }
    }
}
