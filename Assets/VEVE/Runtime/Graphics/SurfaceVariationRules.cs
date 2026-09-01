using System;
using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Deterministic per-biome surface variation: hue/sat/val multipliers and
    /// roughness delta derived from (biomeKey, surfaceKind) via integer FNV-style
    /// hashing — same inputs always yield the same variation, no System.Random.
    /// Ranges are documented and clamped so variation stays plausible.
    /// </summary>
    public static class SurfaceVariationRules
    {
        public const float MaxHueShift = 0.04f;
        public const float MinSatMul = 0.85f;
        public const float MaxSatMul = 1.15f;
        public const float MinValMul = 0.85f;
        public const float MaxValMul = 1.15f;
        public const float MaxRoughDelta = 0.12f;

        public static uint Hash(string biomeKey, string surfaceKind)
        {
            unchecked
            {
                uint h = 2166136261u;
                string s = (biomeKey ?? string.Empty) + "\x1F" + (surfaceKind ?? string.Empty);
                for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 16777619u;
                return h;
            }
        }

        /// <summary>Hue shift in 0..1 hue units, signed within ±MaxHueShift.</summary>
        public static float HueShift(string biomeKey, string surfaceKind)
        {
            uint h = Hash(biomeKey, surfaceKind);
            float u = (h & 0xFFFFu) / 65535f;
            return (u * 2f - 1f) * MaxHueShift;
        }

        public static float SatMul(string biomeKey, string surfaceKind)
        {
            uint h = Hash(biomeKey, surfaceKind);
            float u = ((h >> 16) & 0xFFFFu) / 65535f;
            return MinSatMul + (MaxSatMul - MinSatMul) * u;
        }

        public static float ValMul(string biomeKey, string surfaceKind)
        {
            uint h = Hash(biomeKey, surfaceKind);
            float u = ((h >> 8) & 0xFFFFu) / 65535f;
            return MinValMul + (MaxValMul - MinValMul) * u;
        }

        public static float RoughDelta(string biomeKey, string surfaceKind)
        {
            uint h = Hash(biomeKey, surfaceKind);
            float u = ((h >> 4) & 0xFFFFu) / 65535f;
            return (u * 2f - 1f) * MaxRoughDelta;
        }

        /// <summary>Applies variation to a color in HSV space (clamped 0..1, hue wraps).</summary>
        public static Color ApplyVariation(Color baseColor, string biomeKey, string surfaceKind)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            h = Mathf.Repeat(h + HueShift(biomeKey, surfaceKind), 1f);
            s = Mathf.Clamp01(s * SatMul(biomeKey, surfaceKind));
            v = Mathf.Clamp01(v * ValMul(biomeKey, surfaceKind));
            Color c = Color.HSVToRGB(h, s, v);
            c.a = baseColor.a;
            return c;
        }

        /// <summary>Roughness after variation, clamped 0..1.</summary>
        public static float ApplyRoughVariation(float baseGloss, string biomeKey, string surfaceKind)
        {
            float g = baseGloss + RoughDelta(biomeKey, surfaceKind);
            return g < 0f ? 0f : (g > 1f ? 1f : g);
        }
    }
}
