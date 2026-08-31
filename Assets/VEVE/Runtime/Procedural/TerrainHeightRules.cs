using System;

namespace VEVE.Procedural
{
    /// <summary>
    /// Pure deterministic terrain shaping per biome (integer-only fixed point so
    /// identical seeds generate identical grounds on any platform, no floating-point
    /// drift between clients). Reused by the layout evaluator, sprint costs, and
    /// prop scatter grounding.
    /// </summary>
    public static class TerrainHeightRules
    {
        public const int LatticeScale = 256;      // fixed-point sine domain
        public const int WaveAmplitude000 = 1000; // base swell, scaled per roughness 0..10

        /// <summary>Biome roughness 0..10 (town flat, desert dune swell, arctic crested).</summary>
        public static int RoughnessFor(string biomeKey)
        {
            switch ((biomeKey ?? string.Empty).ToUpperInvariant())
            {
                case "MEDIUM_TOWN": return 1;
                case "INDUSTRIAL_EAST": return 3;
                case "DESERT_CHECKPOINT": return 5;
                case "FOREST_VILLAGE": return 6;
                case "SUBARCTIC_COMPOUND": return 8;
                default: return 4;
            }
        }

        /// <summary>Relative amplitude in metres per biome crest profile.</summary>
        public static float AmplitudeMetres(string biomeKey)
        {
            int r = RoughnessFor(biomeKey);
            return 0.35f * (r + 1);
        }

        /// <summary>Integer fixed-point wave sum (two octaves of deterministic pseudo-sine).</summary>
        public static int HeightUnit(int x, int y, int seed, string biomeKey)
        {
            int rough = Math.Max(1, RoughnessFor(biomeKey));
            // first octave: broad ridge; second octave: rocky noise (both integer, no pow/sin)
            int a = Fold(x + seed * 11, LatticeScale) * rough / rough;
            int b = Fold((x + y * 2 + seed) * 7, LatticeScale) * (rough + 2) / 2;
            int h = a + b - WaveAmplitude000 / 2;
            return ClampSigned(h);
        }

        /// <summary>Height in metres [-amp, +amp], stable across runs and platforms.</summary>
        public static float HeightMeters(int x, int y, int seed, string biomeKey)
        {
            float amp = AmplitudeMetres(biomeKey);
            int max = WaveAmplitude000 / 2 + 200;
            float n = HeightUnit(x, y, seed, biomeKey) / (float)max;
            return n < -1f ? -amp : (n > 1f ? amp : n * amp);
        }

        /// <summary>Local normalized slope penalty: monotonic and capped (flat -> 1).</summary>
        public static float SlopeFactor(float dyPerMetre)
        {
            float a = Math.Abs(dyPerMetre);
            if (a < 0f || float.IsNaN(a) || float.IsInfinity(a)) return 1f;
            float f = 1f / (1f + a * a); // gentle: 0->1, 1->0.5, 2->0.2
            return f > 1f ? 1f : (f < 0.1f ? 0.1f : f);
        }

        static int Fold(int v, int scale)
        {
            unchecked
            {
                uint h = (uint)((long)(v % 1000000007) * 16777619L + (long)(v / 100003) * 2166136261L);
                h ^= (h << 13);
                h ^= (h >> 17);
                h ^= (h << 5);
                h += 0x9E3779B9u;
                return (int)(h % (uint)(scale + scale));
            }
        }

        static int ClampSigned(int v)
        {
            if (v > 6000) return 6000;
            if (v < -6000) return -6000;
            return v;
        }
    }
}
