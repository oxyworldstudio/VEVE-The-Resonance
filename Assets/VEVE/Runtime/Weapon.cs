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
        [SerializeField] private VEVE.Customization.WeaponCustomizationManager customization;
        private string catalogWeaponId;
        private string mountedScopeId;
        private float mountedClickMoa;
        private string lastQueriedOpticId;
        private float opticPollTimer;
        private RangeCard card;
        private double turretMoa;
        [SerializeField, HideInInspector] private int reserveRounds;
        private float reloadUntilTime = -1f;

        /// <summary>Rounds currently waiting in the fielded reserve.</summary>
        public int ReserveRounds => reserveRounds;
        /// <summary>True while a reload timer is blocking the action.</summary>
        public bool IsReloading => Time.time < reloadUntilTime;
        /// <summary>0..1 progress of the in-flight reload (1 when idle).</summary>
        public float ReloadProgress01
        {
            get
            {
                if (!IsReloading) return 1f;
                float total = reloadUntilTime - reloadStartTime;
                return total > 0f ? Mathf.Clamp01((Time.time - reloadStartTime) / total) : 1f;
            }
        }

        private float reloadStartTime;

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
            reserveRounds = VEVE.Combat.AmmunitionModel.StartReserve(magazineSize);
            maintenance = GetComponent<Maintenance>();
            @operator = GetComponentInParent<VEVE.Operators.OperatorInstance>();
            if (@operator != null && identity == null) identity = @operator.Identity;
            ResolveRangeCard();
        }

        /// <summary>
        /// Bakes the battle-zero range card. Sight height uses the MOUNTED optic's real
        /// bore-to-centerline (C3) when the weapon has a ScopeCatalog optic equipped via the
        /// customization manager; otherwise the definition's iron-sight height. Click value
        /// for the dialled turret comes from the optic (0-click red dots fall back to the card).
        /// </summary>
        private void ResolveRangeCard()
        {
            card = null;
            turretMoa = 0.0;
            mountedScopeId = null;
            mountedClickMoa = 0f;
            if (customization == null && WeaponCustomizationHost.Instance != null)
                customization = WeaponCustomizationHost.Instance.Customization;
            string weaponId = identity != null && IconicWeaponCatalog.TryGet(identity.weaponId, out _)
                ? identity.weaponId
                : (definition != null ? ResolveCatalogIdByName(definition.weaponName) : null);
            catalogWeaponId = weaponId;
            if (weaponId == null || definition == null || definition.zeroRange <= 0f) return;

            float sightMm = definition.sightHeight * 1000f;
            ScopeProfile mounted = null;
            if (customization != null && OpticCatalogBridge.TryGetMounted(customization, weaponId, out mounted))
            {
                mountedScopeId = mounted.id;
                if (mounted.boreToOpticCenterlineMm > 0f) sightMm = mounted.boreToOpticCenterlineMm;
                mountedClickMoa = mounted.elevationClickMoa;
            }

            if (!ZeroingSystem.TryComputeCard(weaponId, definition.zeroRange, sightMm, out RangeCard computed)) return;
            card = computed;
            double clickValue = mountedClickMoa > 0f ? mountedClickMoa : card.ClickValueMoa;
            turretMoa = identity != null ? identity.zeroClicksElevation * clickValue : 0.0;
            if (customization != null) lastQueriedOpticId = OpticCatalogBridge.MountedOpticId(customization, weaponId);

            VEVE.EventBus.PublishGlobal(new OpticMountedEvent
            {
                weaponId = weaponId,
                scopeId = mountedScopeId,
                fovDegAtMinZoom = mounted != null ? mounted.fovDegAtMinZoom : 0f,
                elevationClickMoa = mountedClickMoa
            });
        }

        /// <summary>Wires the zero card after Awake and whenever the optic mount changes.</summary>
        [ContextMenu("Rebuild Range Card")]
        public void RebuildRangeCard()
        {
            ResolveRangeCard();
        }

        private void PollOpticMount()
        {
            if (customization == null && WeaponCustomizationHost.Instance != null)
                customization = WeaponCustomizationHost.Instance.Customization;
            if (customization == null || string.IsNullOrEmpty(catalogWeaponId)) return;
            string now = OpticCatalogBridge.MountedOpticId(customization, catalogWeaponId);
            if (string.Equals(now, lastQueriedOpticId, System.StringComparison.Ordinal)) return;
            RebuildRangeCard();
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
            bool wasReloading = IsReloading;
            if (wasReloading && Time.time >= reloadUntilTime)
            {
                // Finish the reload: transfer was already applied at request time.
            }
            if (Input.GetKeyDown(KeyCode.R) && !wasReloading) BeginFullReload();
            if (Input.GetKeyDown(KeyCode.T) && !wasReloading) BeginTacticalReload();
            opticPollTimer -= Time.unscaledDeltaTime;
            if (opticPollTimer <= 0f)
            {
                opticPollTimer = 0.5f;
                PollOpticMount();
            }
            if (!malfunctioned && !wasReloading && Input.GetButton("Fire1") && Time.time >= nextShot) Fire();
        }

        /// <summary>
        /// Dry/full reload: keeps current magazine in the gun conceptually (spent), tops up to
        /// capacity from the reserve, clears a malfunction (stopping a stoppage) at extra time cost.
        /// </summary>
        private void BeginFullReload()
        {
            float baseReload = definition != null ? definition.reloadTime : 2.6f;
            float speedMult = @operator != null ? @operator.ReloadSpeedMultiplier : 1f;
            float seconds = roundsInMagazineAtReloadStart() == 0
                ? VEVE.Combat.AmmunitionModel.DryReloadSeconds(baseReload, speedMult)
                : VEVE.Combat.AmmunitionModel.FullReloadSeconds(baseReload, speedMult);
            if (malfunctioned)
            {
                seconds += 1.2f;
                malfunctioned = false;
            }
            int transferred = VEVE.Combat.AmmunitionModel.TransferForReload(rounds, magazineSize, reserveRounds, out int newReserve);
            if (transferred <= 0) return;
            reserveRounds = newReserve;
            rounds += transferred;
            reloadStartTime = Time.time;
            reloadUntilTime = Time.time + seconds;
            TacticalSound.Emit(transform.position, 12f);
        }

        /// <summary>Tactical swap: spent partial magazine discarded, full one from reserve (whole tube paid).</summary>
        private void BeginTacticalReload()
        {
            if (rounds >= magazineSize || reserveRounds < magazineSize) return;
            VEVE.Combat.AmmunitionModel.TacticalTransfer(rounds, magazineSize, reserveRounds, out int roundsAfter, out int newReserve);
            float baseReload = definition != null ? definition.reloadTime : 2.6f;
            float speedMult = @operator != null ? @operator.ReloadSpeedMultiplier : 1f;
            reloadStartTime = Time.time;
            reloadUntilTime = Time.time + VEVE.Combat.AmmunitionModel.TacticalReloadSeconds(baseReload, speedMult);
            reserveRounds = newReserve;
            rounds = roundsAfter;
            TacticalSound.Emit(transform.position, 12f);
        }

        private int roundsInMagazineAtReloadStart()
        {
            return rounds;
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
            bool onTarget = false;
            bool civilianHarm = false;
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
                    onTarget = true;
                    if (hit.collider.GetComponentInParent<VEVE.Agentic.CivilianAgent>() != null)
                    {
                        civilianHarm = true;
                    }
                    else if (VEVE.Catalog.FamilyXpLedger.Default != null)
                    {
                        VEVE.Catalog.FamilyXpLedger.Default.Grant(OwnerClientId, FamilyKey,
                            FamilyXpLedger.XpPerHitOnTarget);
                    }
                    break;
                }
                if (!absorbed) break;
            }
            RecordPrediction(onTarget);
            VEVE.EventBus.PublishGlobal(new VEVE.Content.ShotResolvedEvent
            {
                onTarget = onTarget,
                civilianHarm = civilianHarm,
                predictedTick = predictedTick,
                predictedOwner = owner
            });
        }

        /// <summary>Local shot predictions for lag-comp reconciliation (bounded ring).</summary>
        public static readonly VEVE.Net.ShotReplayWindow Predictions = new VEVE.Net.ShotReplayWindow(192);
        private int predictedTick;
        private ulong owner;

        private void RecordPrediction(bool hit)
        {
            owner = OwnerClientId != 0 ? OwnerClientId : VEVE.Net.LagCompRules.OfflineOwner;
            predictedTick = Time.frameCount;
            if (OwnerClientId == 0) return; // offline host: reconciliation is a session concern
            Predictions.Mark(new VEVE.Net.ShotPrediction
            {
                tick = predictedTick,
                owner = OwnerClientId,
                localHit = hit,
                distanceM = 0f
            });
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
        /// <summary>Baked battle-zero card (null when no catalogued weapon resolved).</summary>
        public RangeCard ActiveRangeCard => card;
        /// <summary>Turret elevation offset already dialled into the scope, in MOA.</summary>
        public double TurretHoldoverMoa => turretMoa;
        public float ZeroRangeMeters => definition != null ? definition.zeroRange : 0f;
        public float SightHeightMeters => definition != null ? definition.sightHeight : 0f;
        /// <summary>Session owner for proficiency attribution; 0 when offline/unowned so single-player never bypasses the existing progression pipeline.</summary>
        public ulong OwnerClientId { get; set; }
        /// <summary>
        /// Family attribution key: definition name normalized, else catalog id / generic. Stable
        /// with the proficiency system family string contract (lowercase platform/weapon name).
        /// </summary>
        public string FamilyKey
        {
            get
            {
                if (definition != null && !string.IsNullOrEmpty(definition.weaponName))
                    return definition.weaponName.ToLowerInvariant();
                return string.IsNullOrEmpty(catalogWeaponId) ? "generic" : catalogWeaponId.ToLowerInvariant();
            }
        }
        /// <summary>Catalog id resolved for this weapon (C3 optic mounting key).</summary>
        public string CatalogWeaponId => catalogWeaponId;
        /// <summary>ScopeCatalog id of the mounted optic (null for iron sights / red dots not in scope catalog).</summary>
        public string MountedScopeId => mountedScopeId;
        /// <summary>Elevation click value in MOA of the mounted optic (0 when none).</summary>
        public float MountedClickMoa => mountedClickMoa;
    }
}
