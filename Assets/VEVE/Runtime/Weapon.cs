using UnityEngine;

namespace VEVE
{
    public static class TacticalSound
    {
        public static event System.Action<Vector3, float> NoiseProduced;

        public static void Emit(Vector3 position, float loudness)
        {
            NoiseProduced?.Invoke(position, loudness);
        }
    }

    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private int magazineSize = 10;
        [SerializeField] private float muzzleEnergy = 100f;
        [SerializeField] private float damage = 35f;
        [SerializeField] private float fireRate = 0.18f;
        [SerializeField] private WeaponDefinition definition;
        [SerializeField, Range(0f, 1f)] private float fouling;
        [SerializeField, Range(0f, 1f)] private float wear;
        [SerializeField] private float recoilRecovery = 8f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        private int rounds;
        private float nextShot;
        private float recoil;
        private bool malfunctioned;
        private Maintenance maintenance;

        private void Awake()
        {
            if (definition != null)
            {
                magazineSize = definition.magazineCapacity;
                muzzleEnergy = definition.muzzleEnergy;
                damage = definition.damage;
                fireRate = definition.fireInterval;
            }
            rounds = magazineSize;
            maintenance = GetComponent<Maintenance>();
        }

        private void Update()
        {
            recoil = Mathf.MoveTowards(recoil, 0f, recoilRecovery * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.R)) { rounds = magazineSize; malfunctioned = false; }
            if (!malfunctioned && Input.GetButton("Fire1") && Time.time >= nextShot) Fire();
        }

        private void Fire()
        {
            if (rounds <= 0) return;
            if (aimCamera == null)
            {
                Debug.LogError("Weapon requires an aim camera.", this);
                return;
            }
            rounds--;
            nextShot = Time.time + fireRate;
            TacticalSound.Emit(transform.position, 35f);
            if (maintenance != null) maintenance.UseShot();
            fouling = Mathf.Clamp01(fouling + 0.015f);
            recoil += definition != null ? definition.recoilImpulse : 0.8f;
            if (fouling + wear >= 1.25f) { malfunctioned = true; return; }
            RaycastHit[] hits = Physics.RaycastAll(aimCamera.transform.position, aimCamera.transform.forward, 150f, hitMask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float remainingEnergy = muzzleEnergy;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform) continue;
                Damageable target = hit.collider.GetComponentInParent<Damageable>();
                SurfaceMaterial material = hit.collider.sharedMaterial != null && hit.collider.sharedMaterial.name.Contains("Concrete")
                    ? SurfaceMaterial.Concrete : SurfaceMaterial.Wood;
                CoverVolume cover = hit.collider.GetComponent<CoverVolume>();
                float thickness = cover == null ? 0.1f : cover.Thickness;
                remainingEnergy = Ballistics.EnergyAfterDistance(remainingEnergy, hit.distance);
                BallisticImpact impact = Ballistics.ResolveImpact(remainingEnergy, material, thickness);
                remainingEnergy = impact.remainingEnergy;
                if (target != null && impact.incomingEnergy > 0f)
                {
                    float energyRatio = Mathf.Clamp01(impact.incomingEnergy / muzzleEnergy);
                    target.ApplyDamage(damage * energyRatio, hit.collider.name.ToLowerInvariant().Contains("head") ? HitZone.Head : HitZone.Torso);
                    break;
                }
                if (!impact.penetrated) break;
            }
        }

        public int RoundsRemaining => rounds;
        public bool IsMalfunctioned => malfunctioned;
        public float Recoil => recoil;
    }
}
