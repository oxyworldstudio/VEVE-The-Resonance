using UnityEngine;

namespace VEVE
{
    [CreateAssetMenu(menuName = "VEVE/Simulation/Material Definition")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        [SerializeField] private SurfaceMaterial materialType;
        [SerializeField, Min(0.01f)] private float density = 1f;
        [SerializeField, Min(0f)] private float ballisticResistance = 1f;
        [SerializeField, Min(0f)] private float acousticAbsorption = 0.2f;
        [SerializeField, Range(0f, 1f)] private float lightTransmission;
        [SerializeField, Range(0f, 1f)] private float reflectivity = 0.3f;
        [SerializeField, Range(0f, 1f)] private float friction = 0.8f;
        [SerializeField, Min(0f)] private float thickness = 0.1f;

        public SurfaceMaterial MaterialType => materialType;
        public float Density => density;
        public float BallisticResistance => ballisticResistance;
        public float AcousticAbsorption => acousticAbsorption;
        public float LightTransmission => lightTransmission;
        public float Reflectivity => reflectivity;
        public float Friction => friction;
        public float Thickness => thickness;

        public float RemainingEnergy(float incomingEnergy)
        {
            return Mathf.Max(0f, incomingEnergy - ballisticResistance * density * Mathf.Max(0f, thickness));
        }

        public static float GetResistance(SurfaceMaterial material)
        {
            return material switch
            {
                SurfaceMaterial.Wood => 35f,
                SurfaceMaterial.Concrete => 80f,
                SurfaceMaterial.Metal => 120f,
                SurfaceMaterial.Glass => 15f,
                SurfaceMaterial.Fabric => 8f,
                SurfaceMaterial.Dirt => 20f,
                SurfaceMaterial.Ice => 10f,
                _ => 35f,
            };
        }

        public static float GetAbsorption(SurfaceMaterial material)
        {
            return material switch
            {
                SurfaceMaterial.Wood => 0.4f,
                SurfaceMaterial.Concrete => 0.15f,
                SurfaceMaterial.Metal => 0.1f,
                SurfaceMaterial.Glass => 0.05f,
                SurfaceMaterial.Fabric => 0.7f,
                SurfaceMaterial.Dirt => 0.5f,
                SurfaceMaterial.Ice => 0.03f,
                _ => 0.5f,
            };
        }
    }
}
