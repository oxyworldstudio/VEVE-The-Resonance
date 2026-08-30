using UnityEngine;

namespace VEVE
{
    [CreateAssetMenu(menuName = "VEVE/Simulation/Realistic Weapon Definition")]
    public sealed class RealisticWeaponDefinition : ScriptableObject
    {
        [Header("General")]
        public string weaponName;
        public string manufacturer;
        public float weaponMass = 3.2f;

        [Header("Ballistics")]
        public float muzzleVelocity = 850f;
        public float bulletMass = 0.004f;
        public float ballisticCoefficient = 0.28f;
        public float twistRate = 254f;
        public float barrelLength = 368f;

        [Header("Ammunition")]
        public int magazineCapacity = 30;
        public float muzzleEnergy = 1448f;
        public float damage = 35f;
        public float effectiveRange = 500f;
        public float maximumRange = 3500f;
        public float fireInterval = 0.07f;
        public float reloadTime = 2.5f;

        [Header("Recoil & Handling")]
        public float recoilImpulse = 0.8f;
        public float recoilRecovery = 8f;
        public float sightHeight = 0.08f;
        public float zeroRange = 100f;

        [Header("Wear & Maintenance")]
        public float foulingRate = 0.015f;
        public float wearRate = 0.005f;
        public float malfunctionThreshold = 1.25f;

        public float KineticEnergy(float velocity)
        {
            return 0.5f * bulletMass * velocity * velocity;
        }

        public float TimeOfFlight(float distance)
        {
            return distance / muzzleVelocity;
        }

        public float Drop(float distance)
        {
            float time = TimeOfFlight(distance);
            return 0.5f * 9.80665f * time * time;
        }
    }
}
