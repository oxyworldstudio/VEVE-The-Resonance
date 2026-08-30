using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public readonly struct BallisticImpact
    {
        public readonly float incomingEnergy;
        public readonly float remainingEnergy;
        public readonly float residualVelocity;
        public readonly bool penetrated;

        public BallisticImpact(float incomingEnergy, float remainingEnergy, float residualVelocity, bool penetrated)
        {
            this.incomingEnergy = incomingEnergy;
            this.remainingEnergy = remainingEnergy;
            this.residualVelocity = residualVelocity;
            this.penetrated = penetrated;
        }
    }

    public enum SurfaceMaterial { Wood, Concrete, Metal, Glass, Fabric, Dirt, Ice }

    public static class Ballistics
    {
        public static bool TryPenetrate(float energy, SurfaceMaterial material, float thickness, out float remainingEnergy)
        {
            remainingEnergy = EnergyAfterMaterial(energy, material, thickness);
            return remainingEnergy > 0f;
        }

        public static float EnergyAfterMaterial(float energy, SurfaceMaterial material, float thickness)
        {
            float resistance = material switch
            {
                SurfaceMaterial.Wood => 35f,
                SurfaceMaterial.Concrete => 80f,
                SurfaceMaterial.Metal => 120f,
                SurfaceMaterial.Glass => 15f,
                SurfaceMaterial.Fabric => 8f,
                SurfaceMaterial.Dirt => 20f,
                SurfaceMaterial.Ice => 10f,
                _ => 35f
            };
            return Mathf.Max(0f, energy - resistance * thickness);
        }

    public static float EnergyAfterDistance(float energy, float distance, float ballisticCoefficient = 0.3f)
    {
        if (energy <= 0f || distance < 0f) return 0f;
        float velocity = Mathf.Sqrt(2f * energy / 0.01f);
        float dragDeceleration = velocity * velocity * ballisticCoefficient * 1.225f * 0.01f;
        return energy - dragDeceleration * distance;
    }

    public static float GravityDrop(float velocity, float distance, float gravity = 9.80665f)
    {
        if (velocity <= 0f || distance < 0f) return 0f;
        float time = distance / velocity;
        return 0.5f * gravity * time * time;
    }

    public static float WindDrift(float distance, float windSpeed, float windAngle, float bulletSpeed, float dragCoefficient = 0.3f)
    {
        if (bulletSpeed <= 0f || distance < 0f) return 0f;
        float flightTime = distance / bulletSpeed;
        float dragForce = 0.5f * 1.225f * windSpeed * windSpeed * dragCoefficient * 0.01f;
        return dragForce * flightTime * flightTime * 0.5f;
    }

    public static float CoriolisDrift(float latitude, float azimuth, float distance, float bulletSpeed)
    {
        if (bulletSpeed <= 0f || distance < 0f) return 0f;
        float timeOfFlight = distance / bulletSpeed;
        float coriolisAcceleration = 2f * 0.000072921f * Mathf.Sin(latitude * Mathf.Deg2Rad);
        return 0.5f * coriolisAcceleration * timeOfFlight * timeOfFlight;
    }

    public static float SpinDrift(float twistRate, float distance, float bulletSpeed)
    {
        if (bulletSpeed <= 0f || distance < 0f || twistRate <= 0f) return 0f;
        float timeOfFlight = distance / bulletSpeed;
        float gyroscopicStability = 1f / (twistRate * 0.01f);
        return gyroscopicStability * timeOfFlight * 0.02f;
    }

    public static BallisticImpact ResolveImpact(float incomingEnergy, SurfaceMaterial material, float thickness, float bulletMass = 0.01f)
    {
        float remaining = EnergyAfterMaterial(incomingEnergy, material, thickness);
        float residualVelocity = remaining > 0f ? Mathf.Sqrt(2f * remaining / bulletMass) : 0f;
        return new BallisticImpact(incomingEnergy, remaining, residualVelocity, remaining > 0f);
    }

    public static float CalculatePenetrationDepth(float energy, SurfaceMaterial material)
    {
        float resistance = material switch
        {
            SurfaceMaterial.Wood => 35f,
            SurfaceMaterial.Concrete => 80f,
            SurfaceMaterial.Metal => 120f,
            SurfaceMaterial.Glass => 15f,
            SurfaceMaterial.Fabric => 8f,
            SurfaceMaterial.Dirt => 20f,
            SurfaceMaterial.Ice => 10f,
            _ => 35f
        };
        return energy / resistance;
    }
    }
}
