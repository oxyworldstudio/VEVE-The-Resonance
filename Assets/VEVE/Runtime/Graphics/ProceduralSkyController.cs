using System.Collections.Generic;
using UnityEngine;
using VEVE;

namespace VEVE.Graphics
{
    /// <summary>
    /// Immutable snapshot of the sky driver state, mirroring the values read from
    /// <c>VEVE.EnvironmentSimulation</c> (or the neutral baseline when absent).
    /// </summary>
    public struct SkyControllerState
    {
        /// <summary>Solar elevation in degrees, clamped to [-90, 90].</summary>
        public float sunElevationDeg;

        /// <summary>Solar azimuth in degrees, wrapped to [0, 360).</summary>
        public float sunAzimuthDeg;

        /// <summary>Hour of day, wrapped to [0, 24).</summary>
        public float hourOfDay;
    }

    /// <summary>
    /// Runtime procedural photoreal sky: builds an inverted sky-dome sphere (UV-sphere
    /// generated in code), a second rotating star dome fed by a deterministic FNV-hashed
    /// star texture, and sun/moon billboard discs, then keeps their colors in sync with
    /// <see cref="SkyPaletteRules"/> and the live <c>VEVE.EnvironmentSimulation</c>.
    /// All materials use only runtime-generated textures and pipeline-agnostic built-in
    /// unlit shaders (no URP dependency, no external or binary assets). The simulation is
    /// looked up with FindFirstObjectByType on a 0.5 s cache; gradient textures repaint at
    /// most 4x per second. Without a simulation the controller holds a neutral noon
    /// baseline and remains fully functional.
    /// <para>
    /// Wiring guidance (comment only, the orchestrator performs the wiring):
    /// SceneBuilder attaches <see cref="ProceduralSkyController"/> and
    /// <see cref="AtmosphereTintBridge"/> to the environment object next to
    /// <c>VEVE.EnvironmentSimulation</c>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralSkyController : MonoBehaviour
    {
        /// <summary>Seconds between cached FindFirstObjectByType lookups of the simulation.</summary>
        public const float SimLookupIntervalSeconds = 0.5f;

        /// <summary>Minimum seconds between gradient/star/tint repaints (4x per second max).</summary>
        public const float GradientIntervalSeconds = 0.25f;

        /// <summary>Neutral baseline hour used when no EnvironmentSimulation exists.</summary>
        public const float NeutralHour = 12f;

        /// <summary>Neutral baseline solar elevation in degrees.</summary>
        public const float NeutralSunElevationDeg = 55f;

        /// <summary>Neutral baseline solar azimuth in degrees.</summary>
        public const float NeutralSunAzimuthDeg = 180f;

        [SerializeField] private float domeRadius = 900f;
        [SerializeField] private int domeSegments = 16;
        [SerializeField] private int domeRings = 8;
        [SerializeField] private int gradientWidth = 48;
        [SerializeField] private int gradientHeight = 48;
        [SerializeField] private int starTextureSize = 256;
        [SerializeField] private uint starSeed = 20260901u;

        private Mesh domeMesh;
        private readonly List<Mesh> createdMeshes = new List<Mesh>();
        private EnvironmentSimulation sim;
        private float simLookupAt = float.NegativeInfinity;
        private float gradientUpdatedAt = float.NegativeInfinity;
        private SkyControllerState state;
        private float humidity01 = 0.5f;
        private float cloudCover01 = 0.15f;
        private Vector3 sunDirection = Vector3.up;
        private Vector3 moonDirection = Vector3.up;

        private Transform skyDomeT;
        private Transform starDomeT;
        private Transform sunT;
        private Transform moonT;
        private MeshRenderer starRenderer;
        private MeshRenderer sunRenderer;
        private MeshRenderer moonRenderer;
        private Texture2D gradientTexture;
        private Texture2D starTexture;
        private Texture2D sunTexture;
        private Texture2D moonTexture;
        private Material domeMaterial;
        private Material starMaterial;
        private Material sunMaterial;
        private Material moonMaterial;
        private Color[] gradientScratch;

        /// <summary>Gets the active dome radius (children scale and billboard orbit follow it).</summary>
        public float DomeRadius => domeRadius;

        /// <summary>Gets the last state applied by <see cref="Tick"/>/<see cref="ForceRefresh"/>.</summary>
        public SkyControllerState CurrentState => state;

        /// <summary>Gets the cached simulation reference (null when none was found yet).</summary>
        public EnvironmentSimulation Simulation => sim;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            DestroyObject(domeMaterial);
            DestroyObject(starMaterial);
            DestroyObject(sunMaterial);
            DestroyObject(moonMaterial);
            DestroyObject(gradientTexture);
            DestroyObject(starTexture);
            DestroyObject(sunTexture);
            DestroyObject(moonTexture);
            for (int i = 0; i < createdMeshes.Count; i++)
            {
                DestroyObject(createdMeshes[i]);
            }

            createdMeshes.Clear();
            skyDomeT = null;
            starDomeT = null;
            sunT = null;
            moonT = null;
            starRenderer = null;
            sunRenderer = null;
            moonRenderer = null;
        }

        /// <summary>
        /// Creates every procedural child (dome, star dome, sun and moon billboards) and
        /// generated textures if missing. Idempotent and safe to call repeatedly, including
        /// from EditMode tests where Awake does not run automatically.
        /// </summary>
        public void EnsureBuilt()
        {
            if (skyDomeT == null)
            {
                BuildAll();
            }
        }

        /// <summary>
        /// Sets the dome radius (clamped to [5, 100000]) and reapplies child scales;
        /// billboard orbits update on the next tick.
        /// </summary>
        /// <param name="radius">New dome radius in world units.</param>
        public void SetDomeRadius(float radius)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius)) radius = 900f;
            domeRadius = Mathf.Clamp(radius, 5f, 100000f);
            ApplyScales();
        }

        /// <summary>Gets the last applied sky state snapshot.</summary>
        /// <returns>The most recent <see cref="SkyControllerState"/>.</returns>
        public SkyControllerState GetState()
        {
            return state;
        }

        /// <summary>
        /// Forces an immediate simulation lookup, state read, dome rotation, billboard
        /// placement and full color refresh. Safe in EditMode tests and never throws.
        /// </summary>
        public void ForceRefresh()
        {
            simLookupAt = float.NegativeInfinity;
            gradientUpdatedAt = float.NegativeInfinity;
            EnsureBuilt();
            Tick(0f);
        }

        /// <summary>
        /// Advances the controller once: cached simulation lookup (0.5 s), state read,
        /// dome rotation, billboard placement, and palette repaints throttled to
        /// <see cref="GradientIntervalSeconds"/>.
        /// </summary>
        /// <param name="deltaTime">Frame delta (accepted for symmetry; timers use realtime).</param>
        public void Tick(float deltaTime)
        {
            EnsureBuilt();
            float now = Time.realtimeSinceStartup;
            if (now - simLookupAt >= SimLookupIntervalSeconds)
            {
                sim = FindFirstObjectByType<EnvironmentSimulation>();
                simLookupAt = now;
            }

            ReadState();
            RotateDomes();
            PositionBillboards();
            if (now - gradientUpdatedAt >= GradientIntervalSeconds)
            {
                UpdateGradientTexture();
                UpdateStarAlpha();
                UpdateBillboardTints();
                gradientUpdatedAt = now;
            }
        }

        /// <summary>
        /// Builds an inverted unit UV-sphere mesh (normals point inward, triangle winding
        /// faces the interior). Vertex count is (rings + 1) * (segments + 1); segments and
        /// rings are clamped to [3, 256] and [2, 128].
        /// </summary>
        /// <param name="segments">Longitude slices.</param>
        /// <param name="rings">Latitude rings.</param>
        /// <returns>Deterministic procedural dome mesh owned by the caller.</returns>
        public static Mesh CreateDomeMesh(int segments, int rings)
        {
            segments = Mathf.Clamp(segments, 3, 256);
            rings = Mathf.Clamp(rings, 2, 128);
            Mesh mesh = new Mesh { name = "VEVE_ProceduralSkyDome" };
            int cols = segments + 1;
            int rows = rings + 1;
            Vector3[] verts = new Vector3[rows * cols];
            Vector3[] norms = new Vector3[rows * cols];
            Vector2[] uvs = new Vector2[rows * cols];
            Color[] colors = new Color[rows * cols];
            int i = 0;
            for (int r = 0; r < rows; r++)
            {
                float theta = Mathf.PI * r / rings;
                float sinT = Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);
                float v = 1f - (float)r / rings;
                for (int s = 0; s < cols; s++)
                {
                    float phi = Mathf.PI * 2f * s / segments;
                    Vector3 dir = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                    verts[i] = dir;
                    norms[i] = -dir;
                    uvs[i] = new Vector2((float)s / segments, v);
                    colors[i] = Color.white;
                    i++;
                }
            }

            int[] tris = new int[segments * rings * 6];
            int t = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * cols + s;
                    int b = a + 1;
                    int c = a + cols;
                    int d = c + 1;
                    tris[t++] = a;
                    tris[t++] = d;
                    tris[t++] = c;
                    tris[t++] = a;
                    tris[t++] = b;
                    tris[t++] = d;
                }
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a unit-sized XY quad billboard mesh with +Z normals (pairs with
        /// transform.LookAt toward the dome center).
        /// </summary>
        /// <returns>Deterministic procedural quad mesh owned by the caller.</returns>
        public static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh { name = "VEVE_ProceduralBillboardQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Generates a deterministic star texture: <paramref name="starCount"/> gaussian
        /// star splats positioned and brightened by <see cref="SkyHash"/> FNV hashing
        /// (no System.Random). Two calls with identical arguments produce identical pixels.
        /// </summary>
        /// <param name="size">Square texture edge in pixels (clamped 16..1024).</param>
        /// <param name="seed">Deterministic variation seed.</param>
        /// <param name="starCount">Number of stars (clamped 1..4096).</param>
        /// <returns>The generated texture.</returns>
        public static Texture2D CreateStarTexture(int size, uint seed, int starCount = 260)
        {
            size = Mathf.Clamp(size, 16, 1024);
            starCount = Mathf.Clamp(starCount, 1, 4096);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = "VEVE_ProceduralStars",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color32[] px = new Color32[size * size];
            for (int s = 0; s < starCount; s++)
            {
                float fx = SkyHash.Hash01(seed, (uint)(s * 4 + 0));
                float fy = SkyHash.Hash01(seed ^ 0x9E3779B9u, (uint)(s * 4 + 1));
                float bright = 0.55f + 0.45f * SkyHash.Hash01(seed ^ 0x85EBCA6Bu, (uint)(s * 4 + 2));
                float hue = SkyHash.Hash01(seed ^ 0xC2B2AE35u, (uint)(s * 4 + 3));
                int cx = Mathf.Clamp((int)(fx * size), 0, size - 1);
                int cy = Mathf.Clamp((int)(fy * size), 0, size - 1);
                Color tint = Color.Lerp(new Color(0.75f, 0.82f, 1f), new Color(1f, 0.92f, 0.78f), hue);
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int x = (cx + ox + size) % size;
                        int y = (cy + oy + size) % size;
                        float d = Mathf.Sqrt(ox * ox + oy * oy);
                        float a = Mathf.Clamp01(bright * (1f - d / 1.6f));
                        int idx = y * size + x;
                        byte na = (byte)Mathf.Clamp(Mathf.Max(px[idx].a, a * 255f), 0f, 255f);
                        px[idx] = new Color32(
                            (byte)Mathf.Clamp(tint.r * 255f, 0f, 255f),
                            (byte)Mathf.Clamp(tint.g * 255f, 0f, 255f),
                            (byte)Mathf.Clamp(tint.b * 255f, 0f, 255f),
                            na);
                    }
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// Generates the sun disc texture: bright soft-edged radial core with a faint
        /// atmospheric glow, tinted per-frame through billboard vertex colors.
        /// </summary>
        /// <param name="size">Square texture edge in pixels (clamped 16..512).</param>
        /// <returns>The generated texture.</returns>
        public static Texture2D CreateSunDiscTexture(int size = 128)
        {
            return CreateRadialTexture(Mathf.Clamp(size, 16, 512), "VEVE_ProceduralSun", 0.30f, 3f, 0.35f, new Color(1f, 0.94f, 0.82f), 0x6A09E667u);
        }

        /// <summary>
        /// Generates the moon disc texture: crisp limb with hash-derived darker maria
        /// patches, tinted per-frame through billboard vertex colors.
        /// </summary>
        /// <param name="size">Square texture edge in pixels (clamped 16..512).</param>
        /// <param name="seed">Deterministic maria seed.</param>
        /// <returns>The generated texture.</returns>
        public static Texture2D CreateMoonDiscTexture(int size = 128, uint seed = 991u)
        {
            size = Mathf.Clamp(size, 16, 512);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = "VEVE_ProceduralMoon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((0.97f - d) * 24f);
                    float limb = 0.55f + 0.45f * Mathf.Sqrt(Mathf.Clamp01(1f - d * d));
                    float maria = 1f;
                    for (int m = 0; m < 8; m++)
                    {
                        float mx = SkyHash.Hash01(seed, (uint)(m * 3 + 0)) * 1.4f - 0.7f;
                        float my = SkyHash.Hash01(seed ^ 0x27D4EB2Fu, (uint)(m * 3 + 1)) * 1.4f - 0.7f;
                        float mr = 0.10f + 0.14f * SkyHash.Hash01(seed ^ 0x165667B1u, (uint)(m * 3 + 2));
                        float md = Mathf.Sqrt((dx - mx) * (dx - mx) + (dy - my) * (dy - my));
                        maria *= 1f - 0.35f * Mathf.Clamp01(1f - md / mr);
                    }

                    float l = 0.92f * limb * maria;
                    px[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(l * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(l * 1.02f * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(l * 1.08f * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(alpha * 255f, 0f, 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// Converts solar elevation/azimuth degrees to a world direction using the same
        /// convention as <c>VEVE.EnvironmentSimulation.SunDirection</c>.
        /// </summary>
        /// <param name="elevationDeg">Elevation in degrees.</param>
        /// <param name="azimuthDeg">Azimuth in degrees.</param>
        /// <returns>Unit direction vector.</returns>
        public static Vector3 DirFromAngles(float elevationDeg, float azimuthDeg)
        {
            float e = elevationDeg * Mathf.Deg2Rad;
            float a = azimuthDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
        }

        private static Texture2D CreateRadialTexture(int size, string name, float coreWidth, float glowFalloff, float glowAmount, Color baseTint, uint seed)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01((1f - d) / coreWidth);
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - d), glowFalloff) * glowAmount;
                    float speck = 1f - 0.04f * SkyHash.Hash01(seed, (uint)(y * size + x));
                    float a = Mathf.Clamp01(core + glow);
                    float l = (0.70f + 0.30f * core) * speck;
                    px[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(baseTint.r * l * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(baseTint.g * l * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(baseTint.b * l * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D CreateGradientTexture(int width, int height)
        {
            width = Mathf.Clamp(width, 8, 256);
            height = Mathf.Clamp(height, 8, 256);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "VEVE_ProceduralSkyGradient",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            tex.wrapModeV = TextureWrapMode.Clamp;
            Color[] px = new Color[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        private static Material CreateUnlitMaterial(Texture2D tex, string name, int renderQueue)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            Material mat = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            if (tex != null) mat.mainTexture = tex;
            mat.renderQueue = renderQueue;
            return mat;
        }

        private static void ConfigureRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static void DestroyObject(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        private static float SanitizeHour(float hour)
        {
            if (float.IsNaN(hour) || float.IsInfinity(hour)) return NeutralHour;
            float h = hour % 24f;
            if (h < 0f) h += 24f;
            return h;
        }

        private static float SanitizeElevation(float elev)
        {
            if (float.IsNaN(elev) || float.IsInfinity(elev)) return NeutralSunElevationDeg;
            return Mathf.Clamp(elev, -90f, 90f);
        }

        private static float SanitizeAzimuth(float azim)
        {
            if (float.IsNaN(azim) || float.IsInfinity(azim)) return NeutralSunAzimuthDeg;
            float a = azim % 360f;
            if (a < 0f) a += 360f;
            return a;
        }

        private static Vector3 SanitizeDirection(Vector3 dir, Vector3 fallback)
        {
            if (!dir.IsFinite() || dir.sqrMagnitude < 1e-10f) return fallback.normalized;
            return dir.normalized;
        }

        private static void SetQuadColor(MeshRenderer renderer, Color color)
        {
            if (renderer == null) return;
            MeshFilter mf = renderer.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Color[] colors = { color, color, color, color };
            mf.sharedMesh.colors = colors;
        }

        private Transform CreateChild(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private MeshRenderer AttachMesh(Transform child, Mesh mesh, Material material)
        {
            MeshFilter mf = child.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = child.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            ConfigureRenderer(mr);
            return mr;
        }

        private void BuildAll()
        {
            domeMesh = CreateDomeMesh(domeSegments, domeRings);
            createdMeshes.Add(domeMesh);
            Mesh starMesh = CreateDomeMesh(domeSegments, domeRings);
            createdMeshes.Add(starMesh);
            Mesh sunQuad = CreateQuadMesh();
            createdMeshes.Add(sunQuad);
            Mesh moonQuad = CreateQuadMesh();
            createdMeshes.Add(moonQuad);

            gradientTexture = CreateGradientTexture(gradientWidth, gradientHeight);
            starTexture = CreateStarTexture(starTextureSize, starSeed);
            sunTexture = CreateSunDiscTexture(128);
            moonTexture = CreateMoonDiscTexture(128, starSeed ^ 0x5BF03635u);

            skyDomeT = CreateChild("SkyDome");
            domeMaterial = CreateUnlitMaterial(gradientTexture, "VEVE_SkyDome_Mat", 2900);
            AttachMesh(skyDomeT, domeMesh, domeMaterial);

            starDomeT = CreateChild("StarDome");
            starMaterial = CreateUnlitMaterial(starTexture, "VEVE_StarDome_Mat", 2910);
            starRenderer = AttachMesh(starDomeT, starMesh, starMaterial);

            sunT = CreateChild("SunBillboard");
            sunMaterial = CreateUnlitMaterial(sunTexture, "VEVE_SunDisc_Mat", 2920);
            sunRenderer = AttachMesh(sunT, sunQuad, sunMaterial);

            moonT = CreateChild("MoonBillboard");
            moonMaterial = CreateUnlitMaterial(moonTexture, "VEVE_MoonDisc_Mat", 2930);
            moonRenderer = AttachMesh(moonT, moonQuad, moonMaterial);

            ApplyScales();
        }

        private void ApplyScales()
        {
            if (skyDomeT != null) skyDomeT.localScale = Vector3.one * domeRadius;
            if (starDomeT != null) starDomeT.localScale = Vector3.one * (domeRadius * 1.04f);
        }

        private void ReadState()
        {
            if (sim != null)
            {
                state.hourOfDay = SanitizeHour(sim.CurrentHour);
                state.sunElevationDeg = SanitizeElevation(sim.SunElevation);
                state.sunAzimuthDeg = SanitizeAzimuth(sim.SunAzimuth);
                sunDirection = SanitizeDirection(sim.SunDirection, DefaultSunDir());
                moonDirection = SanitizeDirection(sim.MoonDirection, DefaultMoonDir());
                humidity01 = float.IsNaN(sim.Humidity) ? 0.5f : Mathf.Clamp01(sim.Humidity);
                cloudCover01 = SkyPaletteRules.WeatherCloudProxy(sim.CurrentWeather);
            }
            else
            {
                state.hourOfDay = NeutralHour;
                state.sunElevationDeg = NeutralSunElevationDeg;
                state.sunAzimuthDeg = NeutralSunAzimuthDeg;
                sunDirection = DefaultSunDir();
                moonDirection = DefaultMoonDir();
                humidity01 = 0.5f;
                cloudCover01 = 0.15f;
            }
        }

        private static Vector3 DefaultSunDir()
        {
            return DirFromAngles(NeutralSunElevationDeg, NeutralSunAzimuthDeg);
        }

        private static Vector3 DefaultMoonDir()
        {
            return DirFromAngles(35f, 20f);
        }

        private void RotateDomes()
        {
            if (skyDomeT != null) skyDomeT.localRotation = Quaternion.Euler(0f, state.sunAzimuthDeg, 0f);
            if (starDomeT != null) starDomeT.localRotation = Quaternion.AngleAxis(state.hourOfDay * 15f, Vector3.forward);
        }

        private void PositionBillboards()
        {
            float orbit = domeRadius * 0.985f;
            if (sunT != null)
            {
                sunT.localPosition = sunDirection * orbit;
                sunT.LookAt(transform.position, Vector3.up);
                float sunScale = domeRadius * 0.085f * Mathf.Lerp(1.45f, 1f, Mathf.Clamp01(state.sunElevationDeg / 45f));
                sunT.localScale = Vector3.one * sunScale;
            }

            if (moonT != null)
            {
                moonT.localPosition = moonDirection * orbit;
                moonT.LookAt(transform.position, Vector3.up);
                moonT.localScale = Vector3.one * (domeRadius * 0.06f);
            }
        }

        private void UpdateGradientTexture()
        {
            if (gradientTexture == null) return;
            int w = gradientTexture.width;
            int h = gradientTexture.height;
            if (gradientScratch == null || gradientScratch.Length != w * h) gradientScratch = new Color[w * h];
            Color zenith = SkyPaletteRules.ZenithColor(state.hourOfDay, humidity01);
            Color horizon = SkyPaletteRules.HorizonColor(state.hourOfDay, humidity01, 0f);
            Color sunTint = SkyPaletteRules.SunTint(Mathf.Max(state.sunElevationDeg, 0f)) * 0.9f;
            float warmth = Mathf.Clamp01(1f - Mathf.Abs(state.sunElevationDeg) / 20f);
            for (int y = 0; y < h; y++)
            {
                float v = h > 1 ? (float)y / (h - 1) : 1f;
                float e = Mathf.Clamp01((v - 0.5f) * 2f);
                float vertMix = Mathf.Pow(e, 1.3f);
                for (int x = 0; x < w; x++)
                {
                    float u = w > 1 ? (float)x / (w - 1) : 0.5f;
                    float dAz = u - 0.25f;
                    dAz -= Mathf.Round(dAz);
                    float sunSide = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(dAz) * 4f), 2f);
                    Color column = Color.Lerp(horizon, sunTint, sunSide * warmth * 0.8f);
                    Color c = Color.Lerp(column, zenith, vertMix);
                    c.a = 1f;
                    gradientScratch[y * w + x] = c;
                }
            }

            gradientTexture.SetPixels(gradientScratch);
            gradientTexture.Apply(false);
        }

        private void UpdateStarAlpha()
        {
            if (starRenderer == null) return;
            MeshFilter mf = starRenderer.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            float vis = SkyPaletteRules.StarVisibility(state.hourOfDay, cloudCover01);
            Color c = Color.white;
            c.a = vis;
            Vector3[] verts = mf.sharedMesh.vertices;
            Color[] colors = new Color[verts.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = c;
            mf.sharedMesh.colors = colors;
        }

        private void UpdateBillboardTints()
        {
            float elev = state.sunElevationDeg;
            if (sunRenderer != null)
            {
                float brightness = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((elev + 6f) / 36f));
                float alpha = Mathf.Clamp01((elev + 1f) / 2.5f);
                Color c = SkyPaletteRules.SunTint(elev) * brightness;
                c.a = alpha;
                SetQuadColor(sunRenderer, c);
            }

            if (moonRenderer != null)
            {
                float nightness = Mathf.Clamp01((0.1f - SkyPaletteRules.SolarElevationProxy(state.hourOfDay)) / 0.25f);
                float alpha = Mathf.Clamp01(moonDirection.y * 4f) * nightness;
                Color c = SkyPaletteRules.MoonTint();
                c.a = Mathf.Clamp01(alpha);
                SetQuadColor(moonRenderer, c);
            }
        }
    }

    /// <summary>Internal Vector3 NaN/Infinity helpers.</summary>
    internal static class VectorExtensions
    {
        /// <summary>True when every component is finite (not NaN and not infinite).</summary>
        public static bool IsFinite(this Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                     || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
