using UnityEngine;
using VEVE.AI;

namespace VEVE
{
    public sealed class EnemyAwareness : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float viewDistance = 25f;
        [SerializeField] private float viewAngle = 100f;
        [SerializeField] private float hearingScale = 0.8f;
        [SerializeField, Range(0f, 1f)] private float hearingAbsorption;
        [SerializeField, Min(0.02f)] private float perceptionInterval = 0.1f;
        [Tooltip("0..1 combat proficiency: drives time-to-acquire via TuningRules (reflex floor 0.62s, novice 1.5s).")]
        [Range(0f, 1f)]
        [SerializeField] private float aiSkill01 = 0.5f;
        private float acquisitionAccumulator;
        [Tooltip("Do not know gunfire positions perfectly: localize them with a bearing/range error cone.")]
        [SerializeField] private bool estimateNoise = true;
        [Tooltip("An AI that visually ENGAGES broadcasts the contact so nearby elements can converge.")]
        [SerializeField] private bool spreadCallouts = true;
        [SerializeField, Min(10f)] private float calloutRadius = 120f;
        private Vector3 lastKnownPosition;
        private float lastNoiseTime = -Mathf.Infinity;
        private float nextPerception;
        private Weapon targetWeapon;
        private EnvironmentSimulation env;
        public AwarenessState State { get; private set; } = AwarenessState.Patrol;

        private static event System.Action<Vector3, int> AllyContactReported;

        private void OnEnable()
        {
            TacticalSound.NoiseProduced += OnNoise;
            AllyContactReported += OnAllyCallout;
        }

        private void OnDisable()
        {
            TacticalSound.NoiseProduced -= OnNoise;
            AllyContactReported -= OnAllyCallout;
        }

        private void OnNoise(Vector3 position, float loudness)
        {
            float distance = Vector3.Distance(transform.position, position);
            float heardLoudness = SoundPropagation.HeardLoudness(loudness, distance, hearingAbsorption);
            if (heardLoudness * hearingScale >= 1f && Time.time > lastNoiseTime + 0.1f)
            {
                lastKnownPosition = estimateNoise
                    ? AiAcoustics.EstimateNoisePosition(transform.position, position, heardLoudness,
                        (uint)(GetInstanceID() ^ (uint)Time.frameCount))
                    : position;
                lastNoiseTime = Time.time;
                if (State != AwarenessState.Engaged) State = AwarenessState.Investigate;
            }
        }

        /// <summary>
        /// Radio callout relay: a contact reported by a friendly element converges this
        /// shooter onto the (slightly degraded) reported position instead of the true one -
        /// comms are never as good as eyes.
        /// </summary>
        private void OnAllyCallout(Vector3 reportedPosition, int reporterId)
        {
            if (!spreadCallouts || State == AwarenessState.Engaged) return;
            if (reporterId == GetInstanceID()) return;
            float dist = Vector3.Distance(transform.position, reportedPosition);
            if (dist > calloutRadius) return;
            uint seed = AiAcoustics.CalloutSeed(reporterId, reportedPosition);
            lastKnownPosition = AiAcoustics.EstimateNoisePosition(transform.position, reportedPosition,
                26f, seed);
            State = AwarenessState.Investigate;
        }

        private void BroadcastCallout(Vector3 contactPosition)
        {
            if (spreadCallouts) AllyContactReported?.Invoke(contactPosition, GetInstanceID());
        }

        private float GlintBonus()
        {
            if (target == null) return 0f;
            if (targetWeapon == null) targetWeapon = target.GetComponent<Weapon>();
            if (targetWeapon == null || string.IsNullOrEmpty(targetWeapon.MountedScopeId)) return 0f;
            if (env == null) env = UnityEngine.Object.FindFirstObjectByType<EnvironmentSimulation>();
            if (env == null) return 0f;
            float mag = 0f;
            if (VEVE.Content.ScopeCatalogSource.TryGetScoped(targetWeapon.MountedScopeId, out VEVE.WeaponCustomPro.ScopeProfile scope))
                mag = scope.magnificationMax;
            return AiAcoustics.ScopeGlintBonus(mag, env.SunElevation);
        }

        private void Update()
        {
            if (target == null) return;
            Vector3 delta = target.position - transform.position;
            if (Time.unscaledTime >= nextPerception)
            {
                nextPerception = Time.unscaledTime + perceptionInterval;
                UpdatePerception(delta);
            }
            if (lastKnownPosition != Vector3.zero)
            {
                transform.position = Vector3.MoveTowards(transform.position, lastKnownPosition, 1.2f * Time.deltaTime);
                if (Vector3.Distance(transform.position, lastKnownPosition) < 0.2f && State == AwarenessState.Investigate)
                    State = AwarenessState.Patrol;
            }
        }

        private void UpdatePerception(Vector3 delta)
        {
            if (delta.magnitude > viewDistance)
            {
                acquisitionAccumulator = 0f;
                return;
            }
            float angle = Vector3.Angle(transform.forward, delta.normalized);
            float halfAngle = viewAngle * 0.5f;
            if (angle > halfAngle)
            {
                acquisitionAccumulator = 0f;
                return;
            }

            if (Physics.Raycast(transform.position + Vector3.up * 1.6f, delta.normalized, out RaycastHit hit, viewDistance))
            {
                if (hit.transform == target)
                {
                    float distanceFactor = 1f - Mathf.Clamp01(delta.magnitude / viewDistance);
                    float angleFactor = 1f - Mathf.Clamp01(angle / halfAngle);
                    float detectionScore = distanceFactor * 0.6f + angleFactor * 0.4f + GlintBonus();
                    if (detectionScore > 0.4f)
                    {
                        acquisitionAccumulator += perceptionInterval;
                        if (State != AwarenessState.Engaged &&
                            acquisitionAccumulator >= TuningRules.TimeToAcquireSeconds(aiSkill01))
                        {
                            lastKnownPosition = target.position;
                            State = AwarenessState.Engaged;
                            BroadcastCallout(target.position);
                        }
                    }
                    else
                    {
                        acquisitionAccumulator = 0f;
                    }
                    return;
                }

                // Raycast blocked by an occluder: losing visual contact.
                if (State == AwarenessState.Engaged && delta.magnitude > viewDistance * 0.7f)
                {
                    State = AwarenessState.Investigate;
                    acquisitionAccumulator = 0f;
                }
            }
        }
    }
}
