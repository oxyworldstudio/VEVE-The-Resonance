using UnityEngine;
using VEVE;

namespace VEVE.VFX
{
    /// <summary>
    /// Configuration for dust and debris environmental effects.
    /// </summary>
    [System.Serializable]
    public struct DustDebrisConfig
    {
        [Header("Footsteps")]
        public ParticleSystem dustPuffPrefab;
        public float dustScale;
        public int dustParticles;

        [Header("Explosions")]
        public ParticleSystem explosionDustPrefab;
        public ParticleSystem debrisPrefab;
        public int debrisCount;
        public float debrisLifetime;

        [Header("Destruction")]
        public GameObject debrisChunkPrefab;
        public int maxChunks;
        public float chunkLifetime;
        public float chunkExplosionForce;

        [Header("Environment")]
        public float globalDustDensity;
        public float windInfluence;
    }

    /// <summary>
    /// Environmental particle systems for footsteps, explosions, and material destruction.
    /// </summary>
    public sealed class DustAndDebrisSystem : MonoBehaviour
    {
        [Header("Surface Profiles")]
        [SerializeField] private DustDebrisConfig concreteConfig;
        [SerializeField] private DustDebrisConfig woodConfig;
        [SerializeField] private DustDebrisConfig dirtConfig;
        [SerializeField] private DustDebrisConfig metalConfig;
        [SerializeField] private DustDebrisConfig glassConfig;

        [Header("Pooling")]
        [SerializeField, Min(1)] private int debrisPoolSize = 32;
        [SerializeField, Min(1)] private int dustPoolSize = 24;

        private GameObject[] debrisPool;
        private ParticleSystem[] dustPool;
        private Transform fxRoot;

        private void Awake()
        {
            fxRoot = new GameObject("DustDebris").transform;
            fxRoot.SetParent(transform, false);

            InitializePools();
        }

        private void InitializePools()
        {
            debrisPool = new GameObject[debrisPoolSize];
            dustPool = new ParticleSystem[dustPoolSize];

            for (int i = 0; i < debrisPoolSize; i++)
            {
                GameObject go = new GameObject($"Debris_{i}");
                go.transform.SetParent(fxRoot, false);
                go.SetActive(false);

                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.mass = Random.Range(0.05f, 0.5f);
                rb.linearDamping = 0.3f;
                rb.angularDamping = 0.5f;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                if (glassConfig.debrisChunkPrefab != null)
                {
                    Instantiate(glassConfig.debrisChunkPrefab, go.transform);
                }
                else
                {
                    MeshFilter filter = go.AddComponent<MeshFilter>();
                    filter.mesh = Resources.GetBuiltinResource<Mesh>("Default-Cube.fbx");
                    MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                    renderer.material = Resources.GetBuiltinResource<Material>("Default-Diffuse.mat");
                }

                debrisPool[i] = go;
            }

            for (int i = 0; i < dustPoolSize; i++)
            {
                GameObject go = new GameObject($"Dust_{i}");
                go.transform.SetParent(fxRoot, false);
                go.SetActive(false);
                dustPool[i] = go.AddComponent<ParticleSystem>();
            }
        }

        /// <summary>
        /// Emits dust effect for footsteps on the given surface.
        /// </summary>
        /// <param name="point">Foot contact point.</param>
        /// <param name="normal">Surface normal.</param>
        /// <param name="surface">Surface material type.</param>
        /// <param name="intensity">Step intensity for scaling.</param>
        public void EmitFootstepDust(Vector3 point, Vector3 normal, SurfaceMaterial surface, float intensity)
        {
            DustDebrisConfig config = GetConfigForMaterial(surface);
            if (config.dustPuffPrefab == null) return;

            ParticleSystem dust = GetPooledDust();
            if (dust == null) return;

            dust.transform.SetPositionAndRotation(point + Vector3.up * 0.05f, Quaternion.LookRotation(normal));
            var main = dust.main;
            main.startSizeMultiplier = config.dustScale * intensity;
            dust.Emit(Mathf.CeilToInt(config.dustParticles * intensity));
        }

        /// <summary>
        /// Emits explosion dust and debris at the specified point.
        /// </summary>
        /// <param name="point">Explosion center.</param>
        /// <param name="surface">Surface material type.</param>
        /// <param name="force">Explosion force for debris scaling.</param>
        public void EmitExplosion(Vector3 point, SurfaceMaterial surface, float force)
        {
            DustDebrisConfig config = GetConfigForMaterial(surface);
            float intensity = Mathf.Clamp01(force / 500f);

            if (config.explosionDustPrefab != null)
            {
                ParticleSystem dust = Instantiate(config.explosionDustPrefab, point, Quaternion.identity);
                var main = dust.main;
                main.startSpeedMultiplier = intensity;
                dust.Play();
                Destroy(dust.gameObject, 4f);
            }

            if (config.debrisPrefab != null)
            {
                ParticleSystem debris = Instantiate(config.debrisPrefab, point, Quaternion.identity);
                var main = debris.main;
                main.startSpeedMultiplier = intensity * 1.5f;
                debris.Emit(Mathf.CeilToInt(config.debrisCount * intensity));
                Destroy(debris.gameObject, config.debrisLifetime);
            }

            LaunchDebrisChunks(point, config, force);
        }

        /// <summary>
        /// Emits destruction debris for the specified material.
        /// </summary>
        /// <param name="point">Destruction origin.</param>
        /// <param name="normal">Surface normal.</param>
        /// <param name="surface">Surface material type.</param>
        /// <param name="force">Destruction force magnitude.</param>
        public void EmitDestruction(Vector3 point, Vector3 normal, SurfaceMaterial surface, float force)
        {
            DustDebrisConfig config = GetConfigForMaterial(surface);
            float intensity = Mathf.Clamp01(force / 200f);

            int count = Mathf.CeilToInt(config.maxChunks * intensity);
            int emitted = 0;

            for (int i = 0; i < debrisPool.Length && emitted < count; i++)
            {
                if (debrisPool[i] != null && !debrisPool[i].activeSelf)
                {
                    GameObject chunk = debrisPool[i];
                    chunk.transform.SetPositionAndRotation(point + normal * 0.1f, Random.rotation);
                    chunk.SetActive(true);

                    Rigidbody rb = chunk.GetComponent<Rigidbody>();
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.AddForce((normal + Vector3.up * 0.5f + Random.onUnitSphere * 0.5f).normalized * config.chunkExplosionForce * intensity, ForceMode.Impulse);

                    Destroy(chunk, config.chunkLifetime);
                    emitted++;
                }
            }
        }

        private DustDebrisConfig GetConfigForMaterial(SurfaceMaterial surface)
        {
            return surface switch
            {
                SurfaceMaterial.Concrete => concreteConfig,
                SurfaceMaterial.Wood => woodConfig,
                SurfaceMaterial.Dirt => dirtConfig,
                SurfaceMaterial.Metal => metalConfig,
                SurfaceMaterial.Glass => glassConfig,
                _ => concreteConfig
            };
        }

        private void LaunchDebrisChunks(Vector3 point, DustDebrisConfig config, float force)
        {
            if (config.debrisChunkPrefab == null) return;

            int count = Mathf.CeilToInt(config.maxChunks * 0.5f);
            int emitted = 0;

            for (int i = 0; i < debrisPool.Length && emitted < count; i++)
            {
                if (debrisPool[i] != null && !debrisPool[i].activeSelf)
                {
                    GameObject chunk = debrisPool[i];
                    chunk.transform.SetPositionAndRotation(point + Random.onUnitSphere * 0.5f, Random.rotation);
                    chunk.SetActive(true);

                    Rigidbody rb = chunk.GetComponent<Rigidbody>();
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.AddExplosionForce(config.chunkExplosionForce * force * 0.01f, point, 5f, 1f, ForceMode.Impulse);

                    Destroy(chunk, config.chunkLifetime);
                    emitted++;
                }
            }
        }

        private ParticleSystem GetPooledDust()
        {
            for (int i = 0; i < dustPool.Length; i++)
            {
                if (dustPool[i] != null && !dustPool[i].IsAlive())
                {
                    return dustPool[i];
                }
            }
            return null;
        }
    }
}
