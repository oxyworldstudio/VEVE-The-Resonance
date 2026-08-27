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
        private Vector3 lastKnownPosition;
        private float lastNoiseTime = -Mathf.Infinity;
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
            if (delta.magnitude > viewDistance || Vector3.Angle(transform.forward, delta) > viewAngle * 0.5f) return;
            if (Physics.Raycast(transform.position + Vector3.up, delta.normalized, out RaycastHit hit, viewDistance) &&
                hit.transform == target)
            {
                lastKnownPosition = target.position;
                State = AwarenessState.Engaged;
            }
            if (lastKnownPosition != Vector3.zero)
            {
                transform.position = Vector3.MoveTowards(transform.position, lastKnownPosition, 1.2f * Time.deltaTime);
                if (Vector3.Distance(transform.position, lastKnownPosition) < 0.2f && State == AwarenessState.Investigate)
                    State = AwarenessState.Patrol;
            }
        }
    }
}
