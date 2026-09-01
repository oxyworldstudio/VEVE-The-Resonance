using UnityEngine;

namespace VEVE.VFX
{
    /// <summary>
    /// Deterministic procedural decal textures (no binary assets): one cached
    /// 64x64 RGBA texture per <see cref="DecalKind"/> whose SHAPE lives in the
    /// alpha channel (RGB stays white so the runtime tint carries all colour).
    /// All pixel math is closed-form radius/angle evaluation plus FNV-style
    /// integer hash noise - System.Random is never used, so repeated builds
    /// produce byte-identical pixels.
    /// </summary>
    public static class DecalTextureFactory
    {
        /// <summary>Texture edge length in pixels (all kinds share it).</summary>
        public const int TextureSize = 64;

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private static readonly Texture2D[] Cache = new Texture2D[4];

        /// <summary>
        /// Returns the cached alpha-shape texture for a kind, building it
        /// deterministically on first request. Never null.
        /// </summary>
        public static Texture2D GetTextureFor(DecalKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= Cache.Length) i = 0;
            if (Cache[i] == null) Cache[i] = Build((DecalKind)i);
            return Cache[i];
        }

        /// <summary>
        /// Destroys and forgets every cached texture (test hook). In edit mode
        /// the destruction is immediate; in play mode it is deferred to end of
        /// frame. Subsequent <see cref="GetTextureFor"/> calls rebuild identical
        /// pixels by construction.
        /// </summary>
        public static void Clear()
        {
            for (int i = 0; i < Cache.Length; i++)
            {
                if (Cache[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(Cache[i]);
                else Object.DestroyImmediate(Cache[i]);
                Cache[i] = null;
            }
        }

        private static Texture2D Build(DecalKind kind)
        {
            Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "VEVE_Decal_" + kind,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = (x + 0.5f) / TextureSize * 2f - 1f;
                    float v = (y + 0.5f) / TextureSize * 2f - 1f;
                    float a = Mathf.Clamp01(AlphaFor(kind, u, v, x, y));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        /// <summary>Closed-form alpha coverage for one pixel of one kind.</summary>
        private static float AlphaFor(DecalKind kind, float u, float v, int px, int py)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float n = Noise01(kind, px, py, 0);
            switch (kind)
            {
                case DecalKind.BulletHole:
                {
                    // dark punched hole with a ragged soot ring and sparse radial rays
                    if (r < 0.20f) return 1f;
                    float ring = 1f - Mathf.Clamp01((r - 0.20f) / 0.34f);
                    float ragged = ring > 0.12f ? ring * (0.55f + 0.45f * n) : 0f;
                    float ang = Mathf.Atan2(v, u);
                    float spokes = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 12f);
                    float ray = spokes * Mathf.Clamp01(1.1f - r) * (0.35f + 0.65f * n) * 0.85f;
                    return Mathf.Max(ragged, ray);
                }
                case DecalKind.BloodSplat:
                {
                    // soft radial core plus hash-driven satellites at the rim
                    float core = Mathf.Clamp01((0.55f - r) / 0.42f);
                    float sat = n > 0.72f ? (n - 0.72f) / 0.28f * Mathf.Clamp01(1.25f - r) : 0f;
                    return Mathf.Max(core, sat);
                }
                case DecalKind.Scorch:
                {
                    // wide burn: hard carbon core, noisy charred falloff at the rim
                    float a = Mathf.Clamp01((1.05f - r) / 0.78f);
                    if (r > 0.45f) a *= 0.70f + 0.30f * n;
                    if (r < 0.30f) a = 1f;
                    return a;
                }
                case DecalKind.Chip:
                default:
                {
                    // angular chunk: 16 hash-seeded facets carve an irregular crater rim
                    float ang = Mathf.Atan2(v, u);
                    float norm = (ang + Mathf.PI) / (2f * Mathf.PI);
                    int bucket = Mathf.Clamp((int)(norm * 16f), 0, 15);
                    float th = 0.34f + 0.22f * Noise01(kind, bucket, 7, 1);
                    return Mathf.Clamp01((th - r) * 8f);
                }
            }
        }

        /// <summary>Deterministic FNV-style hash noise in [0,1] for integer pixel coordinates.</summary>
        private static float Noise01(DecalKind kind, int a, int b, int salt)
        {
            uint h = FnvOffsetBasis;
            h ^= (byte)kind; h *= FnvPrime;
            h ^= (byte)salt; h *= FnvPrime;
            h ^= (byte)a; h *= FnvPrime;
            h ^= (byte)(a >> 8); h *= FnvPrime;
            h ^= (byte)b; h *= FnvPrime;
            h ^= (byte)(b >> 8); h *= FnvPrime;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return (h & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
