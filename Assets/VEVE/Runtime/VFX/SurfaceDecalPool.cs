using UnityEngine;
using UnityEngine.Rendering;
using VEVE.Graphics;
using QualityLevel = VEVE.Realism.QualityLevel;

namespace VEVE.VFX
{
    /// <summary>
    /// Pooled quad decals for persistent surface wear (bullet holes, blood,
    /// scorch, chips). Every decal is a pooled quad with a procedural alpha
    /// texture from <see cref="DecalTextureFactory"/>, positioned along the hit
    /// normal, oriented with deterministic jitter and tinted through
    /// <see cref="SurfaceArtRules"/> + <see cref="DecalPoolRules.ColorFor"/>.
    /// The pool is a strict FIFO capacity ring: when full the OLDEST decal slot
    /// is recycled; slots fade out over <see cref="DecalPoolRules.FadeSecondsFor"/>
    /// and are released by <see cref="FadeAndRelease(float)"/> (called from
    /// <see cref="Update"/> at runtime, or directly from tests/logic ticks).
    /// No DontDestroyOnLoad, no external packages, no binary assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceDecalPool : MonoBehaviour
    {
        /// <summary>World-space gap between decal quad and surface (z-fighting guard).</summary>
        public const float SurfaceOffsetMeters = 0.01f;
        /// <summary>Fraction of the fade lifetime spent ramping alpha down.</summary>
        public const float FadeTailFraction = 0.30f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private QualityLevel quality = QualityLevel.Medium;

        private GameObject root;
        private Mesh quadMesh;
        private readonly Material[] kindMaterials = new Material[4];
        private Slot[] slots;
        private int head;
        private int count;
        private int placedTotal;
        private MaterialPropertyBlock mpb;
        private bool disposed;

        private sealed class Slot
        {
            public GameObject go;
            public MeshRenderer renderer;
            public DecalKind kind;
            public float age;
            public Color tint;
            public bool fading;
        }

        /// <summary>Quality tier driving the ring capacity (<see cref="DecalPoolRules.CapacityFor"/>). Changing it rebuilds the pool.</summary>
        public QualityLevel Quality
        {
            get { return quality; }
            set
            {
                if (quality == value) return;
                quality = value;
                if (slots != null) EnsureInitialized();
            }
        }

        /// <summary>Maximum simultaneous decals (from <see cref="DecalPoolRules.CapacityFor(QualityLevel)"/>).</summary>
        public int Capacity { get { return DecalPoolRules.CapacityFor(quality); } }

        /// <summary>Number of currently live (placed, not yet faded-out) decals.</summary>
        public int ActiveCount { get { return count; } }

        /// <summary>
        /// Resolves the transform of the decal currently living in ring slot
        /// <paramref name="index"/> (null when the slot was never placed or the
        /// pool is not initialized). Read-only bridge for integration wiring and tests.
        /// </summary>
        public bool TryGetDecalTransform(int index, out Transform decalTransform)
        {
            decalTransform = null;
            if (slots == null || index < 0 || index >= slots.Length) return false;
            decalTransform = slots[index].go != null ? slots[index].go.transform : null;
            return decalTransform != null;
        }

        /// <summary>
        /// Places a decal of <paramref name="kind"/> at <paramref name="position"/>
        /// offset <see cref="SurfaceOffsetMeters"/> along <paramref name="normal"/>,
        /// oriented to face outward with deterministic rotation/scale jitter, tinted
        /// by the surface kind resolved through <see cref="SurfaceArtRules.ResolveKey"/>.
        /// Returns the ring slot index (0..Capacity-1), or -1 when the component is
        /// disabled or the pool cannot hold decals. When the ring is full the OLDEST
        /// slot is recycled immediately (its index is returned and reborn as the new decal).
        /// </summary>
        public int Place(DecalKind kind, Vector3 position, Vector3 normal, string surfaceKindName)
        {
            if (!enabled) return -1;
            int capacity = Capacity;
            if (capacity <= 0) return -1;
            EnsureInitialized();
            if (slots == null) return -1;

            int index;
            if (count < slots.Length)
            {
                index = (head + count) % slots.Length;
                count++;
            }
            else
            {
                index = head;
                head = (head + 1) % slots.Length;
            }

            Slot slot = slots[index];
            int seed = DecalPoolRules.InstanceSeed(kind, placedTotal);
            placedTotal++;
            float scale = DecalPoolRules.ScaleFor(kind, seed);
            float rotDeg = DecalPoolRules.RotationFor(kind, seed);

            Vector3 n = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            Vector3 reference = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 tangent = Vector3.Cross(reference, n).normalized;
            if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.forward;
            Quaternion orientation = Quaternion.LookRotation(n, tangent) * Quaternion.AngleAxis(rotDeg, Vector3.forward);

            slot.kind = kind;
            if (slot.renderer != null) slot.renderer.sharedMaterial = GetOrCreateKindMaterial(kind);
            slot.age = 0f;
            slot.fading = false;
            slot.tint = DecalPoolRules.ColorFor(kind, ResolveBaseColor(surfaceKindName));

            slot.go.transform.SetPositionAndRotation(position + n * SurfaceOffsetMeters, orientation);
            slot.go.transform.localScale = Vector3.one * scale;
            ApplyColor(slot, slot.tint);
            slot.go.SetActive(true);
            return index;
        }

        /// <summary>
        /// Advances fade timers and releases fully-faded oldest decals (FIFO).
        /// Alpha ramps from 1 to 0 during the last <see cref="FadeTailFraction"/>
        /// of the kind's lifetime. Negative or zero dt is ignored. Safe to call
        /// from <see cref="Update"/> or manually from tests/logic ticks.
        /// </summary>
        public void FadeAndRelease(float dt)
        {
            if (slots == null || dt <= 0f) return;
            for (int i = 0; i < count; i++)
            {
                Slot slot = slots[(head + i) % slots.Length];
                slot.age += dt;
                float fade = DecalPoolRules.FadeSecondsFor(slot.kind);
                float fadeStart = fade * (1f - FadeTailFraction);
                if (!slot.fading && slot.age >= fadeStart)
                {
                    slot.fading = true;
                    ApplyColor(slot, TintWithAlpha(slot));
                }
                else if (slot.fading)
                {
                    ApplyColor(slot, TintWithAlpha(slot));
                }
            }
            while (count > 0)
            {
                Slot oldest = slots[head];
                if (oldest.age + 0.001f < DecalPoolRules.FadeSecondsFor(oldest.kind)) break;
                oldest.go.SetActive(false);
                oldest.age = 0f;
                oldest.fading = false;
                head = (head + 1) % slots.Length;
                count--;
            }
        }

        private void Update()
        {
            FadeAndRelease(Time.deltaTime);
        }

        private void OnDestroy()
        {
            DisposeResources();
        }

        private void EnsureInitialized()
        {
            if (disposed) return;
            int capacity = Capacity;
            if (capacity <= 0) return;
            if (slots != null && slots.Length == capacity && root != null) return;
            DisposeResources();
            disposed = false;

            root = new GameObject("VEVE_SurfaceDecals");
            root.transform.SetParent(transform, false);
            quadMesh = BuildQuadMesh();
            slots = new Slot[capacity];
            Material material = GetOrCreateKindMaterial(DecalKind.BulletHole);
            for (int i = 0; i < capacity; i++)
            {
                GameObject go = new GameObject("Decal_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                go.transform.SetParent(root.transform, false);
                go.SetActive(false);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = quadMesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                slots[i] = new Slot { go = go, renderer = renderer };
            }
            head = 0;
            count = 0;
        }

        private void DisposeResources()
        {
            disposed = true;
            if (root != null) SafeDestroy(root);
            root = null;
            if (quadMesh != null) SafeDestroy(quadMesh);
            quadMesh = null;
            for (int i = 0; i < kindMaterials.Length; i++)
            {
                if (kindMaterials[i] != null) SafeDestroy(kindMaterials[i]);
                kindMaterials[i] = null;
            }
            slots = null;
            head = 0;
            count = 0;
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        private static Mesh BuildQuadMesh()
        {
            Mesh mesh = new Mesh { name = "VEVE_DecalQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material GetOrCreateKindMaterial(DecalKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= kindMaterials.Length) return null;
            if (kindMaterials[i] != null) return kindMaterials[i];
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) return null;
            Material material = new Material(shader) { name = "VEVE_DecalMat_" + kind };
            material.mainTexture = DecalTextureFactory.GetTextureFor(kind);
            kindMaterials[i] = material;
            return material;
        }

        private static Color ResolveBaseColor(string surfaceKindName)
        {
            string key = SurfaceArtRules.ResolveKey(surfaceKindName);
            if (SurfaceArtRules.TryPalette(key, out SurfaceArtRules.Palette palette)) return palette.baseColor;
            return Color.white;
        }

        private Color TintWithAlpha(Slot slot)
        {
            float fade = DecalPoolRules.FadeSecondsFor(slot.kind);
            float tail = Mathf.Max(0.01f, fade * FadeTailFraction);
            float alpha = Mathf.Clamp01((fade - slot.age) / tail);
            Color c = slot.tint;
            c.a = Mathf.Clamp01(slot.tint.a * alpha);
            return c;
        }

        private void ApplyColor(Slot slot, Color color)
        {
            MeshRenderer renderer = slot.renderer;
            if (renderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetColor(ColorId, color);
            renderer.SetPropertyBlock(mpb);
        }
    }
}
