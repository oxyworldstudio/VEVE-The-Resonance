using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VEVE.WeaponCustomPro
{
    /// <summary>
    /// Real-world cerakote-family coating classes. H-series is the hardened ceramic top coat
    /// (pencil hardness up to 9H, best abrasion/chemical resistance); G-series is graphite
    /// lubricant-infused (softer, lower friction, matte, higher wear-to-substrate).
    /// </summary>
    public enum CoatingClass
    {
        /// <summary>Graphite-based lubricant infusion (Trijicon-style parkerising descendant).</summary>
        G_Series,
        /// <summary>Hardened ceramic top coat (Cerakote H equivalent).</summary>
        H_Series,
        /// <summary>Type III hard-anodised / mil-spec phosphate, no ceramic over-coat.</summary>
        ConversionCoating,
    }

    /// <summary>
    /// Environmental region keys a weapon can be observed against. Weather/time-of-day bias
    /// strings map onto these in <see cref="CosmeticFinishSystem"/>.
    /// </summary>
    public static class SignatureRegion
    {
        public const string TemperateWoods = "temperate_woods";
        public const string AridDesert = "arid_desert";
        public const string SnowWinter = "snow_winter";
        public const string TropicalJungle = "tropical_jungle";
        public const string UrbanGrey = "urban_grey";
    }

    /// <summary>
    /// A cosmetic weapon finish with stealth-relevant signature data. Detection multipliers
    /// stay in the flavour band [0.95, 1.02]: cosmetics must never dominate loadout maths,
    /// but a winter white rifle in a green treeline should read against a tan FDE in scrub.
    /// </summary>
    [Serializable]
    public sealed class CosmeticFinish
    {
        public string id;
        public string displayName;
        public CoatingClass coatingClass;
        /// <summary>Pencil hardness (H units) of the cured coat — 9H for H-ceramic, 4H for phos.</summary>
        public int pencilHardness = 6;
        /// <summary>Near-IR (NATO 700-1100 nm) hemispherical reflectance factor 0..1.
        /// Note the realism inversion: black anodised/parkerised steels reflect strongly in NIR,
        /// which is why mil-spec black is NOT an IR signature win.</summary>
        [Range(0f, 1f)] public float irReflectance = 0.5f;
        /// <summary>Base visual detection multiplier (≈1 = neutral, <1 = pattern helps).</summary>
        public float visualDetectionMultiplier = 1f;
        /// <summary>Wear fraction at which the substrate starts bleeding through on edges/rails.</summary>
        [Range(0f, 1f)] public float scratchRevealThreshold = 0.5f;
        /// <summary>Per-region camouflage affinity 0..1 used by the signature model.</summary>
        [Range(0f, 1f)] public float affinityTemperateWoods = 0.4f;
        [Range(0f, 1f)] public float affinityAridDesert = 0.4f;
        [Range(0f, 1f)] public float affinitySnowWinter = 0.2f;
        [Range(0f, 1f)] public float affinityTropicalJungle = 0.4f;
        [Range(0f, 1f)] public float affinityUrbanGrey = 0.4f;

        public double AffinityFor(string regionKey)
        {
            switch (regionKey)
            {
                case SignatureRegion.TemperateWoods: return affinityTemperateWoods;
                case SignatureRegion.AridDesert: return affinityAridDesert;
                case SignatureRegion.SnowWinter: return affinitySnowWinter;
                case SignatureRegion.TropicalJungle: return affinityTropicalJungle;
                case SignatureRegion.UrbanGrey: return affinityUrbanGrey;
                default: return 0.0;
            }
        }
    }

    /// <summary>
    /// Static finish registry plus the visual-signature mapping table. Pure data + statics so
    /// the armory UI, stealth AI detection and finish-wear code all share one source.
    /// </summary>
    public static class CosmeticFinishSystem
    {
        /// <summary>Floor of the signed visual detection multiplier band.</summary>
        public const double MinSignatureMultiplier = 0.95;
        /// <summary>Ceiling of the signed visual detection multiplier band (bad finishes push past 1 just for flavour).</summary>
        public const double MaxSignatureMultiplier = 1.02;

        private static readonly CosmeticFinish[] finishes = BuildFinishes();
        private static readonly Dictionary<string, CosmeticFinish> byId = BuildIndex(finishes);

        public static IReadOnlyList<CosmeticFinish> All => finishes;
        public static int Count => finishes.Length;

        public static bool TryGet(string id, out CosmeticFinish finish)
        {
            finish = null;
            return id != null && byId.TryGetValue(id, out finish);
        }

        public static CosmeticFinish Get(string id)
        {
            if (TryGet(id, out CosmeticFinish f)) return f;
            throw new KeyNotFoundException($"No cosmetic finish with id '{id}'.");
        }

        /// <summary>
        /// Weather / scene bias keys folded onto camouflage regions: overcast and rain flatten
        /// everything toward the temperate band, dusk/night fold to urban grey contrast, and an
        /// unknown key is treated as neutral (affinity 0, multiplier = base clamped).
        /// </summary>
        public static string RegionForBiasKey(string weatherBiasKey)
        {
            switch (weatherBiasKey)
            {
                case "snow":
                case "winter": return SignatureRegion.SnowWinter;
                case "desert":
                case "arid":
                case "tropic_dry": return SignatureRegion.AridDesert;
                case "jungle":
                case "tropic": return SignatureRegion.TropicalJungle;
                case "woods":
                case "forest":
                case "green":
                case "temperate": return SignatureRegion.TemperateWoods;
                case "urban":
                case "city": return SignatureRegion.UrbanGrey;
                default: return null;
            }
        }

        /// <summary>
        /// Visual detection multiplier for (finish, weather/scene bias). = base * (1 - 0.05*affinity)
        /// clamped into [MinSignatureMultiplier, MaxSignatureMultiplier]: a perfect regional
        /// match shaves up to 5 % off detection, a mismatched pattern never helps. Monotonic in
        /// affinity by construction, so winter-white-in-snow < winter-white-in-woods always holds.
        /// </summary>
        public static double ComputeVisualSignatureMultiplier(string finishId, string weatherBiasKey)
        {
            double baseMult = 1.0;
            double affinity = 0.0;
            if (TryGet(finishId, out CosmeticFinish f))
            {
                baseMult = f.visualDetectionMultiplier;
                string region = RegionForBiasKey(weatherBiasKey);
                if (region != null) affinity = f.AffinityFor(region);
            }
            return Clamp(baseMult * (1.0 - 0.05 * affinity), MinSignatureMultiplier, MaxSignatureMultiplier);
        }

        /// <summary>
        /// Scratch/sublimate reveal factor 0..1 for a worn finish: zero below the coating's
        /// reveal threshold, ramping linearly after it (G-series and parkerised coats reveal bare
        /// steel sooner than 9H ceramics). Monotone non-decreasing in wear.
        /// </summary>
        public static double ScratchRevealFactor(string finishId, double wear01)
        {
            if (!TryGet(finishId, out CosmeticFinish f)) return Clamp(wear01, 0.0, 1.0);
            double w = Clamp(wear01, 0.0, 1.0);
            double t = Mathf.Clamp01(f.scratchRevealThreshold);
            if (w <= t) return 0.0;
            return (w - t) / Math.Max(1e-3, 1.0 - t);
        }

        private static Dictionary<string, CosmeticFinish> BuildIndex(CosmeticFinish[] items)
        {
            var d = new Dictionary<string, CosmeticFinish>(StringComparer.OrdinalIgnoreCase);
            foreach (CosmeticFinish f in items) d[f.id] = f;
            return d;
        }

        private static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);

        private static CosmeticFinish[] BuildFinishes()
        {
            return new[]
            {
                new CosmeticFinish
                {
                    id = "milspec-black", displayName = "Mil-Spec Black (Type III anodise / phos)",
                    coatingClass = CoatingClass.ConversionCoating, pencilHardness = 4,
                    // Realism note: mil-spec black is visually dark but near-IR bright.
                    irReflectance = 0.72f, visualDetectionMultiplier = 1.0f,
                    scratchRevealThreshold = 0.55f,
                    affinityTemperateWoods = 0.75f, affinityAridDesert = 0.25f, affinitySnowWinter = 0.15f,
                    affinityTropicalJungle = 0.7f, affinityUrbanGrey = 0.6f,
                },
                new CosmeticFinish
                {
                    id = "cerakote-fde", displayName = "Cerakote FDE (Fern Tan H-199R)",
                    coatingClass = CoatingClass.H_Series, pencilHardness = 9,
                    irReflectance = 0.5f, visualDetectionMultiplier = 0.98f,
                    scratchRevealThreshold = 0.7f,
                    affinityTemperateWoods = 0.45f, affinityAridDesert = 0.95f, affinitySnowWinter = 0.2f,
                    affinityTropicalJungle = 0.4f, affinityUrbanGrey = 0.5f,
                },
                new CosmeticFinish
                {
                    id = "od-green", displayName = "OD Green (Fed-Std 34092 class)",
                    coatingClass = CoatingClass.G_Series, pencilHardness = 5,
                    irReflectance = 0.62f, visualDetectionMultiplier = 0.99f,
                    scratchRevealThreshold = 0.45f,
                    affinityTemperateWoods = 0.9f, affinityAridDesert = 0.2f, affinitySnowWinter = 0.1f,
                    affinityTropicalJungle = 0.95f, affinityUrbanGrey = 0.4f,
                },
                new CosmeticFinish
                {
                    id = "multicam-tropic", displayName = "Multicam Tropic",
                    coatingClass = CoatingClass.H_Series, pencilHardness = 8,
                    irReflectance = 0.48f, visualDetectionMultiplier = 0.97f,
                    scratchRevealThreshold = 0.65f,
                    affinityTemperateWoods = 0.6f, affinityAridDesert = 0.55f, affinitySnowWinter = 0.1f,
                    affinityTropicalJungle = 0.9f, affinityUrbanGrey = 0.45f,
                },
                new CosmeticFinish
                {
                    id = "multicam-arid", displayName = "Multicam Arid",
                    coatingClass = CoatingClass.H_Series, pencilHardness = 8,
                    irReflectance = 0.52f, visualDetectionMultiplier = 0.97f,
                    scratchRevealThreshold = 0.65f,
                    affinityTemperateWoods = 0.4f, affinityAridDesert = 0.9f, affinitySnowWinter = 0.25f,
                    affinityTropicalJungle = 0.42f, affinityUrbanGrey = 0.6f,
                },
                new CosmeticFinish
                {
                    id = "multicam-black", displayName = "Multicam Black",
                    coatingClass = CoatingClass.H_Series, pencilHardness = 8,
                    irReflectance = 0.55f, visualDetectionMultiplier = 0.98f,
                    scratchRevealThreshold = 0.6f,
                    affinityTemperateWoods = 0.5f, affinityAridDesert = 0.3f, affinitySnowWinter = 0.15f,
                    affinityTropicalJungle = 0.5f, affinityUrbanGrey = 0.85f,
                },
                new CosmeticFinish
                {
                    id = "winter-white", displayName = "Winter White (over-coat)",
                    coatingClass = CoatingClass.H_Series, pencilHardness = 7,
                    irReflectance = 0.88f, visualDetectionMultiplier = 1.0f,
                    scratchRevealThreshold = 0.35f,
                    affinityTemperateWoods = 0.05f, affinityAridDesert = 0.1f, affinitySnowWinter = 0.95f,
                    affinityTropicalJungle = 0.05f, affinityUrbanGrey = 0.35f,
                },
                new CosmeticFinish
                {
                    id = "rauer-tek", displayName = "Rauer-TEX (KSK brush pattern)",
                    coatingClass = CoatingClass.G_Series, pencilHardness = 5,
                    irReflectance = 0.58f, visualDetectionMultiplier = 0.98f,
                    scratchRevealThreshold = 0.5f,
                    affinityTemperateWoods = 0.85f, affinityAridDesert = 0.4f, affinitySnowWinter = 0.2f,
                    affinityTropicalJungle = 0.6f, affinityUrbanGrey = 0.55f,
                },
                new CosmeticFinish
                {
                    id = "tungsten-cerakote", displayName = "Tungsten Cerakote (H-148)",
                    coatingClass = CoatingClass.H_Series, pencilHardness = 9,
                    irReflectance = 0.6f, visualDetectionMultiplier = 1.0f,
                    scratchRevealThreshold = 0.85f,
                    affinityTemperateWoods = 0.55f, affinityAridDesert = 0.5f, affinitySnowWinter = 0.3f,
                    affinityTropicalJungle = 0.5f, affinityUrbanGrey = 0.9f,
                },
            };
        }
    }
}
