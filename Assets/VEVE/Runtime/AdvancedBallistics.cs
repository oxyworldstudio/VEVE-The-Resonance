using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public enum ProjectileType { FullMetalJacket, HollowPoint, ArmorPiercing, Tracer, Subsonic }

    public readonly struct BallisticSolution
    {
        public readonly float timeOfFlight;
        public readonly float drop;
        public readonly float windDrift;
        public readonly float coriolisDrift;
        public readonly float spinDrift;
        public readonly float energyRemaining;
        public readonly float penetrationDepth;
        public readonly float retainedVelocity;

        public BallisticSolution(float timeOfFlight, float drop, float windDrift, float coriolisDrift, float spinDrift, float energyRemaining, float penetrationDepth, float retainedVelocity)
        {
            this.timeOfFlight = timeOfFlight;
            this.drop = drop;
            this.windDrift = windDrift;
            this.coriolisDrift = coriolisDrift;
            this.spinDrift = spinDrift;
            this.energyRemaining = energyRemaining;
            this.penetrationDepth = penetrationDepth;
            this.retainedVelocity = retainedVelocity;
        }
    }

    public static class AdvancedBallistics
    {
        public static BallisticSolution Solve(
            float muzzleVelocity,
            float distance,
            float bulletMass,
            float ballisticCoefficient,
            float windSpeed,
            float windAngle,
            float latitude,
            float twistRate,
            float temperature = 15f,
            float humidity = 0.5f,
            float altitude = 0f,
            ProjectileType type = ProjectileType.FullMetalJacket)
        {
            float airDensity = RealismConfig.CalculateAirDensity(altitude, temperature);
            float dragDeceleration = 0.5f * airDensity * muzzleVelocity * muzzleVelocity * ballisticCoefficient * 0.01f;
            float flightTime = 2f * distance / (muzzleVelocity + Mathf.Max(0f, muzzleVelocity - dragDeceleration * distance));
            float velocity = muzzleVelocity - dragDeceleration * flightTime;
            float drop = 0.5f * 9.80665f * flightTime * flightTime;
            float windDrift = 0.5f * airDensity * windSpeed * windSpeed * 0.01f * flightTime * flightTime * Mathf.Sin(windAngle * Mathf.Deg2Rad);
            float coriolisDrift = Ballistics.CoriolisDrift(latitude, windAngle, distance, velocity);
            float spinDrift = Ballistics.SpinDrift(twistRate, distance, velocity);
            float energy = 0.5f * bulletMass * velocity * velocity;
            float penetrationDepth = Ballistics.CalculatePenetrationDepth(energy, type switch
            {
                ProjectileType.ArmorPiercing => SurfaceMaterial.Metal,
                ProjectileType.HollowPoint => SurfaceMaterial.Fabric,
                _ => SurfaceMaterial.Concrete
            });
            return new BallisticSolution(flightTime, drop, windDrift, coriolisDrift, spinDrift, energy, penetrationDepth, velocity);
        }

        public static float CalculateStabilityFactor(float twistRate, float bulletLength, float bulletDiameter, float muzzleVelocity)
        {
            float gyration = bulletLength * 0.6f;
            float stability = (twistRate * gyration * gyration) / (0.4f * muzzleVelocity * muzzleVelocity * bulletDiameter * bulletDiameter * bulletDiameter);
            return stability;
        }
    }
}
