using UnityEngine;
using System;
using VEVE.Realism;

namespace VEVE.RealisticPhysics
{
    [Serializable]
    public struct OpticalState
    {
        public float wavelength;
        public float refractiveIndex;
        public float dispersion;
        public float aberration;
        public float transmission;
    }

    [Serializable]
    public struct ReticleState
    {
        public Vector3 position;
        public Vector3 velocity;
        public float parallax;
        public float eyeRelief;
        public float focalPlane;
    }

    public static class OpticalPhysics
    {
        public static float CalculateSnellLaw(float incidentAngle, float n1, float n2)
        {
            float sinTheta2 = (n1 / n2) * Mathf.Sin(incidentAngle * Mathf.Deg2Rad);
            return Mathf.Asin(Mathf.Clamp(sinTheta2, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public static float CalculateChromaticAberration(float wavelength, float focalLength, float dispersion)
        {
            float focalLengthRed = focalLength / (1f + dispersion * (0.7f - wavelength));
            float focalLengthBlue = focalLength / (1f - dispersion * (wavelength - 0.4f));
            return Mathf.Abs(focalLengthRed - focalLengthBlue);
        }

        public static float CalculateReticleParallax(float sightHeight, float eyeRelief, float targetDistance)
        {
            float parallaxAngle = Mathf.Atan(sightHeight / (targetDistance + eyeRelief));
            return parallaxAngle * Mathf.Rad2Deg;
        }

        public static Vector3 CalculateLightScattering(Vector3 lightDirection, Vector3 viewDirection, float turbidity, float sunElevation)
        {
            float scatteringAngle = Vector3.Angle(lightDirection, viewDirection);
            float scatteringCoefficient = 0.0001f * turbidity * Mathf.Sin(scatteringAngle * Mathf.Deg2Rad);
            return lightDirection * scatteringCoefficient;
        }

        public static float CalculateMirageEffect(float temperatureGradient, float sightHeight, float targetDistance)
        {
            float refractiveIndexGradient = temperatureGradient * 0.0001f;
            float mirageAngle = refractiveIndexGradient * sightHeight * targetDistance * 0.01f;
            return Mathf.Clamp(mirageAngle, -5f, 5f);
        }

        public static float CalculateAtmosphericTransmittance(float distance, float airDensity, float wavelength)
        {
            float extinctionCoefficient = 0.0001f * airDensity / (wavelength * wavelength);
            return Mathf.Exp(-extinctionCoefficient * distance);
        }

        public static float CalculateGlare(float luminance, float angle, float pupilDiameter)
        {
            float glareFactor = Mathf.Pow(Mathf.Cos(angle * Mathf.Deg2Rad), 2f);
            float pupilArea = Mathf.PI * (pupilDiameter * 0.5f) * (pupilDiameter * 0.5f);
            return luminance * glareFactor * pupilArea * 0.01f;
        }

        public static float CalculateDepthOfField(float aperture, float focalLength, float subjectDistance, float circleOfConfusion)
        {
            float hyperfocal = (focalLength * focalLength) / (aperture * circleOfConfusion) + focalLength;
            float nearLimit = (hyperfocal * subjectDistance) / (hyperfocal + (subjectDistance - focalLength));
            float farLimit = subjectDistance < hyperfocal ? (hyperfocal * subjectDistance) / (hyperfocal - (subjectDistance - focalLength)) : float.MaxValue;
            return farLimit - nearLimit;
        }

        public static float CalculateLightIntensity(float luminousFlux, float distance, float attenuation)
        {
            return luminousFlux / (4f * Mathf.PI * distance * distance * attenuation);
        }

        public static float CalculateLensTransmission(float lensQuality, float wavelength, float angleOfIncidence)
        {
            float transmissionLoss = (1f - lensQuality) * 0.1f;
            float wavelengthLoss = Mathf.Exp(-Mathf.Abs(wavelength - 0.55f) * 2f);
            float angleLoss = Mathf.Cos(angleOfIncidence * Mathf.Deg2Rad);
            return Mathf.Clamp01(1f - transmissionLoss) * wavelengthLoss * angleLoss;
        }
    }
}
