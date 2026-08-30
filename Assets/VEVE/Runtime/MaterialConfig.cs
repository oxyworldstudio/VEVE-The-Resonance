using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    [CreateAssetMenu(menuName = "VEVE/Realism/Material Configuration")]
    public sealed class MaterialConfig : ScriptableObject
    {
        [SerializeField] private RealismConfig realismConfig;

        [System.Serializable]
        public struct MaterialProperties
        {
            public string name;
            public float density;
            public float yieldStrength;
            public float tensileStrength;
            public float hardness;
            public float acousticImpedance;
            public float thermalConductivity;
            public float specificHeatCapacity;
            public float youngsModulus;
            public float poissonsRatio;
        }

        private static readonly MaterialProperties[] materials = new MaterialProperties[]
        {
            new MaterialProperties
            {
                name = "Steel",
                density = 7850f,
                yieldStrength = 250e6f,
                tensileStrength = 400e6f,
                hardness = 4.5f,
                acousticImpedance = 4.7e7f,
                thermalConductivity = 50f,
                specificHeatCapacity = 490f,
                youngsModulus = 200e9f,
                poissonsRatio = 0.3f
            },
            new MaterialProperties
            {
                name = "Lead",
                density = 11340f,
                yieldStrength = 12e6f,
                tensileStrength = 17e6f,
                hardness = 1.5f,
                acousticImpedance = 1.96e7f,
                thermalConductivity = 35f,
                specificHeatCapacity = 128f,
                youngsModulus = 16e9f,
                poissonsRatio = 0.44f
            },
            new MaterialProperties
            {
                name = "Concrete",
                density = 2400f,
                yieldStrength = 30e6f,
                tensileStrength = 3e6f,
                hardness = 3.0f,
                acousticImpedance = 1.08e7f,
                thermalConductivity = 1.4f,
                specificHeatCapacity = 880f,
                youngsModulus = 30e9f,
                poissonsRatio = 0.2f
            },
            new MaterialProperties
            {
                name = "Wood",
                density = 600f,
                yieldStrength = 40e6f,
                tensileStrength = 80e6f,
                hardness = 2.0f,
                acousticImpedance = 2.7e6f,
                thermalConductivity = 0.15f,
                specificHeatCapacity = 1700f,
                youngsModulus = 10e9f,
                poissonsRatio = 0.3f
            },
            new MaterialProperties
            {
                name = "Glass",
                density = 2500f,
                yieldStrength = 7e6f,
                tensileStrength = 7e6f,
                hardness = 5.5f,
                acousticImpedance = 1.3e7f,
                thermalConductivity = 1.0f,
                specificHeatCapacity = 700f,
                youngsModulus = 70e9f,
                poissonsRatio = 0.23f
            }
        };

        public bool TryGetMaterial(string name, out MaterialProperties properties)
        {
            foreach (var mat in materials)
            {
                if (mat.name == name)
                {
                    properties = mat;
                    return true;
                }
            }

            properties = default;
            return false;
        }

        public static float GetDensity(string name)
        {
            foreach (var mat in materials)
            {
                if (mat.name == name) return mat.density;
            }
            return 1000f;
        }

        public static float GetYoungsModulus(string name)
        {
            foreach (var mat in materials)
            {
                if (mat.name == name) return mat.youngsModulus;
            }
            return 1e9f;
        }

        public static float GetPoissonsRatio(string name)
        {
            foreach (var mat in materials)
            {
                if (mat.name == name) return mat.poissonsRatio;
            }
            return 0.3f;
        }
    }
}
