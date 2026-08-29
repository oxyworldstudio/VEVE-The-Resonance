using UnityEngine;

namespace VEVE
{
    public enum ProjectileType { FullMetalJacket, HollowPoint, ArmorPiercing, Tracer }

    public readonly struct BallisticSolution
    {
        public readonly float timeOfFlight;
        public readonly float drop;
        public readonly float windDrift;
        public readonly float energyRemaining;
        public readonly float penetrationDepth;

        public BallisticSolution(float timeOfFlight, float drop, float windDrift, float energyRemaining, float penetrationDepth)
        {
            this.timeOfFlight = timeOfFlight;
            this.drop = drop;
            this.windDrift = windDrift;
            this.energyRemaining = energyRemaining;
            this.penetrationDepth = penetrationDepth;
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
            float gravity = 9.81f,
            float temperature = 15f,
            float humidity = 0.5f,
            ProjectileType type = ProjectileType.FullMetalJacket)
        {
            float flightTime = distance / muzzleVelocity;
            float drop = 0.5f * gravity * flightTime * flightTime;
            float windDrift = windSpeed * Mathf.Sin(windAngle * Mathf.Deg2Rad) * flightTime * flightTime * 0.5f;
            float airDensity = CalculateAirDensity(temperature, humidity);
            float velocity = muzzleVelocity * Mathf.Exp(-ballisticCoefficient * airDensity * distance);
            float energy = 0.5f * bulletMass * velocity * velocity;
            float penetrationDepth = CalculatePenetration(energy, type);
            return new BallisticSolution(flightTime, drop, windDrift, energy, penetrationDepth);
        }

        private static float CalculateAirDensity(float temperatureCelsius, float humidity)
        {
            float temperatureKelvin = temperatureCelsius + 273.15f;
            float pressure = 101325f;
            float gasConstant = 287.05f;
            float saturationVaporPressure = 610.94f * Mathf.Exp((17.625f * temperatureCelsius) / (temperatureCelsius + 243.04f));
            float vaporPressure = humidity * saturationVaporPressure;
            float dryAirPressure = pressure - vaporPressure;
            return dryAirPressure / (gasConstant * temperatureKelvin);
        }

        private static float CalculatePenetration(float energy, ProjectileType type)
        {
            float coefficient = type switch
            {
                ProjectileType.FullMetalJacket => 1.0f,
                ProjectileType.HollowPoint => 0.6f,
                ProjectileType.ArmorPiercing => 1.8f,
                ProjectileType.Tracer => 0.9f,
                _ => 1.0f,
            };
            return energy * coefficient / 80f;
        }
    }
}
