using UnityEngine;
using VEVE.Realism;

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
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private float muzzleEnergy = 1448f;
        [SerializeField] private float damage = 35f;
        [SerializeField] private float fireRate = 0.07f;
        [SerializeField] private RealisticWeaponDefinition definition;
        [SerializeField, Range(0f, 1f)] private float fouling;
        [SerializeField, Range(0f, 1f)] private float wear;
        [SerializeField] private float recoilRecovery = 8f;
        [SerializeField] private float twistRate = 254f;
        [SerializeField] private float latitude = 0f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private RealismConfig realismConfig;
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
                twistRate = definition.twistRate;
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
            fouling = Mathf.Clamp01(fouling + (definition != null ? definition.foulingRate : 0.015f));
            recoil += definition != null ? definition.recoilImpulse : 0.8f;
            if (fouling + wear >= (definition != null ? definition.malfunctionThreshold : 1.25f)) { malfunctioned = true; return; }
            RaycastHit[] hits = Physics.RaycastAll(aimCamera.transform.position, aimCamera.transform.forward, 150f, hitMask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float remainingEnergy = muzzleEnergy;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform) continue;
                Damageable target = hit.collider.GetComponentInParent<Damageable>();
                remainingEnergy = Ballistics.EnergyAfterDistance(remainingEnergy, hit.distance, definition != null ? definition.ballisticCoefficient : 0.3f);
                Destructible destructible = hit.collider.GetComponent<Destructible>();
                bool absorbed;
                if (destructible != null)
                {
                    absorbed = destructible.AbsorbImpact(remainingEnergy, out remainingEnergy);
                }
                else
                {
                    CoverVolume cover = hit.collider.GetComponent<CoverVolume>();
                    SurfaceMaterial material;
                    float thickness;
                    if (cover != null)
                    {
                        material = cover.Material;
                        thickness = cover.Thickness;
                    }
                    else
                    {
                        Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
                        material = hitRenderer != null && hitRenderer.sharedMaterial != null && hitRenderer.sharedMaterial.name.Contains("Concrete")
                            ? SurfaceMaterial.Concrete : SurfaceMaterial.Wood;
                        thickness = 0.1f;
                    }
                    BallisticImpact impact = Ballistics.ResolveImpact(remainingEnergy, material, thickness, definition != null ? definition.bulletMass : 0.01f);
                    remainingEnergy = impact.remainingEnergy;
                    absorbed = impact.penetrated;
                }
                if (target != null && remainingEnergy >= 0f && absorbed)
                {
                    float energyRatio = Mathf.Clamp01(remainingEnergy / muzzleEnergy);
                    float appliedDamage = damage * energyRatio;
                    HitZone zone = hit.collider.name.ToLowerInvariant().Contains("head") ? HitZone.Head : HitZone.UpperTorso;
                    float bulletMass = definition != null ? definition.bulletMass : 0.01f;
                    VEVE.Gear.DamageableGearAdapter adapter = hit.collider.GetComponentInParent<VEVE.Gear.DamageableGearAdapter>();
                    if (adapter != null)
                    {
                        float impactVelocity = Mathf.Sqrt(2f * remainingEnergy / Mathf.Max(bulletMass, 0.0005f));
                        float impactAngle = Vector3.Angle(-hit.normal, aimCamera.transform.forward);
                        VEVE.Gear.GearMitigationResult mitigation = default;
                        if (adapter.MitigateHit(remainingEnergy, impactVelocity, zone, impactAngle, ref mitigation))
                        {
                            appliedDamage *= mitigation.damageScale;
                            if (mitigation.stopped)
                            {
                                target.GetComponent<Physiology>()?.ApplyWound(
                                    mitigation.traumaEnergyJoules * 0.02f, mitigation.traumaEnergyJoules * 0.05f);
                            }
                        }
                    }
                    target.ApplyDamage(appliedDamage, zone);
                    break;
                }
                if (!absorbed) break;
            }
        }

        public int RoundsRemaining => rounds;
        public bool IsMalfunctioned => malfunctioned;
        public float Recoil => recoil;
    }
}
