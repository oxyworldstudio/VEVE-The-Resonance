using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
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

    public static class CharacterPhysics
    {
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

        public static float CalculateStability(Vector3 centerOfMass, Vector3 supportBase)
        {
            float horizontalDistance = Vector2.Distance(new Vector2(centerOfMass.x, centerOfMass.z), new Vector2(supportBase.x, supportBase.z));
            float height = centerOfMass.y - supportBase.y;
            float stability = Mathf.Clamp01(1f - (horizontalDistance / (height * 0.5f)));
            return stability;
        }

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

        public static float CalculateInjuryDegradation(float injurySeverity, float bodyPartMass, float painLevel)
        {
            float baseDegradation = injurySeverity * 0.5f;
            float painMultiplier = 1f + painLevel * 0.01f;
            float massFactor = bodyPartMass / 70f;
            return Mathf.Clamp01(baseDegradation * painMultiplier * massFactor);
        }

        public static float CalculateBloodSpatterForce(float bloodPressure, float woundSize, float heartRate)
        {
            float pressureFactor = bloodPressure / 120f;
            float sizeFactor = woundSize * 10f;
            float heartRateFactor = heartRate / 65f;
            return pressureFactor * sizeFactor * heartRateFactor * 0.1f;
        }

        public static Vector3 CalculateBloodSprayDirection(Vector3 woundNormal, Vector3 bodyOrientation, float pressure)
        {
            float randomAngle = UnityEngine.Random.Range(-30f, 30f);
            Vector3 baseDirection = -woundNormal;
            Quaternion randomRotation = Quaternion.Euler(randomAngle, randomAngle, 0f);
            Vector3 sprayDirection = randomRotation * baseDirection;
            return sprayDirection.normalized * pressure;
        }

        public static float CalculateMovementPenalty(float leftLegInjury, float rightLegInjury, float torsoInjury, float armInjury)
        {
            float legPenalty = Mathf.Max(leftLegInjury, rightLegInjury) * 0.8f;
            float torsoPenalty = torsoInjury * 0.3f;
            float armPenalty = armInjury * 0.2f;
            return Mathf.Clamp01(legPenalty + torsoPenalty + armPenalty);
        }

        public static float CalculateAimPenalty(float leftArmInjury, float rightArmInjury, float eyeInjury, float headInjury)
        {
            float armPenalty = Mathf.Max(leftArmInjury, rightArmInjury) * 0.7f;
            float visionPenalty = eyeInjury * 0.9f;
            float headPenalty = headInjury * 0.5f;
            return Mathf.Clamp01(armPenalty + visionPenalty + headPenalty);
        }

        public static float CalculateConsciousnessProbability(float bloodLoss, float headInjury, float heartRate, float respirationRate)
        {
            float bloodLossFactor = Mathf.Clamp01(bloodLoss / 2f);
            float headFactor = headInjury * 0.8f;
            float heartRateFactor = Mathf.Clamp01((220f - heartRate) / 100f);
            float respirationFactor = Mathf.Clamp01(respirationRate / 30f);
            return Mathf.Clamp01(bloodLossFactor + headFactor - heartRateFactor * 0.3f - respirationFactor * 0.2f);
        }
    }
}
