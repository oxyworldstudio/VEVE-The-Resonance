using UnityEngine;
using System;

namespace VEVE
{
    /// <summary>
    /// Defines the four seasons for climate simulation.
    /// </summary>
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>
    /// Defines biome types for region-specific weather patterns.
    /// </summary>
    public enum BiomeType { Temperate, Tropical, Arid, Continental, Polar, Coastal, Mountain }

    /// <summary>
    /// Manages seasonal variations, biome-specific weather, and temperature/humidity/pressure cycles.
    /// </summary>
    public sealed class ClimateSystem : MonoBehaviour
    {
        [Header("Seasonal Configuration")]
        [SerializeField] private Season currentSeason = Season.Summer;
        [SerializeField] private float daysPerSeason = 90f;
        [SerializeField] private float currentDay = 1f;
        [SerializeField] private float yearLength = 365f;

        [Header("Biome Configuration")]
        [SerializeField] private BiomeType biome = BiomeType.Temperate;
        [SerializeField] private float latitude = 45f;
        [SerializeField] private float altitude = 0f;

        [Header("Temperature")]
        [SerializeField] private float baseTemperature = 15f;
        [SerializeField] private float seasonalTempVariation = 15f;
        [SerializeField] private float dailyTempVariation = 8f;
        [SerializeField] private float currentTemperature = 15f;
        [SerializeField] private float minTemperature = -10f;
        [SerializeField] private float maxTemperature = 35f;

        [Header("Humidity")]
        [SerializeField] private float baseHumidity = 0.5f;
        [SerializeField] private float seasonalHumidityVariation = 0.2f;
        [SerializeField] private float currentHumidity = 0.5f;

        [Header("Pressure")]
        [SerializeField] private float basePressure = 101325f;
        [SerializeField] private float seasonalPressureVariation = 2000f;
        [SerializeField] private float currentPressure = 101325f;

        [Header("Wind")]
        [SerializeField] private float baseWindSpeed = 5f;
        [SerializeField] private float seasonalWindVariation = 3f;
        [SerializeField] private float currentWindSpeed = 5f;

        [Header("Precipitation")]
        [SerializeField] private float basePrecipitationChance = 0.3f;
        [SerializeField] private float seasonalPrecipitationVariation = 0.2f;
        [SerializeField] private float currentPrecipitationChance = 0.3f;

        [Header("Day/Night Cycle")]
        [SerializeField] private float dayLengthMinutes = 24f;
        [SerializeField] private float currentTimeOfDay = 12f;

        public event Action<Season> OnSeasonChanged;
        public event Action<float> OnDayChanged;

        /// <summary>
        /// Gets the current season.
        /// </summary>
        public Season CurrentSeason => currentSeason;

        /// <summary>
        /// Gets the current day of the year.
        /// </summary>
        public float CurrentDay => currentDay;

        /// <summary>
        /// Gets the current temperature in Celsius.
        /// </summary>
        public float CurrentTemperature => currentTemperature;

        /// <summary>
        /// Gets the current relative humidity (0-1).
        /// </summary>
        public float CurrentHumidity => currentHumidity;

        /// <summary>
        /// Gets the current atmospheric pressure in Pascals.
        /// </summary>
        public float CurrentPressure => currentPressure;

        /// <summary>
        /// Gets the current wind speed in m/s.
        /// </summary>
        public float CurrentWindSpeed => currentWindSpeed;

        /// <summary>
        /// Gets the current precipitation chance (0-1).
        /// </summary>
        public float CurrentPrecipitationChance => currentPrecipitationChance;

        /// <summary>
        /// Gets the current time of day in hours (0-24).
        /// </summary>
        public float CurrentTimeOfDay => currentTimeOfDay;

        /// <summary>
        /// Gets the current biome type.
        /// </summary>
        public BiomeType CurrentBiome => biome;

        private void Start()
        {
            UpdateClimateValues();
        }

        private void Update()
        {
            UpdateTime();
            UpdateClimateValues();
        }

        /// <summary>
        /// Sets the current season.
        /// </summary>
        /// <param name="newSeason">The new season.</param>
        public void SetSeason(Season newSeason)
        {
            if (currentSeason != newSeason)
            {
                currentSeason = newSeason;
                OnSeasonChanged?.Invoke(currentSeason);
            }
        }

        /// <summary>
        /// Sets the biome type.
        /// </summary>
        /// <param name="newBiome">The new biome type.</param>
        public void SetBiome(BiomeType newBiome)
        {
            biome = newBiome;
            UpdateClimateValues();
        }

        /// <summary>
        /// Sets the geographic location.
        /// </summary>
        /// <param name="lat">Latitude in degrees.</param>
        /// <param name="alt">Altitude in meters.</param>
        public void SetLocation(float lat, float alt)
        {
            latitude = lat;
            altitude = alt;
            UpdateClimateValues();
        }

        /// <summary>
        /// Advances the simulation by a specified number of days.
        /// </summary>
        /// <param name="days">Number of days to advance.</param>
        public void AdvanceDays(float days)
        {
            float previousDay = currentDay;
            currentDay += days;
            if (currentDay > yearLength) currentDay -= yearLength;

            UpdateSeasonFromDay();
            OnDayChanged?.Invoke(currentDay);
        }

        /// <summary>
        /// Updates the time of day and day progression.
        /// </summary>
        private void UpdateTime()
        {
            float hoursPerSecond = 24f / (dayLengthMinutes * 60f);
            currentTimeOfDay += Time.deltaTime * hoursPerSecond;

            if (currentTimeOfDay >= 24f)
            {
                currentTimeOfDay -= 24f;
                AdvanceDays(1f);
            }
        }

        /// <summary>
        /// Updates the season based on the current day.
        /// </summary>
        private void UpdateSeasonFromDay()
        {
            Season newSeason;
            if (currentDay <= daysPerSeason) newSeason = Season.Winter;
            else if (currentDay <= daysPerSeason * 2) newSeason = Season.Spring;
            else if (currentDay <= daysPerSeason * 3) newSeason = Season.Summer;
            else newSeason = Season.Autumn;

            if (newSeason != currentSeason)
            {
                currentSeason = newSeason;
                OnSeasonChanged?.Invoke(currentSeason);
            }
        }

        /// <summary>
        /// Updates all climate values based on current conditions.
        /// </summary>
        private void UpdateClimateValues()
        {
            float seasonFactor = CalculateSeasonFactor();
            float dailyFactor = CalculateDailyFactor();
            float biomeFactor = CalculateBiomeFactor();
            float altitudeFactor = CalculateAltitudeFactor();

            currentTemperature = CalculateTemperature(seasonFactor, dailyFactor, biomeFactor, altitudeFactor);
            currentHumidity = CalculateHumidity(seasonFactor);
            currentPressure = CalculatePressure(seasonFactor, altitudeFactor);
            currentWindSpeed = CalculateWindSpeed(seasonFactor);
            currentPrecipitationChance = CalculatePrecipitationChance(seasonFactor);
        }

        /// <summary>
        /// Calculates the seasonal factor (-1 to 1) based on day of year.
        /// </summary>
        private float CalculateSeasonFactor()
        {
            float dayAngle = (currentDay / yearLength) * Mathf.PI * 2f;
            return -Mathf.Cos(dayAngle);
        }

        /// <summary>
        /// Calculates the daily temperature factor (-1 to 1) based on time of day.
        /// </summary>
        private float CalculateDailyFactor()
        {
            float hourAngle = ((currentTimeOfDay - 14f) / 24f) * Mathf.PI * 2f;
            return Mathf.Sin(hourAngle);
        }

        /// <summary>
        /// Calculates the biome-specific factor for climate modification.
        /// </summary>
        private float CalculateBiomeFactor()
        {
            return biome switch
            {
                BiomeType.Temperate => 0f,
                BiomeType.Tropical => 10f,
                BiomeType.Arid => 5f,
                BiomeType.Continental => -5f,
                BiomeType.Polar => -20f,
                BiomeType.Coastal => 2f,
                BiomeType.Mountain => -10f,
                _ => 0f
            };
        }

        /// <summary>
        /// Calculates the altitude temperature reduction factor.
        /// </summary>
        private float CalculateAltitudeFactor()
        {
            return -6.5f * altitude / 1000f;
        }

        /// <summary>
        /// Calculates the current temperature.
        /// </summary>
        private float CalculateTemperature(float seasonFactor, float dailyFactor, float biomeFactor, float altitudeFactor)
        {
            float temp = baseTemperature;
            temp += seasonFactor * seasonalTempVariation;
            temp += dailyFactor * dailyTempVariation;
            temp += biomeFactor;
            temp += altitudeFactor;
            return Mathf.Clamp(temp, minTemperature, maxTemperature);
        }

        /// <summary>
        /// Calculates the current humidity.
        /// </summary>
        private float CalculateHumidity(float seasonFactor)
        {
            float humidity = baseHumidity;
            humidity -= seasonFactor * seasonalHumidityVariation;
            humidity += biome switch
            {
                BiomeType.Tropical => 0.3f,
                BiomeType.Coastal => 0.2f,
                BiomeType.Arid => -0.3f,
                BiomeType.Polar => -0.1f,
                _ => 0f
            };
            return Mathf.Clamp01(humidity);
        }

        /// <summary>
        /// Calculates the current atmospheric pressure.
        /// </summary>
        private float CalculatePressure(float seasonFactor, float altitudeFactor)
        {
            float pressure = basePressure;
            pressure += seasonFactor * seasonalPressureVariation;
            pressure += altitudeFactor * 100f;
            return pressure;
        }

        /// <summary>
        /// Calculates the current wind speed.
        /// </summary>
        private float CalculateWindSpeed(float seasonFactor)
        {
            float wind = baseWindSpeed;
            wind += Mathf.Abs(seasonFactor) * seasonalWindVariation;
            wind += biome switch
            {
                BiomeType.Coastal => 3f,
                BiomeType.Mountain => 5f,
                BiomeType.Polar => 4f,
                _ => 0f
            };
            return Mathf.Max(0f, wind);
        }

        /// <summary>
        /// Calculates the current precipitation chance.
        /// </summary>
        private float CalculatePrecipitationChance(float seasonFactor)
        {
            float chance = basePrecipitationChance;
            chance += seasonFactor * seasonalPrecipitationVariation;
            chance += biome switch
            {
                BiomeType.Tropical => 0.3f,
                BiomeType.Coastal => 0.2f,
                BiomeType.Arid => -0.2f,
                BiomeType.Polar => -0.1f,
                _ => 0f
            };
            return Mathf.Clamp01(chance);
        }

        /// <summary>
        /// Gets the expected weather state based on current climate conditions.
        /// </summary>
        /// <returns>Most likely weather state.</returns>
        public WeatherState GetExpectedWeather()
        {
            if (currentPrecipitationChance > 0.7f)
            {
                if (currentTemperature <= 0f) return WeatherState.Snow;
                if (currentWindSpeed > 20f) return WeatherState.Thunderstorm;
                return WeatherState.Rain;
            }
            if (currentHumidity > 0.8f && currentWindSpeed < 3f) return WeatherState.Fog;
            if (currentHumidity > 0.6f) return WeatherState.Overcast;
            return WeatherState.Clear;
        }

        /// <summary>
        /// Calculates the diurnal temperature range for the current conditions.
        /// </summary>
        /// <returns>Temperature range in Celsius.</returns>
        public float GetDiurnalTemperatureRange()
        {
            float baseRange = dailyTempVariation * 2f;
            float cloudReduction = currentHumidity * 0.5f;
            return baseRange * (1f - cloudReduction);
        }

        /// <summary>
        /// Gets the frost risk based on current conditions.
        /// </summary>
        /// <returns>Frost risk (0-1).</returns>
        public float GetFrostRisk()
        {
            if (currentTemperature > 5f) return 0f;
            return Mathf.Clamp01((5f - currentTemperature) / 15f);
        }

        /// <summary>
        /// Gets the heat stress index based on temperature and humidity.
        /// </summary>
        /// <returns>Heat stress index (0-1).</returns>
        public float GetHeatStressIndex()
        {
            if (currentTemperature < 27f) return 0f;
            float hi = currentTemperature + 0.5f * (currentHumidity * 100f - 50f);
            return Mathf.Clamp01((hi - 27f) / 20f);
        }
    }
}
