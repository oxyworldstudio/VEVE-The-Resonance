using System;
using VEVE;

namespace VEVE.RealisticPhysics
{
    /// <summary>
    /// Immutable physical profile for a surface material: Coulomb friction pair,
    /// coefficient of restitution, specific acoustic impedance (Rayl) and bulk density (kg/m³).
    /// </summary>
    public readonly struct SurfaceMaterialProfile
    {
        /// <summary>Static friction coefficient against rubber-soled footwear.</summary>
        public float StaticFriction { get; }

        /// <summary>Kinetic friction coefficient against rubber-soled footwear.</summary>
        public float KineticFriction { get; }

        /// <summary>Coefficient of restitution for blunt impacts (0 = fully inelastic).</summary>
        public float Restitution { get; }

        /// <summary>Specific acoustic impedance in Rayl (Pa·s/m), Z = ρc.</summary>
        public float AcousticImpedanceRayl { get; }

        /// <summary>Bulk density in kilograms per cubic metre.</summary>
        public float DensityKgPerM3 { get; }

        /// <summary>
        /// Creates a fully specified surface material profile.
        /// </summary>
        /// <param name="staticFriction">Static friction coefficient.</param>
        /// <param name="kineticFriction">Kinetic friction coefficient.</param>
        /// <param name="restitution">Coefficient of restitution in [0, 1].</param>
        /// <param name="acousticImpedanceRayl">Specific acoustic impedance in Rayl.</param>
        /// <param name="densityKgPerM3">Bulk density in kg/m³.</param>
        public SurfaceMaterialProfile(float staticFriction, float kineticFriction, float restitution, float acousticImpedanceRayl, float densityKgPerM3)
        {
            StaticFriction = staticFriction;
            KineticFriction = kineticFriction;
            Restitution = restitution;
            AcousticImpedanceRayl = acousticImpedanceRayl;
            DensityKgPerM3 = densityKgPerM3;
        }
    }

    /// <summary>
    /// Static registry mapping <see cref="SurfaceMaterial"/> to realistic friction, restitution,
    /// acoustic impedance and terminal density values. Density values are CODATA-consistent and
    /// match <see cref="VEVE.Realism.RealismConfig"/> where a counterpart exists
    /// (concrete 2400, steel 7850, wood 600).
    /// </summary>
    public static class PhysicsMaterialDatabase
    {
        private static readonly SurfaceMaterialProfile[] profiles = CreateProfiles();

        /// <summary>Approximate bulk density of ordinary window glass in kg/m³.</summary>
        public const float GlassDensityKgPerM3 = 2500f;

        /// <summary>Approximate bulk density of pure ice at 0 °C in kg/m³.</summary>
        public const float IceDensityKgPerM3 = 917f;

        /// <summary>Approximate effective bulk density of compacted earth/dirt in kg/m³.</summary>
        public const float DirtDensityKgPerM3 = 1600f;

        /// <summary>Approximate effective bulk density of textile/fabric layers in kg/m³.</summary>
        public const float FabricDensityKgPerM3 = 90f;

        /// <summary>
        /// Returns the realistic profile registered for a surface material. Always defined
        /// for every value of <see cref="SurfaceMaterial"/>.
        /// </summary>
        /// <param name="material">Surface material key.</param>
        /// <returns>The registered surface material profile.</returns>
        public static SurfaceMaterialProfile GetProfile(SurfaceMaterial material)
        {
            return profiles[(int)material];
        }

        /// <summary>
        /// Retrieves the registered profile for a surface material if one exists.
        /// </summary>
        /// <param name="material">Surface material key.</param>
        /// <param name="profile">The resulting profile when found.</param>
        /// <returns>Always true for a valid <see cref="SurfaceMaterial"/> value.</returns>
        public static bool TryGetProfile(SurfaceMaterial material, out SurfaceMaterialProfile profile)
        {
            int index = (int)material;
            if (index >= 0 && index < profiles.Length)
            {
                profile = profiles[index];
                return true;
            }

            profile = default;
            return false;
        }

        /// <summary>
        /// Static friction coefficient for a surface material.
        /// </summary>
        /// <param name="material">Surface material key.</param>
        /// <returns>Friction coefficient in [0, 1].</returns>
        public static float GetStaticFriction(SurfaceMaterial material) => GetProfile(material).StaticFriction;

        /// <summary>
        /// Kinetic friction coefficient for a surface material.
        /// </summary>
        /// <param name="material">Surface material key.</param>
        /// <returns>Friction coefficient in [0, 1].</returns>
        public static float GetKineticFriction(SurfaceMaterial material) => GetProfile(material).KineticFriction;

        /// <summary>
        /// Coefficient of restitution for blunt impacts against a surface material.
        /// </summary>
        /// <param name="material">Surface material key.</param>
        /// <returns>Restitution coefficient in [0, 1].</returns>
        public static float GetRestitution(SurfaceMaterial material) => GetProfile(material).Restitution;

        /// <summary>
        /// Specific acoustic impedance (Rayl) for a surface material, used by sound propagation.
        /// </summary>
        /// <param name="material">Surface material key.</param>
        /// <returns>Impedance in Pa·s/m.</returns>
        public static float GetAcousticImpedance(SurfaceMaterial material) => GetProfile(material).AcousticImpedanceRayl;

        /// <summary>
        /// Terminal bulk density (kg/m³) for a surface material.
        /// </summary>
        /// <param name="material">Surface material key.</param>
        /// <returns>Density in kg/m³.</returns>
        public static float GetDensity(SurfaceMaterial material) => GetProfile(material).DensityKgPerM3;

        /// <summary>
        /// Resolves a surface material from a free-text renderer/object name using
        /// case-insensitive substring matching. Falls back to a caller-supplied default.
        /// </summary>
        /// <param name="name">Renderer or GameObject name to classify.</param>
        /// <param name="fallback">Material returned when no keyword matches.</param>
        /// <returns>The classified surface material, or <paramref name="fallback"/>.</returns>
        public static SurfaceMaterial ClassifyByName(string name, SurfaceMaterial fallback = SurfaceMaterial.Dirt)
        {
            if (string.IsNullOrEmpty(name)) return fallback;

            string lower = name.ToLowerInvariant();
            if (lower.Contains("concrete") || lower.Contains("cement")) return SurfaceMaterial.Concrete;
            if (lower.Contains("steel") || lower.Contains("metal") || lower.Contains("iron") || lower.Contains("aluminum")) return SurfaceMaterial.Metal;
            if (lower.Contains("glass")) return SurfaceMaterial.Glass;
            if (lower.Contains("wood") || lower.Contains("timber") || lower.Contains("plywood")) return SurfaceMaterial.Wood;
            if (lower.Contains("fabric") || lower.Contains("cloth") || lower.Contains("carpet") || lower.Contains("canvas")) return SurfaceMaterial.Fabric;
            if (lower.Contains("ice")) return SurfaceMaterial.Ice;
            if (lower.Contains("dirt") || lower.Contains("soil") || lower.Contains("gravel") || lower.Contains("sand") || lower.Contains("mud") || lower.Contains("ground") || lower.Contains("terrain")) return SurfaceMaterial.Dirt;

            return fallback;
        }

        private static SurfaceMaterialProfile[] CreateProfiles()
        {
            return new SurfaceMaterialProfile[]
            {
                new SurfaceMaterialProfile(0.55f, 0.45f, 0.15f, 3.55e6f, MaterialConfig.GetDensity("Wood")),
                new SurfaceMaterialProfile(0.90f, 0.75f, 0.15f, 7.17e6f, MaterialConfig.GetDensity("Concrete")),
                new SurfaceMaterialProfile(0.60f, 0.45f, 0.30f, 4.47e7f, MaterialConfig.GetDensity("Steel")),
                new SurfaceMaterialProfile(0.70f, 0.60f, 0.45f, 1.29e7f, GlassDensityKgPerM3),
                new SurfaceMaterialProfile(0.40f, 0.30f, 0.05f, 4.9e5f, FabricDensityKgPerM3),
                new SurfaceMaterialProfile(0.80f, 0.65f, 0.08f, 3.2e6f, DirtDensityKgPerM3),
                new SurfaceMaterialProfile(0.10f, 0.05f, 0.20f, 2.99e6f, IceDensityKgPerM3)
            };
        }
    }
}
