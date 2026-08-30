using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public enum WeatherState { Clear, Overcast, Rain, Fog, Snow, Thunderstorm }

    public sealed class EnvironmentSimulation : MonoBehaviour
    {
        [SerializeField] private WeatherState weather = WeatherState.Clear;
        [SerializeField] private Light sun;
        [SerializeField] private float dayLengthSeconds = 86400f;
        [SerializeField] private float temperature = 15f;
        [SerializeField] private float humidity = 0.5f;
        [SerializeField] private float windSpeed = 0f;
        [SerializeField] private float windDirection = 0f;
        [SerializeField] private float precipitationIntensity = 0f;
        [SerializeField] private float fogDensity = 0f;
        [SerializeField] private float visibilityRange = 15000f;
        [SerializeField] private RealismConfig realismConfig;

        private float elapsed;

        private void Update()
        {
            elapsed = (elapsed + Time.deltaTime) % Mathf.Max(1f, dayLengthSeconds);
            if (sun != null) sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(-90f, 270f, elapsed / dayLengthSeconds), -30f, 0f);
            UpdateAtmosphericConditions();
        }

        public void SetWeather(WeatherState value, float intensity = 1f)
        {
            weather = value;
            precipitationIntensity = intensity;
            ApplyWeather();
        }

        private void UpdateAtmosphericConditions()
        {
            float altitude = transform.position.y;
            float adjustedTemp = temperature - 6.5f * altitude / 1000f;
            float airDensity = RealismConfig.CalculateAirDensity(altitude, adjustedTemp);
            visibilityRange = Mathf.Lerp(150f, 15000f, Mathf.Clamp01(airDensity / 1.225f));
        }

        private void ApplyWeather()
        {
            if (realismConfig != null)
            {
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogColor = GetFogColor();
                if (sun != null)
                {
                    sun.intensity = Mathf.Lerp(0.1f, 1f, weather == WeatherState.Clear ? 1f : 0.3f);
                    sun.colorTemperature = Mathf.Lerp(3000f, 6500f, weather == WeatherState.Clear ? 1f : 0.5f);
                }
            }
            else
            {
                RenderSettings.fog = weather == WeatherState.Fog || weather == WeatherState.Rain;
                RenderSettings.fogDensity = weather == WeatherState.Fog ? 0.035f : 0.008f;
                RenderSettings.fogColor = weather == WeatherState.Rain ? new Color(0.22f, 0.27f, 0.32f) : new Color(0.5f, 0.55f, 0.6f);
                if (sun != null) sun.intensity = weather == WeatherState.Rain ? 0.55f : 1f;
            }
        }

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

        public float Temperature => temperature;
        public float Humidity => humidity;
        public float WindSpeed => windSpeed;
        public float WindDirection => windDirection;
        public float VisibilityRange => visibilityRange;
    }
}
