using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Physical material response (extreme-realism scope): every authored surface
    /// derives gameplay-relevant physics from the SAME rules that drive visuals -
    /// wet surfaces get slippery AND reflective, hot ones shimmer, dust kills
    /// specular and raises IR signature. Pure statics, all testable.
    /// </summary>
    public static class MaterialPhysics
    {
        /// <summary>Friction multiplier from surface wetness (wet concrete is glass).</summary>
        public static float FrictionMultiplier(float baseFriction, float wetness01)
        {
            float w = Mathf.Clamp01(wetness01);
            float f = baseFriction * (1f - 0.55f * w);
            return f < 0.05f ? 0.05f : f;
        }

        /// <summary>Reflectance/specular boost with wetness (Fresnel-ish, saturates).</summary>
        public static float SpecularBoost(float baseGloss, float wetness01)
        {
            float w = Mathf.Clamp01(wetness01);
            return Mathf.Clamp01(baseGloss + (1f - baseGloss) * 0.7f * w);
        }

        /// <summary>IR emissivity shift: hot/dry surfaces glow in thermal, wet ones go dark.</summary>
        public static float IrSignature(float baseIr, float surfaceTempC, float wetness01)
        {
            float thermal = Mathf.Clamp01((surfaceTempC - 20f) / 40f);
            float ir = baseIr * (1f - 0.5f * Mathf.Clamp01(wetness01)) + 0.35f * thermal;
            return Mathf.Clamp01(ir);
        }

        /// <summary>Ballistic surface coupling: frozen ground reflects (less absorption),
        /// wet ground absorbs (mud eats blast energy).</summary>
        public static float BlastAbsorptionFactor(string surfaceKind, float surfaceTempC, float wetness01)
        {
            float tempFactor = Mathf.Clamp01((surfaceTempC + 10f) / 50f); // 0 = deep frost
            float baseAbs = surfaceKind == "Sand" ? 0.55f
                          : surfaceKind == "Concrete" ? 0.30f
                          : surfaceKind == "Metal" ? 0.15f
                          : 0.40f;
            // frozen: less absorption -> more spall/reflection; wet mud: more absorption
            float frostBonus = (1f - tempFactor) * 0.12f;
            float wetMud = Mathf.Clamp01(wetness01) * 0.18f;
            return Mathf.Clamp01(baseAbs + wetMud - frostBonus);
        }

        /// <summary>Sound absorption shift when wet (rain kills high-frequency propagation).</summary>
        public static float AcousticAbsorptionShift(float baseAbsorption, float wetness01)
        {
            return Mathf.Clamp01(baseAbsorption + 0.15f * Mathf.Clamp01(wetness01));
        }
    }
}
