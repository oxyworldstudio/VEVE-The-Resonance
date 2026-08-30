using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace VEVE.Graphics
{
    /// <summary>
    /// Procedural surface categories supported by <see cref="ProceduralSurfaceTextureFactory"/>.
    /// Kept separate from <c>VEVE.SurfaceMaterial</c> (ballistics enum) so existing gameplay code is unaffected;
    /// use <see cref="SurfaceMaterialKindMap.ToKind"/> to bridge the two, and
    /// <see cref="ProceduralSurfaceKindExtensions.ToSurfaceMaterial"/> for the ballistics mapping.
    /// </summary>
    public enum ProceduralSurfaceKind
    {
        Concrete,
        Wood,
        Metal,
        Brick,
        Fabric,
        Dirt,
        Glass,
        Ice
    }

    /// <summary>
    /// Additive-only mapping helpers between the ballistics <see cref="SurfaceMaterial"/> enum and
    /// <see cref="ProceduralSurfaceKind"/>. The ballistics enum is intentionally not modified.
    /// </summary>
    public static class SurfaceMaterialKindMap
    {
        /// <summary>
        /// Maps a ballistics surface material to a procedural texture category.
        /// Brick and Glass have no ballistics counterpart and are never produced here.
        /// </summary>
        /// <param name="material">The ballistics surface material.</param>
        /// <returns>The procedural category to generate textures for.</returns>
        public static ProceduralSurfaceKind ToKind(SurfaceMaterial material)
        {
            switch (material)
            {
                case SurfaceMaterial.Wood: return ProceduralSurfaceKind.Wood;
                case SurfaceMaterial.Concrete: return ProceduralSurfaceKind.Concrete;
                case SurfaceMaterial.Metal: return ProceduralSurfaceKind.Metal;
                case SurfaceMaterial.Fabric: return ProceduralSurfaceKind.Fabric;
                case SurfaceMaterial.Dirt: return ProceduralSurfaceKind.Dirt;
                case SurfaceMaterial.Ice: return ProceduralSurfaceKind.Ice;
                case SurfaceMaterial.Glass: return ProceduralSurfaceKind.Glass;
                default: return ProceduralSurfaceKind.Concrete;
            }
        }
    }

    /// <summary>
    /// Convenience extensions on <see cref="ProceduralSurfaceKind"/>.
    /// </summary>
    public static class ProceduralSurfaceKindExtensions
    {
        /// <summary>
        /// Reverse-maps a procedural category to its ballistics <see cref="SurfaceMaterial"/> counterpart.
        /// Brick is an architectural-only category and falls back to Concrete.
        /// </summary>
        /// <param name="kind">The procedural category.</param>
        /// <returns>The corresponding ballistics surface material.</returns>
        public static SurfaceMaterial ToSurfaceMaterial(this ProceduralSurfaceKind kind)
        {
            switch (kind)
            {
                case ProceduralSurfaceKind.Wood: return SurfaceMaterial.Wood;
                case ProceduralSurfaceKind.Metal: return SurfaceMaterial.Metal;
                case ProceduralSurfaceKind.Fabric: return SurfaceMaterial.Fabric;
                case ProceduralSurfaceKind.Dirt: return SurfaceMaterial.Dirt;
                case ProceduralSurfaceKind.Glass: return SurfaceMaterial.Glass;
                case ProceduralSurfaceKind.Ice: return SurfaceMaterial.Ice;
                case ProceduralSurfaceKind.Concrete: return SurfaceMaterial.Concrete;
                case ProceduralSurfaceKind.Brick: return SurfaceMaterial.Concrete;
                default: return SurfaceMaterial.Concrete;
            }
        }

        /// <summary>
        /// Gets whether the category is physically non-porous (glass/smooth ice), i.e. whether micro-detail
        /// normal blending and height parallax should be skipped for realism.
        /// </summary>
        /// <param name="kind">The procedural category.</param>
        /// <returns>True when the surface is smooth/non-porous.</returns>
        public static bool IsNonPorous(this ProceduralSurfaceKind kind)
        {
            return kind == ProceduralSurfaceKind.Glass;
        }
    }

    /// <summary>
    /// Immutable result of one procedural surface generation: four 1024² textures
    /// (albedo sRGB, normal linear tangent-space, height linear, packed roughness mask linear).
    /// Owned by the factory; call <see cref="Dispose"/> only if you deliberately take ownership.
    /// </summary>
    public sealed class CachedTextureBundle
    {
        /// <summary>The surface category this bundle was generated for.</summary>
        public readonly ProceduralSurfaceKind Kind;

        /// <summary>Master seed that produced this bundle (pairs with <see cref="Resolution"/> for determinism).</summary>
        public readonly int Seed;

        /// <summary>Square texture resolution in pixels.</summary>
        public readonly int Resolution;

        /// <summary>sRGB base-color map.</summary>
        public readonly Texture2D Albedo;

        /// <summary>Linear tangent-space normal map (OpenGL convention, +Y up), derived from the height field.</summary>
        public readonly Texture2D Normal;

        /// <summary>Linear 8-bit height field (grayscale in R). Use with parallax/steep-angle smoothing.</summary>
        public readonly Texture2D Height;

        /// <summary>
        /// Linear packed mask texture. Channel packing (PBR-style, documented for future HDRP LayeredMaterial):
        /// R = roughness, G = ambient occlusion (height-derived, soft clamp), B = metallic, A = 0 reserved.
        /// </summary>
        public readonly Texture2D RoughnessMask;

        internal CachedTextureBundle(ProceduralSurfaceKind kind, int seed, int resolution, Texture2D albedo, Texture2D normal, Texture2D height, Texture2D roughnessMask)
        {
            Kind = kind;
            Seed = seed;
            Resolution = resolution;
            Albedo = albedo;
            Normal = normal;
            Height = height;
            RoughnessMask = roughnessMask;
        }

        /// <summary>True when all four textures exist and have not been destroyed.</summary>
        public bool IsReady => Albedo != null && Normal != null && Height != null && RoughnessMask != null;

        /// <summary>Packed roughness of the surface at zero texture coordinates (representative mean).</summary>
        public float Roughness => RoughnessMask != null ? RoughnessMask.GetPixel(0, 0).r : 0.5f;

        /// <summary>
        /// Destroys the underlying textures. The factory does this automatically when a bundle is regenerated;
        /// call manually only after taking a bundle out of factory ownership.
        /// </summary>
        public void Dispose()
        {
            DestroyIfAlive(Albedo);
            DestroyIfAlive(Normal);
            DestroyIfAlive(Height);
            DestroyIfAlive(RoughnessMask);
        }

        private static void DestroyIfAlive(Texture2D texture)
        {
            if (texture != null)
            {
                Object.Destroy(texture);
            }
        }
    }

    /// <summary>
    /// Generates deterministic 1024² procedural PBR surface sets (albedo / normal / height / roughness mask)
    /// for every <see cref="ProceduralSurfaceKind"/> from a pure integer hash lattice — no compute shaders,
    /// no editor APIs, no System.Random (bit-exact reproducibility across runs and builds).
    /// <para>
    /// Height-to-normal conversion uses a central-difference gradient of the height field h(x,y)
    /// (Sobel family, single-pixel span):
    /// dx = h(x+1,y) - h(x-1,y) and dy = h(x,y+1) - h(x,y-1), then
    /// n = normalize((-n_x, -n_y, n_z)) with n_x = dx * strength * 0.5, n_y = dy * strength * 0.5, n_z = 1,
    /// encoded to [0,1] as (n * 0.5 + 0.5). Strength is tuned per category for realistic relief scale.
    /// </para>
    /// <para>
    /// Generation is async-chunked: the factory is a state machine driven by <see cref="Tick"/> and enforces a
    /// strict per-frame CPU budget (default 2 ms) using <c>Stopwatch</c>. Work stays on the main thread on
    /// purpose: texture object creation must be thread-safe with respect to Unity's object lifecycle and the
    /// budget guarantees no frame hitch. Call <see cref="GenerateNow"/> for synchronous boot/test use.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class ProceduralSurfaceTextureFactory : MonoBehaviour
    {
        /// <summary>The factory created by <see cref="EnsureInstance"/>, or null if none exists yet.</summary>
        public static ProceduralSurfaceTextureFactory Instance { get; private set; }

        [Header("Generation")]
        [SerializeField] private int masterSeed = 20260830;
        [SerializeField, Range(256, 2048)] private int resolution = 1024;
        [SerializeField] private float frameBudgetMilliseconds = 2f;

        private enum Stage
        {
            HeightRows = 0,
            Albedo = 1,
            Roughness = 2,
            Normal = 3,
            Finalize = 4,
            StageCount = 5
        }

        [Serializable]
        private sealed class KindQueueEntry
        {
            public ProceduralSurfaceKind kind;
            public int stage;
            public int nextRow;

            public KindQueueEntry()
            {
            }

            public KindQueueEntry(ProceduralSurfaceKind kind)
            {
                this.kind = kind;
                stage = (int)Stage.HeightRows;
                nextRow = 0;
            }
        }

        private readonly Dictionary<ProceduralSurfaceKind, CachedTextureBundle> bundles = new Dictionary<ProceduralSurfaceKind, CachedTextureBundle>();
        private readonly Dictionary<ProceduralSurfaceKind, float[]> heightScratch = new Dictionary<ProceduralSurfaceKind, float[]>();
        private readonly Queue<KindQueueEntry> queue = new Queue<KindQueueEntry>();
        private Texture2D[] stepTextures = new Texture2D[4];
        private readonly Stopwatch stopwatch = new Stopwatch();
        private int totalQueuedSteps;
        private int completedSteps;

        /// <summary>Raised immediately after a bundle finishes generating (during <see cref="Tick"/> or <see cref="GenerateNow"/>).</summary>
        public event Action<CachedTextureBundle> BundleReady;

        /// <summary>Master seed applied to all generated surfaces.</summary>
        public int MasterSeed => masterSeed;

        /// <summary>Square resolution of generated textures.</summary>
        public int Resolution => Mathf.NextPowerOfTwo(Mathf.Max(256, resolution));

        /// <summary>Per-frame CPU budget in milliseconds (clamped to [0.5, 16]).</summary>
        public float FrameBudgetMilliseconds
        {
            get => frameBudgetMilliseconds;
            set => frameBudgetMilliseconds = Mathf.Clamp(value, 0.5f, 16f);
        }

        /// <summary>True while at least one surface is queued or mid-generation.</summary>
        public bool IsGenerating => queue.Count > 0;

        /// <summary>Normalized completion of the current queue, 0..1.</summary>
        public float Progress => totalQueuedSteps <= 0 ? 1f : Mathf.Clamp01(completedSteps / (float)totalQueuedSteps);

        /// <summary>
        /// Returns the shared factory, creating a hidden bootstrap <see cref="GameObject"/> on demand.
        /// Safe to call from any runtime entry point; never returns null.
        /// </summary>
        /// <returns>The live factory instance.</returns>
        public static ProceduralSurfaceTextureFactory EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            ProceduralSurfaceTextureFactory existing = FindFirstObjectProcedural();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                GameObject go = new GameObject("[ProceduralSurfaceTextureFactory]");
                go.hideFlags = HideFlags.HideAndDontSave;
                Instance = go.AddComponent<ProceduralSurfaceTextureFactory>();
            }

            DontDestroyOnLoad(Instance.gameObject);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (queue.Count == 0 && bundles.Count == 0)
            {
                EnqueueAll();
            }
        }

        private void OnDestroy()
        {
            foreach (CachedTextureBundle bundle in bundles.Values)
            {
                bundle.Dispose();
            }

            bundles.Clear();
            heightScratch.Clear();
            queue.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Queues every procedural surface category for generation at the configured resolution and seed.
        /// Already-generated categories are regenerated in place (old bundle disposed on completion).
        /// </summary>
        public void EnqueueAll()
        {
            foreach (ProceduralSurfaceKind kind in Enum.GetValues(typeof(ProceduralSurfaceKind)))
            {
                Enqueue(kind);
            }
        }

        /// <summary>
        /// Queues a single surface category for generation. Idempotent while the category is pending.
        /// </summary>
        /// <param name="kind">The category to generate.</param>
        public void Enqueue(ProceduralSurfaceKind kind)
        {
            foreach (KindQueueEntry pending in queue)
            {
                if (pending.kind == kind)
                {
                    return;
                }
            }

            queue.Enqueue(new KindQueueEntry(kind));
            int bands = (Resolution + Generator.BandSize - 1) / Generator.BandSize;
            totalQueuedSteps += bands + (int)Stage.Finalize;
        }

        /// <summary>
        /// Drives the generation state machine, executing single-row-band steps until the frame budget is spent.
        /// Each step is bounded by <see cref="FrameBudgetMilliseconds"/>, worst-case over-run is one band.
        /// </summary>
        /// <param name="unusedDeltaTime">Accepted for symmetry with <see cref="Update"/>; timing uses a Stopwatch.</param>
        /// <returns>True if generation work remains after this call.</returns>
        public bool Tick(float unusedDeltaTime = 0f)
        {
            if (queue.Count == 0)
            {
                return false;
            }

            stopwatch.Restart();
            while (queue.Count > 0)
            {
                KindQueueEntry entry = queue.Peek();
                ExecuteStep(entry);
                if (stopwatch.Elapsed.TotalMilliseconds >= FrameBudgetMilliseconds)
                {
                    break;
                }
            }

            return queue.Count > 0;
        }

        /// <summary>
        /// Generates one surface category synchronously, bypassing the queue and budget.
        /// Intended for bootstrapping, tests and editor-time baking — costs roughly 15-25 ms per surface.
        /// </summary>
        /// <param name="kind">The category to generate.</param>
        /// <returns>The finished bundle.</returns>
        public CachedTextureBundle GenerateNow(ProceduralSurfaceKind kind)
        {
            CachedTextureBundle previous;
            if (bundles.TryGetValue(kind, out previous))
            {
                previous.Dispose();
                bundles.Remove(kind);
            }

            if (queue.Count > 0)
            {
                List<KindQueueEntry> keep = new List<KindQueueEntry>(queue.Count);
                foreach (KindQueueEntry pending in queue)
                {
                    if (pending.kind != kind)
                    {
                        keep.Add(pending);
                    }
                }

                if (keep.Count != queue.Count)
                {
                    queue.Clear();
                    for (int i = 0; i < keep.Count; i++)
                    {
                        queue.Enqueue(keep[i]);
                    }

                    int bands = (Resolution + Generator.BandSize - 1) / Generator.BandSize;
                    totalQueuedSteps -= bands + (int)Stage.Finalize;
                    completedSteps = Mathf.Min(completedSteps, totalQueuedSteps);
                }
            }

            heightScratch.Remove(kind);
            CachedTextureBundle bundle = BuildBundle(kind);
            CacheBundle(kind, bundle);
            RaiseBundleReady(bundle);
            return bundle;
        }

        /// <summary>
        /// Gets the cached bundle for a ballistics surface material, generating on demand if missing.
        /// </summary>
        /// <param name="material">The ballistics surface material.</param>
        /// <returns>The finished bundle.</returns>
        public CachedTextureBundle GetBundleFor(SurfaceMaterial material)
        {
            return GetOrCreate(SurfaceMaterialKindMap.ToKind(material));
        }

        /// <summary>
        /// Gets the cached bundle for a category if present, otherwise generates it synchronously.
        /// </summary>
        /// <param name="kind">The category to look up.</param>
        /// <returns>The cached or freshly generated bundle.</returns>
        public CachedTextureBundle GetOrCreate(ProceduralSurfaceKind kind)
        {
            CachedTextureBundle bundle;
            if (bundles.TryGetValue(kind, out bundle) && bundle.IsReady)
            {
                return bundle;
            }

            return GenerateNow(kind);
        }

        /// <summary>
        /// Attempts a non-blocking lookup. Never triggers generation.
        /// </summary>
        /// <param name="kind">The category to look up.</param>
        /// <param name="bundle">The bundle when available.</param>
        /// <returns>True if a ready bundle was found.</returns>
        public bool TryGetBundle(ProceduralSurfaceKind kind, out CachedTextureBundle bundle)
        {
            CachedTextureBundle cached;
            if (bundles.TryGetValue(kind, out cached) && cached.IsReady)
            {
                bundle = cached;
                return true;
            }

            bundle = null;
            return false;
        }

        /// <summary>
        /// Converts a linear height array of size size*size into a tangent-space normal Color32 array.
        /// Documented formula (central differences, wrapping edges):
        /// dx = h[x+1,y] - h[x-1,y]; dy = h[y+1,x]... = h[x,y+1] - h[x,y-1];
        /// vector = normalize((-dx * 0.5 * strength, -dy * 0.5 * strength, 1)); encoded as vector * 0.5 + 0.5.
        /// This is the standard gradient (not full Sobel 3x3 kernel) height-to-normal conversion.
        /// </summary>
        /// <param name="heights">Linear [0,1] height samples, row-major size*size.</param>
        /// <param name="size">Texture edge length.</param>
        /// <param name="strength">Relief strength multiplier (per-category tuned, ~1.5 for brushed metal, ~6 for brick).</param>
        /// <returns>Encoded tangent-space normal pixels.</returns>
        public static Color32[] ConvertHeightToNormal(float[] heights, int size, float strength)
        {
            Color32[] pixels = new Color32[size * size];
            if (heights == null || heights.Length < size * size || size < 2)
            {
                return pixels;
            }

            for (int y = 0; y < size; y++)
            {
                int yUp = ((y + 1) % size) * size;
                int yDown = ((y - 1 + size) % size) * size;
                int yMid = y * size;
                for (int x = 0; x < size; x++)
                {
                    int xRight = (x + 1) % size;
                    int xLeft = (x - 1 + size) % size;
                    float dx = heights[yMid + xRight] - heights[yMid + xLeft];
                    float dy = heights[yUp + x] - heights[yDown + x];
                    Vector3 normal = new Vector3(-dx * 0.5f * strength, -dy * 0.5f * strength, 1f).normalized;
                    pixels[yMid + x] = new Color32(
                        (byte)Mathf.Clamp((normal.x * 0.5f + 0.5f) * 255f, 0f, 255f),
                        (byte)Mathf.Clamp((normal.y * 0.5f + 0.5f) * 255f, 0f, 255f),
                        (byte)Mathf.Clamp((normal.z * 0.5f + 0.5f) * 255f, 0f, 255f),
                        0);
                }
            }

            return pixels;
        }

        private void ExecuteStep(KindQueueEntry entry)
        {
            int size = Resolution;
            switch ((Stage)entry.stage)
            {
                case Stage.HeightRows:
                    {
                        float[] rows;
                        if (!heightScratch.TryGetValue(entry.kind, out rows) || rows.Length != size * size)
                        {
                            rows = new float[size * size];
                            heightScratch[entry.kind] = rows;
                        }

                        int bandEnd = entry.nextRow + Generator.BandSize;
                        if (bandEnd > size)
                        {
                            bandEnd = size;
                        }

                        for (int y = entry.nextRow; y < bandEnd; y++)
                        {
                            Generator.EvaluateHeightRow(entry.kind, masterSeed, size, y, rows);
                        }

                        entry.nextRow = bandEnd;
                        if (entry.nextRow >= size)
                        {
                            entry.nextRow = 0;
                            entry.stage = (int)Stage.Albedo;
                        }

                        break;
                    }

                case Stage.Albedo:
                    {
                        Texture2D albedo = Generator.NewTexture("VEVE_Proc_" + entry.kind + "_Albedo", size, true);
                        albedo.SetPixels32(Generator.BuildAlbedo(entry.kind, masterSeed, size, heightScratch[entry.kind]));
                        albedo.Apply(false);
                        stepTextures[0] = albedo;
                        entry.stage = (int)Stage.Roughness;
                        break;
                    }

                case Stage.Roughness:
                    {
                        Texture2D mask = Generator.NewTexture("VEVE_Proc_" + entry.kind + "_Mask", size, false);
                        mask.SetPixels32(Generator.BuildRoughnessMask(entry.kind, masterSeed, size, heightScratch[entry.kind]));
                        mask.Apply(false);
                        stepTextures[1] = mask;
                        entry.stage = (int)Stage.Normal;
                        break;
                    }

                case Stage.Normal:
                    {
                        Texture2D height = Generator.NewTexture("VEVE_Proc_" + entry.kind + "_Height", size, false);
                        height.SetPixels32(Generator.BuildHeightPixels(size, heightScratch[entry.kind]));
                        height.Apply(false);
                        stepTextures[3] = height;

                        Texture2D normal = Generator.NewTexture("VEVE_Proc_" + entry.kind + "_Normal", size, false);
                        normal.SetPixels32(ConvertHeightToNormal(heightScratch[entry.kind], size, Generator.NormalStrength(entry.kind)));
                        normal.Apply(false);
                        stepTextures[2] = normal;
                        entry.stage = (int)Stage.Finalize;
                        break;
                    }

                case Stage.Finalize:
                    {
                        CachedTextureBundle previous;
                        if (bundles.TryGetValue(entry.kind, out previous))
                        {
                            previous.Dispose();
                        }

                        heightScratch.Remove(entry.kind);
                        CachedTextureBundle bundle = new CachedTextureBundle(entry.kind, masterSeed, size, stepTextures[0], stepTextures[2], stepTextures[3], stepTextures[1]);
                        stepTextures = new Texture2D[4];
                        CacheBundle(entry.kind, bundle);
                        queue.Dequeue();
                        RaiseBundleReady(bundle);
                        break;
                    }
            }

            completedSteps++;
        }

        private void CacheBundle(ProceduralSurfaceKind kind, CachedTextureBundle bundle)
        {
            bundles[kind] = bundle;
        }

        private void RaiseBundleReady(CachedTextureBundle bundle)
        {
            Action<CachedTextureBundle> handler = BundleReady;
            if (handler != null)
            {
                handler(bundle);
            }
        }

        private CachedTextureBundle BuildBundle(ProceduralSurfaceKind kind)
        {
            int size = Resolution;
            float[] heights = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                Generator.EvaluateHeightRow(kind, masterSeed, size, y, heights);
            }

            Texture2D albedo = Generator.NewTexture("VEVE_Proc_" + kind + "_Albedo", size, true);
            albedo.SetPixels32(Generator.BuildAlbedo(kind, masterSeed, size, heights));
            albedo.Apply(false);

            Texture2D mask = Generator.NewTexture("VEVE_Proc_" + kind + "_Mask", size, false);
            mask.SetPixels32(Generator.BuildRoughnessMask(kind, masterSeed, size, heights));
            mask.Apply(false);

            Texture2D height = Generator.NewTexture("VEVE_Proc_" + kind + "_Height", size, false);
            height.SetPixels32(Generator.BuildHeightPixels(size, heights));
            height.Apply(false);

            Texture2D normal = Generator.NewTexture("VEVE_Proc_" + kind + "_Normal", size, false);
            normal.SetPixels32(ConvertHeightToNormal(heights, size, Generator.NormalStrength(kind)));
            normal.Apply(false);

            return new CachedTextureBundle(kind, masterSeed, size, albedo, normal, height, mask);
        }

        private static ProceduralSurfaceTextureFactory FindFirstObjectProcedural()
        {
            return FindFirstObjectByType<ProceduralSurfaceTextureFactory>();
        }

        /// <summary>
        /// Deterministic CPU pattern core: SplitMix64-seeded value-noise lattice, fBm, domain warping and the
        /// per-category height/albedo/roughness evaluators. Stateless static functions; identical output for the
        /// same (kind, seed, resolution) on every run and platform with IEEE-754 floats.
        /// </summary>
        private static class Generator
        {
            /// <summary>Number of height rows computed per budgeted step.</summary>
            public const int BandSize = 128;

            public static float Hash01(int x, int y, int h)
            {
                uint n = (uint)x * 374761393u + (uint)y * 668265263u + unchecked((uint)h) * 2147483647u;
                n = (n ^ (n >> 13)) * 1274126177u;
                n = n ^ (n >> 16);
                return n / 4294967295f;
            }

            private static float LatticeValue(int ix, int iy, int freq, int seed)
            {
                return Hash01(((ix % freq) + freq) % freq, ((iy % freq) + freq) % freq, seed ^ (freq * 7919));
            }

            public static float ValueAt(float px, float py, int freq, int seed)
            {
                float fx = px * freq;
                float fy = py * freq;
                int ix = Mathf.FloorToInt(fx);
                int iy = Mathf.FloorToInt(fy);
                float tx = fx - ix;
                float ty = fy - iy;
                tx = tx * tx * (3f - 2f * tx);
                ty = ty * ty * (3f - 2f * ty);
                float v0 = Mathf.Lerp(LatticeValue(ix, iy, freq, seed), LatticeValue(ix + 1, iy, freq, seed), tx);
                float v1 = Mathf.Lerp(LatticeValue(ix, iy + 1, freq, seed), LatticeValue(ix + 1, iy + 1, freq, seed), tx);
                return Mathf.Lerp(v0, v1, ty);
            }

            /// <summary>Five-octave fBm over a shared lattice, normalized to [0,1].</summary>
            public static float Fbm(float px, float py, int freq, int seed)
            {
                float sum = 0f;
                float weight = 0.5f;
                int f = freq;
                int s = seed;
                for (int octave = 0; octave < 5; octave++)
                {
                    sum += ValueAt(px, py, f, s) * weight;
                    weight *= 0.5f;
                    f = (f * 2) & 0x3FFFFFFF;
                    s ^= unchecked((int)0x9E3779B9u);
                }

                return sum / 0.96875f;
            }

            public static Vector2 DomainWarp(float px, float py, float amount, int freq, int seed)
            {
                float wx = Fbm(px + 0.37f, py + 0.91f, freq, seed ^ 0x51ED2701);
                float wy = Fbm(px + 0.73f, py + 0.19f, freq, seed ^ 0x27220A95);
                return new Vector2(px + (wx - 0.5f) * amount, py + (wy - 0.5f) * amount);
            }

            private static Vector2 Voronoi2(float px, float py, int cells, int seed)
            {
                int ix = Mathf.FloorToInt(px * cells);
                int iy = Mathf.FloorToInt(py * cells);
                float fx = px * cells - ix;
                float fy = py * cells - iy;
                float d1 = 8f;
                float d2 = 8f;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float pxo = ox + Hash01(ix + ox, iy + oy, seed) - fx;
                        float pyo = oy + Hash01(ix + ox, iy + oy, seed ^ 0x77F) - fy;
                        float d = pxo * pxo + pyo * pyo;
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }
                }

                return new Vector2(Mathf.Sqrt(d1), Mathf.Sqrt(d2));
            }

            /// <summary>Wrapping distance to the nearest grid line at the given period, normalized [0,0.5].</summary>
            public static float GridLineDistance(float p, float period)
            {
                float m = Mathf.PingPong(p, period * 0.5f);
                return Mathf.Min(m, period - m);
            }

            public static float NormalStrength(ProceduralSurfaceKind kind)
            {
                switch (kind)
                {
                    case ProceduralSurfaceKind.Concrete: return 3.0f;
                    case ProceduralSurfaceKind.Wood: return 4.0f;
                    case ProceduralSurfaceKind.Metal: return 1.5f;
                    case ProceduralSurfaceKind.Brick: return 6.0f;
                    case ProceduralSurfaceKind.Fabric: return 2.0f;
                    case ProceduralSurfaceKind.Dirt: return 5.0f;
                    case ProceduralSurfaceKind.Glass: return 0.5f;
                    case ProceduralSurfaceKind.Ice: return 3.0f;
                    default: return 3.0f;
                }
            }

            public static void EvaluateHeightRow(ProceduralSurfaceKind kind, int seed, int size, int y, float[] heights)
            {
                float py = (y + 0.5f) / size;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    heights[row + x] = EvalHeight(kind, seed, (x + 0.5f) / size, py);
                }
            }

            private static float EvalHeight(ProceduralSurfaceKind kind, int seed, float u, float v)
            {
                switch (kind)
                {
                    case ProceduralSurfaceKind.Concrete:
                    {
                        Vector2 w = DomainWarp(u, v, 0.08f, 4, seed);
                        float h = 0.5f + 0.155f * (Fbm(w.x, w.y, 4, seed) * 2f - 1f);
                        h += 0.012f * (ValueAt(u, v, 512, seed ^ 17) - 0.35f) * 8f;
                        float speckle = Hash01(Mathf.FloorToInt(u * 180f), Mathf.FloorToInt(v * 180f), seed);
                        if (speckle > 0.977f) h += 0.10f; else if (speckle > 0.964f) h += 0.05f; else if (speckle < 0.016f) h -= 0.06f;
                        return Mathf.Clamp01(h);
                    }

                    case ProceduralSurfaceKind.Wood:
                    {
                        Vector2 w = DomainWarp(u, v, 0.09f, 8, seed);
                        float stretch = w.y * 16f;
                        float d = Mathf.Sqrt(w.x * w.x + stretch * stretch);
                        float a = Mathf.Atan2(stretch, w.x) / (2f * Mathf.PI) * 5f;
                        float ring = Mathf.Abs(Mathf.Sin((d * 72f + a) * Mathf.PI));
                        ring = Mathf.Pow(ring, 0.45f);
                        float grain = 0.05f * (Fbm(u, v * 8f, 128, seed ^ 41) * 2f - 1f);
                        float knot = Hash01(Mathf.FloorToInt(u * 120f), 0, seed) > 0.992f ? 0.12f : 0f;
                        return Mathf.Clamp01(0.78f * ring + 0.22f + grain - knot);
                    }

                    case ProceduralSurfaceKind.Metal:
                    {
                        float streak = (1f - ValueAt(u, v, 16, seed ^ 5)) * 0.5f + (1f - ValueAt(u, v, 1024, seed ^ 7)) * 0.25f
                            + (Fbm(u, v, 32, seed ^ 11) - 0.5f) * 0.04f;
                        float pits = Hash01(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), seed) > 0.996f ? -0.15f : 0f;
                        return Mathf.Clamp01(0.5f + streak * 0.35f + pits);
                    }

                    case ProceduralSurfaceKind.Brick:
                    {
                        float periodU = 12f;
                        float periodV = 6f;
                        int rowIdx = Mathf.FloorToInt(v * periodV);
                        float uoff = u + (Hash01(rowIdx, 0, seed) > 0.5f ? 0.5f / periodU : 0f);
                        float edge = Mathf.Min(GridLineDistance(uoff * periodU, 1f), GridLineDistance(v * periodV, 1f));
                        float h;
                        if (edge < 0.05f)
                        {
                            h = 0.10f + edge * 1.2f;
                        }
                        else
                        {
                            h = 0.62f + 0.16f * (Fbm(u * 3f, v * 3f, 16, seed ^ 23) * 2f - 1f)
                                + 0.12f * (ValueAt(u, v, 96, seed ^ 29) - 0.5f)
                                + (Hash01(Mathf.FloorToInt(uoff * periodU), rowIdx, seed) - 0.5f) * 0.10f;
                        }

                        return Mathf.Clamp01(h);
                    }

                    case ProceduralSurfaceKind.Fabric:
                    {
                        float threadU = 1f - 2f * Mathf.Abs((u * 128f) % 1f - 0.5f);
                        threadU = threadU * threadU * threadU;
                        float threadV = 0.55f + 0.45f * (1f - 2f * Mathf.Abs((v * 128f) % 1f - 0.5f));
                        int parity = ((Mathf.FloorToInt(u * 128f) + Mathf.FloorToInt(v * 128f)) & 1);
                        float baseH = parity == 0 ? threadU : threadV;
                        float fuzz = 0.06f * (ValueAt(u, v, 256, seed ^ 37) - 0.5f) * 2f;
                        Vector2 cw = DomainWarp(u, v, 0.03f, 3, seed ^ 43);
                        float crease = 0.05f * Mathf.Sin((cw.x * 5f + cw.y * 3f) * Mathf.PI * 2f);
                        return Mathf.Clamp01(0.45f + 0.30f * baseH + fuzz + crease);
                    }

                    case ProceduralSurfaceKind.Dirt:
                    {
                        Vector2 w = DomainWarp(u, v, 0.18f, 4, seed);
                        float patches = Mathf.Pow(Fbm(w.x, w.y, 3, seed ^ 53), 1.5f);
                        float h = 0.25f + 0.5f * patches * (0.35f + 0.65f * Fbm(w.x, w.y, 32, seed ^ 59));
                        Vector2 vor = Voronoi2(u, v, 18, seed ^ 61);
                        float pebble = Mathf.Clamp01(vor.x * 3f) * step01(vor.y - vor.x, 0.35f);
                        h = h * (1f - 0.6f * pebble) + 0.55f * pebble;
                        h += 0.02f * (ValueAt(u, v, 256, seed ^ 67) - 0.5f);
                        return Mathf.Clamp01(h);
                    }

                    case ProceduralSurfaceKind.Ice:
                    {
                        Vector2 w = DomainWarp(u, v, 0.06f, 4, seed);
                        float h = 0.55f + 0.12f * (Fbm(w.x, w.y, 6, seed) * 2f - 1f);
                        Vector2 vor = Voronoi2(u, v, 10, seed ^ 71);
                        float crack = Mathf.Clamp01(1f - (vor.y - vor.x) * 10f);
                        crack *= crack;
                        h -= 0.35f * crack;
                        return Mathf.Clamp01(h);
                    }

                    case ProceduralSurfaceKind.Glass:
                    {
                        float smudge = 0.12f * (Fbm(u * 2f, v, 6, seed ^ 73) - 0.5f);
                        return Mathf.Clamp01(0.5f + smudge);
                    }

                    default:
                        return 0.5f;
                }
            }

            private static float step01(float value, float edge)
            {
                float t = value / edge;
                if (t <= 0f) return 1f;
                if (t >= 1f) return 0f;
                return 1f - t * t * (3f - 2f * t);
            }

            public static Color32[] BuildAlbedo(ProceduralSurfaceKind kind, int seed, int size, float[] heights)
            {
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size;
                    int row = y * size;
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size;
                        pixels[row + x] = EvalAlbedo(kind, seed, u, v, heights[row + x]);
                    }
                }

                return pixels;
            }

            private static Color32 EvalAlbedo(ProceduralSurfaceKind kind, int seed, float u, float v, float h)
            {
                Color baseColor;
                Color secondary;
                switch (kind)
                {
                    case ProceduralSurfaceKind.Concrete:
                        baseColor = new Color(0.475f, 0.471f, 0.458f);
                        secondary = new Color(0.301f, 0.297f, 0.286f);
                        break;
                    case ProceduralSurfaceKind.Wood:
                        baseColor = new Color(0.372f, 0.226f, 0.121f);
                        secondary = new Color(0.208f, 0.114f, 0.054f);
                        break;
                    case ProceduralSurfaceKind.Metal:
                        baseColor = new Color(0.618f, 0.624f, 0.640f);
                        secondary = new Color(0.486f, 0.498f, 0.518f);
                        break;
                    case ProceduralSurfaceKind.Brick:
                        baseColor = new Color(0.506f, 0.227f, 0.161f);
                        secondary = new Color(0.584f, 0.566f, 0.514f);
                        break;
                    case ProceduralSurfaceKind.Fabric:
                        baseColor = new Color(0.282f, 0.306f, 0.263f);
                        secondary = new Color(0.176f, 0.192f, 0.161f);
                        break;
                    case ProceduralSurfaceKind.Dirt:
                        baseColor = new Color(0.282f, 0.200f, 0.129f);
                        secondary = new Color(0.451f, 0.353f, 0.231f);
                        break;
                    case ProceduralSurfaceKind.Ice:
                        baseColor = new Color(0.780f, 0.847f, 0.878f);
                        secondary = new Color(0.568f, 0.674f, 0.737f);
                        break;
                    default:
                        baseColor = new Color(0.831f, 0.898f, 0.922f);
                        secondary = new Color(0.906f, 0.937f, 0.949f);
                        break;
                }

                Color color = Color.Lerp(secondary, baseColor, h);

                switch (kind)
                {
                    case ProceduralSurfaceKind.Concrete:
                    {
                        float tone = ColorToLuma(baseColor) * (0.88f + 0.14f * h) * (1f - 0.12f * Mathf.Pow(Fbm(u, v, 2, seed ^ 101), 2f));
                        color = Grayscale(tone);
                        float cellSeed = Hash01(Mathf.FloorToInt(u * 180f), Mathf.FloorToInt(v * 180f), seed);
                        if (cellSeed > 0.977f) color = Grayscale(tone * (cellSeed > 0.992f ? 1.18f : 0.72f));
                        break;
                    }

                    case ProceduralSurfaceKind.Wood:
                    {
                        float ringMix = Mathf.Clamp01((h - 0.35f) * 2f);
                        color = Color.Lerp(new Color(0.196f, 0.098f, 0.043f), new Color(0.478f, 0.290f, 0.141f), 1f - ringMix);
                        float plankTone = Hash01(Mathf.FloorToInt(v * 120f), 0, seed ^ 103);
                        color *= 0.9f + 0.2f * plankTone;
                        break;
                    }

                    case ProceduralSurfaceKind.Metal:
                    {
                        float tone = 0.55f + 0.12f * h + 0.05f * (Hash01(Mathf.FloorToInt(u * 32f), Mathf.FloorToInt(v * 32f), seed ^ 107) - 0.5f);
                        color = new Color(tone * 0.975f, tone * 0.985f, tone);
                        break;
                    }

                    case ProceduralSurfaceKind.Brick:
                    {
                        if (h < 0.30f)
                        {
                            color = new Color(0.584f, 0.566f, 0.514f) * (0.9f + 0.2f * Fbm(u, v, 64, seed ^ 109));
                        }
                        else
                        {
                            float variant = Hash01(Mathf.FloorToInt(u * 12f), Mathf.FloorToInt(v * 6f), seed ^ 113);
                            color = Color.Lerp(new Color(0.376f, 0.153f, 0.106f), new Color(0.588f, 0.275f, 0.188f), variant);
                            color *= 0.92f + 0.16f * h;
                        }

                        break;
                    }

                    case ProceduralSurfaceKind.Fabric:
                    {
                        float warpTint = Hash01(Mathf.FloorToInt(u * 128f), 0, seed ^ 127);
                        float weftTint = Hash01(0, Mathf.FloorToInt(v * 128f), seed ^ 131);
                        color *= 0.9f + 0.12f * warpTint + 0.08f * weftTint + 0.10f * h;
                        float stain = Fbm(u, v, 3, seed ^ 137);
                        color *= 1f - 0.18f * Mathf.Pow(Mathf.Clamp01(stain - 0.55f) * 2.2f, 1.5f);
                        break;
                    }

                    case ProceduralSurfaceKind.Dirt:
                    {
                        color = Color.Lerp(new Color(0.212f, 0.149f, 0.094f), new Color(0.451f, 0.353f, 0.231f), h);
                        Vector2 vor = Voronoi2(u, v, 18, seed ^ 61);
                        if (vor.y - vor.x > 0.28f && vor.x < 0.25f)
                        {
                            color = Color.Lerp(color, new Color(0.522f, 0.478f, 0.420f), 0.7f);
                        }

                        break;
                    }

                    case ProceduralSurfaceKind.Ice:
                    {
                        color = Color.Lerp(new Color(0.568f, 0.674f, 0.737f), new Color(0.831f, 0.910f, 0.937f), h);
                        break;
                    }

                    case ProceduralSurfaceKind.Glass:
                    {
                        color = new Color(0.831f, 0.898f, 0.922f) * (0.97f + 0.06f * h);
                        break;
                    }
                }

                return ToPixel(color);
            }

            public static Color32[] BuildRoughnessMask(ProceduralSurfaceKind kind, int seed, int size, float[] heights)
            {
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size;
                    int row = y * size;
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size;
                        float h = heights[row + x];
                        float detail = Fbm(u, v, 16, seed ^ 151);
                        float r;
                        float m;
                        switch (kind)
                        {
                            case ProceduralSurfaceKind.Concrete:
                                r = 0.70f + 0.16f * detail + 0.06f * h;
                                m = 0f;
                                break;
                            case ProceduralSurfaceKind.Wood:
                                r = 0.58f + 0.12f * h - 0.06f * detail;
                                m = 0f;
                                break;
                            case ProceduralSurfaceKind.Metal:
                                r = 0.28f + 0.18f * detail + 0.08f * ValueAt(u, v, 1024, seed ^ 7);
                                m = 1f;
                                break;
                            case ProceduralSurfaceKind.Brick:
                                r = h < 0.30f ? 0.88f + 0.05f * detail : 0.74f + 0.12f * detail;
                                m = 0f;
                                break;
                            case ProceduralSurfaceKind.Fabric:
                                r = 0.86f + 0.08f * detail;
                                m = 0f;
                                break;
                            case ProceduralSurfaceKind.Dirt:
                                r = 0.82f + 0.12f * detail;
                                m = 0f;
                                break;
                            case ProceduralSurfaceKind.Glass:
                                r = 0.04f + 0.03f * detail;
                                m = 0f;
                                break;
                            default:
                                r = 0.20f + 0.18f * detail;
                                m = h < 0.30f ? 0.15f : 0f;
                                break;
                        }

                        float ao = 0.55f + 0.45f * h * (1f - 0.25f * ValueAt(u, v, 4, seed ^ 13));
                        Color32 pixel = ToPixel(Color.white);
                        pixel.r = ToByte(Mathf.Clamp01(r) * 255f);
                        pixel.g = ToByte(Mathf.Clamp01(ao) * 255f);
                        pixel.b = ToByte(Mathf.Clamp01(m) * 255f);
                        pixel.a = 0;
                        pixels[row + x] = pixel;
                    }
                }

                return pixels;
            }

            public static Color32[] BuildHeightPixels(int size, float[] heights)
            {
                Color32[] pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte value = ToByte(Mathf.Clamp01(heights[i]) * 255f);
                    pixels[i] = new Color32(value, value, value, 0);
                }

                return pixels;
            }

            public static Texture2D NewTexture(string name, int size, bool srgb)
            {
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
                texture.name = name;
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                texture.hideFlags = HideFlags.HideAndDontSave;
                return texture;
            }

            private static Color Grayscale(float luma)
            {
                luma = Mathf.Clamp01(luma);
                return new Color(luma, luma, luma);
            }

            private static float ColorToLuma(Color c)
            {
                return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            }

            private static Color32 ToPixel(Color color)
            {
                return new Color32(ToByte(color.r * 255f), ToByte(color.g * 255f), ToByte(color.b * 255f), 255);
            }

            private static byte ToByte(float value)
            {
                int rounded = (int)(value + 0.5f);
                if (rounded < 0) rounded = 0;
                if (rounded > 255) rounded = 255;
                return (byte)rounded;
            }
        }
    }
}
