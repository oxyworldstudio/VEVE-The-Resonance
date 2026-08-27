using UnityEngine;

namespace VEVE
{
    public enum AwarenessState { Patrol, Investigate, Engaged, Suppressed }

    public sealed class CoverVolume : MonoBehaviour
    {
        [SerializeField] private SurfaceMaterial material = SurfaceMaterial.Wood;
        [SerializeField, Min(0.01f)] private float thickness = 0.3f;

        public bool Stops(float projectileEnergy, out float remainingEnergy)
        {
            return !Ballistics.TryPenetrate(projectileEnergy, material, thickness, out remainingEnergy);
        }

        public float Thickness => thickness;
    }

    public sealed class FieldMedic : MonoBehaviour
    {
        [SerializeField] private Physiology patient;
        [SerializeField, Min(0.1f)] private float treatmentDuration = 3f;
        private float treatmentRemaining;

        public bool IsTreating => treatmentRemaining > 0f;

        public void BeginTreatment(Physiology target)
        {
            if (target == null || IsTreating) return;
            patient = target;
            treatmentRemaining = treatmentDuration;
        }

        private void Update()
        {
            if (!IsTreating) return;
            treatmentRemaining -= Time.deltaTime;
            if (treatmentRemaining <= 0f && patient != null)
            {
                patient.Treat(35f, 20f);
                patient = null;
            }
        }
    }
}
