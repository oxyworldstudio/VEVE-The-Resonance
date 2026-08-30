using UnityEngine;
using VEVE;

namespace VEVE.VFX
{
    /// <summary>
    /// Configuration for material-specific ballistic impact effects.
    /// </summary>
    [System.Serializable]
    public struct ImpactEffectConfig
    {
        [Header("Decals")]
        public Material decalMaterial;
        public float decalSize;
        public int decalCount;

        [Header("Particles")]
        public ParticleSystem particlePrefab;
        public int particleCount;

        [Header("Audio")]
        public AudioClip impactSound;
        public float volume;
    }

    /// <summary>
    /// Material-specific ballistic impact effects controller.
    /// Handles decals, particle emission, and audio trigger based on SurfaceMaterial.
    /// </summary>
    public sealed class BallisticImpactEffects : MonoBehaviour
    {
        [Header("Wood")]
        [SerializeField] private ImpactEffectConfig woodConfig;

        [Header("Concrete")]
        [SerializeField] private ImpactEffectConfig concreteConfig;

        [Header("Metal")]
        [SerializeField] private ImpactEffectConfig metalConfig;

        [Header("Glass")]
        [SerializeField] private ImpactEffectConfig glassConfig;

        [Header("Fabric")]
        [SerializeField] private ImpactEffectConfig fabricConfig;

        [Header("Dirt")]
        [SerializeField] private ImpactEffectConfig dirtConfig;

        [Header("Ice")]
        [SerializeField] private ImpactEffectConfig iceConfig;

        [Header("Pooling")]
        [SerializeField, Min(1)] private int poolSize = 32;

        private Transform effectRoot;
        private ParticleSystem[] particlePool;

        private void Awake()
        {
            effectRoot = new GameObject("ImpactEffects").transform;
            effectRoot.SetParent(transform, false);

            InitializePool();
        }

        private void InitializePool()
        {
            particlePool = new ParticleSystem[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = new GameObject($"ImpactParticle_{i}");
                go.transform.SetParent(effectRoot, false);
                go.SetActive(false);
                particlePool[i] = go.AddComponent<ParticleSystem>();
            }
        }

        /// <summary>
        /// Plays material-specific impact effects at the given point and normal.
        /// </summary>
        /// <param name="point">World-space impact point.</param>
        /// <param name="normal">Surface normal at impact point.</param>
        /// <param name="material">Surface material type.</param>
        /// <param name="energy">Impact energy for intensity scaling.</param>
        public void PlayImpact(Vector3 point, Vector3 normal, SurfaceMaterial material, float energy)
        {
            if (!TryGetConfigForMaterial(material, out ImpactEffectConfig config)) return;

            float intensity = Mathf.Clamp01(energy / 1000f);

            SpawnDecals(point, normal, config, intensity, material);
            SpawnParticles(point, normal, config, intensity);
            TriggerAudio(point, config, intensity);
        }

        private bool TryGetConfigForMaterial(SurfaceMaterial material, out ImpactEffectConfig config)
        {
            config = default;
            switch (material)
            {
                case SurfaceMaterial.Wood: config = woodConfig; break;
                case SurfaceMaterial.Concrete: config = concreteConfig; break;
                case SurfaceMaterial.Metal: config = metalConfig; break;
                case SurfaceMaterial.Glass: config = glassConfig; break;
                case SurfaceMaterial.Fabric: config = fabricConfig; break;
                case SurfaceMaterial.Dirt: config = dirtConfig; break;
                case SurfaceMaterial.Ice: config = iceConfig; break;
                default: return false;
            }
            return config.decalMaterial != null || config.particlePrefab != null;
        }

        private void SpawnDecals(Vector3 point, Vector3 normal, ImpactEffectConfig config, float intensity, SurfaceMaterial surfaceMaterial)
        {
            if (config.decalMaterial == null || config.decalCount <= 0) return;

            int count = Mathf.CeilToInt(config.decalCount * intensity);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Random.onUnitSphere * config.decalSize * 0.5f;
                Vector3 decalPoint = point + offset;
                Quaternion decalRot = Quaternion.LookRotation(-normal) * Quaternion.Euler(90f, 0f, 0f);

                GameObject decal = new GameObject($"Decal_{surfaceMaterial}");
                decal.transform.SetPositionAndRotation(decalPoint, decalRot);
                decal.transform.localScale = Vector3.one * config.decalSize;

                if (decal.TryGetComponent(out MeshRenderer renderer))
                {
                    renderer.material = config.decalMaterial;
                }
                else
                {
                    MeshFilter filter = decal.AddComponent<MeshFilter>();
                    filter.mesh = Resources.GetBuiltinResource<Mesh>("Default-Cube.fbx");
                    renderer = decal.AddComponent<MeshRenderer>();
                    renderer.material = config.decalMaterial;
                }

                DecalLifetime lifetime = decal.AddComponent<DecalLifetime>();
                lifetime.Initialize(config.decalMaterial, 8f, 20f);
            }
        }

        private void SpawnParticles(Vector3 point, Vector3 normal, ImpactEffectConfig config, float intensity)
        {
            if (config.particlePrefab == null || config.particleCount <= 0) return;

            ParticleSystem cached = GetPooledParticle();
            if (cached == null) return;

            cached.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
            cached.Emit(config.particleCount);
        }

        private ParticleSystem GetPooledParticle()
        {
            for (int i = 0; i < particlePool.Length; i++)
            {
                if (particlePool[i] != null && !particlePool[i].IsAlive())
                {
                    return particlePool[i];
                }
            }
            return null;
        }

        private void TriggerAudio(Vector3 point, ImpactEffectConfig config, float intensity)
        {
            if (config.impactSound == null) return;

            AudioSource.PlayClipAtPoint(config.impactSound, point, config.volume * intensity);
        }
    }

    /// <summary>
    /// Manages decal lifetime and fade-out to prevent accumulation.
    /// </summary>
    public sealed class DecalLifetime : MonoBehaviour
    {
        private Material material;
        private float fadeDelay;
        private float fadeDuration;
        private float elapsed;
        private bool fading;

        /// <summary>
        /// Initializes decal lifetime behavior.
        /// </summary>
        /// <param name="sourceMaterial">Material to clone for decal.</param>
        /// <param name="fadeDelay">Time in seconds before fade starts.</param>
        /// <param name="fadeDuration">Time in seconds for fade-out.</param>
        public void Initialize(Material sourceMaterial, float fadeDelay, float fadeDuration)
        {
            material = new Material(sourceMaterial);
            this.fadeDelay = fadeDelay;
            this.fadeDuration = fadeDuration;
            elapsed = 0f;

            if (TryGetComponent(out MeshRenderer renderer))
            {
                renderer.material = material;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!fading && elapsed >= fadeDelay)
            {
                fading = true;
                elapsed = 0f;
            }

            if (fading)
            {
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                material.color = new Color(material.color.r, material.color.g, material.color.b, alpha);

                if (alpha <= 0.01f)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
