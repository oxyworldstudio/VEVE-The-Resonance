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

    public enum SurfaceMaterial { Wood, Concrete }

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

        public static BallisticImpact ResolveImpact(float incomingEnergy, SurfaceMaterial material, float thickness)
        {
            float remaining = EnergyAfterMaterial(incomingEnergy, material, thickness);
            return new BallisticImpact(incomingEnergy, remaining, remaining > 0f);
        }
    }
}
