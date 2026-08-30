using System;
using UnityEngine;
using VEVE;

namespace VEVE.RealisticPhysics
{
    /// <summary>
    /// Double-ray ground contact probe. Casts one ray down from the configured foot offset and a
    /// second ray from the transform origin to disambiguate stairs, slopes and floating geometry,
    /// classifies the struck surface by renderer name via <see cref="PhysicsMaterialDatabase"/>,
    /// and caches the result for a fixed refresh window (default 0.08 s) so callers can poll at
    /// frame rate without paying raycast cost every frame.
    /// </summary>
    public sealed class GroundContactProbe : MonoBehaviour
    {
        [SerializeField] private float feetOffset = 0.1f;
        [SerializeField] private float maxProbeDistance = 0.6f;
        [SerializeField] private float groundTolerance = 0.02f;
        [SerializeField] private float refreshIntervalSeconds = 0.08f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private SurfaceMaterial fallbackMaterial = SurfaceMaterial.Dirt;
        [SerializeField] private OperatorPosture stance = OperatorPosture.Standing;
        [SerializeField] private float standingHeight = 1.75f;

        private Vector3 cachedNormal = Vector3.up;
        private Vector3 cachedPoint;
        private string cachedMaterialName = string.Empty;
        private SurfaceMaterial cachedMaterial;
        private float cachedSlopeAngle;
        private bool cachedGrounded;
        private float nextRefreshTime;
        private bool hasCache;

        /// <summary>Raised after each cache refresh that changes the grounded state.</summary>
        public event Action<bool> OnGroundStateChanged;

        /// <summary>True when the cached probe result reports solid ground beneath the rig.</summary>
        public bool IsGrounded
        {
            get
            {
                RefreshIfStale();
                return cachedGrounded;
            }
        }

        /// <summary>World-space surface normal of the last detected ground hit (up when airborne).</summary>
        public Vector3 GroundNormal
        {
            get
            {
                RefreshIfStale();
                return cachedNormal;
            }
        }

        /// <summary>World-space point of the last detected ground hit.</summary>
        public Vector3 GroundPoint
        {
            get
            {
                RefreshIfStale();
                return cachedPoint;
            }
        }

        /// <summary>Renderer name of the last struck surface, or empty while airborne.</summary>
        public string SurfaceMaterialName
        {
            get
            {
                RefreshIfStale();
                return cachedMaterialName;
            }
        }

        /// <summary>Classified surface material of the last ground hit.</summary>
        public SurfaceMaterial SurfaceMaterial
        {
            get
            {
                RefreshIfStale();
                return cachedMaterial;
            }
        }

        /// <summary>Physical profile (friction, restitution, impedance, density) of the current surface.</summary>
        public SurfaceMaterialProfile SurfaceProfile
        {
            get
            {
                RefreshIfStale();
                return PhysicsMaterialDatabase.GetProfile(cachedMaterial);
            }
        }

        /// <summary>Slope of the ground plane in degrees away from vertical-up (0 on flat floor).</summary>
        public float SlopeAngle
        {
            get
            {
                RefreshIfStale();
                return cachedSlopeAngle;
            }
        }

        /// <summary>Current eye/head anchor height for the active stance, in metres.</summary>
        public float StanceHeight
        {
            get => ComputeStanceHeight(stance, standingHeight);
            set
            {
                standingHeight = Mathf.Max(0.1f, value);
                stance = OperatorPosture.Standing;
            }
        }

        /// <summary>Active stance reported by the probe rig.</summary>
        public OperatorPosture Stance => stance;

        /// <summary>
        /// Sets the stance used for <see cref="StanceHeight"/> reporting.
        /// </summary>
        /// <param name="newStance">Stance to adopt.</param>
        public void SetStance(OperatorPosture newStance)
        {
            stance = newStance;
        }

        /// <summary>
        /// Forces an immediate cache refresh regardless of the refresh window.
        /// </summary>
        /// <returns>True when ground was detected.</returns>
        public bool ProbeNow()
        {
            PerformProbe();
            hasCache = true;
            nextRefreshTime = Time.time + Mathf.Max(0.001f, refreshIntervalSeconds);
            return cachedGrounded;
        }

        /// <summary>
        /// Pure stance-height model: crouch and prone collapse the standing eye height by their factors.
        /// </summary>
        /// <param name="posture">Stance to evaluate.</param>
        /// <param name="standingHeight">Standing anchor height in metres.</param>
        /// <returns>Anchor height in metres for the given stance.</returns>
        public static float ComputeStanceHeight(OperatorPosture posture, float standingHeight)
        {
            float height = Mathf.Max(0f, standingHeight);
            switch (posture)
            {
                case OperatorPosture.Crouched: return height * 0.55f;
                case OperatorPosture.Prone: return height * 0.25f;
                default: return height;
            }
        }

        private void RefreshIfStale()
        {
            if (hasCache && Time.time < nextRefreshTime) return;

            bool previous = cachedGrounded;
            PerformProbe();
            hasCache = true;
            nextRefreshTime = Time.time + Mathf.Max(0.001f, refreshIntervalSeconds);
            if (previous != cachedGrounded)
            {
                OnGroundStateChanged?.Invoke(cachedGrounded);
            }
        }

        private void PerformProbe()
        {
            Vector3 origin = transform.position;
            Vector3 feetOrigin = origin + Vector3.up * feetOffset;

            bool feetHit = Physics.Raycast(feetOrigin, Vector3.down, out RaycastHit feetInfo, maxProbeDistance, groundMask, QueryTriggerInteraction.Ignore);
            bool bodyHit = Physics.Raycast(origin, Vector3.down, out RaycastHit bodyInfo, maxProbeDistance + feetOffset, groundMask, QueryTriggerInteraction.Ignore);

            if (!feetHit && !bodyHit)
            {
                cachedGrounded = false;
                cachedNormal = Vector3.up;
                cachedPoint = origin;
                cachedMaterialName = string.Empty;
                cachedMaterial = fallbackMaterial;
                cachedSlopeAngle = 0f;
                return;
            }

            RaycastHit primary = feetHit ? feetInfo : bodyInfo;
            Vector3 normal = primary.normal;

            if (feetHit && bodyHit)
            {
                float footClearance = feetOrigin.y - feetInfo.point.y;
                float bodyClearance = origin.y - bodyInfo.point.y;
                float discrepancy = Mathf.Abs(footClearance - bodyClearance);
                normal = discrepancy > groundTolerance
                    ? Vector3.Normalize(feetInfo.normal + bodyInfo.normal)
                    : feetInfo.normal;
            }

            cachedGrounded = true;
            cachedPoint = primary.point;
            cachedNormal = normal;
            cachedMaterialName = GetHitSurfaceName(primary);
            cachedMaterial = PhysicsMaterialDatabase.ClassifyByName(cachedMaterialName, fallbackMaterial);
            cachedSlopeAngle = Vector3.Angle(Vector3.up, normal);
        }

        private static string GetHitSurfaceName(RaycastHit hit)
        {
            if (hit.collider == null) return string.Empty;
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            return renderer != null ? renderer.name : hit.collider.name;
        }

        private void Reset()
        {
            groundMask = ~0;
        }
    }
}
