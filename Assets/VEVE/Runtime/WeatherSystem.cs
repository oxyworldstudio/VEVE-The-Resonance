using UnityEngine;
using System;
using System.Collections.Generic;

namespace VEVE
{
    /// <summary>
    /// Manages dynamic weather transitions with front systems, pressure changes,
    /// and realistic precipitation patterns.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        [Header("Weather Configuration")]
        [SerializeField] private WeatherState currentWeather = WeatherState.Clear;
        [SerializeField] private WeatherState targetWeather = WeatherState.Clear;
        [SerializeField] private float transitionDuration = 300f;
        [SerializeField] private float transitionProgress = 0f;

        [Header("Atmospheric Conditions")]
        [SerializeField] private float temperature = 15f;
        [SerializeField] private float humidity = 0.5f;
        [SerializeField] private float pressure = 101325f;
        [SerializeField] private float windSpeed = 5f;
        [SerializeField] private float windDirection = 0f;
        [SerializeField] private float turbulence = 0.3f;

        [Header("Precipitation")]
        [SerializeField] private float precipitationIntensity = 0f;
        [SerializeField] private float targetPrecipitationIntensity = 0f;
        [SerializeField] private float precipitationTransitionSpeed = 0.1f;
        [SerializeField] private int precipitationLayerCount = 3;

        [Header("Front System")]
        [SerializeField] private List<WeatherFront> activeFronts = new List<WeatherFront>();
        [SerializeField] private int maxFronts = 4;
        [SerializeField] private float frontSpawnInterval = 600f;
        [SerializeField] private float frontSpawnTimer = 0f;

        [Header("Cloud System")]
        [SerializeField] private float cloudCoverage = 0.2f;
        [SerializeField] private float targetCloudCoverage = 0.2f;
        [SerializeField] private float cloudTransitionSpeed = 0.05f;
        [SerializeField] private float cloudHeight = 3000f;

        [Header("Fog")]
        [SerializeField] private float fogDensity = 0f;
        [SerializeField] private float targetFogDensity = 0f;
        [SerializeField] private float fogTransitionSpeed = 0.08f;

        [Header("Lightning")]
        [SerializeField] private float lightningFrequency = 0f;
        [SerializeField] private float lightningIntensity = 1f;
        [SerializeField] private float lightningTimer = 0f;

        public event Action<WeatherState, WeatherState> OnWeatherChanged;
        public event Action<float, float> OnPrecipitationChanged;
        public event Action<Vector3> OnLightningStrike;

        private float previousPrecipitationIntensity;

        /// <summary>
        /// Gets the current weather state.
        /// </summary>
        public WeatherState CurrentWeather => currentWeather;

        /// <summary>
        /// Gets the target weather state during transitions.
        /// </summary>
        public WeatherState TargetWeather => targetWeather;

        /// <summary>
        /// Gets whether a weather transition is in progress.
        /// </summary>
        public bool IsTransitioning => transitionProgress < 1f;

        /// <summary>
        /// Gets the current temperature in Celsius.
        /// </summary>
        public float Temperature => temperature;

        /// <summary>
        /// Gets the current relative humidity (0-1).
        /// </summary>
        public float Humidity => humidity;

        /// <summary>
        /// Gets the current atmospheric pressure in Pascals.
        /// </summary>
        public float Pressure => pressure;

        /// <summary>
        /// Gets the current wind speed in m/s.
        /// </summary>
        public float WindSpeed => windSpeed;

        /// <summary>
        /// Gets the current wind direction in degrees.
        /// </summary>
        public float WindDirection => windDirection;

        /// <summary>
        /// Gets the current precipitation intensity (0-1).
        /// </summary>
        public float PrecipitationIntensity => precipitationIntensity;

        /// <summary>
        /// Gets the current cloud coverage (0-1).
        /// </summary>
        public float CloudCoverage => cloudCoverage;

        /// <summary>
        /// Gets the current fog density.
        /// </summary>
        public float FogDensity => fogDensity;

        /// <summary>
        /// Gets the list of active weather fronts.
        /// </summary>
        public IReadOnlyList<WeatherFront> ActiveFronts => activeFronts;

        private void Update()
        {
            UpdateWeatherTransition();
            UpdatePrecipitation();
            UpdateClouds();
            UpdateFog();
            UpdateFronts();
            UpdateLightning();
            UpdateAtmosphericConditions();
        }

        /// <summary>
        /// Initiates a weather transition to the specified state.
        /// </summary>
        /// <param name="newWeather">Target weather state.</param>
        /// <param name="duration">Transition duration in seconds.</param>
        public void TransitionTo(WeatherState newWeather, float duration = -1f)
        {
            if (newWeather == currentWeather) return;

            targetWeather = newWeather;
            transitionProgress = 0f;
            if (duration > 0f) transitionDuration = duration;

            OnWeatherChanged?.Invoke(currentWeather, targetWeather);
        }

        /// <summary>
        /// Sets the precipitation intensity directly.
        /// </summary>
        /// <param name="intensity">Target precipitation intensity (0-1).</param>
        public void SetPrecipitationIntensity(float intensity)
        {
            targetPrecipitationIntensity = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Adds a new weather front to the simulation.
        /// </summary>
        /// <param name="front">The weather front to add.</param>
        public void AddFront(WeatherFront front)
        {
            if (activeFronts.Count >= maxFronts) return;
            activeFronts.Add(front);
        }

        /// <summary>
        /// Removes a weather front from the simulation.
        /// </summary>
        /// <param name="front">The weather front to remove.</param>
        public void RemoveFront(WeatherFront front)
        {
            activeFronts.Remove(front);
        }

        /// <summary>
        /// Triggers a lightning strike at the specified position.
        /// </summary>
        /// <param name="position">World position of the strike.</param>
        public void TriggerLightning(Vector3 position)
        {
            OnLightningStrike?.Invoke(position);
        }

        /// <summary>
        /// Updates the weather transition progress.
        /// </summary>
        private void UpdateWeatherTransition()
        {
            if (transitionProgress >= 1f)
            {
                if (currentWeather != targetWeather)
                {
                    currentWeather = targetWeather;
                    ApplyWeatherState(currentWeather);
                }
                return;
            }

            transitionProgress += Time.deltaTime / Mathf.Max(0.1f, transitionDuration);
            transitionProgress = Mathf.Clamp01(transitionProgress);

            if (transitionProgress >= 1f)
            {
                currentWeather = targetWeather;
                ApplyWeatherState(currentWeather);
            }
        }

        /// <summary>
        /// Applies the weather state to rendering and physics.
        /// </summary>
        private void ApplyWeatherState(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Clear:
                    targetCloudCoverage = 0.1f;
                    targetPrecipitationIntensity = 0f;
                    targetFogDensity = 0f;
                    lightningFrequency = 0f;
                    break;
                case WeatherState.Overcast:
                    targetCloudCoverage = 0.8f;
                    targetPrecipitationIntensity = 0f;
                    targetFogDensity = 0f;
                    lightningFrequency = 0f;
                    break;
                case WeatherState.Rain:
                    targetCloudCoverage = 0.9f;
                    targetPrecipitationIntensity = 0.6f;
                    targetFogDensity = 0.005f;
                    lightningFrequency = 0f;
                    break;
                case WeatherState.Fog:
                    targetCloudCoverage = 0.5f;
                    targetPrecipitationIntensity = 0f;
                    targetFogDensity = 0.03f;
                    lightningFrequency = 0f;
                    break;
                case WeatherState.Snow:
                    targetCloudCoverage = 0.95f;
                    targetPrecipitationIntensity = 0.5f;
                    targetFogDensity = 0.01f;
                    lightningFrequency = 0f;
                    break;
                case WeatherState.Thunderstorm:
                    targetCloudCoverage = 1f;
                    targetPrecipitationIntensity = 0.9f;
                    targetFogDensity = 0.015f;
                    lightningFrequency = 0.5f;
                    break;
            }
        }

        /// <summary>
        /// Updates precipitation intensity with smooth transitions.
        /// </summary>
        private void UpdatePrecipitation()
        {
            previousPrecipitationIntensity = precipitationIntensity;
            precipitationIntensity = Mathf.Lerp(precipitationIntensity, targetPrecipitationIntensity, Time.deltaTime * precipitationTransitionSpeed);

            if (Mathf.Abs(precipitationIntensity - previousPrecipitationIntensity) > 0.001f)
            {
                OnPrecipitationChanged?.Invoke(previousPrecipitationIntensity, precipitationIntensity);
            }
        }

        /// <summary>
        /// Updates cloud coverage with smooth transitions.
        /// </summary>
        private void UpdateClouds()
        {
            cloudCoverage = Mathf.Lerp(cloudCoverage, targetCloudCoverage, Time.deltaTime * cloudTransitionSpeed);
            Shader.SetGlobalFloat("_CloudCoverage", cloudCoverage);
            Shader.SetGlobalFloat("_CloudHeight", cloudHeight);
        }

        /// <summary>
        /// Updates fog density with smooth transitions.
        /// </summary>
        private void UpdateFog()
        {
            fogDensity = Mathf.Lerp(fogDensity, targetFogDensity, Time.deltaTime * fogTransitionSpeed);
            RenderSettings.fogDensity = fogDensity;
        }

        /// <summary>
        /// Updates active weather fronts.
        /// </summary>
        private void UpdateFronts()
        {
            frontSpawnTimer += Time.deltaTime;
            if (frontSpawnTimer >= frontSpawnInterval && activeFronts.Count < maxFronts)
            {
                frontSpawnTimer = 0f;
                SpawnRandomFront();
            }

            for (int i = activeFronts.Count - 1; i >= 0; i--)
            {
                activeFronts[i].Update(Time.deltaTime);
                if (activeFronts[i].Intensity <= 0.01f)
                {
                    activeFronts.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Spawns a random weather front.
        /// </summary>
        private void SpawnRandomFront()
        {
            WeatherFront front = new WeatherFront();
            front.Position = new Vector2(
                UnityEngine.Random.Range(-5000f, 5000f),
                UnityEngine.Random.Range(-5000f, 5000f)
            );
            front.Velocity = new Vector2(
                UnityEngine.Random.Range(-20f, 20f),
                UnityEngine.Random.Range(-20f, 20f)
            );
            front.Radius = UnityEngine.Random.Range(500f, 2000f);
            front.Intensity = UnityEngine.Random.Range(0.3f, 1f);
            front.Type = (WeatherFront.FrontType)UnityEngine.Random.Range(0, 4);

            switch (front.Type)
            {
                case WeatherFront.FrontType.Cold:
                    front.Temperature = temperature - UnityEngine.Random.Range(5f, 15f);
                    front.Pressure = pressure + UnityEngine.Random.Range(0f, 2000f);
                    break;
                case WeatherFront.FrontType.Warm:
                    front.Temperature = temperature + UnityEngine.Random.Range(5f, 15f);
                    front.Pressure = pressure - UnityEngine.Random.Range(0f, 2000f);
                    break;
                case WeatherFront.FrontType.Occluded:
                    front.Temperature = temperature + UnityEngine.Random.Range(-5f, 5f);
                    front.Pressure = pressure - UnityEngine.Random.Range(1000f, 3000f);
                    break;
                case WeatherFront.FrontType.Stationary:
                    front.Velocity = Vector2.zero;
                    break;
            }

            activeFronts.Add(front);
        }

        /// <summary>
        /// Updates lightning during thunderstorms.
        /// </summary>
        private void UpdateLightning()
        {
            if (lightningFrequency <= 0f) return;

            lightningTimer -= Time.deltaTime;
            if (lightningTimer <= 0f)
            {
                lightningTimer = UnityEngine.Random.Range(1f / lightningFrequency, 3f / lightningFrequency);
                Vector3 strikePosition = new Vector3(
                    UnityEngine.Random.Range(-1000f, 1000f),
                    0f,
                    UnityEngine.Random.Range(-1000f, 1000f)
                );
                TriggerLightning(strikePosition);
            }
        }

        /// <summary>
        /// Updates atmospheric conditions based on fronts and weather.
        /// </summary>
        private void UpdateAtmosphericConditions()
        {
            Vector2 currentPos = new Vector2(transform.position.x, transform.position.z);
            float frontInfluence = 0f;
            float tempDelta = 0f;
            float pressureDelta = 0f;
            float humidityDelta = 0f;

            foreach (var front in activeFronts)
            {
                float influence = front.GetInfluence(currentPos);
                if (influence > 0f)
                {
                    frontInfluence += influence;
                    tempDelta += (front.Temperature - temperature) * influence;
                    pressureDelta += (front.Pressure - pressure) * influence;
                    humidityDelta += (front.Humidity - humidity) * influence;
                }
            }

            if (frontInfluence > 0f)
            {
                temperature += tempDelta * Time.deltaTime * 0.01f;
                pressure += pressureDelta * Time.deltaTime * 0.01f;
                humidity = Mathf.Clamp01(humidity + humidityDelta * Time.deltaTime * 0.01f);
            }

            windSpeed = Mathf.Lerp(windSpeed, CalculateWeatherWindSpeed(), Time.deltaTime * 0.1f);
            windDirection += Mathf.Sin(Time.time * 0.1f) * Time.deltaTime * 5f;
        }

        /// <summary>
        /// Calculates wind speed based on current weather state.
        /// </summary>
        private float CalculateWeatherWindSpeed()
        {
            return currentWeather switch
            {
                WeatherState.Clear => 3f,
                WeatherState.Overcast => 8f,
                WeatherState.Rain => 12f,
                WeatherState.Fog => 2f,
                WeatherState.Snow => 10f,
                WeatherState.Thunderstorm => 25f,
                _ => 5f
            };
        }

        /// <summary>
        /// Gets the precipitation type based on temperature.
        /// </summary>
        /// <returns>0 for rain, 1 for snow, 2 for hail.</returns>
        public int GetPrecipitationType()
        {
            if (currentWeather == WeatherState.Thunderstorm && temperature > 20f) return 2;
            if (temperature <= 0f) return 1;
            return 0;
        }

        /// <summary>
        /// Calculates visibility based on current conditions.
        /// </summary>
        /// <returns>Visibility range in meters.</returns>
        public float CalculateVisibility()
        {
            float baseVisibility = 15000f;
            float precipitationReduction = precipitationIntensity * 8000f;
            float fogReduction = fogDensity * 50000f;
            return Mathf.Max(50f, baseVisibility - precipitationReduction - fogReduction);
        }
    }
}
