using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    public enum ProjectileType { FullMetalJacket, HollowPoint, ArmorPiercing, Tracer, Subsonic, MatchGrade }

    [Serializable]
    public struct ProjectileState
    {
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion orientation;
        public Vector3 angularVelocity;
        public float mass;
        public float caliber;
        public float length;
        public float dragCoefficient;
        public float ballisticCoefficient;
        public float twistRate;
        public float spin;
        public float time;
        public float temperature;
    }

    [Serializable]
    public struct TerminalBallisticsResult
    {
        public float permanentCavity;
        public float temporaryCavity;
        public float fragmentationMass;
        public float residualEnergy;
        public Vector3 exitPoint;
        public bool exited;
        public float penetrationDepth;
        public float yaw;
        public float pitch;
    }

    [Serializable]
    public struct RicochetResult
    {
        public bool ricocheted;
        public Vector3 ricochetDirection;
        public float ricochetVelocity;
        public float energyLoss;
    }

    public static class ProjectileBallistics
    {
        public static ProjectileState Initialize(float muzzleVelocity, float bulletMass, float caliber, float twistRate, Vector3 origin, Vector3 direction)
        {
            float spin = (2f * Mathf.PI * muzzleVelocity) / (twistRate * 0.001f);
            return new ProjectileState
            {
                position = origin,
                velocity = direction.normalized * muzzleVelocity,
                orientation = Quaternion.LookRotation(direction),
                angularVelocity = new Vector3(0f, spin, 0f),
                mass = bulletMass,
                caliber = caliber,
                length = caliber * 3.5f,
                dragCoefficient = 0.295f,
                ballisticCoefficient = 0.28f,
                twistRate = twistRate,
                spin = spin,
                time = 0f,
                temperature = 15f
            };
        }

        public static ProjectileState SimulateStep(ProjectileState state, float deltaTime, float airDensity, float windSpeed, Vector3 windDirection, float gravity)
        {
            float speed = state.velocity.magnitude;
            float machNumber = speed / 343f;
            float dragCoefficient = CalculateDragCoefficient(machNumber);
            float dragForce = 0.5f * airDensity * speed * speed * dragCoefficient * (state.caliber * state.caliber * Mathf.PI * 0.25f);
            Vector3 dragAcceleration = -state.velocity.normalized * dragForce / state.mass;
            Vector3 windForce = CalculateWindForce(speed, windSpeed, windDirection, airDensity);
            Vector3 gravityForce = new Vector3(0f, -gravity, 0f);
            Vector3 magnusForce = CalculateMagnusForce(state);
            Vector3 totalAcceleration = dragAcceleration + windForce + gravityForce + magnusForce;
            Vector3 newVelocity = state.velocity + totalAcceleration * deltaTime;
            Vector3 newPosition = state.position + state.velocity * deltaTime + 0.5f * totalAcceleration * deltaTime * deltaTime;
            float newSpin = state.spin * (1f - 0.0001f * deltaTime);
            Quaternion newOrientation = Quaternion.LookRotation(newVelocity.normalized);
            return new ProjectileState
            {
                position = newPosition,
                velocity = newVelocity,
                orientation = newOrientation,
                angularVelocity = state.angularVelocity * (1f - 0.001f * deltaTime),
                mass = state.mass,
                caliber = state.caliber,
                length = state.length,
                dragCoefficient = dragCoefficient,
                ballisticCoefficient = state.ballisticCoefficient,
                twistRate = state.twistRate,
                spin = newSpin,
                time = state.time + deltaTime,
                temperature = state.temperature
            };
        }

        private static float CalculateDragCoefficient(float machNumber)
        {
            if (machNumber < 0.8f) return 0.295f;
            if (machNumber < 1.0f) return 0.4f + 0.3f * (machNumber - 0.8f) / 0.2f;
            if (machNumber < 1.2f) return 0.7f - 0.2f * (machNumber - 1.0f) / 0.2f;
            if (machNumber < 2.5f) return 0.5f - 0.15f * (machNumber - 1.2f) / 1.3f;
            return 0.35f;
        }

        private static Vector3 CalculateWindForce(float bulletSpeed, float windSpeed, Vector3 windDirection, float airDensity)
        {
            if (windSpeed <= 0f) return Vector3.zero;
            float relativeSpeed = Mathf.Abs(bulletSpeed - windSpeed);
            float dragCoefficient = 1.0f;
            float crossSection = 0.01f;
            float windForceMagnitude = 0.5f * airDensity * relativeSpeed * relativeSpeed * dragCoefficient * crossSection;
            return windDirection.normalized * windForceMagnitude * 0.1f;
        }

        private static Vector3 CalculateMagnusForce(ProjectileState state)
        {
            if (state.spin <= 0f) return Vector3.zero;
            float magnusCoefficient = 0.0001f * state.spin;
            Vector3 spinAxis = state.angularVelocity.normalized;
            Vector3 velocityPerp = Vector3.Cross(state.velocity, spinAxis).normalized;
            return magnusCoefficient * velocityPerp * state.velocity.magnitude;
        }

        public static TerminalBallisticsResult ResolveTerminalBallistics(float impactEnergy, float impactVelocity, SurfaceMaterial material, float thickness, float bulletMass, float caliber, ProjectileType type)
        {
            float resistance = GetMaterialResistance(material);
            float stress = impactEnergy / (caliber * caliber * Mathf.PI * 0.25f);
            bool penetrated = stress > resistance * thickness;
            float penetrationDepth = penetrated ? impactEnergy / (resistance * 1.5f) : 0f;
            float permanentCavity = CalculatePermanentCavity(impactEnergy, material, type);
            float temporaryCavity = CalculateTemporaryCavity(impactEnergy, type);
            float fragmentationMass = CalculateFragmentationMass(impactEnergy, material, type);
            float yaw = CalculateYaw(impactVelocity, material, type);
            float pitch = CalculatePitch(impactVelocity, material);
            return new TerminalBallisticsResult
            {
                permanentCavity = permanentCavity,
                temporaryCavity = temporaryCavity,
                fragmentationMass = fragmentationMass,
                residualEnergy = penetrated ? impactEnergy * 0.4f : 0f,
                exitPoint = Vector3.zero,
                exited = penetrated,
                penetrationDepth = penetrationDepth,
                yaw = yaw,
                pitch = pitch
            };
        }

        public static RicochetResult CalculateRicochet(float impactVelocity, float impactAngle, SurfaceMaterial material, float bulletMass)
        {
            float ricochetThreshold = GetRicochetThreshold(material, impactVelocity);
            bool ricocheted = impactAngle > ricochetThreshold && impactVelocity < 800f;
            float energyLoss = ricocheted ? 0.3f + 0.4f * (1f - impactAngle / 90f) : 0f;
            Vector3 ricochetDirection = ricocheted ? new Vector3(impactAngle, 90f - impactAngle, 0f) : Vector3.zero;
            float ricochetVelocity = ricocheted ? impactVelocity * (1f - energyLoss) : 0f;
            return new RicochetResult
            {
                ricocheted = ricocheted,
                ricochetDirection = ricochetDirection,
                ricochetVelocity = ricochetVelocity,
                energyLoss = energyLoss
            };
        }

        private static float GetMaterialResistance(SurfaceMaterial material)
        {
            return material switch
            {
                SurfaceMaterial.Wood => 35f,
                SurfaceMaterial.Concrete => 80f,
                SurfaceMaterial.Metal => 120f,
                SurfaceMaterial.Glass => 15f,
                SurfaceMaterial.Fabric => 8f,
                SurfaceMaterial.Dirt => 20f,
                SurfaceMaterial.Ice => 10f,
                _ => 35f
            };
        }

        private static float CalculatePermanentCavity(float energy, SurfaceMaterial material, ProjectileType type)
        {
            float baseCavity = energy * 0.001f;
            float materialFactor = material == SurfaceMaterial.Fabric ? 1.5f : material == SurfaceMaterial.Wood ? 1.2f : 1.0f;
            float typeFactor = type == ProjectileType.HollowPoint ? 2.0f : type == ProjectileType.ArmorPiercing ? 0.6f : 1.0f;
            return baseCavity * materialFactor * typeFactor;
        }

        private static float CalculateTemporaryCavity(float energy, ProjectileType type)
        {
            float baseTemporary = energy * 0.003f;
            float typeFactor = type == ProjectileType.HollowPoint ? 3.0f : type == ProjectileType.FullMetalJacket ? 1.0f : 0.5f;
            return baseTemporary * typeFactor;
        }

        private static float CalculateFragmentationMass(float energy, SurfaceMaterial material, ProjectileType type)
        {
            if (type != ProjectileType.FullMetalJacket && type != ProjectileType.Tracer) return 0f;
            float fragmentationProbability = Mathf.Clamp01(energy / 1000f);
            float materialFactor = material == SurfaceMaterial.Metal ? 0.8f : material == SurfaceMaterial.Concrete ? 0.5f : 0.2f;
            return 0.01f * fragmentationProbability * materialFactor;
        }

        private static float CalculateYaw(float velocity, SurfaceMaterial material, ProjectileType type)
        {
            float baseYaw = 0.1f;
            float velocityFactor = Mathf.Clamp01(velocity / 800f);
            float materialFactor = material == SurfaceMaterial.Metal ? 2.0f : material == SurfaceMaterial.Concrete ? 1.5f : 1.0f;
            return baseYaw * velocityFactor * materialFactor * (type == ProjectileType.HollowPoint ? 1.5f : 1.0f);
        }

        private static float CalculatePitch(float velocity, SurfaceMaterial material)
        {
            return Mathf.Clamp01(velocity / 1000f) * (material == SurfaceMaterial.Metal ? 1.5f : 1.0f);
        }

        private static float GetRicochetThreshold(SurfaceMaterial material, float velocity)
        {
            return material switch
            {
                SurfaceMaterial.Metal => 15f,
                SurfaceMaterial.Concrete => 20f,
                SurfaceMaterial.Glass => 5f,
                SurfaceMaterial.Ice => 10f,
                _ => 30f
            };
        }
    }
}
