using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Mission
{
    /// <summary>
    /// A single dialogue choice presented to the player.
    /// </summary>
    [Serializable]
    public sealed class DialogueChoice
    {
        /// <summary>
        /// Text displayed for this choice.
        /// </summary>
        public string text;

        /// <summary>
        /// Index of this choice within the parent node.
        /// </summary>
        public int index;

        /// <summary>
        /// ID of the dialogue node to transition to when this choice is selected. Empty means end dialogue.
        /// </summary>
        public string nextNodeId;

        /// <summary>
        /// Actions to execute when this choice is selected.
        /// </summary>
        public List<ConsequenceAction> consequenceActions;

        /// <summary>
        /// Condition that must be satisfied for this choice to be available.
        /// </summary>
        public ObjectiveCondition availabilityCondition;

        /// <summary>
        /// Indicates whether this choice is currently available based on its condition.
        /// </summary>
        public bool IsAvailable => availabilityCondition == null || availabilityCondition.IsSatisfied;
    }

    /// <summary>
    /// A single node within a dialogue graph.
    /// </summary>
    [Serializable]
    public sealed class DialogueNode
    {
        /// <summary>
        /// Unique identifier for the node.
        /// </summary>
        public string nodeId;

        /// <summary>
        /// ID of the speaker for this node.
        /// </summary>
        public string speakerId;

        /// <summary>
        /// Display name of the speaker.
        /// </summary>
        public string speakerName;

        /// <summary>
        /// Dialogue text spoken in this node.
        /// </summary>
        public string text;

        /// <summary>
        /// Available player choices at this node.
        /// </summary>
        public List<DialogueChoice> choices;

        /// <summary>
        /// Audio clip name or path for voice-over, if any.
        /// </summary>
        public string audioClip;

        /// <summary>
        /// Duration in seconds before the dialogue auto-advances. Zero means manual advance only.
        /// </summary>
        public float autoAdvanceDelay;

        /// <summary>
        /// Animation trigger name to play on the speaker when this node begins.
        /// </summary>
        public string animationTrigger;

        /// <summary>
        /// Indicates whether this node ends the dialogue sequence.
        /// </summary>
        public bool IsEndNode => choices == null || choices.Count == 0;

        /// <summary>
        /// Indicates whether at least one choice is currently available.
        /// </summary>
        public bool HasAvailableChoices
        {
            get
            {
                if (choices == null) return false;
                foreach (DialogueChoice choice in choices)
                {
                    if (choice.IsAvailable)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>
    /// A sequence of dialogue nodes forming a branching conversation.
    /// </summary>
    [Serializable]
    public sealed class DialogueSequence
    {
        /// <summary>
        /// Unique identifier for the dialogue sequence.
        /// </summary>
        public string sequenceId;

        /// <summary>
        /// Title of the dialogue sequence for editor and debugging.
        /// </summary>
        public string sequenceName;

        /// <summary>
        /// All nodes in the dialogue graph.
        /// </summary>
        public List<DialogueNode> nodes;

        /// <summary>
        /// ID of the node where the dialogue begins.
        /// </summary>
        public string startNodeId;

        /// <summary>
        /// Retrieves a node by its ID.
        /// </summary>
        /// <param name="nodeId">The ID of the node to retrieve.</param>
        /// <returns>The matching DialogueNode, or null if not found.</returns>
        public DialogueNode GetNode(string nodeId)
        {
            if (nodes == null) return null;
            foreach (DialogueNode node in nodes)
            {
                if (node.nodeId == nodeId)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieves the starting node of the sequence.
        /// </summary>
        /// <returns>The starting DialogueNode, or null if not found.</returns>
        public DialogueNode GetStartNode()
        {
            return GetNode(startNodeId);
        }
    }
}
