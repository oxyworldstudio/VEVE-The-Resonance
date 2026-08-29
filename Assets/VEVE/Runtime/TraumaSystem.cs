using UnityEngine;

namespace VEVE
{
    public enum InjuryType { None, SuperficialWound, DeepLaceration, Fracture, GunshotWound, BluntTrauma, Burn, Shock, Hypothermia, HeatStroke }

    public readonly struct TraumaReport
    {
        public readonly float bloodLossRate;
        public readonly float painLevel;
        public readonly float mobilityLoss;
        public readonly float consciousnessRisk;
        public readonly float vitalSignsInstability;

        public TraumaReport(float bloodLossRate, float painLevel, float mobilityLoss, float consciousnessRisk, float vitalSignsInstability)
        {
            this.bloodLossRate = bloodLossRate;
            this.painLevel = painLevel;
            this.mobilityLoss = mobilityLoss;
            this.consciousnessRisk = consciousnessRisk;
            this.vitalSignsInstability = vitalSignsInstability;
        }
    }

    public static class TraumaSystem
    {
        public static TraumaReport EvaluateInjury(InjuryType injury, float severity, float bodyPart = 0f)
        {
            return injury switch
            {
                InjuryType.SuperficialWound => new TraumaReport(
                    bloodLossRate: severity * 0.05f,
                    painLevel: severity * 0.2f,
                    mobilityLoss: severity * 0.05f,
                    consciousnessRisk: 0f,
                    vitalSignsInstability: severity * 0.02f
                ),
                InjuryType.DeepLaceration => new TraumaReport(
                    bloodLossRate: severity * 0.25f,
                    painLevel: severity * 0.5f,
                    mobilityLoss: severity * 0.15f,
                    consciousnessRisk: severity * 0.05f,
                    vitalSignsInstability: severity * 0.1f
                ),
                InjuryType.Fracture => new TraumaReport(
                    bloodLossRate: severity * 0.08f,
                    painLevel: severity * 0.7f,
                    mobilityLoss: severity * 0.6f,
                    consciousnessRisk: severity * 0.1f,
                    vitalSignsInstability: severity * 0.15f
                ),
                InjuryType.GunshotWound => new TraumaReport(
                    bloodLossRate: severity * 0.8f,
                    painLevel: severity * 0.9f,
                    mobilityLoss: severity * 0.4f,
                    consciousnessRisk: severity * 0.4f,
                    vitalSignsInstability: severity * 0.5f
                ),
                InjuryType.BluntTrauma => new TraumaReport(
                    bloodLossRate: severity * 0.15f,
                    painLevel: severity * 0.6f,
                    mobilityLoss: severity * 0.3f,
                    consciousnessRisk: severity * 0.2f,
                    vitalSignsInstability: severity * 0.3f
                ),
                InjuryType.Burn => new TraumaReport(
                    bloodLossRate: severity * 0.1f,
                    painLevel: severity * 0.8f,
                    mobilityLoss: severity * 0.2f,
                    consciousnessRisk: severity * 0.15f,
                    vitalSignsInstability: severity * 0.2f
                ),
                InjuryType.Shock => new TraumaReport(
                    bloodLossRate: severity * 0.05f,
                    painLevel: severity * 0.4f,
                    mobilityLoss: severity * 0.1f,
                    consciousnessRisk: severity * 0.6f,
                    vitalSignsInstability: severity * 0.8f
                ),
                InjuryType.Hypothermia => new TraumaReport(
                    bloodLossRate: 0f,
                    painLevel: severity * 0.3f,
                    mobilityLoss: severity * 0.5f,
                    consciousnessRisk: severity * 0.5f,
                    vitalSignsInstability: severity * 0.7f
                ),
                InjuryType.HeatStroke => new TraumaReport(
                    bloodLossRate: 0f,
                    painLevel: severity * 0.5f,
                    mobilityLoss: severity * 0.4f,
                    consciousnessRisk: severity * 0.7f,
                    vitalSignsInstability: severity * 0.9f
                ),
                _ => new TraumaReport(0f, 0f, 0f, 0f, 0f),
            };
        }

        public static float CalculateShockLevel(float bloodLoss, float pain, float stress)
        {
            return Mathf.Clamp01((bloodLoss * 0.4f + pain * 0.35f + stress * 0.25f) / 100f);
        }
    }
}
