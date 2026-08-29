using UnityEngine;

namespace VEVE
{
    public enum AwarenessState { Patrol, Investigate, Engaged, Suppressed, Searching }

    public sealed class CoverVolume : MonoBehaviour
    {
        [SerializeField] private SurfaceMaterial material = SurfaceMaterial.Wood;
        [SerializeField, Min(0.01f)] private float thickness = 0.3f;
        [SerializeField] private bool isPenetrable = true;

        public SurfaceMaterial Material => material;
        public float Thickness => thickness;
        public bool IsPenetrable => isPenetrable;

        public bool Stops(float projectileEnergy, out float remainingEnergy)
        {
            if (!isPenetrable)
            {
                remainingEnergy = 0f;
                return true;
            }
            return !Ballistics.TryPenetrate(projectileEnergy, material, thickness, out remainingEnergy);
        }

        public float GetAcousticTransmission(float noiseIntensity)
        {
            float absorption = MaterialDefinition.GetAbsorption(material);
            return noiseIntensity * (1f - absorption) * Mathf.Exp(-thickness * 2f);
        }
    }

    public sealed class FieldMedic : MonoBehaviour
    {
        [SerializeField] private Physiology patient;
        [SerializeField, Min(0.1f)] private float treatmentDuration = 3f;
        [SerializeField] private float bleedingTreatment = 35f;
        [SerializeField] private float painTreatment = 20f;
        [SerializeField] private float consciousnessRecovery = 10f;
        private float treatmentRemaining;
        private bool treatmentComplete;

        public bool IsTreating => treatmentRemaining > 0f;
        public bool TreatmentComplete => treatmentComplete;

        public void BeginTreatment(Physiology target)
        {
            if (target == null || IsTreating) return;
            patient = target;
            treatmentRemaining = treatmentDuration;
            treatmentComplete = false;
        }

        private void Update()
        {
            if (!IsTreating) return;
            treatmentRemaining -= Time.deltaTime;
            if (treatmentRemaining <= 0f && patient != null && !treatmentComplete)
            {
                patient.Treat(bleedingTreatment, painTreatment);
                patient.ApplyConsciousnessRecovery(consciousnessRecovery);
                treatmentComplete = true;
                patient = null;
            }
        }
    }
}
