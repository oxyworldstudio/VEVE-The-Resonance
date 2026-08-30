using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    [Serializable]
    public struct WindVector
    {
        public Vector3 direction;
        public float speed;
        public float turbulence;
        public float gustFrequency;
        public float gustAmplitude;
    }

    [Serializable]
    public struct AtmosphericState
    {
        public float temperature;
        public float humidity;
        public float pressure;
        public float airDensity;
        public float visibility;
        public float precipitationIntensity;
        public float fogDensity;
    }

    public static class EnvironmentalPhysics
    {
        public static WindVector CalculateWindVector(float altitude, float surfaceWindSpeed, float surfaceWindDirection, float turbulence, float time)
        {
            float windSpeed = RealismConfig.CalculateWindSpeed(altitude, surfaceWindSpeed);
            float gust = Mathf.Sin(time * turbulence * 0.1f) * turbulence * 0.3f;
            windSpeed += gust;
            Vector3 baseDirection = new Vector3(Mathf.Sin(surfaceWindDirection * Mathf.Deg2Rad), 0f, Mathf.Cos(surfaceWindDirection * Mathf.Deg2Rad));
            float shearFactor = Mathf.Pow(altitude / 1000f, 0.143f);
            return new WindVector
            {
                direction = baseDirection,
                speed = windSpeed * shearFactor,
                turbulence = turbulence,
                gustFrequency = turbulence * 0.5f,
                gustAmplitude = turbulence * 0.3f
            };
        }

        public static AtmosphericState CalculateAtmosphericState(float altitude, float temperature, float humidity, float precipitationIntensity, float fogDensity)
        {
            float adjustedTemp = temperature - 6.5f * altitude / 1000f;
            float airDensity = RealismConfig.CalculateAirDensity(altitude, adjustedTemp);
            float visibility = CalculateVisibility(airDensity, precipitationIntensity, fogDensity);
            return new AtmosphericState
            {
                temperature = adjustedTemp,
                humidity = humidity,
                pressure = 101325f * Mathf.Pow(1f - 0.0065f * altitude / (adjustedTemp + 273.15f), 5.256f),
                airDensity = airDensity,
                visibility = visibility,
                precipitationIntensity = precipitationIntensity,
                fogDensity = fogDensity
            };
        }

        public static float CalculateVisibility(float airDensity, float precipitationIntensity, float fogDensity)
        {
            float baseVisibility = 15000f * Mathf.Clamp01(airDensity / 1.225f);
            float precipitationLoss = precipitationIntensity * 100f;
            float fogLoss = fogDensity * 5000f;
            return Mathf.Max(10f, baseVisibility - precipitationLoss - fogLoss);
        }

        public static float CalculateTerminalVelocity(float particleMass, float particleRadius, float airDensity, float dragCoefficient)
        {
            float crossSection = Mathf.PI * particleRadius * particleRadius;
            float gravity = 9.80665f;
            float weight = particleMass * gravity;
            float dragAtTerminal = 0.5f * airDensity * dragCoefficient * crossSection;
            return Mathf.Sqrt(weight / dragAtTerminal);
        }

        public static Vector3 CalculatePrecipitationForce(float particleMass, float terminalVelocity, float precipitationIntensity, Vector3 windDirection)
        {
            float forceMagnitude = particleMass * terminalVelocity * precipitationIntensity * 0.1f;
            return windDirection.normalized * forceMagnitude;
        }

        public static float CalculateAtmosphericScattering(float distance, float airDensity, float sunElevation)
        {
            float scatteringCoefficient = 0.0001f * airDensity;
            float sunFactor = Mathf.Clamp01(sunElevation / 90f);
            return Mathf.Exp(-scatteringCoefficient * distance) * sunFactor;
        }

        public static float CalculateHumidityEffectOnDrag(float humidity, float temperature)
        {
            float saturationVaporPressure = 610.94f * Mathf.Exp((17.625f * temperature) / (temperature + 243.04f));
            float vaporPressure = humidity * saturationVaporPressure;
            float dryAirPressure = 101325f - vaporPressure;
            return dryAirPressure / 101325f;
        }

        public static float CalculateTemperatureGradientEffect(float bulletTemperature, float airTemperature, float velocity)
        {
            float tempDifference = bulletTemperature - airTemperature;
            float coolingRate = 0.01f * Mathf.Abs(tempDifference) * velocity * 0.001f;
            return coolingRate;
        }

        public static float CalculateWindAngleOfAttack(Vector3 bulletVelocity, Vector3 windDirection)
        {
            return Vector3.Angle(bulletVelocity, windDirection);
        }

        public static float CalculateDynamicPressure(float airDensity, float velocity)
        {
            return 0.5f * airDensity * velocity * velocity;
        }
    }
}
