using UnityEngine;

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
        private Vector3 lastKnownPosition;
        private float lastNoiseTime = -Mathf.Infinity;
        private float nextPerception;
        public AwarenessState State { get; private set; } = AwarenessState.Patrol;

        private void OnEnable() => TacticalSound.NoiseProduced += OnNoise;
        private void OnDisable() => TacticalSound.NoiseProduced -= OnNoise;

        private void OnNoise(Vector3 position, float loudness)
        {
            float distance = Vector3.Distance(transform.position, position);
            float heardLoudness = SoundPropagation.HeardLoudness(loudness, distance, hearingAbsorption);
            if (heardLoudness * hearingScale >= 1f && Time.time > lastNoiseTime + 0.1f)
            {
                lastKnownPosition = position;
                lastNoiseTime = Time.time;
                State = AwarenessState.Investigate;
            }
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
        if (delta.magnitude > viewDistance) return;
        float angle = Vector3.Angle(transform.forward, delta.normalized);
        float halfAngle = viewAngle * 0.5f;
        if (angle > halfAngle) return;

        if (Physics.Raycast(transform.position + Vector3.up * 1.6f, delta.normalized, out RaycastHit hit, viewDistance))
        {
            if (hit.transform == target)
            {
                float distanceFactor = 1f - Mathf.Clamp01(delta.magnitude / viewDistance);
                float angleFactor = 1f - Mathf.Clamp01(angle / halfAngle);
                float detectionScore = distanceFactor * 0.6f + angleFactor * 0.4f;
                if (detectionScore > 0.4f)
                {
                    lastKnownPosition = target.position;
                    State = AwarenessState.Engaged;
                }
            }
            else
            {
                if (State == AwarenessState.Engaged && delta.magnitude > viewDistance * 0.7f)
                {
                    State = AwarenessState.Investigate;
                }
            }
        }
    }
    }
}
