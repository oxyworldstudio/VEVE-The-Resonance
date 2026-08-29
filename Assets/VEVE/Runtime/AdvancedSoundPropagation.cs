using UnityEngine;

namespace VEVE
{
    public static class AdvancedSoundPropagation
    {
        public static float CalculateTransmission(float sourceIntensity, float distance, MaterialPreset material, float thickness)
        {
            float absorption = RealisticMaterialLibrary.GetPresetAcousticAbsorption(material);
            float distanceAttenuation = 1.0f / (1.0f + distance * distance * 0.004f);
            float thicknessAttenuation = Mathf.Exp(-thickness * (1.0f - absorption) * 3f);
            return sourceIntensity * distanceAttenuation * thicknessAttenuation * (1.0f - absorption);
        }

        public static float CalculateReflection(Vector3 sourcePos, Vector3 listenerPos, Vector3 surfaceNormal, float surfaceAbsorption)
        {
            Vector3 toListener = (listenerPos - sourcePos).normalized;
            float angle = Vector3.Angle(toListener, surfaceNormal);
            float reflection = Mathf.Cos(angle * Mathf.Deg2Rad) * (1.0f - surfaceAbsorption);
            return Mathf.Max(0f, reflection);
        }

        public static float CalculateReverberation(float roomVolume, float surfaceAbsorption, float distance)
        {
            float rt60 = 0.161f * roomVolume / (surfaceAbsorption * 4f + 0.05f);
            float distanceAttenuation = 1.0f / (1.0f + distance * 0.1f);
            return distanceAttenuation * Mathf.Exp(-rt60 * 0.5f);
        }
    }
}
