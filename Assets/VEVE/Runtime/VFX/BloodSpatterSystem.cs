using UnityEngine;
using VEVE;

namespace VEVE.VFX
{
    /// <summary>
    /// Configuration for directional blood spray effects.
    /// </summary>
    [System.Serializable]
    public struct BloodSpatterConfig
    {
        [Header("Spray")]
        public ParticleSystem sprayPrefab;
        public float sprayConeAngle;
        public int sprayCount;

        [Header("Droplets")]
        public GameObject dropletPrefab;
        public int maxDroplets;
        public float dropletSpeed;
        public float dropletMass;
        public float dropletDrag;

        [Header("Decals")]
        public Material bloodDecalMaterial;
        public float decalSize;
        public int decalCount;

        [Header("Surface Interaction")]
        public float surfaceThreshold;
        public float dripDelay;
    }

    /// <summary>
    /// Directional blood spray with physics-based droplets, decal projection, and surface material interaction.
    /// </summary>
    public sealed class BloodSpatterSystem : MonoBehaviour
    {
        [Header("Blood Settings")]
        [SerializeField] private BloodSpatterConfig config;

        [Header("Pooling")]
        [SerializeField, Min(1)] private int dropletPoolSize = 64;

        private GameObject[] dropletPool;
        private Transform bloodRoot;
        private SurfaceMaterial lastSurface;

        private void Awake()
        {
            bloodRoot = new GameObject("BloodSpatter").transform;
            bloodRoot.SetParent(transform, false);

            InitializePool();
        }

        private void InitializePool()
        {
            dropletPool = new GameObject[dropletPoolSize];
            for (int i = 0; i < dropletPoolSize; i++)
            {
                GameObject go = new GameObject($"BloodDroplet_{i}");
                go.transform.SetParent(bloodRoot, false);
                go.SetActive(false);

                if (config.dropletPrefab != null)
                {
                    Instantiate(config.dropletPrefab, go.transform);
                }
                else
                {
                    SphereCollider col = go.AddComponent<SphereCollider>();
                    col.radius = 0.01f;
                    col.isTrigger = true;

                    Rigidbody rb = go.AddComponent<Rigidbody>();
                    rb.mass = config.dropletMass;
                    rb.linearDamping = config.dropletDrag;
                    rb.angularDamping = 0.5f;
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                BloodDroplet droplet = go.AddComponent<BloodDroplet>();
                droplet.Initialize(config);
            }
        }

        /// <summary>
        /// Emits blood spatter in the specified direction.
        /// </summary>
        /// <param name="origin">Blood emission origin.</param>
        /// <param name="direction">Direction of blood spray.</param>
        /// <param name="surface">Hit surface material for interaction.</param>
        /// <param name="energy">Impact energy for spray intensity.</param>
        public void EmitSpray(Vector3 origin, Vector3 direction, SurfaceMaterial surface, float energy)
        {
            lastSurface = surface;
            float intensity = Mathf.Clamp01(energy / 500f);

            PlaySprayParticles(origin, direction, intensity);
            EmitDroplets(origin, direction, intensity);
            ProjectDecals(origin, direction, surface, intensity);
        }

        private void PlaySprayParticles(Vector3 origin, Vector3 direction, float intensity)
        {
            if (config.sprayPrefab == null) return;

            ParticleSystem spray = Instantiate(config.sprayPrefab, origin, Quaternion.LookRotation(direction));
            var main = spray.main;
            main.startSpeedMultiplier = intensity;
            spray.Emit(Mathf.CeilToInt(config.sprayCount * intensity));
            Destroy(spray.gameObject, 2f);
        }

        private void EmitDroplets(Vector3 origin, Vector3 direction, float intensity)
        {
            int count = Mathf.CeilToInt(config.maxDroplets * intensity);
            int emitted = 0;

            for (int i = 0; i < dropletPool.Length && emitted < count; i++)
            {
                if (dropletPool[i] != null && !dropletPool[i].activeSelf)
                {
                    GameObject droplet = dropletPool[i];
                    droplet.transform.SetPositionAndRotation(origin, Random.rotation);
                    droplet.SetActive(true);

                    BloodDroplet script = droplet.GetComponent<BloodDroplet>();
                    script.Launch(direction, config.sprayConeAngle, config.dropletSpeed);

                    emitted++;
                }
            }
        }

        private void ProjectDecals(Vector3 origin, Vector3 direction, SurfaceMaterial surface, float intensity)
        {
            if (config.bloodDecalMaterial == null || config.decalCount <= 0) return;

            int count = Mathf.CeilToInt(config.decalCount * intensity);
            for (int i = 0; i < count; i++)
            {
                if (Physics.Raycast(origin, direction + Random.onUnitSphere * 0.5f, out RaycastHit hit, 5f))
                {
                    Vector3 decalPoint = hit.point + hit.normal * 0.005f;
                    Quaternion decalRot = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(90f, 0f, 0f);

                    GameObject decal = new GameObject("BloodDecal");
                    decal.transform.SetPositionAndRotation(decalPoint, decalRot);
                    decal.transform.localScale = Vector3.one * config.decalSize * Random.Range(0.5f, 1.5f);

                    if (decal.TryGetComponent(out MeshRenderer renderer))
                    {
                        renderer.material = config.bloodDecalMaterial;
                    }
                    else
                    {
                        MeshFilter filter = decal.AddComponent<MeshFilter>();
                        filter.mesh = Resources.GetBuiltinResource<Mesh>("Default-Cube.fbx");
                        renderer = decal.AddComponent<MeshRenderer>();
                        renderer.material = config.bloodDecalMaterial;
                    }

                    if (surface == SurfaceMaterial.Fabric || surface == SurfaceMaterial.Dirt)
                    {
                        DecalLifetime lifetime = decal.AddComponent<DecalLifetime>();
                        lifetime.Initialize(config.bloodDecalMaterial, config.dripDelay, 6f);
                    }
                    else
                    {
                        DecalLifetime lifetime = decal.AddComponent<DecalLifetime>();
                        lifetime.Initialize(config.bloodDecalMaterial, 15f, 10f);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the last surface material hit by blood spray.
        /// </summary>
        public SurfaceMaterial LastSurface => lastSurface;
    }

    /// <summary>
    /// Physics-based blood droplet with pooling support.
    /// </summary>
    public sealed class BloodDroplet : MonoBehaviour
    {
        private BloodSpatterConfig config;
        private Rigidbody rb;
        private float lifetime;

        /// <summary>
        /// Initializes the droplet with configuration data.
        /// </summary>
        /// <param name="config">Blood spatter configuration.</param>
        public void Initialize(BloodSpatterConfig config)
        {
            this.config = config;
            rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Launches the droplet in the specified direction with cone spread.
        /// </summary>
        /// <param name="baseDirection">Base spray direction.</param>
        /// <param name="coneAngle">Cone spread angle in degrees.</param>
        /// <param name="speed">Initial launch speed.</param>
        public void Launch(Vector3 baseDirection, float coneAngle, float speed)
        {
            Quaternion spread = Quaternion.Euler(
                Random.Range(-coneAngle, coneAngle),
                Random.Range(-coneAngle, coneAngle),
                Random.Range(-coneAngle, coneAngle)
            );

            Vector3 velocity = spread * baseDirection * speed * Random.Range(0.7f, 1.3f);
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.onUnitSphere * Random.Range(2f, 10f);

            lifetime = 3f;
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                StickerEffect(contact.point, contact.normal);
                gameObject.SetActive(false);
            }
        }

        private void StickerEffect(Vector3 point, Vector3 normal)
        {
            if (config.bloodDecalMaterial == null) return;

            GameObject miniDecal = new GameObject("BloodMiniDecal");
            miniDecal.transform.SetPositionAndRotation(point + normal * 0.003f, Quaternion.LookRotation(-normal) * Quaternion.Euler(90f, 0f, 0f));
            miniDecal.transform.localScale = Vector3.one * config.decalSize * Random.Range(0.2f, 0.6f);

            MeshRenderer renderer = miniDecal.AddComponent<MeshRenderer>();
            renderer.material = config.bloodDecalMaterial;

            DecalLifetime lifetime = miniDecal.AddComponent<DecalLifetime>();
            lifetime.Initialize(config.bloodDecalMaterial, 8f, 5f);
        }
    }
}
