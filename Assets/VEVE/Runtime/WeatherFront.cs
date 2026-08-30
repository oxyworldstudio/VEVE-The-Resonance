using UnityEngine;
using System;
using System.Collections.Generic;

namespace VEVE
{
    /// <summary>
    /// Represents a weather front with associated pressure and movement characteristics.
    /// </summary>
    [Serializable]
    public class WeatherFront
    {
        /// <summary>
        /// Types of weather fronts.
        /// </summary>
        public enum FrontType { Cold, Warm, Occluded, Stationary }

        [SerializeField] private FrontType type = FrontType.Cold;
        [SerializeField] private float pressure = 101325f;
        [SerializeField] private float temperature = 15f;
        [SerializeField] private float humidity = 0.5f;
        [SerializeField] private Vector2 position;
        [SerializeField] private Vector2 velocity;
        [SerializeField] private float radius = 1000f;
        [SerializeField] private float intensity = 1f;

        /// <summary>
        /// Gets or sets the type of weather front.
        /// </summary>
        public FrontType Type
        {
            get => type;
            set => type = value;
        }

        /// <summary>
        /// Gets or sets the atmospheric pressure in Pascals.
        /// </summary>
        public float Pressure
        {
            get => pressure;
            set => pressure = value;
        }

        /// <summary>
        /// Gets or sets the temperature in Celsius.
        /// </summary>
        public float Temperature
        {
            get => temperature;
            set => temperature = value;
        }

        /// <summary>
        /// Gets or sets the relative humidity (0-1).
        /// </summary>
        public float Humidity
        {
            get => humidity;
            set => humidity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Gets or sets the position of the front center.
        /// </summary>
        public Vector2 Position
        {
            get => position;
            set => position = value;
        }

        /// <summary>
        /// Gets or sets the velocity of the front.
        /// </summary>
        public Vector2 Velocity
        {
            get => velocity;
            set => velocity = value;
        }

        /// <summary>
        /// Gets or sets the radius of influence.
        /// </summary>
        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        /// <summary>
        /// Gets or sets the intensity of the front (0-1).
        /// </summary>
        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Updates the front position based on velocity and time.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds.</param>
        public void Update(float deltaTime)
        {
            position += velocity * deltaTime;
            intensity = Mathf.Max(0f, intensity - deltaTime * 0.0001f);
        }

        /// <summary>
        /// Calculates the influence of this front at a given point.
        /// </summary>
        /// <param name="point">World position to sample.</param>
        /// <returns>Influence factor (0-1).</returns>
        public float GetInfluence(Vector2 point)
        {
            float distance = Vector2.Distance(position, point);
            if (distance > radius) return 0f;
            return Mathf.Lerp(1f, 0f, distance / radius) * intensity;
        }
    }
}
