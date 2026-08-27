using System;
using UnityEngine;

namespace VEVE
{
    public enum DeathMode { Test, Assisted, Realistic }

    [Serializable]
    public sealed class OperatorProfile
    {
        public string callsign;
        public string specialization;
        public int trainingLevel;
        public float stress;
        public bool alive = true;
    }

    public sealed class CampaignState : MonoBehaviour
    {
        [SerializeField] private DeathMode deathMode = DeathMode.Test;
        [SerializeField] private OperatorProfile activeOperator = new OperatorProfile
        {
            callsign = "VEVE-01",
            specialization = "Rifleman",
            trainingLevel = 1
        };

        public DeathMode CurrentDeathMode => deathMode;
        public OperatorProfile ActiveOperator => activeOperator;

        public bool HandleDeath()
        {
            if (deathMode == DeathMode.Test) return false;
            if (deathMode == DeathMode.Assisted)
            {
                activeOperator.stress = Mathf.Min(100f, activeOperator.stress + 25f);
                return false;
            }
            activeOperator.alive = false;
            return true;
        }

        public void ReplaceOperator(OperatorProfile replacement)
        {
            if (replacement == null || !replacement.alive) throw new ArgumentException("Replacement operator must be alive.", nameof(replacement));
            activeOperator = replacement;
        }
    }
}
