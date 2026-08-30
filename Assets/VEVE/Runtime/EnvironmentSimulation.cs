using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    /// <summary>
    /// Defines the possible weather states in the environment.
    /// </summary>
    public enum WeatherState { Clear, Overcast, Rain, Fog, Snow, Thunderstorm }

    /// <summary>
    /// Defines the lunar phases for realistic moon rendering.
    /// </summary>
    public enum MoonPhase { NewMoon, WaxingCrescent, FirstQuarter, WaxingGibbous, FullMoon, WaningGibbous, LastQuarter, WaningCrescent }

    /// <summary>
    /// Manages the full environment simulation including day/night cycle, astronomical sun positioning,
    /// moon phases, and star field rendering.
    /// </summary>
    public sealed class EnvironmentSimulation : MonoBehaviour
    {
        [Header("Time Configuration")]
        [SerializeField] private float dayLengthSeconds = 86400f;
        [SerializeField] private float startHour = 12f;
        [SerializeField] private bool useRealTime = false;

        [Header("Astronomical Configuration")]
        [SerializeField] private float latitude = 45f;
        [SerializeField] private float longitude = 0f;
        [SerializeField] private float timeZoneOffset = 0f;
        [SerializeField] private int dayOfYear = 172;

        [Header("Sun Configuration")]
        [SerializeField] private Light sun;
        [SerializeField] private float sunIntensity = 1.4f;
        [SerializeField] private AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private Gradient sunColorGradient;

        [Header("Moon Configuration")]
        [SerializeField] private Light moon;
        [SerializeField] private float moonIntensity = 0.3f;
        [SerializeField] private float moonPhaseDuration = 29.53f;

        [Header("Star Field")]
        [SerializeField] private float starFieldIntensity = 1f;
        [SerializeField] private int starCount = 2000;
        [SerializeField] private float starMagnitudeLimit = 6.0f;

        [Header("Environment")]
        [SerializeField] private WeatherState weather = WeatherState.Clear;
        [SerializeField] private float temperature = 15f;
        [SerializeField] private float humidity = 0.5f;
        [SerializeField] private float windSpeed = 0f;
        [SerializeField] private float windDirection = 0f;
        [SerializeField] private float precipitationIntensity = 0f;
        [SerializeField] private float fogDensity = 0f;
        [SerializeField] private float visibilityRange = 15000f;
        [SerializeField] private RealismConfig realismConfig;

        [Header("Atmosphere")]
        [SerializeField] private float turbidity = 2.0f;
        [SerializeField] private float rayleighCoefficient = 1.0f;
        [SerializeField] private float mieCoefficient = 1.0f;

        private float elapsed;
        private float solarTime;
        private float sunDeclination;
        private float equationOfTime;
        private MoonPhase currentMoonPhase;

        /// <summary>
        /// Gets the current hour of the day (0-24).
        /// </summary>
        public float CurrentHour => (elapsed / dayLengthSeconds) * 24f;

        /// <summary>
        /// Gets the current solar elevation angle in degrees.
        /// </summary>
        public float SunElevation { get; private set; }

        /// <summary>
        /// Gets the current solar azimuth angle in degrees.
        /// </summary>
        public float SunAzimuth { get; private set; }

        /// <summary>
        /// Gets the current moon phase.
        /// </summary>
        public MoonPhase CurrentMoonPhase => currentMoonPhase;

        /// <summary>
        /// Gets the current sun direction in world space.
        /// </summary>
        public Vector3 SunDirection { get; private set; }

        /// <summary>
        /// Gets the current moon direction in world space.
        /// </summary>
        public Vector3 MoonDirection { get; private set; }

        /// <summary>
        /// Gets whether it is currently daytime.
        /// </summary>
        public bool IsDaytime => SunElevation > -6f;

        /// <summary>
        /// Gets whether it is currently twilight (civil twilight).
        /// </summary>
        public bool IsTwilight => SunElevation <= 0f && SunElevation > -6f;

        /// <summary>
        /// Gets whether it is currently nighttime.
        /// </summary>
        public bool IsNighttime => SunElevation <= -6f;

        /// <summary>
        /// Gets or sets the ambient temperature in Celsius.
        /// </summary>
        public float Temperature { get => temperature; set => temperature = value; }

        /// <summary>
        /// Gets or sets the relative humidity (0-1).
        /// </summary>
        public float Humidity { get => humidity; set => humidity = Mathf.Clamp01(value); }

        /// <summary>
        /// Gets or sets the wind speed in m/s.
        /// </summary>
        public float WindSpeed { get => windSpeed; set => windSpeed = value; }

        /// <summary>
        /// Gets or sets the wind direction in degrees.
        /// </summary>
        public float WindDirection { get => windDirection; set => windDirection = value; }

        /// <summary>
        /// Gets the current visibility range in meters.
        /// </summary>
        public float VisibilityRange => visibilityRange;

        /// <summary>
        /// Gets the current weather state.
        /// </summary>
        public WeatherState CurrentWeather => weather;

        /// <summary>
        /// Gets the current sun color based on elevation and time.
        /// </summary>
        public Color SunColor { get; private set; }

        /// <summary>
        /// Gets the current moon illumination (0-1).
        /// </summary>
        public float MoonIllumination { get; private set; }

        /// <summary>
        /// Gets the current precipitation intensity (0-1).
        /// </summary>
        public float PrecipitationIntensity => precipitationIntensity;

        private void Awake()
        {
            InitializeAstronomicalData();
            elapsed = (startHour / 24f) * dayLengthSeconds;
            UpdateMoonPhase();
        }

        private void Update()
        {
            UpdateTime();
            CalculateAstronomicalPositions();
            UpdateSunLight();
            UpdateMoonLight();
            UpdateStarField();
            UpdateAtmosphericConditions();
        }

        /// <summary>
        /// Initializes astronomical calculation parameters.
        /// </summary>
        private void InitializeAstronomicalData()
        {
            sunDeclination = CalculateSolarDeclination(dayOfYear);
            equationOfTime = CalculateEquationOfTime(dayOfYear);
            SunColor = sunColorGradient != null && sunColorGradient.colorKeys.Length > 0
                ? sunColorGradient.Evaluate(0.5f)
                : Color.white;
        }

        /// <summary>
        /// Updates the simulation time based on frame delta or real time.
        /// </summary>
        private void UpdateTime()
        {
            if (useRealTime)
            {
                System.DateTime now = System.DateTime.UtcNow;
                elapsed = (float)(now.Hour * 3600 + now.Minute * 60 + now.Second);
            }
            else
            {
                elapsed = (elapsed + Time.deltaTime * (86400f / dayLengthSeconds)) % 86400f;
            }
            solarTime = elapsed + equationOfTime * 60f + (longitude / 15f - timeZoneOffset) * 3600f;
        }

        /// <summary>
        /// Calculates the solar declination angle for the given day of year.
        /// </summary>
        /// <param name="day">Day of year (1-365).</param>
        /// <returns>Solar declination in degrees.</returns>
        public static float CalculateSolarDeclination(int day)
        {
            float angle = (360f / 365f) * (day - 81);
            return 23.45f * Mathf.Sin(angle * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Calculates the equation of time for the given day of year.
        /// </summary>
        /// <param name="day">Day of year (1-365).</param>
        /// <returns>Equation of time in minutes.</returns>
        public static float CalculateEquationOfTime(int day)
        {
            float b = (360f / 365f) * (day - 81) * Mathf.Deg2Rad;
            return 9.87f * Mathf.Sin(2f * b) - 7.53f * Mathf.Cos(b) - 1.5f * Mathf.Sin(b);
        }

        /// <summary>
        /// Calculates sun position using astronomical algorithms.
        /// </summary>
        private void CalculateAstronomicalPositions()
        {
            float hourAngle = ((solarTime / 3600f) - 12f) * 15f;
            float latRad = latitude * Mathf.Deg2Rad;
            float decRad = sunDeclination * Mathf.Deg2Rad;
            float hourRad = hourAngle * Mathf.Deg2Rad;

            float elevation = Mathf.Asin(
                Mathf.Sin(latRad) * Mathf.Sin(decRad) +
                Mathf.Cos(latRad) * Mathf.Cos(decRad) * Mathf.Cos(hourRad)
            ) * Mathf.Rad2Deg;

            float azimuth = Mathf.Atan2(
                -Mathf.Sin(hourRad),
                Mathf.Tan(decRad) * Mathf.Cos(latRad) - Mathf.Sin(latRad) * Mathf.Cos(hourRad)
            ) * Mathf.Rad2Deg + 180f;

            SunElevation = elevation;
            SunAzimuth = azimuth;

            float elevRad = elevation * Mathf.Deg2Rad;
            float azimRad = azimuth * Mathf.Deg2Rad;
            SunDirection = new Vector3(
                Mathf.Cos(elevRad) * Mathf.Sin(azimRad),
                Mathf.Sin(elevRad),
                Mathf.Cos(elevRad) * Mathf.Cos(azimRad)
            );

            float moonLongitude = (elapsed / dayLengthSeconds) * 360f / moonPhaseDuration;
            float moonElevation = elevation * 0.8f + 10f * Mathf.Sin(moonLongitude * Mathf.Deg2Rad);
            float moonAzimuth = azimuth + 180f + 30f * Mathf.Cos(moonLongitude * Mathf.Deg2Rad);
            float moonElevRad = moonElevation * Mathf.Deg2Rad;
            float moonAzimRad = moonAzimuth * Mathf.Deg2Rad;
            MoonDirection = new Vector3(
                Mathf.Cos(moonElevRad) * Mathf.Sin(moonAzimRad),
                Mathf.Sin(moonElevRad),
                Mathf.Cos(moonElevRad) * Mathf.Cos(moonAzimRad)
            );
        }

        /// <summary>
        /// Updates the moon phase based on elapsed time.
        /// </summary>
        private void UpdateMoonPhase()
        {
            float phaseProgress = (elapsed / dayLengthSeconds) / moonPhaseDuration;
            int phaseIndex = Mathf.FloorToInt((phaseProgress % 1f) * 8f);
            currentMoonPhase = (MoonPhase)Mathf.Clamp(phaseIndex, 0, 7);

            float phaseAngle = (phaseProgress % 1f) * 360f;
            MoonIllumination = 0.5f * (1f - Mathf.Cos(phaseAngle * Mathf.Deg2Rad));
        }

        /// <summary>
        /// Updates the sun directional light based on calculated position.
        /// </summary>
        private void UpdateSunLight()
        {
            if (sun == null) return;

            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);

            float normalizedElevation = Mathf.InverseLerp(-18f, 90f, SunElevation);
            float intensityMultiplier = sunIntensityCurve != null ? sunIntensityCurve.Evaluate(normalizedElevation) : normalizedElevation;
            sun.intensity = sunIntensity * intensityMultiplier * GetWeatherIntensityModifier();

            UpdateSunColor();
            UpdateShadowSettings();
        }

        /// <summary>
        /// Updates the sun color based on elevation and atmospheric conditions.
        /// </summary>
        private void UpdateSunColor()
        {
            if (SunElevation > 10f)
            {
                SunColor = Color.white;
                sun.colorTemperature = 6500f;
            }
            else if (SunElevation > 0f)
            {
                float t = SunElevation / 10f;
                SunColor = new Color(1f, Mathf.Lerp(0.5f, 1f, t), Mathf.Lerp(0.2f, 1f, t));
                sun.colorTemperature = Mathf.Lerp(2700f, 6500f, t);
            }
            else if (SunElevation > -6f)
            {
                float t = (SunElevation + 6f) / 6f;
                SunColor = new Color(1f, Mathf.Lerp(0.3f, 0.5f, t), Mathf.Lerp(0.1f, 0.2f, t));
                sun.colorTemperature = Mathf.Lerp(2000f, 2700f, t);
            }
            else
            {
                SunColor = new Color(0.1f, 0.1f, 0.2f);
                sun.colorTemperature = 4500f;
            }

            sun.color = SunColor;
        }

        /// <summary>
        /// Updates shadow quality based on sun position and weather conditions.
        /// </summary>
        private void UpdateShadowSettings()
        {
            if (SunElevation > 0f)
            {
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(SunElevation / 45f));
                sun.shadowBias = 0.05f;
                sun.shadowNormalBias = 0.4f;
            }
            else
            {
                sun.shadows = LightShadows.None;
            }
        }

        /// <summary>
        /// Updates the moon light based on moon position and phase.
        /// </summary>
        private void UpdateMoonLight()
        {
            if (moon == null) return;

            moon.transform.rotation = Quaternion.LookRotation(-MoonDirection);
            moon.intensity = MoonIllumination * moonIntensity * (SunElevation < 0f ? 1f : 0f);
            moon.color = new Color(0.8f, 0.85f, 1f);
            moon.colorTemperature = 4100f;

            moon.shadows = MoonElevationAboveHorizon() && MoonIllumination > 0.3f ? LightShadows.Soft : LightShadows.None;
            moon.shadowStrength = 0.4f;
        }

        /// <summary>
        /// Determines if the moon is above the horizon.
        /// </summary>
        private bool MoonElevationAboveHorizon()
        {
            return MoonDirection.y > 0f;
        }

        /// <summary>
        /// Updates the star field intensity based on time and conditions.
        /// </summary>
        private void UpdateStarField()
        {
            float skyDarkness = 1f - Mathf.Clamp01((SunElevation + 6f) / 6f);
            float weatherDampening = weather == WeatherState.Clear ? 1f : 0.2f;
            float starIntensity = starFieldIntensity * skyDarkness * weatherDampening;
            Shader.SetGlobalFloat("_StarFieldIntensity", starIntensity);
            Shader.SetGlobalFloat("_StarMagnitudeLimit", starMagnitudeLimit);
            Shader.SetGlobalInt("_StarCount", starCount);
        }

        /// <summary>
        /// Gets the weather-based modifier for sun intensity.
        /// </summary>
        private float GetWeatherIntensityModifier()
        {
            return weather switch
            {
                WeatherState.Clear => 1f,
                WeatherState.Overcast => 0.5f,
                WeatherState.Rain => 0.3f,
                WeatherState.Fog => 0.25f,
                WeatherState.Snow => 0.4f,
                WeatherState.Thunderstorm => 0.15f,
                _ => 1f
            };
        }

        /// <summary>
        /// Updates atmospheric conditions including temperature, visibility, and air density.
        /// </summary>
        private void UpdateAtmosphericConditions()
        {
            float altitude = transform.position.y;
            float adjustedTemp = temperature - 6.5f * altitude / 1000f;
            float airDensity = RealismConfig.CalculateAirDensity(altitude, adjustedTemp);
            visibilityRange = Mathf.Lerp(150f, 15000f, Mathf.Clamp01(airDensity / 1.225f));

            if (realismConfig == null)
            {
                RenderSettings.fog = weather == WeatherState.Fog || weather == WeatherState.Rain;
                RenderSettings.fogDensity = weather == WeatherState.Fog ? 0.035f : 0.008f;
                RenderSettings.fogColor = weather == WeatherState.Rain ? new Color(0.22f, 0.27f, 0.32f) : new Color(0.5f, 0.55f, 0.6f);
            }
            else
            {
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogColor = GetFogColor();
            }
        }

        /// <summary>
        /// Sets the weather state and precipitation intensity.
        /// </summary>
        /// <param name="value">The new weather state.</param>
        /// <param name="intensity">The precipitation intensity (0-1).</param>
        public void SetWeather(WeatherState value, float intensity = 1f)
        {
            weather = value;
            precipitationIntensity = intensity;
            ApplyWeather();
        }

        /// <summary>
        /// Applies the current weather settings to rendering.
        /// </summary>
        private void ApplyWeather()
        {
            if (realismConfig != null)
            {
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogColor = GetFogColor();
                if (sun != null)
                {
                    sun.intensity = Mathf.Lerp(0.1f, sunIntensity, GetWeatherIntensityModifier());
                    sun.colorTemperature = Mathf.Lerp(3000f, 6500f, GetWeatherIntensityModifier());
                }
            }
            else
            {
                RenderSettings.fog = weather == WeatherState.Fog || weather == WeatherState.Rain;
                RenderSettings.fogDensity = weather == WeatherState.Fog ? 0.035f : 0.008f;
                RenderSettings.fogColor = weather == WeatherState.Rain ? new Color(0.22f, 0.27f, 0.32f) : new Color(0.5f, 0.55f, 0.6f);
            }
        }

        /// <summary>
        /// Gets the fog color for the current weather state.
        /// </summary>
        private Color GetFogColor()
        {
            return weather switch
            {
                WeatherState.Fog => new Color(0.6f, 0.6f, 0.65f),
                WeatherState.Rain => new Color(0.25f, 0.28f, 0.32f),
                WeatherState.Snow => new Color(0.8f, 0.82f, 0.85f),
                WeatherState.Thunderstorm => new Color(0.15f, 0.18f, 0.22f),
                _ => new Color(0.5f, 0.55f, 0.6f)
            };
        }

        /// <summary>
        /// Sets the day of year and recalculates astronomical parameters.
        /// </summary>
        /// <param name="day">Day of year (1-365).</param>
        public void SetDayOfYear(int day)
        {
            dayOfYear = Mathf.Clamp(day, 1, 365);
            sunDeclination = CalculateSolarDeclination(dayOfYear);
            equationOfTime = CalculateEquationOfTime(dayOfYear);
        }

        /// <summary>
        /// Sets the geographic location for astronomical calculations.
        /// </summary>
        /// <param name="lat">Latitude in degrees (-90 to 90).</param>
        /// <param name="lon">Longitude in degrees (-180 to 180).</param>
        public void SetLocation(float lat, float lon)
        {
            latitude = Mathf.Clamp(lat, -90f, 90f);
            longitude = Mathf.Clamp(lon, -180f, 180f);
        }
    }
}
