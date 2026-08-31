using System;
using System.Collections.Generic;

namespace VEVE.Content
{
    /// <summary>
    /// Data-driven mission template for the procedural content pipeline. Objective
    /// summaries feed the mission loader; all numbers are authored, never synthesized.
    /// </summary>
    [Serializable]
    public struct MissionTemplate
    {
        public string id;
        public string title;
        /// <summary>Region key from EnvironmentContextProfile (e.g. "MEDIUM_TOWN").</summary>
        public string regionKey;
        public int parSeconds;
        public int enemySquadPairs;
        /// <summary>0..1 initial alert posture injected into CampaignEscalationModel.</summary>
        public float alertBias;
        public double intelObjectiveWeight;
        public string[] objectiveSummary;
    }

    /// <summary>Campaign pacing tracks layered on top of death modes (design doc section 5/9).</summary>
    public enum CampaignDifficulty { Regular = 0, Hardened = 1, Elite = 2 }

    /// <summary>
    /// Deterministic authoring catalog: two operations per biome (the five procedural
    /// biomes in VEVE.Procedural), stable ids, monotonic par budgets with band range.
    /// </summary>
    public static class MissionContentCatalog
    {
        private static readonly MissionTemplate[] Templates =
        {
            new MissionTemplate
            {
                id = "MEDITERRA_CACHE", title = "Cache Sweep", regionKey = "MEDIUM_TOWN",
                parSeconds = 600, enemySquadPairs = 2, alertBias = 0.15f, intelObjectiveWeight = 1.0,
                objectiveSummary = new[] { "Primary: eliminate the cache guard (x2)", "Secondary: recover documents", "Hidden: leave no civilian witness" }
            },
            new MissionTemplate
            {
                id = "MEDITERRA_PATROL", title = "Rooftop Interdiction", regionKey = "MEDIUM_TOWN",
                parSeconds = 660, enemySquadPairs = 3, alertBias = 0.35f, intelObjectiveWeight = 0.5,
                objectiveSummary = new[] { "Primary: neutralize the patrol net", "Secondary: mark the courier", "Hidden: no body left in the street" }
            },
            new MissionTemplate
            {
                id = "INDUSTRIAL_BOILER", title = "Boiler Room Breach", regionKey = "INDUSTRIAL_EAST",
                parSeconds = 720, enemySquadPairs = 3, alertBias = 0.4f, intelObjectiveWeight = 1.2,
                objectiveSummary = new[] { "Primary: destroy the ledger server", "Secondary: breach the catwalk (door)", "Hidden: keep the night shift quiet" }
            },
            new MissionTemplate
            {
                id = "INDUSTRAL_SILAGE", title = "Silage Convoy", regionKey = "INDUSTRIAL_EAST",
                parSeconds = 780, enemySquadPairs = 3, alertBias = 0.5f, intelObjectiveWeight = 0.8,
                objectiveSummary = new[] { "Primary: ambush the convoy", "Secondary: capture the driver for intel", "Hidden: no armored survivor" }
            },
            new MissionTemplate
            {
                id = "DESERT_RIDGELINE", title = "Ridgeline Overwatch", regionKey = "DESERT_CHECKPOINT",
                parSeconds = 900, enemySquadPairs = 2, alertBias = 0.25f, intelObjectiveWeight = 1.4,
                objectiveSummary = new[] { "Primary: clear the high overwatch pair", "Secondary: recover the downed drone", "Hidden: engage only beyond 300 m" }
            },
            new MissionTemplate
            {
                id = "DESERT_WELLS", title = "Wells Checkpoint", regionKey = "DESERT_CHECKPOINT",
                parSeconds = 840, enemySquadPairs = 3, alertBias = 0.55f, intelObjectiveWeight = 0.9,
                objectiveSummary = new[] { "Primary: take the checkpoint", "Secondary: seize the fuel cache", "Hidden: leave the militia conscripts alive" }
            },
            new MissionTemplate
            {
                id = "SUBARCTIC_RADIO", title = "Radio Cabin", regionKey = "SUBARCTIC_COMPOUND",
                parSeconds = 840, enemySquadPairs = 3, alertBias = 0.3f, intelObjectiveWeight = 1.5,
                objectiveSummary = new[] { "Primary: tap the uplink", "Secondary: remove the shift operator", "Hidden: exit on the south skieroot" }
            },
            new MissionTemplate
            {
                id = "SUBARCTIC_FENCELINE", title = "Perimeter Fenceline", regionKey = "SUBARCTIC_COMPOUND",
                parSeconds = 960, enemySquadPairs = 4, alertBias = 0.6f, intelObjectiveWeight = 0.7,
                objectiveSummary = new[] { "Primary: cut the fence for the armor approach", "Secondary: kill the dogs (collateral risk)", "Hidden: zero friendly losses" }
            },
            new MissionTemplate
            {
                id = "VILLAGE_ORCHARD", title = "Orchard Ambush", regionKey = "FOREST_VILLAGE",
                parSeconds = 660, enemySquadPairs = 2, alertBias = 0.2f, intelObjectiveWeight = 1.1,
                objectiveSummary = new[] { "Primary: let the technical pass, then kill the tail", "Secondary: seize the maps", "Hidden: do not fire near the wellhouse" }
            },
            new MissionTemplate
            {
                id = "VILLAGE_BELLTOWER", title = "Belltower Spotters", regionKey = "FOREST_VILLAGE",
                parSeconds = 720, enemySquadPairs = 3, alertBias = 0.45f, intelObjectiveWeight = 1.0,
                objectiveSummary = new[] { "Primary: neutralize both spotters", "Secondary: wire the church for charges", "Hidden: village stays off the network" }
            },
            // ---- W8: third operation authored per biome ----
            new MissionTemplate
            {
                id = "MEDITERRA_ROOFTOPS", title = "Rooftop Crossing", regionKey = "MEDIUM_TOWN",
                parSeconds = 660, enemySquadPairs = 3, alertBias = 0.3f, intelObjectiveWeight = 1.15,
                objectiveSummary = new[] { "Primary: clear the three-sided roof contact", "Secondary: recover the courier satchel", "Hidden: no rooftop entry witnessed" }
            },
            new MissionTemplate
            {
                id = "INDUSTRIAL_PIPE", title = "Pipeline Trench", regionKey = "INDUSTRIAL_EAST",
                parSeconds = 760, enemySquadPairs = 4, alertBias = 0.55f, intelObjectiveWeight = 1.0,
                objectiveSummary = new[] { "Primary: sabotage the pump station", "Secondary: deny the trench to the reaction force", "Hidden: leave no digital badge logs" }
            },
            new MissionTemplate
            {
                id = "DESERT_CONVOY", title = "Fuel Convoy", regionKey = "DESERT_CHECKPOINT",
                parSeconds = 900, enemySquadPairs = 4, alertBias = 0.45f, intelObjectiveWeight = 1.3,
                objectiveSummary = new[] { "Primary: disable the lead technical", "Secondary: capture the fuel manifest", "Hidden: no convoy survivors reaching the well" }
            },
            new MissionTemplate
            {
                id = "SUBARCTIC_STOKER", title = "Boiler House", regionKey = "SUBARCTIC_COMPOUND",
                parSeconds = 880, enemySquadPairs = 3, alertBias = 0.5f, intelObjectiveWeight = 1.15,
                objectiveSummary = new[] { "Primary: silence the stoker detail", "Secondary: cut the grid for the comms mast", "Hidden: compound alarm never trips" }
            },
            new MissionTemplate
            {
                id = "VILLAGE_CHURCH", title = "Church Courtyard", regionKey = "FOREST_VILLAGE",
                parSeconds = 700, enemySquadPairs = 3, alertBias = 0.35f, objectiveSummary = new[] { "Primary: clear the courtyard sentries", "Secondary: extract the priest intel", "Hidden: no shots inside the nave" },
                intelObjectiveWeight = 1.35
            }
        };

        public static MissionTemplate[] All => (MissionTemplate[])Templates.Clone();

        public static bool TryGet(string templateId, out MissionTemplate template)
        {
            foreach (MissionTemplate t in Templates)
            {
                if (string.Equals(t.id, templateId, StringComparison.OrdinalIgnoreCase))
                {
                    template = t;
                    return true;
                }
            }
            template = default;
            return false;
        }

        public static IReadOnlyList<string> Regions
        {
            get
            {
                var seen = new List<string>();
                foreach (MissionTemplate t in Templates)
                {
                    if (!seen.Contains(t.regionKey)) seen.Add(t.regionKey);
                }
                return seen;
            }
        }
    }

    /// <summary>
    /// Pure campaign tuning track (layered under death modes). Monotonic by contract:
    /// Regular &lt; Hardened &lt; Elite for skill floor, density and XP; reaction and par tighten.
    /// </summary>
    public static class CampaignDifficultyProfile
    {
        public static float AiSkillFloor(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.Regular: return 0.45f;
                case CampaignDifficulty.Hardened: return 0.60f;
                default: return 0.80f;
            }
        }

        /// <summary>Multiplier on scripted AI reaction times (lower = snappier, harder).</summary>
        public static float ReactionTimeMultiplier(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.Regular: return 1.18f;
                case CampaignDifficulty.Hardened: return 1.0f;
                default: return 0.86f;
            }
        }

        public static float PatrolDensity(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.Regular: return 0.9f;
                case CampaignDifficulty.Hardened: return 1.0f;
                default: return 1.3f;
            }
        }

        /// <summary>Scale on the authored par seconds (more forgiving on lower tracks).</summary>
        public static float ParSecondsFactor(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.Regular: return 1.25f;
                case CampaignDifficulty.Hardened: return 1.0f;
                default: return 0.85f;
            }
        }

        public static float ExperienceMultiplier(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.Regular: return 1.0f;
                case CampaignDifficulty.Hardened: return 1.25f;
                default: return 1.6f;
            }
        }
    }

    /// <summary>
    /// Deterministic mission draft: FNV-1a over (regionKey#cycle) modulo the region's
    /// candidate pool, so identical inputs always draft the identical template and a
    /// region with multiple operations cycles through them.
    /// </summary>
    public static class MissionScheduler
    {
        public static MissionTemplate Draft(string regionKey, int completedInRegion)
        {
            return Draft(regionKey, completedInRegion, null);
        }

        /// <summary>Designer pool variant (C7 asset pipeline): source defaults to the code catalog.</summary>
        public static MissionTemplate Draft(string regionKey, int completedInRegion, System.Collections.Generic.IReadOnlyList<MissionTemplate> source)
        {
            List<MissionTemplate> pool = new List<MissionTemplate>();
            System.Collections.Generic.IReadOnlyList<MissionTemplate> catalog =
                source != null && source.Count > 0 ? source : (System.Collections.Generic.IReadOnlyList<MissionTemplate>)MissionContentCatalog.All;
            for (int i = 0; i < catalog.Count; i++)
            {
                MissionTemplate t = catalog[i];
                if (string.Equals(t.regionKey, regionKey, StringComparison.OrdinalIgnoreCase))
                    pool.Add(t);
            }

            if (pool.Count == 0)
            {
                for (int i = 0; i < catalog.Count; i++) pool.Add(catalog[i]);
            }

            int cycle = completedInRegion >= 0 ? completedInRegion : 0;
            uint hash = Hash(regionKey + "#" + cycle);
            return pool[(int)(hash % (uint)pool.Count)];
        }

        public static uint Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                        h = (h ^ s[i]) * 16777619u;
                }
                return h;
            }
        }
    }
}
