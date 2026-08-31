using UnityEngine;
using VEVE.Catalog;

namespace VEVE.Combat
{
    /// <summary>
    /// Pure grenade/blast rules: quadratic falloff of blast energy to distance and the
    /// single authority for whether a victim's loadout can survive the strike (reuses the
    /// exact armor mitigation path as ballistic hits - same physical truth, no new math).
    /// </summary>
    public static class GrenadeRules
    {
        public const float DefaultBlastEnergyJ = 330f;   // M67-class lethal energy band at torso
        public const float DefaultRadiusM = 12f;         // documented casualty radius
        public const float DefaultFuseSeconds = 4.5f;
        public const float MinimumBlastEnergy = 0f;
        public const float VelocityEquivalentMps = 120f; // fragmentation velocity proxy for trauma math
        public const float ThrowImpulseMps = 14f;
        public const float ThrowCooldownSeconds = 0.35f;

        public static float BlastEnergyAtDistance(float distanceM, float radiusM, float totalEnergyJ)
        {
            float r = Mathf.Max(0.1f, radiusM);
            float d = Mathf.Max(0f, distanceM);
            float t = 1f - Mathf.Clamp01(d / r);
            float energy = totalEnergyJ * t * t;
            return energy < MinimumBlastEnergy ? 0f : energy;
        }

        public static float FuseClamp(float seconds)
        {
            if (seconds <= 0f) return DefaultFuseSeconds;
            if (seconds > 8f) return 8f;
            return seconds;
        }

        /// <summary>
        /// Blast effect on one candidate. Returns final damage scale; the loadout may stop
        /// the fragments (trauma path via physiology is the caller's job).
        /// </summary>
        public static bool ApplyBlastMitigation(VEVE.Gear.GearLoadout loadout, float distanceM,
            float radiusM, float totalEnergyJ, VEVE.HitZone zone, float angleDeg,
            ref VEVE.Gear.GearMitigationResult mitigation)
        {
            float energy = BlastEnergyAtDistance(distanceM, radiusM, totalEnergyJ);
            if (energy <= 0f) return false;
            return VEVE.Gear.DamageableGearAdapter.TryMitigate(loadout, energy,
                VelocityEquivalentMps, zone, angleDeg, ref mitigation);
        }
    }
}
