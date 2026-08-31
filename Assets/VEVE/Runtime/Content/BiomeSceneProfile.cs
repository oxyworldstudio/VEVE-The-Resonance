using System;
using System.Collections.Generic;

namespace VEVE.Content
{
    /// <summary>
    /// Authored biome scene composition: physical weather baseline, lighting key,
    /// prop palette routing and terrain bias. Pure data with tolerant string keys so
    /// the content pipeline and mission drafts stay in one namespace, and apply()
    /// writes through the existing EnvironmentSimulation setters only.
    /// </summary>
    public struct BiomeSceneProfile
    {
        public string biomeKey;
        public string lightingKey;
        public string[] propPalette;
        public float temperatureC;
        public float humidity01;
        public float windSpeedMs;
        public float windDirectionDeg;
        public float fogDensityBias;
        public float alertPostureBase01;
    }

    public static class BiomeSceneProfiles
    {
        private static readonly BiomeSceneProfile[] Catalog =
        {
            new BiomeSceneProfile
            {
                biomeKey = "MEDIUM_TOWN", lightingKey = "WarmLowSun",
                propPalette = new[] { "crate", "furniture", "debris" },
                temperatureC = 22f, humidity01 = 0.45f, windSpeedMs = 2.5f, windDirectionDeg = 240f,
                fogDensityBias = 0.18f, alertPostureBase01 = 0.22f
            },
            new BiomeSceneProfile
            {
                biomeKey = "INDUSTRIAL_EAST", lightingKey = "FlatOvercast",
                propPalette = new[] { "barrel", "vehicle", "debris", "sandbag" },
                temperatureC = 12f, humidity01 = 0.72f, windSpeedMs = 4f, windDirectionDeg = 180f,
                fogDensityBias = 0.34f, alertPostureBase01 = 0.45f
            },
            new BiomeSceneProfile
            {
                biomeKey = "DESERT_CHECKPOINT", lightingKey = "BlindingNoon",
                propPalette = new[] { "sandbag", "vehicle", "foliage_sparse" },
                temperatureC = 38f, humidity01 = 0.12f, windSpeedMs = 5.5f, windDirectionDeg = 90f,
                fogDensityBias = 0.55f, alertPostureBase01 = 0.35f
            },
            new BiomeSceneProfile
            {
                biomeKey = "SUBARCTIC_COMPOUND", lightingKey = "ColdBlueLow",
                propPalette = new[] { "crate", "foliage_snow", "fence" },
                temperatureC = -14f, humidity01 = 0.65f, windSpeedMs = 7f, windDirectionDeg = 310f,
                fogDensityBias = 0.48f, alertPostureBase01 = 0.5f
            },
            new BiomeSceneProfile
            {
                biomeKey = "FOREST_VILLAGE", lightingKey = "DappledGreen",
                propPalette = new[] { "foliage", "furniture", "well", "debris" },
                temperatureC = 16f, humidity01 = 0.8f, windSpeedMs = 1.6f, windDirectionDeg = 20f,
                fogDensityBias = 0.6f, alertPostureBase01 = 0.15f
            },
        };

        public static IReadOnlyList<BiomeSceneProfile> All => Catalog;

        public static bool TryGet(string biomeKey, out BiomeSceneProfile p)
        {
            p = Catalog[0];
            if (string.IsNullOrEmpty(biomeKey)) return true;
            foreach (var b in Catalog)
            {
                if (string.Equals(b.biomeKey, biomeKey, StringComparison.OrdinalIgnoreCase))
                {
                    p = b;
                    return true;
                }
            }
            return false;
        }

        /// <summary>0..4 insert alert floor a biome profile enforces on escalation posture.</summary>
        public static bool TryAlertFloor(string biomeKey, out int alertFloor)
        {
            alertFloor = 0;
            return TryGet(biomeKey, out var p) && TryAlertFloor(p, out alertFloor);
        }

        public static bool TryAlertFloor(in BiomeSceneProfile p, out int alertFloor)
        {
            float v = p.alertPostureBase01 * 4f;
            if (v < 0f) { alertFloor = 0; return false; }
            alertFloor = v > 4f ? 4 : (int)Math.Round(v);
            return true;
        }

        /// <summary>Null-safe weather baseline application; setter clamping lives on the sim.</summary>
        public static void ApplyTo(VEVE.EnvironmentSimulation sim, in BiomeSceneProfile p)
        {
            if (sim == null) return;
            sim.Temperature = p.temperatureC;
            sim.Humidity = p.humidity01;
            sim.WindSpeed = p.windSpeedMs;
            sim.WindDirection = p.windDirectionDeg;
        }

        public static void ApplyDefault(VEVE.EnvironmentSimulation sim)
        {
            ApplyTo(sim, Catalog[0]);
        }
    }
}
