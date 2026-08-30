using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    [Serializable]
    public struct SoundWaveState
    {
        public Vector3 origin;
        public Vector3 direction;
        public float frequency;
        public float amplitude;
        public float wavelength;
        public float speed;
        public float time;
        public float attenuation;
    }

    [Serializable]
    public struct DopplerState
    {
        public float sourceVelocity;
        public float listenerVelocity;
        public float frequency;
        public float observedFrequency;
        public float speedOfSound;
    }

    public static class AcousticPhysics
    {
        public static float CalculateDopplerShift(float sourceVelocity, float listenerVelocity, float frequency, float speedOfSound = 343f)
        {
            float relativeVelocity = sourceVelocity - listenerVelocity;
            if (relativeVelocity >= speedOfSound)
            {
                return frequency * (speedOfSound / (speedOfSound - relativeVelocity));
            }
            return frequency * (speedOfSound / (speedOfSound - relativeVelocity));
        }

        public static float CalculateSonicBoom(float velocity, float speedOfSound = 343f)
        {
            if (velocity < speedOfSound) return 0f;
            float machNumber = velocity / speedOfSound;
            float overpressure = 0.5f * (machNumber * machNumber - 1f) * 101325f;
            return overpressure * 0.001f;
        }

        public static float CalculateAtmosphericAbsorption(float distance, float frequency, float humidity, float temperature)
        {
            float absorptionCoefficient = 0.0001f * frequency * humidity / (temperature + 273.15f);
            return Mathf.Exp(-absorptionCoefficient * distance);
        }

        public static float CalculateMaterialTransmissionLoss(float materialThickness, float materialDensity, float frequency)
        {
            float transmissionLoss = 20f * Mathf.Log10(frequency * materialDensity * materialThickness * 0.001f);
            return Mathf.Clamp(transmissionLoss, 0f, 100f);
        }

        public static float CalculateReverberationTime(float roomVolume, float surfaceAbsorption, float speedOfSound = 343f)
        {
            float meanFreePath = 4f * roomVolume / (6f * Mathf.Max(1f, roomVolume));
            return (meanFreePath * surfaceAbsorption) / speedOfSound;
        }

        public static float CalculateSoundPressureLevel(float intensity, float referenceIntensity = 1e-12f)
        {
            return 10f * Mathf.Log10(intensity / referenceIntensity);
        }

        public static float CalculateIntensity(float pressureAmplitude, float airDensity, float speedOfSound)
        {
            return (pressureAmplitude * pressureAmplitude) / (airDensity * speedOfSound);
        }

        public static float CalculateSoundReductionIndex(float frequency, float materialDensity, float thickness)
        {
            float massPerArea = materialDensity * thickness;
            return 20f * Mathf.Log10(frequency * massPerArea * 0.01f);
        }

        public static SoundWaveState SimulateWavePropagation(SoundWaveState state, float deltaTime, float airDensity, float humidity, float temperature)
        {
            float speed = CalculateSoundSpeed(temperature, humidity);
            Vector3 newPosition = state.origin + state.direction * speed * deltaTime;
            float attenuation = CalculateAtmosphericAbsorption(state.time * speed, state.frequency, humidity, temperature);
            return new SoundWaveState
            {
                origin = newPosition,
                direction = state.direction,
                frequency = state.frequency,
                amplitude = state.amplitude * attenuation,
                wavelength = speed / state.frequency,
                speed = speed,
                time = state.time + deltaTime,
                attenuation = attenuation
            };
        }

        public static float CalculateSoundSpeed(float temperature, float humidity)
        {
            float speedDry = 331.3f + 0.606f * temperature;
            float humidityCorrection = 0.01f * humidity * temperature;
            return speedDry + humidityCorrection;
        }

        public static float CalculateMuzzleSonicSignature(float muzzleVelocity, float bulletMass, float caliber)
        {
            if (muzzleVelocity < 343f) return 0f;
            float kineticEnergy = 0.5f * bulletMass * muzzleVelocity * muzzleVelocity;
            float sonicBoom = CalculateSonicBoom(muzzleVelocity);
            float crack = kineticEnergy * 0.0001f;
            return sonicBoom + crack;
        }

        public static float CalculateEchoIntensity(float sourceIntensity, float distance, float reflectionCoefficient, int reflections)
        {
            float attenuation = 1f / (1f + distance * distance * 0.02f);
            return sourceIntensity * attenuation * Mathf.Pow(reflectionCoefficient, reflections);
        }
    }
}
