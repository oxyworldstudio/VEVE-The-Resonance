using UnityEngine;

namespace VEVE
{
    [CreateAssetMenu(menuName = "VEVE/Simulation/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Min(1)] public int magazineCapacity = 10;
        [Min(0.01f)] public float muzzleEnergy = 100f;
        [Min(0.01f)] public float damage = 35f;
        [Min(0.01f)] public float fireInterval = 0.18f;
        [Min(0f)] public float recoilImpulse = 0.8f;
        [Min(0f)] public float weaponMass = 3.2f;
    }
}
