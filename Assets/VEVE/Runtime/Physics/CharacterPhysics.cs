using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    /// <summary>
    /// Represents a full-body IK joint configuration.
    /// </summary>
    [Serializable]
    public struct IKJointConfig
    {
        public string jointName;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public float stiffness;
        public float damping;
        public bool useAngularLimits;
        public Vector2 angularLimitsX;
        public Vector2 angularLimitsY;
        public Vector2 angularLimitsZ;
        public bool isActive;
    }

    /// <summary>
    /// Full-body IK state for a character.
    /// </summary>
    [Serializable]
    public struct FullBodyIKState
    {
        public Vector3 headPosition;
        public Quaternion headRotation;
        public Vector3 spinePosition;
        public Quaternion spineRotation;
        public Vector3 leftHandPosition;
        public Quaternion leftHandRotation;
        public Vector3 rightHandPosition;
        public Quaternion rightHandRotation;
        public Vector3 leftFootPosition;
        public Quaternion leftFootRotation;
        public Vector3 rightFootPosition;
        public Quaternion rightFootRotation;
        public float stabilityWeight;
        public float balanceWeight;
        public bool isConverged;
        public int iterationCount;
    }

    /// <summary>
    /// Represents balance recovery parameters.
    /// </summary>
    [Serializable]
    public struct BalanceRecoveryState
    {
        public float centerOfMassOffset;
        public float recoveryTorque;
        public float stepPhase;
        public float stepDirection;
        public bool isRecovering;
        public float recoveryProgress;
        public float stabilityMargin;
        public Vector3 supportPolygonCenter;
        public float supportPolygonArea;
    }

    [Serializable]
    public struct CenterOfMassState
    {
        public Vector3 position;
        public Vector3 velocity;
        public float mass;
        public float height;
        public float stability;
    }

    [Serializable]
    public struct JointState
    {
        public string jointName;
        public float torque;
        public float maxTorque;
        public float angle;
        public float angularVelocity;
        public bool isBroken;
        public float fatigue;
    }

    [Serializable]
    public struct MuscleState
    {
        public float strength;
        public float endurance;
        public float fatigue;
        public float recoveryRate;
        public float maximumForce;
    }

    /// <summary>
    /// Enhanced character physics with full-body IK, joint limit enforcement, and balance recovery.
    /// </summary>
    public static class CharacterPhysics
    {
        /// <summary>
        /// Calculates the center of mass for a character.
        /// </summary>
        public static CenterOfMassState CalculateCenterOfMass(Vector3 headPosition, Vector3 torsoPosition, Vector3 hipPosition, float headMass, float torsoMass, float legMass)
        {
            float totalMass = headMass + torsoMass + legMass;
            Vector3 com = (headPosition * headMass + torsoPosition * torsoMass + hipPosition * legMass) / totalMass;
            return new CenterOfMassState
            {
                position = com,
                velocity = Vector3.zero,
                mass = totalMass,
                height = com.y,
                stability = CalculateStability(com, hipPosition)
            };
        }

        /// <summary>
        /// Calculates the stability of a character based on center of mass and support base.
        /// </summary>
        public static float CalculateStability(Vector3 centerOfMass, Vector3 supportBase)
        {
            float horizontalDistance = Vector2.Distance(new Vector2(centerOfMass.x, centerOfMass.z), new Vector2(supportBase.x, supportBase.z));
            float height = centerOfMass.y - supportBase.y;
            float stability = Mathf.Clamp01(1f - (horizontalDistance / (height * 0.5f)));
            return stability;
        }

        /// <summary>
        /// Calculates joint torque with limit enforcement.
        /// </summary>
        public static JointState CalculateJointTorque(float muscleForce, float leverArm, float jointAngle, float maxTorque)
        {
            float torque = muscleForce * leverArm * Mathf.Sin(jointAngle * Mathf.Deg2Rad);
            float fatigue = Mathf.Clamp01(torque / maxTorque);
            return new JointState
            {
                jointName = "",
                torque = torque,
                maxTorque = maxTorque,
                angle = jointAngle,
                angularVelocity = 0f,
                isBroken = torque > maxTorque * 1.2f,
                fatigue = fatigue
            };
        }

        /// <summary>
        /// Calculates muscle fatigue with recovery modeling.
        /// </summary>
        public static MuscleState CalculateMuscleFatigue(float exertionLevel, float time, float recoveryTime)
        {
            float fatigueRate = exertionLevel * 0.1f;
            float recoveryRate = 1f / recoveryTime;
            float fatigue = Mathf.Clamp01(fatigueRate * time);
            float strength = Mathf.Lerp(1f, 0.3f, fatigue);
            float endurance = Mathf.Lerp(1f, 0.5f, fatigue);
            return new MuscleState
            {
                strength = strength,
                endurance = endurance,
                fatigue = fatigue,
                recoveryRate = recoveryRate,
                maximumForce = 1000f * strength
            };
        }

        /// <summary>
        /// Calculates injury degradation based on severity and body part.
        /// </summary>
        public static float CalculateInjuryDegradation(float injurySeverity, float bodyPartMass, float painLevel)
        {
            float baseDegradation = injurySeverity * 0.5f;
            float painMultiplier = 1f + painLevel * 0.01f;
            float massFactor = bodyPartMass / 70f;
            return Mathf.Clamp01(baseDegradation * painMultiplier * massFactor);
        }

        /// <summary>
        /// Calculates blood spatter force based on physiological parameters.
        /// </summary>
        public static float CalculateBloodSpatterForce(float bloodPressure, float woundSize, float heartRate)
        {
            float pressureFactor = bloodPressure / 120f;
            float sizeFactor = woundSize * 10f;
            float heartRateFactor = heartRate / 65f;
            return pressureFactor * sizeFactor * heartRateFactor * 0.1f;
        }

        /// <summary>
        /// Calculates blood spray direction based on wound normal and body orientation.
        /// </summary>
        public static Vector3 CalculateBloodSprayDirection(Vector3 woundNormal, Vector3 bodyOrientation, float pressure)
        {
            float randomAngle = UnityEngine.Random.Range(-30f, 30f);
            Vector3 baseDirection = -woundNormal;
            Quaternion randomRotation = Quaternion.Euler(randomAngle, randomAngle, 0f);
            Vector3 sprayDirection = randomRotation * baseDirection;
            return sprayDirection.normalized * pressure;
        }

        /// <summary>
        /// Calculates movement penalty based on injury distribution.
        /// </summary>
        public static float CalculateMovementPenalty(float leftLegInjury, float rightLegInjury, float torsoInjury, float armInjury)
        {
            float legPenalty = Mathf.Max(leftLegInjury, rightLegInjury) * 0.8f;
            float torsoPenalty = torsoInjury * 0.3f;
            float armPenalty = armInjury * 0.2f;
            return Mathf.Clamp01(legPenalty + torsoPenalty + armPenalty);
        }

        /// <summary>
        /// Calculates aim penalty based on injury distribution.
        /// </summary>
        public static float CalculateAimPenalty(float leftArmInjury, float rightArmInjury, float eyeInjury, float headInjury)
        {
            float armPenalty = Mathf.Max(leftArmInjury, rightArmInjury) * 0.7f;
            float visionPenalty = eyeInjury * 0.9f;
            float headPenalty = headInjury * 0.5f;
            return Mathf.Clamp01(armPenalty + visionPenalty + headPenalty);
        }

        /// <summary>
        /// Calculates consciousness probability based on physiological state.
        /// </summary>
        public static float CalculateConsciousnessProbability(float bloodLoss, float headInjury, float heartRate, float respirationRate)
        {
            float bloodLossFactor = Mathf.Clamp01(bloodLoss / 2f);
            float headFactor = headInjury * 0.8f;
            float heartRateFactor = Mathf.Clamp01((220f - heartRate) / 100f);
            float respirationFactor = Mathf.Clamp01(respirationRate / 30f);
            return Mathf.Clamp01(bloodLossFactor + headFactor - heartRateFactor * 0.3f - respirationFactor * 0.2f);
        }

        /// <summary>
        /// Enforces angular limits on a joint.
        /// </summary>
        public static JointState EnforceJointLimits(JointState joint, IKJointConfig config)
        {
            if (!config.useAngularLimits) return joint;
            Vector3 eulerAngles = new Vector3(joint.angle, 0f, 0f);
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, config.angularLimitsX.x, config.angularLimitsX.y);
            eulerAngles.y = Mathf.Clamp(eulerAngles.y, config.angularLimitsY.x, config.angularLimitsY.y);
            eulerAngles.z = Mathf.Clamp(eulerAngles.z, config.angularLimitsZ.x, config.angularLimitsZ.y);
            joint.angle = eulerAngles.x;
            if (joint.angularVelocity > 0f)
            {
                joint.torque = Mathf.Clamp(joint.torque, -joint.maxTorque, joint.maxTorque);
            }
            return joint;
        }

        /// <summary>
        /// Calculates full-body IK state using FABRIK algorithm approximation.
        /// </summary>
        public static FullBodyIKState SolveFullBodyIK(
            Vector3 rootPosition,
            Quaternion rootRotation,
            Vector3 targetHeadPosition,
            Vector3 targetLeftHand,
            Vector3 targetRightHand,
            Vector3 targetLeftFoot,
            Vector3 targetRightFoot,
            float stabilityWeight = 1f,
            int maxIterations = 10)
        {
            FullBodyIKState state = new FullBodyIKState
            {
                headPosition = targetHeadPosition,
                spinePosition = Vector3.Lerp(rootPosition, targetHeadPosition, 0.5f),
                leftHandPosition = targetLeftHand,
                rightHandPosition = targetRightHand,
                leftFootPosition = targetLeftFoot,
                rightFootPosition = targetRightFoot,
                stabilityWeight = stabilityWeight,
                balanceWeight = 0f,
                isConverged = false,
                iterationCount = 0
            };

            for (int i = 0; i < maxIterations; i++)
            {
                state.spinePosition = Vector3.Lerp(rootPosition, state.headPosition, 0.5f);
                state.spineRotation = Quaternion.LookRotation(state.headPosition - state.spinePosition);
                state.headRotation = Quaternion.LookRotation(state.headPosition - state.spinePosition);
                state.leftHandRotation = Quaternion.LookRotation(state.leftHandPosition - state.spinePosition);
                state.rightHandRotation = Quaternion.LookRotation(state.rightHandPosition - state.spinePosition);
                state.leftFootRotation = Quaternion.LookRotation(state.leftFootPosition - rootPosition);
                state.rightFootRotation = Quaternion.LookRotation(state.rightFootPosition - rootPosition);
                state.balanceWeight = CalculateBalanceWeight(state, rootPosition);
                state.iterationCount = i + 1;
                if (state.balanceWeight > 0.95f)
                {
                    state.isConverged = true;
                    break;
                }
            }
            return state;
        }

        /// <summary>
        /// Calculates balance weight for a full-body IK state.
        /// </summary>
        public static float CalculateBalanceWeight(FullBodyIKState ikState, Vector3 supportPoint)
        {
            Vector3 com = (ikState.headPosition + ikState.spinePosition + ikState.leftHandPosition + ikState.rightHandPosition +
                ikState.leftFootPosition + ikState.rightFootPosition) / 6f;
            float horizontalDistance = Vector2.Distance(new Vector2(com.x, com.z), new Vector2(supportPoint.x, supportPoint.z));
            float verticalDistance = Mathf.Abs(com.y - supportPoint.y);
            float stability = Mathf.Clamp01(1f - (horizontalDistance / (verticalDistance * 0.5f + 0.1f)));
            return stability;
        }

        /// <summary>
        /// Calculates balance recovery state for a falling character.
        /// </summary>
        public static BalanceRecoveryState CalculateBalanceRecovery(
            Vector3 centerOfMass,
            Vector3 supportPolygonCenter,
            float supportPolygonArea,
            Vector3 velocity,
            float stability)
        {
            BalanceRecoveryState recovery = new BalanceRecoveryState
            {
                centerOfMassOffset = Vector3.Distance(centerOfMass, supportPolygonCenter),
                recoveryTorque = 0f,
                stepPhase = 0f,
                stepDirection = 0f,
                isRecovering = false,
                recoveryProgress = 0f,
                stabilityMargin = stability,
                supportPolygonCenter = supportPolygonCenter,
                supportPolygonArea = supportPolygonArea
            };
            if (stability < 0.3f)
            {
                recovery.isRecovering = true;
                Vector3 comToSupport = (supportPolygonCenter - centerOfMass).normalized;
                recovery.recoveryTorque = Vector3.Cross(comToSupport, Vector3.up).magnitude * 10f;
                recovery.stepDirection = Mathf.Sign(comToSupport.x);
                recovery.recoveryProgress = Mathf.Clamp01(1f - stability);
            }
            return recovery;
        }

        /// <summary>
        /// Calculates required step position for balance recovery.
        /// </summary>
        public static Vector3 CalculateRecoveryStepPosition(
            BalanceRecoveryState recoveryState,
            Vector3 currentFootPosition,
            float stepLength)
        {
            if (!recoveryState.isRecovering) return currentFootPosition;
            Vector3 stepDirection = new Vector3(recoveryState.stepDirection, 0f, 1f).normalized;
            Vector3 targetStep = currentFootPosition + stepDirection * stepLength * recoveryState.recoveryProgress;
            return Vector3.Lerp(currentFootPosition, targetStep, 0.5f);
        }

        /// <summary>
        /// Calculates joint limit compliance for a given joint state.
        /// </summary>
        public static float CalculateJointLimitCompliance(JointState joint, IKJointConfig config)
        {
            if (!config.useAngularLimits) return 1f;
            float angleDeviation = 0f;
            if (joint.angle < config.angularLimitsX.x || joint.angle > config.angularLimitsX.y)
            {
                angleDeviation = Mathf.Max(angleDeviation, Mathf.Abs(joint.angle - Mathf.Clamp(joint.angle, config.angularLimitsX.x, config.angularLimitsX.y)));
            }
            float normalizedDeviation = Mathf.Clamp01(angleDeviation / 90f);
            return Mathf.Lerp(1f, 0.3f, normalizedDeviation);
        }

        /// <summary>
        /// Calculates the support polygon area for balance calculation.
        /// </summary>
        public static float CalculateSupportPolygonArea(Vector3 leftFoot, Vector3 rightFoot, Vector3 leftHand, Vector3 rightHand)
        {
            float footArea = Vector3.Distance(leftFoot, rightFoot) * 0.3f;
            float handArea = Vector3.Distance(leftHand, rightHand) * 0.3f;
            return footArea + handArea;
        }

        /// <summary>
        /// Calculates the maximum safe lean angle before balance is lost.
        /// </summary>
        public static float CalculateMaxLeanAngle(float height, float supportArea)
        {
            if (supportArea <= 0f) return 0f;
            return Mathf.Atan(supportArea / (2f * height)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Calculates the angular momentum required for balance recovery.
        /// </summary>
        public static float CalculateRecoveryAngularMomentum(float mass, float height, float leanAngle, float angularVelocity)
        {
            float momentOfInertia = mass * height * height;
            float requiredAngularAcceleration = leanAngle / (height / 9.81f);
            return momentOfInertia * (requiredAngularAcceleration - angularVelocity * 0.1f);
        }
    }
}
