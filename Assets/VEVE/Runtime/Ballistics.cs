using UnityEngine;

namespace VEVE
{
    public readonly struct BallisticImpact
    {
        public readonly float incomingEnergy;
        public readonly float remainingEnergy;
        public readonly bool penetrated;

        public BallisticImpact(float incomingEnergy, float remainingEnergy, bool penetrated)
        {
            this.incomingEnergy = incomingEnergy;
            this.remainingEnergy = remainingEnergy;
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
            float resistance = material == SurfaceMaterial.Wood ? 0.35f : 0.8f;
            return Mathf.Max(0f, energy - resistance * thickness);
        }

    public static float EnergyAfterDistance(float energy, float distance)
    {
        if (energy <= 0f || distance < 0f) return 0f;
        return energy / (1f + distance * distance * 0.004f);
    }

    public static float GravityDrop(float velocity, float distance, float gravity = 9.81f)
    {
        return 0.5f * gravity * (distance / velocity) * (distance / velocity);
    }

    public static float WindDrift(float distance, float windSpeed, float windAngle, float bulletSpeed)
    {
        float flightTime = distance / bulletSpeed;
        return windSpeed * Mathf.Sin(windAngle * Mathf.Deg2Rad) * flightTime * flightTime * 0.5f;
    }

    public static BallisticImpact ResolveImpact(float incomingEnergy, SurfaceMaterial material, float thickness)
    {
        float remaining = EnergyAfterMaterial(incomingEnergy, material, thickness);
        return new BallisticImpact(incomingEnergy, remaining, remaining > 0f);
    }

    public static float CalculatePenetrationDepth(float energy, SurfaceMaterial material)
    {
        float resistance = material == SurfaceMaterial.Wood ? 35f : material == SurfaceMaterial.Concrete ? 80f : 120f;
        return energy / resistance;
    }
    }
}
