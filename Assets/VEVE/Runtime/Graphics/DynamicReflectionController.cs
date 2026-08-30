using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace VEVE.Graphics
{
    /// <summary>
    /// Runtime ReflectionProbe orchestrator for the built-in render pipeline.
    /// <para>
    /// Probes are registered explicitly via <see cref="RegisterProbe"/> or auto-discovered from the scene on
    /// Start. Each probe carries a logical zone id (world XZ cell); the controller keeps a per-zone list,
    /// tracks the player's active zone, and refreshes only the probes within <see cref="ActivationRadius"/>
    /// of the focus point, budgeted to at most <see cref="MaxProbesPerRenderCycle"/> real-time probe renders
    /// spaced by <see cref="RenderIntervalFrames"/> frames (default: 1 probe / 2 frames) so probe cost is
    /// amortized instead of spiking.
    /// </para>
    /// <para>
    /// Weather coupling: the controller polls <see cref="VEVE.EnvironmentSimulation"/> (if a component is
    /// present — discovered lazily, never hard-required) and drives
    /// <c>RenderSettings.ambientIntensity</c> / <c>RenderSettings.reflectionIntensity</c> from
    /// <c>CurrentWeather</c>, <c>SunElevation</c> and <c>VisibilityRange</c>, smoothed over time.
    /// Runtime-only: no editor APIs, every reference null-guarded.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class DynamicReflectionController : MonoBehaviour
    {
        [Header("Zones")]
        [Tooltip("World-space size of one reflection zone cell on X and Z.")]
        [SerializeField] private float zoneSize = 64f;

        [Tooltip("Only probes within this radius of the focus transform participate in the render cycle.")]
        [SerializeField] private float activationRadius = 96f;

        [Header("Render Budget")]
        [Tooltip("Minimum frames between two RenderProbe() calls (1 probe every N frames).")]
        [SerializeField, Range(1, 8)] private int renderIntervalFrames = 2;

        [Tooltip("Maximum real-time probes refreshed per zone change before the cycle idles.")]
        [SerializeField, Range(1, 8)] private int maxProbesPerRenderCycle = 4;

        [Tooltip("Resolution applied to managed real-time probes (square pixels).")]
        [SerializeField] private int probeResolution = 128;

        [Header("Weather Coupling")]
        [Tooltip("Seconds between EnvironmentSimulation polls. The simulation is optional; absence only disables weather response.")]
        [SerializeField] private float weatherPollInterval = 0.5f;

        [Tooltip("Exponent for day/night ambient falloff from sun elevation (higher = darker night).")]
        [SerializeField] private float nightFalloffExponent = 1.6f;

        [Tooltip("Smoothing speed for environment intensity changes.")]
        [SerializeField] private float intensitySmoothing = 2f;

        private sealed class ProbeRecord
        {
            public ReflectionProbe Probe;
            public Vector3 Position;
        }

        private readonly Dictionary<Vector2Int, List<ProbeRecord>> zones = new Dictionary<Vector2Int, List<ProbeRecord>>();
        private readonly List<ProbeRecord> pendingRenders = new List<ProbeRecord>();
        private VEVE.EnvironmentSimulation environment;
        private Transform focus;
        private Vector2Int currentZone = new Vector2Int(int.MinValue, int.MinValue);
        private int lastRenderFrame = int.MinValue;
        private float nextWeatherPoll;
        private float currentAmbientIntensity = 1f;
        private float targetAmbientIntensity = 1f;
        private float currentReflectionIntensity = 1f;
        private float targetReflectionIntensity = 1f;

        /// <summary>World-space edge length of a zone cell (clamped 8..512 on set).</summary>
        public float ZoneSize
        {
            get => zoneSize;
            set => zoneSize = Mathf.Clamp(value, 8f, 512f);
        }

        /// <summary>Radius in meters around the focus point used to select active probes.</summary>
        public float ActivationRadius
        {
            get => activationRadius;
            set => activationRadius = Mathf.Clamp(value, 4f, 512f);
        }

        /// <summary>Frames between budgeted probe renders (clamped 1..8 on set).</summary>
        public int RenderIntervalFrames
        {
            get => renderIntervalFrames;
            set => renderIntervalFrames = Mathf.Clamp(value, 1, 8);
        }

        /// <summary>Focus transform tracking the player. Falls back to Camera.main when unset.</summary>
        public Transform Focus
        {
            get => focus;
            set => focus = value;
        }

        /// <summary>Number of probes currently registered across all zones.</summary>
        public int RegisteredProbeCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<Vector2Int, List<ProbeRecord>> zone in zones)
                {
                    count += zone.Value.Count;
                }

                return count;
            }
        }

        private void Awake()
        {
            AutoDiscoverProbes();
        }

        private void Update()
        {
            Transform tracked = ResolveFocus();
            if (tracked == null)
            {
                return;
            }

            Vector2Int zone = ZoneOf(tracked.position);
            if (zone != currentZone)
            {
                currentZone = zone;
                RebuildRenderQueue(tracked.position);
            }

            PumpBudgetedRenders();
            PollEnvironment();
        }

        /// <summary>
        /// Registers a probe with its owning zone. Safe to call during scene streaming; duplicates are ignored.
        /// </summary>
        /// <param name="probe">Probe to manage. Ignored when null or already destroyed.</param>
        public void RegisterProbe(ReflectionProbe probe)
        {
            if (probe == null)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, List<ProbeRecord>> zone in zones)
            {
                for (int i = 0; i < zone.Value.Count; i++)
                {
                    if (zone.Value[i].Probe == probe)
                    {
                        return;
                    }
                }
            }

            ProbeRecord record = new ProbeRecord
            {
                Probe = probe,
                Position = probe.transform.position
            };

            Vector2Int key = ZoneOf(record.Position);
            List<ProbeRecord> list;
            if (!zones.TryGetValue(key, out list))
            {
                list = new List<ProbeRecord>();
                zones[key] = list;
            }

            list.Add(record);
        }

        /// <summary>
        /// Removes a probe from management (e.g. zone teardown during streaming).
        /// </summary>
        /// <param name="probe">Probe to unregister. Ignored when null.</param>
        public void UnregisterProbe(ReflectionProbe probe)
        {
            if (probe == null)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, List<ProbeRecord>> zone in zones)
            {
                for (int i = zone.Value.Count - 1; i >= 0; i--)
                {
                    if (zone.Value[i].Probe == probe)
                    {
                        zone.Value.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Forces an immediate (still budget-capped) refresh of probes around <paramref name="worldPosition"/>.
        /// </summary>
        /// <param name="worldPosition">Center of the refresh volume.</param>
        public void ForceZoneRefresh(Vector3 worldPosition)
        {
            currentZone = ZoneOf(worldPosition);
            RebuildRenderQueue(worldPosition);
        }

        private void AutoDiscoverProbes()
        {
            ReflectionProbe[] probes = FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            for (int i = 0; i < probes.Length; i++)
            {
                RegisterProbe(probes[i]);
            }
        }

        private Transform ResolveFocus()
        {
            if (focus != null)
            {
                return focus;
            }

            Camera camera = Camera.main;
            return camera != null ? camera.transform : null;
        }

        private Vector2Int ZoneOf(Vector3 position)
        {
            float size = Mathf.Max(8f, zoneSize);
            return new Vector2Int(Mathf.FloorToInt(position.x / size), Mathf.FloorToInt(position.z / size));
        }

        private void RebuildRenderQueue(Vector3 focusPosition)
        {
            pendingRenders.Clear();
            float radiusSqr = activationRadius * activationRadius;

            List<ProbeRecord> candidates = new List<ProbeRecord>();
            foreach (KeyValuePair<Vector2Int, List<ProbeRecord>> zone in zones)
            {
                for (int i = 0; i < zone.Value.Count; i++)
                {
                    ProbeRecord record = zone.Value[i];
                    if (record.Probe == null)
                    {
                        continue;
                    }

                    if ((record.Position - focusPosition).sqrMagnitude <= radiusSqr)
                    {
                        candidates.Add(record);
                    }
                }
            }

            candidates.Sort((a, b) => (a.Position - focusPosition).sqrMagnitude.CompareTo((b.Position - focusPosition).sqrMagnitude));
            int take = Mathf.Min(maxProbesPerRenderCycle, candidates.Count);
            for (int i = 0; i < take; i++)
            {
                ProbeRecord record = candidates[i];
                record.Probe.resolution = Mathf.Max(16, probeResolution);
                pendingRenders.Add(record);
            }

            currentReflectionIntensity = RenderSettings.reflectionIntensity;
            if (pendingRenders.Count > 0 && Time.frameCount - lastRenderFrame >= renderIntervalFrames)
            {
                lastRenderFrame = Time.frameCount - renderIntervalFrames;
            }
        }

        private void PumpBudgetedRenders()
        {
            if (pendingRenders.Count == 0)
            {
                return;
            }

            if (Time.frameCount - lastRenderFrame < renderIntervalFrames)
            {
                return;
            }

            lastRenderFrame = Time.frameCount;
            ProbeRecord record = pendingRenders[0];
            pendingRenders.RemoveAt(0);

            if (record.Probe == null || record.Probe.gameObject.scene.IsValid() == false)
            {
                return;
            }

            if (record.Probe.mode == ReflectionProbeMode.Realtime)
            {
                record.Probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                record.Probe.RenderProbe();
            }
        }

        private void PollEnvironment()
        {
            if (Time.unscaledTime < nextWeatherPoll)
            {
                return;
            }

            nextWeatherPoll = Time.unscaledTime + Mathf.Max(0.1f, weatherPollInterval);

            if (environment == null)
            {
                environment = FindFirstObjectCompatible<VEVE.EnvironmentSimulation>();
                if (environment == null)
                {
                    targetAmbientIntensity = RenderSettings.ambientIntensity;
                    return;
                }
            }

            targetAmbientIntensity = ComputeAmbientMultiplier();
            targetReflectionIntensity = ComputeReflectionMultiplier();
        }

        private T FindFirstObjectCompatible<T>() where T : Behaviour
        {
            T[] all = FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].isActiveAndEnabled)
                {
                    return all[i];
                }
            }

            return all.Length > 0 ? all[0] : null;
        }

        private float ComputeAmbientMultiplier()
        {
            float dayNight = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01((environment.SunElevation + 6f) / 66f), Mathf.Max(0.4f, nightFalloffExponent)));
            float weatherFactor = WeatherAmbientFactor(environment.CurrentWeather);
            float precipitation = Mathf.Clamp01(environment.PrecipitationIntensity);
            return Mathf.Lerp(0.25f, 1f, dayNight) * weatherFactor * (1f - 0.35f * precipitation);
        }

        private float ComputeReflectionMultiplier()
        {
            switch (environment.CurrentWeather)
            {
                case VEVE.WeatherState.Clear: return 1f;
                case VEVE.WeatherState.Overcast: return 0.65f;
                case VEVE.WeatherState.Rain: return 0.45f;
                case VEVE.WeatherState.Fog: return 0.3f;
                case VEVE.WeatherState.Snow: return 0.5f;
                case VEVE.WeatherState.Thunderstorm: return 0.4f;
                default: return 1f;
            }
        }

        private static float WeatherAmbientFactor(VEVE.WeatherState weather)
        {
            switch (weather)
            {
                case VEVE.WeatherState.Clear: return 1f;
                case VEVE.WeatherState.Overcast: return 0.8f;
                case VEVE.WeatherState.Rain: return 0.6f;
                case VEVE.WeatherState.Fog: return 0.75f;
                case VEVE.WeatherState.Snow: return 0.85f;
                case VEVE.WeatherState.Thunderstorm: return 0.5f;
                default: return 1f;
            }
        }

        private void LateUpdate()
        {
            float step = Mathf.Clamp01(Time.deltaTime * Mathf.Max(0f, intensitySmoothing));
            currentAmbientIntensity = Mathf.Lerp(currentAmbientIntensity, targetAmbientIntensity, step);
            currentReflectionIntensity = Mathf.Lerp(currentReflectionIntensity, targetReflectionIntensity, step);
            RenderSettings.ambientIntensity = currentAmbientIntensity;
            RenderSettings.reflectionIntensity = currentReflectionIntensity;
        }

        private void OnDisable()
        {
            pendingRenders.Clear();
        }
    }
}
