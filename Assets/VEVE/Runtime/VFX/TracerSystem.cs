using UnityEngine;
using VEVE;

namespace VEVE.VFX
{
    /// <summary>
    /// Configuration for tracer round trail visualization.
    /// </summary>
    [System.Serializable]
    public struct TracerConfig
    {
        [Header("Trail Settings")]
        public float trailWidth;
        public float trailLength;
        public AnimationCurve widthCurve;
        public Color startColor;
        public Color endColor;

        [Header("Burn")]
        public float burnTime;
        public Gradient temperatureGradient;

        [Header("Fade")]
        public float maxVisibleDistance;
        public float fadeStartDistance;
    }

    /// <summary>
    /// Tracer round visualization with trail renderer, color temperature based on burn time,
    /// and distance-based fade.
    /// </summary>
    public sealed class TracerSystem : MonoBehaviour
    {
        [Header("Caliber Trails")]
        [SerializeField] private TracerConfig rifleTracer;
        [SerializeField] private TracerConfig machineGunTracer;
        [SerializeField] private TracerConfig heavyTracer;

        [Header("Pooling")]
        [SerializeField, Min(1)] private int trailPoolSize = 64;

        private TrailRenderer[] trailPool;
        private Transform trailRoot;

        private void Awake()
        {
            trailRoot = new GameObject("TracerTrails").transform;
            trailRoot.SetParent(transform, false);

            InitializePool();
        }

        private void InitializePool()
        {
            trailPool = new TrailRenderer[trailPoolSize];
            for (int i = 0; i < trailPoolSize; i++)
            {
                GameObject go = new GameObject($"Tracer_{i}");
                go.transform.SetParent(trailRoot, false);
                go.SetActive(false);

                TrailRenderer trail = go.AddComponent<TrailRenderer>();
                trailPool[i] = trail;
                ConfigureTrail(trail, rifleTracer);
            }
        }

        /// <summary>
        /// Fires a tracer round visualization from origin to destination.
        /// </summary>
        /// <param name="origin">Muzzle position.</param>
        /// <param name="destination">Impact or end position.</param>
        /// <param name="caliber">Tracer caliber type.</param>
        /// <param name="burnTime">Remaining burn time for color temperature.</param>
        public void FireTracer(Vector3 origin, Vector3 destination, TracerCaliber caliber, float burnTime)
        {
            TracerConfig config = caliber switch
            {
                TracerCaliber.Rifle => rifleTracer,
                TracerCaliber.MachineGun => machineGunTracer,
                TracerCaliber.Heavy => heavyTracer,
                _ => rifleTracer
            };

            TrailRenderer trail = GetPooledTrail();
            if (trail == null) return;

            float distance = Vector3.Distance(origin, destination);
            float temperatureFactor = Mathf.Clamp01(burnTime / config.burnTime);

            trail.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(destination - origin));
            trail.Clear();
            trail.gameObject.SetActive(true);

            ConfigureTrail(trail, config);
            ApplyTemperatureColor(trail, config, temperatureFactor);
            ApplyDistanceFade(trail, config, distance);

            trail.time = distance / 1000f;
            trail.emitting = true;

            StartCoroutine(DisableTrailAfterTime(trail, distance / 1000f + 0.1f));
        }

        private void ConfigureTrail(TrailRenderer trail, TracerConfig config)
        {
            trail.widthCurve = config.widthCurve;
            trail.widthMultiplier = config.trailWidth;
            trail.time = config.trailLength / 1000f;
            trail.minVertexDistance = 0.1f;
            trail.autodestruct = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void ApplyTemperatureColor(TrailRenderer trail, TracerConfig config, float temperatureFactor)
        {
            if (config.temperatureGradient != null)
            {
                trail.colorGradient = config.temperatureGradient;
            }
            else
            {
                Color hot = Color.Lerp(config.startColor, Color.white, temperatureFactor);
                Color cool = Color.Lerp(config.endColor, Color.black, 1f - temperatureFactor);

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(hot, 0f),
                        new GradientColorKey(cool, 1f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.2f, 1f)
                    }
                );
                trail.colorGradient = gradient;
            }
        }

        private void ApplyDistanceFade(TrailRenderer trail, TracerConfig config, float distance)
        {
            float fadeFactor = distance > config.fadeStartDistance
                ? Mathf.Clamp01(1f - (distance - config.fadeStartDistance) / (config.maxVisibleDistance - config.fadeStartDistance))
                : 1f;

            Color startColor = trail.colorGradient.Evaluate(0f);
            Color endColor = trail.colorGradient.Evaluate(1f);

            startColor.a *= fadeFactor;
            endColor.a *= fadeFactor * 0.5f;

            Gradient fadedGradient = trail.colorGradient;
            fadedGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(fadedGradient.colorKeys[0].color, 0f),
                    new GradientColorKey(fadedGradient.colorKeys[1].color, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(endColor.a, 1f)
                }
            );
            trail.colorGradient = fadedGradient;
        }

        private TrailRenderer GetPooledTrail()
        {
            for (int i = 0; i < trailPool.Length; i++)
            {
                if (trailPool[i] != null && !trailPool[i].emitting)
                {
                    return trailPool[i];
                }
            }
            return null;
        }

        private System.Collections.IEnumerator DisableTrailAfterTime(TrailRenderer trail, float time)
        {
            yield return new WaitForSeconds(time);
            trail.emitting = false;
            trail.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Tracer caliber categories for trail visualization.
    /// </summary>
    public enum TracerCaliber
    {
        Rifle,
        MachineGun,
        Heavy
    }
}
