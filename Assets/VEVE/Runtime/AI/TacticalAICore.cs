using UnityEngine;
using System.Collections.Generic;
using VEVE.AI;

namespace VEVE
{
    /// <summary>
    /// Represents the current tactical stance of an AI agent.
    /// </summary>
    public enum TacticalStance { Patrol, Investigate, Engage, Suppress, Flank, Retreat, Hold, Communicate, Search, React }

    /// <summary>
    /// Encapsulates the result of a tactical decision.
    /// </summary>
    public readonly struct TacticalDecision
    {
        /// <summary>
        /// Gets the recommended tactical stance.
        /// </summary>
        public readonly TacticalStance stance;

        /// <summary>
        /// Gets the destination vector for the chosen action.
        /// </summary>
        public readonly Vector3 destination;

        /// <summary>
        /// Gets the urgency level of the decision.
        /// </summary>
        public readonly float urgency;

        /// <summary>
        /// Gets the confidence level of the decision.
        /// </summary>
        public readonly float confidence;

        /// <summary>
        /// Initializes a new instance of the TacticalDecision struct.
        /// </summary>
        /// <param name="stance">The tactical stance.</param>
        /// <param name="destination">The destination vector.</param>
        /// <param name="urgency">The urgency value.</param>
        /// <param name="confidence">The confidence value.</param>
        public TacticalDecision(TacticalStance stance, Vector3 destination, float urgency, float confidence)
        {
            this.stance = stance;
            this.destination = destination;
            this.urgency = urgency;
            this.confidence = confidence;
        }
    }

    /// <summary>
    /// Core static class for evaluating tactical situations and generating behavior trees.
    /// </summary>
    public static class TacticalAICore
    {
        /// <summary>
        /// Evaluates the tactical situation and returns a decision.
        /// </summary>
        /// <param name="threatDistance">Distance to the nearest threat.</param>
        /// <param name="threatVisibility">Visibility of the threat (0-1).</param>
        /// <param name="coverQuality">Quality of nearby cover (0-1).</param>
        /// <param name="allyCount">Number of nearby allies.</param>
        /// <param name="health">Current health of the agent.</param>
        /// <param name="ammo">Current ammunition level.</param>
        /// <param name="stress">Current stress level.</param>
        /// <param name="lastKnownThreatPosition">Last known position of the threat.</param>
        /// <param name="currentPosition">Current position of the agent.</param>
        /// <returns>A TacticalDecision describing the recommended action.</returns>
        public static TacticalDecision EvaluateSituation(
            float threatDistance,
            float threatVisibility,
            float coverQuality,
            float allyCount,
            float health,
            float ammo,
            float stress,
            Vector3 lastKnownThreatPosition,
            Vector3 currentPosition)
        {
            float dangerLevel = CalculateDangerLevel(threatDistance, threatVisibility, coverQuality, health, ammo);
            float confidence = CalculateConfidence(coverQuality, allyCount, health, ammo);
            float urgency = Mathf.Clamp01(dangerLevel * 0.7f + stress * 0.3f);

            TacticalStance stance = dangerLevel > 0.7f && coverQuality < 0.3f
                ? TacticalStance.Retreat
                : dangerLevel > 0.5f && ammo > 0.3f
                ? TacticalStance.Suppress
                : dangerLevel > 0.3f
                ? TacticalStance.Engage
                : threatVisibility > 0.5f
                ? TacticalStance.Investigate
                : TacticalStance.Patrol;

            if (coverQuality > 0.6f && dangerLevel > 0.4f)
                stance = TacticalStance.Hold;

            if (allyCount >= 2 && dangerLevel > 0.5f && ammo > 0.5f)
                stance = TacticalStance.Flank;

            Vector3 destination = lastKnownThreatPosition != Vector3.zero
                ? lastKnownThreatPosition
                : currentPosition;

            return new TacticalDecision(stance, destination, urgency, confidence);
        }

        /// <summary>
        /// Creates a behavior tree based on the tactical decision and agent state.
        /// </summary>
        /// <param name="decision">The tactical decision to base the tree on.</param>
        /// <param name="health">Current health of the agent.</param>
        /// <param name="ammo">Current ammunition level.</param>
        /// <param name="role">The squad role of the agent.</param>
        /// <returns>A root Node representing the behavior tree.</returns>
        public static Node CreateBehaviorTree(TacticalDecision decision, float health, float ammo, SquadRole role)
        {
            Node root = BuildTreeForStance(decision.stance, health, ammo, role);
            return root;
        }

        private static Node BuildTreeForStance(TacticalStance stance, float health, float ammo, SquadRole role)
        {
            Node baseTree = BuildBaseTree(stance);

            if (role == SquadRole.Support)
            {
                return new SequenceNode
                {
                    Children =
                    {
                        new IsLowHealthNode { threshold = 40f },
                        new RetreatNode { retreatDistance = 20f },
                        baseTree
                    }
                };
            }

            if (role == SquadRole.Medic)
            {
                return new SelectorNode
                {
                    Children =
                    {
                        new SequenceNode
                        {
                            Children =
                            {
                                new IsLowHealthNode { threshold = 50f },
                                new RetreatNode { retreatDistance = 15f }
                            }
                        },
                        baseTree
                    }
                };
            }

            if (role == SquadRole.Marksman)
            {
                return new SequenceNode
                {
                    Children =
                    {
                        new HasLineOfSightNode { maxDistance = 150f },
                        new AttackNode { fireRate = 0.15f, accuracy = 0.95f }
                    }
                };
            }

            if (role == SquadRole.Assault)
            {
                return new SequenceNode
                {
                    Children =
                    {
                        new IsLowHealthNode { threshold = 25f },
                        new RetreatNode { retreatDistance = 12f },
                        baseTree
                    }
                };
            }

            return baseTree;
        }

        private static Node BuildBaseTree(TacticalStance stance)
        {
            switch (stance)
            {
                case TacticalStance.Engage:
                    return new SelectorNode
                    {
                        Children =
                        {
                            new SequenceNode
                            {
                                Children =
                                {
                                    new HasLineOfSightNode { maxDistance = 100f },
                                    new AttackNode { fireRate = 0.08f, accuracy = 0.85f }
                                }
                            },
                            new SequenceNode
                            {
                                Children =
                                {
                                    new InvestigateNode { investigateDuration = 2f }
                                }
                            }
                        }
                    };

                case TacticalStance.Retreat:
                    return new SequenceNode
                    {
                        Children =
                        {
                            new RetreatNode { retreatDistance = 25f },
                            new MoveToNode { speed = 6f, acceptRadius = 2f }
                        }
                    };

                case TacticalStance.Suppress:
                    return new SequenceNode
                    {
                        Children =
                        {
                            new AttackNode { fireRate = 0.05f, accuracy = 0.4f }
                        }
                    };

                case TacticalStance.Flank:
                    return new SequenceNode
                    {
                        Children =
                        {
                            new MoveToNode { speed = 5f, acceptRadius = 1.5f },
                            new AttackNode { fireRate = 0.07f, accuracy = 0.75f }
                        }
                    };

                case TacticalStance.Hold:
                    return new SequenceNode
                    {
                        Children =
                        {
                            new TakeCoverNode { searchRadius = 10f },
                            new AttackNode { fireRate = 0.1f, accuracy = 0.9f }
                        }
                    };

                case TacticalStance.Investigate:
                    return new SequenceNode
                    {
                        Children =
                        {
                            new InvestigateNode { investigateDuration = 3f },
                            new SelectorNode
                            {
                                Children =
                                {
                                    new SequenceNode
                                    {
                                        Children =
                                        {
                                            new CanSeeEnemyNode { viewDistance = 35f, viewAngle = 100f },
                                            new AttackNode { fireRate = 0.09f, accuracy = 0.8f }
                                        }
                                    },
                                    new PatrolNode { patrolRadius = 10f }
                                }
                            }
                        }
                    };

                case TacticalStance.Patrol:
                default:
                    return new SelectorNode
                    {
                        Children =
                        {
                            new SequenceNode
                            {
                                Children =
                                {
                                    new CanSeeEnemyNode { viewDistance = 30f, viewAngle = 90f },
                                    new AttackNode { fireRate = 0.1f, accuracy = 0.7f }
                                }
                            },
                            new PatrolNode { patrolRadius = 15f }
                        }
                    };
            }
        }

        private static float CalculateDangerLevel(float threatDistance, float threatVisibility, float coverQuality, float health, float ammo)
        {
            float distanceFactor = 1.0f - Mathf.Clamp01(threatDistance / 50f);
            float visibilityFactor = threatVisibility * 0.6f;
            float coverFactor = 1.0f - coverQuality;
            float healthFactor = 1.0f - Mathf.Clamp01(health / 100f);
            float ammoFactor = 1.0f - Mathf.Clamp01(ammo);
            return Mathf.Clamp01(distanceFactor * 0.25f + visibilityFactor * 0.25f + coverFactor * 0.2f + healthFactor * 0.15f + ammoFactor * 0.15f);
        }

        private static float CalculateConfidence(float coverQuality, float allyCount, float health, float ammo)
        {
            return Mathf.Clamp01(
                coverQuality * 0.35f +
                Mathf.Clamp01(allyCount / 3f) * 0.25f +
                Mathf.Clamp01(health / 100f) * 0.25f +
                Mathf.Clamp01(ammo) * 0.15f
            );
        }
    }
}
