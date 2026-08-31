using UnityEngine;
using VEVE.WeaponCustomPro;

namespace VEVE.UI
{
    /// <summary>Published when the scope picture changes enough for a new holdover hint.</summary>
    public sealed class ScopeTelemetryEvent : VEVE.IEvent
    {
        public float distanceMeters;
        public float holdoverMoa;
    }

    /// <summary>
    /// Bridges the weapon's baked range card (battle zero + turret clicks) to a live
    /// holdover hint: raycasts the sight ray on a coarse interval and publishes
    /// ScopeTelemetryEvent so HUD elements (or future reticle overlays) can show
    /// "aim X MOA high/low" for the observed target - the real shooter's holds,
    /// diegetically, only when the optic is actually pointed at something within range.
    /// </summary>
    public sealed class ScopeTelemetryBridge : MonoBehaviour
    {
        [SerializeField] private float rayDistance = 500f;
        [SerializeField] private float sampleInterval = 0.15f;
        [SerializeField] private float publishEpsilonMoa = 0.05f;
        [SerializeField] private LayerMask sightMask = Physics.DefaultRaycastLayers;

        private Weapon weapon;
        private float timer;
        private float lastPublished = float.NaN;

        /// <summary>Current suggested aim-off in MOA (+ = hold high).</summary>
        public float HoldoverHintMoa { get; private set; }
        /// <summary>Slant range to the thing the optic currently covers.</summary>
        public float ObservedDistanceMeters { get; private set; }
        /// <summary>True when a weapon with a resolved range card drives the bridge.</summary>
        public bool Resolved => weapon != null && weapon.ActiveRangeCard != null;

        /// <summary>
        /// Pure hint math: card holdover at the observed slant range plus the dialed turret
        /// elevation (already in MOA). distance&lt;=0 or a null card yield no correction.
        /// </summary>
        public static float HintMoa(RangeCard card, double turretHoldoverMoa, double distanceMeters)
        {
            if (card == null || distanceMeters <= 0.0) return (float)turretHoldoverMoa;
            return (float)(ZeroingSystem.ComputeHoldoverMoa(card, distanceMeters) + turretHoldoverMoa);
        }

        private void OnEnable()
        {
            weapon = GetComponentInParent<Weapon>();
            if (weapon == null) weapon = GetComponent<Weapon>();
            LastEvent = null;
        }

        /// <summary>Last published event (debug/UI inspection without subscribing).</summary>
        public ScopeTelemetryEvent LastEvent { get; private set; }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = sampleInterval;

            if (weapon == null)
            {
                weapon = GetComponentInParent<Weapon>();
                if (weapon == null) return;
            }

            RangeCard card = weapon.ActiveRangeCard;
            var cam = Camera.main;
            if (card == null || cam == null)
            {
                HoldoverHintMoa = 0f;
                ObservedDistanceMeters = 0f;
                return;
            }

            float d = 0f;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit,
                    rayDistance, sightMask))
            {
                d = hit.distance;
                ObservedDistanceMeters = d;
            }
            else
            {
                ObservedDistanceMeters = 0f;
            }

            HoldoverHintMoa = d > 0f
                ? HintMoa(card, weapon.TurretHoldoverMoa, d)
                : (float)weapon.TurretHoldoverMoa;

            if (!float.IsNaN(lastPublished) && Mathf.Abs(HoldoverHintMoa - lastPublished) < publishEpsilonMoa)
                return;
            if (d <= 0f) return;

            lastPublished = HoldoverHintMoa;
            LastEvent = new ScopeTelemetryEvent
            {
                distanceMeters = d,
                holdoverMoa = HoldoverHintMoa
            };
            VEVE.EventBus.PublishGlobal(LastEvent);
        }
    }
}
