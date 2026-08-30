using UnityEngine;
using VEVE.Realism;
using VEVE.Catalog;
using VEVE.WeaponCustomPro;

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
        [SerializeField] private WeaponInstanceIdentity identity;
        private int rounds;
        private float nextShot;
        private float recoil;
        private bool malfunctioned;
        private Maintenance maintenance;
        private VEVE.Operators.OperatorInstance @operator;
        private RangeCard card;
        private double turretMoa;

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
            @operator = GetComponentInParent<VEVE.Operators.OperatorInstance>();
            if (@operator != null && identity == null) identity = @operator.Identity;
            ResolveRangeCard();
        }

        /// <summary>
        /// Bakes the battle-zero range card when a catalogued weapon id can be resolved — either
        /// from the serialized identity, or from a definition whose weapon name matches a catalog
        /// id or display name — and the definition carries zeroing geometry. Null leaves the fire
        /// loop on the pure line-of-sight path.
        /// </summary>
        private void ResolveRangeCard()
        {
            card = null;
            turretMoa = 0.0;
            string weaponId = identity != null && IconicWeaponCatalog.TryGet(identity.weaponId, out _)
                ? identity.weaponId
                : (definition != null ? ResolveCatalogIdByName(definition.weaponName) : null);
            if (weaponId == null || definition == null || definition.zeroRange <= 0f) return;
            if (!ZeroingSystem.TryComputeCard(weaponId, definition.zeroRange, definition.sightHeight * 1000f, out RangeCard computed)) return;
            card = computed;
            turretMoa = identity != null ? identity.zeroClicksElevation * card.ClickValueMoa : 0.0;
        }

        private static string ResolveCatalogIdByName(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return null;
            foreach (WeaponSpec spec in IconicWeaponCatalog.All)
            {
                if (string.Equals(spec.id, weaponName, System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(spec.displayName, weaponName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return spec.id;
                }
            }
            return null;
        }

        private void Update()
        {
            float recoveryScale = @operator != null ? @operator.SwayRecoveryMultiplier : 1f;
            recoil = Mathf.MoveTowards(recoil, 0f, recoilRecovery * Mathf.Max(0.25f, recoveryScale) * Time.deltaTime);
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
            for (int i = 0; i < hits.Length; i++)
            {
                hits[i] = ApplyZeroingHoldover(hits[i]);
            }
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

        /// <summary>
        /// Shifts a raw line-of-sight hit onto the actual round path implied by the zeroed
        /// trajectory at that distance: <see cref="ZeroingSystem.ComputeHoldoverMoa(RangeCard,double)"/>
        /// is an aim-side angle (+ = aim above the target), so the bullet path deviates by the
        /// negated angle about the camera right axis, then the corrected ray is re-cast once and
        /// its hit is used for damage. Neutral when no range card was resolved.
        /// </summary>
        private RaycastHit ApplyZeroingHoldover(RaycastHit raw)
        {
            if (card == null || aimCamera == null) return raw;
            double holdoverMoa = ZeroingSystem.ComputeHoldoverMoa(card, raw.distance) + turretMoa;
            if (holdoverMoa == 0.0) return raw;
            float angleRad = (float)(-holdoverMoa * System.Math.PI / 10800.0);
            Vector3 direction = Quaternion.AngleAxis(angleRad, aimCamera.transform.right) * aimCamera.transform.forward;
            if (Physics.Raycast(aimCamera.transform.position, direction, out RaycastHit corrected, 150f, hitMask)
                && !corrected.collider.transform.IsChildOf(transform))
            {
                return corrected;
            }
            return raw;
        }

        public int RoundsRemaining => rounds;
        public bool IsMalfunctioned => malfunctioned;
        public float Recoil => recoil;
    }
}
