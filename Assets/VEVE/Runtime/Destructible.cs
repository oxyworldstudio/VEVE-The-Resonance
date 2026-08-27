using UnityEngine;

namespace VEVE
{
    public enum DestructionState { Intact, Damaged, Destroyed }

    /// <summary>
    /// Distruttibile causale: l'oggetto accumula danno balistico reale,
    /// degrada la propria protezione e cambia stato visibile/tattico.
    /// La distruzione è una conseguenza registrata nella persistenza missione.
    /// </summary>
    public sealed class Destructible : MonoBehaviour, IBallisticTarget
    {
        [SerializeField] private SurfaceMaterial material = SurfaceMaterial.Wood;
        [SerializeField, Min(0.01f)] private float thickness = 0.2f;
        [SerializeField, Min(1f)] private float integrity = 100f;
        [SerializeField] private string destructibleId;

        public DestructionState State { get; private set; } = DestructionState.Intact;
        public float Integrity => integrity;
        public float MaxIntegrity => maxIntegrity;
        public string Id => string.IsNullOrEmpty(destructibleId) ? name : destructibleId;

        private float maxIntegrity;

        private void Awake()
        {
            maxIntegrity = integrity;
            if (string.IsNullOrEmpty(destructibleId)) destructibleId = name;
        }

        /// <summary>
        /// Applica l'impatto al distruttibile. Ritorna true se il proiettile penetra (continua oltre),
        /// false se viene fermato. In entrambi i casi l'oggetto subisce erosione d'integrità.
        /// </summary>
        public bool AbsorbImpact(float incomingEnergy, out float remainingEnergy)
        {
            remainingEnergy = incomingEnergy;
            if (State == DestructionState.Destroyed) return true; // nulla più da fermare

            float effectiveThickness = CurrentThickness();
            BallisticImpact impact = Ballistics.ResolveImpact(incomingEnergy, material, effectiveThickness);
            float absorbedEnergy = impact.incomingEnergy - impact.remainingEnergy;
            integrity -= absorbedEnergy * 0.5f;
            remainingEnergy = impact.remainingEnergy;

            UpdateState();
            return impact.penetrated;
        }

        public float CurrentThickness()
        {
            return State == DestructionState.Destroyed ? 0.01f : thickness * (integrity / maxIntegrity);
        }

        private void UpdateState()
        {
            DestructionState previous = State;
            if (integrity <= maxIntegrity * 0.25f)
            {
                integrity = Mathf.Max(integrity, 0f);
                State = integrity <= 0.01f ? DestructionState.Destroyed : DestructionState.Damaged;
            }
            else if (integrity <= maxIntegrity * 0.6f)
            {
                State = DestructionState.Damaged;
            }

            if (State != previous && State == DestructionState.Destroyed)
            {
                MissionRuntime runtime = FindFirstObjectByType<MissionRuntime>();
                if (runtime != null) runtime.RecordEvent("destroyed:" + Id);
            }
        }
    }

    /// <summary>
    /// Contratto per superfici che assorbono energia balistica.
    /// Consente a Weapon di consumare coperture statiche e distruttibili in modo uniforme.
    /// </summary>
    public interface IBallisticTarget
    {
        bool AbsorbImpact(float incomingEnergy, out float remainingEnergy);
    }
}
