using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    /// <summary>
    /// Represents a 3D wind vector with turbulence and gust characteristics.
    /// </summary>
    [Serializable]
    public struct WindVector
    {
        public Vector3 direction;
        public float speed;
        public float turbulence;
        public float gustFrequency;
        public float gustAmplitude;
    }

    /// <summary>
    /// Represents the complete atmospheric state at a point in space and time.
    /// </summary>
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

    /// <summary>
    /// Represents a precipitation particle with physical properties.
    /// </summary>
    [Serializable]
    public struct PrecipitationParticle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float mass;
        public float radius;
        public float temperature;
        public float dragCoefficient;
    }

    /// <summary>
    /// Represents a fog volume with density and height parameters.
    /// </summary>
    [Serializable]
    public struct FogVolumeData
    {
        public Vector3 center;
        public Vector3 size;
        public float density;
        public float heightFalloff;
        public float noiseScale;
        public float noiseSpeed;
        public Color fogColor;
    }

    /// <summary>
    /// Provides environmental physics calculations for precipitation, fog rendering,
    /// atmospheric scattering, and related phenomena.
    /// </summary>
    public static class EnvironmentalPhysics
    {
        private const float STEFAN_BOLTZMANN = 5.670374419e-8f;
        private const float VON_KARMAN = 0.41f;

        /// <summary>
        /// Calculates the wind vector at a given altitude and time.
        /// </summary>
        /// <param name="altitude">Altitude in meters.</param>
        /// <param name="surfaceWindSpeed">Surface wind speed in m/s.</param>
        /// <param name="surfaceWindDirection">Surface wind direction in degrees.</param>
        /// <param name="turbulence">Turbulence factor (0-1).</param>
        /// <param name="time">Current time in seconds.</param>
        /// <returns>Calculated wind vector.</returns>
        public static WindVector CalculateWindVector(float altitude, float surfaceWindSpeed, float surfaceWindDirection, float turbulence, float time)
        {
            float windSpeed = RealismConfig.CalculateWindSpeed(altitude, surfaceWindSpeed);
            float gust = Mathf.Sin(time * turbulence * 0.1f) * turbulence * 0.3f;
            float secondaryGust = Mathf.Sin(time * turbulence * 0.37f + 1.3f) * turbulence * 0.15f;
            windSpeed += gust + secondaryGust;
            Vector3 baseDirection = new Vector3(Mathf.Sin(surfaceWindDirection * Mathf.Deg2Rad), 0f, Mathf.Cos(surfaceWindDirection * Mathf.Deg2Rad));
            float shearFactor = Mathf.Pow(Mathf.Max(0.1f, altitude) / 1000f, 0.143f);
            float directionVariation = Mathf.Sin(time * 0.05f) * turbulence * 15f;
            Quaternion rotation = Quaternion.Euler(0f, directionVariation, 0f);
            Vector3 finalDirection = rotation * baseDirection;
            return new WindVector
            {
                direction = finalDirection.normalized,
                speed = Mathf.Max(0f, windSpeed * shearFactor),
                turbulence = turbulence,
                gustFrequency = turbulence * 0.5f,
                gustAmplitude = turbulence * 0.3f
            };
        }

        /// <summary>
        /// Calculates the complete atmospheric state at a given position.
        /// </summary>
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

        /// <summary>
        /// Calculates visibility range based on air density, precipitation, and fog.
        /// </summary>
        public static float CalculateVisibility(float airDensity, float precipitationIntensity, float fogDensity)
        {
            float baseVisibility = 15000f * Mathf.Clamp01(airDensity / 1.225f);
            float precipitationLoss = precipitationIntensity * 100f;
            float fogLoss = fogDensity * 5000f;
            return Mathf.Max(10f, baseVisibility - precipitationLoss - fogLoss);
        }

        /// <summary>
        /// Calculates terminal velocity for a falling particle.
        /// </summary>
        public static float CalculateTerminalVelocity(float particleMass, float particleRadius, float airDensity, float dragCoefficient)
        {
            float crossSection = Mathf.PI * particleRadius * particleRadius;
            float gravity = 9.80665f;
            float weight = particleMass * gravity;
            float dragAtTerminal = 0.5f * airDensity * dragCoefficient * crossSection;
            if (dragAtTerminal < 0.0001f) return 100f;
            return Mathf.Sqrt(weight / dragAtTerminal);
        }

        /// <summary>
        /// Calculates the force applied to precipitation by wind.
        /// </summary>
        public static Vector3 CalculatePrecipitationForce(float particleMass, float terminalVelocity, float precipitationIntensity, Vector3 windDirection)
        {
            float forceMagnitude = particleMass * terminalVelocity * precipitationIntensity * 0.1f;
            return windDirection.normalized * forceMagnitude;
        }

        /// <summary>
        /// Calculates atmospheric light scattering based on distance and conditions.
        /// </summary>
        public static float CalculateAtmosphericScattering(float distance, float airDensity, float sunElevation)
        {
            float scatteringCoefficient = 0.0001f * airDensity;
            float sunFactor = Mathf.Clamp01(sunElevation / 90f);
            return Mathf.Exp(-scatteringCoefficient * distance) * sunFactor;
        }

        /// <summary>
        /// Calculates the effect of humidity on aerodynamic drag.
        /// </summary>
        public static float CalculateHumidityEffectOnDrag(float humidity, float temperature)
        {
            float saturationVaporPressure = 610.94f * Mathf.Exp((17.625f * temperature) / (temperature + 243.04f));
            float vaporPressure = humidity * saturationVaporPressure;
            float dryAirPressure = 101325f - vaporPressure;
            return dryAirPressure / 101325f;
        }

        /// <summary>
        /// Calculates temperature gradient effect on projectile cooling.
        /// </summary>
        public static float CalculateTemperatureGradientEffect(float bulletTemperature, float airTemperature, float velocity)
        {
            float tempDifference = bulletTemperature - airTemperature;
            float coolingRate = 0.01f * Mathf.Abs(tempDifference) * velocity * 0.001f;
            return coolingRate;
        }

        /// <summary>
        /// Calculates the angle of attack between bullet velocity and wind.
        /// </summary>
        public static float CalculateWindAngleOfAttack(Vector3 bulletVelocity, Vector3 windDirection)
        {
            return Vector3.Angle(bulletVelocity, windDirection);
        }

        /// <summary>
        /// Calculates dynamic pressure for aerodynamic calculations.
        /// </summary>
        public static float CalculateDynamicPressure(float airDensity, float velocity)
        {
            return 0.5f * airDensity * velocity * velocity;
        }

        /// <summary>
        /// Calculates precipitation particle properties based on type and intensity.
        /// </summary>
        /// <param name="type">Type of precipitation (0=rain, 1=snow, 2=hail).</param>
        /// <param name="intensity">Precipitation intensity (0-1).</param>
        /// <param name="altitude">Current altitude in meters.</param>
        /// <returns>Precipitation particle with calculated properties.</returns>
        public static PrecipitationParticle CalculatePrecipitationParticle(int type, float intensity, float altitude)
        {
            PrecipitationParticle particle = new PrecipitationParticle();
            particle.temperature = 15f - 6.5f * altitude / 1000f;
            particle.dragCoefficient = 0.47f;

            switch (type)
            {
                case 0:
                    particle.radius = UnityEngine.Random.Range(0.0005f, 0.005f);
                    particle.mass = (4f / 3f) * Mathf.PI * particle.radius * particle.radius * particle.radius * 1000f;
                    particle.dragCoefficient = 0.47f;
                    break;
                case 1:
                    particle.radius = UnityEngine.Random.Range(0.002f, 0.008f);
                    particle.mass = (4f / 3f) * Mathf.PI * particle.radius * particle.radius * particle.radius * 100f;
                    particle.dragCoefficient = 1.2f;
                    break;
                case 2:
                    particle.radius = UnityEngine.Random.Range(0.002f, 0.025f);
                    particle.mass = (4f / 3f) * Mathf.PI * particle.radius * particle.radius * particle.radius * 917f;
                    particle.dragCoefficient = 0.5f;
                    break;
            }

            float terminalVel = CalculateTerminalVelocity(particle.mass, particle.radius, 1.225f, particle.dragCoefficient);
            particle.velocity = new Vector3(0f, -terminalVel, 0f);
            return particle;
        }

        /// <summary>
        /// Updates a precipitation particle's position based on physics.
        /// </summary>
        /// <param name="particle">Reference to the particle to update.</param>
        /// <param name="wind">Current wind vector.</param>
        /// <param name="deltaTime">Time step in seconds.</param>
        /// <param name="airDensity">Current air density.</param>
        public static void UpdatePrecipitationParticle(ref PrecipitationParticle particle, WindVector wind, float deltaTime, float airDensity)
        {
            Vector3 windForce = wind.direction * wind.speed;
            float crossSection = Mathf.PI * particle.radius * particle.radius;
            float dragMagnitude = 0.5f * airDensity * particle.dragCoefficient * crossSection * particle.velocity.sqrMagnitude;
            Vector3 dragForce = -particle.velocity.normalized * dragMagnitude;
            Vector3 gravity = Vector3.down * particle.mass * 9.80665f;
            Vector3 totalForce = gravity + dragForce + windForce * crossSection * 0.01f;
            particle.velocity += (totalForce / particle.mass) * deltaTime;
            particle.position += particle.velocity * deltaTime;
        }

        /// <summary>
        /// Calculates the density of a fog volume at a given point.
        /// </summary>
        /// <param name="point">World space point to sample.</param>
        /// <param name="volume">Fog volume parameters.</param>
        /// <param name="time">Current time for noise animation.</param>
        /// <returns>Fog density at the point (0-1).</returns>
        public static float SampleFogVolume(Vector3 point, FogVolumeData volume, float time)
        {
            Vector3 localPoint = point - volume.center;
            Vector3 normalizedPoint = new Vector3(
                localPoint.x / (volume.size.x * 0.5f),
                localPoint.y / (volume.size.y * 0.5f),
                localPoint.z / (volume.size.z * 0.5f)
            );

            float normalizedLength = normalizedPoint.magnitude;
            if (normalizedLength > 1f) return 0f;

            float densityFalloff = 1f - normalizedLength;
            densityFalloff = densityFalloff * densityFalloff;

            float heightFalloff = Mathf.Clamp01(1f + localPoint.y * volume.heightFalloff / volume.size.y);

            float noiseX = (point.x + time * volume.noiseSpeed) * volume.noiseScale;
            float noiseY = (point.z + time * volume.noiseSpeed * 0.7f) * volume.noiseScale;
            float noise = Mathf.PerlinNoise(noiseX, noiseY);
            noise = Mathf.Clamp01(noise + 0.3f);

            return volume.density * densityFalloff * heightFalloff * noise;
        }

        /// <summary>
        /// Renders fog volume parameters to the global shader uniforms.
        /// </summary>
        /// <param name="volume">Fog volume to render.</param>
        /// <param name="index">Volume index for multi-volume support.</param>
        public static void RenderFogVolume(FogVolumeData volume, int index)
        {
            if (index > 3) return;
            Shader.SetGlobalVector($"_FogVolumeCenter{index}", volume.center);
            Shader.SetGlobalVector($"_FogVolumeSize{index}", volume.size);
            Shader.SetGlobalFloat($"_FogVolumeDensity{index}", volume.density);
            Shader.SetGlobalColor($"_FogVolumeColor{index}", volume.fogColor);
        }

        /// <summary>
        /// Calculates atmospheric scattering coefficients for realistic sky rendering.
        /// </summary>
        /// <param name="turbidity">Atmospheric turbidity.</param>
        /// <param name="sunElevation">Sun elevation in degrees.</param>
        /// <returns>Rayleigh and Mie scattering coefficients.</returns>
        public static Vector4 CalculateScatteringCoefficients(float turbidity, float sunElevation)
        {
            float rayleigh = 0.0025f / (1f + turbidity * 0.1f);
            float mie = 0.01f * turbidity;
            float sunFactor = Mathf.Clamp01(Mathf.Sin(sunElevation * Mathf.Deg2Rad));
            float mieDirectionalG = 0.8f;
            return new Vector4(rayleigh, mie, sunFactor, mieDirectionalG);
        }

        /// <summary>
        /// Calculates the color of scattered light based on wavelength and conditions.
        /// </summary>
        /// <param name="wavelength">Light wavelength in nanometers.</param>
        /// <param name="airDensity">Current air density.</param>
        /// <param name="sunElevation">Sun elevation in degrees.</param>
        /// <returns>Scattered light color.</returns>
        public static Color CalculateScatteredColor(float wavelength, float airDensity, float sunElevation)
        {
            float wavelengthM = wavelength * 1e-9f;
            float wavelengthFactor = 1f / (wavelengthM * wavelengthM * wavelengthM * wavelengthM);
            float scattering = wavelengthFactor * airDensity * 1e-30f;
            float sunFactor = Mathf.Clamp01(Mathf.Sin(sunElevation * Mathf.Deg2Rad));

            float r = Mathf.Clamp01(scattering * (680f / wavelength));
            float g = Mathf.Clamp01(scattering * (550f / wavelength));
            float b = Mathf.Clamp01(scattering * (440f / wavelength));

            return new Color(r, g, b) * sunFactor;
        }

        /// <summary>
        /// Calculates extinction coefficient for light passing through atmosphere.
        /// </summary>
        /// <param name="distance">Distance traveled through atmosphere.</param>
        /// <param name="airDensity">Current air density.</param>
        /// <param name="fogDensity">Current fog density.</param>
        /// <returns>Extinction factor (0 = full extinction, 1 = no extinction).</returns>
        public static float CalculateExtinction(float distance, float airDensity, float fogDensity)
        {
            float rayleighExtinction = 0.0001f * airDensity * distance;
            float mieExtinction = fogDensity * 0.01f * distance;
            return Mathf.Exp(-rayleighExtinction - mieExtinction);
        }

        /// <summary>
        /// Calculates air refraction index for optical effects.
        /// </summary>
        /// <param name="temperature">Air temperature in Celsius.</param>
        /// <param name="pressure">Air pressure in Pascals.</param>
        /// <param name="humidity">Relative humidity (0-1).</param>
        /// <returns>Refractive index of air.</returns>
        public static float CalculateAirRefractionIndex(float temperature, float pressure, float humidity)
        {
            float dryTerm = 1f + (pressure * 0.0029f / 101325f) * (293.15f / (273.15f + temperature)) * 0.000292f;
            float wetTerm = 1f - humidity * 0.00004f * (temperature / 20f);
            return dryTerm * wetTerm;
        }

        /// <summary>
        /// Calculates the dew point temperature.
        /// </summary>
        /// <param name="temperature">Air temperature in Celsius.</param>
        /// <param name="humidity">Relative humidity (0-1).</param>
        /// <returns>Dew point temperature in Celsius.</returns>
        public static float CalculateDewPoint(float temperature, float humidity)
        {
            if (humidity <= 0f) return -100f;
            float a = 17.27f;
            float b = 237.7f;
            float alpha = (a * temperature) / (b + temperature) + Mathf.Log(humidity);
            return (b * alpha) / (a - alpha);
        }

        /// <summary>
        /// Calculates the wet-bulb temperature.
        /// </summary>
        /// <param name="temperature">Air temperature in Celsius.</param>
        /// <param name="humidity">Relative humidity (0-1).</param>
        /// <param name="pressure">Air pressure in Pascals.</param>
        /// <returns>Wet-bulb temperature in Celsius.</returns>
        public static float CalculateWetBulbTemperature(float temperature, float humidity, float pressure)
        {
            float dewPoint = CalculateDewPoint(temperature, humidity);
            float wetBulb = temperature * Mathf.Atan(0.151977f * Mathf.Sqrt(humidity + 8.313659f))
                + Mathf.Atan(temperature + humidity) - Mathf.Atan(humidity - 1.676331f)
                + 0.00391838f * Mathf.Pow(humidity, 1.5f) * Mathf.Atan(0.023101f * humidity) - 4.686035f;
            return Mathf.Lerp(dewPoint, wetBulb, 0.5f);
        }
    }
}
