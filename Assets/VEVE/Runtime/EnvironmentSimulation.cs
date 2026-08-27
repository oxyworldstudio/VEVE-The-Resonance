using UnityEngine;

namespace VEVE
{
    public enum WeatherState { Clear, Rain, Fog }

    public sealed class EnvironmentSimulation : MonoBehaviour
    {
        [SerializeField] private WeatherState weather = WeatherState.Clear;
        [SerializeField] private Light sun;
        [SerializeField] private float dayLengthSeconds = 600f;
        private float elapsed;

        private void Awake() => ApplyWeather();

        private void Update()
        {
            elapsed = (elapsed + Time.deltaTime) % Mathf.Max(1f, dayLengthSeconds);
            if (sun != null) sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(-20f, 200f, elapsed / dayLengthSeconds), -30f, 0f);
        }

        public void SetWeather(WeatherState value)
        {
            weather = value;
            ApplyWeather();
        }

        private void ApplyWeather()
        {
            RenderSettings.fog = weather == WeatherState.Fog || weather == WeatherState.Rain;
            RenderSettings.fogDensity = weather == WeatherState.Fog ? 0.035f : 0.008f;
            RenderSettings.fogColor = weather == WeatherState.Rain ? new Color(0.22f, 0.27f, 0.32f) : new Color(0.5f, 0.55f, 0.6f);
            if (sun != null) sun.intensity = weather == WeatherState.Rain ? 0.55f : 1f;
        }
    }
}
