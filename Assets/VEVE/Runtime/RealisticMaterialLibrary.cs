using UnityEngine;
using System.Collections.Generic;

namespace VEVE
{
    public enum MaterialPreset { Wood, Concrete, Metal, Glass, Fabric, Dirt, Ice, Water, Brick, Drywall, Sand, Snow }

    public sealed class RealisticMaterialLibrary : ScriptableObject
    {
        [SerializeField] private List<MaterialPreset> presets = new();
        [SerializeField] private List<float> densities = new();
        [SerializeField] private List<float> ballisticResistances = new();
        [SerializeField] private List<float> acousticAbsorptions = new();
        [SerializeField] private List<float> reflectivities = new();
        [SerializeField] private List<float> frictions = new();
        [SerializeField] private List<float> thicknesses = new();
        [SerializeField] private List<Color> albedos = new();

        public bool TryGetMaterialData(MaterialPreset preset, out float density, out float ballisticResistance, out float acousticAbsorption, out float reflectivity, out float friction, out float thickness, out Color albedo)
        {
            int index = presets.IndexOf(preset);
            if (index >= 0 && index < densities.Count)
            {
                density = densities[index];
                ballisticResistance = ballisticResistances[index];
                acousticAbsorption = acousticAbsorptions[index];
                reflectivity = reflectivities[index];
                friction = frictions[index];
                thickness = thicknesses[index];
                albedo = albedos[index];
                return true;
            }

            density = 1f;
            ballisticResistance = 1f;
            acousticAbsorption = 0.2f;
            reflectivity = 0.3f;
            friction = 0.8f;
            thickness = 0.1f;
            albedo = Color.white;
            return false;
        }

        public static float GetPresetDensity(MaterialPreset preset)
        {
            return preset switch
            {
                MaterialPreset.Wood => 0.6f,
                MaterialPreset.Concrete => 2.4f,
                MaterialPreset.Metal => 7.8f,
                MaterialPreset.Glass => 2.5f,
                MaterialPreset.Fabric => 0.2f,
                MaterialPreset.Dirt => 1.5f,
                MaterialPreset.Ice => 0.9f,
                MaterialPreset.Water => 1.0f,
                MaterialPreset.Brick => 1.8f,
                MaterialPreset.Drywall => 0.8f,
                MaterialPreset.Sand => 1.6f,
                MaterialPreset.Snow => 0.2f,
                _ => 1.0f,
            };
        }

        public static float GetPresetBallisticResistance(MaterialPreset preset)
        {
            return preset switch
            {
                MaterialPreset.Wood => 35f,
                MaterialPreset.Concrete => 80f,
                MaterialPreset.Metal => 120f,
                MaterialPreset.Glass => 15f,
                MaterialPreset.Fabric => 8f,
                MaterialPreset.Dirt => 20f,
                MaterialPreset.Ice => 10f,
                MaterialPreset.Water => 5f,
                MaterialPreset.Brick => 90f,
                MaterialPreset.Drywall => 12f,
                MaterialPreset.Sand => 18f,
                MaterialPreset.Snow => 6f,
                _ => 35f,
            };
        }

        public static float GetPresetAcousticAbsorption(MaterialPreset preset)
        {
            return preset switch
            {
                MaterialPreset.Wood => 0.4f,
                MaterialPreset.Concrete => 0.15f,
                MaterialPreset.Metal => 0.1f,
                MaterialPreset.Glass => 0.05f,
                MaterialPreset.Fabric => 0.7f,
                MaterialPreset.Dirt => 0.5f,
                MaterialPreset.Ice => 0.03f,
                MaterialPreset.Water => 0.2f,
                MaterialPreset.Brick => 0.3f,
                MaterialPreset.Drywall => 0.4f,
                MaterialPreset.Sand => 0.6f,
                MaterialPreset.Snow => 0.8f,
                _ => 0.5f,
            };
        }

        public static Color GetPresetAlbedo(MaterialPreset preset)
        {
            return preset switch
            {
                MaterialPreset.Wood => new Color(0.35f, 0.16f, 0.06f),
                MaterialPreset.Concrete => new Color(0.45f, 0.45f, 0.45f),
                MaterialPreset.Metal => new Color(0.6f, 0.6f, 0.65f),
                MaterialPreset.Glass => new Color(0.9f, 0.95f, 1.0f),
                MaterialPreset.Fabric => new Color(0.3f, 0.2f, 0.25f),
                MaterialPreset.Dirt => new Color(0.3f, 0.2f, 0.1f),
                MaterialPreset.Ice => new Color(0.8f, 0.9f, 1.0f),
                MaterialPreset.Water => new Color(0.1f, 0.3f, 0.8f),
                MaterialPreset.Brick => new Color(0.5f, 0.25f, 0.2f),
                MaterialPreset.Drywall => new Color(0.8f, 0.8f, 0.75f),
                MaterialPreset.Sand => new Color(0.76f, 0.7f, 0.5f),
                MaterialPreset.Snow => new Color(0.95f, 0.95f, 1.0f),
                _ => Color.white,
            };
        }
    }
}
