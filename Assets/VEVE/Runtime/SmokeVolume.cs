using UnityEngine;
using System;
using System.Collections.Generic;

namespace VEVE
{
    /// <summary>
    /// Represents a single smoke particle with physical properties.
    /// </summary>
    [Serializable]
    public struct SmokeParticle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float size;
        public float density;
        public float temperature;
        public float age;
        public float lifetime;
        public float opacity;
        public Color color;
    }

    /// <summary>
    /// Manages volumetric smoke with thermal simulation and particle-based rendering.
    /// </summary>
    public sealed class SmokeVolume : MonoBehaviour
    {
        [Header("Volume Configuration")]
        [SerializeField] private Vector3 volumeSize = new Vector3(10f, 10f, 10f);
        [SerializeField, Range(0f, 1f)] private float density = 0.7f;
        [SerializeField] private float targetDensity = 0.7f;
        [SerializeField] private float densityTransitionSpeed = 0.2f;

        [Header("Particle Configuration")]
        [SerializeField] private int maxParticles = 500;
        [SerializeField] private float particleSpawnRate = 50f;
        [SerializeField] private float particleLifetime = 10f;
        [SerializeField] private float particleSizeMin = 0.1f;
        [SerializeField] private float particleSizeMax = 0.5f;
        [SerializeField] private float particleSizeGrowth = 2f;

        [Header("Thermal Simulation")]
        [SerializeField] private float initialTemperature = 100f;
        [SerializeField] private float ambientTemperature = 20f;
        [SerializeField] private float coolingRate = 0.5f;
        [SerializeField] private float buoyancy = 2f;
        [SerializeField] private float thermalExpansion = 0.1f;

        [Header("Physics")]
        [SerializeField] private Vector3 windInfluence = Vector3.zero;
        [SerializeField] private float dragCoefficient = 0.5f;
        [SerializeField] private float turbulence = 0.3f;
        [SerializeField] private float diffusionRate = 0.1f;

        [Header("Rendering")]
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private Color smokeColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color hotSmokeColor = new Color(0.5f, 0.3f, 0.2f, 0.6f);
        [SerializeField] private float opacity = 0.5f;
        [SerializeField] private float scatteringCoefficient = 0.5f;

        [Header("Emission")]
        [SerializeField] private bool isEmitting = true;
        [SerializeField] private Vector3 emissionCenter;
        [SerializeField] private float emissionRadius = 0.5f;
        [SerializeField] private Vector3 emissionVelocity = Vector3.up;

        private List<SmokeParticle> particles;
        private float spawnAccumulator;
        private float currentDensity;
        private Mesh particleMesh;
        private ComputeBuffer particleBuffer;
        private ComputeBuffer argsBuffer;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        private static readonly int ParticleSizeStride = sizeof(float) * 20;

        /// <summary>
        /// Gets or sets the smoke density.
        /// </summary>
        public float Density
        {
            get => density;
            set
            {
                targetDensity = Mathf.Clamp01(value);
                density = targetDensity;
            }
        }

        /// <summary>
        /// Gets the current number of active particles.
        /// </summary>
        public int ActiveParticleCount => particles != null ? particles.Count : 0;

        /// <summary>
        /// Gets the current smoke opacity.
        /// </summary>
        public float CurrentOpacity => opacity * currentDensity;

        /// <summary>
        /// Gets whether the smoke is currently emitting particles.
        /// </summary>
        public bool IsEmitting => isEmitting;

        private void Awake()
        {
            particles = new List<SmokeParticle>(maxParticles);
            currentDensity = density;
            InitializeParticleMesh();
            InitializeBuffers();
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        private void Update()
        {
            UpdateDensity();
            UpdateParticles();
            UpdateRendering();
        }

        /// <summary>
        /// Initializes the particle mesh for instanced rendering.
        /// </summary>
        private void InitializeParticleMesh()
        {
            if (particleMesh != null) return;

            particleMesh = new Mesh();
            particleMesh.name = "SmokeParticleMesh";

            Vector3[] vertices = new Vector3[4];
            Vector2[] uvs = new Vector2[4];
            int[] triangles = new int[6];

            vertices[0] = new Vector3(-0.5f, -0.5f, 0f);
            vertices[1] = new Vector3(0.5f, -0.5f, 0f);
            vertices[2] = new Vector3(-0.5f, 0.5f, 0f);
            vertices[3] = new Vector3(0.5f, 0.5f, 0f);

            uvs[0] = new Vector2(0f, 0f);
            uvs[1] = new Vector2(1f, 0f);
            uvs[2] = new Vector2(0f, 1f);
            uvs[3] = new Vector2(1f, 1f);

            triangles[0] = 0;
            triangles[1] = 2;
            triangles[2] = 1;
            triangles[3] = 2;
            triangles[4] = 3;
            triangles[5] = 1;

            particleMesh.vertices = vertices;
            particleMesh.uv = uvs;
            particleMesh.triangles = triangles;
            particleMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);

            args[0] = particleMesh.GetIndexCount(0);
            args[1] = 0;
        }

        /// <summary>
        /// Initializes GPU buffers for particle rendering.
        /// </summary>
        private void InitializeBuffers()
        {
            if (particleBuffer != null) return;

            particleBuffer = new ComputeBuffer(maxParticles, ParticleSizeStride);
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        /// <summary>
        /// Releases GPU buffers.
        /// </summary>
        private void ReleaseBuffers()
        {
            particleBuffer?.Release();
            argsBuffer?.Release();
            particleBuffer = null;
            argsBuffer = null;
        }

        /// <summary>
        /// Updates the smoke density with smooth transitions.
        /// </summary>
        private void UpdateDensity()
        {
            currentDensity = Mathf.Lerp(currentDensity, targetDensity, Time.deltaTime * densityTransitionSpeed);
        }

        /// <summary>
        /// Updates all active particles.
        /// </summary>
        private void UpdateParticles()
        {
            if (particles == null) return;

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                SmokeParticle particle = particles[i];
                UpdateParticle(ref particle, Time.deltaTime);
                particles[i] = particle;

                if (particle.age >= particle.lifetime)
                {
                    particles.RemoveAt(i);
                }
            }

            if (isEmitting)
            {
                SpawnParticles();
            }
        }

        /// <summary>
        /// Updates a single particle's physics.
        /// </summary>
        private void UpdateParticle(ref SmokeParticle particle, float deltaTime)
        {
            particle.age += deltaTime;

            float normalizedAge = particle.age / particle.lifetime;
            particle.size = Mathf.Lerp(particle.size, particle.size * particleSizeGrowth, normalizedAge);

            float tempDiff = particle.temperature - ambientTemperature;
            float buoyancyForce = buoyancy * tempDiff / initialTemperature;
            particle.velocity += Vector3.up * buoyancyForce * deltaTime;

            particle.velocity += windInfluence * deltaTime;

            Vector3 turbulenceForce = new Vector3(
                Mathf.PerlinNoise(particle.position.x * 0.5f + Time.time, particle.position.z * 0.5f) - 0.5f,
                Mathf.PerlinNoise(particle.position.y * 0.5f + Time.time, particle.position.x * 0.5f) - 0.5f,
                Mathf.PerlinNoise(particle.position.z * 0.5f + Time.time, particle.position.y * 0.5f) - 0.5f
            ) * turbulence;
            particle.velocity += turbulenceForce * deltaTime;

            particle.velocity -= particle.velocity * dragCoefficient * deltaTime;

            particle.position += particle.velocity * deltaTime;

            particle.temperature = Mathf.Lerp(particle.temperature, ambientTemperature, coolingRate * deltaTime);

            particle.opacity = opacity * (1f - normalizedAge * normalizedAge);

            float tempRatio = Mathf.Clamp01((particle.temperature - ambientTemperature) / initialTemperature);
            particle.color = Color.Lerp(smokeColor, hotSmokeColor, tempRatio);
        }

        /// <summary>
        /// Spawns new particles based on emission rate.
        /// </summary>
        private void SpawnParticles()
        {
            float spawnCount = particleSpawnRate * Time.deltaTime * currentDensity;
            spawnAccumulator += spawnCount;

            while (spawnAccumulator >= 1f && particles.Count < maxParticles)
            {
                spawnAccumulator -= 1f;
                SpawnSingleParticle();
            }
        }

        /// <summary>
        /// Spawns a single smoke particle.
        /// </summary>
        private void SpawnSingleParticle()
        {
            SmokeParticle particle = new SmokeParticle();

            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-emissionRadius, emissionRadius),
                UnityEngine.Random.Range(-emissionRadius, emissionRadius),
                UnityEngine.Random.Range(-emissionRadius, emissionRadius)
            );

            particle.position = transform.position + emissionCenter + randomOffset;
            particle.velocity = emissionVelocity + new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f),
                UnityEngine.Random.Range(-0.2f, 0.2f),
                UnityEngine.Random.Range(-0.5f, 0.5f)
            );
            particle.size = UnityEngine.Random.Range(particleSizeMin, particleSizeMax);
            particle.density = currentDensity;
            particle.temperature = initialTemperature + UnityEngine.Random.Range(-10f, 10f);
            particle.age = 0f;
            particle.lifetime = particleLifetime * UnityEngine.Random.Range(0.8f, 1.2f);
            particle.opacity = opacity;
            particle.color = hotSmokeColor;

            particles.Add(particle);
        }

        /// <summary>
        /// Updates the particle rendering.
        /// </summary>
        private void UpdateRendering()
        {
            if (particles.Count == 0 || smokeMaterial == null || particleBuffer == null) return;

            SmokeParticle[] particleArray = particles.ToArray();
            particleBuffer.SetData(particleArray);

            smokeMaterial.SetBuffer("_Particles", particleBuffer);
            smokeMaterial.SetFloat("_Opacity", opacity);
            smokeMaterial.SetColor("_SmokeColor", smokeColor);
            smokeMaterial.SetFloat("_ScatteringCoeff", scatteringCoefficient);

            args[1] = (uint)particles.Count;
            argsBuffer.SetData(args);

            UnityEngine.Graphics.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                smokeMaterial,
                new Bounds(transform.position, volumeSize * 2f),
                argsBuffer
            );
        }

        /// <summary>
        /// Checks if the smoke reduces visibility between two points.
        /// </summary>
        /// <param name="observer">Observer position.</param>
        /// <param name="target">Target position.</param>
        /// <returns>True if visibility is reduced.</returns>
        public bool ReducesVisibility(Vector3 observer, Vector3 target)
        {
            Vector3 midPoint = (observer + target) * 0.5f;
            float distance = Vector3.Distance(transform.position, midPoint);
            return distance < volumeSize.magnitude * 0.5f;
        }

        /// <summary>
        /// Calculates visibility reduction factor between two points.
        /// </summary>
        /// <param name="observer">Observer position.</param>
        /// <param name="target">Target position.</param>
        /// <returns>Visibility factor (0 = no visibility, 1 = full visibility).</returns>
        public float GetVisibilityFactor(Vector3 observer, Vector3 target)
        {
            if (!ReducesVisibility(observer, target)) return 1f;

            Vector3 direction = target - observer;
            float distance = direction.magnitude;
            int sampleCount = 8;
            float totalDensity = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (i + 0.5f) / sampleCount;
                Vector3 samplePoint = observer + direction * t;
                totalDensity += SampleDensity(samplePoint);
            }

            float avgDensity = totalDensity / sampleCount;
            return Mathf.Exp(-avgDensity * scatteringCoefficient * distance);
        }

        /// <summary>
        /// Samples the smoke density at a given world position.
        /// </summary>
        /// <param name="worldPosition">World space position.</param>
        /// <returns>Smoke density at the position.</returns>
        public float SampleDensity(Vector3 worldPosition)
        {
            Vector3 localPoint = worldPosition - transform.position;
            Vector3 normalizedPoint = new Vector3(
                localPoint.x / (volumeSize.x * 0.5f),
                localPoint.y / (volumeSize.y * 0.5f),
                localPoint.z / (volumeSize.z * 0.5f)
            );

            float normalizedLength = normalizedPoint.magnitude;
            if (normalizedLength > 1f) return 0f;

            float falloff = 1f - normalizedLength;
            return currentDensity * falloff * falloff;
        }

        /// <summary>
        /// Sets the emission state.
        /// </summary>
        /// <param name="emitting">Whether to emit particles.</param>
        public void SetEmission(bool emitting)
        {
            isEmitting = emitting;
        }

        /// <summary>
        /// Sets the emission parameters.
        /// </summary>
        /// <param name="center">Emission center offset.</param>
        /// <param name="radius">Emission radius.</param>
        /// <param name="velocity">Initial emission velocity.</param>
        public void SetEmissionParameters(Vector3 center, float radius, Vector3 velocity)
        {
            emissionCenter = center;
            emissionRadius = radius;
            emissionVelocity = velocity;
        }

        /// <summary>
        /// Sets the wind influence on smoke particles.
        /// </summary>
        /// <param name="wind">Wind vector.</param>
        public void SetWind(Vector3 wind)
        {
            windInfluence = wind;
        }

        /// <summary>
        /// Sets the initial temperature of emitted smoke.
        /// </summary>
        /// <param name="temperature">Temperature in Celsius.</param>
        public void SetTemperature(float temperature)
        {
            initialTemperature = temperature;
        }

        /// <summary>
        /// Clears all active particles.
        /// </summary>
        public void ClearParticles()
        {
            particles?.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0.3f);
            Gizmos.DrawCube(transform.position, volumeSize);
            Gizmos.color = smokeColor;
            Gizmos.DrawWireCube(transform.position, volumeSize);

            if (Application.isPlaying && particles != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var particle in particles)
                {
                    Gizmos.DrawSphere(particle.position, particle.size * 0.1f);
                }
            }
        }
    }
}
