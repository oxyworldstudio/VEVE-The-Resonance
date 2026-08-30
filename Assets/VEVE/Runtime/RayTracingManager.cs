using UnityEngine;
using UnityEngine.Rendering;
using VEVE.Realism;

namespace VEVE
{
    public enum RayTracingQuality { Disabled, Low, Medium, High, Ultra }

    public sealed class RayTracingManager : MonoBehaviour
    {
        [SerializeField] private RayTracingQuality quality = RayTracingQuality.Ultra;
        [SerializeField] private bool enableReflections = true;
        [SerializeField] private bool enableShadows = true;
        [SerializeField] private bool enableAmbientOcclusion = true;
        [SerializeField] private bool enableGlobalIllumination = true;
        [SerializeField] private float reflectionDistance = 500f;
        [SerializeField] private float shadowDistance = 200f;
        [SerializeField] private int reflectionSamples = 4;
        [SerializeField] private int shadowSamples = 4;
        [SerializeField] private float aoRadius = 0.5f;
        [SerializeField] private int aoSamples = 32;
        [SerializeField] private RealismConfig realismConfig;

        public RayTracingQuality Quality
        {
            get => quality;
            set
            {
                quality = value;
                ApplyQualitySettings();
            }
        }

        private void Start()
        {
            ApplyQualitySettings();
            if (realismConfig != null)
            {
                Quality = realismConfig.ForceUltraQuality ? RayTracingQuality.Ultra : RayTracingQuality.High;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) CycleQuality();
        }

        private void CycleQuality()
        {
            int count = System.Enum.GetValues(typeof(RayTracingQuality)).Length;
            int current = (int)quality;
            int next = (current + 1) % count;
            Quality = (RayTracingQuality)next;
            Debug.Log($"[RayTracing] Quality set to {quality}");
        }

        private void ApplyQualitySettings()
        {
            switch (quality)
            {
                case RayTracingQuality.Disabled:
                    SetRayTracingEnabled(false);
                    break;
                case RayTracingQuality.Low:
                    SetRayTracingEnabled(true);
                    reflectionSamples = 1;
                    shadowSamples = 1;
                    aoSamples = 8;
                    reflectionDistance = 100f;
                    shadowDistance = 50f;
                    break;
                case RayTracingQuality.Medium:
                    SetRayTracingEnabled(true);
                    reflectionSamples = 2;
                    shadowSamples = 2;
                    aoSamples = 16;
                    reflectionDistance = 200f;
                    shadowDistance = 100f;
                    break;
                case RayTracingQuality.High:
                    SetRayTracingEnabled(true);
                    reflectionSamples = 4;
                    shadowSamples = 4;
                    aoSamples = 32;
                    reflectionDistance = 500f;
                    shadowDistance = 200f;
                    break;
                case RayTracingQuality.Ultra:
                    SetRayTracingEnabled(true);
                    reflectionSamples = 8;
                    shadowSamples = 8;
                    aoSamples = 64;
                    reflectionDistance = 1000f;
                    shadowDistance = 500f;
                    break;
            }

            if (realismConfig != null)
            {
                reflectionDistance = realismConfig.ShadowDistance;
                shadowDistance = realismConfig.ShadowDistance;
            }
        }

        private void SetRayTracingEnabled(bool enabled)
        {
            if (enabled)
            {
                Debug.Log("[RayTracing] Ray tracing enabled. Ensure your render pipeline supports DXR/VK_KHR_ray_tracing.");
            }
            else
            {
                Debug.Log("[RayTracing] Ray tracing disabled.");
            }
        }
    }
}
