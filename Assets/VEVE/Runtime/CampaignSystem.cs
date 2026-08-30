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

        private VEVE.Operators.OperatorLegacySystem _legacy;

        /// <summary>
        /// Lazily-created legacy ledger for realistic-mode succession (never serialized;
        /// persists through <c>Legacy.ToSaveString()</c> via the save pipeline seam).
        /// </summary>
        public VEVE.Operators.OperatorLegacySystem Legacy
        {
            get
            {
                if (_legacy == null) _legacy = new VEVE.Operators.OperatorLegacySystem();
                return _legacy;
            }
        }

        public bool HandleDeath()
        {
            if (deathMode == DeathMode.Test) return false;
            if (deathMode == DeathMode.Assisted)
            {
                activeOperator.stress = Mathf.Min(100f, activeOperator.stress + 25f);
                return false;
            }
            activeOperator.alive = false;
            TryCommissionSuccessor("killed in action");
            return true;
        }

        /// <summary>
        /// Records the current operator as KIA in the legacy ledger and replaces the active
        /// campaign operator with a mentorship-influenced successor. Proxy mapping is documented:
        /// the lightweight campaign profile carries no combat tallies, so service time is
        /// approximated from trainingLevel (~90 days/level) and killed-officer stats remain zero.
        /// </summary>
        /// <param name="cause">Cause-of-death text written to the memorial.</param>
        /// <returns>True when a successor was installed.</returns>
        public bool TryCommissionSuccessor(string cause)
        {
            VEVE.Operators.OperatorLegacySystem legacy = Legacy;
            if (legacy == null || activeOperator == null) return false;

            VEVE.Operators.OperatorProfile fallen = new VEVE.Operators.OperatorProfile
            {
                operatorId = activeOperator.callsign,
                callsign = activeOperator.callsign,
                familyId = activeOperator.callsign,
                defaultSpecialty = MapSpecialty(activeOperator.specialization),
                serviceDays = Mathf.Max(1, activeOperator.trainingLevel) * 90,
                missionsSurvived = Mathf.Max(0, activeOperator.trainingLevel) / 3,
                confirmedKills = 0
            };

            VEVE.Operators.KilledInActionRecord record = legacy.RecordKia(fallen, cause, DateTime.Now);
            VEVE.Operators.LegacyBonusResult bonus = VEVE.Operators.OperatorLegacySystem.ComputeLegacyBonus(record);

            VEVE.Operators.OperatorProfile successorBase = null;
            foreach (VEVE.Operators.OperatorProfile candidate in VEVE.Operators.OperatorProfile.CreateDefaultRoster())
            {
                successorBase = candidate;
                break;
            }
            if (successorBase == null) return false;

            successorBase.operatorId = activeOperator.callsign + "-S" + (legacy.LossCount);
            successorBase.callsign = activeOperator.callsign + " II";
            successorBase.familyId = record.familyId;
            successorBase.defaultSpecialty = fallen.defaultSpecialty;

            VEVE.Operators.OperatorProfile successor = VEVE.Operators.OperatorLegacySystem.ApplyTo(successorBase, bonus, fallen.defaultSpecialty);
            ReplaceOperator(new OperatorProfile
            {
                callsign = successor.callsign,
                specialization = successor.defaultSpecialty.ToString(),
                trainingLevel = 1 + Mathf.Max(0, successor.startingXpGrant) / 240,
                stress = 0f,
                alive = true
            });
            return true;
        }

        private static VEVE.Operators.OperatorSpecialty MapSpecialty(string specialization)
        {
            if (string.IsNullOrEmpty(specialization)) return VEVE.Operators.OperatorSpecialty.Pointman;
            if (System.Enum.TryParse(specialization, out VEVE.Operators.OperatorSpecialty match)) return match;
            switch (specialization.ToLowerInvariant())
            {
                case "medic": return VEVE.Operators.OperatorSpecialty.Medic;
                case "sniper":
                case "marksman": return VEVE.Operators.OperatorSpecialty.Marksman;
                case "support":
                case "autorifle": return VEVE.Operators.OperatorSpecialty.SupportGunner;
                case "recon":
                case "scout": return VEVE.Operators.OperatorSpecialty.Recon;
                case "comms":
                case "radio": return VEVE.Operators.OperatorSpecialty.Comms;
                case "breacher":
                case "shotgunner": return VEVE.Operators.OperatorSpecialty.Breacher;
                case "demo":
                case "demolitions": return VEVE.Operators.OperatorSpecialty.Demolitions;
                default: return VEVE.Operators.OperatorSpecialty.Pointman;
            }
        }

        public void ReplaceOperator(OperatorProfile replacement)
        {
            if (replacement == null || !replacement.alive) throw new ArgumentException("Replacement operator must be alive.", nameof(replacement));
            activeOperator = replacement;
        }
    }
}
