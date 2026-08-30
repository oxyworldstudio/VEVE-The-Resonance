using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    [Serializable]
    public struct BoltCarrierState
    {
        public float position;
        public float velocity;
        public float mass;
        public bool isLocked;
        public bool isOpen;
        public float springForce;
    }

    [Serializable]
    public struct RecoilSpringState
    {
        public float compression;
        public float velocity;
        public float springConstant;
        public float damping;
        public float mass;
    }

    [Serializable]
    public struct BarrelThermalState
    {
        public float temperature;
        public float mass;
        public float heatCapacity;
        public float surfaceArea;
        public float emissivity;
        public float coolingRate;
        public float heatingRate;
    }

    [Serializable]
    public struct MagazineSpringState
    {
        public float compression;
        public float force;
        public float springConstant;
        public int roundsRemaining;
        public int capacity;
    }

    public static class WeaponPhysics
    {
        public static BoltCarrierState SimulateBoltCarrier(BoltCarrierState state, bool triggerPulled, float chamberPressure, float deltaTime)
        {
            float gasPressureForce = chamberPressure * 0.01f;
            float springForce = state.springForce * (1f - state.position);
            float totalForce = gasPressureForce + springForce;
            float acceleration = totalForce / state.mass;
            float newVelocity = state.velocity + acceleration * deltaTime;
            float newPosition = state.position + newVelocity * deltaTime;
            bool isLocked = newPosition < 0.1f && !triggerPulled;
            bool isOpen = newPosition > 0.5f;
            return new BoltCarrierState
            {
                position = Mathf.Clamp01(newPosition),
                velocity = newVelocity,
                mass = state.mass,
                isLocked = isLocked,
                isOpen = isOpen,
                springForce = state.springForce
            };
        }

        public static RecoilSpringState SimulateRecoilSpring(RecoilSpringState state, float recoilImpulse, float deltaTime)
        {
            float springForce = state.springConstant * state.compression;
            float dampingForce = state.damping * state.velocity;
            float totalForce = -springForce - dampingForce + recoilImpulse;
            float acceleration = totalForce / state.mass;
            float newVelocity = state.velocity + acceleration * deltaTime;
            float newCompression = state.compression + newVelocity * deltaTime;
            return new RecoilSpringState
            {
                compression = Mathf.Max(0f, newCompression),
                velocity = newVelocity,
                springConstant = state.springConstant,
                damping = state.damping,
                mass = state.mass
            };
        }

        public static BarrelThermalState SimulateBarrelThermal(BarrelThermalState state, float shotEnergy, float ambientTemperature, float deltaTime)
        {
            float heatingRate = shotEnergy * 0.001f;
            float coolingRate = state.emissivity * 5.67f * Mathf.Pow(state.temperature, 4f) * state.surfaceArea * 0.01f;
            float temperatureChange = (heatingRate - coolingRate) / (state.heatCapacity * state.mass);
            float newTemperature = state.temperature + temperatureChange * deltaTime;
            newTemperature = Mathf.MoveTowards(newTemperature, ambientTemperature, state.coolingRate * deltaTime);
            return new BarrelThermalState
            {
                temperature = Mathf.Clamp(newTemperature, ambientTemperature, 800f),
                heatCapacity = state.heatCapacity,
                surfaceArea = state.surfaceArea,
                emissivity = state.emissivity,
                coolingRate = state.coolingRate,
                heatingRate = state.heatingRate + heatingRate
            };
        }

        public static MagazineSpringState SimulateMagazineSpring(MagazineSpringState state, int roundsFired, float deltaTime)
        {
            float compressionPerRound = 1f / state.capacity;
            float newCompression = state.compression - roundsFired * compressionPerRound;
            float force = state.springConstant * newCompression;
            return new MagazineSpringState
            {
                compression = Mathf.Max(0f, newCompression),
                force = force,
                springConstant = state.springConstant,
                roundsRemaining = Mathf.Max(0, state.roundsRemaining - roundsFired),
                capacity = state.capacity
            };
        }

        public static float CalculateChamberPressure(float powderMass, float barrelLength, float chamberVolume, float temperature)
        {
            float burnRate = 0.01f * temperature;
            float gasGeneration = powderMass * burnRate;
            float expansionRatio = barrelLength / chamberVolume;
            return gasGeneration * expansionRatio * 1000f;
        }

        public static float CalculateRecoilImpulse(float muzzleEnergy, float bulletMass, float weaponMass)
        {
            float momentum = bulletMass * Mathf.Sqrt(2f * muzzleEnergy / bulletMass);
            return momentum / weaponMass;
        }

        public static float CalculateBarrelHarmonics(float barrelLength, float barrelMass, float YoungsModulus)
        {
            float stiffness = YoungsModulus * 0.01f / barrelLength;
            float naturalFrequency = Mathf.Sqrt(stiffness / barrelMass) / (2f * Mathf.PI);
            return naturalFrequency;
        }

        public static float CalculateHeatDissipation(float temperature, float ambientTemperature, float surfaceArea, float emissivity)
        {
            float deltaT = temperature - ambientTemperature;
            return emissivity * 5.67f * surfaceArea * Mathf.Pow(deltaT, 4f) * 0.01f;
        }

        public static float CalculateBarrelWear(float shotsFired, float chamberPressure, float bulletMass, float barrelLength)
        {
            float wearPerShot = chamberPressure * bulletMass * 0.0001f / barrelLength;
            return shotsFired * wearPerShot;
        }
    }
}
