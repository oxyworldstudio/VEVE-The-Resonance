using UnityEngine;
using QualityLevel = VEVE.Realism.QualityLevel;

namespace VEVE.VFX
{
    /// <summary>
    /// Kind of persistent surface mark produced by impacts and blasts. Each kind
    /// owns its fade lifetime, scale range, rotation jitter and tint blend rule
    /// (see <see cref="DecalPoolRules"/>).
    /// </summary>
    public enum DecalKind
    {
        /// <summary>Small dark ring + hole left by a bullet impact.</summary>
        BulletHole = 0,
        /// <summary>Radial splat left by blood contact.</summary>
        BloodSplat = 1,
        /// <summary>Large blackened radial burn left by explosions.</summary>
        Scorch = 2,
        /// <summary>Light plaster/wood chunk spalled off by an impact.</summary>
        Chip = 3,
    }

    /// <summary>
    /// Pure deterministic rules for pooled surface decals: pool capacity per
    /// quality tier, fade lifetimes, scale ranges with integer-seed jitter
    /// (FNV-style hashing, never System.Random), rotation jitter and kind-aware
    /// tint blending against the physical surface palette. Every value is
    /// clamped and monotonic so the pool can never drift outside contract.
    /// </summary>
    public static class DecalPoolRules
    {
        /// <summary>FNV-1a 32-bit offset basis.</summary>
        private const uint FnvOffsetBasis = 2166136261u;
        /// <summary>FNV-1a 32-bit prime.</summary>
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// Pool capacity for a quality tier: Low 32, Medium 64, High 128,
        /// Ultra 256. Monotonic in the tier; unknown/out-of-range values clamp
        /// to the nearest defined bound.
        /// </summary>
        public static int CapacityFor(QualityLevel quality)
        {
            int q = (int)quality;
            if (q < (int)QualityLevel.Low) q = (int)QualityLevel.Low;
            if (q > (int)QualityLevel.Ultra) q = (int)QualityLevel.Ultra;
            return 32 << q;
        }

        /// <summary>
        /// Fade lifetime in seconds per kind. Documented ordering (longest to
        /// shortest): Scorch 45 &gt; BulletHole 20 &gt; BloodSplat 12 &gt; Chip 8.
        /// </summary>
        public static float FadeSecondsFor(DecalKind kind)
        {
            switch (kind)
            {
                case DecalKind.BulletHole: return 20f;
                case DecalKind.BloodSplat: return 12f;
                case DecalKind.Scorch: return 45f;
                case DecalKind.Chip: return 8f;
                default: return 20f;
            }
        }

        /// <summary>Smallest world-space quad edge (metres) for a kind.</summary>
        public static float MinScale(DecalKind kind)
        {
            switch (kind)
            {
                case DecalKind.BulletHole: return 0.05f;
                case DecalKind.BloodSplat: return 0.18f;
                case DecalKind.Scorch: return 0.80f;
                case DecalKind.Chip: return 0.06f;
                default: return 0.05f;
            }
        }

        /// <summary>Largest world-space quad edge (metres) for a kind.</summary>
        public static float MaxScale(DecalKind kind)
        {
            switch (kind)
            {
                case DecalKind.BulletHole: return 0.09f;
                case DecalKind.BloodSplat: return 0.42f;
                case DecalKind.Scorch: return 1.40f;
                case DecalKind.Chip: return 0.14f;
                default: return 0.09f;
            }
        }

        /// <summary>Mid-range scale of the kind (deterministic default when no jitter seed is wanted).</summary>
        public static float ScaleFor(DecalKind kind)
        {
            return (MinScale(kind) + MaxScale(kind)) * 0.5f;
        }

        /// <summary>
        /// Deterministic scale inside the kind's range for an integer seed:
        /// <see cref="Jitter01"/> drives a clamped lerp between
        /// <see cref="MinScale"/> and <see cref="MaxScale"/>. Same seed always
        /// yields the same scale; every result stays inside the range.
        /// </summary>
        public static float ScaleFor(DecalKind kind, int seed)
        {
            float min = MinScale(kind);
            float max = MaxScale(kind);
            float s = Mathf.Lerp(min, max, Jitter01(seed));
            return Mathf.Clamp(s, Mathf.Min(min, max), Mathf.Max(min, max));
        }

        /// <summary>
        /// Maximum rotation jitter in degrees applied around the surface normal:
        /// BulletHole 360 (round hole, free spin), BloodSplat 25, Scorch 15,
        /// Chip 90. Always &gt;= 0.
        /// </summary>
        public static float RotationJitterDeg(DecalKind kind)
        {
            switch (kind)
            {
                case DecalKind.BulletHole: return 360f;
                case DecalKind.BloodSplat: return 25f;
                case DecalKind.Scorch: return 15f;
                case DecalKind.Chip: return 90f;
                default: return 360f;
            }
        }

        /// <summary>Deterministic rotation offset in [0, <see cref="RotationJitterDeg"/>] for a seed.</summary>
        public static float RotationFor(DecalKind kind, int seed)
        {
            float span = RotationJitterDeg(kind);
            float t = Jitter01(seed ^ unchecked((int)0x9E3779B9));
            return Mathf.Clamp(span * t, 0f, span);
        }

        /// <summary>
        /// Tint blend rule against the hit surface's base albedo:
        /// <list type="bullet">
        /// <item>BulletHole darkens hard toward soot black (hole + carbon).</item>
        /// <item>BloodSplat darkens toward dried-blood red (never brighter than the surface).</item>
        /// <item>Scorch blackens almost fully toward carbon black.</item>
        /// <item>Chip lightens toward plaster gray (spalled fresh material).</item>
        /// </list>
        /// All channels are clamped to [0,1]; alpha is always 1 (shape lives in the texture alpha).
        /// </summary>
        public static Color ColorFor(DecalKind kind, Color baseSurfaceColor)
        {
            Color c;
            switch (kind)
            {
                case DecalKind.BulletHole:
                    // darken: crush the surface albedo, then pull toward soot
                    c = Color.Lerp(baseSurfaceColor * 0.35f, new Color(0.05f, 0.05f, 0.05f), 0.65f);
                    break;
                case DecalKind.BloodSplat:
                    // darken toward dried blood red, then absorb a little more light
                    c = Color.Lerp(baseSurfaceColor, new Color(0.30f, 0.02f, 0.02f), 0.78f) * 0.85f;
                    break;
                case DecalKind.Scorch:
                    // blacken: near-carbon, a whisper of the base remains
                    c = Color.Lerp(baseSurfaceColor, new Color(0.03f, 0.03f, 0.03f), 0.85f);
                    break;
                case DecalKind.Chip:
                default:
                    // lighten: fresh spalled material reads brighter than a weathered face
                    c = Color.Lerp(baseSurfaceColor, new Color(0.82f, 0.82f, 0.80f), 0.55f);
                    break;
            }
            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// Stable deterministic jitter in [0,1] from an integer seed. FNV-1a
        /// byte folding plus an integer avalanche step; no System.Random, no
        /// floating-point state, identical across runs and machines.
        /// </summary>
        public static float Jitter01(int seed)
        {
            uint h = FnvOffsetBasis;
            h ^= (byte)seed; h *= FnvPrime;
            h ^= (byte)(seed >> 8); h *= FnvPrime;
            h ^= (byte)(seed >> 16); h *= FnvPrime;
            h ^= (byte)(seed >> 24); h *= FnvPrime;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return (h & 0x00FFFFFFu) * (1f / 16777215f);
        }

        /// <summary>
        /// Per-instance seed for a decal kind and a monotonically increasing
        /// placement counter (FNV-style fold). Stable for the same counter,
        /// distinct across kinds, suitable input for <see cref="ScaleFor(DecalKind,int)"/>
        /// and <see cref="RotationFor(DecalKind,int)"/>.
        /// </summary>
        public static int InstanceSeed(DecalKind kind, int instanceIndex)
        {
            uint h = FnvOffsetBasis;
            h ^= (byte)kind; h *= FnvPrime;
            uint idx = unchecked((uint)instanceIndex);
            h ^= (byte)idx; h *= FnvPrime;
            h ^= (byte)(idx >> 8); h *= FnvPrime;
            h ^= (byte)(idx >> 16); h *= FnvPrime;
            h ^= (byte)(idx >> 24); h *= FnvPrime;
            h ^= h >> 15;
            return unchecked((int)h);
        }
    }
}
